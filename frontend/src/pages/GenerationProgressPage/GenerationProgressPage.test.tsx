import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { GenerationProgressPage } from './GenerationProgressPage'
import { renderWithProviders } from '../../test/testUtils'

import * as api from '../../services/api'

vi.mock('../../services/api', async () => {
  const actual = await vi.importActual<typeof import('../../services/api')>('../../services/api')
  return {
    ...actual,
    subscribeToGeneration: vi.fn(),
    getGeneration: vi.fn(),
  }
})

beforeEach(() => {
  vi.clearAllMocks()
})

test('redirects to /config when there is no generationId', async () => {
  renderWithProviders(<GenerationProgressPage />, { route: '/progress' })
  expect(await screen.findByTestId('location')).toHaveTextContent('/config')
})

test('handles completion and allows navigation to /results', async () => {
  const user = userEvent.setup()

  const completedGeneration = {
    id: 'g1',
    status: 'Completed',
    progress: 100,
    createdAt: new Date().toISOString(),
    questionSet: {
      id: 'qs1',
      totalQuestions: 1,
      availableExportFormats: ['pdf'],
      questions: [
        {
          id: 'q1',
          displayId: 'Q001',
          itemTypeCode: 'M1',
          typeDisplayName: 'Multiple Choice',
          sourceNodeTitle: 'Compute',
          difficulty: 'Easy',
          content: {},
        },
      ],
    },
  } as any

  vi.mocked(api.getGeneration).mockResolvedValue(completedGeneration)

  // Fake SSE subscription: immediately emit complete event.
  vi.mocked(api.subscribeToGeneration).mockImplementation((_id, onEvent) => {
    onEvent({ type: 'complete', generationId: 'g1' } as any)
    return () => {}
  })

  renderWithProviders(<GenerationProgressPage />, {
    route: '/progress',
    wizardInitialState: { generationId: 'g1', questionCount: 1 } as any,
  })

  expect(await screen.findByRole('heading', { name: /Generation Complete!/i })).toBeInTheDocument()

  const btn = await screen.findByRole('button', { name: /view results/i })
  await user.click(btn)

  expect(await screen.findByTestId('location')).toHaveTextContent('/results')
})

test('shows error message and provides retry actions when SSE emits an error event', async () => {
  const user = userEvent.setup()

  vi.mocked(api.subscribeToGeneration).mockImplementation((_id, onEvent) => {
    onEvent({ type: 'error', message: 'Server unavailable' } as any)
    return () => {}
  })

  renderWithProviders(<GenerationProgressPage />, {
    route: '/progress',
    wizardInitialState: { generationId: 'g1', questionCount: 3 } as any,
  })

  expect(await screen.findByRole('alert')).toHaveTextContent('Server unavailable')

  // Error state renders retry actions.
  const tryAgain = await screen.findByRole('button', { name: /try again/i })
  await user.click(tryAgain)
  expect(await screen.findByTestId('location')).toHaveTextContent('/config')
})

test('on connection loss, fetches final status and shows backend failure message', async () => {
  const user = userEvent.setup()

  vi.mocked(api.getGeneration).mockResolvedValue({
    id: 'g1',
    status: 'Failed',
    progress: 42,
    errorMessage: 'Generation failed due to stream disconnect',
    createdAt: new Date().toISOString(),
  } as any)

  vi.mocked(api.subscribeToGeneration).mockImplementation((_id, _onEvent, onError) => {
    onError?.(new Error('Connection to generation stream lost'))
    return () => {}
  })

  renderWithProviders(<GenerationProgressPage />, {
    route: '/progress',
    wizardInitialState: { generationId: 'g1', questionCount: 1 } as any,
  })

  // fetchFinalResults should replace the transient connection error with the backend failure reason.
  expect(await screen.findByRole('alert')).toHaveTextContent(
    'Generation failed due to stream disconnect'
  )

  const startOver = await screen.findByRole('button', { name: /start over/i })
  await user.click(startOver)
  expect(await screen.findByTestId('location')).toHaveTextContent('/')
})

test('on connection loss, keeps the connection error when final status fetch fails', async () => {
  vi.mocked(api.getGeneration).mockRejectedValue(
    new api.ApiError(500, { title: 'Fetch failed', status: 500, detail: 'Boom' })
  )

  vi.mocked(api.subscribeToGeneration).mockImplementation((_id, _onEvent, onError) => {
    onError?.(new Error('Connection to generation stream lost'))
    return () => {}
  })

  renderWithProviders(<GenerationProgressPage />, {
    route: '/progress',
    wizardInitialState: { generationId: 'g1', questionCount: 1 } as any,
  })

  // Since fetchFinalResults fails, the original connection error should remain visible.
  expect(await screen.findByRole('alert')).toHaveTextContent('Connection to generation stream lost')
})

test('in dev, shows MCP queries when provided on the generation', async () => {
  vi.mocked(api.getGeneration).mockResolvedValue({
    id: 'g1',
    status: 'Failed',
    progress: 0,
    errorMessage: 'Generation failed',
    createdAt: new Date().toISOString(),
    devMcpQueries: [
      { timestamp: new Date().toISOString(), outlineNodeId: 'n1', query: 'Azure Functions triggers http' },
    ],
  } as any)

  vi.mocked(api.subscribeToGeneration).mockImplementation((_id, _onEvent, onError) => {
    onError?.(new Error('Connection to generation stream lost'))
    return () => {}
  })

  renderWithProviders(<GenerationProgressPage />, {
    route: '/progress',
    wizardInitialState: { generationId: 'g1', questionCount: 1 } as any,
  })

  expect(await screen.findByRole('alert')).toHaveTextContent(/generation failed/i)
  expect(await screen.findByLabelText(/mcp queries/i)).toHaveTextContent(
    'Azure Functions triggers http'
  )
})
