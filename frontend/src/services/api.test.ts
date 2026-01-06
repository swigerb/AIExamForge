import {
  parseOutline,
  startGeneration,
  getExportUrl,
  downloadExport,
  triggerDownload,
  checkHealth,
  ApiError,
} from './api'

afterEach(() => {
  vi.unstubAllGlobals()
})

test('parseOutline posts to /outlines/parse', async () => {
  const fetchMock = vi.fn().mockResolvedValue({
    ok: true,
    json: () => Promise.resolve({ id: 'o1' }),
  })
  vi.stubGlobal('fetch', fetchMock)

  await parseOutline({ text: '1. Compute', examName: 'AZ-204' } as any)

  expect(fetchMock).toHaveBeenCalledWith(
    'http://localhost:5000/api/outlines/parse',
    expect.objectContaining({ method: 'POST' })
  )
})

test('startGeneration throws ApiError on non-OK response', async () => {
  const fetchMock = vi.fn().mockResolvedValue({
    ok: false,
    status: 400,
    statusText: 'Bad Request',
    json: () => Promise.resolve({ title: 'Bad', status: 400, errors: { foo: ['bar'] } }),
  })
  vi.stubGlobal('fetch', fetchMock)

  await expect(startGeneration({} as any)).rejects.toBeInstanceOf(ApiError)
})

test('getExportUrl includes query params', () => {
  const url = getExportUrl('g1', 'pdf', 'Exam')
  expect(url).toContain('/questions/g1/export?')
  expect(url).toContain('format=pdf')
  expect(url).toContain('mode=Exam')
})

test('downloadExport returns a blob for successful export', async () => {
  const blob = new Blob(['x'])
  const fetchMock = vi.fn().mockResolvedValue({
    ok: true,
    blob: () => Promise.resolve(blob),
  })
  vi.stubGlobal('fetch', fetchMock)

  const result = await downloadExport('g1', 'pdf', 'Study')
  expect(result).toBe(blob)
})

test('triggerDownload creates and clicks an anchor', () => {
  const createObjectURL = vi.fn().mockReturnValue('blob:1')
  const revokeObjectURL = vi.fn()
  vi.stubGlobal('URL', { ...URL, createObjectURL, revokeObjectURL } as any)

  const click = vi.fn()
  const appendChild = vi.spyOn(document.body, 'appendChild')
  const removeChild = vi.spyOn(document.body, 'removeChild')

  const originalCreateElement = document.createElement.bind(document)
  vi.spyOn(document, 'createElement').mockImplementation((tagName: any) => {
    const el = originalCreateElement(tagName)
    if (tagName === 'a') {
      ;(el as any).click = click
    }
    return el
  })

  triggerDownload(new Blob(['x']), 'file.pdf')

  expect(createObjectURL).toHaveBeenCalled()
  expect(appendChild).toHaveBeenCalled()
  expect(click).toHaveBeenCalled()
  expect(removeChild).toHaveBeenCalled()
  expect(revokeObjectURL).toHaveBeenCalled()
})

test('checkHealth calls /health on the API origin', async () => {
  const fetchMock = vi.fn().mockResolvedValue({
    ok: true,
    json: () => Promise.resolve({ status: 'Healthy' }),
  })
  vi.stubGlobal('fetch', fetchMock)

  await checkHealth()

  expect(fetchMock).toHaveBeenCalledWith(
    'http://localhost:5000/health',
    expect.objectContaining({ method: 'GET' })
  )
})
