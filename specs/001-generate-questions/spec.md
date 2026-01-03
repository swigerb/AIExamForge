# Feature Specification: Generate Practice Exam Questions from Outline

**Feature Branch**: `001-generate-questions`  
**Created**: 2026-01-02  
**Updated**: 2026-01-03  
**Status**: Draft  
**Input**: User description: "AIExamForge: Generate original practice exam questions grounded exclusively in official Microsoft Learn documentation retrieved via the Microsoft Learn MCP server. Users input exam outlines by pasting text OR uploading DOCX/PDF. System extracts text, parses to outline hierarchy, and lets user select a section/subsection. User chooses question count and item types by short code (M1, BA, DA, SC, DOMC, CHOOSE_N, AS, BL, MW, HA, DD, MG, GI, MS, PA, CA, AN, LABS, SA). Generated content must conform to docs/exam-writing-style-guide.md and docs/item-types.md. All question output must validate against docs/schemas/question.schema.json. Microsoft Learn MCP server is the only source of factual information. Exam Mode export excludes rationales; Study Mode includes rationales in separate field/section. Downloads: JSON, Markdown, DOCX, PDF."

## User Scenarios & Testing *(mandatory)*

<!--
  IMPORTANT: User stories should be PRIORITIZED as user journeys ordered by importance.
  Each user story/journey must be INDEPENDENTLY TESTABLE - meaning if you implement just ONE of them,
  you should still have a viable MVP (Minimum Viable Product) that delivers value.
  
  Assign priorities (P1, P2, P3, etc.) to each story, where P1 is the most critical.
  Think of each story as a standalone slice of functionality that can be:
  - Developed independently
  - Tested independently
  - Deployed independently
  - Demonstrated to users independently
-->

### User Story 1 - Upload/Paste Exam Outline and Visualize Structure (Priority: P1)

A user provides an exam outline by either: (a) pasting plain text into the UI, (b) uploading a Word document (.docx), or (c) uploading a PDF (.pdf). The system parses and extracts text from the document, parses it into a hierarchical outline (sections/subsections), and displays it in an interactive tree. User can collapse/expand sections and select a section for question generation.

**Why this priority**: Outline parsing and input is a blocking prerequisite for question generation. Users must be able to provide outlines via multiple methods (text, file). This story delivers core value: flexible outline input and structured navigation of exam content.

**Independent Test**: Can be tested by (1) pasting text and parsing, (2) uploading a .docx and extracting text, (3) uploading a .pdf and extracting text, verifying the parsed tree renders correctly, and allowing selection of any leaf node. Test malformed/encrypted files with proper error messages.

**Acceptance Scenarios**:

1. **Given** user pastes a well-formed outline with 3+ levels of nesting, **When** system parses it, **Then** hierarchical tree renders with section names and nesting depth preserved.
2. **Given** user uploads a .docx file, **When** system extracts text and parses it, **Then** text is correctly extracted, outline hierarchy is rendered, and user can select sections.
3. **Given** user uploads a .pdf file, **When** system extracts text and parses it, **Then** text is correctly extracted, outline hierarchy is rendered, and user can select sections.
4. **Given** user uploads an encrypted or password-protected PDF, **When** system attempts extraction, **Then** displays user-friendly error: "Password-protected PDFs are not supported in v1. Please remove the password or provide a plaintext/Word document."
5. **Given** user uploads a file larger than size limit, **When** system validates upload, **Then** rejects with error: "File too large. Max [limit] MB allowed."
6. **Given** user hovers over a section node, **When** UI displays summary info, **Then** shows section title and question count estimate (if available).
7. **Given** user clicks a subsection node, **When** section is selected, **Then** UI highlights selection and enables next step (question generation config).
8. **Given** user pastes malformed or empty outline, **When** system attempts parse, **Then** displays user-friendly error with guidance. Max outline size: 500 KB.

---

### User Story 2 - Configure Question Generation Parameters (Priority: P1)

User selects generation parameters: number of questions, question types (with checkboxes), and optional difficulty distribution and tone profile. System validates inputs and displays configuration summary before generation.

**Why this priority**: Required to move from outline selection to question generation. Unblocks the core generation flow.

**Independent Test**: Can be tested by selecting a section, configuring various parameter combinations (e.g., 5 questions, 2 types, difficulty easy/medium), and verifying validation and UI state.

**Acceptance Scenarios**:

1. **Given** user has selected a section, **When** user opens generation dialog, **Then** form displays exam name field, question count input (1-50), question type checkboxes, difficulty slider, and tone dropdown.
2. **Given** user selects "auto mix" for question types, **When** user configures, **Then** system disables individual type checkboxes and applies default distribution on generation.
3. **Given** user enters question count exceeding limit, **When** user submits form, **Then** validation error appears with max limit and helpful message.
4. **Given** user configures parameters, **When** user clicks "Generate", **Then** system displays config summary and initiates generation (starts Story 3).

---

### User Story 3 - Generate Question Set with Progress Tracking (Priority: P1)

User clicks Generate. System sends request to backend, which generates questions according to configuration. UI shows progress indicator/streaming updates during generation. Completed question set is displayed in a structured view with all required metadata.

**Why this priority**: Core value delivery. Generates the actual questions users need. Streaming/progress ensures good UX for potentially long-running operation.

**Independent Test**: Can be tested by generating a small question set (e.g., 3-5 questions, 2 types), verifying all questions contain required metadata, and checking that explanations are present and non-empty.

**Acceptance Scenarios**:

1. **Given** valid generation request submitted, **When** server begins processing, **Then** UI shows progress indicator (e.g., "Generating 1/5") and updates in real-time (streaming or polling).
2. **Given** generation completes, **When** question set is ready, **Then** UI displays questions in list view; each question shows ID, type, section tag, difficulty, question text, correct answer(s), and explanation.
3. **Given** user has generated a set, **When** user reviews question, **Then** can see: unique ID (e.g., Q001), question type label, objective/section tags, difficulty (easy/medium/hard), full question content, correct answer(s) formatted per type, and explanation/rationale.
4. **Given** generation fails (e.g., timeout, API error), **When** error occurs, **Then** UI displays user-friendly error message with retry button and guidance (e.g., "Generation took longer than expected. Try fewer questions or simpler types.").
5. **Given** generation succeeds, **When** user views set, **Then** all questions validate against their question type schemas (backend check) and include non-empty explanations.

---

### User Story 4 - Review, Regenerate, and Edit Questions (Priority: P2)

User reviews generated questions. Can regenerate the full set, regenerate specific questions, or manually edit question text/answers. Changes are reflected immediately in the working set.

**Why this priority**: Enhances usability and user control. Not blocking MVP, but essential for adoption. Users need ability to refine output.

**Independent Test**: Can be tested by generating a set, manually editing one question's text, verifying edit persists in the set, and regenerating that single question to verify it is replaced with a new one.

**Acceptance Scenarios**:

1. **Given** user has generated a question set, **When** user clicks "Regenerate All", **Then** system re-runs generation with same parameters and replaces the entire set.
2. **Given** user views a specific question, **When** user clicks "Regenerate This", **Then** system regenerates only that question, preserves section/type/difficulty, and updates the set.
3. **Given** user views a question, **When** user clicks "Edit", **Then** UI opens inline editor with question text, answers, and explanation fields; user can modify; changes persist in the working set.
4. **Given** user edits and then regenerates that question, **When** user confirms regenerate, **Then** system discards manual edit and generates a fresh question with same parameters.

---

### User Story 5 - Export Question Set in Multiple Formats (Priority: P2)

User can export the question set in JSON, Markdown, Word (.docx), and PDF (.pdf) formats. All exports include complete metadata, answers, and explanations. Word and PDF exports include professional formatting with title page, formatted questions, and visible disclaimers.

**Why this priority**: Enables user to use questions outside the app (e.g., in learning platforms, printed materials, Word documents). Adds significant value without blocking core flow. Word/PDF support is critical for non-technical users and print workflows.

**Independent Test**: Can be tested by generating a set, exporting to all four formats (JSON, Markdown, Word, PDF), verifying content integrity, and confirming no internal MCP references appear in any export. Verify Word/PDF have title page and proper formatting.

**Acceptance Scenarios**:

1. **Given** user has generated a question set, **When** user clicks "Export as JSON", **Then** system downloads a .json file containing all questions, metadata, and configuration used for generation.
2. **Given** user has generated a question set, **When** user clicks "Export as Markdown", **Then** system downloads a .md file with questions rendered in human-readable format with sections, type labels, difficulty, and explanations.
3. **Given** user has generated a question set, **When** user clicks "Export as Word", **Then** system downloads a .docx file with: title page (exam name, section, generation date), formatted questions with type labels/difficulty, correct answers, detailed explanations, visible disclaimer, and a table of contents (optional v1). No internal MCP reference IDs included.
4. **Given** user has generated a question set, **When** user clicks "Export as PDF", **Then** system downloads a .pdf file matching the Word export content, with consistent formatting, embedded disclaimer, and no internal MCP references.
5. **Given** user exports a set in any format, **When** export file is created, **Then** file includes: exam name, section/objective, generation timestamp, all questions with IDs/types/difficulty/explanations, and a prominent disclaimer stating "These are AI-generated original practice questions, not official Microsoft exam items."
6. **Given** exported Word/PDF files are created, **When** file is examined, **Then** no internal MCP reference IDs, internal prompts, or secrets are included in the output.

---

### User Story 6 - Display Content Policy Disclaimers (Priority: P1)

App displays prominent disclaimers and guardrails stating that generated questions are original content, distinct from real Microsoft exam items, and intended for practice only.

**Why this priority**: Mandatory legal/ethical requirement per constitution. Protects from liability and sets user expectations.

**Independent Test**: Can be tested by verifying disclaimer text is visible on generation config screen, in exported files, and in a help/about section.

**Acceptance Scenarios**:

1. **Given** user opens the app, **When** any page loads, **Then** a visible disclaimer states: "These questions are AI-generated original practice questions, not official Microsoft exam items. For official exam preparation, use Microsoft Learn and official practice exams."
2. **Given** user initiates generation, **When** generation config dialog appears, **Then** disclaimer restates original content guarantee and links to help.
3. **Given** user exports a set, **When** export file is created, **Then** file header includes disclaimer language.
4. **Given** user navigates to Help/About, **When** page loads, **Then** full content policy is explained: no reproduction of real exam items, original content only, intended for study/practice.

---

### Edge Cases

- What happens if outline is extremely large (>100 KB) or deeply nested (>10 levels)? → Reject with friendly error and size/depth limit guidance.
- What if user requests 100+ questions in one go? → Reject with per-request cap and guidance on limits.
- What if generation times out or partially completes? → Display timeout error, offer retry with fewer questions or simpler types.
- What if a question fails schema validation after generation? → Log the failure, display warning to user, suggest regenerating that question.
- What if user navigates away or closes browser during generation? → Prevent loss by saving draft state (backend); user can resume on return.
- What if the same outline section is generated multiple times? → Each generation should produce different questions (no caching/replay).

## Requirements *(mandatory)*

### Functional Requirements

**Outline Parsing & Interaction**

- **FR-001**: System MUST parse plain-text exam outlines and extract hierarchical structure (sections, subsections, leaf nodes).
- **FR-002**: System MUST validate outline format (max size 500 KB, max nesting depth 10 levels).
- **FR-003**: System MUST render parsed outline as an interactive tree in the UI with expand/collapse and node selection.
- **FR-004**: Users MUST be able to select any section or subsection for question generation.

**File Upload & Document Processing**

- **FR-001a**: System MUST support multiple outline input methods: (a) text paste in UI, (b) .docx file upload, (c) .pdf file upload.
- **FR-001b**: System MUST validate uploaded files: enforce file size limits (max 10 MB), validate content-type (only .docx and .pdf), and reject unsupported formats with clear error messages.
- **FR-001c**: System MUST extract text from uploaded .docx files using standard document parsing (no macro execution, no embedded object trust).
- **FR-001d**: System MUST extract text from uploaded .pdf files using PDF text extraction. Reject password-protected or encrypted PDFs with user-friendly message: "Password-protected PDFs are not supported in v1. Please remove the password or provide a plaintext or Word document."
- **FR-001e**: System MUST scan uploaded files for malware in production environments (integrate with a scanning service/step; skip in dev/test environments for speed).
- **FR-001f**: System MUST store uploaded files temporarily during processing and delete them immediately after text extraction, unless user explicitly saves them. Temporary files MUST not persist longer than 1 hour.
- **FR-001g**: System MUST display clear, helpful errors for all file upload failures (unsupported format, file too large, encrypted PDF, text extraction failure, malware detected) with recovery guidance.

**Question Generation Configuration**

- **FR-005**: System MUST support input fields: exam name (optional), target section (required), question count (1 to 50), question types (checkboxes by short code: M1, BA, DA, SC, DOMC, CHOOSE_N, AS, BL, MW, HA, DD, MG, GI, MS, PA, CA, AN, LABS, SA, or "auto mix"), difficulty distribution (optional slider: easy/medium/hard), tone profile (optional dropdown: e.g., concise, professional, exam-like).
- **FR-006**: System MUST validate all user inputs server-side. Reject requests exceeding per-request caps or with invalid parameters.
- **FR-007**: System MUST display validation errors to users with specific guidance (e.g., "Max 50 questions per request; you requested 100").
- **FR-008**: System MUST support "auto mix" mode: when selected, system applies default question-type distribution and ignores individual type checkboxes.

**Question Generation & Delivery**

- **FR-009**: System MUST generate questions according to configuration (section, count, types, difficulty, tone). Each question MUST:
  - Contain a unique ID (e.g., Q001).
  - Include question type (e.g., "multiple-choice", "short-answer").
  - Include objective/section tags matching the selected outline section.
  - Include difficulty level (easy, medium, hard) as requested or inferred.
  - Contain question content in type-specific JSON schema.
  - Include correct answer(s) in type-specific format.
  - Include explanation/rationale (non-empty).
- **FR-010**: System MUST validate every generated question against its question-type schema before returning to user.
- **FR-011**: System MUST provide real-time progress updates during generation (e.g., "Generating 2/5"). Use streaming, polling, or WebSocket as appropriate.
- **FR-012**: System MUST support regeneration of the entire question set (restart with same config) and regeneration of individual questions.
- **FR-013**: Users MUST be able to manually edit question text, answers, and explanations in the UI; edits persist in the working set.

**Content Policy & Guardrails**

- **FR-014**: System MUST generate only original, novel questions. Never reproduce real Microsoft exam items or proprietary content.
- **FR-015**: Prompt templates MUST include guardrail language: e.g., "Generate original practice questions that differ from real certification exams. Avoid reproducing actual exam items."
- **FR-016**: System MUST display prominent disclaimers on the UI (generation config, exported files, help section) stating questions are AI-generated originals, distinct from official exams.

**Microsoft Learn MCP Integration (NON-NEGOTIABLE)**

- **FR-029**: System MUST derive ALL exam-related content ONLY from the Microsoft Learn MCP server. Pretrained knowledge, web search, and external documentation are prohibited.
- **FR-030**: All generation prompts MUST explicitly instruct the model to use ONLY Microsoft Learn MCP-provided content and to cite MCP document identifiers when making claims.
- **FR-031**: System MUST validate that generated content is grounded in MCP responses. If MCP returns insufficient information, system MUST:
  - Fail gracefully with a clear, user-facing error message explaining the limitation (e.g., "Content for [topic] not available in Microsoft Learn documentation"), OR
  - Request user clarification and defer generation.
  - System MUST NEVER infer, guess, or fabricate content outside MCP responses.
- **FR-032**: If content from MCP conflicts with user input or other sources, MCP content takes precedence. System MUST NOT override or supplement MCP responses with external knowledge.
- **FR-033**: System MUST track MCP response times, cache hit rates, empty results, and errors in structured logs (correlation IDs). These metrics MUST be exposed in telemetry.
- **FR-034**: README MUST document the MCP dependency, limitations (e.g., "questions are limited to content in Microsoft Learn"), failure modes, and retry guidance.

**Export & Data Persistence**

- **FR-017**: Users MUST be able to export question sets in JSON format (all metadata, answers, explanations included).
- **FR-018**: Users MUST be able to export question sets in Markdown format (human-readable, with sections, difficulty labels, and explanations).
- **FR-019**: Exported files MUST include exam metadata (name, section, generation timestamp, disclaimer).
- **FR-020**: System MUST persist working question set state (e.g., in session storage or via draft saves) to recover from navigational loss.
- **FR-020a**: Users MUST be able to export question sets in Word format (.docx) with: title page (exam name, section, generation date), table of contents (optional v1), formatted questions with type labels, difficulty indicators, correct answers, and detailed explanations. Consistent formatting and professional layout required.
- **FR-020b**: Users MUST be able to export question sets in PDF format (.pdf) with content matching the Word export as closely as possible. PDF must include title page, formatted questions, answers, explanations, and visible disclaimer.
- **FR-020c**: All exported files (JSON, Markdown, Word, PDF) MUST include a visible disclaimer stating: "These are AI-generated original practice questions, not official Microsoft exam items. For official exam preparation, use Microsoft Learn and official practice exams."
- **FR-020d**: Exported Word and PDF files MUST NOT include internal MCP reference IDs, internal generation prompts, secrets, or any hidden/metadata that could leak system implementation details.

**Security & Compliance**

- **FR-021**: System MUST authenticate all users (Microsoft Entra ID via OpenID Connect).
- **FR-022**: System MUST enforce per-user rate limits on generation requests (e.g., max 10 requests per minute, max 100 questions per day).
- **FR-023**: System MUST validate all user inputs server-side (outline size, question count, parameter ranges).
- **FR-024**: System MUST implement input size limits (outline: 500 KB, question count: 50 per request, exam name: 256 chars).
- **FR-025**: System MUST log generation requests with correlation IDs for auditing and debugging.

**Health & Observability**

- **FR-026**: System MUST expose a /health endpoint for App Service monitoring (returns 200 OK if healthy, details on DB/dependencies and MCP service availability).
- **FR-027**: System MUST use structured logging with correlation IDs; logs must be App Insights compatible.
- **FR-028**: System MUST track telemetry: request count, success rate, generation latency (p50, p95, p99), MCP response times, MCP cache hits/misses, error types, and per-user quota usage.

### Non-Functional Requirements (from Project Description)

**Technology Constraints (Implementation Details)**

- **NFR-000a**: Backend MUST be built with .NET 10 LTS (ASP.NET Core Minimal APIs).
- **NFR-000b**: Frontend preference is React TypeScript (not mandatory but strongly preferred).
- **NFR-000c**: Hosting platform is Azure App Service.

**Security & Compliance**

- **NFR-001**: Production-grade security on Azure App Service (secrets in Key Vault, auth via Entra ID, input validation, rate limiting).
- **NFR-002**: Friendly error messages for all failure scenarios (outline too large, question count exceeded, generation timeout, MCP unavailable, MCP insufficient data, etc.). MCP-specific errors MUST state the limitation explicitly: e.g., "Content for [topic] not available in Microsoft Learn documentation." No silent fallback to model knowledge.
- **NFR-003**: Generation performance: support streaming or incremental status updates to keep UX responsive (e.g., show progress every 1-2 seconds).
- **NFR-004**: Cost controls: enforce per-request caps (50 questions max per generation) and per-user limits (100 questions max per day) to manage AI service costs and MCP API load. System MUST also cache MCP responses per generation request to avoid duplicate API calls for the same content.
- **NFR-005**: Accessibility: UI must meet WCAG 2.1 AA standards (keyboard navigation, ARIA labels, color contrast, screen reader support).
- **NFR-006**: Responsiveness: UI must adapt to mobile, tablet, and desktop viewports.
- **NFR-007**: MCP Integration (NON-NEGOTIABLE): All content generation MUST be grounded in Microsoft Learn MCP. The system MUST fail gracefully if MCP is unavailable or returns insufficient data. README MUST document MCP dependency, limitations, failure modes (e.g., timeout, empty results), and retry guidance. MCP is the single source of truth; system MUST NOT infer, guess, or supplement with external knowledge.
- **NFR-008**: File Handling Security: (a) Uploaded files must be validated for size, content-type, and malware before processing. (b) No macros executed; no embedded objects trusted. (c) Encrypted/password-protected PDFs rejected with clear messaging. (d) Temporary files deleted immediately after processing. (e) Exported Word/PDF files must not contain internal MCP references, secrets, or hidden metadata. (f) File scanning integrated in production; skipped in dev/test for speed.

**Observability Requirements**

- **NFR-009**: System MUST expose a /health endpoint for Azure App Service health checks. Returns 200 OK if healthy, includes dependency status (database, MCP service).
- **NFR-010**: System MUST use structured logging with correlation IDs. Logs MUST be compatible with Azure Application Insights.
- **NFR-011**: System MUST integrate with Azure Application Insights for telemetry (request metrics, dependency tracking, custom events for generation success/failure).

**Exam Authoring Governance (NON-NEGOTIABLE)**

- **NFR-012**: Exam item generation MUST comply with:
  - `docs/exam-writing-style-guide.md` (human interpretation)
  - `docs/exam-writing-style-rules.md` (machine enforcement)
  - `docs/exam-writing-traceability.md` (auditability)
- **NFR-013**: All generated exam items MUST validate against:
  - `docs/schemas/question.schema.json`
  - The corresponding item-type schema (`docs/schemas/item-types/[CODE].schema.json`)
- **NFR-014**: If any rule fails, generation MUST fail or regenerate. Silent correction is NOT allowed. The system MUST return a structured error explaining which rule failed (rule ID, description, violating content).

### Question Type System

- **FR-035**: System MUST support a pluggable question-type architecture. Each question type MUST define: (a) writing rules (e.g., number of options, complexity constraints), (b) JSON schema for content validation, (c) validation logic for correctness checks. Example types: multiple-choice (4-5 options, single correct), multiple-select (4+ options, 2+ correct), short-answer (open-ended text), drag-and-drop (item mapping with constraints), scenario-based (multi-part with context).
- **FR-036**: At v1 launch, system MUST support all 19 item types defined in `docs/item-types.md`: M1 (Multiple Choice Select One), BA (Best Answer), DA (Dynamic Answer Choice), SC (Sentence Correction), DOMC (Dynamic Option Multiple Choice), CHOOSE_N (Multiple-Alphabetic/Choose n), AS (Active Screen), BL (Build List), MW (Magnetic Words), HA (Hot Area), DD (Drag and Drop), MG (Matching), GI (Graphics Interpretation), MS (Multi-Source Reasoning), PA (Two-Part Analysis), CA (Code/Table Analysis), AN (Statement Analysis), LABS (Task-Based Labs), SA (Short Answer Code). Each type MUST include documented writing guidelines (in `prompts/item-types/[CODE].md`) and JSON schema (in `docs/schemas/item-types/[CODE].schema.json`) for generation prompts and validation.
- **FR-037**: Question type schemas and writing guidelines MUST be versioned in the repository and immutable by users. All schemas MUST be stored in a schema registry and referenced by generation prompts.
- **FR-038**: System MUST apply global exam-writing guidance to all question types: clarity (no ambiguous wording), no trick questions or unfair wording, plausible distractors for multiple-choice (grounded in common misconceptions), consistent tone and difficulty progression, accessibility (plain language, no jargon without definition).
- **FR-039**: All question-type schemas and writing guidelines MUST be grounded in Microsoft Learn best practices and referenced MCP documentation. System MUST maintain internal MCP reference IDs for each schema version, mapping guideline principles to specific Learn content (not exposed to end users).

**Item Type Codes & Schema Validation (NON-NEGOTIABLE)**

- **FR-040**: System MUST support ALL item types defined in `docs/item-types.md`. The following item type codes are required: M1 (Multiple Choice Select One), BA (Best Answer), DA (Dynamic Answer Choice), SC (Sentence Correction), DOMC (Dynamic Option Multiple Choice), CHOOSE_N (Multiple-Alphabetic/Choose n), AS (Active Screen), BL (Build List), MW (Magnetic Words), HA (Hot Area), DD (Drag and Drop), MG (Matching), GI (Graphics Interpretation), MS (Multi-Source Reasoning), PA (Two-Part Analysis), CA (Code/Table Analysis), AN (Statement Analysis), LABS (Task-Based Labs), SA (Short Answer Code).
- **FR-041**: Every generated question MUST include an `itemTypeCode` field containing one of the supported item type codes (e.g., `"itemTypeCode": "M1"`).
- **FR-042**: System MUST validate every generated question against the JSON schema for its item type (stored in `docs/schemas/item-types/[CODE].schema.json`). Questions that fail schema validation MUST NOT be returned to the user.
- **FR-043**: If schema validation fails, system MUST either: (a) regenerate the question up to 3 times using the same parameters, OR (b) return a structured error to the user explaining which validation rule failed (e.g., "M1 question requires exactly 1 correct answer; found 2").

**Exam Item Style Compliance (NON-NEGOTIABLE)**

- **FR-044**: All generated exam item text (stems, scenarios, instructions, answer choices, exhibit text) MUST comply with `docs/exam-writing-style-guide.md`.
- **FR-045**: System MUST enforce the following linting rules on all exam item text:
  - (a) **No contractions**: Contractions (e.g., "don't", "can't", "won't") are forbidden. System MUST reject or auto-correct contractions.
  - (b) **No explanations in item text**: Stems, scenarios, instructions, and answer choices MUST NOT contain explanations or rationales. Explanations are allowed only in a separate `explanation` field.
  - (c) **Numeric ordering preserved**: If answer choices contain numbers or numerical values, they MUST be presented in ascending numerical order. Randomization MUST be disabled for numeric choices.
- **FR-046**: Style compliance MUST follow the precedence defined in `docs/style-governance.md`: Exam Writing Style Guide governs exam item text; Microsoft Writing Style Guide governs UI/help/README.
- **FR-047**: If a generated question fails linting rules, system MUST either: (a) regenerate the question with corrected prompts (up to 3 retries), OR (b) return a structured error explaining which linting rule failed (e.g., "Contraction 'don't' found in stem; contractions are forbidden in exam items").
- **FR-048**: System MUST NOT persist, export, or display any question that fails item type schema validation or exam item style linting. Invalid questions are discarded before reaching the user.

**Exam Mode vs Study Mode (NON-NEGOTIABLE)**

- **FR-049**: System MUST support two distinct render modes for question output: Exam Mode and Study Mode.
- **FR-050**: **Exam Mode**: Questions are rendered WITHOUT rationales/explanations. The `explanation` field is omitted from all exports (JSON, Markdown, DOCX, PDF). Use for simulating real exam conditions.
- **FR-051**: **Study Mode**: Questions are rendered WITH rationales/explanations. The `explanation` field is included in a clearly separated section (not inline with question stem or answer choices). Use for learning and review.
- **FR-052**: User MUST be able to select render mode (Exam or Study) at export time via a UI toggle or dropdown. Default mode is Study Mode.
- **FR-053**: All four export formats (JSON, Markdown, DOCX, PDF) MUST respect the selected render mode:
  - **JSON**: In Exam Mode, omit the `explanation` property from each question object. In Study Mode, include `explanation` as a separate property.
  - **Markdown**: In Exam Mode, render questions without explanation sections. In Study Mode, render explanations in a dedicated "## Explanation" subsection per question or in a consolidated "Explanations" section at the end.
  - **DOCX/PDF**: In Exam Mode, exclude explanation text entirely. In Study Mode, render explanations in a visually distinct section (e.g., after each question or in an "Answer Key with Explanations" section at the end).
- **FR-054**: The render mode selection MUST be clearly labeled in the export UI: "Exam Mode (no rationales)" and "Study Mode (with rationales)".
- **FR-055**: Generated questions always store explanations internally (for regeneration/editing), but the render mode controls visibility at export time.

### Key Entities *(include if feature involves data)*

**Outline**
- A structured text document provided by user. Contains sections and subsections. Attributes: content (string), parseDate (timestamp), size (bytes).
- Parsed into a tree of OutlineNodes.

**OutlineNode**
- Represents a section or subsection in the outline. Attributes: id (UUID), parentId (nullable), title (string), level (int 1-10), children (list of OutlineNodes).

**GenerationRequest**
- Submitted by user. Attributes: id (UUID), userId (string), examName (string, optional), targetSectionId (UUID), questionCount (int 1-50), questionTypes (list of strings), difficulty (optional enum: easy/medium/hard), tone (optional string), timestamp (datetime), status (enum: pending, generating, completed, failed).

**Question**
- Generated by backend. Attributes: id (UUID, e.g., Q001), itemTypeCode (string, required, one of: M1/BA/DA/SC/DOMC/CHOOSE_N/AS/BL/MW/HA/DD/MG/GI/MS/PA/CA/AN/LABS/SA), type (string, human-readable name), sectionId (UUID), sectionTitle (string), difficulty (enum: easy/medium/hard), content (JSON object, type-specific, validated against `docs/schemas/item-types/[CODE].schema.json`), correctAnswer(s) (JSON object or array, type-specific), explanation (string, non-empty, referencing Learn-derived concepts, stored separately from item text; included or omitted based on render mode), generationRequestId (UUID), timestamp (datetime), internalMcpReferenceIds (list of strings, internal tracking of MCP sources used for generation, not shown to end users).

**QuestionSet**
- Container for a generation result. Attributes: id (UUID), generationRequestId (UUID, foreign key), questions (list of Question), exportFormats (list: json, markdown), lastModified (datetime).

## Success Criteria *(mandatory)*

### Measurable Outcomes

**User Capability**

- **SC-001**: A user can paste an exam outline, parse it, select a section, configure generation parameters (5-10 questions, 2 question types), and generate a complete question set in under 3 minutes (end-to-end, including generation time).
- **SC-001a**: A user can upload a .docx or .pdf file containing an exam outline, extract text, parse the outline, select a section, configure parameters, generate questions, and export results in Word or PDF format (end-to-end). Process completes successfully in under 5 minutes.
- **SC-001b**: A user can export a generated question set in all four formats (JSON, Markdown, Word, PDF) with complete metadata, correct answers, explanations, and visible disclaimers.
- **SC-002**: 100% of generated questions contain a unique ID, question type, section tag, difficulty, content, correct answer(s), and explanation.
- **SC-003**: 100% of generated questions validate against their question-type schema (zero schema violations).
- **SC-003a**: 100% of generated questions include a valid `itemTypeCode` matching one of the 19 supported types (M1, BA, DA, SC, DOMC, CHOOSE_N, AS, BL, MW, HA, DD, MG, GI, MS, PA, CA, AN, LABS, SA).
- **SC-003b**: 100% of generated exam item text (stems, scenarios, instructions, answer choices) contains zero contractions. System rejects or auto-corrects contractions before returning questions.
- **SC-003c**: 100% of generated questions have explanations/rationales stored ONLY in the `explanation` field, never embedded in stems, scenarios, instructions, or answer choices.
- **SC-003d**: 100% of numeric answer choices are presented in ascending numerical order; randomization is disabled for numeric choices.
- **SC-003e**: If schema validation or linting fails after 3 regeneration attempts, system returns a structured error to the user explaining the specific rule that failed. Zero invalid questions are persisted, exported, or displayed.
- **SC-004**: A user can export a question set in JSON, Markdown, Word, and PDF formats, and all exports include all required metadata, disclaimers, and proper formatting. Word and PDF exports include title page with exam name, section, and generation date.

**Exam Mode vs Study Mode**

- **SC-004a**: In Exam Mode exports, 100% of questions have NO explanation/rationale visible in the output (JSON omits `explanation` property; Markdown/DOCX/PDF omit explanation text).
- **SC-004b**: In Study Mode exports, 100% of questions have explanations rendered in a clearly separated section (not inline with stem or answer choices).
- **SC-004c**: User can switch between Exam Mode and Study Mode at export time without regenerating questions. Toggle is clearly labeled and defaults to Study Mode.

**File Handling & Export Quality**

- **SC-010a**: File uploads (.docx, .pdf) are validated for size, content-type, and malware. Unsupported files, encrypted PDFs, and oversized files are rejected with clear error messages (no silent failures). Text extraction succeeds for all valid files.
- **SC-010b**: Generated Word and PDF exports contain zero internal MCP reference IDs, internal generation prompts, or hidden metadata. Exports are "clean" and safe for sharing.
- **SC-010c**: Temporary files uploaded by users are deleted within 1 hour after processing, unless user explicitly saves them.
- **SC-010d**: Word and PDF exports have professional formatting with title page, properly formatted questions, visible disclaimer, and consistent styling across all documents.

**MCP Compliance & Content Integrity**

- **SC-005**: 100% of generated questions are grounded in Microsoft Learn MCP responses. Every generated question can be traced to a specific MCP document or API response (verified via internal citation logs).
- **SC-006**: If MCP returns insufficient information for a generation request, system fails gracefully with a user-facing error message (not a hallucination). Error message explains the limitation and suggests alternatives (e.g., broader outline topic, different exam).
- **SC-007**: Zero instances of content synthesized outside MCP responses. All factual claims, code examples, service limits, and behaviors originate from MCP. Verified via prompt audits and logs.
- **SC-008**: MCP response latency p95 <= 10 seconds. If MCP latency exceeds timeout, system returns user-friendly error with retry option.
- **SC-009**: MCP failures (timeouts, errors, empty results) are logged with correlation IDs and tracked in telemetry. No silent failures.

**Reliability & Performance**

- **SC-011**: Generation latency: p95 <= 45 seconds for a 10-question request (multi-type, standard difficulty distribution). Timeout occurs at 60 seconds; user receives friendly error and retry option.
- **SC-012**: System handles 100 concurrent generation requests without degradation; response times remain within p95 threshold.
- **SC-013**: UI responsiveness: progress updates appear at least every 2 seconds during generation (streaming or polling). No frozen UI.
- **SC-014**: 99.9% uptime (measured monthly) for the /health endpoint and auth flows.

**Quality & Security**

- **SC-015**: 100% of generated questions are original (non-plagiarized) and distinct from real Microsoft exam items. [Achieved via prompt guardrails and manual spot-checks.]
- **SC-016**: 100% of requests are authenticated; unauthorized requests receive 401 response.
- **SC-017**: Per-user rate limits are enforced: max 10 requests/minute, max 100 questions/day. Requests exceeding limits receive 429 response with friendly error.
- **SC-018**: All user input (text, file uploads) is validated server-side; requests with invalid parameters receive 400 response with specific error message.

**User Satisfaction**

- **SC-019**: 90% of users successfully complete a full question generation workflow on first attempt (paste/upload outline → select section → configure → generate → export).
- **SC-020**: Accessibility: UI supports keyboard navigation, screen readers, and WCAG 2.1 AA color contrast. At least 2 manual accessibility audits per release.

### Assumptions

- Outline parsing relies on heuristic indentation/section markers; users provide reasonably well-structured outlines. (Malformed outlines are rejected with guidance.)
- Question generation latency depends on backend LLM service availability and load. Per-request caps (50 questions) are set to keep p95 latency <= 45 seconds.
- Rate limits (10 req/min, 100 questions/day) are conservative to manage costs and prevent abuse. Can be adjusted post-launch based on usage patterns.
- "Original content" is ensured by prompt guardrails and manual spot-checks; no automated plagiarism detection is in scope for v1.
- UI accessibility is a "shift-left" priority; full WCAG 2.1 AA compliance is required, tested with automated tools and manual audit.

### Out of Scope (v1)

- Payments/subscriptions (user accounts are free).
- Enterprise multi-tenant features beyond basic user isolation (single organization assumed).
- Advanced question-type customization by users (schema is fixed per launch).
- Collaborative editing (single-user per session).
- Question set versioning/history (users can regenerate but not browse prior sets).
