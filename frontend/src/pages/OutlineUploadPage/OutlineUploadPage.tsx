import { useState, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, Input, TextArea, FileUpload, Alert, Card } from '../../components';
import { useWizard } from '../../context';
import { parseOutline, uploadOutline, ApiError } from '../../services/api';
import styles from './OutlineUploadPage.module.css';

type InputMode = 'text' | 'file';

export function OutlineUploadPage() {
  const navigate = useNavigate();
  const { setOutline, outline } = useWizard();

  const [inputMode, setInputMode] = useState<InputMode>('text');
  const [examName, setExamName] = useState('');
  const [textContent, setTextContent] = useState('');
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleModeChange = (mode: InputMode) => {
    setInputMode(mode);
    setError(null);
  };

  const handleTextChange = useCallback((e: React.ChangeEvent<HTMLTextAreaElement>) => {
    setTextContent(e.target.value);
    setError(null);
  }, []);

  const handleFileSelect = useCallback((file: File | null) => {
    setSelectedFile(file);
    setError(null);
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setIsLoading(true);

    try {
      let response;

      if (inputMode === 'text') {
        if (!textContent.trim()) {
          setError('Please enter your exam outline text');
          setIsLoading(false);
          return;
        }
        response = await parseOutline({
          text: textContent,
          examName: examName || undefined,
        });
      } else {
        if (!selectedFile) {
          setError('Please select a file to upload');
          setIsLoading(false);
          return;
        }
        response = await uploadOutline(selectedFile, examName || undefined);
      }

      setOutline(response);
      navigate('/select');
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isValidationError && err.problemDetails.errors) {
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

  const canSubmit = inputMode === 'text' ? textContent.trim().length > 0 : selectedFile !== null;

  return (
    <div className={styles.container}>
      <header className={styles.header}>
        <h1 className={styles.title}>Upload Exam Outline</h1>
        <p className={styles.description}>
          Provide your exam outline to generate practice questions. You can paste text directly or
          upload a Word document or PDF.
        </p>
      </header>

      <form onSubmit={handleSubmit} className={styles.form}>
        <Card className={styles.inputCard}>
          <div className={styles.modeToggle} role="tablist" aria-label="Input method">
            <button
              type="button"
              role="tab"
              aria-selected={inputMode === 'text'}
              className={`${styles.modeButton} ${inputMode === 'text' ? styles.modeButtonActive : ''}`}
              onClick={() => handleModeChange('text')}
            >
              Paste Text
            </button>
            <button
              type="button"
              role="tab"
              aria-selected={inputMode === 'file'}
              className={`${styles.modeButton} ${inputMode === 'file' ? styles.modeButtonActive : ''}`}
              onClick={() => handleModeChange('file')}
            >
              Upload File
            </button>
          </div>

          <div className={styles.inputContent} role="tabpanel">
            {inputMode === 'text' ? (
              <TextArea
                label="Exam Outline"
                placeholder="Paste your exam outline here...

Example:
# AZ-204: Developing Solutions for Microsoft Azure

## 1. Develop Azure compute solutions (25-30%)

### 1.1 Implement containerized solutions
- Create and manage container images
- Publish an image to Azure Container Registry
- Run containers by using Azure Container Instance"
                value={textContent}
                onChange={handleTextChange}
                required
                hint="Enter your exam outline with sections and subsections"
              />
            ) : (
              <FileUpload
                label="Document File"
                accept=".docx,.pdf"
                maxSizeMB={10}
                onFileSelect={handleFileSelect}
                hint="Supported formats: .docx, .pdf (max 10 MB)"
              />
            )}
          </div>

          <Input
            label="Exam Name (Optional)"
            placeholder="e.g., AZ-204, DP-100"
            value={examName}
            onChange={(e) => setExamName(e.target.value)}
            hint="Enter the exam code or name for reference"
            maxLength={256}
          />
        </Card>

        {error && (
          <Alert variant="error" onDismiss={() => setError(null)}>
            {error}
          </Alert>
        )}

        {outline && (
          <Alert variant="success">
            Outline loaded: {outline.nodes.length} top-level section
            {outline.nodes.length !== 1 ? 's' : ''} found
            {outline.examName && ` for ${outline.examName}`}
          </Alert>
        )}

        <div className={styles.actions}>
          <Button
            type="submit"
            variant="primary"
            size="lg"
            loading={isLoading}
            disabled={!canSubmit}
          >
            {isLoading ? 'Processing...' : 'Continue to Selection'}
          </Button>
        </div>
      </form>
    </div>
  );
}
