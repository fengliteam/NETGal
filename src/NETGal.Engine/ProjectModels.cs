using System.Text.Json;
using System.Text.Json.Serialization;

namespace NETGal.Engine;

public sealed class GameProject
{
    public string Id { get; set; } = "my-game";
    public string Title { get; set; } = "我的 GalGame";
    public string Author { get; set; } = "NETGal 创作者";
    public string Version { get; set; } = "0.1.0";
    public string StartScene { get; set; } = "intro";
    public GameSettings Settings { get; set; } = new();
    public Dictionary<string, JsonElement> Variables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<StoryScene> Scenes { get; set; } = [];

    [JsonIgnore]
    public StoryScene? Start => Scenes.FirstOrDefault(scene => scene.Id == StartScene) ?? Scenes.FirstOrDefault();

    public static GameProject CreateSample(string title = "我的第一个 GalGame")
    {
        return new GameProject
        {
            Id = Slugify(title),
            Title = title,
            Scenes =
            [
                new StoryScene
                {
                    Id = "intro",
                    Title = "序章",
                    Background = "",
                    Speaker = "旁白",
                    Text = "一个新的故事，正在等待被写下。",
                    Choices =
                    [
                        new ChoiceOption { Id = "begin", Text = "开始这个故事", Next = "morning" },
                        new ChoiceOption { Id = "close", Text = "再安静地待一会儿", Next = "quiet" }
                    ]
                },
                new StoryScene
                {
                    Id = "morning",
                    Title = "新的清晨",
                    Speaker = "米卡",
                    Text = "第一缕晨光落在窗边。今天会变成怎样的一天呢？",
                    Choices =
                    [
                        new ChoiceOption { Id = "walk", Text = "走到外面去", Next = "ending" },
                        new ChoiceOption { Id = "write", Text = "写下接下来的场景", Next = "ending" }
                    ]
                },
                new StoryScene
                {
                    Id = "quiet",
                    Title = "安静的片刻",
                    Speaker = "旁白",
                    Text = "有时候，故事也需要先停下来，轻轻呼吸一会儿。",
                    Choices = [new ChoiceOption { Id = "return", Text = "回到故事开头", Next = "intro" }]
                },
                new StoryScene
                {
                    Id = "ending",
                    Title = "未完待续",
                    Speaker = "旁白",
                    Text = "这是示例章节的结尾。你可以在编辑器里添加更多场景。",
                    Choices = [new ChoiceOption { Id = "restart", Text = "再玩一次", Next = "intro" }]
                }
            ]
        };
    }

    public static async Task<GameProject> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var project = await JsonSerializer.DeserializeAsync(stream, GameJsonContext.Default.GameProject, cancellationToken);
        return project ?? throw new InvalidDataException($"Could not read a game project from '{path}'.");
    }

    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, this, GameJsonContext.Default.GameProject, cancellationToken);
    }

    public static string Slugify(string value)
    {
        var chars = value.ToLowerInvariant().Where(character => char.IsLetterOrDigit(character) || character == ' ').ToArray();
        var slug = new string(chars).Trim().Replace(' ', '-');
        return string.IsNullOrWhiteSpace(slug) ? "my-game" : slug;
    }
}

public sealed class GameSettings
{
    public string Theme { get; set; } = "midnight";
    public string WindowMode { get; set; } = "responsive";
    public bool ShowSaveMenu { get; set; } = true;
}

public sealed class StoryScene
{
    public string Id { get; set; } = "scene";
    public string Title { get; set; } = "未命名场景";
    public string Background { get; set; } = "";
    public string Speaker { get; set; } = "旁白";
    public string Text { get; set; } = "请在这里写下场景内容。";
    public string? Character { get; set; }
    public List<StoryCommand> Commands { get; set; } = [];
    public List<ChoiceOption> Choices { get; set; } = [];

    [JsonIgnore]
    public string DisplayName => $"{Id} - {Title}";
}

public sealed class StoryCommand
{
    public string Cmd { get; set; } = "text";
    public Dictionary<string, JsonElement> Args { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ChoiceOption
{
    public string Id { get; set; } = "choice";
    public string Text { get; set; } = "继续";
    public string Next { get; set; } = "";
    public string? Condition { get; set; }
}

public sealed class StorySaveData
{
    public int FormatVersion { get; set; } = 1;
    public string GameId { get; set; } = "";
    public string CurrentSceneId { get; set; } = "";
    public Dictionary<string, JsonElement> Variables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> ReadScenes { get; set; } = [];
    public DateTimeOffset SavedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(GameProject))]
[JsonSerializable(typeof(GameSettings))]
[JsonSerializable(typeof(StoryScene))]
[JsonSerializable(typeof(ChoiceOption))]
[JsonSerializable(typeof(List<StoryScene>))]
[JsonSerializable(typeof(StoryCommand))]
[JsonSerializable(typeof(List<StoryCommand>))]
[JsonSerializable(typeof(List<ChoiceOption>))]
[JsonSerializable(typeof(StorySaveData))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
public partial class GameJsonContext : JsonSerializerContext
{
}
