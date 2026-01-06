import * as components from './index'

test('components barrel exports exist', () => {
  // These are runtime exports (types are erased).
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
})
