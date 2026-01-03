# Exam Writing Style Guide
Authoritative Standard for Exam Item Authoring

## 1. Purpose and Scope

This guide defines the standards for writing high-quality exam items that accurately measure candidate skills and knowledge.

It applies to all exam item content, including:
- Question stems and scenarios
- Instruction statements
- Answer choices and distractors
- Exhibits, tables, graphics, and captions
- Any text presented as part of an exam item

This guide governs what appears inside exam items. It does not govern product UI text, onboarding, or documentation.

---

## 2. Core Exam Design Principles

### 2.1 Measure Skill, Not Instruction

Exam items must measure the target skill and must not teach.

- Do not define terms in the stem.
- Do not explain concepts in the stem or answer choices.
- Do not guide the candidate toward the correct answer.

Instructional content belongs outside the exam item (for example, in study materials or post-submission rationales).

---

### 2.2 Originality and Fairness

- All items must be original practice content.
- Items must not reproduce or closely resemble real exam questions.
- Items must be fair to all candidates and free of bias.
- Avoid culture-specific idioms, metaphors, or humor.
- Avoid unnecessary complexity that does not measure the target skill.

---

### 2.3 Clarity and Precision

- Use clear, concise language.
- Avoid ambiguity.
- Use consistent terminology throughout the item.
- Do not include unnecessary background information.

---

## 3. Question Stems and Scenarios

### 3.1 Structure

- The stem must present a complete, self-contained problem.
- The stem must clearly identify what is being asked.
- Avoid embedding answer options or hints in the stem.

---

### 3.2 Interrogatives

- Use “which” when the possible answers are known.
- Use “what” or “how” when the response is open-ended within constraints.
- Avoid vague interrogatives.

---

### 3.3 Negative Construction

- Avoid negative wording whenever possible.
- If negative wording is required:
  - Use it sparingly.
  - Format clearly.
  - Avoid double negatives.
- Do not hide negative terms in long sentences.

---

### 3.4 True/False Statements

- Standalone true/false constructions are not acceptable.
- Binary judgment must be expressed using approved item types.

---

### 3.5 Scenario-Based Items

- Scenarios must be realistic and technically plausible.
- Include only information relevant to the task.
- Avoid excessive narrative or storytelling.
- Ensure all required information is present in the stem or exhibits.

---

## 4. Instruction Statements

### 4.1 Purpose

Instruction statements tell the candidate how to respond, not how to solve the problem.

---

### 4.2 Rules

- Instructions must be concise and standardized.
- Instructions must not teach or explain.
- Instructions must appear before the stem when required by the item type.
- Item-type-specific instructions must use approved wording.

---

### 4.3 Consistency

- Use consistent phrasing for the same item type across the exam.
- Do not vary instruction wording unless explicitly allowed.

---

## 5. Answer Choices

### 5.1 Minimum Requirements

- Single-select items must include at least three answer choices.
- Multiple-select items must meet the minimum requirements defined by the item type.

---

### 5.2 Parallel Structure

Answer choices must be parallel in:
- Grammatical structure
- Length (as much as possible)
- Technical depth
- Level of detail

If perfect parallelism is not possible:
- The correct answer must not stand out.
- Distractors must be parallel to one another.

---

### 5.3 Distractors

- Distractors must be plausible.
- Distractors should reflect common mistakes or misconceptions.
- Do not include distractors that are obviously incorrect.

---

### 5.4 Prohibited Answer Patterns

The following are not allowed:
- All of the above
- None of the above
- Both A and B
- Choose all that apply

---

### 5.5 Explanations in Answer Choices

- Do not include explanations or justifications in answer choices.
- Do not embed reasoning or instructional text in distractors.

---

### 5.6 Numeric Answer Choices

When answer choices contain numeric values:
- Present them in ascending or descending order.
- Disable randomization of answer choices.
- Ensure numeric formats are consistent.

---

## 6. Language and Style Rules

### 6.1 Contractions

- Do not use contractions in exam items.
- Spell out all words (for example, “do not”, “cannot”).

---

### 6.2 Terminology

- Use terminology familiar to the target audience.
- Use each term consistently.
- Do not introduce unnecessary synonyms.

---

### 6.3 Acronyms and Abbreviations

- On first use, spell out the term followed by the acronym in parentheses.
- If an acronym first appears in answer choices:
  - Spell out the term in each applicable choice.
- Do not mix spelled-out and abbreviated forms within the same item.

---

### 6.4 Absolutes and Qualifiers

- Avoid absolutes such as “always” and “never” unless technically accurate.
- Use qualifiers such as “best”, “most likely”, or “first” only when required by the item type.
- When using qualifiers, ensure the stem clearly signals their intent.

---

## 7. Exhibits, Tables, and Graphics

### 7.1 General Rules

- Use exhibits only when they add value.
- Do not overload items with unnecessary visuals.
- Ensure all visual elements are readable and accessible.

---

### 7.2 Tables

- Use sentence capitalization.
- Keep column headings concise.
- Use singular headings for single values.
- Ensure consistent formatting.

---

### 7.3 Graphics and Diagrams

- Labels must be clear and consistent.
- Avoid clutter.
- Ensure adequate contrast and legibility.
- Do not rely on color alone to convey meaning.

---

## 8. Item-Type Compliance

Each exam item must conform to the structural and scoring rules of its item type.

Item-type definitions, constraints, and scoring models are defined separately and must be followed exactly.

---

## 9. Rationales and Feedback

- Rationales are not part of exam items.
- Rationales must not appear in stems, instructions, or answer choices.
- Rationales may be provided separately for study or review purposes.

---

## 10. Accessibility and Usability

- Ensure text is readable at standard zoom levels.
- Avoid unnecessarily complex sentence structures.
- Ensure graphics and tables can be understood without excessive interpretation.
- Maintain consistent layout and formatting.

---

## 11. Quality Review

Before approval, verify that:
- The item measures the intended skill.
- The item follows all language and structure rules.
- The item conforms to its item type.
- The item is fair, clear, and unambiguous.
- No prohibited patterns are present.

---

## 12. Governance

This Exam Writing Style Guide is authoritative for all exam item text.

If any other style guidance conflicts:
- This guide governs exam items.
- Other style guides may govern UI, documentation, or non-exam content.

---

## Appendix A: Lint Rule Mapping

This appendix maps the exam writing style rules in this document to the machine-enforceable lint rules implemented in AIExamForge. Use these rule codes when reviewing validation errors or configuring lint behavior.

### Absolute Prohibitions (Section 2)

| Rule Code | Rule Name | Section | Description |
|-----------|-----------|---------|-------------|
| 100 | `NoTeachingText` | 2.1 | Exam items must measure skill, not teach. Teaching indicators like "this is because", "remember that", "note that" are forbidden in stems and choices. |
| 101 | `NoContractions` | 6.1 | Contractions (don't, can't, won't, etc.) are forbidden in exam items. Spell out all words. |
| 102 | `NoForbiddenAnswerPatterns` | 5.4 | Patterns like "all of the above", "none of the above", "both A and B", "choose all that apply" are not allowed. |
| 103 | `NoTrueFalseItems` | 3.4 | Standalone true/false constructions are not acceptable. |

### Structural Rules (Section 3-5)

| Rule Code | Rule Name | Section | Description |
|-----------|-----------|---------|-------------|
| 200 | `MinimumAnswerChoices` | 5.1 | Single-select items must include at least three answer choices. |
| 201 | `ParallelAnswerChoices` | 5.2 | Answer choices must be parallel in grammatical structure, length, and depth. |
| 202 | `NoExplanationsInChoices` | 5.5 | Answer choices must not include explanations, justifications, or reasoning text. |
| 203 | `NumericAnswerOrdering` | 5.6 | Numeric answer choices must be in ascending or descending order. |
| 300 | `StemCompleteness` | 3.1 | The stem must present a complete, self-contained problem. |
| 301 | `NegativeConstruction` | 3.3 | Negative wording should be avoided; when required, format clearly and avoid double negatives. |
| 302 | `InterrogativeClarity` | 3.2 | Use "which" for known answers, "what"/"how" for open-ended responses. |
| 400 | `InstructionSeparation` | 4.2 | Instructions must be separate from problem-solving guidance. |
| 401 | `InstructionPlacement` | 4.2 | Instructions must appear before the stem when required by item type. |

### Language and Style Rules (Section 6-7)

| Rule Code | Rule Name | Section | Description |
|-----------|-----------|---------|-------------|
| 500 | `TerminologyConsistency` | 6.2 | Use each term consistently; do not introduce unnecessary synonyms. |
| 501 | `AcronymDefinition` | 6.3 | On first use, spell out the term followed by the acronym in parentheses. |
| 600 | `NoIncorrectAbsolutes` | 6.4 | Absolutes (always, never, only) should be avoided unless technically accurate. |
| 601 | `SignaledQualifiers` | 6.4 | When using qualifiers (best, most likely, first), ensure the stem clearly signals intent. |

### Exhibits and Visuals (Section 7)

| Rule Code | Rule Name | Section | Description |
|-----------|-----------|---------|-------------|
| 700 | `ExhibitNecessity` | 7.1 | Use exhibits only when they add value. |
| 701 | `TableFormatting` | 7.2 | Tables must use sentence capitalization, concise headings, and consistent formatting. |
| 702 | `GraphicsAccessibility` | 7.3 | Graphics must be clear, have adequate contrast, and not rely on color alone. |

### Item Type Compliance (Section 8)

| Rule Code | Rule Name | Section | Description |
|-----------|-----------|---------|-------------|
| 800 | `ValidItemTypeCode` | 8 | Each exam item must use a valid item type code. |
| 801 | `ItemTypeConstraints` | 8 | Each item must conform to the structural and scoring rules of its item type. |
| 802 | `SchemaValidation` | 8 | Item content must validate against the item type's JSON schema. |

### Rationales (Section 9)

| Rule Code | Rule Name | Section | Description |
|-----------|-----------|---------|-------------|
| 900 | `NoRationaleLeakage` | 9 | Rationale content must not appear in stems, instructions, or answer choices. |

### Severity Levels

- **Error**: Violation that must be corrected before the item can be used.
- **Warning**: Potential issue that should be reviewed but may be acceptable.
- **Info**: Informational notice for best practice considerations.

### Implementation Notes

1. Lint validation runs automatically during question generation.
2. Violations are included in the `validationResult` of each question.
3. Users can review and accept warnings but must fix errors.
4. Custom lint rule configuration may be available in future versions.
