using System.Diagnostics;
using System.Text.Json;
using NETGal.Engine;

var command = args.FirstOrDefault()?.ToLowerInvariant();
var commandArgs = args.Skip(1).ToArray();

if (command is "new" or "create")
{
    var directory = commandArgs.ElementAtOrDefault(0) ?? "MyGame";
    var title = commandArgs.ElementAtOrDefault(1) ?? Path.GetFileName(Path.GetFullPath(directory));
    var target = Path.GetFullPath(directory);
    Directory.CreateDirectory(target);
    Directory.CreateDirectory(Path.Combine(target, "assets"));
    await GameProject.CreateSample(title).SaveAsync(Path.Combine(target, "game.json"));
    Console.WriteLine($"Created NETGal project: {target}");
    Console.WriteLine("Next: dotnet run --project src/NETGal -- play " + directory);
    return;
}

if (command == "build")
{
    var projectDirectory = Path.GetFullPath(commandArgs.ElementAtOrDefault(0) ?? ".");
    var outputDirectory = Path.GetFullPath(commandArgs.ElementAtOrDefault(1) ?? Path.Combine(projectDirectory, "dist"));
    var builder = new StaticPackageBuilder(Path.Combine(AppContext.BaseDirectory, "wwwroot", "player-template"));
    var result = await builder.BuildAsync(projectDirectory, outputDirectory);
    Console.WriteLine($"Built game package: {result.OutputDirectory}");
    Console.WriteLine($"Zip archive: {result.ZipPath}");
    Console.WriteLine($"Assets copied: {result.AssetCount}");
    return;
}

if (command is "publish" or "native")
{
    var projectDirectory = Path.GetFullPath(commandArgs.ElementAtOrDefault(0) ?? ".");
    var platform = commandArgs.ElementAtOrDefault(1)?.ToLowerInvariant() ?? "windows";
    var configuration = commandArgs.ElementAtOrDefault(2) ?? "Release";
    var outputDirectory = Path.GetFullPath(commandArgs.ElementAtOrDefault(3) ?? Path.Combine(projectDirectory, "dist", platform));
    await PublishNativePlayerAsync(projectDirectory, platform, configuration, outputDirectory);
    return;
}

var projectPath = command is "play" or "editor" ? commandArgs.ElementAtOrDefault(0) : null;
var projectDirectoryForServer = Path.GetFullPath(projectPath ?? ".");
if (!File.Exists(Path.Combine(projectDirectoryForServer, "game.json")))
{
    var sampleDirectory = Path.Combine(projectDirectoryForServer, "samples", "Starfall");
    Directory.CreateDirectory(Path.Combine(sampleDirectory, "assets"));
    await GameProject.CreateSample("Starfall · A Tiny Story").SaveAsync(Path.Combine(sampleDirectory, "game.json"));
    projectDirectoryForServer = sampleDirectory;
    Console.WriteLine($"No game.json found. Created a starter project at {sampleDirectory}");
}

var builderApp = WebApplication.CreateBuilder(args);
builderApp.WebHost.UseUrls("http://localhost:5178");
var app = builderApp.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

var projectFile = Path.Combine(projectDirectoryForServer, "game.json");
var assetsDirectory = Path.Combine(projectDirectoryForServer, "assets");
Directory.CreateDirectory(assetsDirectory);
var projectLock = new SemaphoreSlim(1, 1);

app.MapGet("/api/project", async (CancellationToken cancellationToken) =>
{
    var project = await GameProject.LoadAsync(projectFile, cancellationToken);
    return Results.Json(project, JsonOptions.Default);
});

app.MapPut("/api/project", async (HttpRequest request, CancellationToken cancellationToken) =>
{
    var project = await JsonSerializer.DeserializeAsync<GameProject>(request.Body, JsonOptions.Default, cancellationToken);
    if (project is null)
    {
        return Results.BadRequest(new { message = "Project payload is empty." });
    }

    var issues = ProjectValidator.Validate(project);
    if (issues.Any(issue => issue.Severity == "error"))
    {
        return Results.BadRequest(new { message = "Fix validation errors before saving.", issues });
    }

    await projectLock.WaitAsync(cancellationToken);
    try
    {
        await project.SaveAsync(projectFile, cancellationToken);
    }
    finally
    {
        projectLock.Release();
    }

    return Results.Json(new { savedAt = DateTimeOffset.Now, project }, JsonOptions.Default);
});

app.MapGet("/api/validate", async (CancellationToken cancellationToken) =>
{
    var project = await GameProject.LoadAsync(projectFile, cancellationToken);
    return Results.Json(new { issues = ProjectValidator.Validate(project) }, JsonOptions.Default);
});

app.MapGet("/api/assets/{**assetPath}", (string assetPath) =>
{
    var safePath = Path.GetFullPath(Path.Combine(assetsDirectory, assetPath));
    if (!safePath.StartsWith(Path.GetFullPath(assetsDirectory), StringComparison.OrdinalIgnoreCase) || !File.Exists(safePath))
    {
        return Results.NotFound();
    }

    return Results.File(safePath);
});

app.MapGet("/api/export", async (CancellationToken cancellationToken) =>
{
    var project = await GameProject.LoadAsync(projectFile, cancellationToken);
    var issues = ProjectValidator.Validate(project).Where(issue => issue.Severity == "error").ToArray();
    if (issues.Length > 0)
    {
        return Results.BadRequest(new { message = "Fix validation errors before exporting.", issues });
    }

    return Results.Json(new { message = "Use the CLI to export a static package.", command = $"dotnet run --project src/NETGal -- build \"{projectDirectoryForServer}\" dist/{project.Id}" }, JsonOptions.Default);
});

app.MapFallbackToFile("index.html");
Console.WriteLine($"NETGal editor ready: http://localhost:5178");
Console.WriteLine($"Project: {projectDirectoryForServer}");
await app.RunAsync();

static async Task PublishNativePlayerAsync(string projectDirectory, string platform, string configuration, string outputDirectory)
{
    var projectFile = Path.Combine(projectDirectory, "game.json");
    if (!File.Exists(projectFile))
    {
        throw new FileNotFoundException("The project does not contain game.json.", projectFile);
    }

    var project = await GameProject.LoadAsync(projectFile);
    var issues = ProjectValidator.Validate(project).Where(issue => issue.Severity == "error").ToArray();
    if (issues.Length > 0)
    {
        throw new InvalidDataException("Cannot publish an invalid project:\n" + string.Join("\n", issues.Select(issue => $"- {issue.Message}")));
    }

    var targetFramework = platform switch
    {
        "android" => "net10.0-android",
        "windows" or "win" => "net10.0-windows10.0.19041.0",
        "ios" => "net10.0-ios",
        "maccatalyst" or "mac" or "macos" => "net10.0-maccatalyst",
        _ => throw new ArgumentException("Target must be android, windows, ios, or maccatalyst.")
    };

    Directory.CreateDirectory(outputDirectory);
    var workspaceRoot = FindWorkspaceRoot();
    var dotnetHome = Path.Combine(workspaceRoot, "work", "dotnet-home");
    var packageRoot = Path.Combine(workspaceRoot, "work", "nuget-packages");
    var httpCache = Path.Combine(workspaceRoot, "work", "nuget-http-cache");
    var appData = Path.Combine(dotnetHome, "AppData", "Roaming");
    Directory.CreateDirectory(appData);

    var mauiProject = Path.Combine(workspaceRoot, "src", "NETGal.Player.Maui", "NETGal.Player.Maui.csproj");
    var startInfo = new ProcessStartInfo
    {
        FileName = "dotnet",
        WorkingDirectory = workspaceRoot,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    startInfo.ArgumentList.Add("publish");
    startInfo.ArgumentList.Add(mauiProject);
    startInfo.ArgumentList.Add("-f");
    startInfo.ArgumentList.Add(targetFramework);
    startInfo.ArgumentList.Add("-p:TargetFrameworks=" + targetFramework);
    startInfo.ArgumentList.Add("-p:TargetFramework=" + targetFramework);
    startInfo.ArgumentList.Add("-p:NETGalSingleTarget=true");
    startInfo.ArgumentList.Add("-p:NETGalTargetFramework=" + targetFramework);
    startInfo.ArgumentList.Add("-c");
    startInfo.ArgumentList.Add(configuration);
    startInfo.ArgumentList.Add("-p:GameContentRoot=" + projectDirectory);
    startInfo.ArgumentList.Add("-p:GameContentId=" + GameProject.Slugify(project.Id));
    startInfo.ArgumentList.Add("-p:GameContentTitle=" + project.Title);
    startInfo.ArgumentList.Add("-p:GameContentVersion=" + project.Version);
    startInfo.ArgumentList.Add("-p:GameContentBuild=1");
    startInfo.ArgumentList.Add("-o");
    startInfo.ArgumentList.Add(outputDirectory);
    startInfo.Environment["DOTNET_CLI_HOME"] = dotnetHome;
    startInfo.Environment["NUGET_PACKAGES"] = packageRoot;
    startInfo.Environment["NUGET_HTTP_CACHE_PATH"] = httpCache;
    startInfo.Environment["APPDATA"] = appData;

    Console.WriteLine($"Publishing native {platform} player for {project.Title}...");
    Console.WriteLine($"Target framework: {targetFramework}");
    Console.WriteLine($"Output: {outputDirectory}");
    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start dotnet publish.");
    var stdoutTask = ForwardOutputAsync(process.StandardOutput, Console.Out);
    var stderrTask = ForwardOutputAsync(process.StandardError, Console.Error);
    await process.WaitForExitAsync();
    await Task.WhenAll(stdoutTask, stderrTask);
    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"Native publish failed with exit code {process.ExitCode}.");
    }

    Console.WriteLine($"Native {platform} player published to {outputDirectory}");
}

static async Task ForwardOutputAsync(StreamReader reader, TextWriter writer)
{
    while (await reader.ReadLineAsync() is { } line)
    {
        await writer.WriteLineAsync(line);
    }
}

static string FindWorkspaceRoot()
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "NETGal.sln")))
    {
        directory = directory.Parent;
    }

    return directory?.FullName ?? Directory.GetCurrentDirectory();
}
