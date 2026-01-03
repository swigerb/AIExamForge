# Milestone Checklists: AIExamForge Implementation

**Feature**: 001-generate-questions  
**Date**: 2026-01-03  
**Source**: spec.md, engineering-plan.md, data-model.md, contracts/openapi.yaml

---

## Milestone 0: Authoritative Specification Files

**Goal**: Establish all authoritative documents that govern code generation and validation.

**Acceptance Criteria**:
- All 19 item type schemas exist and validate with JSON Schema Draft 2020-12
- All 19 prompt fragments exist with generation instructions
- Exam writing style guide defines all lint rules
- Style governance defines precedence hierarchy
- Master question schema references all item-type schemas

### 0.1 Documentation Structure

- [ ] M0.1.1 Create `docs/exam-writing-style-guide.md` with exam item writing rules (no contractions, no teaching text, numeric ordering, parallel form)
- [ ] M0.1.2 Create `docs/item-types.md` with definitions for all 19 types (M1, BA, DA, SC, DOMC, CHOOSE_N, AS, BL, MW, HA, DD, MG, GI, MS, PA, CA, AN, LABS, SA)
- [ ] M0.1.3 Create `docs/style-governance.md` with style precedence rules (Exam Style Guide → Microsoft Writing Style Guide)
- [ ] M0.1.4 Create `docs/schemas/README.md` documenting schema structure and validation process

### 0.2 Master Question Schema

- [ ] M0.2.1 Create `docs/schemas/question.schema.json` as top-level wrapper with `$ref` to item-type schemas
- [ ] M0.2.2 Create `docs/schemas/common/metadata.schema.json` (displayId, difficulty, sourceNodeTitle, timestamps)
- [ ] M0.2.3 Create `docs/schemas/common/difficulty.schema.json` (enum: Easy, Medium, Hard)
- [ ] M0.2.4 Create `docs/schemas/common/item-type-code.schema.json` (enum of 19 codes)
- [ ] M0.2.5 Validate master schema with NJsonSchema or ajv locally

**Acceptance**: `dotnet test` passes schema loading; all `$ref` resolve correctly

### 0.3 Dichotomous Item Type Schemas (Single Correct Answer)

- [ ] M0.3.1 Create `docs/schemas/item-types/M1.schema.json` (Multiple Choice Select One: 4-5 choices, exactly 1 correct)
- [ ] M0.3.2 Create `docs/schemas/item-types/BA.schema.json` (Best Answer: 4-5 choices, 1 best answer with plausible distractors)
- [ ] M0.3.3 Create `docs/schemas/item-types/DA.schema.json` (Dynamic Answer Choice: stem with variable, choices depend on variable value)
- [ ] M0.3.4 Create `docs/schemas/item-types/SC.schema.json` (Sentence Correction: sentence with error, 4-5 correction options)
- [ ] M0.3.5 Create `docs/schemas/item-types/DOMC.schema.json` (Dynamic Option Multiple Choice: sequential reveal, 3 correct + 3 incorrect)

**Acceptance**: Each schema validates 3+ golden examples; rejects 2+ invalid examples

### 0.4 Polytomous Item Type Schemas (Multiple/Partial Credit)

- [ ] M0.4.1 Create `docs/schemas/item-types/CHOOSE_N.schema.json` (Choose N from list: N+2 minimum choices, N correct answers)
- [ ] M0.4.2 Create `docs/schemas/item-types/AS.schema.json` (Active Screen: UI simulation with interaction points)
- [ ] M0.4.3 Create `docs/schemas/item-types/BL.schema.json` (Build List: unordered items → correct sequence)
- [ ] M0.4.4 Create `docs/schemas/item-types/MW.schema.json` (Magnetic Words: word bank → fill blanks)
- [ ] M0.4.5 Create `docs/schemas/item-types/HA.schema.json` (Hot Area: image with clickable regions)
- [ ] M0.4.6 Create `docs/schemas/item-types/DD.schema.json` (Drag and Drop: items → targets mapping)
- [ ] M0.4.7 Create `docs/schemas/item-types/MG.schema.json` (Matching: left column → right column pairs)
- [ ] M0.4.8 Create `docs/schemas/item-types/GI.schema.json` (Graphics Interpretation: graph/chart with dropdown questions)
- [ ] M0.4.9 Create `docs/schemas/item-types/MS.schema.json` (Multi-Source Reasoning: tabbed content + questions)
- [ ] M0.4.10 Create `docs/schemas/item-types/PA.schema.json` (Two-Part Analysis: two related columns, row selection)
- [ ] M0.4.11 Create `docs/schemas/item-types/CA.schema.json` (Code/Table Analysis: code or table + Yes/No statements)
- [ ] M0.4.12 Create `docs/schemas/item-types/AN.schema.json` (Statement Analysis: statements with Yes/No/NA responses)
- [ ] M0.4.13 Create `docs/schemas/item-types/LABS.schema.json` (Task-Based Labs: multi-step scenario with tasks)
- [ ] M0.4.14 Create `docs/schemas/item-types/SA.schema.json` (Short Answer Code: code completion with acceptable patterns)

**Acceptance**: Each schema validates golden examples; constraint tests pass (e.g., CHOOSE_N requires N+2 choices)

### 0.5 Prompt Fragments

- [ ] M0.5.1 Create `prompts/system-mcp-only.md` (system prompt enforcing MCP-exclusive sourcing)
- [ ] M0.5.2 Create `prompts/exam-style.md` (exam tone, no contractions, professional language)
- [ ] M0.5.3 Create `prompts/item-types/M1.md` through `prompts/item-types/SA.md` (19 files, one per type)
- [ ] M0.5.4 Each prompt fragment includes: type definition, structure requirements, scoring model, 2+ examples
- [ ] M0.5.5 Validate all prompt files load at startup (registry initialization test)

**Acceptance**: ItemTypeRegistry loads all 19 types without error; prompts contain required sections

---

## Milestone 1: Lint Validation Engine

**Goal**: Implement style rule enforcement derived from `docs/exam-writing-style-guide.md`.

**Acceptance Criteria**:
- All lint rules from style guide are implemented
- Each rule has unit tests with pass/fail examples
- Lint violations include rule ID, message, location, and violating text
- Validation fails fast on first error OR collects all errors (configurable)

### 1.1 Lint Rule Definitions

- [ ] M1.1.1 Define `LintRule` enum in `backend/src/AIExamForge.Domain/ValueObjects/LintRule.cs`
- [ ] M1.1.2 Create `LintRuleDefinition` record with: RuleId, Description, Severity, AppliesTo (stem/choices/instruction/explanation)

### 1.2 Text Analysis Rules

| Rule ID | Rule | Implementation |
|---------|------|----------------|
| M1.2.1 | `NO_CONTRACTIONS` | Regex: `\b(don't|won't|can't|isn't|aren't|...)\b` → reject |
| M1.2.2 | `NO_FORBIDDEN_PHRASES` | Blocklist: "click here", "as follows", "note that" |
| M1.2.3 | `NO_TEACHING_TEXT` | Detect explanatory patterns in stem/choices (e.g., "because", "this means") |
| M1.2.4 | `NO_NEGATIVES_WITHOUT_EMPHASIS` | Detect "not" without caps/bold: `\bnot\b` without `NOT` |
| M1.2.5 | `NO_ABSOLUTES` | Detect "always", "never", "all", "none" (flag for review) |
| M1.2.6 | `ACRONYM_FIRST_USE_DEFINED` | Track acronyms, ensure first use has expansion |

- [ ] M1.2.1 Implement `NoContractionsRule` in `backend/src/AIExamForge.Application/Validation/Rules/NoContractionsRule.cs`
- [ ] M1.2.2 Implement `NoForbiddenPhrasesRule` in `backend/src/AIExamForge.Application/Validation/Rules/NoForbiddenPhrasesRule.cs`
- [ ] M1.2.3 Implement `NoTeachingTextRule` in `backend/src/AIExamForge.Application/Validation/Rules/NoTeachingTextRule.cs`
- [ ] M1.2.4 Implement `NegativeEmphasisRule` in `backend/src/AIExamForge.Application/Validation/Rules/NegativeEmphasisRule.cs`
- [ ] M1.2.5 Implement `NoAbsolutesRule` in `backend/src/AIExamForge.Application/Validation/Rules/NoAbsolutesRule.cs`
- [ ] M1.2.6 Implement `AcronymDefinitionRule` in `backend/src/AIExamForge.Application/Validation/Rules/AcronymDefinitionRule.cs`

### 1.3 Structural Rules

| Rule ID | Rule | Implementation |
|---------|------|----------------|
| M1.3.1 | `NUMERIC_CHOICES_ASCENDING` | If choices are numbers, must be ascending order |
| M1.3.2 | `PARALLEL_FORM` | All choices start with same part of speech |
| M1.3.3 | `NO_ALL_OF_ABOVE` | Reject "All of the above" / "None of the above" |
| M1.3.4 | `CONSISTENT_PUNCTUATION` | All choices end same way (period or no period) |
| M1.3.5 | `NO_OVERLAPPING_CHOICES` | Choices must be mutually exclusive |

- [ ] M1.3.1 Implement `NumericOrderRule` in `backend/src/AIExamForge.Application/Validation/Rules/NumericOrderRule.cs`
- [ ] M1.3.2 Implement `ParallelFormRule` in `backend/src/AIExamForge.Application/Validation/Rules/ParallelFormRule.cs`
- [ ] M1.3.3 Implement `NoAllOfAboveRule` in `backend/src/AIExamForge.Application/Validation/Rules/NoAllOfAboveRule.cs`
- [ ] M1.3.4 Implement `ConsistentPunctuationRule` in `backend/src/AIExamForge.Application/Validation/Rules/ConsistentPunctuationRule.cs`
- [ ] M1.3.5 Implement `MutuallyExclusiveRule` in `backend/src/AIExamForge.Application/Validation/Rules/MutuallyExclusiveRule.cs`

### 1.4 Explanation Isolation Rules

| Rule ID | Rule | Implementation |
|---------|------|----------------|
| M1.4.1 | `EXPLANATION_NOT_IN_STEM` | Stem must not contain rationale/explanation |
| M1.4.2 | `EXPLANATION_NOT_IN_CHOICES` | Choices must not explain why correct/incorrect |
| M1.4.3 | `EXPLANATION_REQUIRED` | Explanation field must be non-empty |

- [ ] M1.4.1 Implement `ExplanationIsolationRule` in `backend/src/AIExamForge.Application/Validation/Rules/ExplanationIsolationRule.cs`
- [ ] M1.4.2 Add detection for explanation patterns: "because", "since", "therefore", "this is correct because"
- [ ] M1.4.3 Implement `ExplanationRequiredRule` in `backend/src/AIExamForge.Application/Validation/Rules/ExplanationRequiredRule.cs`

### 1.5 Lint Validator Integration

- [ ] M1.5.1 Create `ILintValidator` interface in `backend/src/AIExamForge.Application/Validation/Interfaces/ILintValidator.cs`
- [ ] M1.5.2 Implement `LintValidator` that runs all rules against question content
- [ ] M1.5.3 Create `LintValidationResult` with list of `LintViolation` objects
- [ ] M1.5.4 Add configuration for fail-fast vs collect-all mode
- [ ] M1.5.5 Write unit tests for each rule with 3+ pass cases and 3+ fail cases

**Acceptance**: `dotnet test --filter "Category=Lint"` passes; 100% rule coverage

---

## Milestone 2: Per-Item-Type Validators

**Goal**: Implement validation rules that JSON Schema cannot express.

**Acceptance Criteria**:
- Each item type has a dedicated validator class
- Validators enforce type-specific business rules
- Validation errors include specific constraint violated
- All validators registered in DI and invoked by type code

### 2.1 Validator Infrastructure

- [ ] M2.1.1 Create `IItemTypeValidator` interface in `backend/src/AIExamForge.Application/Validation/Interfaces/IItemTypeValidator.cs`
- [ ] M2.1.2 Create `ItemTypeValidatorFactory` that returns validator by `ItemTypeCode`
- [ ] M2.1.3 Create base class `ItemTypeValidatorBase` with common validation helpers

### 2.2 Dichotomous Type Validators

| Type | Constraint Beyond Schema |
|------|-------------------------|
| M1 | Exactly 1 correct; NOT true/false (must have 4+ choices); correct answer not longest |
| BA | Exactly 1 best; distractors must be plausible (not obviously wrong) |
| DA | Variable referenced in stem must appear in at least 2 choice conditions |
| SC | Original sentence must contain error; correction options must fix that error |
| DOMC | Exactly 3 correct + 3 incorrect options; reveal order matters |

- [ ] M2.2.1 Implement `M1Validator` in `backend/src/AIExamForge.Application/Validation/ItemTypes/M1Validator.cs`
  - Reject if only 2 choices (true/false prohibition per FR-045)
  - Reject if correct answer is significantly longer than others
- [ ] M2.2.2 Implement `BAValidator` in `backend/src/AIExamForge.Application/Validation/ItemTypes/BAValidator.cs`
- [ ] M2.2.3 Implement `DAValidator` in `backend/src/AIExamForge.Application/Validation/ItemTypes/DAValidator.cs`
- [ ] M2.2.4 Implement `SCValidator` in `backend/src/AIExamForge.Application/Validation/ItemTypes/SCValidator.cs`
- [ ] M2.2.5 Implement `DOMCValidator` in `backend/src/AIExamForge.Application/Validation/ItemTypes/DOMCValidator.cs`
  - Validate exactly 3 correct + 3 incorrect options
  - Validate reveal order defined

### 2.3 Polytomous Type Validators

| Type | Constraint Beyond Schema |
|------|-------------------------|
| CHOOSE_N | Must have N+2 choices minimum; exactly N correct answers |
| AS | All interaction points must have defined correct actions |
| BL | Sequence length matches item count; no duplicate positions |
| MW | Word bank has exactly enough words (no extras, no missing) |
| HA | Hot areas must not overlap; at least 1 correct region |
| DD | Items and targets counts compatible; mapping is valid |
| MG | Left and right columns same length; 1:1 mapping |
| GI | Dropdown options cover valid interpretations of graphic |
| MS | All tabs referenced by at least one question |
| PA | Both columns have valid answer options; row selections valid |
| CA | Statement count matches expected; Yes/No logic consistent |
| AN | All statements have defined correct response |
| LABS | All tasks achievable with given scenario; steps ordered |
| SA | Acceptable patterns compile as valid regex or code patterns |

- [ ] M2.3.1 Implement `ChooseNValidator` in `backend/src/AIExamForge.Application/Validation/ItemTypes/ChooseNValidator.cs`
  - Enforce N+2 minimum choices
  - Enforce exactly N correct answers
- [ ] M2.3.2 Implement `ASValidator` in `backend/src/AIExamForge.Application/Validation/ItemTypes/ASValidator.cs`
- [ ] M2.3.3 Implement `BLValidator` in `backend/src/AIExamForge.Application/Validation/ItemTypes/BLValidator.cs`
- [ ] M2.3.4 Implement `MWValidator` in `backend/src/AIExamForge.Application/Validation/ItemTypes/MWValidator.cs`
- [ ] M2.3.5 Implement `HAValidator` in `backend/src/AIExamForge.Application/Validation/ItemTypes/HAValidator.cs`
- [ ] M2.3.6 Implement `DDValidator` in `backend/src/AIExamForge.Application/Validation/ItemTypes/DDValidator.cs`
- [ ] M2.3.7 Implement `MGValidator` in `backend/src/AIExamForge.Application/Validation/ItemTypes/MGValidator.cs`
- [ ] M2.3.8 Implement `GIValidator` in `backend/src/AIExamForge.Application/Validation/ItemTypes/GIValidator.cs`
- [ ] M2.3.9 Implement `MSValidator` in `backend/src/AIExamForge.Application/Validation/ItemTypes/MSValidator.cs`
- [ ] M2.3.10 Implement `PAValidator` in `backend/src/AIExamForge.Application/Validation/ItemTypes/PAValidator.cs`
- [ ] M2.3.11 Implement `CAValidator` in `backend/src/AIExamForge.Application/Validation/ItemTypes/CAValidator.cs`
- [ ] M2.3.12 Implement `ANValidator` in `backend/src/AIExamForge.Application/Validation/ItemTypes/ANValidator.cs`
- [ ] M2.3.13 Implement `LABSValidator` in `backend/src/AIExamForge.Application/Validation/ItemTypes/LABSValidator.cs`
- [ ] M2.3.14 Implement `SAValidator` in `backend/src/AIExamForge.Application/Validation/ItemTypes/SAValidator.cs`

### 2.4 Validation Pipeline Integration

- [ ] M2.4.1 Create `QuestionValidationPipeline` that chains: Schema → ItemType → Lint
- [ ] M2.4.2 Implement retry logic: on validation failure, regenerate up to 3 times
- [ ] M2.4.3 Create structured error response with all validation failures
- [ ] M2.4.4 Add telemetry for validation pass/fail rates by type

**Acceptance**: Each validator has 5+ unit tests; pipeline correctly chains validations; retry logic works

---

## Milestone 3: File Upload and Document Processing

**Goal**: Support DOCX and PDF upload with text extraction for outline parsing.

**Acceptance Criteria**:
- DOCX files extracted using Open XML SDK (no macro execution)
- PDF files extracted using Azure AI Document Intelligence
- Encrypted PDFs rejected with clear error message
- Files deleted within 1 hour of upload
- Malware scanning hook available (enabled in production)

### 3.1 Upload Infrastructure

- [ ] M3.1.1 Create `FileUpload` entity in `backend/src/AIExamForge.Domain/Entities/FileUpload.cs`
- [ ] M3.1.2 Configure Azure Blob Storage container `temp-uploads` with 1-hour lifecycle policy
- [ ] M3.1.3 Create `IBlobStorageClient` interface and implementation
- [ ] M3.1.4 Create `POST /api/outlines/upload` endpoint accepting `multipart/form-data`

### 3.2 File Validation

- [ ] M3.2.1 Validate file size ≤ 10 MB (return 413 if exceeded)
- [ ] M3.2.2 Validate content-type: only `.docx` and `.pdf` allowed (return 400 for others)
- [ ] M3.2.3 Create malware scanning interface `IMalwareScanner` (stub in dev, real in prod)
- [ ] M3.2.4 Detect encrypted/password-protected PDFs and return 400 with message: "Password-protected PDFs are not supported"

### 3.3 DOCX Extraction

- [ ] M3.3.1 Create `IDocumentExtractor` interface in `backend/src/AIExamForge.Application/Documents/Interfaces/`
- [ ] M3.3.2 Implement `DocxExtractor` using Open XML SDK in `backend/src/AIExamForge.Infrastructure/Documents/`
- [ ] M3.3.3 Preserve heading hierarchy (Heading1 → Level 1, Heading2 → Level 2, etc.)
- [ ] M3.3.4 Extract numbered/bulleted lists with structure
- [ ] M3.3.5 Reject documents with macros (log warning, do not execute)
- [ ] M3.3.6 Write tests with sample DOCX files (well-formed, malformed, with macros)

### 3.4 PDF Extraction

- [ ] M3.4.1 Implement `PdfExtractor` using Azure AI Document Intelligence (Read API)
- [ ] M3.4.2 Configure Document Intelligence client with Key Vault credentials
- [ ] M3.4.3 Detect heading structure via font size/weight heuristics
- [ ] M3.4.4 Handle multi-column layouts
- [ ] M3.4.5 Return clear error for scanned-only PDFs (no OCR in v1)
- [ ] M3.4.6 Write tests with sample PDF files (text-based, scanned, encrypted)

### 3.5 Cleanup Service

- [ ] M3.5.1 Create `TempFileCleanupService` as `IHostedService`
- [ ] M3.5.2 Run every 15 minutes, delete files with `UploadedAt` > 1 hour ago
- [ ] M3.5.3 Update `FileUpload.Status` to `Deleted` after cleanup
- [ ] M3.5.4 Log cleanup metrics (files deleted, errors)

**Acceptance**: Upload → Extract → Parse flow works for DOCX and PDF; cleanup deletes old files; malware hook callable

---

## Milestone 4: Export Generation

**Goal**: Export question sets to JSON, Markdown, DOCX, and PDF with Exam/Study render modes.

**Acceptance Criteria**:
- All 4 formats produce valid output
- Exam Mode excludes explanations entirely
- Study Mode includes explanations in separate section
- DOCX/PDF include title page, formatted questions, visible disclaimer
- No internal MCP references in any export

### 4.1 Export Infrastructure

- [ ] M4.1.1 Create `IQuestionExporter` interface with `ExportAsync(QuestionSet, ExportFormat, RenderMode)`
- [ ] M4.1.2 Create `ExportContext` model holding render mode, metadata, questions
- [ ] M4.1.3 Create `RenderModeFilter` that removes explanations for Exam mode
- [ ] M4.1.4 Create `GET /api/questions/{generationId}/export?format=&renderMode=` endpoint

### 4.2 JSON Exporter

- [ ] M4.2.1 Implement `JsonExporter` in `backend/src/AIExamForge.Application/Export/JsonExporter.cs`
- [ ] M4.2.2 Exam Mode: Omit `explanation` property from each question object
- [ ] M4.2.3 Study Mode: Include `explanation` as separate property
- [ ] M4.2.4 Include metadata header: examName, generatedAt, totalQuestions, disclaimer
- [ ] M4.2.5 Exclude `internalMcpReferenceIds` from output

### 4.3 Markdown Exporter

- [ ] M4.3.1 Implement `MarkdownExporter` in `backend/src/AIExamForge.Application/Export/MarkdownExporter.cs`
- [ ] M4.3.2 Render questions with: `## Q001 [M1] [Medium]` headers
- [ ] M4.3.3 Format choices as lettered list (A, B, C, D)
- [ ] M4.3.4 Exam Mode: No explanation sections
- [ ] M4.3.5 Study Mode: `### Explanation` subsection per question OR consolidated at end
- [ ] M4.3.6 Include disclaimer at top of document

### 4.4 DOCX Exporter

- [ ] M4.4.1 Implement `DocxExporter` in `backend/src/AIExamForge.Infrastructure/Export/DocxExporter.cs` using Open XML SDK
- [ ] M4.4.2 Create title page: exam name, section, generation date, disclaimer
- [ ] M4.4.3 Format questions with consistent styles (Heading2 for questions, Normal for content)
- [ ] M4.4.4 Add type badge and difficulty indicator per question
- [ ] M4.4.5 Exam Mode: Questions and answers only
- [ ] M4.4.6 Study Mode: "Answer Key with Explanations" section at end
- [ ] M4.4.7 Verify no internal MCP references in document properties or content
- [ ] M4.4.8 Test output opens correctly in Word

### 4.5 PDF Exporter

- [ ] M4.5.1 Implement `PdfExporter` in `backend/src/AIExamForge.Infrastructure/Export/PdfExporter.cs` using QuestPDF
- [ ] M4.5.2 Match DOCX layout and content structure
- [ ] M4.5.3 Embed fonts for consistency across systems
- [ ] M4.5.4 Include page numbers and headers
- [ ] M4.5.5 Apply same render mode logic as DOCX
- [ ] M4.5.6 Verify no internal data in PDF metadata
- [ ] M4.5.7 Test output opens correctly in PDF viewers

### 4.6 Export Security Audit

- [ ] M4.6.1 Create unit test that verifies no `internalMcpReferenceIds` in any export format
- [ ] M4.6.2 Create unit test that verifies no system prompts in any export format
- [ ] M4.6.3 Create unit test that verifies disclaimer present in all export formats
- [ ] M4.6.4 Audit DOCX document properties for hidden metadata
- [ ] M4.6.5 Audit PDF metadata for hidden information

**Acceptance**: Export all 4 formats; security audit passes; render modes work correctly

---

## Milestone 5: MCP-Only Enforcement

**Goal**: Ensure all content derives exclusively from Microsoft Learn MCP server.

**Acceptance Criteria**:
- System prompt explicitly forbids non-MCP sources
- Generation fails gracefully if MCP returns insufficient content
- No fallback to pretrained knowledge
- MCP usage tracked in telemetry
- Internal MCP reference IDs stored but never exported

### 5.1 MCP Client Implementation

- [ ] M5.1.1 Create `IMcpClient` interface with `QueryAsync(topics, keywords)`
- [ ] M5.1.2 Implement `McpClient` in `backend/src/AIExamForge.Infrastructure/MCP/McpClient.cs`
- [ ] M5.1.3 Configure MCP endpoint and authentication
- [ ] M5.1.4 Implement request timeout (10 seconds per FR-033)
- [ ] M5.1.5 Implement retry with exponential backoff (1 retry)

### 5.2 MCP Content Model

- [ ] M5.2.1 Create `McpContent` model: documentId, title, url, contentChunk, retrievedAt
- [ ] M5.2.2 Create `McpRetrievalResult` with list of `McpContent` and `insufficientContentNotes`
- [ ] M5.2.3 Track MCP document IDs for internal citation (stored in `Question.InternalMcpReferenceIds`)

### 5.3 MCP-Only Prompt Enforcement

- [ ] M5.3.1 Create `prompts/system-mcp-only.md` with explicit instructions:
  ```
  You MUST use ONLY the provided Microsoft Learn content.
  Do NOT use pretrained knowledge, web search, or external documentation.
  If the provided content is insufficient, respond with INSUFFICIENT_CONTENT marker.
  Cite MCP document IDs when making factual claims.
  ```
- [ ] M5.3.2 Inject MCP content into prompt as context block
- [ ] M5.3.3 Detect `INSUFFICIENT_CONTENT` marker in LLM response
- [ ] M5.3.4 On insufficient content: return 422 with explanation, do NOT generate

### 5.4 Graceful Failure Handling

- [ ] M5.4.1 If MCP timeout: retry once, then return error with retry guidance
- [ ] M5.4.2 If MCP returns empty for topic: add to `insufficientContentNotes`, continue with other topics
- [ ] M5.4.3 If ALL topics return empty: return 422 "Unable to generate - insufficient content in Microsoft Learn"
- [ ] M5.4.4 Error messages must explicitly state MCP limitation (per NFR-002)

### 5.5 MCP Telemetry

- [ ] M5.5.1 Track `mcp_request_count` metric
- [ ] M5.5.2 Track `mcp_response_latency_ms` histogram (p50, p95, p99)
- [ ] M5.5.3 Track `mcp_cache_hit_rate` metric (per-request caching)
- [ ] M5.5.4 Track `mcp_empty_result_count` metric
- [ ] M5.5.5 Track `mcp_error_count` by error type
- [ ] M5.5.6 Log MCP requests with correlation IDs

### 5.6 MCP Caching

- [ ] M5.6.1 Implement per-request MCP response cache (avoid duplicate calls for same content)
- [ ] M5.6.2 Cache key: topic + keywords hash
- [ ] M5.6.3 Cache lifetime: request duration only (no cross-request caching in v1)

**Acceptance**: MCP-only prompt in place; graceful failures; telemetry dashboards show MCP metrics

---

## Milestone 6: Security, Authentication & Rate Limiting

**Goal**: Production-grade security with Entra ID auth, rate limits, and input validation.

**Acceptance Criteria**:
- All endpoints require authentication (except /health)
- Rate limits enforced: 10 req/min, 100 questions/day
- All inputs validated server-side
- Secrets stored in Azure Key Vault
- Security headers configured

### 6.1 Authentication

- [ ] M6.1.1 Configure Microsoft Entra ID (Azure AD) in `appsettings.json`
- [ ] M6.1.2 Add JWT bearer authentication middleware
- [ ] M6.1.3 Create `AuthenticationExtensions.cs` for DI setup
- [ ] M6.1.4 Extract `UserId` from JWT claims for all operations
- [ ] M6.1.5 Return 401 for missing/invalid tokens
- [ ] M6.1.6 Test authentication with valid and invalid tokens

### 6.2 Rate Limiting

- [ ] M6.2.1 Create `RateLimitingMiddleware` in `backend/src/AIExamForge.Api/Middleware/`
- [ ] M6.2.2 Implement per-user rate limit: 10 generation requests per minute
- [ ] M6.2.3 Implement per-user daily limit: 100 questions per day
- [ ] M6.2.4 Store rate limit counters in distributed cache (Redis or in-memory for dev)
- [ ] M6.2.5 Return 429 with `Retry-After` header when limit exceeded
- [ ] M6.2.6 Include friendly error message with limit explanation

### 6.3 Input Validation

- [ ] M6.3.1 Validate outline text size ≤ 500 KB (FR-024)
- [ ] M6.3.2 Validate question count 1-50 per request (FR-024)
- [ ] M6.3.3 Validate exam name ≤ 256 characters (FR-024)
- [ ] M6.3.4 Validate item type codes against allowed list
- [ ] M6.3.5 Validate difficulty percentages sum to 100
- [ ] M6.3.6 Validate selected node IDs exist in outline
- [ ] M6.3.7 Return 400 with specific validation errors

### 6.4 Secrets Management

- [ ] M6.4.1 Configure Azure Key Vault integration
- [ ] M6.4.2 Store Azure OpenAI API key in Key Vault
- [ ] M6.4.3 Store MCP credentials in Key Vault
- [ ] M6.4.4 Store database connection string in Key Vault
- [ ] M6.4.5 Store Blob Storage connection string in Key Vault
- [ ] M6.4.6 No secrets in `appsettings.json` or environment variables

### 6.5 Security Headers

- [ ] M6.5.1 Add `X-Content-Type-Options: nosniff`
- [ ] M6.5.2 Add `X-Frame-Options: DENY`
- [ ] M6.5.3 Add `X-XSS-Protection: 1; mode=block`
- [ ] M6.5.4 Add `Strict-Transport-Security` (HSTS)
- [ ] M6.5.5 Configure CORS for frontend origin only

**Acceptance**: Auth required on all endpoints; rate limits work; security headers present; no secrets in code

---

## Milestone 7: Observability & Health Checks

**Goal**: Production observability with structured logging, health checks, and Application Insights.

**Acceptance Criteria**:
- /health endpoint returns dependency status
- All logs include correlation IDs
- Application Insights receives telemetry
- Custom metrics for generation success/failure

### 7.1 Health Checks

- [ ] M7.1.1 Create `/health` endpoint in `backend/src/AIExamForge.Api/Endpoints/HealthEndpoints.cs`
- [ ] M7.1.2 Check database connectivity
- [ ] M7.1.3 Check Azure Blob Storage connectivity
- [ ] M7.1.4 Check MCP service availability (lightweight ping)
- [ ] M7.1.5 Check Azure OpenAI service availability
- [ ] M7.1.6 Return `Healthy`, `Degraded`, or `Unhealthy` status
- [ ] M7.1.7 Include dependency details in response body

### 7.2 Structured Logging

- [ ] M7.2.1 Configure Serilog or Microsoft.Extensions.Logging with structured output
- [ ] M7.2.2 Create `CorrelationIdMiddleware` that generates/propagates correlation IDs
- [ ] M7.2.3 Include correlation ID in all log entries
- [ ] M7.2.4 Log generation requests: userId, outlineId, questionCount, itemTypes
- [ ] M7.2.5 Log generation results: success/failure, duration, questionCount
- [ ] M7.2.6 Log MCP calls: query, responseTime, resultCount, errors
- [ ] M7.2.7 Format logs for Azure Application Insights ingestion

### 7.3 Application Insights Integration

- [ ] M7.3.1 Add `Microsoft.ApplicationInsights.AspNetCore` package
- [ ] M7.3.2 Configure instrumentation key from Key Vault
- [ ] M7.3.3 Enable request telemetry
- [ ] M7.3.4 Enable dependency tracking (SQL, HTTP)
- [ ] M7.3.5 Enable exception telemetry

### 7.4 Custom Metrics

- [ ] M7.4.1 Track `generation_request_count` by status (success/failure)
- [ ] M7.4.2 Track `generation_duration_seconds` histogram
- [ ] M7.4.3 Track `questions_generated_count` by item type
- [ ] M7.4.4 Track `validation_failure_count` by validation type (schema/lint/item-type)
- [ ] M7.4.5 Track `export_count` by format
- [ ] M7.4.6 Track `active_users_daily` count

### 7.5 Alerting Setup

- [ ] M7.5.1 Create alert for error rate > 5%
- [ ] M7.5.2 Create alert for p95 latency > 45 seconds
- [ ] M7.5.3 Create alert for health check failures
- [ ] M7.5.4 Create alert for MCP error spike
- [ ] M7.5.5 Document alert runbook

**Acceptance**: /health returns correct status; logs have correlation IDs; App Insights shows metrics

---

## Milestone 8: Azure App Service Deployment

**Goal**: Deploy AIExamForge to Azure App Service with CI/CD.

**Acceptance Criteria**:
- App runs on Azure App Service (Linux container)
- CI/CD pipeline deploys on merge to main
- Environment-specific configuration works
- Startup health check passes

### 8.1 Infrastructure as Code

- [ ] M8.1.1 Create `infra/main.bicep` for all Azure resources
- [ ] M8.1.2 Define App Service Plan (Linux, P1v3 or higher)
- [ ] M8.1.3 Define App Service with container deployment
- [ ] M8.1.4 Define Azure SQL Database
- [ ] M8.1.5 Define Azure Blob Storage account with `temp-uploads` container
- [ ] M8.1.6 Define Azure Key Vault with access policies
- [ ] M8.1.7 Define Application Insights resource
- [ ] M8.1.8 Define Azure OpenAI resource (or reference existing)

### 8.2 Container Configuration

- [ ] M8.2.1 Create `Dockerfile` for backend API
- [ ] M8.2.2 Create `.dockerignore` to exclude unnecessary files
- [ ] M8.2.3 Configure multi-stage build for smaller image
- [ ] M8.2.4 Set non-root user for security
- [ ] M8.2.5 Expose port 8080
- [ ] M8.2.6 Test container locally with `docker build` and `docker run`

### 8.3 CI/CD Pipeline

- [ ] M8.3.1 Create `.github/workflows/ci.yml` for build and test
- [ ] M8.3.2 Create `.github/workflows/cd.yml` for deployment
- [ ] M8.3.3 Run `dotnet test` in CI
- [ ] M8.3.4 Run `npm test` for frontend in CI
- [ ] M8.3.5 Build and push container image to Azure Container Registry
- [ ] M8.3.6 Deploy to App Service on merge to `main`
- [ ] M8.3.7 Run smoke tests after deployment

### 8.4 Environment Configuration

- [ ] M8.4.1 Create `appsettings.Development.json` for local dev
- [ ] M8.4.2 Create `appsettings.Production.json` for production
- [ ] M8.4.3 Configure Key Vault references in App Service configuration
- [ ] M8.4.4 Set `ASPNETCORE_ENVIRONMENT=Production` in App Service
- [ ] M8.4.5 Configure connection strings via Key Vault

### 8.5 Startup Validation

- [ ] M8.5.1 Configure App Service health check path to `/health`
- [ ] M8.5.2 Set startup probe timeout to 60 seconds
- [ ] M8.5.3 Verify all dependencies connect on startup
- [ ] M8.5.4 Log startup success/failure with configuration summary
- [ ] M8.5.5 Verify ItemTypeRegistry loads all 19 types on startup

### 8.6 Deployment Verification

- [ ] M8.6.1 Verify /health returns 200 OK
- [ ] M8.6.2 Verify authentication works (get token, call protected endpoint)
- [ ] M8.6.3 Verify database migrations applied
- [ ] M8.6.4 Verify Blob Storage accessible
- [ ] M8.6.5 Verify MCP connectivity
- [ ] M8.6.6 Generate test question set end-to-end

**Acceptance**: App deployed; CI/CD runs; health check green; end-to-end generation works

---

## Summary

| Milestone | Tasks | Focus Area |
|-----------|-------|------------|
| M0 | 24 | Authoritative Specification Files |
| M1 | 21 | Lint Validation Engine |
| M2 | 23 | Per-Item-Type Validators |
| M3 | 20 | File Upload & Document Processing |
| M4 | 22 | Export Generation |
| M5 | 18 | MCP-Only Enforcement |
| M6 | 22 | Security, Auth & Rate Limiting |
| M7 | 19 | Observability & Health Checks |
| M8 | 22 | Azure App Service Deployment |
| **Total** | **191** | |

---

## Execution Order

```
M0 (Specs) ──────────────────────────────────────────────────────────┐
                                                                      │
M1 (Lint) + M2 (Validators) + M5 (MCP) ◄──────────────────────────────┤ Can parallelize
                                                                      │
M3 (Upload) + M4 (Export) ◄───────────────────────────────────────────┤ Can parallelize
                                                                      │
M6 (Security) + M7 (Observability) ◄──────────────────────────────────┤ Can parallelize
                                                                      │
M8 (Deployment) ◄─────────────────────────────────────────────────────┘ After all above
```

---

**Generated**: 2026-01-03 | **Status**: Milestone Planning Complete
