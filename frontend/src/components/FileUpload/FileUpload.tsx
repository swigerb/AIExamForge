import { useCallback, useRef, useState, useId } from 'react';
import type { DragEvent, ChangeEvent } from 'react';
import styles from './FileUpload.module.css';

export interface FileUploadProps {
  label?: string;
  accept?: string;
  error?: string;
  hint?: string;
  maxSizeMB?: number;
  onFileSelect: (file: File | null) => void;
  disabled?: boolean;
  className?: string;
}

export function FileUpload({
  label,
  accept = '.docx,.pdf',
  error,
  hint,
  maxSizeMB = 10,
  onFileSelect,
  disabled = false,
  className,
}: FileUploadProps) {
  const id = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [localError, setLocalError] = useState<string | null>(null);

  const validateFile = useCallback(
    (file: File): string | null => {
      const maxSizeBytes = maxSizeMB * 1024 * 1024;
      if (file.size > maxSizeBytes) {
        return `File size exceeds ${maxSizeMB} MB limit`;
      }

      const acceptedTypes = accept.split(',').map((t) => t.trim().toLowerCase());
      const fileExtension = `.${file.name.split('.').pop()?.toLowerCase()}`;
      const isAccepted = acceptedTypes.some(
        (type) => type === fileExtension || (type.includes('/') && file.type.match(type))
      );

      if (!isAccepted) {
        return `File type not accepted. Please upload: ${accept}`;
      }

      return null;
    },
    [accept, maxSizeMB]
  );

  const handleFile = useCallback(
    (file: File | null) => {
      if (!file) {
        setSelectedFile(null);
        setLocalError(null);
        onFileSelect(null);
        return;
      }

      const validationError = validateFile(file);
      if (validationError) {
        setLocalError(validationError);
        setSelectedFile(null);
        onFileSelect(null);
        return;
      }

      setLocalError(null);
      setSelectedFile(file);
      onFileSelect(file);
    },
    [validateFile, onFileSelect]
  );

  const handleDragOver = useCallback((e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(true);
  }, []);

  const handleDragLeave = useCallback((e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);
  }, []);

  const handleDrop = useCallback(
    (e: DragEvent<HTMLDivElement>) => {
      e.preventDefault();
      e.stopPropagation();
      setIsDragging(false);

      if (disabled) return;

      const file = e.dataTransfer.files[0];
      handleFile(file || null);
    },
    [disabled, handleFile]
  );

  const handleInputChange = useCallback(
    (e: ChangeEvent<HTMLInputElement>) => {
      const file = e.target.files?.[0] || null;
      handleFile(file);
    },
    [handleFile]
  );

  const handleClick = useCallback(() => {
    if (!disabled) {
      inputRef.current?.click();
    }
  }, [disabled]);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        handleClick();
      }
    },
    [handleClick]
  );

  const handleClear = useCallback(
    (e: React.MouseEvent) => {
      e.stopPropagation();
      if (inputRef.current) {
        inputRef.current.value = '';
      }
      handleFile(null);
    },
    [handleFile]
  );

  const displayError = error || localError;

  return (
    <div className={`${styles.container} ${className || ''}`}>
      {label && (
        <span className={styles.label}>
          {label}
        </span>
      )}

      <div
        role="button"
        tabIndex={disabled ? -1 : 0}
        className={`${styles.dropzone} ${isDragging ? styles.dragging : ''} ${
          displayError ? styles.error : ''
        } ${disabled ? styles.disabled : ''}`}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
        onClick={handleClick}
        onKeyDown={handleKeyDown}
        aria-label={selectedFile ? `Selected file: ${selectedFile.name}` : 'Upload file'}
        aria-describedby={displayError ? `${id}-error` : hint ? `${id}-hint` : undefined}
      >
        <input
          ref={inputRef}
          type="file"
          id={id}
          accept={accept}
          onChange={handleInputChange}
          disabled={disabled}
          className={styles.input}
          tabIndex={-1}
          aria-hidden="true"
        />

        {selectedFile ? (
          <div className={styles.selectedFile}>
            <span className={styles.fileIcon} aria-hidden="true">
              📄
            </span>
            <div className={styles.fileInfo}>
              <span className={styles.fileName}>{selectedFile.name}</span>
              <span className={styles.fileSize}>
                {(selectedFile.size / 1024).toFixed(1)} KB
              </span>
            </div>
            <button
              type="button"
              className={styles.clearButton}
              onClick={handleClear}
              aria-label="Remove file"
              disabled={disabled}
            >
              ✕
            </button>
          </div>
        ) : (
          <div className={styles.placeholder}>
            <span className={styles.uploadIcon} aria-hidden="true">
              📁
            </span>
            <span className={styles.uploadText}>
              <strong>Click to upload</strong> or drag and drop
            </span>
            <span className={styles.uploadHint}>
              {accept} (max {maxSizeMB} MB)
            </span>
          </div>
        )}
      </div>

      {hint && !displayError && (
        <p id={`${id}-hint`} className={styles.hint}>
          {hint}
        </p>
      )}
      {displayError && (
        <p id={`${id}-error`} className={styles.errorText} role="alert">
          {displayError}
        </p>
      )}
    </div>
  );
}
