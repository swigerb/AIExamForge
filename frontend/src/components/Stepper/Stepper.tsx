import styles from './Stepper.module.css';

export interface Step {
  id: string;
  label: string;
  completed?: boolean;
}

export interface StepperProps {
  steps: Step[];
  currentStepId: string;
  className?: string;
}

export function Stepper({ steps, currentStepId, className }: StepperProps) {
  const currentIndex = steps.findIndex((s) => s.id === currentStepId);

  return (
    <nav className={`${styles.stepper} ${className || ''}`} aria-label="Progress">
      <ol className={styles.stepList}>
        {steps.map((step, index) => {
          const isCurrent = step.id === currentStepId;
          const isCompleted = step.completed || index < currentIndex;
          const isPending = index > currentIndex && !step.completed;

          return (
            <li key={step.id} className={styles.stepItem}>
              <div
                className={`${styles.step} ${isCurrent ? styles.current : ''} ${
                  isCompleted ? styles.completed : ''
                } ${isPending ? styles.pending : ''}`}
                aria-current={isCurrent ? 'step' : undefined}
              >
                <span className={styles.stepIndicator}>
                  {isCompleted ? '✓' : index + 1}
                </span>
                <span className={styles.stepLabel}>{step.label}</span>
              </div>
              {index < steps.length - 1 && (
                <div
                  className={`${styles.connector} ${isCompleted ? styles.connectorCompleted : ''}`}
                  aria-hidden="true"
                />
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
