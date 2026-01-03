import { useEffect, useCallback, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, ProgressBar, Card, Alert } from '../../components';
import { useWizard } from '../../context';
import {
  subscribeToGeneration,
  getGeneration,
  ApiError,
} from '../../services/api';
import type { GenerationEvent, Question } from '../../types/api';
import styles from './GenerationProgressPage.module.css';

export function GenerationProgressPage() {
  const navigate = useNavigate();
  const {
    generationId,
    generation,
    setGeneration,
    questionCount,
  } = useWizard();

  const [progress, setProgress] = useState(0);
  const [statusMessage, setStatusMessage] = useState('Initializing...');
  const [generatedQuestions, setGeneratedQuestions] = useState<Question[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [isComplete, setIsComplete] = useState(false);
  const [insufficientContentNotes, setInsufficientContentNotes] = useState<string[]>([]);

  // Redirect if no generation started
  useEffect(() => {
    if (!generationId) {
      navigate('/config');
    }
  }, [generationId, navigate]);

  // Subscribe to SSE stream
  useEffect(() => {
    if (!generationId) return;

    const handleEvent = (event: GenerationEvent) => {
      switch (event.type) {
        case 'progress':
          setProgress(event.progress);
          if (event.message) {
            setStatusMessage(event.message);
          }
          break;

        case 'question':
          setGeneratedQuestions((prev) => [...prev, event.question]);
          setProgress(Math.round((event.questionNumber / event.totalQuestions) * 100));
          setStatusMessage(`Generated question ${event.questionNumber} of ${event.totalQuestions}`);
          break;

        case 'complete':
          setProgress(100);
          setStatusMessage('Generation complete!');
          setIsComplete(true);
          // Fetch final results
          fetchFinalResults();
          break;

        case 'error':
          setError(event.message);
          setStatusMessage('Generation failed');
          break;
      }
    };

    const handleError = (err: Error) => {
      setError(err.message);
      // Try to fetch current status as fallback
      fetchFinalResults();
    };

    const cleanup = subscribeToGeneration(generationId, handleEvent, handleError);

    return cleanup;
  }, [generationId]);

  const fetchFinalResults = useCallback(async () => {
    if (!generationId) return;

    try {
      const result = await getGeneration(generationId);
      setGeneration(result);

      if (result.status === 'Completed') {
        setIsComplete(true);
        setProgress(100);
        if (result.questionSet) {
          setGeneratedQuestions(result.questionSet.questions);
        }
        if (result.insufficientContentNotes) {
          setInsufficientContentNotes(result.insufficientContentNotes);
        }
      } else if (result.status === 'Failed') {
        setError(result.errorMessage || 'Generation failed');
      } else if (result.status === 'InProgress') {
        setProgress(result.progress);
        setStatusMessage(`Generation in progress... ${result.progress}%`);
      }
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.problemDetails.detail || err.problemDetails.title);
      }
    }
  }, [generationId, setGeneration]);

  const handleContinueToResults = () => {
    navigate('/results');
  };

  const handleStartOver = () => {
    navigate('/');
  };

  const handleRetry = () => {
    navigate('/config');
  };

  if (!generationId) {
    return null;
  }

  return (
    <div className={styles.container}>
      <header className={styles.header}>
        <h1 className={styles.title}>
          {isComplete ? 'Generation Complete!' : 'Generating Questions...'}
        </h1>
        <p className={styles.description}>{statusMessage}</p>
      </header>

      <Card className={styles.progressCard}>
        <ProgressBar
          value={progress}
          label="Generation Progress"
          size="lg"
          variant={error ? 'error' : isComplete ? 'success' : 'default'}
        />

        <div className={styles.stats}>
          <div className={styles.stat}>
            <span className={styles.statLabel}>Questions Generated</span>
            <span className={styles.statValue}>
              {generatedQuestions.length} / {questionCount}
            </span>
          </div>
          <div className={styles.stat}>
            <span className={styles.statLabel}>Status</span>
            <span className={`${styles.statValue} ${styles[generation?.status?.toLowerCase() || 'pending']}`}>
              {generation?.status || 'Pending'}
            </span>
          </div>
        </div>
      </Card>

      {generatedQuestions.length > 0 && (
        <Card title="Generated Questions Preview" className={styles.previewCard}>
          <ul className={styles.questionList} aria-label="Generated questions">
            {generatedQuestions.slice(-5).map((q) => (
              <li key={q.id} className={styles.questionItem}>
                <span className={styles.questionBadge}>{q.itemTypeCode}</span>
                <span className={styles.questionId}>{q.displayId}</span>
                <span className={styles.questionDifficulty} data-difficulty={q.difficulty.toLowerCase()}>
                  {q.difficulty}
                </span>
              </li>
            ))}
            {generatedQuestions.length > 5 && (
              <li className={styles.questionMore}>
                +{generatedQuestions.length - 5} more questions
              </li>
            )}
          </ul>
        </Card>
      )}

      {error && (
        <Alert variant="error" title="Generation Error">
          {error}
          <p className={styles.errorHelp}>
            This may be due to insufficient content from Microsoft Learn for the selected topics.
            Try selecting different sections or reducing the question count.
          </p>
        </Alert>
      )}

      {insufficientContentNotes.length > 0 && (
        <Alert variant="warning" title="Some Topics Had Limited Content">
          <p>The following topics had insufficient Microsoft Learn content for question generation:</p>
          <ul className={styles.notesList}>
            {insufficientContentNotes.map((note, index) => (
              <li key={index}>{note}</li>
            ))}
          </ul>
        </Alert>
      )}

      <div className={styles.actions}>
        {error ? (
          <>
            <Button variant="outline" onClick={handleStartOver}>
              Start Over
            </Button>
            <Button variant="primary" onClick={handleRetry}>
              Try Again
            </Button>
          </>
        ) : isComplete ? (
          <>
            <Button variant="outline" onClick={handleStartOver}>
              Start Over
            </Button>
            <Button variant="primary" size="lg" onClick={handleContinueToResults}>
              View Results & Export
            </Button>
          </>
        ) : (
          <div className={styles.loadingIndicator} role="status" aria-live="polite">
            <span className={styles.spinner} aria-hidden="true" />
            <span>Please wait while questions are being generated...</span>
          </div>
        )}
      </div>
    </div>
  );
}
