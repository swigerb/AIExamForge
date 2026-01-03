import { useEffect, useState, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, Input, Select, Checkbox, Card, Alert } from '../../components';
import { useWizard } from '../../context';
import { ITEM_TYPES, type ItemTypeCode, type RenderMode } from '../../types/api';
import { startGeneration, ApiError } from '../../services/api';
import styles from './GenerationConfigPage.module.css';

export function GenerationConfigPage() {
  const navigate = useNavigate();
  const {
    questionCount,
    setQuestionCount,
    questionTypes,
    setQuestionTypes,
    renderMode,
    setRenderMode,
    difficulty,
    setDifficulty,
    canProceedToConfig,
    canProceedToGeneration,
    getGenerationRequest,
    setGenerationId,
    selectedNodeIds,
  } = useWizard();

  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Redirect if prerequisites not met
  useEffect(() => {
    if (!canProceedToConfig()) {
      navigate('/select');
    }
  }, [canProceedToConfig, navigate]);

  const handleQuestionCountChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const value = parseInt(e.target.value, 10);
    if (!isNaN(value)) {
      setQuestionCount(value);
    }
  };

  const handleTypeToggle = useCallback(
    (code: ItemTypeCode) => {
      if (questionTypes.includes(code)) {
        setQuestionTypes(questionTypes.filter((t) => t !== code));
      } else {
        setQuestionTypes([...questionTypes, code]);
      }
    },
    [questionTypes, setQuestionTypes]
  );

  const handleDifficultyChange = (key: 'easyPercent' | 'mediumPercent' | 'hardPercent') => (
    e: React.ChangeEvent<HTMLInputElement>
  ) => {
    const value = parseInt(e.target.value, 10);
    if (!isNaN(value) && value >= 0 && value <= 100) {
      setDifficulty({
        ...difficulty,
        [key]: value,
      });
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    const request = getGenerationRequest();
    if (!request) {
      setError('Please complete all required fields');
      return;
    }

    setIsLoading(true);

    try {
      const response = await startGeneration(request);
      setGenerationId(response.generationId);
      navigate('/progress');
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isRateLimited) {
          setError('Rate limit exceeded. Please wait a moment and try again.');
        } else if (err.isValidationError && err.problemDetails.errors) {
          const messages = Object.values(err.problemDetails.errors).flat();
          setError(messages.join('. '));
        } else {
          setError(err.problemDetails.detail || err.problemDetails.title);
        }
      } else {
        setError('An unexpected error occurred. Please try again.');
      }
    } finally {
      setIsLoading(false);
    }
  };

  const difficultySum = difficulty.easyPercent + difficulty.mediumPercent + difficulty.hardPercent;

  // Group item types by category
  const groupedTypes = ITEM_TYPES.reduce(
    (acc, type) => {
      if (!acc[type.category]) {
        acc[type.category] = [];
      }
      acc[type.category].push(type);
      return acc;
    },
    {} as Record<string, typeof ITEM_TYPES>
  );

  const categoryLabels: Record<string, string> = {
    'multiple-choice': 'Multiple Choice',
    interactive: 'Interactive',
    build: 'Build/Order',
    analysis: 'Analysis',
    lab: 'Lab & Code',
  };

  return (
    <div className={styles.container}>
      <header className={styles.header}>
        <h1 className={styles.title}>Configure Generation</h1>
        <p className={styles.description}>
          Customize how questions are generated for your {selectedNodeIds.size} selected section
          {selectedNodeIds.size !== 1 ? 's' : ''}.
        </p>
      </header>

      <form onSubmit={handleSubmit} className={styles.form}>
        <Card title="Question Count">
          <Input
            label="Number of Questions"
            type="number"
            min={1}
            max={50}
            value={questionCount}
            onChange={handleQuestionCountChange}
            hint="Generate between 1 and 50 questions"
            required
          />
        </Card>

        <Card title="Question Types">
          <p className={styles.typeHint}>
            Select one or more question types to include. Questions will be distributed across
            selected types.
          </p>
          <div className={styles.typeCategories}>
            {Object.entries(groupedTypes).map(([category, types]) => (
              <div key={category} className={styles.typeCategory}>
                <h4 className={styles.categoryLabel}>{categoryLabels[category] || category}</h4>
                <div className={styles.typeGrid}>
                  {types.map((type) => (
                    <Checkbox
                      key={type.code}
                      label={`${type.code} - ${type.name}`}
                      checked={questionTypes.includes(type.code)}
                      onChange={() => handleTypeToggle(type.code)}
                      description={type.description}
                    />
                  ))}
                </div>
              </div>
            ))}
          </div>
          {questionTypes.length === 0 && (
            <Alert variant="warning" className={styles.typeWarning}>
              Please select at least one question type.
            </Alert>
          )}
        </Card>

        <Card title="Difficulty Distribution">
          <p className={styles.difficultyHint}>
            Set the percentage of questions at each difficulty level. Total must equal 100%.
          </p>
          <div className={styles.difficultyGrid}>
            <Input
              label="Easy %"
              type="number"
              min={0}
              max={100}
              value={difficulty.easyPercent}
              onChange={handleDifficultyChange('easyPercent')}
            />
            <Input
              label="Medium %"
              type="number"
              min={0}
              max={100}
              value={difficulty.mediumPercent}
              onChange={handleDifficultyChange('mediumPercent')}
            />
            <Input
              label="Hard %"
              type="number"
              min={0}
              max={100}
              value={difficulty.hardPercent}
              onChange={handleDifficultyChange('hardPercent')}
            />
          </div>
          <div
            className={`${styles.difficultyTotal} ${difficultySum !== 100 ? styles.totalError : ''}`}
            aria-live="polite"
          >
            Total: {difficultySum}% {difficultySum !== 100 && '(must equal 100%)'}
          </div>
        </Card>

        <Card title="Output Mode">
          <Select
            label="Render Mode"
            value={renderMode}
            onChange={(e) => setRenderMode(e.target.value as RenderMode)}
            options={[
              { value: 'Study', label: 'Study Mode - Include explanations and rationales' },
              { value: 'Exam', label: 'Exam Mode - Questions only, no explanations' },
            ]}
            hint="Study mode is recommended for learning; Exam mode simulates real test conditions"
          />
        </Card>

        {error && (
          <Alert variant="error" onDismiss={() => setError(null)}>
            {error}
          </Alert>
        )}

        <div className={styles.actions}>
          <Button variant="outline" onClick={() => navigate('/select')}>
            Back
          </Button>
          <Button
            type="submit"
            variant="primary"
            size="lg"
            loading={isLoading}
            disabled={!canProceedToGeneration() || difficultySum !== 100}
          >
            {isLoading ? 'Starting...' : 'Generate Questions'}
          </Button>
        </div>
      </form>
    </div>
  );
}
