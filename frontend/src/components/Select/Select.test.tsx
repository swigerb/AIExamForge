import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Select } from './Select'

function SelectHarness() {
  const [value, setValue] = useState('pdf')

  return (
    <Select
      label="Export Format"
      hint="Choose an export format"
      value={value}
      onChange={(e) => setValue(e.target.value)}
      options={[
        { value: 'pdf', label: 'PDF' },
        { value: 'docx', label: 'Word Document' },
      ]}
    />
  )
}

test('renders label, hint, and options and supports selection', async () => {
  const user = userEvent.setup()

  render(<SelectHarness />)

  const select = screen.getByLabelText(/export format/i)
  expect(select).toBeInTheDocument()
  expect(screen.getByText(/choose an export format/i)).toBeInTheDocument()

  await user.selectOptions(select, 'docx')
  expect((select as HTMLSelectElement).value).toBe('docx')
})

test('shows error and applies aria-invalid/aria-describedby', () => {
  render(
    <Select
      label="Export Format"
      value=""
      onChange={() => {}}
      error="Required"
      options={[{ value: 'pdf', label: 'PDF' }]}
    />
  )

  const select = screen.getByLabelText(/export format/i)
  expect(select).toHaveAttribute('aria-invalid', 'true')
  expect(select.getAttribute('aria-describedby')).toBeTruthy()
  expect(screen.getByRole('alert')).toHaveTextContent('Required')
})
