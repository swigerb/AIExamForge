import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { OutlineUploadPage } from './OutlineUploadPage'
import { renderWithProviders } from '../../test/testUtils'

import * as api from '../../services/api'

vi.mock('../../services/api', async () => {
  const actual = await vi.importActual<typeof import('../../services/api')>('../../services/api')
  return {
    ...actual,
    parseOutline: vi.fn(),
    uploadOutline: vi.fn(),
  }
})

test('switches between paste-text and upload-file modes', async () => {
  const user = userEvent.setup()
  renderWithProviders(<OutlineUploadPage />)

  expect(screen.getByText('Paste Text')).toBeInTheDocument()
  expect(screen.getByText('Upload File')).toBeInTheDocument()

  // Text mode default
  expect(screen.getByLabelText(/exam outline/i)).toBeInTheDocument()

  await user.click(screen.getByRole('tab', { name: 'Upload File' }))
  expect(screen.getByText('Document File')).toBeInTheDocument()
})

test('disables continue button when outline is empty', async () => {
  const user = userEvent.setup()
  renderWithProviders(<OutlineUploadPage />)

  const button = screen.getByRole('button', { name: /continue to selection/i })
  expect(button).toBeDisabled()

  // Clicking should not submit while disabled
  await user.click(button)
  expect(screen.queryByText(/please enter your exam outline text/i)).not.toBeInTheDocument()
})

test('submits pasted outline and navigates to /select', async () => {
  const user = userEvent.setup()

  vi.mocked(api.parseOutline).mockResolvedValue({
    id: 'o1',
    sourceType: 'TextPaste',
    nodes: [],
    createdAt: new Date().toISOString(),
  } as any)

  renderWithProviders(<OutlineUploadPage />)

  await user.type(screen.getByLabelText(/exam outline/i), '1. Compute')
  await user.click(screen.getByRole('button', { name: /continue to selection/i }))

  expect(api.parseOutline).toHaveBeenCalledWith({ text: '1. Compute', examName: undefined })
  expect(await screen.findByTestId('location')).toHaveTextContent('/select')
})

test('shows validation error from ApiError when parseOutline fails', async () => {
  const user = userEvent.setup()

  vi.mocked(api.parseOutline).mockRejectedValue(
    new api.ApiError(400, {
      title: 'Validation Failed',
      status: 400,
      errors: {
        text: ['Outline is too short'],
      },
    })
  )

  renderWithProviders(<OutlineUploadPage />)

  await user.type(screen.getByLabelText(/exam outline/i), 'x')
  await user.click(screen.getByRole('button', { name: /continue to selection/i }))

  expect(await screen.findByRole('alert')).toHaveTextContent('Outline is too short')
  expect(await screen.findByTestId('location')).toHaveTextContent('/')
})

test('shows generic error when an unexpected error occurs', async () => {
  const user = userEvent.setup()

  vi.mocked(api.parseOutline).mockRejectedValue(new Error('boom'))

  renderWithProviders(<OutlineUploadPage />)

  await user.type(screen.getByLabelText(/exam outline/i), 'x')
  await user.click(screen.getByRole('button', { name: /continue to selection/i }))

  expect(await screen.findByRole('alert')).toHaveTextContent(
    'An unexpected error occurred. Please try again.'
  )
})
