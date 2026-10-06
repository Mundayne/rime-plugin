using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using RimeReadyCheck.Windows;

namespace RimeReadyCheck;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/rrc";

    public Configuration Configuration { get; init; }
    public ReadyCheckSound ReadyCheckSound { get; init; }

    public readonly WindowSystem WindowSystem = new("RimeReadyCheck");
    private ConfigWindow ConfigWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ReadyCheckSound = new ReadyCheckSound(Configuration);

        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Rime Ready Check settings"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        // both the settings and the main button in the plugin installer open the settings window
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUI;
    }

    public void Dispose()
    {
        // unregister all actions to not leak anything during disposal of plugin
        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleConfigUI;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        ReadyCheckSound.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        ToggleConfigUI();
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
}
