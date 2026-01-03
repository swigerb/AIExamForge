# Research: Generate Practice Exam Questions from Microsoft Learn (MCP-Exclusive)

**Feature**: 001-generate-questions  
**Date**: 2026-01-02  
**Status**: Complete

## Research Summary

This document captures research findings for all technical decisions and unknowns identified during planning. Each section documents the decision, rationale, and alternatives considered.

---

## 1. Microsoft Learn MCP Integration

### Decision
Use Microsoft Learn MCP server as the **exclusive** content source for question generation.

### Rationale
- Constitution Principle VI mandates MCP-exclusive sourcing (non-negotiable)
- Official Microsoft documentation ensures accuracy and currency
- Traceability: every question can cite source documentation
- Legal protection: content derived from official sources

### Implementation Pattern
```csharp
public interface IMcpClient
{
    Task<McpQueryResult> QueryAsync(string topic, CancellationToken ct);
}

public record McpQueryResult(
    string Content,
    IReadOnlyList<string> SourceUrls,
    string QueryId);
```

### Failure Handling
| Scenario | Response |
|----------|----------|
| MCP timeout (>10s) | User error: "Search took too long. Please try again." |
| Empty result | Skip node, note in response: "No content found for {topic}" |
| Partial result | Generate with available content, note gaps |
| All nodes empty | 422 error: "Unable to find content. Try different topics." |

### Alternatives Considered
| Alternative | Why Rejected |
|-------------|--------------|
| Pretrained model knowledge | Violates constitution; risks hallucination |
| Web search | Not authoritative; can't verify accuracy |
| Cached static docs | Stale content; MCP provides fresh results |

---

## 2. PDF Extraction Technology

### Decision
**Primary**: Azure AI Document Intelligence (prebuilt-read model)  
**Fallback**: Local library (PdfPig or iText7), disabled by default

### Rationale
- Azure AI Document Intelligence provides highest accuracy for layout-aware extraction
- Handles complex PDFs (multi-column, tables, headings)
- Fallback option for development/offline scenarios

### Configuration
```json
{
  "PdfExtraction": {
    "Provider": "AzureDocumentIntelligence",
    "AllowLocalFallback": false,
    "Endpoint": "https://<name>.cognitiveservices.azure.com/",
    "TimeoutSeconds": 30
  }
}
```

### Cost Considerations
- Azure AI Document Intelligence: $1.50 per 1000 pages (Read API)
- Estimated usage: 100 users × 5 PDFs/day × 10 pages = 5000 pages/day = ~$225/month

### Alternatives Considered
| Alternative | Why Rejected |
|-------------|--------------|
| iText7 only | Lower accuracy, especially for complex layouts |
| Tesseract OCR | Only needed for scanned PDFs (not in scope v1) |
| Apache PDFBox | Java dependency, added complexity |

---

## 3. DOCX Processing Technology

### Decision
**Open XML SDK** (`DocumentFormat.OpenXml`) for both extraction and export

### Rationale
- Native .NET library, no external dependencies
- Full control over document structure
- Same library for read and write operations
- Well-documented, stable API

### Implementation
```csharp
public interface IDocxExtractionService
{
    Task<ExtractionResult> ExtractTextAsync(Stream docxStream);
}

public interface IDocxExportService
{
    Task<Stream> ExportAsync(QuestionSet questionSet);
}
```

### Capabilities
- Extract text with paragraph structure
- Detect heading styles (Heading1, Heading2, etc.)
- Extract numbered/bulleted lists
- Export with professional formatting (styles, headers, footers)

### Alternatives Considered
| Alternative | Why Rejected |
|-------------|--------------|
| Aspose.Words | Commercial license cost; Open XML SDK is free |
| DocX library | Less mature; Open XML SDK is official |
| Interop (COM) | Requires Office installation; not cross-platform |

---

## 4. PDF Export Technology

### Decision
Evaluate **QuestPDF** (primary) and **HTML-to-PDF** (fallback) during M3

### Options

#### QuestPDF
- Native .NET library
- Fluent API for document composition
- No external dependencies
- MIT license
- Pros: Fast, predictable output, full control
- Cons: Learning curve for complex layouts

#### HTML-to-PDF (Puppeteer/PuppeteerSharp)
- Render HTML template to PDF
- Leverage existing CSS/HTML skills
- Pros: Familiar for web developers, CSS styling
- Cons: External dependency (Chromium), larger container size

### Recommendation
Start with QuestPDF for native .NET approach. If complex formatting proves difficult, evaluate HTML-to-PDF as alternative.

### Alternatives Considered
| Alternative | Why Rejected |
|-------------|--------------|
| iTextSharp | Complex licensing (AGPL) |
| PDFSharp | Less actively maintained |
| wkhtmltopdf | External binary, security concerns |

---

## 5. Azure OpenAI Configuration

### Decision
Use **Azure OpenAI** with **MCP tool usage enabled** for question generation

### Model Selection
- Model: `gpt-4-turbo` (or latest available)
- Temperature: 0.7 (balance creativity/consistency)
- Max tokens: 4096 per question
- Response format: JSON mode enabled

### MCP Tool Integration
```
System Prompt:
You are an exam question generator for Microsoft certification exams.
You MUST use ONLY the provided Microsoft Learn content to generate questions.
Do NOT use any external knowledge or make up information.
If the provided content is insufficient, respond with: {"error": "INSUFFICIENT_CONTENT"}
All facts MUST be directly attributable to the provided content.
```

### Cost Estimation
- GPT-4 Turbo: $0.01/1K input tokens, $0.03/1K output tokens
- Average question: ~500 input tokens (MCP content + prompt), ~300 output tokens
- 100 users × 20 questions/day = 2000 questions/day
- Estimated: $80-100/day = ~$2,500/month

### Rate Limiting
- Azure OpenAI: Configure TPM (tokens per minute) limits
- App-level: 10 requests/minute per user, 100 questions/day per user

---

## 6. Question Type Design

### Decision
Support 5 question types at v1 launch with pluggable architecture

### Types

#### Multiple-Choice
```json
{
  "type": "multiple-choice",
  "prompt": "Which Azure service provides...",
  "options": ["A. Service A", "B. Service B", "C. Service C", "D. Service D"],
  "correctIndex": 0,
  "explanation": "Service A is correct because..."
}
```

#### Multiple-Select
```json
{
  "type": "multiple-select",
  "prompt": "Select all that apply...",
  "options": ["A. Option A", "B. Option B", "C. Option C", "D. Option D"],
  "correctIndices": [0, 2],
  "explanation": "Options A and C are correct because..."
}
```

#### Short-Answer
```json
{
  "type": "short-answer",
  "prompt": "What is the maximum size of...",
  "acceptableAnswers": ["4 MB", "4MB", "4 megabytes"],
  "explanation": "The maximum size is 4 MB because..."
}
```

#### Drag-and-Drop
```json
{
  "type": "drag-and-drop",
  "prompt": "Match each Azure service to its use case...",
  "items": ["Azure Functions", "Azure Logic Apps", "Azure Container Apps"],
  "targets": ["Serverless compute", "Workflow automation", "Container orchestration"],
  "correctMapping": {"0": 0, "1": 1, "2": 2},
  "explanation": "Azure Functions provides serverless compute..."
}
```

#### Scenario-Based
```json
{
  "type": "scenario-based",
  "context": "Your company is migrating to Azure...",
  "parts": [
    {
      "prompt": "Which migration approach should you use?",
      "type": "multiple-choice",
      "options": [...],
      "correctIndex": 1,
      "explanation": "..."
    },
    {
      "prompt": "What tool should you use?",
      "type": "multiple-choice",
      "options": [...],
      "correctIndex": 0,
      "explanation": "..."
    }
  ]
}
```

### Plugin Interface
```csharp
public interface IQuestionTypePlugin
{
    string TypeName { get; }
    string PromptFragment { get; }
    JsonSchema JsonSchema { get; }
    ValidationResult Validate(JsonElement question);
    string Export(Question question, ExportFormat format);
}
```

---

## 7. Authentication Strategy

### Decision
**Microsoft Entra ID** (Azure AD) with **OpenID Connect**

### Flow
1. User accesses app → Redirected to Entra ID login
2. User authenticates → Receives ID token + access token
3. Frontend stores tokens, attaches to API calls
4. Backend validates JWT, extracts user claims

### Configuration
- App registration: `AIExamForge-API` (backend), `AIExamForge-SPA` (frontend)
- Scopes: `api://aiexamforge/Questions.Generate`, `api://aiexamforge/Questions.Read`
- Token lifetime: 1 hour (refresh via MSAL)

### Frontend (MSAL.js)
```typescript
const msalConfig = {
  auth: {
    clientId: "frontend-client-id",
    authority: "https://login.microsoftonline.com/{tenant}",
    redirectUri: "https://aiexamforge.com",
  },
  scopes: ["api://aiexamforge/Questions.Generate"],
};
```

### Backend (ASP.NET Core)
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration);
```

---

## 8. File Size Limits

### Decision
| Resource | Limit | Rationale |
|----------|-------|-----------|
| Outline text | 500 KB | Largest realistic exam outline |
| File upload | 10 MB | Reasonable for DOCX/PDF with images |
| Request body | 10 MB | Aligns with file upload |
| Questions per request | 50 | Cost/latency balance |
| Questions per day | 100 | Cost control |
| Nesting depth | 10 levels | All realistic exam structures |

### Enforcement
- Server-side validation mandatory
- Client-side validation for UX (file picker, size counter)
- 413 Payload Too Large for oversized requests
- 400 Bad Request for parameter violations

---

## 9. Temporary File Handling

### Decision
Azure Blob Storage with 1-hour lifecycle policy

### Implementation
```
Container: temp-uploads
├── {guid}.docx  (metadata: userId, uploadTime, originalFilename)
├── {guid}.pdf
└── ...

Lifecycle Policy:
- Delete blobs older than 1 hour
- Background service runs every 15 minutes
```

### Security
- Container is private (no public access)
- Files accessed via backend only
- Managed Identity for App Service access
- No user-visible URLs to temp files

### Alternatives Considered
| Alternative | Why Rejected |
|-------------|--------------|
| Local file system | Not scalable, App Service ephemeral storage |
| Database BLOB | Performance concerns for large files |
| Memory only | Risk of memory pressure with large PDFs |

---

## 10. Export File Lifecycle

### Decision
Azure Blob Storage with 7-day lifecycle policy

### Implementation
```
Container: exports
├── {generationId}/questions.json
├── {generationId}/questions.md
├── {generationId}/questions.docx
├── {generationId}/questions.pdf
└── ...

Lifecycle Policy:
- Delete blobs older than 7 days
- Users can re-export if needed
```

### Download Flow
1. User clicks "Export as PDF"
2. Backend generates PDF, stores in Blob
3. Backend returns SAS URL (5-minute expiry)
4. Browser downloads file
5. Blob retained for 7 days for re-download

---

## Summary of Decisions

| Topic | Decision |
|-------|----------|
| Content Source | Microsoft Learn MCP (exclusive) |
| PDF Extraction | Azure AI Document Intelligence |
| DOCX Processing | Open XML SDK |
| PDF Export | QuestPDF (primary) |
| AI Model | Azure OpenAI GPT-4 Turbo |
| Auth | Microsoft Entra ID (OIDC) |
| File Storage | Azure Blob Storage |
| Database | Azure SQL + EF Core 10 |
| Frontend | React TypeScript + Vite |
| Backend | .NET 10 + ASP.NET Core Minimal APIs |

---

**Generated**: 2026-01-02 | **Status**: All clarifications resolved

---

## 11. Style & Item-Type Validation Architecture

### Decision
Implement a **QuestionTypeRegistry** with pluggable validators, **ExamItemLinter** for style enforcement, and **JSON schemas** per item type.

### Rationale
- Constitution Principle VI + Authoritative Specs in Repo require validation against `docs/item-types.md` and `docs/exam-writing-style-guide.md`
- Pluggable architecture allows adding new item types without modifying core pipeline
- Schemas stored in `docs/schemas/item-types/` for version control and auditability
- Linter rules derived from authoritative style guide ensure consistency

### Components

#### QuestionTypeRegistry
| Responsibility | Implementation |
|----------------|----------------|
| Map itemTypeCode to definition | Dictionary<ItemTypeCode, QuestionTypeDefinition> |
| Load schemas at startup | NJsonSchema from `docs/schemas/item-types/{CODE}.schema.json` |
| Load prompt fragments | File.ReadAllText from `prompts/item-types/{CODE}.md` |
| Provide validators | IQuestionValidator per type |
| Provide exporters | IQuestionExporter per type |

#### ExamItemLinter Rules
| Rule | Source | Implementation |
|------|--------|----------------|
| No contractions | exam-writing-style-guide.md | Regex pattern match |
| No explanations in item text | exam-writing-style-guide.md | Keyword detection (because, therefore, etc.) |
| Numeric ordering | exam-writing-style-guide.md | Parse numeric values, check ascending order |
| Parallel form | exam-writing-style-guide.md | NLP-based structure comparison (v2) |

#### Schema Validation
| Aspect | Approach |
|--------|----------|
| Schema format | JSON Schema Draft 2020-12 |
| Validation library | NJsonSchema |
| Schema location | `docs/schemas/item-types/{CODE}.schema.json` |
| Validation timing | After model response, before persistence |
| Failure handling | Retry up to 3 times, then return structured error |

### Alternatives Considered
| Alternative | Why Rejected |
|-------------|--------------|
| Inline schema definitions | Not auditable; harder to version control |
| Single monolithic validator | Not extensible for 19+ item types |
| Post-hoc linting only | Style violations should prevent persistence |

---

## 12. Exam Mode vs Study Mode

### Decision
Implement **RenderMode** enum with Exam (no rationales) and Study (rationales in separate fields).

### Rationale
- Exam Mode simulates real exam conditions without hints
- Study Mode provides learning value with explanations
- Constitution requires explanations NOT appear in stems/choices/instructions
- Separation ensures clean data model and export flexibility

### Behavior Matrix

| Mode | Stem | Choices | Instruction | Explanation Field | Rationale Field |
|------|------|---------|-------------|-------------------|-----------------|
| Exam | ✅ Shown | ✅ Shown | ✅ Shown | ❌ Hidden | ❌ Hidden |
| Study | ✅ Shown | ✅ Shown | ✅ Shown | ✅ Shown (separate) | ✅ Shown (separate) |

### Export Behavior

| Format | Exam Mode | Study Mode |
|--------|-----------|------------|
| JSON | Omit explanation/rationale fields | Include all fields |
| Markdown | Questions only | Questions + Explanations section |
| Word/PDF | Questions only | Questions + Explanations with page break |

### Alternatives Considered
| Alternative | Why Rejected |
|-------------|--------------|
| Single mode with toggle | More complex UI; cleaner to have distinct modes |
| Explanation in stem | Violates exam-writing-style-guide.md |
