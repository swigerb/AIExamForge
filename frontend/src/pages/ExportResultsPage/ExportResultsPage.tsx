import { useEffect, useState, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, Card, Alert, Select } from '../../components';
import { useWizard } from '../../context';
import { downloadExport, triggerDownload, getGeneration, ApiError } from '../../services/api';
import type { ExportFormat, RenderMode, Question } from '../../types/api';
import styles from './ExportResultsPage.module.css';

const EXPORT_FORMATS: { value: ExportFormat; label: string; description: string }[] = [
  { value: 'json', label: 'JSON', description: 'Machine-readable format for integration' },
  { value: 'markdown', label: 'Markdown', description: 'Plain text with formatting' },
  { value: 'docx', label: 'Word Document', description: 'Microsoft Word format (.docx)' },
  { value: 'pdf', label: 'PDF', description: 'Portable document format' },
];

export function ExportResultsPage() {
  const navigate = useNavigate();
  const { generationId, generation, setGeneration, renderMode, outline, reset, questionCount } = useWizard();

  const [selectedFormat, setSelectedFormat] = useState<ExportFormat>('pdf');
  const [selectedMode, setSelectedMode] = useState<RenderMode>(renderMode);
  const [isExporting, setIsExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);
  const [exportSuccess, setExportSuccess] = useState<string | null>(null);

  // Redirect if no generation completed
  useEffect(() => {
    if (!generationId) {
      navigate('/');
      return;
    }

    // Fetch generation if not loaded
    if (!generation || generation.status !== 'Completed') {
      getGeneration(generationId).then((result) => {
        setGeneration(result);
        if (result.status !== 'Completed') {
          navigate('/progress');
        }
      }).catch(() => {
        navigate('/');
      });
    }
  }, [generationId, generation, setGeneration, navigate]);

  const handleExport = useCallback(async () => {
    if (!generationId) return;

    setIsExporting(true);
    setExportError(null);
    setExportSuccess(null);

    try {
      const blob = await downloadExport(generationId, selectedFormat, selectedMode);

      // Generate filename
      const examName = outline?.examName || 'questions';
      const timestamp = new Date().toISOString().split('T')[0];
      const modeLabel = selectedMode.toLowerCase();
      const extension = selectedFormat === 'docx' ? 'docx' : selectedFormat;
      const filename = `${examName}-${modeLabel}-${timestamp}.${extension}`;

      triggerDownload(blob, filename);
      setExportSuccess(`Successfully exported ${filename}`);
    } catch (err) {
      if (err instanceof ApiError) {
        setExportError(err.problemDetails.detail || err.problemDetails.title);
      } else {
        setExportError('Failed to export. Please try again.');
      }
    } finally {
      setIsExporting(false);
    }
  }, [generationId, selectedFormat, selectedMode, outline]);

  const handleStartNew = () => {
    reset();
    navigate('/');
  };

  if (!generation?.questionSet) {
    return (
      <div className={styles.container}>
        <div className={styles.loading}>Loading results...</div>
      </div>
    );
  }

  const { questionSet } = generation;
  const questions = questionSet.questions;

  // Group questions by type for summary
  const questionsByType = questions.reduce(
    (acc, q) => {
      if (!acc[q.itemTypeCode]) {
        acc[q.itemTypeCode] = [];
      }
      acc[q.itemTypeCode].push(q);
      return acc;
    },
    {} as Record<string, Question[]>
  );

  // Group questions by difficulty
  const questionsByDifficulty = questions.reduce(
    (acc, q) => {
      if (!acc[q.difficulty]) {
        acc[q.difficulty] = [];
      }
      acc[q.difficulty].push(q);
      return acc;
    },
    {} as Record<string, Question[]>
  );

  return (
    <div className={styles.container}>
      <header className={styles.header}>
        <h1 className={styles.title}>Generation Results</h1>
        <p className={styles.description}>
          Successfully generated {questionSet.totalQuestions} question
          {questionSet.totalQuestions !== 1 ? 's' : ''}
          {outline?.examName && ` for ${outline.examName}`}
        </p>
      </header>

      <div className={styles.content}>
        <Card title="Summary" className={styles.summaryCard}>
          <div className={styles.summaryGrid}>
            <div className={styles.summarySection}>
              <h4 className={styles.summaryLabel}>By Type</h4>
              <ul className={styles.summaryList}>
                {Object.entries(questionsByType).map(([type, qs]) => (
                  <li key={type} className={styles.summaryItem}>
                    <span className={styles.typeBadge}>{type}</span>
                    <span>{qs.length} question{qs.length !== 1 ? 's' : ''}</span>
                  </li>
                ))}
              </ul>
            </div>

            <div className={styles.summarySection}>
              <h4 className={styles.summaryLabel}>By Difficulty</h4>
              <ul className={styles.summaryList}>
                {['Easy', 'Medium', 'Hard'].map((difficulty) => {
                  const count = questionsByDifficulty[difficulty]?.length || 0;
                  return (
                    <li key={difficulty} className={styles.summaryItem}>
                      <span className={styles.difficultyBadge} data-difficulty={difficulty.toLowerCase()}>
                        {difficulty}
                      </span>
                      <span>{count} question{count !== 1 ? 's' : ''}</span>
                    </li>
                  );
                })}
              </ul>
            </div>
          </div>
        </Card>

        <Card title="Question Preview" className={styles.previewCard}>
          <div className={styles.questionGrid}>
            {questions.slice(0, 6).map((q, index) => (
              <div key={q.id} className={styles.questionPreview}>
                <div className={styles.questionHeader}>
                  <span className={styles.questionId}>
                    Question {index + 1}/{questionCount || questionSet.totalQuestions}
                  </span>
                  <span className={styles.typeBadge}>{q.itemTypeCode}</span>
                </div>
                <p className={styles.questionSource}>{q.sourceNodeTitle}</p>
                <span className={styles.difficultyBadge} data-difficulty={q.difficulty.toLowerCase()}>
                  {q.difficulty}
                </span>
              </div>
            ))}
          </div>
          {questions.length > 6 && (
            <p className={styles.moreQuestions}>
              +{questions.length - 6} more questions in export
            </p>
          )}
        </Card>

        <Card title="Export Options" className={styles.exportCard}>
          <div className={styles.exportOptions}>
            <Select
              label="Export Format"
              value={selectedFormat}
              onChange={(e) => setSelectedFormat(e.target.value as ExportFormat)}
              options={EXPORT_FORMATS.map((f) => ({
                value: f.value,
                label: `${f.label} - ${f.description}`,
              }))}
            />

            <Select
              label="Content Mode"
              value={selectedMode}
              onChange={(e) => setSelectedMode(e.target.value as RenderMode)}
              options={[
                { value: 'Study', label: 'Study Mode - Include explanations' },
                { value: 'Exam', label: 'Exam Mode - Questions only' },
              ]}
              hint="Study mode includes rationales; Exam mode simulates real test"
            />
          </div>

          <div className={styles.formatCards}>
            {EXPORT_FORMATS.map((format) => (
              <button
                key={format.value}
                type="button"
                className={`${styles.formatCard} ${selectedFormat === format.value ? styles.formatCardSelected : ''}`}
                onClick={() => setSelectedFormat(format.value)}
                aria-pressed={selectedFormat === format.value}
              >
                <span className={styles.formatIcon}>
                  {format.value === 'json' && '{ }'}
                  {format.value === 'markdown' && '# '}
                  {format.value === 'docx' && '📄'}
                  {format.value === 'pdf' && '📕'}
                </span>
                <span className={styles.formatName}>{format.label}</span>
              </button>
            ))}
          </div>

          {exportError && (
            <Alert variant="error" onDismiss={() => setExportError(null)}>
              {exportError}
            </Alert>
          )}

          {exportSuccess && (
            <Alert variant="success" onDismiss={() => setExportSuccess(null)}>
              {exportSuccess}
            </Alert>
          )}

          <Button
            variant="primary"
            size="lg"
            fullWidth
            loading={isExporting}
            onClick={handleExport}
          >
            {isExporting ? 'Preparing Export...' : `Download as ${selectedFormat.toUpperCase()}`}
          </Button>
        </Card>
      </div>

      {generation.insufficientContentNotes && generation.insufficientContentNotes.length > 0 && (
        <Alert variant="info" title="Notes">
          <p>Some topics had limited Microsoft Learn content:</p>
          <ul className={styles.notesList}>
            {generation.insufficientContentNotes.map((note, i) => (
              <li key={i}>{note}</li>
            ))}
          </ul>
        </Alert>
      )}

      <div className={styles.actions}>
        <Button variant="outline" onClick={handleStartNew}>
          Generate New Questions
        </Button>
      </div>
    </div>
  );
}
