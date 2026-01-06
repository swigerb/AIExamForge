import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { GenerationConfigPage } from './GenerationConfigPage'
import { renderWithProviders } from '../../test/testUtils'

import * as api from '../../services/api'

vi.mock('../../services/api', async () => {
  const actual = await vi.importActual<typeof import('../../services/api')>('../../services/api')
  return {
    ...actual,
    startGeneration: vi.fn(),
  }
})

beforeEach(() => {
  vi.clearAllMocks()
})

function fakeOutline() {
  return {
    id: 'outline-1',
    examName: 'AZ-204',
    sourceType: 'TextPaste',
    createdAt: new Date().toISOString(),
    nodes: [{ id: 'n1', title: 'Compute', level: 1, children: [] }],
  } as any
}

test('redirects to /select if prerequisites are missing', async () => {
  renderWithProviders(<GenerationConfigPage />, { route: '/config' })
  expect(await screen.findByTestId('location')).toHaveTextContent('/select')
})

test('starts generation and navigates to /progress', async () => {
  const user = userEvent.setup()

  vi.mocked(api.startGeneration).mockResolvedValue({
    generationId: 'g1',
    status: 'Pending',
    streamUrl: '/api/generations/g1/stream',
  } as any)

  renderWithProviders(<GenerationConfigPage />, {
    route: '/config',
    wizardInitialState: { outline: fakeOutline(), selectedNodeIds: new Set(['n1']) } as any,
  })

  const btn = await screen.findByRole('button', { name: /generate questions/i })
  expect(btn).toBeEnabled()

  await user.click(btn)

  expect(api.startGeneration).toHaveBeenCalled()
  expect(await screen.findByTestId('location')).toHaveTextContent('/progress')
})

test('shows rate limit message when startGeneration is throttled', async () => {
  const user = userEvent.setup()

  vi.mocked(api.startGeneration).mockRejectedValue(
    new api.ApiError(429, { title: 'Too Many Requests', status: 429, detail: 'Slow down' })
  )

  renderWithProviders(<GenerationConfigPage />, {
    route: '/config',
    wizardInitialState: { outline: fakeOutline(), selectedNodeIds: new Set(['n1']) } as any,
  })

  const btn = await screen.findByRole('button', { name: /generate questions/i })
  await user.click(btn)

  expect(await screen.findByRole('alert')).toHaveTextContent(
    'Rate limit exceeded. Please wait a moment and try again.'
  )
  expect(await screen.findByTestId('location')).toHaveTextContent('/config')
})
