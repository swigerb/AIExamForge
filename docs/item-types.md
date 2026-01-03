# Item Types (Authoritative)

All question generation must conform to these item types and constraints. Short codes in parentheses are required identifiers.

## Multiple Choice, Select One (M1)
- Exactly 1 correct answer
- At least 2 distractors
- True/false style items are not acceptable
Variations:
- Best Answer (BA): one answer is best; must instruct “select the BEST answer”; >=2 distractors
- Dynamic Answer Choice (DA): author >=2 correct answers and >=6 distractors; delivery shows 1 correct + (2 or 3) distractors based on distractor count

## Sentence Correction (SC)
- Include “No change is needed.” as the first choice
- Do not randomize answer choices
- Instruction statement precedes the question
- Exactly 1 correct answer
- At least 2 distractors in addition to “No change is needed.”

## Dynamic Option Multiple Choice (DOMC)
- Set-based: same problem statement; different solution each item
- Learner answers Yes/No
- Each set: >=1 correct solution, >=3 incorrect solutions
- Learner sees 3 randomly selected questions from the set per delivery
- Must appear last; learner cannot navigate back
- Set counts as a single item

## Multiple-Alphabetic / Choose n (CHOOSE_N)
- Must specify exact number to select; "choose all that apply" not allowed
- Author specifies whether each correct answer is a partial or complete solution
- Must offer at least n+2 choices (e.g., Choose 2 >=4, Choose 3 >=5)
- Polytomous scoring (each correct action earns a point)

## Active Screen (AS)
- Screenshot-based interactive config
- Minimum 4 active areas
- Polytomous scoring

## Build List (BL)
- Used when order is critical and there are only one or two valid orderings
- Minimum 3 steps; maximum 5 steps
- Up to 2 solutions allowed (same options; different order)
- Must state number of tasks to move in instruction statement
- Polytomous scoring

Variation:
- Magnetic Words (MW): code blocks; 6–12 blocks; 3–5 steps; polytomous scoring

## Hot Area (HA)
- Click hot areas in an image/screenshot
- Min 4 hot areas; max 8
- Requires selecting at least 2 areas
- Polytomous scoring

## Drag and Drop (DD)
- Sources: min 3, max 8
- Targets: min 2, max 5
- Empty targets not allowed
- Instruction must specify whether sources can be reused and/or unused
- Polytomous scoring

Variation:
- Matching (MG): targets min 3, max 6; empty targets not allowed; polytomous scoring

## Graphics Interpretation (GI)
- Exactly 1 embedded graphic
- 2–5 partially complete statements/questions
- Two dropdown lists in answer area
- Each dropdown contains 3–5 choices
- Use bold “[answer choice]” placeholder in partial statements
- Polytomous scoring

## Yes/No Table Variants
- Multi-Source Reasoning (MS): reference >=3 data points; >=3 Yes/No statements; polytomous scoring
- Two-Part Analysis (PA): 2 answer columns, >=3 rows; non-negative statements; 1 answer per column; polytomous scoring
- Code/Table Analysis (CA): 3–8 statements; non-negative statements; predict outcomes; polytomous scoring
- Statement Analysis (AN): 3–8 statements; non-negative statements; determine true/false; polytomous scoring

## Task-Based Labs (LABS)
- Task-based in a virtual environment; scoring based on end state or dependent question
- Polytomous scoring; tasks phrased as problems to solve

## Short Answer Code (SA)
- Free-text code/command entry; may include syntax checking (not correctness)
- Provide necessary names/values in stem/exhibits
- Typically a single output per scenario
- Constraints: <=10 lines total; average 4–6 lines; <=5 pieces in single-line; <=4 lines in multi-line
- Polytomous scoring based on complexity