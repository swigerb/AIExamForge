import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Button } from './Button'

test('renders children and forwards disabled', () => {
  render(<Button disabled>Click me</Button>)
  const btn = screen.getByRole('button', { name: 'Click me' })
  expect(btn).toBeDisabled()
  expect(btn).toHaveAttribute('aria-busy', 'false')
})

test('loading sets aria-busy and disables button', () => {
  render(<Button loading>Save</Button>)
  const btn = screen.getByRole('button', { name: 'Save' })
  expect(btn).toBeDisabled()
  expect(btn).toHaveAttribute('aria-busy', 'true')
})

test('onClick fires when enabled', async () => {
  const user = userEvent.setup()
  const onClick = vi.fn()
  render(<Button onClick={onClick}>Run</Button>)

  await user.click(screen.getByRole('button', { name: 'Run' }))
  expect(onClick).toHaveBeenCalledTimes(1)
})

test('onClick does not fire when loading', async () => {
  const user = userEvent.setup()
  const onClick = vi.fn()
  render(
    <Button loading onClick={onClick}>
      Run
    </Button>
  )

  await user.click(screen.getByRole('button', { name: 'Run' }))
  expect(onClick).toHaveBeenCalledTimes(0)
})
