<p align="center">
  <img src="frontend/src/assets/logo.png" alt="AI Exam Forge" width="400" />
</p>

<h1 align="center">AI Exam Forge</h1>

<p align="center">
  AI-powered exam question generator using Microsoft Learn content through Model Context Protocol (MCP).
</p>

<p align="center">
  <a href="#overview">Overview</a> •
  <a href="#quickstart">Quickstart</a> •
  <a href="#aspire-dashboard-opentelemetry">Aspire Dashboard</a> •
  <a href="#azure-deployment">Azure Deployment</a> •
  <a href="#architecture">Architecture</a> •
  <a href="#documentation">Documentation</a>
</p>

---

## Overview

AI Exam Forge generates high-quality practice exam questions by leveraging Microsoft Learn documentation through the Model Context Protocol (MCP). All question content is derived exclusively from official Microsoft documentation, ensuring accuracy and alignment with certification exam objectives.

### Key Features

- **MCP-Powered Content**: All questions are sourced from Microsoft Learn documentation
- **Multiple Question Types**: Supports various item types including multiple choice, drag-and-drop, and build list
- **Quality Validation**: Automated lint validation ensures compliance with exam writing standards
- **Export Formats**: Export questions as JSON, Markdown, DOCX, or PDF
- **Study/Exam Modes**: Toggle between study mode (with explanations) and exam mode (without)

### Core Objectives

- **Always up-to-date Learn content** without running your own crawler/indexer
- **Query-time retrieval** (“generate questions for AZ-204 storage triggers” and it pulls relevant pages on demand)
- **Better grounding + citation paths** as the MCP server returns sources/URLs/sections
- **Centralized governance**: the backend controls what tools are available, logging, rate limits, prompt injection defenses, etc.

---

## Quickstart

### Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 10.0+ | Backend runtime |
| Node.js | 20.19+ LTS | Frontend tooling |
| Azure CLI | Latest | Cloud resource management |
| Git | Latest | Version control |

### Local Setup

#### 1. Clone Repository

```bash
git clone https://github.com/swigerb/AIExamForge.git
cd AIExamForge
```

#### 2. Build the solution

```bash
dotnet restore
dotnet build
```

Edit `backend/src/AIExamForge.Api/appsettings.json` with your Azure service endpoints (or provide them via environment variables / Key Vault).

Optional: create a local-only override file at `backend/src/AIExamForge.Api/appsettings.Development.json` (this file is gitignored).

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-openai.openai.azure.com/",
    "DeploymentName": "gpt-4-turbo",
    "ApiKey": "your-api-key"
  },
  "BlobStorage": {
    "ConnectionString": "UseDevelopmentStorage=true"
  }
}
```

Environment variable alternatives (recommended for local dev):

- `AzureOpenAI__Endpoint`
- `AzureOpenAI__DeploymentName`
- `AzureOpenAI__ApiKey`

#### 3. Run via Aspire AppHost (recommended)

This starts:

- the Aspire Dashboard
- the API
- the Vite frontend (if Node/npm are installed and `frontend/` dependencies are installed)

```bash
dotnet run --project backend/src/AIExamForge.AppHost
```

Then open the frontend from the Aspire dashboard (look for **Open Frontend**).

#### 4. Run API (standalone)

```bash
# From repo root:
cd backend/src/AIExamForge.Api
dotnet run

# API available at: http://localhost:5180
# OpenAPI document (Development only): http://localhost:5180/openapi/v1.json
# HTTPS is available via the "https" launch profile if you want it.
```

### Aspire Dashboard (OpenTelemetry)

When running in Development, the API exports OpenTelemetry traces/logs/metrics via OTLP so you can view:

- UI → API requests (ASP.NET Core + HttpClient instrumentation)
- MCP retrieval spans (custom spans: `mcp.query` and `mcp.tools.call`)

By default, the API exports OTLP over gRPC to `http://localhost:18889`.

#### 1. Start the Aspire Dashboard

Option A: Use the Aspire dashboard command (if you have the Aspire CLI/workload installed):

```bash
dotnet aspire dashboard
```

Option B: Use Docker terminal (requires Docker Desktop to be running):

```bash
docker run --rm -it -p 18888:18888 -p 18889:18889 mcr.microsoft.com/dotnet/aspire-dashboard:latest
```

#### 2. Run the API (Development)

Start the API using the `http` or `https` launch profile. The repo includes Development defaults in the launch profile:

- `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:18889`
- `OTEL_EXPORTER_OTLP_PROTOCOL=grpc`
- `OTEL_TRACES_SAMPLER=always_on`

You can override these with environment variables, or with:

- `OpenTelemetry:Enabled`
- `OpenTelemetry:OtlpEndpoint`

#### 3. View traces in the dashboard

Open the dashboard UI:

- <http://localhost:18888>

Look for the service:

- `AIExamForge.Api`

Useful spans/tags to filter on:

- Span names: `mcp.query`, `mcp.tools.call`
- Tag: `mcp.tool.name` (tool being called)
- Tag: `mcp.query_id` (correlates all tool calls for one query)

#### 5. Frontend Setup (standalone)

```bash
cd frontend
npm install
echo "VITE_API_BASE_URL=http://localhost:5180/api" > .env.development
npm run dev

# App available at: http://localhost:5173
```

Note: `frontend/.env.development` is gitignored. Use it for your local API URL.

### Verify Setup

```bash
# Backend health check
curl http://localhost:5180/health
```

### Running Tests

```bash
# Backend tests
dotnet test

# Frontend tests
cd frontend
npm test
```

Frontend tests are executed with Vitest.

### Common Issues

| Issue | Solution |
|-------|----------|
| Database connection failed | Ensure SQL Server LocalDB is installed and running |
| Azurite connection failed | Run `azurite --silent` in a separate terminal |
| MCP service unavailable | Verify network connectivity to Microsoft Learn |
| Azure OpenAI rate limited | Wait for reset or increase TPM quota |

---

## Azure Deployment

AIExamForge is configured for deployment using the [Azure Developer CLI (azd)](https://learn.microsoft.com/azure/developer/azure-developer-cli/).

### Prerequisites

1. **Azure Developer CLI** - Install from [aka.ms/azd-install](https://aka.ms/azd-install)
2. **Azure subscription** - With permissions to create resources
3. **.NET 10.0 SDK** - For building the backend
4. **Node.js 20.x** - For building the frontend

### Deploy with `azd up`

The `azd up` command provisions all Azure resources and deploys the application in a single step:

```bash
# Login to Azure
azd auth login

# Initialize and deploy (first time)
azd up
```

You'll be prompted to:
- Select an Azure subscription
- Choose a deployment region
- Provide an environment name (e.g., `dev`, `prod`)

### What Gets Deployed

| Resource | Description |
|----------|-------------|
| **Azure App Service** | Hosts the .NET API backend |
| **Azure Static Web App** | Hosts the React frontend |
| **Azure OpenAI Service** | Powers question generation (GPT-4) |
| **Azure Key Vault** | Securely stores secrets and API keys |
| **Azure Storage Account** | Stores uploaded outlines and generated content |
| **Application Insights** | Monitoring and diagnostics |
| **Log Analytics Workspace** | Centralized logging |

### Common `azd` Commands

```bash
azd deploy              # Deploy code changes only (skip infrastructure)
azd show                # View deployed resources
azd monitor --overview  # Open the deployed application in browser
azd down                # Tear down all resources
azd env set VAR value   # Set environment variables
azd env list            # View current environment
```

### Environment Configuration

After deployment, configure secrets in Azure Key Vault:

```bash
# Get the Key Vault name
azd env get-values | grep AZURE_KEY_VAULT_NAME

# Add secrets via Azure CLI
az keyvault secret set --vault-name <your-keyvault> --name "AzureOpenAI--ApiKey" --value "<your-key>"
```

### Cost Considerations

| Resource | Default Tier |
|----------|--------------|
| App Service | Basic (B1) |
| Static Web App | Free |
| Azure OpenAI | Pay-as-you-go |
| Storage | Standard LRS |

For production workloads, consider upgrading the App Service plan and enabling autoscaling.

---

## Architecture

### Process Flow
1.	Frontend calls backend API: “Generate 15 questions for topic X”
2.	Backend API calls the AI model with tool access enabled
3.	AI Model calls MCP tools like “search docs” / “get page” with Microsoft Learn MCP server
4.	Backend receives tool results, model generates questions grounded in that content
5.	Backend returns JSON to Frontend

- Frontend (Vite/TS/React): purely UI
-	Backend API (ASP.NET Core):
    - authenticates user
    - calls AI LLM
    -	enables tool calling to Microsoft Learn MCP server
    -	validates and post-processes output (lint rules, schema validation, dedupe)
    -	stores results and citations
    - generates DOCX/MD/PDF/JSON output of questions

### Application Flow (Mermaid)

```mermaid
sequenceDiagram
  autonumber
  actor User
  participant Web as Web UI (React)
  participant API as API (.NET Minimal API)
  participant DB as SQL Database
  participant Blob as Blob Storage
  participant Worker as Background Worker
  participant MCP as Microsoft Learn MCP
  participant AOAI as Azure OpenAI

  User->>Web: Upload/paste exam outline
  alt Paste outline text
    Web->>API: POST /api/outlines/parse
    API->>DB: Persist outline + nodes
    API-->>Web: OutlineResponse
  else Upload DOCX/PDF
    Web->>API: POST /api/outlines/upload (multipart)
    API->>Blob: Store uploaded document
    API->>DB: Persist extracted outline
    API-->>Web: FileUploadResponse + OutlineResponse
  end

  User->>Web: Select outline sections + options
  Web->>API: POST /api/generations
  API->>DB: Create GenerationRequest (Queued)
  API-->>Web: 202 Accepted (generationId + streamUrl)

  Web->>API: GET /api/generations/{id}/stream (SSE)
  Note over Web,API: SSE events: progress | question | complete | error

  API->>Worker: Dequeue GenerationWorkItem
  Worker->>DB: Update status: RetrievingContent
  Worker->>MCP: Retrieve Microsoft Learn content
  Worker->>DB: Update status: Generating
  Worker->>AOAI: Generate questions from MCP-grounded prompt
  Worker->>DB: Update status: Validating
  Worker->>DB: Persist QuestionSet + Questions
  Worker-->>API: Publish progress/question events
  API-->>Web: Stream progress/question/complete

  User->>Web: Download export (json/md/docx/pdf)
  Web->>API: GET /api/questions/{generationId}/export?format=...&mode=...
  API->>Blob: (Optional) Store export artifact/record
  API-->>Web: File download
```

### Deployment Architecture (Mermaid)

```mermaid
graph LR
  U[User] --> Web[Azure Static Web App]
  Web --> Api[Azure App Service API]

  U --> Entra[Microsoft Entra ID]
  Entra --> Api

  Api --> Kv[Azure Key Vault]
  Api --> Aoai[Azure OpenAI]
  Api --> Mcp[Microsoft Learn MCP]
  Api --> Storage[Azure Storage Account]
  Api --> Db[(SQL Database)]
  Api --> Ai[Application Insights]
  Ai --> La[Log Analytics Workspace]
```

AIExamForge follows Clean Architecture principles:

```
backend/
├── src/
│   ├── AIExamForge.Api/           # HTTP API endpoints
│   ├── AIExamForge.Application/   # Business logic and services
│   ├── AIExamForge.Domain/        # Domain entities and value objects
│   └── AIExamForge.Infrastructure/ # External dependencies (DB, MCP, Storage)
└── tests/
    ├── AIExamForge.UnitTests/
    ├── AIExamForge.IntegrationTests/
    └── AIExamForge.ContractTests/

frontend/
└── src/
    ├── components/    # Reusable UI components
    ├── pages/         # Route pages (wizard steps)
    ├── types/         # TypeScript type definitions
    └── api/           # API client
```

### MCP Dependency

AIExamForge relies on the Model Context Protocol (MCP) to retrieve content from Microsoft Learn documentation. MCP provides structured access to Microsoft's official documentation, ensuring all generated questions are grounded in authoritative sources.

**Content Limitations**: Questions are limited to topics documented in Microsoft Learn. Content not yet published, recently updated, or region-specific may have limited coverage.

| Scenario | Recovery Action |
|----------|-----------------|
| MCP timeout | Reduce topic scope, retry |
| MCP unavailable | Wait 5-10 minutes, retry |
| Empty results | Verify topic names match Microsoft Learn |
| Rate limited | Wait as indicated, retry |

---

## Configuration

### Required Settings

| Setting | Description |
|---------|-------------|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string |
| `AzureOpenAI:Endpoint` | Azure OpenAI service endpoint |
| `AzureOpenAI:ApiKey` | Azure OpenAI API key |

### Optional Settings

| Setting | Description | Default |
|---------|-------------|---------|
| `DatabaseSettings:CommandTimeout` | SQL command timeout (seconds) | 30 |
| `ApplicationInsights:ConnectionString` | App Insights telemetry | (disabled) |
| `KeyVault:VaultUri` | Azure Key Vault URI | (disabled) |

---

## Documentation

- [Exam Writing Style Guide](docs/exam-writing-style-guide.md) - Question authoring standards
- [Style Governance](docs/style-governance.md) - Style precedence rules
- [Data Model](specs/001-generate-questions/data-model.md) - Entity definitions
- [API Contracts](specs/001-generate-questions/contracts/) - OpenAPI specification
- [Item Types](docs/item-types.md) - Supported question types

---

## License

Licensed under the MIT License. See [LICENSE](LICENSE).

## Contributing

Please read the contribution guidelines before submitting pull requests.

## Disclaimer

AIExamForge generates practice exam questions based on Microsoft Learn content. These questions are:

- **Not actual exam questions**: All content is original practice material
- **Not endorsed by Microsoft**: This is an independent educational tool
- **Subject to content limitations**: Questions are limited to topics covered in Microsoft Learn
- **For practice only**: Not intended to represent actual certification exam content or difficulty
