import { render, screen } from '@testing-library/react'
import { Stepper } from './Stepper'

test('marks the current step with aria-current and completes prior steps', () => {
  render(
    <Stepper
      currentStepId="config"
      steps={[
        { id: 'outline', label: 'Outline' },
        { id: 'select', label: 'Selection' },
        { id: 'config', label: 'Config' },
        { id: 'progress', label: 'Progress' },
      ]}
    />
  )

  expect(screen.getByRole('navigation', { name: /progress/i })).toBeInTheDocument()

  // Current step uses aria-current on its container.
  const current = screen.getByText('Config').closest('[aria-current="step"]')
  expect(current).toBeInTheDocument()

  // Steps before current index should be considered completed and show checkmarks.
  // There should be at least two completed indicators (Outline + Selection).
  const checks = screen.getAllByText('✓')
  expect(checks.length).toBeGreaterThanOrEqual(2)

  // Later step indicators should remain numeric (e.g., Progress is step 4)
  expect(screen.getByText('4')).toBeInTheDocument()
})
