/**
 * API Types - Generated from OpenAPI spec
 * These types represent the contract between frontend and backend.
 */

// ============================================================================
// Enums
// ============================================================================

export type SourceType = 'TextPaste' | 'DocxUpload' | 'PdfUpload';

export type GenerationStatus = 'Pending' | 'InProgress' | 'Completed' | 'Failed' | 'Cancelled';

export type RenderMode = 'Exam' | 'Study';

export type ExportFormat = 'json' | 'markdown' | 'docx' | 'pdf';

export type Difficulty = 'Easy' | 'Medium' | 'Hard';

export type HealthStatus = 'Healthy' | 'Degraded' | 'Unhealthy';

/**
 * Item type codes as defined in item-types.md
 */
export type ItemTypeCode =
  | 'M1'       // Multiple Choice Select One
  | 'BA'       // Best Answer
  | 'DA'       // Dynamic Answer Choice
  | 'SC'       // Sentence Correction
  | 'DOMC'     // Dynamic Option Multiple Choice
  | 'CHOOSE_N' // Multiple-Alphabetic/Choose n
  | 'AS'       // Active Screen
  | 'BL'       // Build List
  | 'MW'       // Magnetic Words
  | 'HA'       // Hot Area
  | 'DD'       // Drag and Drop
  | 'MG'       // Matching
  | 'GI'       // Graphics Interpretation
  | 'MS'       // Multi-Source Reasoning
  | 'PA'       // Two-Part Analysis
  | 'CA'       // Code/Table Analysis
  | 'AN'       // Statement Analysis
  | 'LABS'     // Task-Based Labs
  | 'SA';      // Short Answer Code

// ============================================================================
// Item Type Metadata
// ============================================================================

export interface ItemTypeInfo {
  code: ItemTypeCode;
  name: string;
  description: string;
  category: 'multiple-choice' | 'interactive' | 'build' | 'analysis' | 'lab';
}

export const ITEM_TYPES: ItemTypeInfo[] = [
  { code: 'M1', name: 'Multiple Choice (Select One)', description: 'Exactly 1 correct answer with at least 2 distractors', category: 'multiple-choice' },
  { code: 'BA', name: 'Best Answer', description: 'One answer is best; must select the BEST answer', category: 'multiple-choice' },
  { code: 'DA', name: 'Dynamic Answer Choice', description: 'Multiple correct answers; shows 1 correct + random distractors', category: 'multiple-choice' },
  { code: 'SC', name: 'Sentence Correction', description: 'Includes "No change is needed" as first choice', category: 'multiple-choice' },
  { code: 'DOMC', name: 'Dynamic Option Multiple Choice', description: 'Set-based Yes/No questions; appears last', category: 'multiple-choice' },
  { code: 'CHOOSE_N', name: 'Choose N', description: 'Select exact number of correct answers', category: 'multiple-choice' },
  { code: 'AS', name: 'Active Screen', description: 'Screenshot-based interactive configuration', category: 'interactive' },
  { code: 'BL', name: 'Build List', description: 'Order critical steps (3-5 steps)', category: 'build' },
  { code: 'MW', name: 'Magnetic Words', description: 'Code blocks ordering (6-12 blocks, 3-5 steps)', category: 'build' },
  { code: 'HA', name: 'Hot Area', description: 'Click areas in image/screenshot (4-8 areas)', category: 'interactive' },
  { code: 'DD', name: 'Drag and Drop', description: 'Drag sources to targets (3-8 sources, 2-5 targets)', category: 'interactive' },
  { code: 'MG', name: 'Matching', description: 'Match items to targets (3-6 targets)', category: 'interactive' },
  { code: 'GI', name: 'Graphics Interpretation', description: 'Graphic with dropdown answers (2-5 statements)', category: 'analysis' },
  { code: 'MS', name: 'Multi-Source Reasoning', description: 'Reference data points with Yes/No statements', category: 'analysis' },
  { code: 'PA', name: 'Two-Part Analysis', description: '2 answer columns with 3+ rows', category: 'analysis' },
  { code: 'CA', name: 'Code/Table Analysis', description: 'Predict outcomes from code/table (3-8 statements)', category: 'analysis' },
  { code: 'AN', name: 'Statement Analysis', description: 'Determine true/false for statements (3-8)', category: 'analysis' },
  { code: 'LABS', name: 'Task-Based Labs', description: 'Virtual environment tasks', category: 'lab' },
  { code: 'SA', name: 'Short Answer Code', description: 'Free-text code/command entry (≤10 lines)', category: 'lab' },
];

// ============================================================================
// Outline Types
// ============================================================================

export interface ParseOutlineRequest {
  text: string;
  examName?: string;
}

export interface OutlineNode {
  id: string;
  title: string;
  content?: string;
  level: number;
  children: OutlineNode[];
}

export interface OutlineResponse {
  id: string;
  examName?: string;
  sourceType: SourceType;
  sourceFileName?: string;
  nodes: OutlineNode[];
  createdAt: string;
}

// ============================================================================
// Generation Types
// ============================================================================

export interface DifficultyDistribution {
  easyPercent: number;
  mediumPercent: number;
  hardPercent: number;
}

export interface GenerationRequest {
  outlineId: string;
  selectedNodeIds: string[];
  questionCount: number;
  questionTypes: ItemTypeCode[];
  renderMode?: RenderMode;
  difficulty?: DifficultyDistribution;
  toneProfile?: 'concise' | 'professional' | 'exam-like';
}

export interface GenerationStartResponse {
  generationId: string;
  status: 'Pending';
  streamUrl: string;
}

export interface Question {
  id: string;
  displayId: string;
  itemTypeCode: ItemTypeCode;
  typeDisplayName: string;
  sourceNodeTitle: string;
  difficulty: Difficulty;
  content: Record<string, unknown>;
  explanation?: string;
}

export interface QuestionSet {
  id: string;
  totalQuestions: number;
  questions: Question[];
  availableExportFormats: ExportFormat[];
}

export interface GenerationResponse {
  id: string;
  status: GenerationStatus;
  progress: number;
  errorMessage?: string;
  questionSet?: QuestionSet;
  createdAt: string;
  completedAt?: string;
  insufficientContentNotes?: string[];
}

// ============================================================================
// Export Types
// ============================================================================

export interface ExportMetadata {
  examName?: string;
  generatedAt: string;
  totalQuestions: number;
  disclaimer: string;
}

export interface ExportedQuestionSet {
  metadata: ExportMetadata;
  questions: Question[];
}

// ============================================================================
// Health Types
// ============================================================================

export interface HealthChecks {
  database: HealthStatus;
  blobStorage: HealthStatus;
  mcpService: HealthStatus;
  openAiService: HealthStatus;
}

export interface HealthResponse {
  status: HealthStatus;
  checks: HealthChecks;
}

// ============================================================================
// Error Types
// ============================================================================

export interface ProblemDetails {
  type?: string;
  title: string;
  status: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
}

// ============================================================================
// SSE Event Types
// ============================================================================

export interface ProgressEvent {
  type: 'progress';
  progress: number;
  message?: string;
}

export interface QuestionEvent {
  type: 'question';
  question: Question;
  questionNumber: number;
  totalQuestions: number;
}

export interface CompleteEvent {
  type: 'complete';
  generationId: string;
}

export interface ErrorEvent {
  type: 'error';
  message: string;
  code?: string;
}

export type GenerationEvent = ProgressEvent | QuestionEvent | CompleteEvent | ErrorEvent;
