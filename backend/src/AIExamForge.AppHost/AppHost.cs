var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.AIExamForge_Api>("api")
    .WithExternalHttpEndpoints();

// Run the Vite dev server as an executable resource so it starts/stops with the AppHost.
// The UI reads its API base URL from VITE_API_BASE_URL (see frontend/src/services/api.ts).
static string FindRepoRoot(string startDir)
{
    var dir = new DirectoryInfo(startDir);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "AIExamForge.slnx")))
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    throw new DirectoryNotFoundException("Could not locate repo root containing AIExamForge.slnx.");
}

static string? FindOnPath(string fileName)
{
    var path = Environment.GetEnvironmentVariable("PATH");
    if (string.IsNullOrWhiteSpace(path))
    {
        return null;
    }

    foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        try
        {
            var candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        catch
        {
            // ignore malformed PATH entries
        }
    }

    return null;
}

var repoRoot = FindRepoRoot(builder.Environment.ContentRootPath);
var uiWorkingDir = Path.Combine(repoRoot, "frontend");

var npmExe = OperatingSystem.IsWindows() ? "npm.cmd" : "npm";
var npmPath = FindOnPath(npmExe);

if (npmPath is not null && Directory.Exists(uiWorkingDir))
{
    // On Windows, npm is a .cmd shim. Execute it through cmd.exe to avoid CreateProcess issues.
    var uiExe = OperatingSystem.IsWindows() ? "cmd.exe" : npmExe;

    var ui = (OperatingSystem.IsWindows()
            ? builder.AddExecutable("frontend", uiExe, uiWorkingDir, "/c", "npm", "run", "dev", "--", "--port", "5173")
            : builder.AddExecutable("frontend", uiExe, uiWorkingDir, "run", "dev", "--", "--port", "5173"))
        .WithHttpEndpoint(targetPort: 5173, name: "http")
        .WithExternalHttpEndpoints()
        .WithUrlForEndpoint("http", url => url.DisplayText = "Open Frontend");

    ui.WithEnvironment("BROWSER", "none");

    // Best-effort wiring: pass API base URL into the UI.
    // Prefer HTTP locally to avoid dev-cert trust issues that can break fetch/EventSource.
    try
    {
        ui.WithEnvironment("VITE_API_BASE_URL", $"{api.GetEndpoint("http").Url.TrimEnd('/')}/api");
    }
    catch
    {
        try
        {
            ui.WithEnvironment("VITE_API_BASE_URL", $"{api.GetEndpoint("https").Url.TrimEnd('/')}/api");
        }
        catch
        {
            // no-op
        }
    }
}

builder.Build().Run();
