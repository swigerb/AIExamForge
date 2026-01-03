# Implementation Plan: Generate Practice Exam Questions from Outline

**Branch**: `001-generate-questions` | **Date**: 2026-01-02 | **Updated**: 2026-01-03 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-generate-questions/spec.md`

## Summary

Generate original practice exam questions grounded exclusively in Microsoft Learn MCP content. Users provide exam outlines via text paste or DOCX/PDF upload. System extracts text, parses outline hierarchy, and enables section selection. Users configure question count and select from 19 item types by short code. Generated content validates against JSON schemas and exam writing style guide. Exports support Exam Mode (no rationales) and Study Mode (rationales in separate section) in JSON, Markdown, DOCX, and PDF formats.

## Technical Context

**Language/Version**: C# .NET 10 (LTS)  
**Primary Dependencies**: ASP.NET Core Minimal APIs, NJsonSchema, Open XML SDK, QuestPDF, Azure.AI.DocumentIntelligence, Azure.AI.OpenAI  
**Storage**: Azure SQL Database, Azure Blob Storage  
**Testing**: xUnit, Moq, FluentAssertions (backend); Vitest, React Testing Library (frontend)  
**Target Platform**: Azure App Service (Linux container)  
**Project Type**: Web application (backend API + frontend SPA)  
**Performance Goals**: p95 generation latency ≤45s for 10 questions, 100 concurrent users  
**Constraints**: MCP latency p95 ≤10s, max 50 questions/request, max 100 questions/user/day  
**Scale/Scope**: Single-tenant, 100-1000 users, 19 item types

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Evidence |
|-----------|--------|----------|
| I. Tech Stack | ✅ PASS | .NET 10, ASP.NET Core Minimal APIs, React+TS, Azure App Service |
| II. Security | ✅ PASS | Entra ID auth, Key Vault secrets, input validation, rate limiting, file upload safety |
| III. Testing | ✅ PASS | TDD, xUnit/Vitest, CI gates, MCP response handling tests |
| IV. Observability | ✅ PASS | Structured logging, /health endpoint, App Insights telemetry |
| V. UX Consistency | ✅ PASS | WCAG 2.1 AA, progress indicators, error guidance |
| VI. MCP-Exclusive | ✅ PASS | MCP-only sourcing, graceful failure, prompt guardrails |
| Authoritative Specs | ✅ PASS | Schema validation, style compliance, lint enforcement |

**All gates passed. No violations requiring justification.**

## Project Structure

### Documentation (this feature)

```text
specs/001-generate-questions/
├── plan.md              # This file
├── research.md          # Phase 0 output (complete)
├── data-model.md        # Phase 1 output (complete)
├── quickstart.md        # Phase 1 output (complete)
├── contracts/           # Phase 1 output
│   └── openapi.yaml     # API contract (complete)
├── checklists/          # Quality checklists
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Phase 2 output (175 tasks)
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── AIExamForge.Api/           # ASP.NET Core Minimal API
│   │   ├── Endpoints/             # Outline, Generation, Export endpoints
│   │   ├── Middleware/            # Auth, error handling, rate limiting
│   │   └── Program.cs
│   ├── AIExamForge.Application/   # Application services
│   │   ├── Generation/            # Question generation orchestration
│   │   ├── Parsing/               # Outline parsing services
│   │   ├── Export/                # Export formatters (JSON, MD, DOCX, PDF)
│   │   └── Validation/            # Schema + lint validation
│   ├── AIExamForge.Domain/        # Domain models
│   │   ├── Entities/              # Outline, Question, QuestionSet, etc.
│   │   ├── ValueObjects/          # ItemTypeCode, RenderMode, etc.
│   │   └── Interfaces/            # Repository interfaces
│   └── AIExamForge.Infrastructure/ # External integrations
│       ├── MCP/                   # Microsoft Learn MCP client
│       ├── OpenAI/                # Azure OpenAI client
│       ├── Documents/             # DOCX/PDF extraction
│       ├── Persistence/           # EF Core repositories
│       └── Storage/               # Azure Blob Storage
└── tests/
    ├── AIExamForge.UnitTests/
    ├── AIExamForge.IntegrationTests/
    └── AIExamForge.ContractTests/

frontend/
├── src/
│   ├── components/                # UI components
│   │   ├── outline/               # OutlineTree, NodeSelector
│   │   ├── generation/            # ConfigForm, ProgressIndicator
│   │   ├── questions/             # QuestionList, QuestionEditor
│   │   └── export/                # ExportDialog, FormatSelector
│   ├── pages/                     # Route pages
│   ├── services/                  # API client, auth service
│   ├── hooks/                     # Custom React hooks
│   └── types/                     # TypeScript interfaces
└── tests/

docs/
├── exam-writing-style-guide.md    # Exam item style rules (authoritative)
├── item-types.md                  # 19 item type definitions (authoritative)
├── style-governance.md            # Lint rules and enforcement (authoritative)
└── schemas/
    ├── question.schema.json       # Master question schema
    ├── common/                    # Shared schema components
    └── item-types/                # Per-type schemas (19 files)

prompts/
├── system-mcp-only.md             # MCP-exclusive system prompt
├── exam-style.md                  # Exam tone/style instructions
└── item-types/                    # Per-type prompt fragments (19 files)
```

**Structure Decision**: Web application with backend API and frontend SPA. Clean architecture with Domain → Application → Infrastructure → API layering. Authoritative specs in `docs/` and `prompts/` directories.

## Item Type Codes (19 Types)

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

## Render Modes

| Mode | Behavior | Use Case |
|------|----------|----------|
| Exam | No rationales/explanations in exports | Simulate real exam conditions |
| Study | Rationales in separate field/section (never inline) | Learning and review |

## Complexity Tracking

> No violations requiring justification. All gates passed.

---

**Generated**: 2026-01-02 | **Updated**: 2026-01-03 | **Status**: Phase 1 Design Complete
