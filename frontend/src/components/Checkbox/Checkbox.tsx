import { useId } from 'react';
import styles from './Checkbox.module.css';

export interface CheckboxProps {
  label: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
  disabled?: boolean;
  indeterminate?: boolean;
  description?: string;
  className?: string;
}

export function Checkbox({
  label,
  checked,
  onChange,
  disabled = false,
  indeterminate = false,
  description,
  className,
}: CheckboxProps) {
  const id = useId();
  const descId = `${id}-desc`;

  return (
    <div className={`${styles.container} ${className || ''}`}>
      <div className={styles.checkboxWrapper}>
        <input
          type="checkbox"
          id={id}
          checked={checked}
          onChange={(e) => onChange(e.target.checked)}
          disabled={disabled}
          ref={(el) => {
            if (el) el.indeterminate = indeterminate;
          }}
          className={styles.input}
          aria-describedby={description ? descId : undefined}
        />
        <span className={styles.checkmark} aria-hidden="true">
          {indeterminate ? '−' : checked ? '✓' : ''}
        </span>
      </div>
      <div className={styles.labelWrapper}>
        <label htmlFor={id} className={styles.label}>
          {label}
        </label>
        {description && (
          <span id={descId} className={styles.description}>
            {description}
          </span>
        )}
      </div>
    </div>
  );
}
