# Exam Writing Style Rules
Machine-Enforceable Requirements for Exam Items

## 1. Scope

These rules apply to all exam item text, including:
- Instruction statements
- Question stems and scenarios
- Answer choices and distractors
- Exhibit text, tables, captions, and labels

These rules do not apply to UI text, documentation, or study-mode rationales.

---

## 2. Absolute Prohibitions (Hard Fail)

### 2.1 Teaching or Explanation Text
Exam items must not include:
- Definitions
- Explanations
- Reasoning statements
- Instructional guidance

Any occurrence results in an invalid item.

---

### 2.2 Contractions
Contractions are forbidden in exam item text, including but not limited to:
can't, won't, don't, doesn't, isn't, aren't, wasn't, weren't,
it's, that's, there's, here's, you're, they're, we're, I'm, let's

Any occurrence results in an invalid item.

---

### 2.3 Forbidden Answer Patterns
The following phrases are forbidden:
all of the above
none of the above
both A and B
choose all that apply

Any occurrence results in an invalid item.

---

### 2.4 True / False Items
Standalone true/false questions are forbidden.

Binary judgment must use an approved item type.

---

## 3. Required Structural Rules

### 3.1 Answer Choice Minimums
- Single-select items must include at least three answer choices.
- Multiple-select items must meet item-type minimums.

Violation results in an invalid item.

---

### 3.2 Parallel Answer Choices
Answer choices must be parallel in:
- Grammatical structure
- Sentence vs fragment form
- Technical depth

Non-parallel choices result in an invalid item.

---

### 3.3 No Explanations in Answer Choices
Answer choices must not contain explanations or reasoning.

Violation results in an invalid item.

---

### 3.4 Numeric Answer Ordering
If answer choices contain numeric values:
- Values must be ordered ascending or descending.
- Randomization must be disabled.

Violation results in an invalid item.

---

## 4. Question Stem Rules

### 4.1 Completeness
The stem must present a complete, self-contained problem.

Incomplete stems result in an invalid item.

---

### 4.2 Negative Construction
- Negative wording must be avoided unless required.
- If used:
  - Only one negative term is allowed.
  - The negative must be clearly visible.

Hidden or multiple negatives result in an invalid item.

---

### 4.3 Interrogative Clarity
The stem must clearly indicate what the candidate must select or perform.

Ambiguity results in an invalid item.

---

## 5. Instruction Statement Rules

### 5.1 Separation of Concerns
Instructions must describe how to respond, not how to solve.

Teaching language results in an invalid item.

---

### 5.2 Placement
If required by the item type, the instruction must appear before the stem.

Incorrect placement results in an invalid item.

---

## 6. Terminology Rules

### 6.1 Consistency
Each technical term must be used consistently.

Inconsistent terminology results in an invalid item.

---

### 6.2 Acronyms
- First occurrence must spell out the term followed by the acronym.
- If first used in answer choices, the term must be spelled out in each applicable choice.
- Do not mix abbreviated and spelled-out forms.

Violation results in an invalid item.

---

## 7. Qualifiers and Absolutes

### 7.1 Absolutes
Words such as “always”, “never”, or “only” must not be used unless technically accurate and required.

Incorrect usage results in an invalid item.

---

### 7.2 Qualifiers
Words such as “best”, “most likely”, or “first” may be used only when:
- Required by the item type
- Explicitly signaled in the stem

Unsignaled qualifiers result in an invalid item.

---

## 8. Exhibits, Tables, and Graphics

### 8.1 Necessity
Exhibits must add measurable value.

Unnecessary exhibits result in an invalid item.

---

### 8.2 Tables
- Sentence capitalization required
- Consistent formatting
- Singular headings for single values

Violation results in an invalid item.

---

### 8.3 Graphics
- Readable labels
- Adequate contrast
- Meaning must not rely on color alone

Accessibility violations result in an invalid item.

---

## 9. Item-Type Compliance

Every exam item must:
- Declare a valid itemTypeCode
- Conform to item-type constraints
- Validate against the corresponding JSON schema

Any violation results in an invalid item.

---

## 10. Rationales

- Rationales must not appear in exam item text.
- Rationales must be stored and rendered separately.

Rationale leakage results in an invalid item.

---

## 11. Enforcement Model

An exam item is valid only if:
1. Schema validation passes
2. All rules in this file pass
3. Item-type-specific constraints pass

If any rule fails:
- The item must be rejected or regenerated
- Silent correction is not allowed

---

## 12. Governance

This file is authoritative for machine enforcement.

If conflicts arise:
- This file governs validation
- The full Exam Writing Style Guide governs interpretation
- Both must be satisfied for approval