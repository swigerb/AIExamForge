import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Alert } from './Alert'

test('renders as role=alert and shows title + message', () => {
  render(
    <Alert variant="success" title="Done">
      Saved successfully
    </Alert>
  )

  expect(screen.getByRole('alert')).toBeInTheDocument()
  expect(screen.getByText('Done')).toBeInTheDocument()
  expect(screen.getByText('Saved successfully')).toBeInTheDocument()
})

test('calls onDismiss when dismiss button is clicked', async () => {
  const user = userEvent.setup()
  const onDismiss = vi.fn()

  render(
    <Alert variant="info" onDismiss={onDismiss}>
      Hello
    </Alert>
  )

  await user.click(screen.getByRole('button', { name: /dismiss alert/i }))
  expect(onDismiss).toHaveBeenCalledTimes(1)
})
