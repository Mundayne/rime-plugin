using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.Chat;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Sound;
using NAudio.Wave;

namespace RimeReadyCheck;

// A game sound seen by the PlaySound hook. SoundNumber is the index of the sound inside the .scd file.
public readonly record struct GameSound(string Path, uint SoundNumber);

public sealed unsafe class ReadyCheckSound : IDisposable
{
    // LogMessage sheet rows
    private const uint OwnReadyCheckLogId = 3790;   // "You have commenced a ready check."
    private const uint OtherReadyCheckLogId = 3791; // "<name> initiated a ready check."

    // how far either side of the ready check message a game sound counts as "played around the ready check"
    private const long CaptureWindowMs = 1500;

    // bools are passed as bytes so the native 1-byte bools aren't marshalled as 4-byte BOOLs
    private delegate SoundData* PlaySoundDelegate(
        SoundManager* thisPtr, byte* path, float volume, uint fadeInDuration, float posX, float posY, float posZ,
        float speed, int a9, uint soundNumber, byte autoRelease, SoundVolumeCategory volumeCategory, byte a13,
        int midiNote, byte a15, byte defaultFadeOut, byte isPositional, byte a18);

    private readonly Configuration configuration;
    private readonly Hook<PlaySoundDelegate> playSoundHook;

    // recent game sounds, so we can look back at what played just before a ready check message arrived
    private readonly object soundLock = new();
    private readonly Queue<(long Time, GameSound Sound)> recentSounds = new();
    private long? lastReadyCheckTime;

    private WaveOut? output;
    private AudioFileReader? reader;

    // the bundled recordings in the Sounds folder next to the plugin dll, one is picked at random per ready check
    private readonly string[] soundFiles;

    // game sounds that played around the most recent ready check, shown in the config window
    public IReadOnlyList<GameSound> DetectedSounds { get; private set; } = [];

    public int SoundCount => soundFiles.Length;

    public ReadyCheckSound(Configuration configuration)
    {
        this.configuration = configuration;

        var soundDir = Path.Combine(Plugin.PluginInterface.AssemblyLocation.Directory!.FullName, "Sounds");
        soundFiles = Directory.Exists(soundDir) ? Directory.GetFiles(soundDir) : [];
        if (soundFiles.Length == 0)
            Plugin.Log.Warning($"No sounds found in {soundDir}");

        playSoundHook = Plugin.GameInteropProvider.HookFromAddress<PlaySoundDelegate>(
            (nint)SoundManager.MemberFunctionPointers.PlaySound, PlaySoundDetour);
        playSoundHook.Enable();

        Plugin.ChatGui.LogMessage += OnLogMessage;
    }

    public void Dispose()
    {
        Plugin.ChatGui.LogMessage -= OnLogMessage;
        playSoundHook.Dispose();
        StopCustomSound();
    }

    private SoundData* PlaySoundDetour(
        SoundManager* thisPtr, byte* path, float volume, uint fadeInDuration, float posX, float posY, float posZ,
        float speed, int a9, uint soundNumber, byte autoRelease, SoundVolumeCategory volumeCategory, byte a13,
        int midiNote, byte a15, byte defaultFadeOut, byte isPositional, byte a18)
    {
        try
        {
            var soundPath = Marshal.PtrToStringUTF8((nint)path);
            if (soundPath != null)
            {
                var sound = new GameSound(soundPath, soundNumber);
                RecordSound(sound);

                // play the original at zero volume rather than skipping it, callers expect a valid SoundData back
                if (ShouldMute(sound))
                    volume = 0f;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error in PlaySound detour");
        }

        return playSoundHook.Original(thisPtr, path, volume, fadeInDuration, posX, posY, posZ, speed, a9, soundNumber,
                                      autoRelease, volumeCategory, a13, midiNote, a15, defaultFadeOut, isPositional, a18);
    }

    private bool ShouldMute(GameSound sound)
    {
        return configuration.Enabled
               && SoundCount > 0
               && configuration.MutedGameSoundPath != null
               && sound.SoundNumber == configuration.MutedGameSoundNumber
               && string.Equals(sound.Path, configuration.MutedGameSoundPath, StringComparison.OrdinalIgnoreCase);
    }

    private void RecordSound(GameSound sound)
    {
        var now = Environment.TickCount64;
        lock (soundLock)
        {
            recentSounds.Enqueue((now, sound));
            while (recentSounds.Count > 0 && now - recentSounds.Peek().Time > CaptureWindowMs)
                recentSounds.Dequeue();

            // sounds that come in shortly after the ready check message also count
            // (a null lastReadyCheckTime makes the comparison false, so nothing is captured before the first ready check)
            if (now - lastReadyCheckTime <= CaptureWindowMs && !DetectedSounds.Contains(sound))
                DetectedSounds = [..DetectedSounds, sound];
        }
    }

    private void OnLogMessage(ILogMessage message)
    {
        var isOwn = message.LogMessageId == OwnReadyCheckLogId;
        if (!isOwn && message.LogMessageId != OtherReadyCheckLogId)
            return;

        lock (soundLock)
        {
            lastReadyCheckTime = Environment.TickCount64;
            DetectedSounds = recentSounds.Select(s => s.Sound).Distinct().ToList();
        }

        Plugin.Log.Debug($"Ready check started (log message {message.LogMessageId})");

        if (!configuration.Enabled || (isOwn && !configuration.PlayOnOwnReadyCheck))
            return;

        PlayRandomSound();
    }

    public void PlayRandomSound()
    {
        StopCustomSound();

        if (SoundCount == 0)
            return;

        var file = soundFiles[Random.Shared.Next(soundFiles.Length)];
        try
        {
            // AudioFileReader falls back to Media Foundation for .m4a
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
