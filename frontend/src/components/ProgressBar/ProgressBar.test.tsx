import { render, screen } from '@testing-library/react'
import { ProgressBar } from './ProgressBar'

test('renders with a label and exposes correct progressbar attributes', () => {
  render(<ProgressBar label="Generation Progress" value={25} max={100} />)

  const bar = screen.getByRole('progressbar')
  expect(screen.getByText('Generation Progress')).toBeInTheDocument()
  expect(bar).toHaveAttribute('aria-valuenow', '25')
  expect(bar).toHaveAttribute('aria-valuemin', '0')
  expect(bar).toHaveAttribute('aria-valuemax', '100')
})

test('falls back to aria-label when no label is provided', () => {
  render(<ProgressBar value={50} max={200} />)

  const bar = screen.getByRole('progressbar')
  expect(bar).toHaveAttribute('aria-label', 'Progress: 25%')
})

test('clamps percentage between 0 and 100', () => {
  const { rerender } = render(<ProgressBar value={-10} max={100} showValue />)
  expect(screen.getByText('0%')).toBeInTheDocument()

  rerender(<ProgressBar value={999} max={100} showValue />)
  expect(screen.getByText('100%')).toBeInTheDocument()
})
