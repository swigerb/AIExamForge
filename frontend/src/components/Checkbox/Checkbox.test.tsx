import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Checkbox } from './Checkbox'

function CheckboxHarness({ indeterminate = false, description }: { indeterminate?: boolean; description?: string }) {
  const [checked, setChecked] = useState(false)
  return (
    <Checkbox
      label="Select section"
      checked={checked}
      indeterminate={indeterminate}
      description={description}
      onChange={setChecked}
    />
  )
}

test('toggles checked state when clicked', async () => {
  const user = userEvent.setup()
  render(<CheckboxHarness />)

  const box = screen.getByLabelText('Select section')
  expect(box).not.toBeChecked()

  await user.click(box)
  expect(box).toBeChecked()
})

test('sets indeterminate property when indeterminate is true', () => {
  render(<CheckboxHarness indeterminate />)
  const box = screen.getByLabelText('Select section') as HTMLInputElement
  expect(box.indeterminate).toBe(true)
  expect(screen.getByText('−')).toBeInTheDocument()
})

test('wires description to aria-describedby', () => {
  render(<CheckboxHarness description="Some details" />)
  const box = screen.getByLabelText('Select section')
  expect(box.getAttribute('aria-describedby')).toBeTruthy()
  expect(screen.getByText('Some details')).toBeInTheDocument()
})
