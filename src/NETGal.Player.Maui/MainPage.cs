using System.Text.Json;
using NETGal.Engine;

namespace NETGal.Player.Maui;

public sealed class MainPage : ContentPage
{
    private readonly Image _background = new()
    {
        Aspect = Aspect.AspectFill,
        Opacity = 0.92,
        IsVisible = false
    };
    private readonly Label _sceneTitle = new()
    {
        TextColor = Color.FromArgb("#C5CEDA"),
        FontSize = 11,
        CharacterSpacing = 2,
        LineBreakMode = LineBreakMode.TailTruncation
    };
    private readonly Label _speaker = new()
    {
        TextColor = Color.FromArgb("#E19B67"),
        FontSize = 17,
        FontAttributes = FontAttributes.Bold
    };
    private readonly Label _dialogue = new()
    {
        TextColor = Colors.White,
        FontSize = 21,
        LineBreakMode = LineBreakMode.WordWrap,
        MaxLines = 8
    };
    private readonly VerticalStackLayout _choices = new() { Spacing = 10 };
    private readonly ActivityIndicator _loading = new()
    {
        Color = Color.FromArgb("#E19B67"),
        IsRunning = true,
        IsVisible = true,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center
    };
    private GameProject? _project;
    private StoryRuntime? _runtime;

    public MainPage()
    {
        BackgroundColor = Color.FromArgb("#0D141D");
        Title = "NETGal Player";

        var topBar = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            Padding = new Thickness(22, 16, 22, 0)
        };
        topBar.Children.Add(new Label
        {
            Text = "NETGAL PLAYER",
            TextColor = Color.FromArgb("#748195"),
            FontSize = 10,
            CharacterSpacing = 3,
            VerticalOptions = LayoutOptions.Start
        });
        var restart = new Button
        {
            Text = "Restart",
            TextColor = Color.FromArgb("#E19B67"),
            BackgroundColor = Colors.Transparent,
            BorderColor = Color.FromArgb("#4A3A2F"),
            BorderWidth = 1,
            Padding = new Thickness(12, 6),
            Command = new Command(Restart)
        };
        Grid.SetColumn(restart, 1);
        topBar.Children.Add(restart);

        var dialoguePanel = new Grid
        {
            Padding = new Thickness(22, 22, 22, 24),
            VerticalOptions = LayoutOptions.End,
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) }
        };
        dialoguePanel.Children.Add(_sceneTitle);
        Grid.SetRow(_speaker, 1);
        dialoguePanel.Children.Add(_speaker);
        Grid.SetRow(_dialogue, 2);
        dialoguePanel.Children.Add(_dialogue);
        Grid.SetRow(_choices, 3);
        dialoguePanel.Children.Add(_choices);

        var shade = new BoxView { Color = Color.FromArgb("#1F2935"), Opacity = 0.7 };
        Grid.SetRowSpan(_background, 2);
        Grid.SetRowSpan(shade, 2);
        Grid.SetRow(topBar, 0);
        Grid.SetRow(dialoguePanel, 1);
        Grid.SetRow(_loading, 1);

        var root = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) },
            Children = { _background, shade, topBar, dialoguePanel, _loading }
        };

        Content = root;
        Loaded += async (_, _) => await LoadGameAsync();
    }

    private async Task LoadGameAsync()
    {
        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync("game.json");
            _project = await JsonSerializer.DeserializeAsync(stream, GameJsonContext.Default.GameProject);
            if (_project is null)
            {
                throw new InvalidDataException("game.json is empty.");
            }

            _runtime = new StoryRuntime(_project);
            await RenderAsync();
        }
        catch (Exception exception)
        {
            _sceneTitle.Text = "LOAD ERROR";
            _speaker.Text = "NETGal";
            _dialogue.Text = exception.Message;
        }
        finally
        {
            _loading.IsRunning = false;
            _loading.IsVisible = false;
        }
    }

    private async Task RenderAsync()
    {
        if (_runtime is null) return;
        var snapshot = _runtime.Snapshot();
        _sceneTitle.Text = snapshot.SceneTitle.ToUpperInvariant();
        _speaker.Text = snapshot.Speaker;
        _dialogue.Text = snapshot.Text;
        _choices.Clear();
        foreach (var choice in snapshot.Choices)
        {
            var option = new Button
            {
                Text = choice.Text,
                HorizontalOptions = LayoutOptions.Fill,
                TextColor = Color.FromArgb("#F5CFAD"),
                BorderColor = Color.FromArgb("#E19B67"),
                BorderWidth = 1,
                BackgroundColor = Color.FromArgb("#18212C"),
                Padding = new Thickness(14, 11),
                MinimumHeightRequest = 46
            };
            option.Clicked += async (_, _) =>
            {
                _runtime.Choose(choice.Id);
                await RenderAsync();
            };
            _choices.Add(option);
        }

        await LoadBackgroundAsync(snapshot.Background);
    }

    private async Task LoadBackgroundAsync(string? backgroundPath)
    {
        _background.Source = null;
        _background.IsVisible = false;
        if (string.IsNullOrWhiteSpace(backgroundPath)) return;

        var assetPath = backgroundPath.Replace('\\', '/');
        if (assetPath.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
        {
            assetPath = assetPath["assets/".Length..];
        }

        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync($"assets/{assetPath}");
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            var bytes = memory.ToArray();
            _background.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
            _background.IsVisible = true;
        }
        catch (FileNotFoundException)
        {
            // An empty stage is a valid fallback when an optional asset is missing.
        }
    }

    private async void Restart()
    {
        if (_runtime is null) return;
        _runtime.Restart();
        await RenderAsync();
    }
}
