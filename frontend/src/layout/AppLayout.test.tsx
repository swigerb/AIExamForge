import { Routes, Route } from 'react-router-dom'
import { screen } from '@testing-library/react'
import { AppLayout } from './AppLayout'
import { renderWithProviders } from '../test/testUtils'

test('marks the correct step as current based on route', () => {
  renderWithProviders(
    <Routes>
      <Route element={<AppLayout />}>
        <Route path="/config" element={<div>Config Page</div>} />
      </Route>
    </Routes>,
    { route: '/config' }
  )

  expect(screen.getByTestId('location')).toHaveTextContent('/config')
  expect(screen.getByText('Configure').closest('[aria-current="step"]')).toBeTruthy()
})
