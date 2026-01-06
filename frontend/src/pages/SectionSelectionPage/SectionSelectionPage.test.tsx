import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Routes, Route } from 'react-router-dom'
import { SectionSelectionPage } from './SectionSelectionPage'
import { renderWithProviders, WizardStateInitializer } from '../../test/testUtils'

function fakeOutline() {
  return {
    id: 'outline-1',
    examName: 'AZ-204',
    sourceType: 'TextPaste',
    createdAt: new Date().toISOString(),
    nodes: [
      {
        id: 'n1',
        title: 'Compute',
        level: 1,
        children: [],
      },
    ],
  } as any
}

test('redirects to / when no outline is loaded', async () => {
  renderWithProviders(
    <Routes>
      <Route path="/" element={<div>Home</div>} />
      <Route path="/select" element={<SectionSelectionPage />} />
    </Routes>,
    { route: '/select' }
  )

  expect(await screen.findByTestId('location')).toHaveTextContent('/')
})

test('shows warning when no sections are selected', () => {
  renderWithProviders(
    <>
      <WizardStateInitializer outline={fakeOutline()} selectedNodeIds={[]} />
      <SectionSelectionPage />
    </>,
    { route: '/select' }
  )

  expect(screen.getByText('Select Sections')).toBeInTheDocument()
  expect(screen.getByText(/Please select at least one section/i)).toBeInTheDocument()
})

test('enables continue when there is a selection', async () => {
  const user = userEvent.setup()

  renderWithProviders(
    <>
      <WizardStateInitializer outline={fakeOutline()} selectedNodeIds={['n1']} />
      <SectionSelectionPage />
    </>,
    { route: '/select' }
  )

  const btn = screen.getByRole('button', { name: /continue to configuration/i })
  expect(btn).toBeEnabled()

  await user.click(btn)
  expect(await screen.findByTestId('location')).toHaveTextContent('/config')
})
