import { render, screen } from '@testing-library/react'
import * as components from './index'

test('components barrel exports can be imported and rendered', () => {
  expect(components).toHaveProperty('Button')
  expect(components).toHaveProperty('Input')
  expect(components).toHaveProperty('TextArea')
  expect(components).toHaveProperty('FileUpload')
  expect(components).toHaveProperty('ProgressBar')
  expect(components).toHaveProperty('Select')
  expect(components).toHaveProperty('Checkbox')
  expect(components).toHaveProperty('TreeView')
  expect(components).toHaveProperty('Alert')
  expect(components).toHaveProperty('Card')
  expect(components).toHaveProperty('Stepper')

  render(
    <div>
      <components.Button>Btn</components.Button>
      <components.Input label="In" value="" onChange={() => {}} />
      <components.TextArea label="Ta" value="" onChange={() => {}} />
      <components.FileUpload onFileSelect={() => {}} />
      <components.ProgressBar value={1} max={10} />
      <components.Select value="a" onChange={() => {}} options={[{ value: 'a', label: 'A' }]} />
      <components.Checkbox label="Cb" checked={false} onChange={() => {}} />
      <components.TreeView nodes={[]} selectedIds={new Set()} onSelectionChange={() => {}} />
      <components.Alert>Hi</components.Alert>
      <components.Card>Card</components.Card>
      <components.Stepper steps={[{ id: 's1', label: 'S1' }]} currentStepId="s1" />
    </div>
  )

  expect(screen.getByText('Btn')).toBeInTheDocument()
  expect(screen.getByText('Card')).toBeInTheDocument()
})
