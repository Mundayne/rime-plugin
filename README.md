# Rime Ready Check

A [Dalamud](https://dalamud.dev) plugin for FINAL FANTASY XIV that plays custom sounds during ready checks. Each event plays a random clip:

| Event | Plays from |
|---|---|
| A ready check starts | `RimeReadyCheck/Sounds/Start` |
| Everyone is ready | `RimeReadyCheck/Sounds/Success` |
| Someone isn't ready | `RimeReadyCheck/Sounds/Fail` |

The clips play alongside the game's own ready check sounds, which are not muted.

## Install

1. In game, type `/xlsettings` and open **Experimental**.
2. Under **Custom Plugin Repositories**, add this URL, tick **Enabled**, and save:
   ```
   https://raw.githubusercontent.com/Mundayne/rime-plugin/master/repo.json
   ```
3. Type `/xlplugins` and install **Rime Ready Check**.

Type `/rrc` to open the settings, where you can set the volume, test each set of sounds, and choose whether the start sound plays for ready checks you start yourself.

## Development

You need the .NET 10 SDK and XIVLauncher with Dalamud, run at least once.

```
dotnet build
```

To load your build in game, add `RimeReadyCheck\bin\x64\Debug\RimeReadyCheck.dll` (as a full path) under **Dev Plugin Locations** in `/xlsettings` → **Experimental**.

To change the sounds, add or remove `.wav`, `.mp3`, `.m4a`, `.aac` or `.wma` files in the `Sounds` folders and rebuild.

## Releasing

Update `<Version>` in `RimeReadyCheck/RimeReadyCheck.csproj`, commit and push, then push a matching tag:

```
git tag v1.2.3
git push origin v1.2.3
```

The release workflow builds the plugin, publishes `latest.zip` as a GitHub release, and updates `repo.json` on `master`. Don't edit `repo.json` by hand.
