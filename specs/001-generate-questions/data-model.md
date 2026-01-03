# Data Model: AIExamForge

**Feature**: 001-generate-questions  
**Date**: 2026-01-02  
**Updated**: 2026-01-03  
**Status**: Phase 1 Design (Updated for CHOOSE_N/LABS codes)

## Overview

This document defines the domain entities, relationships, and validation rules for AIExamForge.

---

## Entities

### Outline

Represents a user-provided exam outline (text or extracted from uploaded file).

```csharp
public class Outline
{
    public Guid Id { get; init; }
    public string UserId { get; init; } = null!;
    public string? ExamName { get; set; }
    public string RawContent { get; init; } = null!;
    public OutlineSourceType SourceType { get; init; }
    public string? SourceFileName { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? DeletedAt { get; set; }
    
    // Navigation
    public ICollection<OutlineNode> Nodes { get; init; } = new List<OutlineNode>();
    public ICollection<GenerationRequest> GenerationRequests { get; init; } = new List<GenerationRequest>();
}

public enum OutlineSourceType
{
    TextPaste,
    DocxUpload,
    PdfUpload
}
```

**Validation Rules**:
- `RawContent`: Required, max 500 KB
- `ExamName`: Optional, max 256 characters
- `SourceFileName`: Required if `SourceType` is `DocxUpload` or `PdfUpload`
- `UserId`: Required, from authentication token

**Indexes**:
- `UserId` (for user's outlines query)
- `CreatedAt` (for cleanup/ordering)

---

### OutlineNode

Represents a section or subsection in the parsed outline hierarchy.

```csharp
public class OutlineNode
{
    public Guid Id { get; init; }
    public Guid OutlineId { get; init; }
    public Guid? ParentId { get; init; }
    public string Title { get; init; } = null!;
    public string? Content { get; init; }
    public int Level { get; init; }
    public int SortOrder { get; init; }
    
    // Navigation
    public Outline Outline { get; init; } = null!;
    public OutlineNode? Parent { get; init; }
    public ICollection<OutlineNode> Children { get; init; } = new List<OutlineNode>();
}
```

**Validation Rules**:
- `Title`: Required, max 500 characters
- `Level`: 1-10 (enforced depth limit)
- `SortOrder`: Unique within siblings

**Indexes**:
- `OutlineId` (for outline's nodes query)
- `ParentId` (for tree traversal)
- Composite: `OutlineId, ParentId, SortOrder` (for ordering)

---

### FileUpload

Tracks uploaded files for auditing and cleanup.

```csharp
public class FileUpload
{
    public Guid Id { get; init; }
    public string UserId { get; init; } = null!;
    public string OriginalFileName { get; init; } = null!;
    public string StoredFileName { get; init; } = null!;
    public FileUploadType FileType { get; init; }
    public long FileSizeBytes { get; init; }
    public string ContentType { get; init; } = null!;
    public FileUploadStatus Status { get; init; }
    public DateTime UploadedAt { get; init; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? OutlineId { get; set; }
}

public enum FileUploadType
{
    Docx,
    Pdf
}

public enum FileUploadStatus
{
    Uploaded,
    Processing,
    Completed,
    Failed,
    Deleted
}
```

**Validation Rules**:
- `FileSizeBytes`: Max 10 MB (10,485,760 bytes)
- `ContentType`: Must match `FileType`
- `OriginalFileName`: Max 255 characters

**Indexes**:
- `UserId` (for user's uploads)
- `Status, UploadedAt` (for cleanup job)

---

### GenerationRequest

Captures user's question generation configuration.

```csharp
public class GenerationRequest
{
    public Guid Id { get; init; }
    public string UserId { get; init; } = null!;
    public Guid OutlineId { get; init; }
    public IReadOnlyList<Guid> SelectedNodeIds { get; init; } = Array.Empty<Guid>();
    public string? ExamName { get; init; }
    public int QuestionCount { get; init; }
    public IReadOnlyList<string> QuestionTypes { get; init; } = Array.Empty<string>();
    public DifficultyDistribution? Difficulty { get; init; }
    public string? ToneProfile { get; init; }
    public GenerationStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; private set; }
    
    // Navigation
    public Outline Outline { get; init; } = null!;
    public QuestionSet? QuestionSet { get; set; }
}

public enum GenerationStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

public record DifficultyDistribution(int EasyPercent, int MediumPercent, int HardPercent);
```

**Validation Rules**:
- `QuestionCount`: 1-50
- `QuestionTypes`: At least one type, valid type names only
- `SelectedNodeIds`: At least one node, must exist in outline
- `DifficultyDistribution`: Percentages must sum to 100

**Indexes**:
- `UserId, CreatedAt` (for user's history)
- `Status` (for pending requests processing)

---

### Question

Represents a generated question with type-specific content.

```csharp
public class Question
{
    public Guid Id { get; init; }
    public Guid QuestionSetId { get; init; }
    public string DisplayId { get; init; } = null!; // e.g., "Q001"
    public string Type { get; init; } = null!;
    public Guid SourceNodeId { get; init; }
    public string SourceNodeTitle { get; init; } = null!;
    public QuestionDifficulty Difficulty { get; init; }
    public JsonDocument Content { get; init; } = null!; // Type-specific JSON
    public string Explanation { get; init; } = null!;
    public IReadOnlyList<string> InternalMcpReferenceIds { get; init; } = Array.Empty<string>(); // NOT exported
    public DateTime CreatedAt { get; init; }
    
    // Navigation
    public QuestionSet QuestionSet { get; init; } = null!;
}

public enum QuestionDifficulty
{
    Easy,
    Medium,
    Hard
}
```

**Validation Rules**:
- `DisplayId`: Unique within set, format "Q###"
- `Type`: Must be valid question type name
- `Content`: Must validate against type's JSON schema
- `Explanation`: Required, non-empty

**Indexes**:
- `QuestionSetId` (for set's questions)
- `Type` (for filtering by type)

---

### QuestionSet

Container for a generation result.

```csharp
public class QuestionSet
{
    public Guid Id { get; init; }
    public Guid GenerationRequestId { get; init; }
    public int TotalQuestions { get; private set; }
    public IReadOnlyList<string> AvailableExportFormats { get; init; } = new[] { "json", "markdown", "docx", "pdf" };
    public DateTime CreatedAt { get; init; }
    public DateTime LastModifiedAt { get; private set; }
    
    // Navigation
    public GenerationRequest GenerationRequest { get; init; } = null!;
    public ICollection<Question> Questions { get; init; } = new List<Question>();
    public ICollection<ExportRecord> ExportRecords { get; init; } = new List<ExportRecord>();
}
```

**Indexes**:
- `GenerationRequestId` (unique, one-to-one)
- `CreatedAt` (for retention policy)

---

### ExportRecord

Tracks exports for auditing and re-download.

```csharp
public class ExportRecord
{
    public Guid Id { get; init; }
    public Guid QuestionSetId { get; init; }
    public ExportFormat Format { get; init; }
    public string BlobPath { get; init; } = null!;
    public long FileSizeBytes { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    
    // Navigation
    public QuestionSet QuestionSet { get; init; } = null!;
}

public enum ExportFormat
{
    Json,
    Markdown,
    Docx,
    Pdf
}
```

**Indexes**:
- `QuestionSetId, Format` (for existing export lookup)
- `ExpiresAt` (for cleanup job)

---

## Relationships

```
┌──────────────┐
│   Outline    │
├──────────────┤
│ Id (PK)      │
│ UserId       │◄────┐
│ ExamName     │     │
│ RawContent   │     │ User's outlines
│ SourceType   │     │
└──────┬───────┘     │
       │             │
       │ 1:N         │
       ▼             │
┌──────────────┐     │
│ OutlineNode  │     │
├──────────────┤     │
│ Id (PK)      │     │
│ OutlineId(FK)│     │
│ ParentId(FK) │──┐  │ Self-referential
│ Title        │  │  │
│ Level        │◄─┘  │
│ SortOrder    │     │
└──────────────┘     │
                     │
┌──────────────┐     │
│ FileUpload   │     │
├──────────────┤     │
│ Id (PK)      │     │
│ UserId       │◄────┘
│ OutlineId(FK)│───────► Outline
│ Status       │
└──────────────┘

┌───────────────────┐
│ GenerationRequest │
├───────────────────┤
│ Id (PK)           │
│ UserId            │
│ OutlineId (FK)    │───────► Outline
│ SelectedNodeIds   │
│ QuestionCount     │
│ Status            │
└─────────┬─────────┘
          │
          │ 1:1
          ▼
┌──────────────┐
│ QuestionSet  │
├──────────────┤
│ Id (PK)      │
│ GenReqId(FK) │
│ TotalQs      │
└──────┬───────┘
       │
       │ 1:N
       ▼
┌──────────────┐      ┌──────────────┐
│   Question   │      │ ExportRecord │
├──────────────┤      ├──────────────┤
│ Id (PK)      │      │ Id (PK)      │
│ QSetId (FK)  │      │ QSetId (FK)  │───► QuestionSet
│ Type         │      │ Format       │
│ Content(JSON)│      │ BlobPath     │
│ McpRefIds    │      │ ExpiresAt    │
└──────────────┘      └──────────────┘
```

---

## Question Content Schemas

### Multiple-Choice

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "required": ["prompt", "options", "correctIndex"],
  "properties": {
    "prompt": { "type": "string", "minLength": 10, "maxLength": 2000 },
    "options": {
      "type": "array",
      "items": { "type": "string", "minLength": 1, "maxLength": 500 },
      "minItems": 4,
      "maxItems": 5
    },
    "correctIndex": { "type": "integer", "minimum": 0 }
  }
}
```

### Multiple-Select

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "required": ["prompt", "options", "correctIndices"],
  "properties": {
    "prompt": { "type": "string", "minLength": 10, "maxLength": 2000 },
    "options": {
      "type": "array",
      "items": { "type": "string", "minLength": 1, "maxLength": 500 },
      "minItems": 4,
      "maxItems": 8
    },
    "correctIndices": {
      "type": "array",
      "items": { "type": "integer", "minimum": 0 },
      "minItems": 2
    }
  }
}
```

### Short-Answer

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "required": ["prompt", "acceptableAnswers"],
  "properties": {
    "prompt": { "type": "string", "minLength": 10, "maxLength": 2000 },
    "acceptableAnswers": {
      "type": "array",
      "items": { "type": "string", "minLength": 1, "maxLength": 200 },
      "minItems": 1,
      "maxItems": 10
    }
  }
}
```

### Drag-and-Drop

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "required": ["prompt", "items", "targets", "correctMapping"],
  "properties": {
    "prompt": { "type": "string", "minLength": 10, "maxLength": 2000 },
    "items": {
      "type": "array",
      "items": { "type": "string", "minLength": 1, "maxLength": 300 },
      "minItems": 3,
      "maxItems": 8
    },
    "targets": {
      "type": "array",
      "items": { "type": "string", "minLength": 1, "maxLength": 300 },
      "minItems": 3,
      "maxItems": 8
    },
    "correctMapping": {
      "type": "object",
      "additionalProperties": { "type": "integer" }
    }
  }
}
```

### Scenario-Based

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "required": ["context", "parts"],
  "properties": {
    "context": { "type": "string", "minLength": 50, "maxLength": 5000 },
    "parts": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["prompt", "type", "correctAnswer"],
        "properties": {
          "prompt": { "type": "string" },
          "type": { "enum": ["multiple-choice", "multiple-select", "short-answer"] },
          "options": { "type": "array", "items": { "type": "string" } },
          "correctAnswer": {}
        }
      },
      "minItems": 2,
      "maxItems": 5
    }
  }
}
```

---

## State Transitions

### GenerationRequest Status

```
                 ┌──────────────┐
                 │   Pending    │
                 └──────┬───────┘
                        │ Start generation
                        ▼
                 ┌──────────────┐
       ┌─────────│ InProgress   │─────────┐
       │         └──────┬───────┘         │
       │ Cancel         │ Complete        │ Error
       ▼                ▼                 ▼
┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│  Cancelled   │ │  Completed   │ │    Failed    │
└──────────────┘ └──────────────┘ └──────────────┘
```

### FileUpload Status

```
┌──────────────┐
│   Uploaded   │
└──────┬───────┘
       │ Start extraction
       ▼
┌──────────────┐
│  Processing  │
└──────┬───────┘
       │
       ├─── Success ──► Completed ──► (linked to Outline)
       │
       └─── Failure ──► Failed
                             │
                             │ Cleanup job
                             ▼
                        ┌──────────────┐
                        │   Deleted    │
                        └──────────────┘
```

---

## Style & Item-Type Validation Entities

### ItemTypeCode (Value Object)

Represents a supported question type identifier.

```csharp
public record ItemTypeCode
{
    public string Value { get; }
    
    private ItemTypeCode(string value) => Value = value;
    
    public static readonly ItemTypeCode M1 = new("M1");           // Multiple Choice, Select One
    public static readonly ItemTypeCode BA = new("BA");           // Best Answer
    public static readonly ItemTypeCode DA = new("DA");           // Dynamic Answer Choice
    public static readonly ItemTypeCode SC = new("SC");           // Sentence Correction
    public static readonly ItemTypeCode DOMC = new("DOMC");       // Dynamic Option Multiple Choice
    public static readonly ItemTypeCode CHOOSE_N = new("CHOOSE_N"); // Multiple-Alphabetic (Choose n)
    public static readonly ItemTypeCode AS = new("AS");           // Active Screen
    public static readonly ItemTypeCode BL = new("BL");           // Build List
    public static readonly ItemTypeCode MW = new("MW");           // Magnetic Words
    public static readonly ItemTypeCode HA = new("HA");           // Hot Area
    public static readonly ItemTypeCode DD = new("DD");           // Drag and Drop
    public static readonly ItemTypeCode MG = new("MG");           // Matching
    public static readonly ItemTypeCode GI = new("GI");           // Graphics Interpretation
    public static readonly ItemTypeCode MS = new("MS");           // Multi-Source Reasoning
    public static readonly ItemTypeCode PA = new("PA");           // Two-Part Analysis
    public static readonly ItemTypeCode CA = new("CA");           // Code/Table Analysis
    public static readonly ItemTypeCode AN = new("AN");           // Statement Analysis
    public static readonly ItemTypeCode LABS = new("LABS");       // Task-Based Labs
    public static readonly ItemTypeCode SA = new("SA");           // Short Answer Code
    
    public static IReadOnlyList<ItemTypeCode> All => new[]
    {
        M1, BA, DA, SC, DOMC, CHOOSE_N, AS, BL, MW, HA, DD, MG, GI, MS, PA, CA, AN, LABS, SA
    };
    
    public static ItemTypeCode? TryParse(string value) =>
        All.FirstOrDefault(c => c.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
    
    public static ItemTypeCode Parse(string value) =>
        TryParse(value) ?? throw new ArgumentException($"Unknown item type code: {value}");
}
```

**Validation Rules**:
- `Value`: Must be one of the 19 supported codes
- Case-insensitive parsing for user input
- Stored as string in database

---

### QuestionTypeDefinition (Domain Model)

Represents a complete question type definition loaded at startup.

```csharp
public class QuestionTypeDefinition
{
    public ItemTypeCode Code { get; init; }
    public string DisplayName { get; init; } = null!;
    public string Description { get; init; } = null!;
    public JsonSchema Schema { get; init; } = null!;          // From docs/schemas/item-types/{CODE}.schema.json
    public string PromptFragment { get; init; } = null!;       // From prompts/item-types/{CODE}.md
    public IReadOnlyList<string> ScoringType { get; init; }    // "dichotomous" or "polytomous"
    public IReadOnlyList<TypeConstraint> Constraints { get; init; } = Array.Empty<TypeConstraint>();
}

public record TypeConstraint(
    string Property,      // e.g., "choices", "correctIndices"
    string Rule,          // e.g., "minItems", "exactCount"
    object Value);        // e.g., 3, [2, 5]
```

**Source Files**:
- Schema: `docs/schemas/item-types/{CODE}.schema.json`
- Prompt: `prompts/item-types/{CODE}.md`
- Constraints: Derived from `docs/item-types.md`

---

### ValidationResult (Value Object)

Represents the outcome of schema or lint validation.

```csharp
public record ValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<ValidationError> Errors { get; init; } = Array.Empty<ValidationError>();
    
    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Failed(IEnumerable<ValidationError> errors) =>
        new() { IsValid = false, Errors = errors.ToList() };
}

public record ValidationError(
    string Code,          // e.g., "SCHEMA_001", "LINT_CONTRACTION"
    string Message,       // Human-readable error
    string? Path,         // JSON path or field name, e.g., "stem", "choices[2].text"
    ValidationSeverity Severity);

public enum ValidationSeverity
{
    Error,    // Blocks persistence
    Warning   // Logged but allowed (future use)
}
```

---

### LintViolation (Value Object)

Represents a specific style rule violation.

```csharp
public record LintViolation(
    LintRule Rule,
    string Message,
    string? Location,     // e.g., "stem", "choice[2]", "instruction"
    string? ViolatingText // The actual text that violated the rule
);

public enum LintRule
{
    NoContractions,              // "don't" → "do not"
    NoExplanationsInItemText,    // Explanations only in separate field
    NumericOrderPreserved,       // Numeric choices in ascending order
    ParallelForm,                // Answer choices have parallel structure
    NoAcronymWithoutDefinition,  // Acronyms must be defined on first use
    NoTrueFalseForM1             // M1 must not be true/false style
}
```

---

### RenderMode (Enum)

Controls how questions are rendered for display and export.

```csharp
public enum RenderMode
{
    /// <summary>
    /// Exam Mode: No rationales or explanations shown.
    /// Simulates real exam conditions.
    /// </summary>
    Exam = 0,
    
    /// <summary>
    /// Study Mode: Rationales and explanations shown in separate fields.
    /// Provides learning value. Explanations NEVER appear in stem/choices/instructions.
    /// </summary>
    Study = 1
}
```

**Usage**:
- Stored in `GenerationRequest.RenderMode`
- Applied during export (JSON, Markdown, Word, PDF)
- UI renders differently based on mode

---

### Updated Question Entity

```csharp
public class Question
{
    public Guid Id { get; init; }
    public Guid QuestionSetId { get; init; }
    public string DisplayId { get; init; } = null!;           // e.g., "Q001"
    public ItemTypeCode ItemTypeCode { get; init; } = null!;  // REQUIRED: M1, BA, DA, SC, etc.
    public string TypeDisplayName { get; init; } = null!;     // Human-readable: "Multiple Choice"
    public Guid SourceNodeId { get; init; }
    public string SourceNodeTitle { get; init; } = null!;
    public QuestionDifficulty Difficulty { get; init; }
    public JsonDocument Content { get; init; } = null!;       // Type-specific JSON, validated against schema
    public string? Explanation { get; init; }                 // SEPARATE from item text (Study Mode only)
    public string? Rationale { get; init; }                   // SEPARATE from item text (Study Mode only)
    public IReadOnlyList<string> InternalMcpReferenceIds { get; init; } = Array.Empty<string>();
    public DateTime CreatedAt { get; init; }
    
    // Validation metadata
    public bool PassedSchemaValidation { get; init; }
    public bool PassedLintValidation { get; init; }
    public IReadOnlyList<string>? ValidationWarnings { get; init; }
    
    // Navigation
    public QuestionSet QuestionSet { get; init; } = null!;
}
```

**Validation Rules**:
- `ItemTypeCode`: Required, must be valid code
- `Content`: Must validate against `docs/schemas/item-types/{CODE}.schema.json`
- `Explanation`: Allowed only if not embedded in Content stem/choices
- `PassedSchemaValidation`: Must be true before persistence
- `PassedLintValidation`: Must be true before persistence

---

### Updated GenerationRequest Entity

```csharp
public class GenerationRequest
{
    // ... existing fields ...
    
    public RenderMode RenderMode { get; init; } = RenderMode.Study;  // Default to Study Mode
    public IReadOnlyList<ItemTypeCode> RequestedItemTypes { get; init; } = Array.Empty<ItemTypeCode>();
}
```

---

## Database Retention Policies

| Entity | Retention | Cleanup Mechanism |
|--------|-----------|-------------------|
| FileUpload (temp) | 1 hour | Background service |
| ExportRecord | 7 days | Background service + Blob lifecycle |
| GenerationRequest | 30 days | Soft delete, then hard delete |
| QuestionSet | 30 days | Cascades from GenerationRequest |
| Outline | Until user deletes | Soft delete on user request |

---

**Generated**: 2026-01-02 | **Updated**: 2026-01-03 | **Status**: Phase 1 Design Complete (Updated for CHOOSE_N/LABS codes and Exam/Study Mode)
