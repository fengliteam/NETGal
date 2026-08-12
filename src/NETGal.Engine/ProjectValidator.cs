namespace NETGal.Engine;

public sealed record ValidationIssue(string Severity, string Path, string Message);

public static class ProjectValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(GameProject project)
    {
        var issues = new List<ValidationIssue>();

        if (string.IsNullOrWhiteSpace(project.Title))
        {
            issues.Add(new("error", "title", "The project needs a title."));
        }

        if (project.Scenes.Count == 0)
        {
            issues.Add(new("error", "scenes", "Add at least one scene."));
            return issues;
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var scene in project.Scenes)
        {
            if (string.IsNullOrWhiteSpace(scene.Id))
            {
                issues.Add(new("error", "scenes", $"Scene '{scene.Title}' has no id."));
            }
            else if (!ids.Add(scene.Id))
            {
                issues.Add(new("error", scene.Id, "Scene ids must be unique."));
            }

            if (string.IsNullOrWhiteSpace(scene.Text))
            {
                issues.Add(new("warning", scene.Id, "This scene has no dialogue text."));
            }

            foreach (var choice in scene.Choices)
            {
                if (string.IsNullOrWhiteSpace(choice.Text))
                {
                    issues.Add(new("warning", scene.Id, "A choice has no visible text."));
                }

                if (!string.IsNullOrWhiteSpace(choice.Next) && !project.Scenes.Any(target => target.Id == choice.Next))
                {
                    issues.Add(new("error", scene.Id, $"Choice '{choice.Text}' points to missing scene '{choice.Next}'."));
                }
            }
        }

        if (!project.Scenes.Any(scene => scene.Id == project.StartScene))
        {
            issues.Add(new("error", "startScene", $"Start scene '{project.StartScene}' does not exist."));
        }

        return issues;
    }
}

