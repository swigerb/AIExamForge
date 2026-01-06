import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ExportResultsPage } from './ExportResultsPage'
import { renderWithProviders, WizardStateInitializer } from '../../test/testUtils'

import * as api from '../../services/api'

vi.mock('../../services/api', async () => {
  const actual = await vi.importActual<typeof import('../../services/api')>('../../services/api')
  return {
    ...actual,
    downloadExport: vi.fn(),
    triggerDownload: vi.fn(),
    getGeneration: vi.fn(),
  }
})

beforeEach(() => {
  vi.clearAllMocks()
})

test('renders results summary when generation is completed', () => {
  const generation = {
    id: 'g1',
    status: 'Completed',
    progress: 100,
    createdAt: new Date().toISOString(),
    questionSet: {
      id: 'qs1',
      totalQuestions: 2,
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
        {
          id: 'q2',
          displayId: 'Q002',
          itemTypeCode: 'M1',
          typeDisplayName: 'Multiple Choice',
          sourceNodeTitle: 'Compute',
          difficulty: 'Medium',
          content: {},
        },
      ],
    },
  } as any

  const outline = {
    id: 'o1',
    examName: 'AZ-204',
    sourceType: 'TextPaste',
    createdAt: new Date().toISOString(),
    nodes: [],
  } as any

  renderWithProviders(
    <>
      <WizardStateInitializer generationId={'g1'} generation={generation} outline={outline} />
      <ExportResultsPage />
    </>,
    { route: '/results' }
  )

  expect(screen.getByText('Generation Results')).toBeInTheDocument()
  expect(screen.getByText(/Successfully generated 2 questions/i)).toBeInTheDocument()
})

test('downloads export and shows success message', async () => {
  const user = userEvent.setup()

  const generation = {
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

  const outline = {
    id: 'o1',
    examName: 'AZ-204',
    sourceType: 'TextPaste',
    createdAt: new Date().toISOString(),
    nodes: [],
  } as any

  vi.mocked(api.downloadExport).mockResolvedValue(new Blob(['x'], { type: 'application/pdf' }))

  renderWithProviders(
    <>
      <WizardStateInitializer generationId={'g1'} generation={generation} outline={outline} />
      <ExportResultsPage />
    </>,
    { route: '/results' }
  )

  await user.click(screen.getByRole('button', { name: /download as pdf/i }))

  expect(api.downloadExport).toHaveBeenCalledWith('g1', 'pdf', 'Study')
  expect(api.triggerDownload).toHaveBeenCalled()
  expect(await screen.findByText(/Successfully exported/i)).toBeInTheDocument()
})

test('shows error message when export download fails', async () => {
  const user = userEvent.setup()

  const generation = {
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

  const outline = {
    id: 'o1',
    examName: 'AZ-204',
    sourceType: 'TextPaste',
    createdAt: new Date().toISOString(),
    nodes: [],
  } as any

  vi.mocked(api.downloadExport).mockRejectedValue(
    new api.ApiError(500, { title: 'Export Failed', status: 500, detail: 'Server unavailable' })
  )

  renderWithProviders(
    <>
      <WizardStateInitializer generationId={'g1'} generation={generation} outline={outline} />
      <ExportResultsPage />
    </>,
    { route: '/results' }
  )

  await user.click(screen.getByRole('button', { name: /download as pdf/i }))

  expect(await screen.findByRole('alert')).toHaveTextContent('Server unavailable')
  expect(api.triggerDownload).not.toHaveBeenCalled()
})
