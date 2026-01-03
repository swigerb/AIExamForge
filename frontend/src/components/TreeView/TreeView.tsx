import { useState, useCallback, useId } from 'react';
import type { OutlineNode } from '../../types/api';
import { Checkbox } from '../Checkbox';
import styles from './TreeView.module.css';

export interface TreeViewProps {
  nodes: OutlineNode[];
  selectedIds: Set<string>;
  onSelectionChange: (selectedIds: Set<string>) => void;
  label?: string;
  className?: string;
}

interface TreeNodeProps {
  node: OutlineNode;
  selectedIds: Set<string>;
  onToggle: (nodeId: string, select: boolean) => void;
  expandedIds: Set<string>;
  onExpandToggle: (nodeId: string) => void;
  level: number;
}

function getAllDescendantIds(node: OutlineNode): string[] {
  const ids: string[] = [];
  const stack: OutlineNode[] = [node];
  while (stack.length > 0) {
    const current = stack.pop()!;
    ids.push(current.id);
    stack.push(...current.children);
  }
  return ids;
}

function TreeNode({
  node,
  selectedIds,
  onToggle,
  expandedIds,
  onExpandToggle,
  level,
}: TreeNodeProps) {
  const hasChildren = node.children.length > 0;
  const isExpanded = expandedIds.has(node.id);
  const isSelected = selectedIds.has(node.id);

  // Check if some but not all descendants are selected
  const descendantIds = getAllDescendantIds(node);
  const selectedDescendants = descendantIds.filter((id) => selectedIds.has(id));
  const isIndeterminate =
    hasChildren &&
    selectedDescendants.length > 0 &&
    selectedDescendants.length < descendantIds.length;

  const handleCheckboxChange = (checked: boolean) => {
    onToggle(node.id, checked);
  };

  const handleExpandClick = () => {
    onExpandToggle(node.id);
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      if (hasChildren) {
        onExpandToggle(node.id);
      }
    }
  };

  return (
    <li
      className={styles.treeNode}
      style={{ '--level': level } as React.CSSProperties}
      role="treeitem"
      aria-expanded={hasChildren ? isExpanded : undefined}
      aria-selected={isSelected}
    >
      <div className={styles.nodeContent}>
        {hasChildren ? (
          <button
            type="button"
            className={styles.expandButton}
            onClick={handleExpandClick}
            onKeyDown={handleKeyDown}
            aria-label={isExpanded ? 'Collapse' : 'Expand'}
          >
            <span className={`${styles.expandIcon} ${isExpanded ? styles.expanded : ''}`}>
              ▶
            </span>
          </button>
        ) : (
          <span className={styles.expandSpacer} aria-hidden="true" />
        )}

        <Checkbox
          label={node.title}
          checked={isSelected}
          onChange={handleCheckboxChange}
          indeterminate={isIndeterminate}
          description={node.content ? `${node.content.substring(0, 100)}...` : undefined}
        />
      </div>

      {hasChildren && isExpanded && (
        <ul className={styles.childList} role="group">
          {node.children.map((child) => (
            <TreeNode
              key={child.id}
              node={child}
              selectedIds={selectedIds}
              onToggle={onToggle}
              expandedIds={expandedIds}
              onExpandToggle={onExpandToggle}
              level={level + 1}
            />
          ))}
        </ul>
      )}
    </li>
  );
}

export function TreeView({
  nodes,
  selectedIds,
  onSelectionChange,
  label,
  className,
}: TreeViewProps) {
  const id = useId();
  const [expandedIds, setExpandedIds] = useState<Set<string>>(() => {
    // Expand first level by default
    return new Set(nodes.map((n) => n.id));
  });

  const handleExpandToggle = useCallback((nodeId: string) => {
    setExpandedIds((prev) => {
      const next = new Set(prev);
      if (next.has(nodeId)) {
        next.delete(nodeId);
      } else {
        next.add(nodeId);
      }
      return next;
    });
  }, []);

  const handleToggle = useCallback(
    (nodeId: string, select: boolean) => {
      // Find the node
      const findNode = (nodes: OutlineNode[], id: string): OutlineNode | null => {
        for (const node of nodes) {
          if (node.id === id) return node;
          const found = findNode(node.children, id);
          if (found) return found;
        }
        return null;
      };

      const node = findNode(nodes, nodeId);
      if (!node) return;

      const descendantIds = getAllDescendantIds(node);

      const next = new Set(selectedIds);
      if (select) {
        descendantIds.forEach((id) => next.add(id));
      } else {
        descendantIds.forEach((id) => next.delete(id));
      }

      onSelectionChange(next);
    },
    [nodes, selectedIds, onSelectionChange]
  );

  const handleExpandAll = () => {
    const allIds = new Set<string>();
    const collectIds = (nodes: OutlineNode[]) => {
      nodes.forEach((node) => {
        if (node.children.length > 0) {
          allIds.add(node.id);
          collectIds(node.children);
        }
      });
    };
    collectIds(nodes);
    setExpandedIds(allIds);
  };

  const handleCollapseAll = () => {
    setExpandedIds(new Set());
  };

  const handleSelectAll = () => {
    const allIds = new Set<string>();
    const collectIds = (nodes: OutlineNode[]) => {
      nodes.forEach((node) => {
        allIds.add(node.id);
        collectIds(node.children);
      });
    };
    collectIds(nodes);
    onSelectionChange(allIds);
  };

  const handleDeselectAll = () => {
    onSelectionChange(new Set());
  };

  return (
    <div className={`${styles.container} ${className || ''}`}>
      {label && (
        <span id={`${id}-label`} className={styles.label}>
          {label}
        </span>
      )}

      <div className={styles.toolbar}>
        <button type="button" className={styles.toolbarButton} onClick={handleExpandAll}>
          Expand All
        </button>
        <button type="button" className={styles.toolbarButton} onClick={handleCollapseAll}>
          Collapse All
        </button>
        <span className={styles.toolbarDivider} aria-hidden="true" />
        <button type="button" className={styles.toolbarButton} onClick={handleSelectAll}>
          Select All
        </button>
        <button type="button" className={styles.toolbarButton} onClick={handleDeselectAll}>
          Deselect All
        </button>
      </div>

      <ul
        className={styles.tree}
        role="tree"
        aria-labelledby={label ? `${id}-label` : undefined}
      >
        {nodes.map((node) => (
          <TreeNode
            key={node.id}
            node={node}
            selectedIds={selectedIds}
            onToggle={handleToggle}
            expandedIds={expandedIds}
            onExpandToggle={handleExpandToggle}
            level={0}
          />
        ))}
      </ul>

      <div className={styles.summary} aria-live="polite">
        {selectedIds.size} section{selectedIds.size !== 1 ? 's' : ''} selected
      </div>
    </div>
  );
}
