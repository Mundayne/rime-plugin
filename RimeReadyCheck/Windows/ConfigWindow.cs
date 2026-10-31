using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace RimeReadyCheck.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration Configuration;
    private readonly ReadyCheckSound ReadyCheckSound;

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ConfigWindow(Plugin plugin) : base("Rime Ready Check Settings###RimeReadyCheckConfig")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Configuration = plugin.Configuration;
        ReadyCheckSound = plugin.ReadyCheckSound;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var enabled = Configuration.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled))
        {
            Configuration.Enabled = enabled;
            Configuration.Save();
        }

        var playOnOwn = Configuration.PlayOnOwnReadyCheck;
        if (ImGui.Checkbox("Also play when I start the ready check", ref playOnOwn))
        {
            Configuration.PlayOnOwnReadyCheck = playOnOwn;
            Configuration.Save();
        }

        ImGui.Separator();
        DrawCustomSound();
    }

    private void DrawCustomSound()
    {
        var volume = Configuration.Volume * 100f;
        ImGui.SetNextItemWidth(200);
        if (ImGui.SliderFloat("Volume", ref volume, 0f, 100f, "%.0f%%"))
        {
            Configuration.Volume = volume / 100f;
            Configuration.Save();
        }

        foreach (var evt in Enum.GetValues<ReadyCheckEvent>())
        {
            using var id = ImRaii.PushId((int)evt);
            ImGui.Spacing();
            ImGui.TextUnformatted($"On {evt.ToString().ToLowerInvariant()}");

            var count = ReadyCheckSound.SoundCount(evt);
            using (ImRaii.PushIndent())
            {
                if (count == 0)
                {
                    ImGui.TextDisabled($"No sounds in Sounds/{evt}.");
                }
                else
                {
                    ImGui.TextUnformatted($"Plays one of {count} sound(s) at random.");
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Test"))
                        ReadyCheckSound.PlayRandomSound(evt);
                }
            }
        }
    }
}
