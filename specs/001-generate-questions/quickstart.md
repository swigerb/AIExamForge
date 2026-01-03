# Quickstart: AIExamForge Development

**Feature**: 001-generate-questions  
**Date**: 2026-01-02

## Prerequisites

### Required Software

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 10.0+ | Backend runtime |
| Node.js | 20.x LTS | Frontend tooling |
| SQL Server | LocalDB or Docker | Local database |
| Azure CLI | Latest | Cloud resource management |
| Git | Latest | Version control |

### Azure Resources (Development)

For local development, you'll need access to:

1. **Azure OpenAI Service** - For question generation
2. **Azure AI Document Intelligence** - For PDF extraction
3. **Azure Blob Storage** - For file storage (or use Azurite emulator)

> **Tip**: Use Azure Developer CLI (`azd`) to provision dev resources automatically.

---

## Local Setup

### 1. Clone Repository

```bash
git clone https://github.com/your-org/aiexamforge.git
cd aiexamforge
```

### 2. Backend Setup

```bash
# Navigate to backend
cd backend

# Restore packages
dotnet restore

# Create appsettings.Development.json
cp src/AIExamForge.Api/appsettings.json src/AIExamForge.Api/appsettings.Development.json
```

Edit `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=AIExamForge;Trusted_Connection=True;"
  },
  "AzureOpenAI": {
    "Endpoint": "https://your-openai.openai.azure.com/",
    "DeploymentName": "gpt-4-turbo",
    "ApiKey": "your-api-key"
  },
  "AzureDocumentIntelligence": {
    "Endpoint": "https://your-di.cognitiveservices.azure.com/",
    "ApiKey": "your-api-key"
  },
  "BlobStorage": {
    "ConnectionString": "UseDevelopmentStorage=true"
  },
  "MCP": {
    "Endpoint": "https://learn.microsoft.com/mcp",
    "TimeoutSeconds": 10
  }
}
```

### 3. Database Setup

```bash
# Apply migrations
cd src/AIExamForge.Api
dotnet ef database update

# Verify database created
# Check SQL Server Object Explorer in VS or Azure Data Studio
```

### 4. Start Blob Emulator (Azurite)

```bash
# Install Azurite if not already installed
npm install -g azurite

# Start Azurite
azurite --silent --location ./azurite-data --debug ./azurite-debug.log
```

### 5. Run Backend

```bash
# From backend/src/AIExamForge.Api
dotnet run

# API available at: https://localhost:5001
# Swagger UI at: https://localhost:5001/swagger
```

### 6. Frontend Setup

```bash
# Navigate to frontend
cd ../../frontend

# Install dependencies
npm install

# Create .env.development
echo "VITE_API_BASE_URL=https://localhost:5001/api" > .env.development
```

### 7. Run Frontend

```bash
npm run dev

# App available at: http://localhost:5173
```

---

## Verify Setup

### Backend Health Check

```bash
curl https://localhost:5001/api/health
```

Expected response:
```json
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "blobStorage": "Healthy",
    "mcpService": "Healthy",
    "openAiService": "Healthy"
  }
}
```

### Frontend Connection

1. Open http://localhost:5173
2. You should see the AIExamForge home page
3. Navigation should work (routes: /, /outline, /generate, /export)

### Test Outline Parsing

```bash
curl -X POST https://localhost:5001/api/outlines/parse \
  -H "Content-Type: application/json" \
  -d '{"text": "1. Azure Compute Solutions\n1.1 Virtual Machines\n1.2 Azure Functions\n2. Storage Solutions\n2.1 Blob Storage"}'
```

---

## Development Workflow

### Running Tests

```bash
# Backend unit tests
cd backend
dotnet test --filter "Category=Unit"

# Backend integration tests (requires dependencies)
dotnet test --filter "Category=Integration"

# Frontend tests
cd frontend
npm test
```

### Code Quality

```bash
# Backend formatting
cd backend
dotnet format --verify-no-changes

# Frontend linting
cd frontend
npm run lint
npm run format:check
```

### Generate API Client

After modifying backend endpoints:

```bash
# Export OpenAPI spec
cd backend/src/AIExamForge.Api
dotnet run -- export-openapi > ../../../specs/001-generate-questions/contracts/openapi.yaml

# Regenerate frontend client
cd ../../../frontend
npm run generate-api
```

---

## Project Structure Quick Reference

```
aiexamforge/
├── backend/
│   ├── AIExamForge.sln
│   ├── src/
│   │   ├── AIExamForge.Api/           # Entry point, endpoints
│   │   ├── AIExamForge.Application/   # Business logic, services
│   │   ├── AIExamForge.Domain/        # Entities, value objects
│   │   └── AIExamForge.Infrastructure/ # DB, external services
│   └── tests/
│       ├── AIExamForge.Tests.Unit/
│       └── AIExamForge.Tests.Integration/
├── frontend/
│   ├── src/
│   │   ├── api/generated/             # Generated API client
│   │   ├── components/                # Reusable UI components
│   │   ├── pages/                     # Route pages
│   │   └── hooks/                     # Custom React hooks
│   └── tests/
└── specs/
    └── 001-generate-questions/        # Feature documentation
```

---

## Common Issues

### Database Connection Failed

**Error**: `A network-related or instance-specific error occurred`

**Solution**: 
1. Ensure SQL Server LocalDB is installed and running
2. Or use Docker: `docker run -e ACCEPT_EULA=Y -e SA_PASSWORD=P@ssw0rd -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest`

### Azurite Connection Failed

**Error**: `Unable to connect to Blob Storage`

**Solution**:
1. Ensure Azurite is running: `azurite --silent`
2. Check connection string is `UseDevelopmentStorage=true`

### MCP Service Unavailable

**Error**: `MCP service returned error`

**Solution**:
1. Verify network connectivity to Microsoft Learn MCP endpoint
2. Check MCP credentials in configuration
3. For local testing, use MCP stub/mock (see test fixtures)

### Azure OpenAI Rate Limited

**Error**: `429 Too Many Requests`

**Solution**:
1. Wait for rate limit reset (check Retry-After header)
2. Reduce concurrent requests
3. Consider increasing Azure OpenAI TPM quota

---

## Style & Item-Type Validation

### Understanding the Validation Architecture

AIExamForge uses a two-stage validation pipeline:

1. **Schema Validation**: JSON Schema validation against `docs/schemas/item-types/{CODE}.schema.json`
2. **Lint Validation**: Style rule enforcement from `docs/exam-writing-style-guide.md`

### Adding a New Item Type

1. **Define the type** in `docs/item-types.md`
2. **Create the JSON schema** in `docs/schemas/item-types/{CODE}.schema.json`
3. **Create the prompt fragment** in `prompts/item-types/{CODE}.md`
4. **Register the type** in `QuestionTypeRegistry`
5. **Add tests** in `AIExamForge.Application.Tests/Validation/`

### Running Validation Tests

```bash
cd backend
dotnet test --filter "Category=Validation"
```

### Lint Rules Enforced

| Rule | Description | Example Violation |
|------|-------------|-------------------|
| NoContractions | No contractions in exam text | "don't" → "do not" |
| NoExplanationsInItemText | Explanations in separate field only | "because it uses..." in stem |
| NumericOrderPreserved | Numeric choices in ascending order | "10, 5, 15, 20" |

### Render Modes

- **Exam Mode**: Questions only, no explanations (simulates real exam)
- **Study Mode**: Questions + explanations in separate section (for learning)

Toggle in UI or via API: `?renderMode=exam` or `?renderMode=study`

---

## Next Steps

1. **Read the [plan.md](plan.md)** for architecture overview
2. **Review [data-model.md](data-model.md)** for entity definitions
3. **Check [tasks.md](tasks.md)** for implementation checklist
4. **Start with Milestone 1** (Foundation + UI + Parsing)

---

**Generated**: 2026-01-02 | **Status**: Ready for development
