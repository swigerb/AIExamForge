import { render, screen } from '@testing-library/react'
import { Card } from './Card'

test('renders children content', () => {
  render(
    <Card>
      <div>Body</div>
    </Card>
  )

  expect(screen.getByText('Body')).toBeInTheDocument()
})

test('renders a title when provided', () => {
  render(
    <Card title="Summary">
      <div>Body</div>
    </Card>
  )

  expect(screen.getByRole('heading', { name: 'Summary' })).toBeInTheDocument()
})
