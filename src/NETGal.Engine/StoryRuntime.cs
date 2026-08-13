using System.Globalization;
using System.Text.Json;

namespace NETGal.Engine;

public sealed class StoryRuntime
{
    private const int MaxCommandSteps = 256;
    private readonly GameProject _project;
    private readonly Dictionary<string, JsonElement> _variables = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _readScenes = new(StringComparer.OrdinalIgnoreCase);
    private string? _preparedSceneId;
    private CommandPresentation? _preparedPresentation;
    private bool _skipSideEffectsForCurrentScene;

    public StoryRuntime(GameProject project)
    {
        _project = project;
        ResetVariables();
        CurrentSceneId = project.Start?.Id ?? project.StartScene;
    }

    public string CurrentSceneId { get; private set; }
    public IReadOnlyDictionary<string, JsonElement> Variables => _variables;
    public IReadOnlyCollection<string> ReadScenes => _readScenes;

    public RuntimeSnapshot Snapshot()
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var step = 0; step < MaxCommandSteps; step++)
        {
            var scene = FindScene(CurrentSceneId);
            if (scene is null) return MissingSnapshot();
            _readScenes.Add(scene.Id);

            if (!visited.Add(scene.Id))
            {
                return CreateSnapshot(scene, scene.Background, scene.Speaker, scene.Text, scene.Character, scene.Choices);
            }

            var presentation = GetPresentation(scene);

            if (!string.IsNullOrWhiteSpace(presentation.NextSceneId) && FindScene(presentation.NextSceneId) is not null)
            {
                CurrentSceneId = presentation.NextSceneId;
                continue;
            }

            return CreateSnapshot(scene, presentation.Background, presentation.Speaker, presentation.Text, presentation.Character, presentation.Choices);
        }

        throw new InvalidDataException("指令流跳转超过 256 步，可能存在循环跳转。");
    }

    public RuntimeSnapshot Choose(string choiceId)
    {
        var choice = Snapshot().Choices.FirstOrDefault(option => option.Id == choiceId);
        if (choice is not null && !string.IsNullOrWhiteSpace(choice.Next) && FindScene(choice.Next) is not null)
        {
            CurrentSceneId = choice.Next;
            ClearPresentationCache();
        }

        return Snapshot();
    }

    public RuntimeSnapshot Restart()
    {
        ResetVariables();
        _readScenes.Clear();
        _skipSideEffectsForCurrentScene = false;
        ClearPresentationCache();
        CurrentSceneId = _project.Start?.Id ?? _project.StartScene;
        return Snapshot();
    }

    public StorySaveData CaptureSave()
    {
        return new StorySaveData
        {
            GameId = _project.Id,
            CurrentSceneId = CurrentSceneId,
            Variables = _variables.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.OrdinalIgnoreCase),
            ReadScenes = _readScenes.ToList(),
            SavedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public void RestoreSave(StorySaveData save)
    {
        if (!string.Equals(save.GameId, _project.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("存档不属于当前游戏。");
        }

        var targetScene = FindScene(save.CurrentSceneId);
        CurrentSceneId = targetScene?.Id ?? _project.Start?.Id ?? _project.StartScene;
        _variables.Clear();
        foreach (var pair in save.Variables) _variables[pair.Key] = pair.Value.Clone();
        _readScenes.Clear();
        foreach (var sceneId in save.ReadScenes) _readScenes.Add(sceneId);
        _skipSideEffectsForCurrentScene = true;
        ClearPresentationCache();
    }

    public static StoryRuntime FromSave(GameProject project, StorySaveData save)
    {
        var runtime = new StoryRuntime(project);
        runtime.RestoreSave(save);
        return runtime;
    }

    private CommandPresentation ExecuteCommands(StoryScene scene, bool applySideEffects = true)
    {
        var background = scene.Background;
        var speaker = scene.Speaker;
        var text = scene.Text;
        var character = scene.Character;
        var choices = new List<ChoiceOption>();

        foreach (var command in scene.Commands)
        {
            var cmd = command.Cmd.Trim().ToLowerInvariant();
            var args = command.Args;
            switch (cmd)
            {
                case "bg":
                    background = GetString(args, "file", "path", "background") ?? background;
                    break;
                case "char":
                    character = GetString(args, "id", "character", "expression") ?? character;
                    break;
                case "text":
                    speaker = GetString(args, "speaker") ?? speaker;
                    text = GetString(args, "content", "text") ?? text;
                    break;
                case "set":
                    if (applySideEffects) ApplySet(args);
                    break;
                case "if":
                    var target = EvaluateCondition(args)
                        ? GetString(args, "then", "next")
                        : GetString(args, "else", "elseNext");
                    if (!string.IsNullOrWhiteSpace(target)) return new CommandPresentation(background, speaker, text, character, choices, target);
                    break;
                case "goto":
                case "jump":
                    return new CommandPresentation(background, speaker, text, character, choices, GetString(args, "next", "scene"));
                case "choice":
                    choices.AddRange(ReadChoices(args));
                    return new CommandPresentation(background, speaker, text, character, choices, null);
            }
        }

        return new CommandPresentation(background, speaker, text, character, choices, null);
    }

    private CommandPresentation GetPresentation(StoryScene scene)
    {
        if (scene.Commands.Count == 0)
        {
            _skipSideEffectsForCurrentScene = false;
            return new CommandPresentation(scene.Background, scene.Speaker, scene.Text, scene.Character, VisibleChoices(scene.Choices), null);
        }

        if (string.Equals(_preparedSceneId, scene.Id, StringComparison.OrdinalIgnoreCase) && _preparedPresentation is not null)
        {
            return _preparedPresentation;
        }

        _preparedSceneId = scene.Id;
        _preparedPresentation = ExecuteCommands(scene, !_skipSideEffectsForCurrentScene);
        _skipSideEffectsForCurrentScene = false;
        return _preparedPresentation;
    }

    private void ClearPresentationCache()
    {
        _preparedSceneId = null;
        _preparedPresentation = null;
    }

    private void ApplySet(IReadOnlyDictionary<string, JsonElement> args)
    {
        var name = GetString(args, "name", "variable");
        if (string.IsNullOrWhiteSpace(name)) return;
        if (TryGetArg(args, "value", out var value))
        {
            _variables[name] = value.Clone();
        }
    }

    private bool EvaluateCondition(IReadOnlyDictionary<string, JsonElement> args)
    {
        var expression = GetString(args, "condition");
        if (!string.IsNullOrWhiteSpace(expression)) return EvaluateExpression(expression);

        var variable = GetString(args, "variable", "name");
        if (string.IsNullOrWhiteSpace(variable) || !_variables.TryGetValue(variable, out var actual)) return false;
        var operation = GetString(args, "operator", "op") ?? "truthy";
        if (operation.Equals("truthy", StringComparison.OrdinalIgnoreCase)) return IsTruthy(actual);
        if (!TryGetArg(args, "value", out var expected)) return false;
        return Compare(actual, expected, operation);
    }

    private bool EvaluateExpression(string expression)
    {
        var trimmed = expression.Trim();
        if (trimmed.StartsWith('!')) return !EvaluateExpression(trimmed[1..]);
        foreach (var operation in new[] { ">=", "<=", "!=", "==", ">", "<" })
        {
            var index = trimmed.IndexOf(operation, StringComparison.Ordinal);
            if (index < 0) continue;
            var variable = trimmed[..index].Trim();
            var expectedText = trimmed[(index + operation.Length)..].Trim();
            if (!_variables.TryGetValue(variable, out var actual)) return false;
            return Compare(actual, ParseLiteral(expectedText), operation);
        }

        return _variables.TryGetValue(trimmed, out var value) && IsTruthy(value);
    }

    private List<ChoiceOption> ReadChoices(IReadOnlyDictionary<string, JsonElement> args)
    {
        var result = new List<ChoiceOption>();
        if (!TryGetArg(args, "options", out var options) || options.ValueKind != JsonValueKind.Array) return result;
        foreach (var item in options.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var condition = GetString(item, "condition");
            if (!string.IsNullOrWhiteSpace(condition) && !EvaluateExpression(condition)) continue;
            result.Add(new ChoiceOption
            {
                Id = GetString(item, "id") ?? $"choice-{result.Count + 1}",
                Text = GetString(item, "text", "label") ?? "继续",
                Next = GetString(item, "next", "scene") ?? "",
                Condition = condition
            });
        }

        return result;
    }

    private RuntimeSnapshot CreateSnapshot(StoryScene scene, string background, string speaker, string text, string? character, IEnumerable<ChoiceOption> choices)
    {
        return new RuntimeSnapshot(scene.Id, scene.Title, background, speaker, text, character, choices.ToArray())
        {
            Variables = _variables,
            ReadScenes = _readScenes
        };
    }

    private RuntimeSnapshot MissingSnapshot() => new(CurrentSceneId, "场景不存在", "", "NETGal", "当前场景不存在。", null, [])
    {
        Variables = _variables,
        ReadScenes = _readScenes
    };

    private void ResetVariables()
    {
        _variables.Clear();
        foreach (var pair in _project.Variables) _variables[pair.Key] = pair.Value.Clone();
    }

    private StoryScene? FindScene(string id) => _project.Scenes.FirstOrDefault(scene => scene.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    private IReadOnlyList<ChoiceOption> VisibleChoices(IEnumerable<ChoiceOption> choices)
    {
        return choices.Where(choice => string.IsNullOrWhiteSpace(choice.Condition) || EvaluateExpression(choice.Condition)).ToArray();
    }

    private static bool TryGetArg(IReadOnlyDictionary<string, JsonElement> args, string name, out JsonElement value)
    {
        foreach (var pair in args)
        {
            if (pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? GetString(IReadOnlyDictionary<string, JsonElement> args, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetArg(args, name, out var value)) continue;
            return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        }

        return null;
    }

    private static string? GetString(JsonElement objectValue, params string[] names)
    {
        if (objectValue.ValueKind != JsonValueKind.Object) return null;
        foreach (var property in objectValue.EnumerateObject())
        {
            if (!names.Any(name => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;
            return property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : property.Value.ToString();
        }

        return null;
    }

    private static bool IsTruthy(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False or JsonValueKind.Null or JsonValueKind.Undefined => false,
        JsonValueKind.Number => value.TryGetDecimal(out var number) && number != 0,
        JsonValueKind.String => !string.IsNullOrWhiteSpace(value.GetString()),
        _ => true
    };

    private static bool Compare(JsonElement actual, JsonElement expected, string operation)
    {
        if (actual.ValueKind == JsonValueKind.Number && expected.ValueKind == JsonValueKind.Number &&
            actual.TryGetDecimal(out var actualNumber) && expected.TryGetDecimal(out var expectedNumber))
        {
            return operation switch
            {
                "==" => actualNumber == expectedNumber,
                "!=" => actualNumber != expectedNumber,
                ">" => actualNumber > expectedNumber,
                ">=" => actualNumber >= expectedNumber,
                "<" => actualNumber < expectedNumber,
                "<=" => actualNumber <= expectedNumber,
                _ => false
            };
        }

        var actualText = actual.ToString();
        var expectedText = expected.ToString();
        var comparison = string.Compare(actualText, expectedText, StringComparison.OrdinalIgnoreCase);
        return operation switch
        {
            "==" => comparison == 0,
            "!=" => comparison != 0,
            ">" => comparison > 0,
            ">=" => comparison >= 0,
            "<" => comparison < 0,
            "<=" => comparison <= 0,
            _ => false
        };
    }

    private static JsonElement ParseLiteral(string text)
    {
        var trimmed = text.Trim();
        if ((trimmed.StartsWith('"') && trimmed.EndsWith('"')) || (trimmed.StartsWith('\'') && trimmed.EndsWith('\'')))
        {
            return JsonSerializer.SerializeToElement(trimmed[1..^1]);
        }

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(trimmed);
        }
    }

    private sealed record CommandPresentation(string Background, string Speaker, string Text, string? Character, IReadOnlyList<ChoiceOption> Choices, string? NextSceneId);
}

public sealed record RuntimeSnapshot(
    string SceneId,
    string SceneTitle,
    string Background,
    string Speaker,
    string Text,
    string? Character,
    IReadOnlyList<ChoiceOption> Choices)
{
    public IReadOnlyDictionary<string, JsonElement> Variables { get; init; } = new Dictionary<string, JsonElement>();
    public IReadOnlyCollection<string> ReadScenes { get; init; } = Array.Empty<string>();
}
