import styles from './Card.module.css';

export interface CardProps {
  children: React.ReactNode;
  title?: string;
  className?: string;
  padding?: 'none' | 'sm' | 'md' | 'lg';
}

export function Card({ children, title, className, padding = 'md' }: CardProps) {
  return (
    <div className={`${styles.card} ${styles[`padding-${padding}`]} ${className || ''}`}>
      {title && <h3 className={styles.title}>{title}</h3>}
      {children}
    </div>
  );
}
