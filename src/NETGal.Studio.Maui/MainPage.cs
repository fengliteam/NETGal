using System.Text.Json;
using NETGal.Engine;

namespace NETGal.Studio.Maui;

public sealed class MainPage : ContentPage
{
    private readonly VerticalStackLayout _sceneList = new() { Spacing = 4 };
    private readonly VerticalStackLayout _editor = new() { Spacing = 14 };
    private readonly Label _projectTitle = new() { Text = "NETGal Studio", TextColor = Colors.White, FontSize = 19, FontAttributes = FontAttributes.Bold };
    private readonly Label _status = new() { Text = "Open a game.json to begin.", TextColor = Color.FromArgb("#8894A7"), FontSize = 12 };
    private readonly Button _saveButton;
    private GameProject? _project;
    private string? _projectPath;
    private string? _selectedSceneId;

    public MainPage()
    {
        BackgroundColor = Color.FromArgb("#0D141D");
        Title = "NETGal Studio";

        var openButton = CreateHeaderButton("Open project", OpenProjectAsync);
        _saveButton = CreateHeaderButton("Save project", SaveProjectAsync);
        _saveButton.IsEnabled = false;
        var addSceneButton = CreateHeaderButton("+ Scene", AddScene);

        var header = new Grid
        {
            Padding = new Thickness(20, 16),
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 9
        };
        header.Children.Add(new VerticalStackLayout { Children = { _projectTitle, _status } });
        Grid.SetColumn(openButton, 1);
        Grid.SetColumn(_saveButton, 2);
        Grid.SetColumn(addSceneButton, 3);
        header.Children.Add(openButton);
        header.Children.Add(_saveButton);
        header.Children.Add(addSceneButton);

        var scenePanel = new Border
        {
            Stroke = Color.FromArgb("#293445"),
            StrokeThickness = 1,
            BackgroundColor = Color.FromArgb("#111923"),
            Padding = new Thickness(10),
            Content = new ScrollView { Content = _sceneList }
        };
        var editorPanel = new Border
        {
            Stroke = Color.FromArgb("#293445"),
            StrokeThickness = 1,
            BackgroundColor = Color.FromArgb("#101923"),
            Padding = new Thickness(22),
            Content = new ScrollView { Content = _editor }
        };
        var workspace = new Grid
        {
            Padding = new Thickness(14, 0, 14, 14),
            ColumnDefinitions = { new ColumnDefinition(new GridLength(230)), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 14,
            Children = { scenePanel, editorPanel }
        };
        Grid.SetColumn(editorPanel, 1);

        var root = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) },
            Children = { header, workspace }
        };
        Grid.SetRow(workspace, 1);
        Content = root;
        RenderEmptyState();
    }

    private Button CreateHeaderButton(string text, Func<Task> action)
    {
        var button = new Button
        {
            Text = text,
            TextColor = Color.FromArgb("#F1D0B2"),
            BackgroundColor = Color.FromArgb("#18212C"),
            BorderColor = Color.FromArgb("#405064"),
            BorderWidth = 1,
            Padding = new Thickness(12, 7),
            FontSize = 12
        };
        button.Clicked += async (_, _) => await action();
        return button;
    }

    private void RenderEmptyState()
    {
        _sceneList.Clear();
        _editor.Clear();
        _editor.Add(new Label { Text = "Native project studio", TextColor = Colors.White, FontSize = 22, FontAttributes = FontAttributes.Bold });
        _editor.Add(new Label { Text = "Open a game.json file to edit scenes, dialogue, choices, and project settings. This editor uses native Windows controls and writes the same JSON consumed by the Android, Windows, iOS, and Mac Catalyst players.", TextColor = Color.FromArgb("#8894A7"), FontSize = 14, LineBreakMode = LineBreakMode.WordWrap });
        _editor.Add(new Label { Text = "The browser editor remains available as a lightweight alternative, but exported games never use a browser UI.", TextColor = Color.FromArgb("#E19B67"), FontSize = 12, LineBreakMode = LineBreakMode.WordWrap });
    }

    private async Task OpenProjectAsync()
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Open NETGal game.json" });
        if (file is null) return;

        await using var stream = await file.OpenReadAsync();
        _project = await JsonSerializer.DeserializeAsync(stream, GameJsonContext.Default.GameProject);
        if (_project is null)
        {
            await DisplayAlert("Open failed", "The selected file is not a valid NETGal project.", "OK");
            return;
        }

        _projectPath = file.FullPath;
        _selectedSceneId = _project.Start?.Id ?? _project.StartScene;
        _projectTitle.Text = _project.Title;
        _status.Text = _projectPath;
        _saveButton.IsEnabled = true;
        RenderProject();
    }

    private async Task SaveProjectAsync()
    {
        if (_project is null || string.IsNullOrWhiteSpace(_projectPath)) return;
        var issues = ProjectValidator.Validate(_project);
        if (issues.Any(issue => issue.Severity == "error"))
        {
            await DisplayAlert("Cannot save", string.Join(Environment.NewLine, issues.Where(issue => issue.Severity == "error").Select(issue => issue.Message)), "OK");
            return;
        }

        await _project.SaveAsync(_projectPath);
        _status.Text = $"Saved {DateTime.Now:HH:mm:ss} · {_projectPath}";
    }

    private Task AddScene()
    {
        if (_project is null) return Task.CompletedTask;
        var index = _project.Scenes.Count + 1;
        var scene = new StoryScene { Id = $"scene-{index}", Title = "New Scene", Speaker = "Narrator", Text = "Write your scene here." };
        _project.Scenes.Add(scene);
        _selectedSceneId = scene.Id;
        RenderProject();
        return Task.CompletedTask;
    }

    private void RenderProject()
    {
        if (_project is null) return;
        _sceneList.Clear();
        foreach (var scene in _project.Scenes)
        {
            var sceneButton = new Button
            {
                Text = $"{scene.Title}\n{scene.Id}",
                HorizontalOptions = LayoutOptions.Fill,
                TextColor = scene.Id == _selectedSceneId ? Color.FromArgb("#F1D0B2") : Color.FromArgb("#8894A7"),
                BackgroundColor = scene.Id == _selectedSceneId ? Color.FromArgb("#32271F") : Colors.Transparent,
                BorderColor = scene.Id == _selectedSceneId ? Color.FromArgb("#E19B67") : Colors.Transparent,
                BorderWidth = 1,
                Padding = new Thickness(10, 9),
                FontSize = 12
            };
            sceneButton.Clicked += (_, _) => { _selectedSceneId = scene.Id; RenderProject(); };
            _sceneList.Add(sceneButton);
        }

        RenderSceneEditor(_project.Scenes.FirstOrDefault(scene => scene.Id == _selectedSceneId));
    }

    private void RenderSceneEditor(StoryScene? scene)
    {
        _editor.Clear();
        if (scene is null)
        {
            _editor.Add(new Label { Text = "Select a scene", TextColor = Color.FromArgb("#8894A7"), FontSize = 16 });
            return;
        }

        _editor.Add(new Label { Text = scene.Title, TextColor = Colors.White, FontSize = 22, FontAttributes = FontAttributes.Bold });
        _editor.Add(CreateSection("SCENE"));
        _editor.Add(CreateEntry("Scene ID", scene.Id, value => { scene.Id = value; _selectedSceneId = value; RenderProject(); }));
        _editor.Add(CreateEntry("Display title", scene.Title, value => { scene.Title = value; RenderProject(); }));
        _editor.Add(CreateEntry("Background path", scene.Background, value => scene.Background = value));
        _editor.Add(CreateSection("DIALOGUE"));
        _editor.Add(CreateEntry("Speaker", scene.Speaker, value => scene.Speaker = value));
        _editor.Add(CreateEditor("Dialogue", scene.Text, value => scene.Text = value));
        _editor.Add(CreateSection("CHOICES"));

        foreach (var choice in scene.Choices.ToArray())
        {
            var choiceRow = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(160)), new ColumnDefinition(GridLength.Auto) },
                ColumnSpacing = 8
            };
            var textEntry = new Entry { Text = choice.Text, Placeholder = "Choice text", TextColor = Colors.White, BackgroundColor = Color.FromArgb("#18212C") };
            textEntry.TextChanged += (_, args) => choice.Text = args.NewTextValue;
            var nextPicker = new Picker { Title = "Next scene", TextColor = Colors.White, BackgroundColor = Color.FromArgb("#18212C") };
            foreach (var target in _project.Scenes) nextPicker.Items.Add(target.Title);
            nextPicker.SelectedIndex = Math.Max(0, _project.Scenes.FindIndex(target => target.Id == choice.Next));
            nextPicker.SelectedIndexChanged += (_, _) => { if (nextPicker.SelectedIndex >= 0) choice.Next = _project.Scenes[nextPicker.SelectedIndex].Id; };
            var remove = new Button { Text = "×", TextColor = Color.FromArgb("#ED7A7A"), BackgroundColor = Colors.Transparent, FontSize = 20 };
            remove.Clicked += (_, _) => { scene.Choices.Remove(choice); RenderSceneEditor(scene); };
            choiceRow.Children.Add(textEntry);
            Grid.SetColumn(nextPicker, 1);
            choiceRow.Children.Add(nextPicker);
            Grid.SetColumn(remove, 2);
            choiceRow.Children.Add(remove);
            _editor.Add(choiceRow);
        }

        var addChoice = new Button { Text = "+ Add choice", TextColor = Color.FromArgb("#E19B67"), BackgroundColor = Colors.Transparent, HorizontalOptions = LayoutOptions.Start };
        addChoice.Clicked += (_, _) => { scene.Choices.Add(new ChoiceOption { Id = $"choice-{scene.Choices.Count + 1}", Text = "New choice", Next = _project.Scenes.FirstOrDefault(target => target.Id != scene.Id)?.Id ?? scene.Id }); RenderSceneEditor(scene); };
        _editor.Add(addChoice);
    }

    private static Label CreateSection(string text) => new() { Text = text, TextColor = Color.FromArgb("#748195"), FontSize = 10, CharacterSpacing = 2, Margin = new Thickness(0, 10, 0, 0) };

    private static View CreateEntry(string title, string value, Action<string> changed)
    {
        var stack = new VerticalStackLayout { Spacing = 5 };
        stack.Add(new Label { Text = title, TextColor = Color.FromArgb("#A2ADBD"), FontSize = 11 });
        var entry = new Entry { Text = value, TextColor = Colors.White, BackgroundColor = Color.FromArgb("#121D29") };
        entry.TextChanged += (_, args) => changed(args.NewTextValue);
        stack.Add(entry);
        return stack;
    }

    private static View CreateEditor(string title, string value, Action<string> changed)
    {
        var stack = new VerticalStackLayout { Spacing = 5 };
        stack.Add(new Label { Text = title, TextColor = Color.FromArgb("#A2ADBD"), FontSize = 11 });
        var editor = new Editor { Text = value, TextColor = Colors.White, BackgroundColor = Color.FromArgb("#121D29"), AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 130 };
        editor.TextChanged += (_, args) => changed(args.NewTextValue);
        stack.Add(editor);
        return stack;
    }
}
