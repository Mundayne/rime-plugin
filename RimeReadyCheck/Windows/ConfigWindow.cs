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

        ImGui.Separator();
        DrawMutedGameSound();
    }

    private void DrawCustomSound()
    {
        if (ReadyCheckSound.SoundCount == 0)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1f)))
                ImGui.TextUnformatted("No sounds found in the plugin's Sounds folder.");
        }
        else
        {
            ImGui.TextUnformatted($"Plays one of {ReadyCheckSound.SoundCount} sounds at random.");
        }

        var volume = Configuration.Volume * 100f;
        ImGui.SetNextItemWidth(200);
        if (ImGui.SliderFloat("Volume", ref volume, 0f, 100f, "%.0f%%"))
        {
            Configuration.Volume = volume / 100f;
            Configuration.Save();
        }

        ImGui.SameLine();
        using (ImRaii.Disabled(ReadyCheckSound.SoundCount == 0))
        {
            if (ImGui.Button("Test"))
                ReadyCheckSound.PlayRandomSound();
        }
    }

    private void DrawMutedGameSound()
    {
        ImGui.TextUnformatted("Game sound to mute");

        if (Configuration.MutedGameSoundPath == null)
        {
            ImGui.TextDisabled("None selected. The game's ready check sound will still play.");
        }
        else
        {
            ImGui.TextUnformatted($"{Configuration.MutedGameSoundPath} #{Configuration.MutedGameSoundNumber}");
            ImGui.SameLine();
            if (ImGui.SmallButton("Clear"))
            {
                Configuration.MutedGameSoundPath = null;
                Configuration.MutedGameSoundNumber = 0;
                Configuration.Save();
            }
        }

        ImGui.Spacing();
        ImGui.TextWrapped("Sounds heard around the last ready check. Start or receive a ready check, then pick the one to mute:");

        var detected = ReadyCheckSound.DetectedSounds;
        if (detected.Count == 0)
        {
            ImGui.TextDisabled("No ready check seen yet.");
            return;
        }

        for (var i = 0; i < detected.Count; i++)
        {
            var sound = detected[i];
            using var id = ImRaii.PushId(i);
            if (ImGui.SmallButton("Mute this"))
            {
                Configuration.MutedGameSoundPath = sound.Path;
                Configuration.MutedGameSoundNumber = sound.SoundNumber;
                Configuration.Save();
            }

            ImGui.SameLine();
            ImGui.TextUnformatted($"{sound.Path} #{sound.SoundNumber}");
        }
    }
}
