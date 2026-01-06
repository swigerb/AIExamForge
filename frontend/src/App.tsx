import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { WizardProvider } from './context';
import { AppLayout } from './layout';
import {
  OutlineUploadPage,
  SectionSelectionPage,
  GenerationConfigPage,
  GenerationProgressPage,
  ExportResultsPage,
} from './pages';

function App() {
  return (
    <BrowserRouter>
      <WizardProvider>
        <Routes>
          <Route element={<AppLayout />}>
            <Route path="/" element={<OutlineUploadPage />} />
            <Route path="/select" element={<SectionSelectionPage />} />
            <Route path="/config" element={<GenerationConfigPage />} />
            <Route path="/progress" element={<GenerationProgressPage />} />
            <Route path="/results" element={<ExportResultsPage />} />
          </Route>
        </Routes>
      </WizardProvider>
    </BrowserRouter>
  );
}

export default App;
