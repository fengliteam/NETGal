using System.Text.Json;
using NETGal.Engine;

var root = Path.Combine(Directory.GetCurrentDirectory(), "work", "engine-smoke");
Directory.CreateDirectory(root);

var project = new GameProject
{
    Id = "smoke-game",
    Title = "引擎冒烟测试",
    Variables = new Dictionary<string, JsonElement>
    {
        ["affection"] = JsonSerializer.SerializeToElement(1)
    },
    StartScene = "intro",
    Scenes =
    [
        new StoryScene
        {
            Id = "intro",
            Title = "开场",
            Commands =
            [
                new StoryCommand { Cmd = "text", Args = new() { ["speaker"] = JsonSerializer.SerializeToElement("旁白"), ["content"] = JsonSerializer.SerializeToElement("条件指令正常。") } },
                new StoryCommand { Cmd = "choice", Args = new() { ["options"] = JsonSerializer.SerializeToElement(new[] { new { id = "next", text = "继续", next = "end", condition = "affection >= 1" } }) } }
            ]
        },
        new StoryScene { Id = "end", Title = "结尾", Text = "完成。" }
    ]
};

var runtime = new StoryRuntime(project);
Require(runtime.Snapshot().Choices.Count == 1, "条件选项未显示");
runtime.Choose("next");
Require(runtime.CurrentSceneId == "end", "选项跳转失败");

var key = GameSaveFile.DeriveKey(project.Id);
var savePath = Path.Combine(root, "slot.ngsave");
await GameSaveFile.SaveAsync(savePath, runtime.CaptureSave(), key);
var restored = await GameSaveFile.LoadAsync(savePath, key);
Require(restored.CurrentSceneId == "end", "存档读取失败");
Require(StoryCommandParser.Parse("[{\"cmd\":\"text\",\"args\":{\"content\":\"ok\"}}]").Count == 1, "指令解析失败");

var tamperedSave = await File.ReadAllBytesAsync(savePath);
tamperedSave[^1] ^= 0x01;
await File.WriteAllBytesAsync(Path.Combine(root, "tampered.ngsave"), tamperedSave);
try
{
    await GameSaveFile.LoadAsync(Path.Combine(root, "tampered.ngsave"), key);
    throw new InvalidOperationException("被篡改的存档没有被拒绝");
}
catch (InvalidDataException)
{
}

var packagePath = Path.Combine(root, "game.pkg");
await ProtectedGamePackage.CreateAsync(Directory.GetCurrentDirectory(), project, key, packagePath);
await using (var packageStream = File.OpenRead(packagePath))
{
    var package = await ProtectedGamePackage.LoadAsync(packageStream, key);
    Require(package.Project.Title == project.Title, "保护包读取失败");
}

var packageBytes = await File.ReadAllBytesAsync(packagePath);
packageBytes[^1] ^= 0x01;
var tamperedPackagePath = Path.Combine(root, "tampered.pkg");
await File.WriteAllBytesAsync(tamperedPackagePath, packageBytes);
try
{
    await using var packageStream = File.OpenRead(tamperedPackagePath);
    await ProtectedGamePackage.LoadAsync(packageStream, key);
    throw new InvalidOperationException("被篡改的游戏包没有被拒绝");
}
catch (InvalidDataException)
{
}

Console.WriteLine("NETGal.Engine smoke tests passed.");

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
