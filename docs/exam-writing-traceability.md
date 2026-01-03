# Exam Writing Traceability Map
Guide → Rules → Schemas → Validators

## 1. Purpose

This document establishes traceability between:

1. Human-authoritative guidance
2. Machine-enforceable rules
3. Structural JSON schemas
4. Future validators and generators

Every enforced rule must be traceable to written guidance.  
Every piece of guidance that affects output must be enforceable or explicitly non-enforceable.

---

## 2. Authoritative Documents

### 2.1 Human Interpretation (Narrative)

| File | Purpose |
|-----|---------|
| `docs/exam-writing-style-guide.md` | Full, human-readable exam authoring standard |
| `docs/item-types.md` | Item type definitions, constraints, and scoring models |

These documents explain intent, rationale, and nuance.

---

### 2.2 Machine Enforcement (Deterministic)

| File | Purpose |
|-----|---------|
| `docs/exam-writing-style-rules.md` | Hard validation rules |
| `docs/schemas/question.schema.json` | Top-level output contract |
| `docs/schemas/common/*.schema.json` | Shared structural definitions |
| `docs/schemas/item-types/*.schema.json` | Item-type-specific structure |

These documents define what is allowed.

---

## 3. Traceability Matrix

### 3.1 Teaching or Explanation Prohibition

| Layer | Artifact |
|------|----------|
| Guide | §2.1 Measure Skill, Not Instruction |
| Rules | §2.1 Teaching or Explanation Text |
| Schema | Not expressible structurally |
| Validator | Text lint rule (regex + heuristics) |

---

### 3.2 Contractions Prohibition

| Layer | Artifact |
|------|----------|
| Guide | §6.1 Contractions |
| Rules | §2.2 Contractions |
| Schema | Not expressible |
| Validator | Regex-based lint rule |

---

### 3.3 Forbidden Answer Patterns

| Layer | Artifact |
|------|----------|
| Guide | §5.4 Prohibited Answer Patterns |
| Rules | §2.3 Forbidden Answer Patterns |
| Schema | Not expressible |
| Validator | Regex-based lint rule |

---

### 3.4 True / False Items Prohibition

| Layer | Artifact |
|------|----------|
| Guide | §3.4 True/False Statements |
| Rules | §2.4 True / False Items |
| Schema | `itemTypeCode` enumeration |
| Validator | Item-type enforcement |

---

### 3.5 Minimum Answer Choices

| Layer | Artifact |
|------|----------|
| Guide | §5.1 Minimum Requirements |
| Rules | §3.1 Answer Choice Minimums |
| Schema | `minItems` constraints |
| Validator | Schema validation |

---

### 3.6 Parallel Answer Choices

| Layer | Artifact |
|------|----------|
| Guide | §5.2 Parallel Structure |
| Rules | §3.2 Parallel Answer Choices |
| Schema | Not expressible |
| Validator | Structural and linguistic linting |

---

### 3.7 Numeric Answer Ordering

| Layer | Artifact |
|------|----------|
| Guide | §5.6 Numeric Answer Choices |
| Rules | §3.4 Numeric Answer Ordering |
| Schema | Partial (randomization flags) |
| Validator | Numeric parsing and ordering check |

---

### 3.8 Negative Construction

| Layer | Artifact |
|------|----------|
| Guide | §3.3 Negative Construction |
| Rules | §4.2 Negative Construction |
| Schema | Not expressible |
| Validator | Keyword and count check |

---

### 3.9 Instruction Statement Placement

| Layer | Artifact |
|------|----------|
| Guide | §4 Instruction Statements |
| Rules | §5 Instruction Statement Rules |
| Schema | Required and optional fields |
| Validator | Item-type rule enforcement |

---

### 3.10 Terminology Consistency

| Layer | Artifact |
|------|----------|
| Guide | §6.2 Terminology |
| Rules | §6.1 Consistency |
| Schema | Not expressible |
| Validator | Term reuse detection |

---

### 3.11 Acronym Usage

| Layer | Artifact |
|------|----------|
| Guide | §6.3 Acronyms and Abbreviations |
| Rules | §6.2 Acronyms |
| Schema | Not expressible |
| Validator | First-use detection |

---

### 3.12 Qualifiers and Absolutes

| Layer | Artifact |
|------|----------|
| Guide | §6.4 Absolutes and Qualifiers |
| Rules | §7 Qualifiers and Absolutes |
| Schema | Not expressible |
| Validator | Keyword and context analysis |

---

### 3.13 Exhibits, Tables, and Graphics

| Layer | Artifact |
|------|----------|
| Guide | §7 Exhibits, Tables, and Graphics |
| Rules | §8 Exhibits, Tables, and Graphics |
| Schema | Structural fields |
| Validator | Accessibility and necessity checks |

---

### 3.14 Item-Type Compliance

| Layer | Artifact |
|------|----------|
| Guide | §8 Item-Type Compliance |
| Rules | §9 Item-Type Compliance |
| Schema | Per-item-type schemas |
| Validator | Schema and rule validators |

---

### 3.15 Rationales Separation

| Layer | Artifact |
|------|----------|
| Guide | §9 Rationales and Feedback |
| Rules | §10 Rationales |
| Schema | Separate `rationale` field |
| Validator | Field isolation check |

---

## 4. Enforcement Flow

1. Generation references:
   - `docs/exam-writing-style-guide.md`
   - `docs/exam-writing-style-rules.md`
   - `docs/item-types.md`

2. Structural validation:
   - Validate against `docs/schemas/question.schema.json`
   - Validate against the corresponding item-type schema

3. Rule validation:
   - Apply all rules in `docs/exam-writing-style-rules.md`
   - Apply item-type-specific constraints

4. Failure handling:
   - Any failure results in rejection or regeneration
   - Silent correction is not allowed

---

## 5. Non-Enforceable Guidance

The following guidance is intentionally not enforced automatically:

- Plausibility of distractors
- Fairness across demographics
- Cognitive load appropriateness
- Editorial tone consistency across an exam set

These remain human-review responsibilities.

---

## 6. Governance and Change Control

- Changes to the style guide require review.
- New enforceable rules must be added to:
  - `docs/exam-writing-style-rules.md`
  - This traceability map
- Schemas and validators must remain aligned with rules.

---

## 7. Summary

This traceability map ensures:

- No rule exists without justification
- No guidance is enforced accidentally
- Deterministic AI generation
- Auditability and explainability

This document completes the specification chain for exam item authoring.
