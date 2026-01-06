import { render, screen } from '@testing-library/react'
import App from './App'

test('renders the initial route (outline upload)', () => {
  render(<App />)
  expect(screen.getByText('AI Exam Forge')).toBeInTheDocument()
  expect(screen.getByText('Upload Exam Outline')).toBeInTheDocument()
})
