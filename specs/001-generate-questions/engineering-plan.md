# Engineering Plan: AIExamForge

**Feature**: 001-generate-questions  
**Date**: 2026-01-03  
**Status**: Architecture Design  
**Spec**: [spec.md](./spec.md) | **Data Model**: [data-model.md](./data-model.md) | **API Contract**: [contracts/openapi.yaml](./contracts/openapi.yaml)

---

## 1. Authoritative Contracts

All implementation MUST conform to these authoritative files. Code is derived from specs; specs are not derived from code.

| Contract File | Purpose | Governance |
|---------------|---------|------------|
| `docs/exam-writing-style-guide.md` | Exam item writing standards (no contractions, no explanations in stems, numeric ordering) | Style linter rules derived from this |
| `docs/item-types.md` | 19 item type definitions with constraints | Registry metadata and schema constraints |
| `docs/style-governance.md` | Precedence rules between style guides | Lint rule priority |
| `docs/schemas/question.schema.json` | Master schema for all questions | Schema validator entry point |
| `docs/schemas/common/*.schema.json` | Shared components (difficulty, metadata) | Referenced by master schema |
| `docs/schemas/item-types/*.schema.json` | Per-type validation (M1, CHOOSE_N, DD, etc.) | Type-specific structure validation |
| `prompts/system-mcp-only.md` | MCP-exclusive content sourcing prompt | LLM system prompt |
| `prompts/exam-style.md` | Exam tone and formatting instructions | LLM user prompt fragment |
| `prompts/item-types/*.md` | Per-type generation instructions | Type-specific prompt fragments |
| `specs/001-generate-questions/contracts/openapi.yaml` | API contract | Request/response schemas |

**Change Control**: Any change to authoritative files requires corresponding updates to derived code. CI MUST verify consistency.

---

## 2. Generation Pipeline

The question generation pipeline is a sequential flow with validation gates. Each stage has explicit inputs, outputs, and failure modes.

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                           GENERATION PIPELINE                                    │
└─────────────────────────────────────────────────────────────────────────────────┘

    ┌──────────────┐     ┌──────────────┐     ┌──────────────┐     ┌──────────────┐
    │   OUTLINE    │     │     MCP      │     │   PROMPT     │     │     LLM      │
    │   PARSING    │────▶│  RETRIEVAL   │────▶│  ASSEMBLY    │────▶│  GENERATION  │
    └──────────────┘     └──────────────┘     └──────────────┘     └──────────────┘
           │                    │                    │                    │
           ▼                    ▼                    ▼                    ▼
    OutlineNode[]         McpContent[]         PromptPayload        RawJsonOutput
                                                                         │
    ┌──────────────┐     ┌──────────────┐     ┌──────────────┐          │
    │    EXPORT    │◀────│   PERSIST    │◀────│    LINT      │◀─────────┘
    │  (on demand) │     │              │     │  VALIDATION  │
    └──────────────┘     └──────────────┘     └──────────────┘
           │                    │                    │
           ▼                    ▼                    ▼
    File (JSON/MD/        QuestionSet          LintResult
    DOCX/PDF)             (database)           (pass/fail)
                               ▲
                               │
                         ┌──────────────┐
                         │   SCHEMA     │
                         │  VALIDATION  │
                         └──────────────┘
                               │
                               ▼
                         ValidationResult
                         (pass/fail)
```

### 2.1 Stage: Outline Parsing

**Input**: Raw text (pasted) OR extracted text (from DOCX/PDF upload)

**Process**:
1. Detect outline structure via heading markers, numbering patterns, indentation
2. Build hierarchical tree of `OutlineNode` entities
3. Assign unique IDs to each node for selection reference

**Output**: `Outline` entity with nested `OutlineNode[]`

**Failure Modes**:
- Text exceeds 500 KB → 413 Payload Too Large
- Nesting exceeds 10 levels → 400 Bad Request with guidance
- No structure detected → 400 Bad Request with parse error

**Contract Reference**: `OutlineResponse` schema in `openapi.yaml`

---

### 2.2 Stage: MCP Retrieval

**Input**: Selected `OutlineNode` IDs + topic keywords extracted from node titles/content

**Process**:
1. For each selected node, construct MCP query from topic keywords
2. Call Microsoft Learn MCP server with query
3. Cache responses per-request to avoid duplicate calls
4. Track MCP document IDs for internal citation (not exported)

**Output**: `McpContent[]` - array of content chunks with source URLs and document IDs

**Failure Modes**:
- MCP timeout (>10s) → Retry once, then return partial results with note
- MCP empty result for topic → Skip topic, include in `insufficientContentNotes`
- All topics return empty → 422 Unprocessable Entity with explanation

**Non-Negotiable**: Content MUST come from MCP. No fallback to pretrained knowledge.

**Contract Reference**: Constitution Principle VI, `prompts/system-mcp-only.md`

---

### 2.3 Stage: Prompt Assembly

**Input**: 
- `McpContent[]` from retrieval
- Generation config (question count, item types, difficulty, tone)
- Selected `OutlineNode` metadata

**Process**:
1. Load system prompt from `prompts/system-mcp-only.md`
2. Load exam style instructions from `prompts/exam-style.md`
3. For each requested item type, load prompt fragment from `prompts/item-types/{CODE}.md`
4. Compose final prompt with:
   - System instructions (MCP-only, JSON output format)
   - MCP content as context (with citation IDs)
   - Item type constraints and examples
   - Quantity and difficulty distribution

**Output**: `PromptPayload` - fully assembled prompt ready for LLM

**Contract Reference**: `prompts/*.md` files

---

### 2.4 Stage: LLM Generation (Strict JSON Output)

**Input**: `PromptPayload`

**Process**:
1. Call Azure OpenAI with JSON mode enabled
2. Request structured output matching expected question format
3. Parse response as JSON array of question objects

**Output**: `RawJsonOutput` - array of question JSON objects (not yet validated)

**Failure Modes**:
- LLM timeout → 504 Gateway Timeout with retry guidance
- Invalid JSON → Log error, retry with clarified prompt (up to 3 attempts)
- `INSUFFICIENT_CONTENT` marker in response → Return 422 with MCP limitation message

**Contract Reference**: Response must conform to `Question` schema structure

---

### 2.5 Stage: Schema Validation

**Input**: `RawJsonOutput` - each question JSON object

**Process**:
1. Extract `itemTypeCode` from question
2. Look up type-specific schema from `docs/schemas/item-types/{CODE}.schema.json`
3. Validate question against schema using JSON Schema validator (NJsonSchema)
4. Collect all validation errors

**Output**: `SchemaValidationResult` per question
- `isValid`: boolean
- `errors`: array of schema violation details (path, message)

**What Schema Validation Catches**:
- Missing required fields (`stem`, `choices`, `correctAnswer`)
- Wrong data types (string vs array vs object)
- Array length constraints (min/max items)
- Enum value violations (difficulty must be Easy/Medium/Hard)
- Structural shape mismatches

**Failure Handling**:
- Schema failure → Regenerate question (up to 3 retries)
- Persistent failure → Return error to user with specific violation

**Contract Reference**: `docs/schemas/item-types/*.schema.json`

---

### 2.6 Stage: Lint Validation (Rule Validation)

**Input**: Schema-valid question JSON

**Process**:
1. Apply lint rules from `docs/exam-writing-style-guide.md`
2. Check all text fields (stem, choices, instructions, scenarios)
3. Collect violations with location and rule ID

**Output**: `LintValidationResult` per question
- `isValid`: boolean
- `violations`: array of `LintViolation` (rule, message, location, violatingText)

**What Lint Validation Catches (Beyond Schema)**:

| Rule ID | Description | Example Violation |
|---------|-------------|-------------------|
| `NO_CONTRACTIONS` | No contractions in exam text | "don't" → "do not" |
| `NO_EXPLANATIONS_IN_ITEM` | Explanations only in separate field | Rationale in stem text |
| `NUMERIC_ORDER` | Numeric choices in ascending order | [10, 5, 15, 2] → reject |
| `NO_TRUE_FALSE_M1` | M1 cannot be true/false style | Only 2 choices (True/False) |
| `PARALLEL_FORM` | Answer choices have parallel structure | Mixed verb forms |
| `ACRONYM_DEFINED` | Acronyms defined on first use | "Use AKS" without expansion |

**Failure Handling**:
- Lint failure → Regenerate with corrected prompt (up to 3 retries)
- Persistent failure → Return error with specific lint rule violation

**Contract Reference**: `docs/exam-writing-style-guide.md`, `docs/style-governance.md`

---

### 2.7 Stage: Persist

**Input**: Fully validated `Question[]`

**Process**:
1. Assign display IDs (Q001, Q002, ...)
2. Create `QuestionSet` entity linked to `GenerationRequest`
3. Store questions with all metadata including internal MCP reference IDs
4. Update generation status to `Completed`

**Output**: Persisted `QuestionSet` with ID

**Contract Reference**: `data-model.md` entity definitions

---

### 2.8 Stage: Export (On Demand)

**Input**: `QuestionSet` ID + export format + render mode (Exam/Study)

**Process**:
1. Load question set from database
2. Apply render mode:
   - **Exam Mode**: Omit `explanation` and `rationale` fields
   - **Study Mode**: Include explanations in designated section
3. Transform to requested format via format-specific exporter
4. Strip internal data (MCP reference IDs, validation metadata)

**Output**: File stream (JSON, Markdown, DOCX, or PDF)

**Contract Reference**: `openapi.yaml` export endpoint, FR-049 through FR-055 (Render Modes)

---

## 3. File Upload Handling

### 3.1 Upload Flow

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   RECEIVE   │     │  VALIDATE   │     │   EXTRACT   │     │    PARSE    │
│   UPLOAD    │────▶│    FILE     │────▶│    TEXT     │────▶│   OUTLINE   │
└─────────────┘     └─────────────┘     └─────────────┘     └─────────────┘
      │                   │                   │                   │
      ▼                   ▼                   ▼                   ▼
  multipart/         FileUpload           RawText            OutlineNode[]
  form-data          (tracked)            (string)
```

### 3.2 File Validation (Pre-Extraction)

| Check | Limit | Failure Response |
|-------|-------|------------------|
| File size | ≤ 10 MB | 413 Payload Too Large |
| Content-Type | `.docx` or `.pdf` only | 400 Bad Request |
| Malware scan | Clean (production only) | 400 Bad Request |
| PDF encryption | Not encrypted | 400 "Password-protected PDFs not supported" |

### 3.3 Text Extraction

**DOCX Extraction**:
- Use Open XML SDK to read document structure
- Preserve paragraph hierarchy via heading styles (Heading1, Heading2, etc.)
- Extract numbered/bulleted list structure
- No macro execution; no embedded object trust

**PDF Extraction**:
- Use Azure AI Document Intelligence (Read API) for layout-aware extraction
- Fall back to local library (disabled by default) for development
- Detect heading structure via font size/weight heuristics
- Reject scanned-only PDFs (no OCR in v1)

### 3.4 Temporary File Handling

- Uploaded files stored in Azure Blob Storage (`temp-uploads` container)
- Lifecycle policy: auto-delete after 1 hour
- Files deleted immediately after successful text extraction
- No long-term storage of uploaded files unless user explicitly saves outline

---

## 4. Export Handling

### 4.1 Export Flow

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│    LOAD     │     │   APPLY     │     │  TRANSFORM  │     │   STREAM    │
│  QUESTIONS  │────▶│ RENDER MODE │────▶│  TO FORMAT  │────▶│  RESPONSE   │
└─────────────┘     └─────────────┘     └─────────────┘     └─────────────┘
      │                   │                   │                   │
      ▼                   ▼                   ▼                   ▼
  QuestionSet        Filtered            Formatted           File bytes
  (from DB)          Questions           Document
```

### 4.2 Render Mode Application

| Mode | Behavior |
|------|----------|
| **Exam** | Exclude `explanation`, `rationale` from all outputs |
| **Study** | Include explanations in separate section (never inline with stem/choices) |

### 4.3 Format-Specific Export

**JSON Export**:
- Serialize question set to JSON
- Include metadata header (exam name, generated date, disclaimer)
- Exam Mode: omit `explanation` property entirely
- Study Mode: include `explanation` as property

**Markdown Export**:
- Render questions as readable Markdown
- Exam Mode: questions only, no explanations
- Study Mode: explanations in `## Explanation` subsections or consolidated at end

**DOCX Export** (Open XML SDK):
- Professional document with styles
- Title page: exam name, section, generation date
- Formatted questions with type badges, difficulty indicators
- Study Mode: "Answer Key with Explanations" section at end
- Visible disclaimer on first page
- No hidden metadata, no MCP IDs

**PDF Export** (QuestPDF):
- Match DOCX layout and content
- Embedded fonts for consistency
- Same render mode behavior as DOCX
- Clean output with no internal references

---

## 5. Item Type Registry

The Item Type Registry is the central mapping from `itemTypeCode` to all type-specific resources.

### 5.1 Registry Structure

```
ItemTypeRegistry
├── M1 ─────────┬── Schema:      docs/schemas/item-types/M1.schema.json
│               ├── Prompt:      prompts/item-types/M1.md
│               ├── Validator:   M1Validator (schema + lint rules)
│               └── Exporter:    M1Exporter (format-specific rendering)
│
├── BA ─────────┬── Schema:      docs/schemas/item-types/BA.schema.json
│               ├── Prompt:      prompts/item-types/BA.md
│               ├── Validator:   BAValidator
│               └── Exporter:    BAExporter
│
├── CHOOSE_N ───┬── Schema:      docs/schemas/item-types/CHOOSE_N.schema.json
│               ├── Prompt:      prompts/item-types/CHOOSE_N.md
│               ├── Validator:   ChooseNValidator
│               └── Exporter:    ChooseNExporter
│
├── DD ─────────┬── Schema:      docs/schemas/item-types/DD.schema.json
│               ├── Prompt:      prompts/item-types/DD.md
│               ├── Validator:   DDValidator
│               └── Exporter:    DDExporter
│
└── ... (19 types total)
```

### 5.2 Registry Components

| Component | Source | Purpose |
|-----------|--------|---------|
| **Schema** | `docs/schemas/item-types/{CODE}.schema.json` | Structural validation |
| **Prompt Fragment** | `prompts/item-types/{CODE}.md` | Generation instructions |
| **Validator** | Code derived from schema + `docs/item-types.md` constraints | Schema + rule validation |
| **Exporter** | Code implementing format-specific rendering | Render question to JSON/MD/DOCX/PDF |

### 5.3 Supported Item Types

| Code | Name | Scoring |
|------|------|---------|
| M1 | Multiple Choice Select One | Dichotomous |
| BA | Best Answer | Dichotomous |
| DA | Dynamic Answer Choice | Dichotomous |
| SC | Sentence Correction | Dichotomous |
| DOMC | Dynamic Option Multiple Choice | Dichotomous |
| CHOOSE_N | Multiple-Alphabetic/Choose n | Polytomous |
| AS | Active Screen | Polytomous |
| BL | Build List | Polytomous |
| MW | Magnetic Words | Polytomous |
| HA | Hot Area | Polytomous |
| DD | Drag and Drop | Polytomous |
| MG | Matching | Polytomous |
| GI | Graphics Interpretation | Polytomous |
| MS | Multi-Source Reasoning | Polytomous |
| PA | Two-Part Analysis | Polytomous |
| CA | Code/Table Analysis | Polytomous |
| AN | Statement Analysis | Polytomous |
| LABS | Task-Based Labs | Polytomous |
| SA | Short Answer Code | Polytomous |

### 5.4 Registry Initialization

At application startup:
1. Scan `docs/schemas/item-types/` for schema files
2. Scan `prompts/item-types/` for prompt fragments
3. Build registry mapping `itemTypeCode` → resources
4. Validate all 19 types are present (fail startup if missing)

---

## 6. Validation Architecture

### 6.1 Validation Layers

```
┌─────────────────────────────────────────────────────────────────┐
│                      VALIDATION PIPELINE                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌────────────────────┐    ┌────────────────────┐               │
│  │  SCHEMA VALIDATION │    │   LINT VALIDATION  │               │
│  │  (JSON Schema)     │───▶│   (Business Rules) │               │
│  └────────────────────┘    └────────────────────┘               │
│           │                          │                           │
│           ▼                          ▼                           │
│  • Structure correctness     • Style compliance                  │
│  • Required fields           • Content rules                     │
│  • Data types                • Domain constraints                │
│  • Array lengths             • Cross-field logic                 │
│  • Enum values               • Text analysis                     │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 6.2 Schema Validation Scope

Schema validation uses JSON Schema (Draft 2020-12) via NJsonSchema library.

**What Schema CAN Express**:
- Field presence (`required`)
- Data types (`type: string/array/object/integer`)
- String patterns (`pattern: "^Q\\d{3}$"`)
- Numeric ranges (`minimum: 1, maximum: 50`)
- Array constraints (`minItems: 4, maxItems: 5`)
- Enum restrictions (`enum: ["Easy", "Medium", "Hard"]`)
- Nested object structure

**Schema Location**: `docs/schemas/item-types/{CODE}.schema.json`

### 6.3 Lint Validation Scope (Rules Beyond Schema)

Lint validation catches constraints that JSON Schema cannot express.

**What Schema CANNOT Express** (Lint handles):

| Category | Example Rules |
|----------|---------------|
| **Text Content Analysis** | No contractions ("don't" → "do not") |
| **Cross-Field Relationships** | Explanation must not appear in stem |
| **Semantic Ordering** | Numeric choices in ascending order |
| **Domain Constraints** | M1 cannot have exactly 2 choices (true/false prohibition) |
| **Parallel Structure** | All choices must have parallel grammatical form |
| **Acronym Usage** | Acronyms must be defined on first use |

**Lint Rules Source**: Derived from `docs/exam-writing-style-guide.md`

### 6.4 Validation Execution Order

1. **Schema Validation First**: Structural correctness required before content analysis
2. **Lint Validation Second**: Only runs on schema-valid questions
3. **Both Must Pass**: Question rejected if either validation fails

### 6.5 Validation Failure Handling

```
Question fails validation
         │
         ▼
    Retry count < 3?
         │
    ┌────┴────┐
   Yes        No
    │          │
    ▼          ▼
Regenerate   Return error
with hints   to user with
             specific violation
```

---

## 7. Frontend Architecture

### 7.1 Technology Choice

**Preferred**: React with TypeScript

React is the preferred frontend framework due to:
- Component-based architecture suitable for complex forms
- Strong ecosystem for accessibility (React Aria, Radix)
- State management options for generation progress (React Query, Zustand)
- Streaming support for real-time progress updates (EventSource API)

### 7.2 High-Level Components

```
┌─────────────────────────────────────────────────────────────────┐
│                        FRONTEND STRUCTURE                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Pages                                                           │
│  ├── OutlineInputPage         Upload/paste outline               │
│  ├── OutlineViewerPage        Interactive tree view              │
│  ├── GenerationConfigPage     Parameter selection                │
│  ├── GenerationProgressPage   Real-time progress                 │
│  ├── QuestionReviewPage       View/edit generated questions      │
│  └── ExportPage               Format and mode selection          │
│                                                                  │
│  Shared Components                                               │
│  ├── OutlineTree              Hierarchical node selector         │
│  ├── ItemTypeSelector         Checkbox grid for 19 types         │
│  ├── DifficultySlider         Easy/Medium/Hard distribution      │
│  ├── ProgressIndicator        Generation progress bar            │
│  ├── QuestionCard             Single question display            │
│  ├── RenderModeToggle         Exam/Study mode switch             │
│  └── ExportFormatPicker       JSON/MD/DOCX/PDF selector          │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 7.3 Key Interactions

| User Action | API Call | Response Handling |
|-------------|----------|-------------------|
| Paste outline text | `POST /api/outlines/parse` | Display parsed tree |
| Upload DOCX/PDF | `POST /api/outlines/upload` | Display parsed tree |
| Start generation | `POST /api/generations` | Connect to SSE stream |
| Monitor progress | `GET /api/generations/{id}/stream` | Update progress UI |
| Export questions | `GET /api/questions/{id}/export?format=&renderMode=` | Download file |

### 7.4 Accessibility Requirements

Per Constitution Principle V and NFR-005:
- WCAG 2.1 AA compliance required
- Keyboard navigation for all interactive elements
- ARIA labels on custom components
- Color contrast ratios meeting standards
- Screen reader compatible progress announcements

---

## 8. Non-Functional Architecture

### 8.1 Security Architecture

| Concern | Implementation |
|---------|----------------|
| Authentication | Microsoft Entra ID (OpenID Connect) |
| Authorization | JWT bearer tokens with scope validation |
| Secrets | Azure Key Vault (no secrets in code/config) |
| Input Validation | Server-side validation on all endpoints |
| Rate Limiting | 10 requests/minute, 100 questions/day per user |
| File Safety | Content-type validation, malware scanning, temp cleanup |

### 8.2 Observability Architecture

| Concern | Implementation |
|---------|----------------|
| Health Checks | `/health` endpoint with dependency status |
| Logging | Structured logs with correlation IDs (App Insights format) |
| Metrics | Request count, latency (p50/p95/p99), error rates |
| Tracing | Distributed tracing for generation pipeline |
| Alerting | App Insights alerts for error spikes, latency degradation |

### 8.3 Performance Targets

| Metric | Target |
|--------|--------|
| Generation latency (10 questions) | p95 ≤ 45 seconds |
| MCP response latency | p95 ≤ 10 seconds |
| Export generation | p95 ≤ 5 seconds |
| Concurrent users | 100 without degradation |
| Availability | 99.9% monthly |

---

## 9. Deployment Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      AZURE DEPLOYMENT                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────┐     ┌─────────────┐     ┌─────────────┐        │
│  │   Azure     │     │   Azure     │     │   Azure     │        │
│  │ App Service │────▶│    SQL      │     │    Blob     │        │
│  │  (Backend)  │     │  Database   │     │   Storage   │        │
│  └─────────────┘     └─────────────┘     └─────────────┘        │
│         │                                       │                │
│         │            ┌─────────────┐           │                │
│         │            │   Azure     │           │                │
│         └───────────▶│  Key Vault  │◀──────────┘                │
│         │            └─────────────┘                            │
│         │                                                        │
│         │            ┌─────────────┐     ┌─────────────┐        │
│         └───────────▶│   Azure     │     │  Microsoft  │        │
│                      │   OpenAI    │     │  Learn MCP  │        │
│                      └─────────────┘     └─────────────┘        │
│                                                                  │
│  ┌─────────────┐     ┌─────────────┐                            │
│  │   Azure     │     │   Azure     │                            │
│  │ Static Web  │     │ Application │                            │
│  │ Apps (SPA)  │     │  Insights   │                            │
│  └─────────────┘     └─────────────┘                            │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 10. Document Cross-References

| This Document Section | Related Contract/Spec |
|-----------------------|----------------------|
| §2 Generation Pipeline | `spec.md` FR-009 through FR-013 |
| §2.2 MCP Retrieval | `spec.md` FR-029 through FR-034, Constitution §VI |
| §2.5 Schema Validation | `docs/schemas/*.json`, `spec.md` FR-042 |
| §2.6 Lint Validation | `docs/exam-writing-style-guide.md`, `spec.md` FR-044 through FR-048 |
| §3 File Upload | `spec.md` FR-001a through FR-001g, NFR-008 |
| §4 Export | `spec.md` FR-017 through FR-020d, FR-049 through FR-055 |
| §5 Item Type Registry | `docs/item-types.md`, `spec.md` FR-035 through FR-043 |
| §7 Frontend | `spec.md` NFR-005, NFR-006, Constitution §V |
| §8.1 Security | `spec.md` FR-021 through FR-025, NFR-001, Constitution §II |
| §8.2 Observability | `spec.md` FR-026 through FR-028, NFR-009 through NFR-011, Constitution §IV |

---

**Generated**: 2026-01-03 | **Status**: Architecture Design Complete
