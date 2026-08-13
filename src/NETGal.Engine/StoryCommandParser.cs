using System.Text.Json;

namespace NETGal.Engine;

public static class StoryCommandParser
{
    private static readonly HashSet<string> SupportedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "bg", "char", "text", "set", "if", "goto", "jump", "choice"
    };

    public static IReadOnlyList<StoryCommand> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException("AI 输出必须是 JSON 指令数组。");

        var commands = new List<StoryCommand>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object) throw new InvalidDataException("每条指令必须是 JSON 对象。");
            if (!element.TryGetProperty("cmd", out var commandElement) || commandElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException("指令缺少 cmd 字段。");
            }

            var command = commandElement.GetString()?.Trim() ?? "";
            if (!SupportedCommands.Contains(command)) throw new InvalidDataException($"不支持的指令：{command}。");
            var args = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            if (element.TryGetProperty("args", out var argsElement))
            {
                if (argsElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException($"指令 {command} 的 args 必须是对象。");
                foreach (var property in argsElement.EnumerateObject()) args[property.Name] = property.Value.Clone();
            }

            commands.Add(new StoryCommand { Cmd = command, Args = args });
        }

        return commands;
    }
}
