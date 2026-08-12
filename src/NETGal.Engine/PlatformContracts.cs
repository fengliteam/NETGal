namespace NETGal.Engine;

// The story engine only depends on these contracts. UI, audio, file access, and
// platform lifecycle stay in the host app, which keeps the core AOT-friendly.
public interface IGameStorage
{
    Task<GameProject> LoadProjectAsync(CancellationToken cancellationToken = default);
    Task SaveProjectAsync(GameProject project, CancellationToken cancellationToken = default);
}

public interface IGameHost
{
    string PlatformName { get; }
    Task RunAsync(GameProject project, CancellationToken cancellationToken = default);
}

public sealed record PlatformCapabilities(bool Touch, bool Keyboard, bool NativePublish, bool WebExport);

