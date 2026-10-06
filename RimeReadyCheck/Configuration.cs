using Dalamud.Configuration;
using System;

namespace RimeReadyCheck;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = true;

    // 0.0 - 1.0
    public float Volume { get; set; } = 1.0f;

    // also play the custom sound when we start the ready check ourselves
    public bool PlayOnOwnReadyCheck { get; set; } = true;

    // the game sound (scd path + index inside the scd) that gets muted, picked from the detected sounds list
    public string? MutedGameSoundPath { get; set; }
    public uint MutedGameSoundNumber { get; set; }

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
