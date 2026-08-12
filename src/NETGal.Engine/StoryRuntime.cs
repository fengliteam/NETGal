namespace NETGal.Engine;

public sealed class StoryRuntime
{
    private readonly GameProject _project;

    public StoryRuntime(GameProject project)
    {
        _project = project;
        CurrentSceneId = project.Start?.Id ?? project.StartScene;
    }

    public string CurrentSceneId { get; private set; }

    public RuntimeSnapshot Snapshot()
    {
        var scene = FindScene(CurrentSceneId);
        return new RuntimeSnapshot(
            scene?.Id ?? "",
            scene?.Title ?? "Missing scene",
            scene?.Background ?? "",
            scene?.Speaker ?? "",
            scene?.Text ?? "This scene does not exist.",
            scene?.Character,
            scene?.Choices ?? []);
    }

    public RuntimeSnapshot Choose(string choiceId)
    {
        var scene = FindScene(CurrentSceneId);
        var choice = scene?.Choices.FirstOrDefault(option => option.Id == choiceId);
        if (choice is not null && !string.IsNullOrWhiteSpace(choice.Next))
        {
            CurrentSceneId = choice.Next;
        }

        return Snapshot();
    }

    public RuntimeSnapshot Restart()
    {
        CurrentSceneId = _project.Start?.Id ?? _project.StartScene;
        return Snapshot();
    }

    private StoryScene? FindScene(string id) => _project.Scenes.FirstOrDefault(scene => scene.Id == id);
}

public sealed record RuntimeSnapshot(
    string SceneId,
    string SceneTitle,
    string Background,
    string Speaker,
    string Text,
    string? Character,
    IReadOnlyList<ChoiceOption> Choices);

