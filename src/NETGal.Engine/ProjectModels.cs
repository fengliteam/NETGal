using System.Text.Json;
using System.Text.Json.Serialization;

namespace NETGal.Engine;

public sealed class GameProject
{
    public string Id { get; set; } = "my-game";
    public string Title { get; set; } = "My GalGame";
    public string Author { get; set; } = "NETGal Creator";
    public string Version { get; set; } = "0.1.0";
    public string StartScene { get; set; } = "intro";
    public GameSettings Settings { get; set; } = new();
    public List<StoryScene> Scenes { get; set; } = [];

    [JsonIgnore]
    public StoryScene? Start => Scenes.FirstOrDefault(scene => scene.Id == StartScene) ?? Scenes.FirstOrDefault();

    public static GameProject CreateSample(string title = "My First GalGame")
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
                    Title = "Opening",
                    Background = "",
                    Speaker = "Narrator",
                    Text = "A new story is waiting to be written.",
                    Choices =
                    [
                        new ChoiceOption { Id = "begin", Text = "Begin the story", Next = "morning" },
                        new ChoiceOption { Id = "close", Text = "Stay a little longer", Next = "quiet" }
                    ]
                },
                new StoryScene
                {
                    Id = "morning",
                    Title = "A New Morning",
                    Speaker = "Mika",
                    Text = "The first light reaches the window. What kind of day will this become?",
                    Choices =
                    [
                        new ChoiceOption { Id = "walk", Text = "Go outside", Next = "ending" },
                        new ChoiceOption { Id = "write", Text = "Write the next scene", Next = "ending" }
                    ]
                },
                new StoryScene
                {
                    Id = "quiet",
                    Title = "A Quiet Moment",
                    Speaker = "Narrator",
                    Text = "Sometimes a story needs a breath before it moves forward.",
                    Choices = [new ChoiceOption { Id = "return", Text = "Return to the beginning", Next = "intro" }]
                },
                new StoryScene
                {
                    Id = "ending",
                    Title = "To Be Continued",
                    Speaker = "Narrator",
                    Text = "This is the end of the sample chapter. Add more scenes in the editor.",
                    Choices = [new ChoiceOption { Id = "restart", Text = "Play again", Next = "intro" }]
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
    public string Title { get; set; } = "Untitled Scene";
    public string Background { get; set; } = "";
    public string Speaker { get; set; } = "Narrator";
    public string Text { get; set; } = "Write your scene here.";
    public string? Character { get; set; }
    public List<ChoiceOption> Choices { get; set; } = [];
}

public sealed class ChoiceOption
{
    public string Id { get; set; } = "choice";
    public string Text { get; set; } = "Continue";
    public string Next { get; set; } = "";
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
[JsonSerializable(typeof(List<ChoiceOption>))]
public partial class GameJsonContext : JsonSerializerContext
{
}
