# MidiToMove

Accessible Windows utility for converting standard MIDI files into Ableton Move and Ableton Note `.ablbundle` sets.

Current version: 1.0.

Project page: <https://github.com/OnjLouis/MidiToMove>

## Build

```powershell
.\Build.ps1
```

The build script writes:

```text
portable\MidiToMove.exe
```

To choose another output path:

```powershell
.\Build.ps1 -OutputPath "C:\Tools\MidiToMove\MidiToMove.exe"
```

The build script does not create an INI file by default. Use `-CreateDefaultIni` when you want a starter `MidiToMove.ini` beside the executable.

## Release

Run:

```powershell
.\Release.ps1
```

To publish a GitHub release after the package checks pass:

```powershell
.\Release.ps1 -Publish
```

The release package includes `MidiToMove.exe`, `README.md`, and `LICENSE.txt`. It must not include `MidiToMove.ini`, logs, temp files, test bundles, or token files.

## Behavior

- Opens one or more `.mid` or `.midi` files, or a folder of MIDI files.
- Writes Ableton Move and Ableton Note `.ablbundle` files containing `Song.abl`.
- Uses Move-compatible four-track and eight-scene limits.
- Preserves tempo, time signature, track names, notes, note lengths, and velocities.
- Splits long MIDI files into Move scenes and reports when the source is longer than eight scenes.
- Rounds each clip loop length to the next bar instead of always using a full 16-bar clip.
- Normalizes overlapping notes on the same pitch so Move accepts older or messy MIDI files.
- Lets users select MIDI parts and assign several parts to the same Move track when merging is needed.
- Can prefer MIDI channel 10 drum parts on Move track 1.
- Source MIDI files are never overwritten.
- Existing output bundles are never overwritten; a number is added when needed.
- Settings are stored in `MidiToMove.ini` beside the executable.
- `Help > Check for Updates...` checks GitHub Releases.
- `Help > Version History...` shows the latest GitHub release notes.
- `Help > Donate...` opens <https://onj.me/donate> for optional support.

## Command line

```powershell
MidiToMove.exe --output "C:\Output\Bundles" "C:\Music\song.mid"
MidiToMove.exe --output "C:\Output\Bundles" "C:\Music\MIDI Folder"
MidiToMove.exe --no-drum-preference "C:\Music\song.mid"
```

`--scenes` is intentionally not supported. Move sets always use up to eight scenes.
