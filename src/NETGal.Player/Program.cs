using NETGal.Engine;

var projectDirectory = Path.GetFullPath(args.FirstOrDefault() ?? ".");
var projectPath = Path.Combine(projectDirectory, "game.json");
if (!File.Exists(projectPath))
{
    Console.Error.WriteLine($"No game.json found in {projectDirectory}");
    return 2;
}

var project = await GameProject.LoadAsync(projectPath);
var runtime = new StoryRuntime(project);
var interactiveTerminal = !Console.IsInputRedirected && !Console.IsOutputRedirected;
if (interactiveTerminal)
{
    Console.CursorVisible = false;
}
try
{
    while (true)
    {
        if (interactiveTerminal)
        {
            Console.Clear();
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("--- NETGal scene ---");
        }
        var snapshot = runtime.Snapshot();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"{project.Title}  |  native .NET player  |  {snapshot.SceneTitle}");
        var separatorWidth = interactiveTerminal ? Math.Min(Console.WindowWidth, 72) : 60;
        Console.WriteLine(new string('-', separatorWidth));
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine(snapshot.Speaker);
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine();
        Console.WriteLine(snapshot.Text);
        Console.WriteLine();

        for (var index = 0; index < snapshot.Choices.Count; index++)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write($"[{index + 1}] ");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(snapshot.Choices[index].Text);
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("Choose a number, R to restart, or Q to quit.");
        var key = interactiveTerminal
            ? Console.ReadKey(intercept: true).Key
            : ParseKey(Console.ReadLine());
        if (key == ConsoleKey.Q) break;
        if (key == ConsoleKey.R) { runtime.Restart(); continue; }
        var choiceIndex = key - ConsoleKey.D1;
        if (choiceIndex >= 0 && choiceIndex < snapshot.Choices.Count)
        {
            runtime.Choose(snapshot.Choices[choiceIndex].Id);
        }
    }
}
finally
{
    Console.ResetColor();
    if (interactiveTerminal)
    {
        Console.CursorVisible = true;
    }
}

return 0;

static ConsoleKey ParseKey(string? input)
{
    if (string.Equals(input?.Trim(), "q", StringComparison.OrdinalIgnoreCase)) return ConsoleKey.Q;
    if (string.Equals(input?.Trim(), "r", StringComparison.OrdinalIgnoreCase)) return ConsoleKey.R;
    return input?.Trim() switch
    {
        "1" => ConsoleKey.D1,
        "2" => ConsoleKey.D2,
        "3" => ConsoleKey.D3,
        "4" => ConsoleKey.D4,
        "5" => ConsoleKey.D5,
        "6" => ConsoleKey.D6,
        "7" => ConsoleKey.D7,
        "8" => ConsoleKey.D8,
        "9" => ConsoleKey.D9,
        _ => ConsoleKey.NoName
    };
}
