# Tasks: Generate Practice Exam Questions from Outline

**Input**: Design documents from `/specs/001-generate-questions/`  
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/openapi.yaml ✅, quickstart.md ✅, engineering-plan.md ✅

---

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

## Path Conventions (from plan.md)

```text
backend/src/AIExamForge.Api/           # Endpoints, middleware
backend/src/AIExamForge.Application/   # Services, orchestration
backend/src/AIExamForge.Domain/        # Entities, value objects
backend/src/AIExamForge.Infrastructure/# External integrations
backend/tests/                         # Unit, integration, contract tests
frontend/src/                          # React components, pages
docs/                                  # Authoritative specs (schemas, guides)
prompts/                               # LLM prompt fragments
```

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization, solution structure, and configuration

- [X] T001 Create solution structure: `dotnet new sln -n AIExamForge` in repository root
- [X] T002 [P] Create backend project: `backend/src/AIExamForge.Api/AIExamForge.Api.csproj` (.NET 10, ASP.NET Core Minimal APIs)
- [X] T003 [P] Create application layer: `backend/src/AIExamForge.Application/AIExamForge.Application.csproj`
- [X] T004 [P] Create domain layer: `backend/src/AIExamForge.Domain/AIExamForge.Domain.csproj`
- [X] T005 [P] Create infrastructure layer: `backend/src/AIExamForge.Infrastructure/AIExamForge.Infrastructure.csproj`
- [X] T006 [P] Create unit test project: `backend/tests/AIExamForge.UnitTests/AIExamForge.UnitTests.csproj` (xUnit, FluentAssertions, Moq)
- [X] T007 [P] Create integration test project: `backend/tests/AIExamForge.IntegrationTests/AIExamForge.IntegrationTests.csproj`
- [X] T008 [P] Create contract test project: `backend/tests/AIExamForge.ContractTests/AIExamForge.ContractTests.csproj`
- [ ] T009 [P] Initialize frontend: `npm create vite@latest frontend -- --template react-ts` (React + TypeScript)
- [X] T010 Add project references to solution file (all backend projects + tests)
- [ ] T011 [P] Configure `.editorconfig` for C# and TypeScript formatting standards
- [X] T012 [P] Configure `Directory.Build.props` with common properties (LangVersion, ImplicitUsings, Nullable)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Domain Layer Foundation

- [X] T013 Create value object `ItemTypeCode` in `backend/src/AIExamForge.Domain/ValueObjects/ItemTypeCode.cs` per data-model.md (19 codes)
- [X] T014 [P] Create enum `RenderMode` in `backend/src/AIExamForge.Domain/ValueObjects/RenderMode.cs` (Exam, Study)
- [X] T015 [P] Create enum `QuestionDifficulty` in `backend/src/AIExamForge.Domain/ValueObjects/QuestionDifficulty.cs` (Easy, Medium, Hard)
- [X] T016 [P] Create enum `GenerationStatus` in `backend/src/AIExamForge.Domain/ValueObjects/GenerationStatus.cs` per data-model.md
- [X] T017 [P] Create enum `FileUploadStatus` in `backend/src/AIExamForge.Domain/ValueObjects/FileUploadStatus.cs` per data-model.md
- [X] T018 [P] Create enum `ExportFormat` in `backend/src/AIExamForge.Domain/ValueObjects/ExportFormat.cs` (Json, Markdown, Docx, Pdf)
- [X] T019 [P] Create record `DifficultyDistribution` in `backend/src/AIExamForge.Domain/ValueObjects/DifficultyDistribution.cs`
- [X] T020 [P] Create record `ValidationResult` in `backend/src/AIExamForge.Domain/ValueObjects/ValidationResult.cs` per data-model.md
- [X] T021 [P] Create record `LintViolation` in `backend/src/AIExamForge.Domain/ValueObjects/LintViolation.cs` per data-model.md
- [X] T022 [P] Create enum `LintRule` in `backend/src/AIExamForge.Domain/ValueObjects/LintRule.cs` (NoContractions, etc.)

### Entity Foundation

- [X] T023 Create entity `Outline` in `backend/src/AIExamForge.Domain/Entities/Outline.cs` per data-model.md
- [X] T024 [P] Create entity `OutlineNode` in `backend/src/AIExamForge.Domain/Entities/OutlineNode.cs` per data-model.md
- [X] T025 [P] Create entity `FileUpload` in `backend/src/AIExamForge.Domain/Entities/FileUpload.cs` per data-model.md
- [X] T026 [P] Create entity `GenerationRequest` in `backend/src/AIExamForge.Domain/Entities/GenerationRequest.cs` per data-model.md
- [X] T027 [P] Create entity `QuestionSet` in `backend/src/AIExamForge.Domain/Entities/QuestionSet.cs` per data-model.md
- [X] T028 [P] Create entity `Question` in `backend/src/AIExamForge.Domain/Entities/Question.cs` per data-model.md
- [X] T029 [P] Create entity `ExportRecord` in `backend/src/AIExamForge.Domain/Entities/ExportRecord.cs` per data-model.md
- [X] T030 Create domain model `QuestionTypeDefinition` in `backend/src/AIExamForge.Domain/Entities/QuestionTypeDefinition.cs` per data-model.md

### Repository Interfaces

- [X] T031 Create interface `IOutlineRepository` in `backend/src/AIExamForge.Domain/Repositories/IOutlineRepository.cs`
- [X] T032 [P] Create interface `IFileUploadRepository` in `backend/src/AIExamForge.Domain/Repositories/IFileUploadRepository.cs`
- [X] T033 [P] Create interface `IGenerationRequestRepository` in `backend/src/AIExamForge.Domain/Repositories/IGenerationRequestRepository.cs`
- [X] T034 [P] Create interface `IQuestionSetRepository` in `backend/src/AIExamForge.Domain/Repositories/IQuestionSetRepository.cs`
- [X] T035 [P] Create interface `IExportRecordRepository` in `backend/src/AIExamForge.Domain/Repositories/IExportRecordRepository.cs`

### Infrastructure Foundation

- [X] T036 Create EF Core DbContext `AIExamForgeDbContext` in `backend/src/AIExamForge.Infrastructure/Data/AIExamForgeDbContext.cs`
- [X] T037 Create entity configurations for all entities in `backend/src/AIExamForge.Infrastructure/Data/Configurations/`
- [X] T038 [P] Implement `OutlineRepository` in `backend/src/AIExamForge.Infrastructure/Repositories/OutlineRepository.cs`
- [X] T039 [P] Implement `FileUploadRepository` in `backend/src/AIExamForge.Infrastructure/Repositories/FileUploadRepository.cs`
- [X] T040 [P] Implement `GenerationRequestRepository` in `backend/src/AIExamForge.Infrastructure/Repositories/GenerationRequestRepository.cs`
- [X] T041 [P] Implement `QuestionSetRepository` in `backend/src/AIExamForge.Infrastructure/Repositories/QuestionSetRepository.cs`
- [X] T042 [P] Implement `ExportRecordRepository` in `backend/src/AIExamForge.Infrastructure/Repositories/ExportRecordRepository.cs`


### API Foundation

- [X] T043 Configure `Program.cs` in `backend/src/AIExamForge.Api/Program.cs` with DI, middleware, and routing
- [X] T044 [P] Create error handling middleware in `backend/src/AIExamForge.Api/Middleware/ErrorHandlingMiddleware.cs`
- [X] T045 [P] Create rate limiting middleware in `backend/src/AIExamForge.Api/Middleware/RateLimitingMiddleware.cs` (10 req/min, 100 questions/day)
- [X] T046 [P] Create correlation ID middleware in `backend/src/AIExamForge.Api/Middleware/CorrelationIdMiddleware.cs`
- [X] T047 Configure authentication (Microsoft Entra ID) in `backend/src/AIExamForge.Api/Extensions/AuthenticationExtensions.cs`
- [X] T048 Create `/health` endpoint in `backend/src/AIExamForge.Api/Program.cs` per openapi.yaml

### Authoritative Specs Foundation

- [X] T049 Create master question schema `docs/schemas/common/question.schema.json` per engineering-plan.md
- [X] T050 [P] Create common schema components in `docs/schemas/common/` (choice.schema.json, question-base.schema.json)
- [X] T051 [P] Create item-type schemas for dichotomous types (M1, BA, DA, SC, DOMC) in `docs/schemas/common/item-types/`
- [X] T052 [P] Create item-type schemas for polytomous types (CHOOSE_N, AS, BL, MW, HA, DD, MG, GI, MS, PA, CA, AN, LABS, SA) in `docs/schemas/common/item-types/`
- [X] T053 [P] Create system prompt `prompts/system-mcp-only.md` per engineering-plan.md
- [X] T054 [P] Create exam style prompt `prompts/exam-style.md` per engineering-plan.md
- [X] T055 [P] Create prompt fragments for all 19 item types in `prompts/item-types/{CODE}.md`

### Item Type Registry

- [X] T056 Create interface `IItemTypeRegistry` in `backend/src/AIExamForge.Domain/Interfaces/IItemTypeRegistry.cs`
- [X] T057 Implement `ItemTypeRegistry` in `backend/src/AIExamForge.Infrastructure/TypeRegistry/ItemTypeRegistry.cs` (loads schemas + prompts at startup)

### Schema Validation Foundation

- [X] T058 Create interface `ISchemaValidator` in `backend/src/AIExamForge.Application/Validation/Interfaces/ISchemaValidator.cs`
- [X] T059 Implement `SchemaValidator` in `backend/src/AIExamForge.Application/Validation/SchemaValidator.cs` (NJsonSchema)
- [X] T060 Create interface `ILintValidator` in `backend/src/AIExamForge.Application/Validation/Interfaces/ILintValidator.cs`
- [X] T061 Implement `LintValidator` in `backend/src/AIExamForge.Application/Validation/LintValidator.cs` per docs/exam-writing-style-guide.md

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Upload/Paste Exam Outline and Visualize Structure (Priority: P1) 🎯 MVP

**Goal**: Users can provide exam outlines via text paste or file upload, see parsed hierarchy, and select sections

**Independent Test**: Parse text, upload .docx/.pdf, verify tree renders, select any node, handle errors (malformed, encrypted, oversized)

### Implementation for User Story 1

#### Backend - Text Parsing

- [X] T062 [US1] Create interface `IOutlineParser` in `backend/src/AIExamForge.Application/Parsing/Interfaces/IOutlineParser.cs`
- [X] T063 [US1] Implement `OutlineParser` in `backend/src/AIExamForge.Application/Parsing/OutlineParser.cs` (detect headings, indentation, numbering)
- [X] T064 [US1] Create DTO `ParseOutlineRequest` in `backend/src/AIExamForge.Application/Parsing/DTOs/ParseOutlineRequest.cs` per openapi.yaml
- [X] T065 [P] [US1] Create DTO `OutlineResponse` in `backend/src/AIExamForge.Application/Parsing/DTOs/OutlineResponse.cs` per openapi.yaml
- [X] T066 [US1] Create service `OutlineService` in `backend/src/AIExamForge.Application/Parsing/OutlineService.cs` (orchestrates parsing + persistence)
- [X] T067 [US1] Create endpoint `POST /api/outlines/parse` in `backend/src/AIExamForge.Api/Endpoints/OutlineEndpoints.cs` per openapi.yaml
- [X] T068 [US1] Create endpoint `GET /api/outlines/{outlineId}` in `backend/src/AIExamForge.Api/Endpoints/OutlineEndpoints.cs` per openapi.yaml

#### Backend - File Upload & Extraction

- [X] T069 [US1] Create interface `IDocumentExtractor` in `backend/src/AIExamForge.Application/Documents/Interfaces/IDocumentExtractor.cs`
- [X] T070 [P] [US1] Implement `DocxExtractor` in `backend/src/AIExamForge.Infrastructure/Documents/DocxExtractor.cs` (Open XML SDK)
- [X] T071 [P] [US1] Implement `PdfExtractor` in `backend/src/AIExamForge.Infrastructure/Documents/PdfExtractor.cs` (Azure AI Document Intelligence)
- [X] T072 [US1] Create service `FileUploadService` in `backend/src/AIExamForge.Application/Documents/FileUploadService.cs` (validation, extraction, cleanup)
- [X] T073 [US1] Create endpoint `POST /api/outlines/upload` in `backend/src/AIExamForge.Api/Endpoints/OutlineEndpoints.cs` per openapi.yaml
- [X] T074 [US1] Configure Azure Blob Storage client in `backend/src/AIExamForge.Infrastructure/Storage/BlobStorageClient.cs` (temp-uploads container)
- [X] T075 [US1] Create background service `TempFileCleanupService` in `backend/src/AIExamForge.Infrastructure/BackgroundServices/TempFileCleanupService.cs` (1-hour lifecycle)

#### Frontend - Outline Input & Tree View

- [ ] T076 [P] [US1] Create `OutlineInputPage` in `frontend/src/pages/OutlineInputPage.tsx` (text paste or file upload)
- [ ] T077 [P] [US1] Create `OutlineTree` component in `frontend/src/components/outline/OutlineTree.tsx` (expand/collapse, selection)
- [ ] T078 [P] [US1] Create `FileUploadDropzone` component in `frontend/src/components/outline/FileUploadDropzone.tsx`
- [ ] T079 [US1] Create `useOutlineParser` hook in `frontend/src/hooks/useOutlineParser.ts` (API calls, loading state)
- [ ] T080 [US1] Create API service `outlineService` in `frontend/src/services/outlineService.ts`
- [ ] T081 [US1] Create types `Outline`, `OutlineNode` in `frontend/src/types/outline.ts` matching openapi.yaml
- [ ] T082 [US1] Add ARIA labels and keyboard navigation to `OutlineTree` per WCAG 2.1 AA

**Checkpoint**: User Story 1 complete - users can paste/upload outlines and see/select sections

---

## Phase 4: User Story 2 - Configure Question Generation Parameters (Priority: P1)

**Goal**: Users configure generation parameters (count, types, difficulty, tone) with validation

**Independent Test**: Select section, configure parameters (5 questions, 2 types), verify validation and UI state

### Implementation for User Story 2

#### Backend - Configuration Validation

- [X] T083 [US2] Create DTO `GenerationRequestDto` in `backend/src/AIExamForge.Application/Generation/DTOs/GenerationRequestDto.cs` per openapi.yaml
- [X] T084 [US2] Create validator `GenerationRequestValidator` in `backend/src/AIExamForge.Application/Generation/Validators/GenerationRequestValidator.cs` (FluentValidation)
- [X] T085 [US2] Add validation rules: questionCount 1-50, at least one node selected, valid item type codes

#### Frontend - Configuration Form

- [ ] T086 [P] [US2] Create `GenerationConfigPage` in `frontend/src/pages/GenerationConfigPage.tsx`
- [ ] T087 [P] [US2] Create `ItemTypeSelector` component in `frontend/src/components/generation/ItemTypeSelector.tsx` (checkboxes for 19 types)
- [ ] T088 [P] [US2] Create `DifficultySlider` component in `frontend/src/components/generation/DifficultySlider.tsx` (Easy/Medium/Hard distribution)
- [ ] T089 [P] [US2] Create `ToneDropdown` component in `frontend/src/components/generation/ToneDropdown.tsx` (concise, professional, exam-like)
- [ ] T090 [US2] Create `GenerationConfigForm` component in `frontend/src/components/generation/GenerationConfigForm.tsx` (composes all inputs)
- [ ] T091 [US2] Create `useGenerationConfig` hook in `frontend/src/hooks/useGenerationConfig.ts` (validation, state)
- [ ] T092 [US2] Create types `GenerationRequest`, `DifficultyDistribution` in `frontend/src/types/generation.ts` matching openapi.yaml
- [ ] T093 [US2] Add accessibility labels and keyboard support to form controls per WCAG 2.1 AA

**Checkpoint**: User Story 2 complete - users can configure generation parameters with validation

---

## Phase 5: User Story 3 - Generate Question Set with Progress Tracking (Priority: P1)

**Goal**: Users click Generate, see real-time progress, receive validated question set

**Independent Test**: Generate 3-5 questions with 2 types, verify metadata, check explanations present

### Implementation for User Story 3

#### Backend - MCP Integration

- [X] T094 [US3] Create interface `IMcpClient` in `backend/src/AIExamForge.Application/Mcp/Interfaces/IMcpClient.cs`
- [X] T095 [US3] Implement `McpClient` in `backend/src/AIExamForge.Infrastructure/MCP/McpClient.cs` (Microsoft Learn MCP server)
- [X] T096 [US3] Create `McpContent` model in `backend/src/AIExamForge.Domain/Models/McpContent.cs` (content chunks with source URLs)
- [X] T097 [US3] Create `McpRetrievalService` in `backend/src/AIExamForge.Application/Mcp/McpRetrievalService.cs` (query construction, caching)

#### Backend - Prompt Assembly

- [X] T098 [US3] Create interface `IPromptAssembler` in `backend/src/AIExamForge.Application/Generation/Interfaces/IPromptAssembler.cs`
- [X] T099 [US3] Implement `PromptAssembler` in `backend/src/AIExamForge.Application/Generation/PromptAssembler.cs` (loads system/exam/type prompts, composes)
- [X] T100 [US3] Create `PromptPayload` model in `backend/src/AIExamForge.Domain/Models/PromptPayload.cs`

#### Backend - LLM Generation

- [X] T101 [US3] Create interface `ILlmClient` in `backend/src/AIExamForge.Application/Generation/Interfaces/ILlmClient.cs`
- [X] T102 [US3] Implement `AzureOpenAiClient` in `backend/src/AIExamForge.Infrastructure/OpenAI/AzureOpenAiClient.cs` (Azure OpenAI, JSON mode)
- [X] T103 [US3] Create `QuestionGenerationService` in `backend/src/AIExamForge.Application/Generation/QuestionGenerationService.cs` (orchestrates full pipeline)

#### Backend - Validation Pipeline

- [X] T104 [US3] Create `QuestionValidationPipeline` in `backend/src/AIExamForge.Application/Validation/QuestionValidationPipeline.cs` (schema → lint → persist)
- [X] T105 [US3] Integrate schema validation using `ISchemaValidator` (from Phase 2)
- [X] T106 [US3] Integrate lint validation using `ILintValidator` (from Phase 2)
- [X] T107 [US3] Implement retry logic (up to 3 attempts) for validation failures in `QuestionGenerationService`

#### Backend - Generation Endpoint & Streaming

- [X] T108 [US3] Create endpoint `POST /api/generations` in `backend/src/AIExamForge.Api/Endpoints/GenerationEndpoints.cs` per openapi.yaml
- [X] T109 [US3] Create endpoint `GET /api/generations/{generationId}` in `backend/src/AIExamForge.Api/Endpoints/GenerationEndpoints.cs` per openapi.yaml
- [X] T110 [US3] Create SSE endpoint `GET /api/generations/{generationId}/stream` in `backend/src/AIExamForge.Api/Endpoints/GenerationEndpoints.cs` per openapi.yaml
- [X] T111 [US3] Create `GenerationProgressHub` or SSE handler in `backend/src/AIExamForge.Api/Streaming/GenerationProgressHandler.cs`
- [X] T112 [US3] Create DTO `GenerationStartResponse` in `backend/src/AIExamForge.Application/Generation/DTOs/GenerationStartResponse.cs` per openapi.yaml
- [X] T113 [US3] Create DTO `GenerationResponse` in `backend/src/AIExamForge.Application/Generation/DTOs/GenerationResponse.cs` per openapi.yaml

#### Frontend - Generation Progress

- [ ] T114 [P] [US3] Create `GenerationProgressPage` in `frontend/src/pages/GenerationProgressPage.tsx`
- [ ] T115 [P] [US3] Create `ProgressIndicator` component in `frontend/src/components/generation/ProgressIndicator.tsx`
- [ ] T116 [US3] Create `useGenerationStream` hook in `frontend/src/hooks/useGenerationStream.ts` (EventSource for SSE)
- [ ] T117 [US3] Create API service `generationService` in `frontend/src/services/generationService.ts`
- [ ] T118 [US3] Create types `GenerationStatus`, `QuestionSet`, `Question` in `frontend/src/types/generation.ts` matching openapi.yaml

#### Frontend - Question Display

- [ ] T119 [P] [US3] Create `QuestionReviewPage` in `frontend/src/pages/QuestionReviewPage.tsx`
- [ ] T120 [P] [US3] Create `QuestionList` component in `frontend/src/components/questions/QuestionList.tsx`
- [ ] T121 [P] [US3] Create `QuestionCard` component in `frontend/src/components/questions/QuestionCard.tsx` (displays ID, type, difficulty, content, answer, explanation)
- [ ] T122 [US3] Add live region announcements for progress updates (screen reader accessibility)

**Checkpoint**: User Story 3 complete - users can generate questions and see real-time progress

---

## Phase 6: User Story 4 - Review, Regenerate, and Edit Questions (Priority: P2)

**Goal**: Users review, regenerate (all/single), and manually edit questions

**Independent Test**: Generate set, edit one question, verify edit persists, regenerate single question, verify replacement

### Implementation for User Story 4

#### Backend - Regeneration

- [X] T123 [US4] Add method `RegenerateAllAsync` to `QuestionGenerationService` in `backend/src/AIExamForge.Application/Generation/QuestionGenerationService.cs`
- [X] T124 [US4] Add method `RegenerateQuestionAsync` to `QuestionGenerationService` (single question regeneration)
- [X] T125 [US4] Create endpoint `POST /api/generations/{generationId}/regenerate` in `backend/src/AIExamForge.Api/Endpoints/GenerationEndpoints.cs`
- [X] T126 [US4] Create endpoint `POST /api/questions/{questionId}/regenerate` in `backend/src/AIExamForge.Api/Endpoints/QuestionEndpoints.cs`

#### Backend - Question Editing

- [X] T127 [US4] Create DTO `UpdateQuestionRequest` in `backend/src/AIExamForge.Application/Questions/DTOs/UpdateQuestionRequest.cs`
- [X] T128 [US4] Create service `QuestionEditingService` in `backend/src/AIExamForge.Application/Questions/QuestionEditingService.cs`
- [X] T129 [US4] Create endpoint `PUT /api/questions/{questionId}` in `backend/src/AIExamForge.Api/Endpoints/QuestionEndpoints.cs`
- [X] T130 [US4] Validate edited questions against schema and lint rules before persisting

#### Frontend - Edit & Regenerate UI

- [ ] T131 [P] [US4] Create `QuestionEditor` component in `frontend/src/components/questions/QuestionEditor.tsx` (inline editing)
- [ ] T132 [P] [US4] Create `RegenerateButton` component in `frontend/src/components/questions/RegenerateButton.tsx`
- [ ] T133 [US4] Create `useQuestionEdit` hook in `frontend/src/hooks/useQuestionEdit.ts`
- [ ] T134 [US4] Add "Regenerate All" button to `QuestionReviewPage`
- [ ] T135 [US4] Add "Regenerate This" and "Edit" buttons to `QuestionCard`

**Checkpoint**: User Story 4 complete - users can edit and regenerate questions

---

## Phase 7: User Story 5 - Export Question Set in Multiple Formats (Priority: P2)

**Goal**: Users export to JSON, Markdown, DOCX, PDF with Exam/Study render modes

**Independent Test**: Generate set, export all 4 formats, verify content integrity, no internal MCP refs, title page in Word/PDF

### Implementation for User Story 5

#### Backend - Export Infrastructure

- [X] T136 [US5] Create interface `IQuestionExporter` in `backend/src/AIExamForge.Application/Export/Interfaces/IQuestionExporter.cs`
- [X] T137 [US5] Create `ExportContext` model in `backend/src/AIExamForge.Domain/Models/ExportContext.cs` (render mode, metadata, questions)

#### Backend - Format-Specific Exporters

- [X] T138 [P] [US5] Implement `JsonExporter` in `backend/src/AIExamForge.Application/Export/JsonExporter.cs` (omit explanation in Exam mode)
- [X] T139 [P] [US5] Implement `MarkdownExporter` in `backend/src/AIExamForge.Application/Export/MarkdownExporter.cs` (render mode aware)
- [X] T140 [P] [US5] Implement `DocxExporter` in `backend/src/AIExamForge.Infrastructure/Export/DocxExporter.cs` (Open XML SDK, title page, no MCP refs)
- [X] T141 [P] [US5] Implement `PdfExporter` in `backend/src/AIExamForge.Infrastructure/Export/PdfExporter.cs` (QuestPDF, matches DOCX layout)

#### Backend - Render Mode Logic

- [X] T142 [US5] Create `RenderModeFilter` in `backend/src/AIExamForge.Application/Export/RenderModeFilter.cs` (strips explanations in Exam mode)
- [X] T143 [US5] Ensure all exporters call `RenderModeFilter` before rendering

#### Backend - Export Endpoint

- [X] T144 [US5] Create endpoint `GET /api/questions/{generationId}/export` in `backend/src/AIExamForge.Api/Endpoints/ExportEndpoints.cs` per openapi.yaml
- [X] T145 [US5] Create DTO `ExportedQuestionSet` in `backend/src/AIExamForge.Application/Export/DTOs/ExportedQuestionSet.cs` per openapi.yaml
- [X] T146 [US5] Create service `ExportService` in `backend/src/AIExamForge.Application/Export/ExportService.cs` (format routing, blob storage)

#### Frontend - Export UI

- [ ] T147 [P] [US5] Create `ExportPage` in `frontend/src/pages/ExportPage.tsx`
- [ ] T148 [P] [US5] Create `ExportFormatPicker` component in `frontend/src/components/export/ExportFormatPicker.tsx` (JSON/MD/DOCX/PDF)
- [ ] T149 [P] [US5] Create `RenderModeToggle` component in `frontend/src/components/export/RenderModeToggle.tsx` (Exam/Study with labels)
- [ ] T150 [US5] Create `ExportDialog` component in `frontend/src/components/export/ExportDialog.tsx` (combines format + mode)
- [ ] T151 [US5] Create `useExport` hook in `frontend/src/hooks/useExport.ts`
- [ ] T152 [US5] Create API service `exportService` in `frontend/src/services/exportService.ts`

**Checkpoint**: User Story 5 complete - users can export in all formats with Exam/Study modes

---

## Phase 8: User Story 6 - Display Content Policy Disclaimers (Priority: P1)

**Goal**: Prominent disclaimers visible on all pages and in all exports

**Independent Test**: Verify disclaimer on config screen, in exports, in help/about section

### Implementation for User Story 6

#### Frontend - Disclaimer Components

- [ ] T153 [P] [US6] Create `DisclaimerBanner` component in `frontend/src/components/common/DisclaimerBanner.tsx`
- [ ] T154 [P] [US6] Create `HelpAboutPage` in `frontend/src/pages/HelpAboutPage.tsx` (full content policy explanation)
- [ ] T155 [US6] Add `DisclaimerBanner` to `GenerationConfigPage`, `QuestionReviewPage`, and app layout
- [ ] T156 [US6] Ensure disclaimer text matches FR-016 wording exactly

#### Backend - Export Disclaimers

- [X] T157 [US6] Ensure all exporters include disclaimer header per FR-020c
- [X] T158 [US6] Verify DOCX/PDF exporters include visible disclaimer on first page per FR-020c

**Checkpoint**: User Story 6 complete - disclaimers visible throughout app and exports

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Quality improvements, security hardening, documentation, and validation

### Observability

- [X] T159 [P] Configure Azure Application Insights integration in `backend/src/AIExamForge.Api/Extensions/TelemetryExtensions.cs`
- [X] T160 [P] Add structured logging with correlation IDs per FR-025, FR-027
- [X] T161 [P] Add telemetry events for generation success/failure per FR-028
- [X] T162 Add MCP metrics tracking (response times, cache hits, empty results) per FR-033

### Security Hardening

- [X] T163 [P] Add input validation middleware for all endpoints per FR-023
- [X] T164 [P] Add file upload security: size check, content-type validation, malware scan hook per FR-001e, NFR-008
- [X] T165 [P] Ensure no internal data in exports (audit all exporters) per FR-020d
- [X] T166 Configure Azure Key Vault integration for secrets per NFR-001

### Documentation

- [X] T167 [P] Update `docs/exam-writing-style-guide.md` with lint rule mappings
- [X] T168 [P] Create `docs/style-governance.md` with precedence rules per FR-046
- [X] T169 [P] Update `README.md` with MCP dependency documentation per FR-034
- [X] T170 Run `quickstart.md` validation - verify all steps work end-to-end

### Frontend Polish

- [ ] T171 [P] Add responsive design breakpoints (mobile, tablet, desktop) per NFR-006
- [ ] T172 [P] Add loading states and error boundaries to all pages
- [ ] T173 [P] Run WCAG 2.1 AA accessibility audit on all components per NFR-005
- [ ] T174 Add error recovery guidance UI for common failures (timeout, MCP unavailable) per NFR-002

### Performance

- [X] T175 Add MCP response caching per request per NFR-004
- [X] T176 Optimize database queries with proper indexes per data-model.md
- [X] T177 Add connection pooling configuration for Azure SQL

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup) ─────────────────────────────────────────────────────┐
                                                                      │
Phase 2 (Foundational) ◄──────────────────────────────────────────────┘
      │
      │ BLOCKS all user stories
      ▼
┌─────────────────────────────────────────────────────────────────────┐
│                                                                     │
│  Phase 3 (US1: Outline)  ◄─── Required first (outline is input)    │
│         │                                                           │
│         ▼                                                           │
│  Phase 4 (US2: Config)   ◄─── Depends on outline selection         │
│         │                                                           │
│         ▼                                                           │
│  Phase 5 (US3: Generate) ◄─── Core value delivery                  │
│         │                                                           │
│         ├────────────────────┐                                      │
│         ▼                    ▼                                      │
│  Phase 6 (US4: Edit)    Phase 7 (US5: Export)  ◄─── Can parallelize│
│                                                                     │
│  Phase 8 (US6: Disclaimers) ◄─── Can run in parallel with US4/US5  │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
      │
      ▼
Phase 9 (Polish) ◄─── After all user stories complete
```

### User Story Dependencies

| Story | Depends On | Can Parallelize With |
|-------|------------|---------------------|
| US1 (Outline) | Foundational | - |
| US2 (Config) | US1 | - |
| US3 (Generate) | US2 | - |
| US4 (Edit) | US3 | US5, US6 |
| US5 (Export) | US3 | US4, US6 |
| US6 (Disclaimers) | Foundational | US4, US5 |

### Parallel Opportunities

**Within Phase 1**: T002-T009 can all run in parallel (project creation)

**Within Phase 2**: 
- T014-T022 (value objects) can run in parallel
- T024-T030 (entities) can run in parallel after T023
- T032-T035 (interfaces) can run in parallel after T031
- T038-T042 (repositories) can run in parallel after T037
- T049-T055 (authoritative specs) can run in parallel

**Within Phase 3 (US1)**:
- T070, T071 (extractors) can run in parallel
- T076-T078 (frontend components) can run in parallel

**Within Phase 5 (US3)**:
- T114-T115, T119-T121 (frontend pages/components) can run in parallel

**Within Phase 7 (US5)**:
- T138-T141 (exporters) can run in parallel
- T147-T149 (frontend components) can run in parallel

**Within Phase 9**: Most tasks are independent and can parallelize

---

## Implementation Strategy

### MVP Scope (Minimum Viable Product)

**MVP = Phase 1 + Phase 2 + Phase 3 (US1) + Phase 4 (US2) + Phase 5 (US3) + Phase 8 (US6)**

This delivers:
- ✅ Outline input (paste + upload)
- ✅ Configuration UI
- ✅ Question generation with progress
- ✅ Basic question review
- ✅ Required disclaimers

**Post-MVP**:
- Phase 6 (US4): Edit/regenerate
- Phase 7 (US5): Multi-format export
- Phase 9: Polish

### Incremental Delivery

1. **Increment 1**: Setup + Foundational + US1 → Users can input outlines
2. **Increment 2**: US2 + US3 → Users can generate questions
3. **Increment 3**: US6 → Legal compliance (disclaimers)
4. **Increment 4**: US4 + US5 → Full editing and export capabilities
5. **Increment 5**: Polish → Production-ready

---

**Generated**: 2026-01-03 | **Status**: Task Generation Complete

**Summary**:
- **Total Tasks**: 177
- **Phase 1 (Setup)**: 12 tasks
- **Phase 2 (Foundational)**: 49 tasks
- **Phase 3 (US1)**: 21 tasks
- **Phase 4 (US2)**: 11 tasks
- **Phase 5 (US3)**: 29 tasks
- **Phase 6 (US4)**: 13 tasks
- **Phase 7 (US5)**: 17 tasks
- **Phase 8 (US6)**: 6 tasks
- **Phase 9 (Polish)**: 19 tasks
