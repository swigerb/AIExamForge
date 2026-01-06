import type { ReactElement } from 'react'
import { useLayoutEffect } from 'react'
import { MemoryRouter, useLocation } from 'react-router-dom'
import { render } from '@testing-library/react'
import { WizardProvider, useWizard } from '../context'
import type { WizardState } from '../context'
import type { DifficultyDistribution, GenerationResponse, OutlineResponse, RenderMode } from '../types/api'

type RenderOptions = {
  route?: string
  wizardInitialState?: Partial<WizardState>
}

export function LocationDisplay() {
  const location = useLocation()
  return <div data-testid="location">{location.pathname}</div>
}

type WizardInit = {
  outline?: OutlineResponse | null
  selectedNodeIds?: string[]
  questionCount?: number
  questionTypes?: string[]
  renderMode?: RenderMode
  difficulty?: DifficultyDistribution
  generationId?: string | null
  generation?: GenerationResponse | null
}

export function WizardStateInitializer(props: WizardInit) {
  const wizard = useWizard()

  // Use layout effect to ensure wizard state is initialized before
  // page-level redirect effects run (prevents test flakiness).
  useLayoutEffect(() => {
    if (props.outline !== undefined) wizard.setOutline(props.outline)
    if (props.selectedNodeIds !== undefined) wizard.setSelectedNodeIds(new Set(props.selectedNodeIds))
    if (props.questionCount !== undefined) wizard.setQuestionCount(props.questionCount)
    if (props.questionTypes !== undefined) wizard.setQuestionTypes(props.questionTypes as any)
    if (props.renderMode !== undefined) wizard.setRenderMode(props.renderMode)
    if (props.difficulty !== undefined) wizard.setDifficulty(props.difficulty)
    if (props.generationId !== undefined) wizard.setGenerationId(props.generationId)
    if (props.generation !== undefined) wizard.setGeneration(props.generation)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  return null
}

export function renderWithProviders(ui: ReactElement, options: RenderOptions = {}) {
  const { route = '/', wizardInitialState } = options

  return render(
    <MemoryRouter initialEntries={[route]}>
      <WizardProvider initialState={wizardInitialState}>
        <LocationDisplay />
        {ui}
      </WizardProvider>
    </MemoryRouter>
  )
}
