import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Input } from './Input'

function InputHarness() {
  const [value, setValue] = useState('')
  return (
    <Input
      label="Exam Name (Optional)"
      hint="Enter the exam code or name"
      value={value}
      onChange={(e) => setValue(e.target.value)}
      maxLength={256}
    />
  )
}

test('renders label and hint and allows typing', async () => {
  const user = userEvent.setup()
  render(<InputHarness />)

  const input = screen.getByLabelText(/exam name/i)
  expect(screen.getByText(/enter the exam code or name/i)).toBeInTheDocument()

  await user.type(input, 'AZ-204')
  expect((input as HTMLInputElement).value).toBe('AZ-204')
})

test('renders error as alert and sets aria-invalid', () => {
  render(<Input label="Exam Name" value="" onChange={() => {}} error="Required" />)

  const input = screen.getByLabelText(/exam name/i)
  expect(input).toHaveAttribute('aria-invalid', 'true')
  expect(screen.getByRole('alert')).toHaveTextContent('Required')
})
