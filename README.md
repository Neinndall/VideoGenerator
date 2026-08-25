## VideoGenerator

[![Latest Release](https://img.shields.io/github/v/release/Neinndall/VideoGenerator?color=yellow&logo=github&logoColor=white&label=Release&style=flat)](https://github.com/Neinndall/VideoGenerator/releases)
[![Downloads](https://img.shields.io/github/downloads/Neinndall/VideoGenerator/total?color=blue&logo=github&logoColor=white&label=Downloads&style=flat)](https://github.com/Neinndall/VideoGenerator/releases)
[![License](https://img.shields.io/github/license/Neinndall/VideoGenerator)](https://github.com/Neinndall/VideoGenerator/blob/main/LICENSE)

VideoGenerator is a Windows desktop application for converting League of Legends voice-over event folders into localized 1080p HUD showcase videos. It combines event parsing, CommunityDragon/DDragon assets, offline Whisper transcription, ImageSharp composition, and FFmpeg encoding in a WPF interface.

## Production workflow

1. **Process Folders** scans the selected media directory, parses event names, associates audio files and families, and builds the event pipeline. All folders with supported audio are eligible, including `_cast2D` and `_cast3D` events.
2. **Prepare Dialogues** resolves icons, transcribes pending audio with Whisper, stores dialogue segments, and prepares HUD images.
3. **Review Dialogues** opens the segment editor for playback, correction, validation, retranscription, and vertically resizable transcript fields.
4. **Render Videos** assembles the prepared images and audio into final MP4 files without repeating transcription.

The global status bar uses deterministic work budgets for preparation, family merging, review, and rendering. Progress only reaches 100% after all planned work has completed or been explicitly skipped.

## Key features

- Dynamic event rules with simple, target, and interaction behavior.
- English, Spanish, and Turkish translations with non-destructive local merging.
- Offline transcription through Whisper.net with selectable model, language, thread count, forced batch retranscription, and optional ambient-tag cleanup.
- Segmented multi-audio dialogue using `||`, with one HUD image and video segment per dialogue part.
- Optional audio-family merging. Bracketed family folders remain attached to their parent event:
  - **OFF:** source audios remain consecutive tracks.
  - **ON:** each family becomes one cached WAV track.
- Dynamic champion, skin, skinline, item, monster, structure, region, and system icon resolution.
- Canonical skinline matching across punctuation variants such as `KDA`, `K D A`, and `K/DA`.
- Live 1920x1080 preview with quick editing, keyboard navigation, cached rendering, and cancellable background work.
- Background Design Studio with custom art, typography, icon alignment, dialogue bubble controls, brightness, contrast, and saturation.
- Deterministic FFmpeg render progress covering images, silence tracks, temporary clips, audio joins, encoding, and final concatenation.
- Quiet batch console with detailed diagnostics retained in local log files.

## Requirements

- Windows x64.
- .NET 10 SDK for source builds.
- Internet access for the initial CommunityDragon/DDragon synchronization, icon downloads, and first Whisper model download.

FFmpeg binaries are embedded and extracted automatically when required. Whisper transcription runs locally after its model has been downloaded.

## Build and run

From the application project directory:

```powershell
cd VideoGenerator_v1.3.0.1
dotnet restore
dotnet build -c Release
dotnet run -c Release
```

## Developer validation

Run deterministic tests with xUnit:

```powershell
dotnet test VideoGenerator.Tests\VideoGenerator.Tests.csproj
```

Parser and pipeline regressions are covered by the xUnit project; it does not open the WPF UI or access external services.

## Configuration files

The application creates and maintains its editable configuration under `%LOCALAPPDATA%/VideoGenerator/Config/`, including:

- `settings.json`
- `event_rules.json`
- `translations.json`
- `dialogues.json`
- `groups.json`
- `champion_aliases.json`
- `skinlines.json`

Bundled defaults and newly introduced keys are merged without overwriting existing user customizations.
