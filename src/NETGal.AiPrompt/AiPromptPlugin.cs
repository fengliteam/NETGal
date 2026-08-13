using NETGal.Engine;

namespace NETGal.AiPrompt;

public sealed class AiPromptPlugin : INetGalPlugin
{
    public string Id => "netgal.ai-prompt";
    public string Name => "NETGal AI 辅助";
    public string Version => "1.0.0";

    public void Configure(INetGalPluginHost host)
    {
        host.RegisterCommand(new PluginCommandDefinition("ai_generate", "将已确认的 AI JSON 指令插入当前场景", ["json"]));
        host.RegisterMenuItem(new PluginMenuItem("ai.copy-spec", "复制 AI 项目规范提示词"));
        host.RegisterPanel(new PluginPanelDefinition("ai.assistant", "AI 辅助输入"));
    }

    public static string GetSpecificationPrompt() => "请参阅 docs/AI提示词模板.md。先询问故事需求，确认后只输出 NETGal JSON 指令数组。";

    public static IReadOnlyList<StoryCommand> ParseGeneratedCommands(string json) => StoryCommandParser.Parse(json);
}
