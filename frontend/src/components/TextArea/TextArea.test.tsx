import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { TextArea } from './TextArea'

function TextAreaHarness() {
  const [value, setValue] = useState('')
  return (
    <TextArea
      label="Exam Outline"
      hint="Enter your exam outline"
      value={value}
      onChange={(e) => setValue(e.target.value)}
      required
    />
  )
}

test('renders label (including required) and allows typing', async () => {
  const user = userEvent.setup()
  render(<TextAreaHarness />)

  const area = screen.getByLabelText(/exam outline/i)
  expect(screen.getByText(/enter your exam outline/i)).toBeInTheDocument()

  await user.type(area, '1. Compute')
  expect((area as HTMLTextAreaElement).value).toBe('1. Compute')
})

test('renders error as alert and sets aria-invalid', () => {
  render(<TextArea label="Exam Outline" value="" onChange={() => {}} error="Required" />)

  const area = screen.getByLabelText(/exam outline/i)
  expect(area).toHaveAttribute('aria-invalid', 'true')
  expect(screen.getByRole('alert')).toHaveTextContent('Required')
})
