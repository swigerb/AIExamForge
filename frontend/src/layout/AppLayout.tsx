import { Outlet, useLocation } from 'react-router-dom';
import { Stepper } from '../components';
import type { Step } from '../components/Stepper';
import styles from './AppLayout.module.css';

const STEPS: Step[] = [
  { id: 'upload', label: 'Upload Outline' },
  { id: 'select', label: 'Select Sections' },
  { id: 'config', label: 'Configure' },
  { id: 'progress', label: 'Generate' },
  { id: 'results', label: 'Export' },
];

const PATH_TO_STEP: Record<string, string> = {
  '/': 'upload',
  '/select': 'select',
  '/config': 'config',
  '/progress': 'progress',
  '/results': 'results',
};

export function AppLayout() {
  const location = useLocation();
  const currentStepId = PATH_TO_STEP[location.pathname] || 'upload';

  // Mark previous steps as completed
  const stepsWithCompletion = STEPS.map((step, index) => {
    const currentIndex = STEPS.findIndex((s) => s.id === currentStepId);
    return {
      ...step,
      completed: index < currentIndex,
    };
  });

  return (
    <div className={styles.layout}>
      <header className={styles.header}>
        <div className={styles.headerContent}>
          <a href="/" className={styles.logo} aria-label="AI Exam Forge Home">
            <span className={styles.logoText}>AI Exam Forge</span>
          </a>
          <nav className={styles.stepperNav} aria-label="Generation progress">
            <Stepper steps={stepsWithCompletion} currentStepId={currentStepId} />
          </nav>
        </div>
      </header>

      <main className={styles.main} id="main-content">
        <Outlet />
      </main>

      <footer className={styles.footer}>
        <div className={styles.footerContent}>
          <p className={styles.disclaimer}>
            <strong>Disclaimer:</strong> Questions are generated from Microsoft Learn content for
            practice purposes only. This is not an official Microsoft product.
          </p>
          <p className={styles.copyright}>
            © {new Date().getFullYear()} AIExamForge. All questions derived from Microsoft Learn.
          </p>
        </div>
      </footer>
    </div>
  );
}
