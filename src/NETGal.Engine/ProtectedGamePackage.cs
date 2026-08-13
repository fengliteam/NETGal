using System.IO.Compression;
using System.Security.Cryptography;

namespace NETGal.Engine;

// Native exports contain one authenticated package instead of editable JSON and loose assets.
public sealed class ProtectedGamePackage
{
    private static readonly byte[] Magic = "NGPK"u8.ToArray();
    private const byte Version = 1;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int HeaderSize = 4 + 1 + NonceSize + TagSize;

    private ProtectedGamePackage(GameProject project, Dictionary<string, byte[]> assets)
    {
        Project = project;
        Assets = assets;
    }

    public GameProject Project { get; }
    public IReadOnlyDictionary<string, byte[]> Assets { get; }

    public bool TryGetAsset(string path, out byte[] data)
    {
        return Assets.TryGetValue(NormalizeAssetPath(path), out data!);
    }

    public static byte[] CreateKey() => RandomNumberGenerator.GetBytes(32);

    public static async Task CreateAsync(
        string projectDirectory,
        GameProject project,
        ReadOnlyMemory<byte> key,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key.Span);
        var payload = await CreatePayloadAsync(projectDirectory, project, cancellationToken);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[payload.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(key.Span, TagSize);
        aes.Encrypt(nonce, payload, ciphertext, tag, Magic.AsSpan());

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        await using var output = File.Create(outputPath);
        await output.WriteAsync(Magic, cancellationToken);
        await output.WriteAsync(new[] { Version }, cancellationToken);
        await output.WriteAsync(nonce, cancellationToken);
        await output.WriteAsync(tag, cancellationToken);
        await output.WriteAsync(ciphertext, cancellationToken);
    }

    public static async Task<ProtectedGamePackage> LoadAsync(
        Stream input,
        ReadOnlyMemory<byte> key,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key.Span);
        using var packageStream = new MemoryStream();
        await input.CopyToAsync(packageStream, cancellationToken);
        var package = packageStream.ToArray();
        if (package.Length < HeaderSize) throw new InvalidDataException("游戏包不完整。");
        if (!package.AsSpan(0, Magic.Length).SequenceEqual(Magic)) throw new InvalidDataException("不是有效的 NETGal 游戏包。");
        if (package[Magic.Length] != Version) throw new InvalidDataException("游戏包版本不受支持。");

        var nonce = package.AsSpan(5, NonceSize);
        var tag = package.AsSpan(5 + NonceSize, TagSize);
        var ciphertext = package.AsSpan(HeaderSize);
        var payload = new byte[ciphertext.Length];
        using var aes = new AesGcm(key.Span, TagSize);
        try
        {
            aes.Decrypt(nonce, ciphertext, tag, payload, Magic.AsSpan());
        }
        catch (CryptographicException exception)
        {
            throw new InvalidDataException("游戏包校验失败，文件可能已被修改或损坏。", exception);
        }

        using var archiveStream = new MemoryStream(payload);
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read);
        var projectEntry = archive.GetEntry("game.json") ?? throw new InvalidDataException("游戏包缺少剧情数据。");
        await using var projectStream = projectEntry.Open();
        var project = await System.Text.Json.JsonSerializer.DeserializeAsync(projectStream, GameJsonContext.Default.GameProject, cancellationToken)
            ?? throw new InvalidDataException("游戏包中的剧情数据无效。");

        var assets = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries.Where(entry => entry.FullName.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)))
        {
            await using var assetStream = entry.Open();
            using var assetBuffer = new MemoryStream();
            await assetStream.CopyToAsync(assetBuffer, cancellationToken);
            assets[NormalizeAssetPath(entry.FullName)] = assetBuffer.ToArray();
        }

        return new ProtectedGamePackage(project, assets);
    }

    private static async Task<byte[]> CreatePayloadAsync(string projectDirectory, GameProject project, CancellationToken cancellationToken)
    {
        using var payloadStream = new MemoryStream();
        using (var archive = new ZipArchive(payloadStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var projectEntry = archive.CreateEntry("game.json", CompressionLevel.Fastest);
            await using (var projectStream = projectEntry.Open())
            {
                await System.Text.Json.JsonSerializer.SerializeAsync(projectStream, project, GameJsonContext.Default.GameProject, cancellationToken);
            }

            var assetsDirectory = Path.Combine(projectDirectory, "assets");
            if (Directory.Exists(assetsDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(assetsDirectory, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var relativePath = Path.GetRelativePath(projectDirectory, file).Replace('\\', '/');
                    var entry = archive.CreateEntry(relativePath, CompressionLevel.Fastest);
                    await using var input = File.OpenRead(file);
                    await using var output = entry.Open();
                    await input.CopyToAsync(output, cancellationToken);
                }
            }
        }

        return payloadStream.ToArray();
    }

    private static string NormalizeAssetPath(string path)
    {
        var normalized = path.Replace('\\', '/').TrimStart('/');
        return normalized.StartsWith("assets/", StringComparison.OrdinalIgnoreCase) ? normalized : "assets/" + normalized;
    }

    private static void ValidateKey(ReadOnlySpan<byte> key)
    {
        if (key.Length is not (16 or 24 or 32)) throw new ArgumentException("Game package keys must be 16, 24, or 32 bytes.", nameof(key));
    }
}
