using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Game.Chat;
using NAudio.Wave;

namespace RimeReadyCheck;

// Each event plays a random file from the matching Sounds/<event> folder next to the plugin dll.
public enum ReadyCheckEvent
{
    Start,
    Success,
    Fail,
}

public sealed class ReadyCheckSound : IDisposable
{
    // LogMessage sheet rows
    private const uint OwnReadyCheckLogId = 3790;   // "You have commenced a ready check."
    private const uint OtherReadyCheckLogId = 3791; // "<name> initiated a ready check."
    private const uint ReadyCheckCompleteLogId = 3794; // "Ready check complete. Ready: lnum1/lnum3 Not Ready: lnum2/lnum3"

    private static readonly string[] AudioExtensions = [".wav", ".mp3", ".m4a", ".aac", ".wma"];

    private readonly Configuration configuration;

    private WaveOut? output;
    private AudioFileReader? reader;

    private readonly Dictionary<ReadyCheckEvent, string[]> soundFiles = new();

    public ReadyCheckSound(Configuration configuration)
    {
        this.configuration = configuration;

        var soundRoot = Path.Combine(Plugin.PluginInterface.AssemblyLocation.Directory!.FullName, "Sounds");
        foreach (var evt in Enum.GetValues<ReadyCheckEvent>())
        {
            var dir = Path.Combine(soundRoot, evt.ToString());
            soundFiles[evt] = Directory.Exists(dir)
                                  ? Directory.GetFiles(dir).Where(f => AudioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())).ToArray()
                                  : [];
            Plugin.Log.Debug($"{soundFiles[evt].Length} sound(s) for {evt} in {dir}");
        }

        Plugin.ChatGui.LogMessage += OnLogMessage;
    }

    public void Dispose()
    {
        Plugin.ChatGui.LogMessage -= OnLogMessage;
        StopCustomSound();
    }

    public int SoundCount(ReadyCheckEvent evt) => soundFiles[evt].Length;

    private void OnLogMessage(ILogMessage message)
    {
        ReadyCheckEvent evt;
        switch (message.LogMessageId)
        {
            case OwnReadyCheckLogId:
            case OtherReadyCheckLogId:
                evt = ReadyCheckEvent.Start;
                break;
            case ReadyCheckCompleteLogId:
                if (!TryGetNotReadyCount(message, out var notReady))
                {
                    Plugin.Log.Warning($"Could not read ready check result: {message.FormatLogMessageForDebugging()}");
                    return;
                }
                evt = notReady == 0 ? ReadyCheckEvent.Success : ReadyCheckEvent.Fail;
                break;
            default:
                return;
        }

        Plugin.Log.Debug($"Ready check {evt} (log message {message.LogMessageId})");

        if (!configuration.Enabled)
            return;

        if (message.LogMessageId == OwnReadyCheckLogId && !configuration.PlayOnOwnReadyCheck)
            return;

        PlayRandomSound(evt);
    }

    // 3794 has three int parameters: ready, not ready, total. Checking that they add up guards against
    // reading the wrong slots if the parameter indexing ever differs from what we expect.
    private static bool TryGetNotReadyCount(ILogMessage message, out int notReady)
    {
        if (message.TryGetIntParameter(0, out var ready)
            && message.TryGetIntParameter(1, out notReady)
            && message.TryGetIntParameter(2, out var total)
            && ready + notReady == total)
            return true;

        notReady = 0;
        return false;
    }

    public void PlayRandomSound(ReadyCheckEvent evt)
    {
        StopCustomSound();

        var files = soundFiles[evt];
        if (files.Length == 0)
            return;

        var file = files[Random.Shared.Next(files.Length)];
        try
        {
            reader = new AudioFileReader(file) { Volume = Math.Clamp(configuration.Volume, 0f, 1f) };
            output = new WaveOut();
            output.Init(reader);
            output.Play();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Failed to play sound {file}");
            StopCustomSound();
        }
    }

    private void StopCustomSound()
    {
        output?.Dispose();
        output = null;
        reader?.Dispose();
        reader = null;
    }
}
