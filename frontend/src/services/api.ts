/**
 * API Client for AIExamForge backend
 * Handles HTTP requests and Server-Sent Events for question generation
 */

import type {
  ParseOutlineRequest,
  OutlineResponse,
  GenerationRequest,
  GenerationStartResponse,
  GenerationResponse,
  GenerationEvent,
  HealthResponse,
  ProblemDetails,
  ExportFormat,
  RenderMode,
} from '../types/api';

// ============================================================================
// Configuration
// ============================================================================

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api';

function getApiOrigin(): string {
  return API_BASE_URL.endsWith('/api') ? API_BASE_URL.slice(0, -4) : API_BASE_URL;
}

// ============================================================================
// Error Classes
// ============================================================================

export class ApiError extends Error {
  readonly status: number;
  readonly problemDetails: ProblemDetails;

  constructor(
    status: number,
    problemDetails: ProblemDetails
  ) {
    super(problemDetails.detail || problemDetails.title);
    this.name = 'ApiError';
    this.status = status;
    this.problemDetails = problemDetails;
  }

  get isValidationError(): boolean {
    return this.status === 400 && !!this.problemDetails.errors;
  }

  get isRateLimited(): boolean {
    return this.status === 429;
  }

  get isUnauthorized(): boolean {
    return this.status === 401;
  }

  get isNotFound(): boolean {
    return this.status === 404;
  }
}

// ============================================================================
// HTTP Helpers
// ============================================================================

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let problemDetails: ProblemDetails;
    try {
      problemDetails = await response.json();
    } catch {
      problemDetails = {
        title: 'Request Failed',
        status: response.status,
        detail: response.statusText,
      };
    }
    throw new ApiError(response.status, problemDetails);
  }
  return response.json();
}

async function get<T>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'GET',
    headers: {
      'Accept': 'application/json',
    },
  });
  return handleResponse<T>(response);
}

async function post<T>(path: string, body: unknown): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
    },
    body: JSON.stringify(body),
  });
  return handleResponse<T>(response);
}

async function postFormData<T>(path: string, formData: FormData): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'POST',
    headers: {
      'Accept': 'application/json',
    },
    body: formData,
  });
  return handleResponse<T>(response);
}

// ============================================================================
// Outline API
// ============================================================================

/**
 * Parse a plain text outline into hierarchical structure
 */
export async function parseOutline(request: ParseOutlineRequest): Promise<OutlineResponse> {
  return post<OutlineResponse>('/outlines/parse', request);
}

/**
 * Upload a DOCX or PDF file and extract outline
 */
export async function uploadOutline(file: File, examName?: string): Promise<OutlineResponse> {
  const formData = new FormData();
  formData.append('file', file);
  if (examName) {
    formData.append('examName', examName);
  }
  return postFormData<OutlineResponse>('/outlines/upload', formData);
}

/**
 * Get an outline by ID
 */
export async function getOutline(outlineId: string): Promise<OutlineResponse> {
  return get<OutlineResponse>(`/outlines/${outlineId}`);
}

// ============================================================================
// Generation API
// ============================================================================

/**
 * Start question generation
 */
export async function startGeneration(request: GenerationRequest): Promise<GenerationStartResponse> {
  return post<GenerationStartResponse>('/generations', request);
}

/**
 * Get generation status and results
 */
export async function getGeneration(generationId: string): Promise<GenerationResponse> {
  return get<GenerationResponse>(`/generations/${generationId}`);
}

/**
 * Subscribe to generation progress via Server-Sent Events
 */
export function subscribeToGeneration(
  generationId: string,
  onEvent: (event: GenerationEvent) => void,
  onError?: (error: Error) => void
): () => void {
  const eventSource = new EventSource(`${API_BASE_URL}/generations/${generationId}/stream`);

  eventSource.addEventListener('progress', (e) => {
    const data = JSON.parse(e.data);
    onEvent({ type: 'progress', ...data });
  });

  eventSource.addEventListener('question', (e) => {
    const data = JSON.parse(e.data);
    onEvent({ type: 'question', ...data });
  });

  eventSource.addEventListener('complete', (e) => {
    const data = JSON.parse(e.data);
    onEvent({ type: 'complete', ...data });
    eventSource.close();
  });

  eventSource.addEventListener('error', (e) => {
    if (e instanceof MessageEvent && e.data) {
      const data = JSON.parse(e.data);
      onEvent({ type: 'error', ...data });
    }
    eventSource.close();
  });

  eventSource.onerror = () => {
    onError?.(new Error('Connection to generation stream lost'));
    eventSource.close();
  };

  // Return cleanup function
  return () => {
    eventSource.close();
  };
}

// ============================================================================
// Export API
// ============================================================================

/**
 * Get export URL for downloading generated questions
 */
export function getExportUrl(
  generationId: string,
  format: ExportFormat,
  renderMode: RenderMode = 'Study'
): string {
  const params = new URLSearchParams({
    format,
    renderMode,
  });
  return `${API_BASE_URL}/questions/${generationId}/export?${params}`;
}

/**
 * Download export file
 */
export async function downloadExport(
  generationId: string,
  format: ExportFormat,
  renderMode: RenderMode = 'Study'
): Promise<Blob> {
  const url = getExportUrl(generationId, format, renderMode);
  const response = await fetch(url, {
    method: 'GET',
  });

  if (!response.ok) {
    let problemDetails: ProblemDetails;
    try {
      problemDetails = await response.json();
    } catch {
      problemDetails = {
        title: 'Export Failed',
        status: response.status,
        detail: response.statusText,
      };
    }
    throw new ApiError(response.status, problemDetails);
  }

  return response.blob();
}

/**
 * Trigger file download in browser
 */
export function triggerDownload(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

// ============================================================================
// Health API
// ============================================================================

/**
 * Check API health status
 */
export async function checkHealth(): Promise<HealthResponse> {
  const response = await fetch(`${getApiOrigin()}/health`, {
    method: 'GET',
    headers: {
      'Accept': 'application/json',
    },
  });
  return handleResponse<HealthResponse>(response);
}
