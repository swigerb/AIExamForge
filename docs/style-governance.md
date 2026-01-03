# Style Governance

**Feature**: 001-generate-questions  
**Date**: 2026-01-04  
**Status**: Active

## Overview

This document defines the style precedence rules for AIExamForge. It establishes which style guide governs each content type and how conflicts between style guides are resolved.

**Requirement Reference**: FR-046

---

## Content Types and Governing Guides

### Exam Item Content

**Governing Guide**: [Exam Writing Style Guide](exam-writing-style-guide.md)

Exam item content includes:
- Question stems and scenarios
- Instruction statements
- Answer choices and distractors
- Exhibits, tables, graphics, and captions
- Any text presented as part of an exam item
- Rationales and explanations (study mode only)

The Exam Writing Style Guide is the authoritative source for all exam item text. Its rules take absolute precedence over any other style guidance when applied to exam content.

### Application UI and User-Facing Text

**Governing Guide**: [Microsoft Writing Style Guide](https://learn.microsoft.com/en-us/style-guide/welcome/)

Application UI content includes:
- Button labels and UI controls
- Navigation menus and page titles
- Error messages and notifications
- Form labels and input placeholders
- Tooltips and help text
- Progress indicators and status messages
- Modal dialogs and confirmation messages

### Documentation and README Files

**Governing Guide**: [Microsoft Writing Style Guide](https://learn.microsoft.com/en-us/style-guide/welcome/)

Documentation content includes:
- README.md and Getting Started guides
- API documentation
- Developer guides and tutorials
- Architecture documentation
- Configuration instructions
- Changelog and release notes

### Code Comments and Technical Content

**Governing Guide**: Standard C# documentation conventions with Microsoft terminology

Code content includes:
- XML documentation comments
- Inline code comments
- Variable and method naming
- Error logging messages
- API response messages

---

## Precedence Rules

### Rule 1: Content Type Determines Authority

The content type determines which style guide has authority:

```
Exam Item Text → Exam Writing Style Guide (absolute authority)
Application UI  → Microsoft Writing Style Guide
Documentation   → Microsoft Writing Style Guide
Code Comments   → C# Documentation Conventions
```

### Rule 2: No Cross-Domain Conflicts

When a piece of text appears in multiple contexts:
- The most restrictive applicable rule applies
- Exam item rules always take precedence for exam content
- UI rules cannot override exam item rules within exam items

### Rule 3: Conflict Resolution Hierarchy

If multiple rules could apply to the same text:

1. **Exam Writing Style Guide rules** (when text is exam item content)
2. **Machine-enforceable lint rules** (automated validation)
3. **Microsoft Writing Style Guide rules** (for non-exam content)
4. **Project-specific conventions** (for code and internal documentation)

### Rule 4: Ambiguous Content

When content type is ambiguous:
- If the content will appear in an exam item → Exam Writing Style Guide governs
- If the content is purely instructional → Microsoft Writing Style Guide governs
- When in doubt, apply the more restrictive rule

---

## Lint Rule Enforcement

### Automated Validation

AIExamForge implements automated lint validation for exam item content. These rules are derived from the Exam Writing Style Guide and are machine-enforced during question generation.

See [Exam Writing Style Guide - Appendix A: Lint Rule Mapping](exam-writing-style-guide.md#appendix-a-lint-rule-mapping) for the complete list of automated lint rules.

### Enforcement Levels

| Severity | Behavior | User Action Required |
|----------|----------|---------------------|
| Error | Blocks item approval | Must fix before use |
| Warning | Allows item approval | Should review, may accept |
| Info | Informational only | Optional consideration |

### Lint Rule Categories

| Category | Rule Codes | Description |
|----------|------------|-------------|
| Absolute Prohibitions | 100-103 | Rules that cannot be overridden |
| Structural Requirements | 200-401 | Item structure and formatting rules |
| Language Rules | 500-601 | Terminology and style rules |
| Visual Content | 700-702 | Exhibits, tables, and graphics |
| Item Type Compliance | 800-802 | Type-specific validation |
| Rationale Rules | 900 | Rationale content restrictions |

---

## Example Scenarios

### Scenario 1: Button in Exam Question

**Context**: A question stem describes clicking a button in a UI screenshot.

**Resolution**: The button label in the screenshot follows Microsoft UI conventions, but the question stem text describing the action follows the Exam Writing Style Guide.

### Scenario 2: Error Message in Rationale

**Context**: A rationale explains an error message the user would see.

**Resolution**: The quoted error message follows its original style, but the explanatory text in the rationale follows the Exam Writing Style Guide.

### Scenario 3: Technical Term in UI vs Exam

**Context**: A technical term like "deployment" appears both in the UI and in exam questions.

**Resolution**: The term should be used consistently. The Exam Writing Style Guide's terminology consistency rule (501) applies to exam content; Microsoft style applies to UI labels.

---

## Maintenance and Updates

### Style Guide Updates

1. The Exam Writing Style Guide is the source of truth for exam item rules
2. Changes to the style guide require corresponding lint rule updates
3. Lint rule changes must be documented in the Appendix A mapping

### Review Cadence

- Quarterly review of lint rule effectiveness
- Annual review of style guide alignment
- Immediate review if exam quality issues arise

### Change Management

Style governance changes require:
1. Documentation update in this file
2. Lint rule implementation update (if automated)
3. Review of existing content for compliance
4. Communication to content authors

---

## Related Documents

- [Exam Writing Style Guide](exam-writing-style-guide.md) - Authoritative exam item standards
- [Microsoft Writing Style Guide](https://learn.microsoft.com/en-us/style-guide/welcome/) - UI and documentation standards
- [Item Type Specification](../specs/001-generate-questions/contracts/item-types.json) - Item type structural rules
- [Lint Rule Implementation](../backend/src/AIExamForge.Application/Validation/LintValidator.cs) - Automated validation code 