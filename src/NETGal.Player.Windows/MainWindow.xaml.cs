using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using NETGal.Engine;

namespace NETGal.Player.Windows;

public partial class MainWindow : Window
{
    private GameProject? _project;
    private StoryRuntime? _runtime;
    private string _projectDirectory = Directory.GetCurrentDirectory();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadFromArgumentsAsync();
    }

    private async Task LoadFromArgumentsAsync()
    {
        var input = Environment.GetCommandLineArgs().Skip(1).FirstOrDefault(argument => !argument.StartsWith('-'));
        var path = string.IsNullOrWhiteSpace(input) ? Path.Combine(_projectDirectory, "game.json") : input;
        if (Directory.Exists(path)) path = Path.Combine(path, "game.json");
        await LoadProjectAsync(Path.GetFullPath(path));
    }

    private async Task LoadProjectAsync(string projectPath)
    {
        try
        {
            if (!File.Exists(projectPath)) throw new FileNotFoundException("找不到 game.json 文件。", projectPath);
            _projectDirectory = Path.GetDirectoryName(projectPath) ?? Directory.GetCurrentDirectory();
            _project = await GameProject.LoadAsync(projectPath);
            _runtime = new StoryRuntime(_project);
            Title = $"{_project.Title} · NETGal 游戏播放器";
            LoadingLabel.Visibility = Visibility.Collapsed;
            await RenderAsync();
        }
        catch (Exception exception)
        {
            LoadingLabel.Text = exception.Message;
            SpeakerLabel.Text = "NETGal";
            DialogueLabel.Text = "游戏无法打开，请检查 game.json 文件。";
        }
    }

    private async Task RenderAsync()
    {
        if (_runtime is null) return;
        var snapshot = _runtime.Snapshot();
        SceneLabel.Text = snapshot.SceneTitle;
        SpeakerLabel.Text = snapshot.Speaker;
        DialogueLabel.Text = snapshot.Text;
        ChoicesPanel.Children.Clear();
        foreach (var choice in snapshot.Choices)
        {
            var button = new Button { Content = choice.Text, HorizontalContentAlignment = HorizontalAlignment.Left, Tag = choice.Id, MinWidth = 280 };
            button.Click += async (_, _) =>
            {
                _runtime.Choose((string)button.Tag);
                await RenderAsync();
            };
            ChoicesPanel.Children.Add(button);
        }

        await LoadBackgroundAsync(snapshot.Background);
    }

    private async Task LoadBackgroundAsync(string? background)
    {
        BackgroundImage.Source = null;
        if (string.IsNullOrWhiteSpace(background)) return;
        var path = background.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var assetPath = Path.Combine(_projectDirectory, path.StartsWith("assets" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? path : Path.Combine("assets", path));
        if (!File.Exists(assetPath)) return;
        await using var input = File.OpenRead(assetPath);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = input;
        bitmap.EndInit();
        bitmap.Freeze();
        BackgroundImage.Source = bitmap;
    }

    private async void RestartButton_Click(object sender, RoutedEventArgs e)
    {
        _runtime?.Restart();
        await RenderAsync();
    }
}
