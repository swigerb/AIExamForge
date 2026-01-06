import { createContext, useContext, useState, useCallback } from 'react';
import type { ReactNode } from 'react';
import type {
  OutlineResponse,
  GenerationRequest,
  GenerationResponse,
  ItemTypeCode,
  RenderMode,
  DifficultyDistribution,
} from '../types/api';

// ============================================================================
// State Types
// ============================================================================

export interface WizardState {
  // Step 1: Outline
  outline: OutlineResponse | null;
  
  // Step 2: Selection
  selectedNodeIds: Set<string>;
  
  // Step 3: Configuration
  questionCount: number;
  questionTypes: ItemTypeCode[];
  renderMode: RenderMode;
  difficulty: DifficultyDistribution;
  
  // Step 4: Generation
  generationId: string | null;
  generation: GenerationResponse | null;
}

interface WizardContextValue extends WizardState {
  // Setters
  setOutline: (outline: OutlineResponse | null) => void;
  setSelectedNodeIds: (ids: Set<string>) => void;
  setQuestionCount: (count: number) => void;
  setQuestionTypes: (types: ItemTypeCode[]) => void;
  setRenderMode: (mode: RenderMode) => void;
  setDifficulty: (dist: DifficultyDistribution) => void;
  setGenerationId: (id: string | null) => void;
  setGeneration: (gen: GenerationResponse | null) => void;
  
  // Helpers
  getGenerationRequest: () => GenerationRequest | null;
  reset: () => void;
  canProceedToSelection: () => boolean;
  canProceedToConfig: () => boolean;
  canProceedToGeneration: () => boolean;
}

// ============================================================================
// Default State
// ============================================================================

const defaultState: WizardState = {
  outline: null,
  selectedNodeIds: new Set(),
  questionCount: 10,
  questionTypes: ['M1'],
  renderMode: 'Study',
  difficulty: {
    easyPercent: 30,
    mediumPercent: 50,
    hardPercent: 20,
  },
  generationId: null,
  generation: null,
};

// ============================================================================
// Context
// ============================================================================

const WizardContext = createContext<WizardContextValue | null>(null);

export function useWizard(): WizardContextValue {
  const context = useContext(WizardContext);
  if (!context) {
    throw new Error('useWizard must be used within WizardProvider');
  }
  return context;
}

// ============================================================================
// Provider
// ============================================================================

interface WizardProviderProps {
  children: ReactNode;
  initialState?: Partial<WizardState>;
}

export function WizardProvider({ children, initialState }: WizardProviderProps) {
  const initialSelectedNodeIds =
    initialState?.selectedNodeIds instanceof Set
      ? new Set(initialState.selectedNodeIds)
      : defaultState.selectedNodeIds;

  const [outline, setOutlineState] = useState<OutlineResponse | null>(
    initialState?.outline ?? defaultState.outline
  );
  const [selectedNodeIds, setSelectedNodeIdsState] = useState<Set<string>>(
    initialSelectedNodeIds
  );
  const [questionCount, setQuestionCountState] = useState(
    initialState?.questionCount ?? defaultState.questionCount
  );
  const [questionTypes, setQuestionTypesState] = useState<ItemTypeCode[]>(
    initialState?.questionTypes ?? defaultState.questionTypes
  );
  const [renderMode, setRenderModeState] = useState<RenderMode>(
    initialState?.renderMode ?? defaultState.renderMode
  );
  const [difficulty, setDifficultyState] = useState<DifficultyDistribution>(
    initialState?.difficulty ?? defaultState.difficulty
  );
  const [generationId, setGenerationIdState] = useState<string | null>(
    initialState?.generationId ?? defaultState.generationId
  );
  const [generation, setGenerationState] = useState<GenerationResponse | null>(
    initialState?.generation ?? defaultState.generation
  );

  const setOutline = useCallback((o: OutlineResponse | null) => {
    setOutlineState(o);
    // Reset downstream state when outline changes
    setSelectedNodeIdsState(new Set());
    setGenerationIdState(null);
    setGenerationState(null);
  }, []);

  const setSelectedNodeIds = useCallback((ids: Set<string>) => {
    setSelectedNodeIdsState(ids);
  }, []);

  const setQuestionCount = useCallback((count: number) => {
    setQuestionCountState(Math.min(Math.max(count, 1), 50));
  }, []);

  const setQuestionTypes = useCallback((types: ItemTypeCode[]) => {
    setQuestionTypesState(types);
  }, []);

  const setRenderMode = useCallback((mode: RenderMode) => {
    setRenderModeState(mode);
  }, []);

  const setDifficulty = useCallback((dist: DifficultyDistribution) => {
    // Ensure percentages sum to 100
    const total = dist.easyPercent + dist.mediumPercent + dist.hardPercent;
    if (total !== 100) {
      // Normalize
      const factor = 100 / total;
      dist = {
        easyPercent: Math.round(dist.easyPercent * factor),
        mediumPercent: Math.round(dist.mediumPercent * factor),
        hardPercent: Math.round(dist.hardPercent * factor),
      };
      // Fix rounding errors
      const newTotal = dist.easyPercent + dist.mediumPercent + dist.hardPercent;
      if (newTotal !== 100) {
        dist.mediumPercent += 100 - newTotal;
      }
    }
    setDifficultyState(dist);
  }, []);

  const setGenerationId = useCallback((id: string | null) => {
    setGenerationIdState(id);
    if (!id) {
      setGenerationState(null);
    }
  }, []);

  const setGeneration = useCallback((gen: GenerationResponse | null) => {
    setGenerationState(gen);
  }, []);

  const getGenerationRequest = useCallback((): GenerationRequest | null => {
    if (!outline || selectedNodeIds.size === 0 || questionTypes.length === 0) {
      return null;
    }

    return {
      outlineId: outline.id,
      selectedNodeIds: Array.from(selectedNodeIds),
      questionCount,
      questionTypes,
      renderMode,
      difficulty,
    };
  }, [outline, selectedNodeIds, questionCount, questionTypes, renderMode, difficulty]);

  const reset = useCallback(() => {
    setOutlineState(null);
    setSelectedNodeIdsState(new Set());
    setQuestionCountState(defaultState.questionCount);
    setQuestionTypesState(defaultState.questionTypes);
    setRenderModeState(defaultState.renderMode);
    setDifficultyState(defaultState.difficulty);
    setGenerationIdState(null);
    setGenerationState(null);
  }, []);

  const canProceedToSelection = useCallback(() => {
    return outline !== null && outline.nodes.length > 0;
  }, [outline]);

  const canProceedToConfig = useCallback(() => {
    return canProceedToSelection() && selectedNodeIds.size > 0;
  }, [canProceedToSelection, selectedNodeIds]);

  const canProceedToGeneration = useCallback(() => {
    return canProceedToConfig() && questionTypes.length > 0 && questionCount > 0;
  }, [canProceedToConfig, questionTypes, questionCount]);

  const value: WizardContextValue = {
    outline,
    selectedNodeIds,
    questionCount,
    questionTypes,
    renderMode,
    difficulty,
    generationId,
    generation,
    setOutline,
    setSelectedNodeIds,
    setQuestionCount,
    setQuestionTypes,
    setRenderMode,
    setDifficulty,
    setGenerationId,
    setGeneration,
    getGenerationRequest,
    reset,
    canProceedToSelection,
    canProceedToConfig,
    canProceedToGeneration,
  };

  return <WizardContext.Provider value={value}>{children}</WizardContext.Provider>;
}
