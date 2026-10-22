# CloverPit Performance Patch

A reversible, unofficial patch for CloverPit that reduces repeated CPU work, avoids unnecessary text-mesh updates, and adds transition speeds up to **64×**.

All fixes install together in one step. The patch targets **Steam build 22785177**, built with **Unity 6000.2.6f2**. The installer checks the game files before patching and refuses unsupported or unknown modified assemblies.

## What changes

- **Data lookups:** reuse completed identity-array checks instead of converting every symbol, pattern, or powerup enum to a string on routine getters. Original repair logic still runs when needed, including load/save boundaries and array replacement.
- **Scoring:** compact the pattern pool in linear time, sort only active entries, and calculate each active entry's order weight once. The original ordering of tied records and bonus targets is preserved.
- **Text opacity:** eligible plain text updates its color buffers without rebuilding the text mesh. CRT waves, flicker, pauses, and reduced-flashing behavior remain. Rich or uncertain text uses the original setter.
- **Hidden Surgery Machine carousel:** avoid transform writes while previews are hidden, then catch up before showing them again. Visible animation, bounce, sound, and RNG behavior remain.
- **Transition speed:** choose **1×, 2×, 3×, 4×, 8×, 16×, 32×, or 64×**. The menu and console command use the same choices. Existing animation mappings remain, so the selected number is not a literal multiplier for every animation.

I can confidently say gameplay is smoother on my setup, and I can no longer reproduce my original issue. I’m the only gameplay tester so far, on Linux through Proton. I haven’t measured a controlled FPS gain. See [validation and limitations](VALIDATION.md).

## Install

1. Download the appropriate **Windows x64** or **Linux x64** archive from [Releases](https://github.com/JackSkellet/CloverPit-Performance-Patch/releases) and extract it. The binaries are self-contained; no separate .NET installation is required.
2. Save your progress and close CloverPit. Find its installation folder through Steam's **Manage → Browse local files**.
3. Run the installer with that folder. Examples for a default Steam library:

Windows PowerShell:

```powershell
.\CloverPitPatch.exe install "C:\Program Files (x86)\Steam\steamapps\common\CloverPit"
```

Linux:

```bash
chmod +x ./CloverPitPatch
./CloverPitPatch install "$HOME/.local/share/Steam/steamapps/common/CloverPit"
```

Use your actual library path if it differs. The installer creates `CloverPitPerformancePatch-backup` inside the game folder and checks the installed result. Launch the game normally through Steam afterward. No extra Steam launch option is needed.

## Verify or restore

With the game closed, use the same program and game-folder argument:

```text
CloverPitPatch verify <game-directory>
CloverPitPatch restore <game-directory>
```

Use `./CloverPitPatch` on Linux or `.\CloverPitPatch.exe` in Windows PowerShell. Restoration returns the game assembly to the supported original build and preserves current save progress rather than rolling back to the installation-time save backup.

`boot.config`, Unity libraries, save formats, and existing saved options are not changed. Steam verification or a game update may replace patched assemblies. Restore before applying another binary mod; do not force this patch onto a different build.

## Compatibility and scope

Automated tests and installer builds pass on Windows and Linux. **Windows gameplay has not been tested.**

Active-pattern sorting still uses the original quadratic exchange ordering to preserve tie behavior. Pattern-containment checks and insertion scans remain. The patch reduces their surrounding work rather than claiming to replace the entire scoring system.

At the higher transition settings, some sequences become difficult to follow. Lower the setting in **Game & Inputs** when needed.

## Build and test

Use **.NET SDK 10.0.402**, pinned in `global.json`. From the repository root:

```bash
dotnet build src/Installer/Installer.csproj -c Release -r linux-x64
dotnet run --project tests/AlgorithmTests/AlgorithmTests.csproj -c Release
dotnet publish src/Installer/Installer.csproj -c Release -r linux-x64 --self-contained true -o artifacts/linux-x64
dotnet publish src/Installer/Installer.csproj -c Release -r win-x64 --self-contained true -o artifacts/win-x64
```

The algorithm runner uses fixtures and does not need or modify a game installation. It is an executable test runner, so use `dotnet run`, not `dotnet test`. Publishing produces `CloverPitPatch` for Linux and `CloverPitPatch.exe` for Windows.

## Source and licensing

This repository distributes patch code and tooling, **not CloverPit's game assemblies, assets, or decompiled source**. You must own and install the supported game build. The patch operates on your local copy.

Building the installer from source requires **.NET SDK 10.0.402**. The release binaries include their runtime and do not require .NET or Python to be installed.

The patch's own code is provided under the MIT license. CloverPit and third-party components retain their respective licenses and ownership. This project is not affiliated with or endorsed by the game's developers.
