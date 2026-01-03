import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, TreeView, Card, Alert } from '../../components';
import { useWizard } from '../../context';
import styles from './SectionSelectionPage.module.css';

export function SectionSelectionPage() {
  const navigate = useNavigate();
  const {
    outline,
    selectedNodeIds,
    setSelectedNodeIds,
    canProceedToSelection,
    canProceedToConfig,
  } = useWizard();

  // Redirect if no outline loaded
  useEffect(() => {
    if (!canProceedToSelection()) {
      navigate('/');
    }
  }, [canProceedToSelection, navigate]);

  if (!outline) {
    return null;
  }

  const handleBack = () => {
    navigate('/');
  };

  const handleContinue = () => {
    if (canProceedToConfig()) {
      navigate('/config');
    }
  };

  // Count total nodes for helpful messaging
  const countNodes = (nodes: typeof outline.nodes): number => {
    return nodes.reduce((acc, node) => acc + 1 + countNodes(node.children), 0);
  };
  const totalNodes = countNodes(outline.nodes);

  return (
    <div className={styles.container}>
      <header className={styles.header}>
        <h1 className={styles.title}>Select Sections</h1>
        <p className={styles.description}>
          Choose which sections and subsections you want to generate questions for.
          {outline.examName && (
            <span className={styles.examName}> — {outline.examName}</span>
          )}
        </p>
      </header>

      <Card className={styles.contentCard}>
        <div className={styles.stats}>
          <span className={styles.statItem}>
            📚 {outline.nodes.length} main section{outline.nodes.length !== 1 ? 's' : ''}
          </span>
          <span className={styles.statItem}>
            📝 {totalNodes} total topic{totalNodes !== 1 ? 's' : ''}
          </span>
        </div>

        <TreeView
          nodes={outline.nodes}
          selectedIds={selectedNodeIds}
          onSelectionChange={setSelectedNodeIds}
          label="Exam Outline Sections"
        />
      </Card>

      {selectedNodeIds.size === 0 && (
        <Alert variant="warning">
          Please select at least one section to generate questions for.
        </Alert>
      )}

      <div className={styles.actions}>
        <Button variant="outline" onClick={handleBack}>
          Back
        </Button>
        <Button
          variant="primary"
          size="lg"
          onClick={handleContinue}
          disabled={!canProceedToConfig()}
        >
          Continue to Configuration
        </Button>
      </div>
    </div>
  );
}
