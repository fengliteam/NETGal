using System.IO.Compression;

namespace NETGal.Engine;

public sealed record BuildResult(string OutputDirectory, string ZipPath, int AssetCount);

public sealed class StaticPackageBuilder
{
    private readonly string _templateDirectory;

    public StaticPackageBuilder(string templateDirectory)
    {
        _templateDirectory = templateDirectory;
    }

    public async Task<BuildResult> BuildAsync(string projectDirectory, string outputDirectory, CancellationToken cancellationToken = default)
    {
        var projectPath = Path.Combine(projectDirectory, "game.json");
        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException("The project does not contain game.json.", projectPath);
        }

        var project = await GameProject.LoadAsync(projectPath, cancellationToken);
        var issues = ProjectValidator.Validate(project).Where(issue => issue.Severity == "error").ToArray();
        if (issues.Length > 0)
        {
            throw new InvalidDataException("Cannot build an invalid project:\n" + string.Join("\n", issues.Select(issue => $"- {issue.Message}")));
        }

        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        Directory.CreateDirectory(outputDirectory);
        await CopyTemplateAsync("index.html", outputDirectory, cancellationToken);
        await CopyTemplateAsync("player.js", outputDirectory, cancellationToken);
        await CopyTemplateAsync("player.css", outputDirectory, cancellationToken);
        await project.SaveAsync(Path.Combine(outputDirectory, "game.json"), cancellationToken);

        var sourceAssets = Path.Combine(projectDirectory, "assets");
        var targetAssets = Path.Combine(outputDirectory, "assets");
        var assetCount = 0;
        if (Directory.Exists(sourceAssets))
        {
            CopyDirectory(sourceAssets, targetAssets, ref assetCount);
        }

        var zipPath = outputDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".zip";
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        ZipFile.CreateFromDirectory(outputDirectory, zipPath, CompressionLevel.Fastest, includeBaseDirectory: false);
        return new BuildResult(outputDirectory, zipPath, assetCount);
    }

    private async Task CopyTemplateAsync(string name, string outputDirectory, CancellationToken cancellationToken)
    {
        var source = Path.Combine(_templateDirectory, name);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException("A player template is missing.", source);
        }

        await using var input = File.OpenRead(source);
        await using var output = File.Create(Path.Combine(outputDirectory, name));
        await input.CopyToAsync(output, cancellationToken);
    }

    private static void CopyDirectory(string source, string target, ref int assetCount)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
            assetCount++;
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)), ref assetCount);
        }
    }
}

