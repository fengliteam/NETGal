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
        Title = "NETGal 编辑器";

        var openButton = CreateHeaderButton("打开项目", OpenProjectAsync);
        _saveButton = CreateHeaderButton("保存项目", SaveProjectAsync);
        _saveButton.IsEnabled = false;
        var addSceneButton = CreateHeaderButton("+ 新增场景", AddScene);

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
        _editor.Add(new Label { Text = "原生项目编辑器", TextColor = Colors.White, FontSize = 22, FontAttributes = FontAttributes.Bold });
        _editor.Add(new Label { Text = "打开 game.json 后，可以编辑场景、对白、选项和项目设置。编辑器使用原生 Windows 控件，并写入 Android、Windows、iOS 和 Mac Catalyst 播放器都能使用的同一份 JSON。", TextColor = Color.FromArgb("#8894A7"), FontSize = 14, LineBreakMode = LineBreakMode.WordWrap });
        _editor.Add(new Label { Text = "浏览器编辑器仍然可以作为轻量备用工具，但导出的游戏不会使用浏览器界面。", TextColor = Color.FromArgb("#E19B67"), FontSize = 12, LineBreakMode = LineBreakMode.WordWrap });
    }

    private async Task OpenProjectAsync()
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "打开 NETGal game.json" });
        if (file is null) return;

        await using var stream = await file.OpenReadAsync();
        _project = await JsonSerializer.DeserializeAsync(stream, GameJsonContext.Default.GameProject);
        if (_project is null)
        {
            await DisplayAlert("打开失败", "选择的文件不是有效的 NETGal 项目。", "确定");
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
            await DisplayAlert("无法保存", string.Join(Environment.NewLine, issues.Where(issue => issue.Severity == "error").Select(issue => issue.Message)), "确定");
            return;
        }

        await _project.SaveAsync(_projectPath);
        _status.Text = $"已保存 {DateTime.Now:HH:mm:ss} · {_projectPath}";
    }

    private Task AddScene()
    {
        if (_project is null) return Task.CompletedTask;
        var index = _project.Scenes.Count + 1;
        var scene = new StoryScene { Id = $"scene-{index}", Title = "新场景", Speaker = "旁白", Text = "请在这里写下场景内容。" };
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
        _editor.Add(new Label { Text = "请选择一个场景", TextColor = Color.FromArgb("#8894A7"), FontSize = 16 });
            return;
        }

        _editor.Add(new Label { Text = scene.Title, TextColor = Colors.White, FontSize = 22, FontAttributes = FontAttributes.Bold });
        _editor.Add(CreateSection("场景"));
        _editor.Add(CreateEntry("场景 ID", scene.Id, value => { scene.Id = value; _selectedSceneId = value; RenderProject(); }));
        _editor.Add(CreateEntry("显示标题", scene.Title, value => { scene.Title = value; RenderProject(); }));
        _editor.Add(CreateEntry("背景图片路径", scene.Background, value => scene.Background = value));
        _editor.Add(CreateSection("对白"));
        _editor.Add(CreateEntry("说话人", scene.Speaker, value => scene.Speaker = value));
        _editor.Add(CreateEditor("对白内容", scene.Text, value => scene.Text = value));
        _editor.Add(CreateSection("选项"));

        foreach (var choice in scene.Choices.ToArray())
        {
            var choiceRow = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(160)), new ColumnDefinition(GridLength.Auto) },
                ColumnSpacing = 8
            };
            var textEntry = new Entry { Text = choice.Text, Placeholder = "选项文字", TextColor = Colors.White, BackgroundColor = Color.FromArgb("#18212C") };
            textEntry.TextChanged += (_, args) => choice.Text = args.NewTextValue;
            var nextPicker = new Picker { Title = "下一场景", TextColor = Colors.White, BackgroundColor = Color.FromArgb("#18212C") };
            foreach (var target in _project.Scenes) nextPicker.Items.Add(target.DisplayName);
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

        var addChoice = new Button { Text = "+ 新增选项", TextColor = Color.FromArgb("#E19B67"), BackgroundColor = Colors.Transparent, HorizontalOptions = LayoutOptions.Start };
        addChoice.Clicked += (_, _) => { scene.Choices.Add(new ChoiceOption { Id = $"choice-{scene.Choices.Count + 1}", Text = "新选项", Next = _project.Scenes.FirstOrDefault(target => target.Id != scene.Id)?.Id ?? scene.Id }); RenderSceneEditor(scene); };
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
