<!--
Sync Impact Report (2026-01-02, Amendment 7)
Version: 3.3.0 (MINOR update for exam authoring specifications)
Modified principles: None
Added/Updated sections:
  - Renamed "Authoritative Specs in Repo" → "Authoritative Exam Authoring Specifications"
  - Added docs/exam-writing-style-rules.md to authoritative files
  - Added docs/exam-writing-traceability.md to authoritative files
  - Added explicit conformance statement: "All specifications, plans, tasks, validators, and generated code MUST conform"
  - Added explicit override prohibition: "No content may conflict with or override these specifications"
Removed sections: None (additive change)
Templates requiring updates: None
Follow-up TODOs: Ensure all three exam-writing docs exist and are synchronized
Breaking change: None (strengthens existing requirements)
Previous amendment: 3.2.0 (Amendment 6) - JSON schema governance
-->

# AIExamForge Constitution

## Core Principles

### I. Technology Stack & Architecture
**Backend**: C# with ASP.NET Core on .NET 10 (LTS). Host on Azure App Service.

**Design patterns**: Minimal APIs for HTTP endpoints; OpenAPI-first; clean separation (Web/API → Application → Domain → Infrastructure → Tests). All domain logic MUST be UI-agnostic.

**Async-first**: Use async/await throughout. Enforce dependency injection, options pattern, and structured error handling.

**Frontend**: Modern SPA using React + TypeScript

*Rationale*: Deliberate stack choices reduce decision friction and ensure team consistency. Layered architecture enables testing, reuse, and scalability.

### II. Security & Content Integrity
**Authentication**: Must be production-ready. Default: Microsoft Entra ID via OpenID Connect. Never store secrets in code; use App Service configuration and/or Azure Key Vault.

**Input validation**: Server-side validation mandatory for ALL user input. Enforce size limits and schema validation. Implement rate limiting and abuse prevention for generation endpoints.

**Content policy**: Generate ORIGINAL practice questions only. Do NOT reproduce real Microsoft exam items or proprietary content. Include UI disclaimers, README disclaimers, and prompt guardrails in code.

**File Upload & Download Safety**: 
- Support uploading exam outlines as .docx and .pdf.
- Enforce strict file size limits and content-type validation server-side.
- Reject password-protected/encrypted PDFs for v1 with a clear user message.
- Do not execute macros; do not trust embedded objects.
- Scan uploads for malware when hosting in production (use a scanning step/service).
- Store uploaded files temporarily; delete after processing unless user explicitly saves them.
- Generated downloads (.docx/.pdf) must not include hidden data, secrets, or internal MCP reference IDs.

*Rationale*: Security failures and content violations expose the application to legal, regulatory, and reputational risk. Defense-in-depth (code + config + secrets mgmt) is non-negotiable. File handling is a critical attack surface for malware, data exfiltration, and information leakage.

### III. Testing Standards
**Test coverage**: Unit tests MUST cover outline parsing, request validation, question schema validation, MCP response handling, and per-type question renderers. Integration tests MUST cover key API endpoints and MCP integration.

**CI gates**: Build, tests, and formatting checks are REQUIRED on all PRs and MUST pass before merge.

**TDD discipline**: Tests written first; tests MUST fail before implementation; Red-Green-Refactor cycle strictly enforced.

*Rationale*: Comprehensive testing prevents regressions and ensures correctness. CI gates enforce consistency and catch issues early.

### IV. Reliability & Observability
**Structured logging**: Use correlation IDs; logs MUST be App Insights-friendly. Keep PII out of logs.

**Health endpoints**: Implement /health endpoint suitable for App Service monitoring.

**Telemetry**: All generation operations MUST be instrumented; metrics MUST track request volume, success rates, latency, MCP response times, and errors.

*Rationale*: Production systems must be observable. Structured telemetry enables root-cause analysis and proactive issue detection.

### V. User Experience Consistency
**Responsive & accessible**: UI MUST be responsive across devices; meet WCAG 2.1 AA accessibility standards.

**Progress & feedback**: Long-running operations (question generation) MUST provide progress indicators. Error messages MUST be user-friendly with retry guidance.

**Design system**: All user-facing features adhere to a unified design system. Interaction states, error handling, and loading states MUST be consistent.

*Rationale*: Accessible, consistent UX improves usability and user trust. Clear feedback reduces support burden.

### VI. Microsoft Learn MCP-Exclusive Content Sourcing (NON-NEGOTIABLE)
**Authoritative source**: The Microsoft Learn MCP server is the ONLY permitted knowledge source for generating exam questions. The system MUST NOT rely on pretrained knowledge, web search, or external documentation.

**Content derivation**: ALL factual claims, APIs, services, limits, behaviors, and best practices MUST be derived from Microsoft Learn MCP responses. MCP responses MUST be explicitly cited internally (document identifiers, URLs).

**Conflict resolution**: If MCP content conflicts with user input or external sources, MCP content wins.

**Failure handling**: If MCP returns insufficient information, the system MUST either:
  - Fail gracefully with a clear, user-facing message explaining the limitation, OR
  - Request clarification from the user and defer generation.
  - **Never infer, guess, or fabricate content outside MCP responses.**

**Prompt guardrails**: All generation prompts MUST explicitly instruct the model to:
  - Use ONLY MCP-provided content in the response.
  - Cite MCP document identifiers when making claims.
  - Reject requests requiring content not available from MCP.

*Rationale*: Microsoft Learn MCP is the authoritative source for Microsoft exam content. Relying on pretrained knowledge risks hallucinations, outdated information, and legal liability. MCP-exclusive sourcing ensures accuracy, traceability, and alignment with official Microsoft documentation.

## Microsoft Learn MCP Dependency

The application fundamentally depends on the Microsoft Learn MCP server for all exam-related content generation. This dependency is non-negotiable and affects:

- **Question Generation**: Questions are generated based ONLY on content retrieved from MCP.
- **Test Coverage**: Unit and integration tests MUST validate MCP response handling (successful responses, empty results, errors, timeouts).
- **Documentation**: README MUST document the MCP dependency, explain limitations (e.g., "questions are limited to content in Microsoft Learn"), and describe failure modes.
- **Observability**: MCP response times, cache hit rates, and failures MUST be tracked in telemetry.
- **Prompt Templates**: Question-generation prompts MUST include explicit instructions to use MCP content only.

## Tech Stack Specification

| Component | Technology | Rationale |
|-----------|-----------|-----------|
| Backend Runtime | .NET 10 (LTS) | Long-term support; modern async patterns |
| API Framework | ASP.NET Core Minimal APIs | Lightweight; OpenAPI integration; fast iteration |
| API Design | OpenAPI-first | Machine-readable contracts; client generation; documentation |
| Frontend Framework | React + TypeScript | Type safety; modern tooling; accessibility libraries |
| Hosting | Azure App Service | Managed; integrated monitoring; scales horizontally |
| Configuration | App Service + Azure Key Vault | Secure secrets; environment-specific config; no hardcoding |
| Authentication | Microsoft Entra ID (OpenID Connect) | Production-grade; integrates with Azure |
| Logging | Structured (App Insights compatible) | Centralized observability; correlation IDs |
| Testing | xUnit + Moq (backend); Jest/Vitest + React Testing Library (frontend) | Standard; good async support; fast feedback |
| Content Source | Microsoft Learn MCP server | Authoritative, official Microsoft documentation; MCP-exclusive |

## Development Workflow

1. **MCP-first design**: Question generation architecture designed around MCP as exclusive content source. Prompts explicitly instruct model to use MCP content only.
2. **Security first**: Secrets in Key Vault, input validation server-side, rate limits on generation endpoints.
3. **Content guardrails**: Prompt templates include disclaimer language and MCP-exclusive instructions; question schema validates against reuse detection.
4. **Test-driven**: Write failing test → user approves → implement → verify pass. MCP response handling tests are mandatory.
5. **CI/CD discipline**: All PRs run build + tests + linting. No merge without green CI.
6. **Structured telemetry**: Log correlation IDs; track generation requests and MCP response metrics; monitor health endpoint.
7. **Code reviews**: Mandatory for all changes; verify principle compliance, security checks, test coverage, and MCP compliance.

## Deployment & Documentation

**README requirements**: Local dev setup, configuration (Key Vault, Entra ID), deployment steps, content disclaimers, **MCP dependency explanation**, failure modes, and troubleshooting.

**Version control**: Prompt templates, question-type schemas, and MCP prompt instructions MUST be versioned in repo. Breaking changes to question schema or MCP requirements require version bump.

**Documentation standards**: Keep docs in-repo; explain trade-offs; reference constitution for architecture decisions. Document MCP limitations and failure modes.

## Governance

**Decision precedence**: MCP correctness > Security > Spec requirements > Convenience.

**Amendments**: Constitution requires written justification, team review, and explicit approval. Non-trivial changes bump MINOR version. Breaking architectural changes (e.g., new MCP requirements, stack changes, content sourcing rules) bump MAJOR version.

**Compliance checks**: Every plan and PR must verify compliance against all six principles, with explicit MCP compliance verification. Violations must be documented and justified (with risk trade-offs) in the plan.

All application code MUST be generated to satisfy the authoritative specifications.
Specifications are not derived from code; code is derived from specifications.

### Authoritative Exam Authoring Specifications (NON-NEGOTIABLE)

The following files are the **authoritative source of truth** for all exam item generation, validation, and enforcement:

- `docs/exam-writing-style-guide.md`
- `docs/exam-writing-style-rules.md`
- `docs/exam-writing-traceability.md`
- `docs/item-types.md`
- `docs/schemas/question.schema.json`
- `docs/schemas/common/*.schema.json`
- `docs/schemas/item-types/*.schema.json`

**All specifications, plans, tasks, validators, and generated code MUST conform to these documents.**
**No content may conflict with or override these specifications.**

Additional authoritative files:

| File | Purpose |
|------|---------|
| `docs/style-governance.md` | Style enforcement rules and lint configurations |
| `prompts/system-mcp-only.md` | System prompt enforcing MCP-exclusive content |
| `prompts/exam-style.md` | Exam-style tone and formatting instructions |
| `prompts/item-types/*.md` | Per-type prompt fragments for generation |

**Output Contracts (Validation Enforcement)**:
- ALL generated questions MUST validate against `docs/schemas/question.schema.json` (which references per-type schemas).
- The application MUST apply lint rules derived from `style-governance.md` to all generated content.
- Questions that **fail schema or lint validation MUST be rejected** and either:
  - Regenerated automatically (with retry limit), OR
  - Returned as errors to the user with specific validation failure details.
- **No invalid questions may be persisted or exported.**

**Style Governance Hierarchy**:
- **Exam item text** (stems, choices, explanations): MUST follow `docs/exam-writing-style-guide.md` and `docs/item-types.md`.
- **UI, help text, README, and documentation**: MUST follow the [Microsoft Writing Style Guide](https://learn.microsoft.com/style-guide/welcome/).
- **Conflict resolution**: If rules conflict, the Exam Writing Style Guide wins for exam items.

**Schema/prompt synchronization**:
- Code-level schemas and prompt templates MUST be generated from or validated against these docs.
- Any change to these docs requires corresponding updates to code schemas and prompts.
- CI MUST verify schema/prompt consistency with source docs.

*Rationale*: Centralizing style and type definitions in versioned docs ensures consistency across prompts, validation, and exports. Repo files are authoritative; code is derived. This prevents drift between documentation and implementation.

**MCP Compliance Verification**: Every feature, endpoint, and prompt that generates exam content MUST verify that:
  - Content is derived from MCP responses only.
  - Prompts include explicit MCP-exclusive instructions.
  - Tests cover MCP response handling (success, empty, error cases).
  - Failure modes are handled gracefully with user-facing error messages.
  - README documents the MCP dependency and limitations.

**Guidance files**: Use `.github/agents/` for AI assistant guidance; update when principles change. Include MCP usage rules in agent-specific guidance.

**Version**: 3.3.0 | **Ratified**: 2026-01-02 | **Last Amended**: 2026-01-02
