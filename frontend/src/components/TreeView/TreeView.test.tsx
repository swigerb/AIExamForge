import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { TreeView } from './TreeView'
import type { OutlineNode } from '../../types/api'

const nodes: OutlineNode[] = [
  {
    id: 'n1',
    title: 'Compute',
    content: 'Compute section',
    children: [
      { id: 'n1-1', title: 'Containers', content: 'Containers content', children: [] },
      { id: 'n1-2', title: 'Functions', content: 'Functions content', children: [] },
    ],
  },
  {
    id: 'n2',
    title: 'Storage',
    content: 'Storage section',
    children: [],
  },
]

function TreeHarness({ initialSelected = [] as string[] }) {
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set(initialSelected))
  return (
    <TreeView
      label="Sections"
      nodes={nodes}
      selectedIds={selectedIds}
      onSelectionChange={setSelectedIds}
    />
  )
}

test('renders toolbar and tree, with first level expanded by default', () => {
  render(<TreeHarness />)

  expect(screen.getByText('Expand All')).toBeInTheDocument()
  expect(screen.getByText('Collapse All')).toBeInTheDocument()
  expect(screen.getByText('Select All')).toBeInTheDocument()
  expect(screen.getByText('Deselect All')).toBeInTheDocument()

  expect(screen.getByRole('tree', { name: /sections/i })).toBeInTheDocument()

  // Root n1 is expanded by default, so its children are visible.
  expect(screen.getByLabelText('Containers')).toBeInTheDocument()
  expect(screen.getByLabelText('Functions')).toBeInTheDocument()
})

test('selecting a parent selects all descendants', async () => {
  const user = userEvent.setup()
  render(<TreeHarness />)

  await user.click(screen.getByLabelText('Compute'))

  // n1 + n1-1 + n1-2 should be selected.
  expect(screen.getByText(/3 sections selected/i)).toBeInTheDocument()
  expect(screen.getByLabelText('Compute')).toBeChecked()
  expect(screen.getByLabelText('Containers')).toBeChecked()
  expect(screen.getByLabelText('Functions')).toBeChecked()
})

test('partial child selection makes parent indeterminate', async () => {
  const user = userEvent.setup()
  const { container } = render(<TreeHarness />)

  await user.click(screen.getByLabelText('Containers'))

  // Parent should not be checked, but should show indeterminate mark.
  expect(screen.getByLabelText('Compute')).not.toBeChecked()
  expect(container).toHaveTextContent('−')
})

test('collapse all hides descendants and expand all shows them again', async () => {
  const user = userEvent.setup()
  render(<TreeHarness />)

  await user.click(screen.getByText('Collapse All'))
  expect(screen.queryByLabelText('Containers')).not.toBeInTheDocument()

  await user.click(screen.getByText('Expand All'))
  expect(screen.getByLabelText('Containers')).toBeInTheDocument()
})

test('select all and deselect all update the summary', async () => {
  const user = userEvent.setup()
  render(<TreeHarness />)

  await user.click(screen.getByText('Select All'))
  expect(screen.getByText(/4 sections selected/i)).toBeInTheDocument()

  await user.click(screen.getByText('Deselect All'))
  expect(screen.getByText(/0 sections selected/i)).toBeInTheDocument()
})
