using System.Reflection;
using System.Runtime.Loader;

namespace NETGal.Engine;

public interface INetGalPlugin
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    void Configure(INetGalPluginHost host);
}

public interface INetGalPluginHost
{
    IReadOnlyCollection<PluginCommandDefinition> Commands { get; }
    IReadOnlyCollection<PluginMenuItem> MenuItems { get; }
    IReadOnlyCollection<PluginPanelDefinition> Panels { get; }
    void RegisterCommand(PluginCommandDefinition command);
    void RegisterMenuItem(PluginMenuItem menuItem);
    void RegisterPanel(PluginPanelDefinition panel);
}

public sealed record PluginCommandDefinition(string Id, string Description, IReadOnlyList<string> Arguments);
public sealed record PluginMenuItem(string Id, string Title);
public sealed record PluginPanelDefinition(string Id, string Title);
public sealed record LoadedNetGalPlugin(string Id, string Name, string Version, string AssemblyPath);

public sealed class PluginCatalog : INetGalPluginHost
{
    private readonly List<PluginCommandDefinition> _commands = [];
    private readonly List<PluginMenuItem> _menuItems = [];
    private readonly List<PluginPanelDefinition> _panels = [];
    private readonly List<LoadedNetGalPlugin> _plugins = [];

    public IReadOnlyCollection<PluginCommandDefinition> Commands => _commands;
    public IReadOnlyCollection<PluginMenuItem> MenuItems => _menuItems;
    public IReadOnlyCollection<PluginPanelDefinition> Panels => _panels;
    public IReadOnlyCollection<LoadedNetGalPlugin> Plugins => _plugins;

    public IReadOnlyList<LoadedNetGalPlugin> LoadFromDirectory(string directory, Action<Exception>? onError = null)
    {
        if (!Directory.Exists(directory)) return _plugins;
        foreach (var assemblyPath in Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));
                foreach (var pluginType in assembly.GetTypes().Where(type => !type.IsAbstract && typeof(INetGalPlugin).IsAssignableFrom(type)))
                {
                    if (Activator.CreateInstance(pluginType) is not INetGalPlugin plugin) continue;
                    plugin.Configure(this);
                    _plugins.Add(new LoadedNetGalPlugin(plugin.Id, plugin.Name, plugin.Version, assemblyPath));
                }
            }
            catch (Exception exception) when (exception is BadImageFormatException or FileLoadException or ReflectionTypeLoadException or TargetInvocationException)
            {
                onError?.Invoke(exception);
            }
        }

        return _plugins;
    }

    public void RegisterCommand(PluginCommandDefinition command)
    {
        if (_commands.All(existing => !existing.Id.Equals(command.Id, StringComparison.OrdinalIgnoreCase))) _commands.Add(command);
    }

    public void RegisterMenuItem(PluginMenuItem menuItem)
    {
        if (_menuItems.All(existing => !existing.Id.Equals(menuItem.Id, StringComparison.OrdinalIgnoreCase))) _menuItems.Add(menuItem);
    }

    public void RegisterPanel(PluginPanelDefinition panel)
    {
        if (_panels.All(existing => !existing.Id.Equals(panel.Id, StringComparison.OrdinalIgnoreCase))) _panels.Add(panel);
    }
}
