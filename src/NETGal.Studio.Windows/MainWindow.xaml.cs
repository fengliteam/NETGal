using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using NETGal.AiPrompt;
using NETGal.Engine;

namespace NETGal.Studio.Windows;

public partial class MainWindow : Window
{
    private GameProject? _project;
    private string? _projectPath;
    private StoryRuntime? _previewRuntime;
    private readonly PluginCatalog _plugins = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            var input = Environment.GetCommandLineArgs().Skip(1).FirstOrDefault(argument => !argument.StartsWith('-'));
            if (!string.IsNullOrWhiteSpace(input)) await LoadProjectAsync(Directory.Exists(input) ? Path.Combine(input, "game.json") : input);
        };
    }

    private async void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "NETGal 项目|game.json|JSON 文件|*.json", Title = "打开 NETGal game.json" };
        if (dialog.ShowDialog() == true) await LoadProjectAsync(dialog.FileName);
    }

    private async Task LoadProjectAsync(string path)
    {
        try
        {
            _project = await GameProject.LoadAsync(Path.GetFullPath(path));
            _projectPath = Path.GetFullPath(path);
            var projectDirectory = Path.GetDirectoryName(_projectPath) ?? Directory.GetCurrentDirectory();
            _plugins.LoadFromDirectory(Path.Combine(projectDirectory, "plugins"));
            _previewRuntime = new StoryRuntime(_project);
            ProjectTitle.Text = _project.Title;
            StatusLabel.Text = _projectPath;
            SaveButton.IsEnabled = true;
            RenderProject();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "无法打开项目", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_project is null || _projectPath is null) return;
        var issues = ProjectValidator.Validate(_project).Where(issue => issue.Severity == "error").ToArray();
        if (issues.Length > 0)
        {
            MessageBox.Show(string.Join(Environment.NewLine, issues.Select(issue => issue.Message)), "请先修复项目错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        await _project.SaveAsync(_projectPath);
        StatusLabel.Text = $"已保存 {DateTime.Now:HH:mm:ss} · {_projectPath}";
    }

    private void AddSceneButton_Click(object sender, RoutedEventArgs e)
    {
        if (_project is null) return;
        var id = $"scene-{_project.Scenes.Count + 1}";
        _project.Scenes.Add(new StoryScene { Id = id, Title = "新场景", Speaker = "旁白", Text = "请在这里写下场景内容。" });
        RenderProject();
        SceneList.SelectedIndex = _project.Scenes.Count - 1;
    }

    private void AiPromptButton_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(AiPromptPlugin.GetSpecificationPrompt());
        StatusLabel.Text = "AI 规范已复制；请先让 AI 提问，再把 JSON 指令粘贴回编辑器。";
    }

    private void SceneList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SceneList.SelectedItem is StoryScene scene) RenderScene(scene);
    }

    private void RenderProject()
    {
        if (_project is null) return;
        SceneList.ItemsSource = null;
        SceneList.ItemsSource = _project.Scenes;
        SceneList.DisplayMemberPath = nameof(StoryScene.DisplayName);
        if (SceneList.SelectedIndex < 0 && _project.Scenes.Count > 0) SceneList.SelectedIndex = 0;
    }

    private void RenderScene(StoryScene scene)
    {
        EditorPanel.Children.Clear();
        EditorPanel.Children.Add(Heading(scene.Title));
        EditorPanel.Children.Add(Label("场景"));
        EditorPanel.Children.Add(Field("场景 ID", scene.Id, value => { scene.Id = value; SceneList.Items.Refresh(); }));
        EditorPanel.Children.Add(Field("显示标题", scene.Title, value => { scene.Title = value; ProjectTitle.Text = _project?.Title ?? "NETGal 编辑器"; SceneList.Items.Refresh(); }));
        EditorPanel.Children.Add(Field("背景图片路径", scene.Background, value => { scene.Background = value; RenderPreview(scene); }));
        EditorPanel.Children.Add(Label("对白"));
        EditorPanel.Children.Add(Field("说话人", scene.Speaker, value => { scene.Speaker = value; RenderPreview(scene); }));
        EditorPanel.Children.Add(MultilineField("对白内容", scene.Text, value => { scene.Text = value; RenderPreview(scene); }));
        EditorPanel.Children.Add(MultilineField("指令 JSON（可选）", SerializeCommands(scene.Commands), value =>
        {
            try
            {
                scene.Commands = JsonSerializer.Deserialize(value, GameJsonContext.Default.ListStoryCommand) ?? [];
                StatusLabel.Text = "指令已更新";
            }
            catch (JsonException)
            {
                StatusLabel.Text = "指令 JSON 格式有误，保存前请修正";
            }
        }));
        EditorPanel.Children.Add(Label("选项"));
        foreach (var choice in scene.Choices.ToArray()) EditorPanel.Children.Add(ChoiceRow(scene, choice));
        var addChoice = new Button { Content = "+ 新增选项", HorizontalAlignment = HorizontalAlignment.Left, Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush") };
        addChoice.Click += (_, _) => { scene.Choices.Add(new ChoiceOption { Id = $"choice-{scene.Choices.Count + 1}", Text = "新选项", Next = _project?.Scenes.FirstOrDefault(item => item.Id != scene.Id)?.Id ?? scene.Id }); RenderScene(scene); };
        EditorPanel.Children.Add(addChoice);
        RenderPreview(scene);
    }

    private FrameworkElement ChoiceRow(StoryScene scene, ChoiceOption choice)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new TextBox { Text = choice.Text };
        text.TextChanged += (sender, _) => { choice.Text = ((TextBox)sender).Text; RenderPreview(scene); };
        var next = new ComboBox { ItemsSource = _project?.Scenes, DisplayMemberPath = nameof(StoryScene.DisplayName), SelectedValuePath = nameof(StoryScene.Id), SelectedValue = choice.Next, Margin = new Thickness(8, 4, 8, 10) };
        next.SelectionChanged += (_, _) => { if (next.SelectedValue is string value) choice.Next = value; };
        var remove = new Button { Content = "×", Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush") };
        remove.Click += (_, _) => { scene.Choices.Remove(choice); RenderScene(scene); };
        grid.Children.Add(text);
        Grid.SetColumn(next, 1);
        grid.Children.Add(next);
        Grid.SetColumn(remove, 2);
        grid.Children.Add(remove);
        return grid;
    }

    private FrameworkElement Field(string title, string? value, Action<string> changed)
    {
        var panel = new StackPanel();
        panel.Children.Add(Label(title));
        var box = new TextBox { Text = value ?? "" };
        box.TextChanged += (sender, _) => changed(((TextBox)sender).Text);
        panel.Children.Add(box);
        return panel;
    }

    private FrameworkElement MultilineField(string title, string? value, Action<string> changed)
    {
        var panel = new StackPanel();
        panel.Children.Add(Label(title));
        var box = new TextBox { Text = value ?? "", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 130, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        box.TextChanged += (sender, _) => changed(((TextBox)sender).Text);
        panel.Children.Add(box);
        return panel;
    }

    private TextBlock Label(string text) => new() { Text = text, Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush"), FontSize = 11, Margin = new Thickness(0, 8, 0, 0) };
    private TextBlock Heading(string text) => new() { Text = text, Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"), FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 16) };
    private static string SerializeCommands(IReadOnlyList<StoryCommand> commands) => JsonSerializer.Serialize(commands, GameJsonContext.Default.ListStoryCommand);

    private void RenderPreview(StoryScene scene)
    {
        PreviewTitle.Text = scene.Title.ToUpperInvariant();
        PreviewSpeaker.Text = scene.Speaker;
        PreviewText.Text = scene.Text;
        PreviewChoices.Children.Clear();
        foreach (var choice in scene.Choices) PreviewChoices.Children.Add(new Button { Content = choice.Text, IsHitTestVisible = false });
        _ = LoadPreviewImageAsync(scene.Background);
    }

    private async Task LoadPreviewImageAsync(string? background)
    {
        PreviewBackground.Source = null;
        if (string.IsNullOrWhiteSpace(background) || _projectPath is null) return;
        var projectDirectory = Path.GetDirectoryName(_projectPath) ?? Directory.GetCurrentDirectory();
        var relative = background.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var full = Path.Combine(projectDirectory, relative.StartsWith("assets" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? relative : Path.Combine("assets", relative));
        if (!File.Exists(full)) return;
        await using var input = File.OpenRead(full);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = input;
        bitmap.EndInit();
        bitmap.Freeze();
        PreviewBackground.Source = bitmap;
    }
}
