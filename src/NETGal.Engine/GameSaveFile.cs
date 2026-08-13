using System.Security.Cryptography;
using System.Text;

namespace NETGal.Engine;

public static class GameSaveFile
{
    private static readonly byte[] Magic = "NGSV"u8.ToArray();
    private const byte Version = 1;
    private const int TagSize = 32;
    private const int HeaderSize = 4 + 1 + TagSize;

    public static byte[] DeriveKey(string gameId)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes($"NETGal save key\0{gameId}"));
    }

    public static async Task SaveAsync(
        string path,
        StorySaveData save,
        ReadOnlyMemory<byte> key,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key.Span);
        var payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(save, GameJsonContext.Default.StorySaveData);
        var tag = ComputeTag(key.Span, payload);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        await using var output = File.Create(path);
        await output.WriteAsync(Magic, cancellationToken);
        await output.WriteAsync(new[] { Version }, cancellationToken);
        await output.WriteAsync(tag, cancellationToken);
        await output.WriteAsync(payload, cancellationToken);
    }

    public static async Task<StorySaveData> LoadAsync(
        string path,
        ReadOnlyMemory<byte> key,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key.Span);
        var data = await File.ReadAllBytesAsync(path, cancellationToken);
        if (data.Length < HeaderSize) throw new InvalidDataException("存档文件不完整。");
        if (!data.AsSpan(0, Magic.Length).SequenceEqual(Magic)) throw new InvalidDataException("不是有效的 NETGal 存档。");
        if (data[Magic.Length] != Version) throw new InvalidDataException("存档版本不受支持。");

        var tag = data.AsSpan(Magic.Length + 1, TagSize);
        var payload = data.AsSpan(HeaderSize);
        var expectedTag = ComputeTag(key.Span, payload);
        if (!CryptographicOperations.FixedTimeEquals(tag, expectedTag))
        {
            throw new InvalidDataException("存档校验失败，文件可能已被修改或损坏。");
        }

        return System.Text.Json.JsonSerializer.Deserialize(payload, GameJsonContext.Default.StorySaveData)
            ?? throw new InvalidDataException("存档内容为空。");
    }

    private static byte[] ComputeTag(ReadOnlySpan<byte> key, ReadOnlySpan<byte> payload)
    {
        using var hmac = new HMACSHA256(key.ToArray());
        return hmac.ComputeHash(payload.ToArray());
    }

    private static void ValidateKey(ReadOnlySpan<byte> key)
    {
        if (key.Length is not (16 or 24 or 32)) throw new ArgumentException("存档密钥必须是 16、24 或 32 字节。", nameof(key));
    }
}
