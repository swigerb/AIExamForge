# Specification Quality Checklist: Generate Practice Exam Questions from Outline

**Purpose**: Validate specification completeness and quality before proceeding to planning

**Created**: 2026-01-02

**Feature**: [spec.md](../spec.md)

**Updated**: 2026-01-03 (Updated to reflect resolved [NEEDS CLARIFICATION] markers; added Exam Mode vs Study Mode; updated item type codes to use CHOOSE_N instead of M2+; added LABS instead of Labs)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed
- [x] MCP dependency and limitations clearly explained

**Notes**: 
- Technology constraints (NFR-000a/b/c: .NET 10, React TypeScript, Azure App Service) are appropriately labeled as constraints, not implementation code.
- All user stories describe user value and workflows, not technical implementation.
- MCP integration requirements (FR-029 through FR-034) are business/capability-focused, not implementation details.
- File handling requirements (FR-001a-g, FR-020a-d) are security/user-focused, not implementation details.

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified
- [x] MCP requirements are explicit and non-negotiable
- [x] Failure modes for MCP unavailability are defined

**Resolved [NEEDS CLARIFICATION] markers** (all now resolved with defaults):
1. **FR-002 (outline max size)**: Resolved → 500 KB
2. **FR-005 (per-request max questions)**: Resolved → 50 questions
3. **FR-002 (max nesting depth)**: Resolved → 10 levels
4. **SC-008 (MCP response latency p95)**: Resolved → 10 seconds
5. **FR-001b (file upload size limit)**: Resolved → 10 MB
6. **FR-001f (temp file retention)**: Resolved → 1 hour
6. **FR-001f (temp file retention)**: "[NEEDS CLARIFICATION: recommend 1 hour]"
   - Reason: Balances recovery/debugging with security cleanup. Default recommendation provided.

All six are **design parameters** (not scope ambiguities) that will be finalized in planning based on technical constraints and service characteristics.

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification
- [x] MCP integration is non-negotiable and explicitly required

**Coverage**:
- **Primary flow**: Upload/paste outline (US-1) → Configure parameters (US-2) → Generate (US-3) → Export (US-5) → Disclaimers (US-6). All stories independent and testable. File upload/export now integral to primary flow.
- **Secondary flow**: Review/regenerate (US-4) enhances primary flow.
- **File handling**: US-1 expanded to support paste, .docx, .pdf inputs. US-5 expanded to support JSON, Markdown, Word, PDF exports with title pages and formatting.
- **Edge cases**: 6+ edge cases including file handling (unsupported format, encrypted PDF, file too large, malware, extraction failure, text extraction loss).
- **MCP compliance**: FR-029 through FR-034 mandate MCP-exclusive content sourcing, graceful failure modes, and prompt guardrails. FR-035 through FR-039 define pluggable question-type architecture with writing rules, JSON schemas, validation logic, and MCP reference ID tracking.
- **File Handling**: FR-001a-g define upload validation, document extraction, error handling, and temporary file cleanup. FR-020a-d define Word/PDF export with title page, formatting, no internal references.
- **Question Type System**: Enhanced with explicit writing rules, schema definitions, validation logic, schema registry requirement, and internal MCP reference ID tracking for guideline traceability (FR-035-039).
- **Export Formats**: All four formats (JSON, Markdown, Word, PDF) now required with full compliance to FR-017-020d and SC-001b, SC-010a-d.
- **Exam Mode vs Study Mode**: FR-049-055 define render modes, user selection, and export behavior for all 4 formats.
- **NFR coverage**: Security (FR-021-025, NFR-001, NFR-008), Observability (FR-026-028, NFR-009-011), Accessibility (NFR-005), Performance (SC-011-014), UX (NFR-002, NFR-003, NFR-006), **MCP Integration (NFR-007, FR-029-034, SC-005-009)**, **File Handling (NFR-008, FR-001a-g, FR-020a-d, SC-010a-d)**, **Technology Constraints (NFR-000a-c)**.

## Item Type Coverage

All 19 item types explicitly documented with short codes:

| Code | Name | Notes |
|------|------|-------|
| M1 | Multiple Choice Select One | Exactly 1 correct, ≥2 distractors |
| BA | Best Answer | Variation of M1; "select the BEST answer" |
| DA | Dynamic Answer Choice | ≥2 correct, ≥6 distractors; shows 1 correct + 2-3 distractors |
| SC | Sentence Correction | "No change needed" first; no randomization |
| DOMC | Dynamic Option Multiple Choice | Set-based Yes/No; ≥1 correct, ≥3 incorrect per set |
| CHOOSE_N | Multiple-Alphabetic/Choose n | Specify exact n; ≥n+2 choices; polytomous scoring |
| AS | Active Screen | Screenshot-based; min 4 active areas; polytomous |
| BL | Build List | Order critical; 3-5 steps; up to 2 solutions; polytomous |
| MW | Magnetic Words | Code blocks variation of BL; 6-12 blocks; 3-5 steps |
| HA | Hot Area | Click hot areas in image; 4-8 areas; ≥2 selections; polytomous |
| DD | Drag and Drop | Sources 3-8, targets 2-5; no empty targets; polytomous |
| MG | Matching | DD variation; targets 3-6; polytomous |
| GI | Graphics Interpretation | 1 graphic, 2-5 statements, 2 dropdowns; polytomous |
| MS | Multi-Source Reasoning | ≥3 data points, ≥3 Yes/No statements; polytomous |
| PA | Two-Part Analysis | 2 columns, ≥3 rows; 1 answer per column; polytomous |
| CA | Code/Table Analysis | 3-8 statements; predict outcomes; polytomous |
| AN | Statement Analysis | 3-8 statements; true/false; polytomous |
| LABS | Task-Based Labs | Virtual environment; end-state scoring; polytomous |
| SA | Short Answer Code | Free-text code; ≤10 lines; polytomous |

## Exam Mode vs Study Mode Coverage

- [x] **FR-049**: Two render modes defined (Exam Mode, Study Mode)
- [x] **FR-050**: Exam Mode behavior - No rationales in exports
- [x] **FR-051**: Study Mode behavior - Rationales in separate section
- [x] **FR-052**: User toggle at export time, defaults to Study Mode
- [x] **FR-053**: All 4 formats respect render mode (JSON, Markdown, DOCX, PDF)
- [x] **FR-054**: Clear labeling in export UI
- [x] **FR-055**: Internal storage always includes explanations
- [x] **SC-004a**: Exam Mode success criteria
- [x] **SC-004b**: Study Mode success criteria
- [x] **SC-004c**: Mode switching without regeneration

## Summary

**Status**: ✅ READY FOR PLANNING

**Validation Results**:
- Content Quality: PASS (5/5 items)
- Requirement Completeness: PASS (10/10 items - all [NEEDS CLARIFICATION] resolved)
- Feature Readiness: PASS (5/5 items)
- Item Type Coverage: PASS (19/19 types documented)
- Exam Mode vs Study Mode: PASS (all requirements and criteria defined)

**Outstanding Items**: None. All [NEEDS CLARIFICATION] markers resolved with defaults:
- Outline size cap: 500 KB ✅
- Per-request question cap: 50 questions ✅
- Max outline nesting depth: 10 levels ✅
- MCP response latency p95: 10 seconds ✅
- File upload size limit: 10 MB ✅
- Temporary file retention: 1 hour ✅

**Critical Compliance Points**:
1. **MCP-Only Knowledge Sourcing**: Microsoft Learn MCP is the ONLY knowledge source. Generation fails gracefully if MCP returns insufficient data. No fallback to model knowledge.
2. **Item Type Validation**: All 19 item types use explicit short codes (CHOOSE_N, LABS, not M2+/Labs). Every question validated against `docs/schemas/item-types/[CODE].schema.json`.
3. **Exam Mode vs Study Mode**: Render mode selected at export time. Exam Mode omits rationales; Study Mode includes rationales in separate section.
4. **Schema Validation**: All questions validated against `docs/schemas/question.schema.json` before returning to user.
5. **Style Compliance**: All exam item text complies with `docs/exam-writing-style-guide.md`.

**Next Steps**: Proceed to `/speckit.plan` to refine technical architecture. Specification is complete and ready for planning phase.
