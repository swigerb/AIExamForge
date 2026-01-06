import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { FileUpload } from './FileUpload'

function getFileInput(): HTMLInputElement {
  const input = document.querySelector('input[type="file"]')
  if (!input) throw new Error('File input not found')
  return input as HTMLInputElement
}

test('accepts a valid file and allows clearing it', async () => {
  const user = userEvent.setup()
  const onFileSelect = vi.fn()

  render(<FileUpload label="Document File" accept=".pdf" onFileSelect={onFileSelect} />)

  expect(screen.getByRole('button', { name: /upload file/i })).toBeInTheDocument()

  const file = new File(['hello'], 'outline.pdf', { type: 'application/pdf' })
  await user.upload(getFileInput(), file)

  expect(onFileSelect).toHaveBeenCalledWith(file)
  expect(screen.getByRole('button', { name: /selected file: outline\.pdf/i })).toBeInTheDocument()
  expect(screen.getByText('outline.pdf')).toBeInTheDocument()

  await user.click(screen.getByRole('button', { name: /remove file/i }))
  expect(onFileSelect).toHaveBeenLastCalledWith(null)
  expect(screen.getByRole('button', { name: /upload file/i })).toBeInTheDocument()
})

test('shows validation error for unacceptable file type', async () => {
  const onFileSelect = vi.fn()

  render(<FileUpload accept=".pdf" onFileSelect={onFileSelect} />)

  const file = new File(['hello'], 'outline.txt', { type: 'text/plain' })

  // userEvent.upload may be blocked by input.accept filtering; drop bypasses that.
  fireEvent.drop(screen.getByRole('button', { name: /upload file/i }), {
    dataTransfer: { files: [file] },
  })

  expect(onFileSelect).toHaveBeenCalledWith(null)
  expect(screen.getByRole('alert')).toHaveTextContent(/file type not accepted/i)
})

test('shows validation error when file exceeds max size', async () => {
  const user = userEvent.setup()
  const onFileSelect = vi.fn()

  // Use a tiny max size to avoid allocating huge buffers in test.
  render(<FileUpload accept=".pdf" maxSizeMB={0.0001} onFileSelect={onFileSelect} />)

  const file = new File(['x'.repeat(2048)], 'outline.pdf', { type: 'application/pdf' })
  await user.upload(getFileInput(), file)

  expect(onFileSelect).toHaveBeenCalledWith(null)
  expect(screen.getByRole('alert')).toHaveTextContent(/file size exceeds/i)
})
