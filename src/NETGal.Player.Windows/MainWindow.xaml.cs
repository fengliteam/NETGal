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
    private ProtectedGamePackage? _protectedPackage;
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
            var packageKey = GamePackageKey.Load();
            if (packageKey is not null)
            {
                var packagePath = ResolvePackagePath(projectPath);
                if (!File.Exists(packagePath)) throw new FileNotFoundException("找不到受保护的 game.pkg 文件。", packagePath);
                await using var packageStream = File.OpenRead(packagePath);
                _protectedPackage = await ProtectedGamePackage.LoadAsync(packageStream, packageKey);
                _project = _protectedPackage.Project;
                _projectDirectory = Path.GetDirectoryName(packagePath) ?? AppContext.BaseDirectory;
            }
            else
            {
                if (!File.Exists(projectPath)) throw new FileNotFoundException("找不到 game.json 文件。", projectPath);
                _projectDirectory = Path.GetDirectoryName(projectPath) ?? Directory.GetCurrentDirectory();
                _project = await GameProject.LoadAsync(projectPath);
            }
            _runtime = new StoryRuntime(_project);
            Title = $"{_project.Title} · NETGal 游戏播放器";
            LoadingLabel.Visibility = Visibility.Collapsed;
            await RenderAsync();
        }
        catch (Exception exception)
        {
            LoadingLabel.Text = exception.Message;
            SpeakerLabel.Text = "NETGal";
            DialogueLabel.Text = "游戏无法打开，请检查项目文件或受保护游戏包。";
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
        if (_protectedPackage is not null)
        {
            if (!_protectedPackage.TryGetAsset(background, out var protectedBytes)) return;
            using var protectedInput = new MemoryStream(protectedBytes);
            BackgroundImage.Source = LoadBitmap(protectedInput);
            return;
        }

        var path = background.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var assetPath = Path.Combine(_projectDirectory, path.StartsWith("assets" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? path : Path.Combine("assets", path));
        if (!File.Exists(assetPath)) return;
        await using var input = File.OpenRead(assetPath);
        BackgroundImage.Source = LoadBitmap(input);
    }

    private string ResolvePackagePath(string projectPath)
    {
        if (projectPath.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase)) return projectPath;
        if (Directory.Exists(projectPath)) return Path.Combine(projectPath, "game.pkg");
        return Path.Combine(AppContext.BaseDirectory, "game.pkg");
    }

    private static BitmapImage LoadBitmap(Stream input)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = input;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private async void RestartButton_Click(object sender, RoutedEventArgs e)
    {
        _runtime?.Restart();
        await RenderAsync();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_project is null || _runtime is null) return;
        await GameSaveFile.SaveAsync(GetSavePath(), _runtime.CaptureSave(), GetSaveKey());
        SpeakerLabel.Text = "NETGal";
        DialogueLabel.Text = "进度已保存。";
    }

    private async void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_project is null) return;
        var path = GetSavePath();
        if (!File.Exists(path))
        {
            DialogueLabel.Text = "还没有找到存档。";
            return;
        }

        try
        {
            var save = await GameSaveFile.LoadAsync(path, GetSaveKey());
            _runtime = StoryRuntime.FromSave(_project, save);
            await RenderAsync();
        }
        catch (InvalidDataException exception)
        {
            DialogueLabel.Text = exception.Message;
        }
    }

    private string GetSavePath()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NETGal", "Saves");
        return Path.Combine(directory, $"{GameProject.Slugify(_project?.Id ?? "game")}.ngsave");
    }

    private byte[] GetSaveKey() => GamePackageKey.Load() ?? GameSaveFile.DeriveKey(_project?.Id ?? "game");
}
