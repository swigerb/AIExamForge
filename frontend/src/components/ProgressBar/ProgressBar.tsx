import { useId } from 'react';
import styles from './ProgressBar.module.css';

export interface ProgressBarProps {
  value: number;
  max?: number;
  label?: string;
  showValue?: boolean;
  size?: 'sm' | 'md' | 'lg';
  variant?: 'default' | 'success' | 'warning' | 'error';
  className?: string;
}

export function ProgressBar({
  value,
  max = 100,
  label,
  showValue = true,
  size = 'md',
  variant = 'default',
  className,
}: ProgressBarProps) {
  const id = useId();
  const percentage = Math.min(Math.max((value / max) * 100, 0), 100);

  return (
    <div className={`${styles.container} ${className || ''}`}>
      {(label || showValue) && (
        <div className={styles.header}>
          {label && (
            <label id={`${id}-label`} className={styles.label}>
              {label}
            </label>
          )}
          {showValue && (
            <span className={styles.value} aria-hidden="true">
              {Math.round(percentage)}%
            </span>
          )}
        </div>
      )}
      <div
        role="progressbar"
        aria-valuenow={value}
        aria-valuemin={0}
        aria-valuemax={max}
        aria-labelledby={label ? `${id}-label` : undefined}
        aria-label={!label ? `Progress: ${Math.round(percentage)}%` : undefined}
        className={`${styles.track} ${styles[size]}`}
      >
        <div
          className={`${styles.fill} ${styles[variant]}`}
          style={{ width: `${percentage}%` }}
        />
      </div>
    </div>
  );
}
