<div align="center">
  <img src="VideoGenerator/Resources/logo.png" alt="VideoGenerator logo" width="104">
</div>

# 🎬 VideoGenerator

[![Latest Release](https://img.shields.io/github/v/release/Neinndall/VideoGenerator?color=yellow&logo=github&logoColor=white&label=Release&style=flat)](https://github.com/Neinndall/VideoGenerator/releases)
[![Downloads](https://img.shields.io/github/downloads/Neinndall/VideoGenerator/total?color=blue&logo=github&logoColor=white&label=Downloads&style=flat)](https://github.com/Neinndall/VideoGenerator/releases)
[![License](https://img.shields.io/github/license/Neinndall/VideoGenerator)](LICENSE)

VideoGenerator is a Windows application for turning **League of Legends voice-over event folders into polished, localized 1080p showcase videos**. It brings folder analysis, event recognition, voice transcription, HUD design, dialogue review, and video rendering together in one production workspace.

The app uses the League of Legends files installed on your PC to find champion portraits, skin artwork, items, monsters, structures, and other game imagery. No external image or hash catalog service is required for game assets.

## 🚀 Getting Started

### Requirements

- **Windows x64**.
- **.NET 10 Desktop Runtime** for framework-dependent builds.
- A local **League of Legends installation** containing Game and Plugins WAD archives for game image and metadata extraction.
- Voice-over audio files to process (including `.ogg`, `.wav`, and `.mp3`).
- Internet access only when needed to obtain a Whisper transcription model; transcription then runs locally.

### Installation and first launch

1. Check the [Releases page](https://github.com/Neinndall/VideoGenerator/releases) for an available build. **VideoGenerator 2.0 is not yet available as a published installer.**
2. When using a packaged build, extract the complete archive and launch `VideoGenerator.exe`.
3. Open **Settings → General & Media** and select your League of Legends installation directory, containing the **Game** and **Plugins** folders. The application checks whether the WAD files are available.
4. Choose your media source and preferred dictionary language, then adjust audio and transcription settings as needed.
5. Open **Dashboard** and select **SCAN FOLDERS** to analyze your voice-over folders.

VideoGenerator never replaces the game installation path you chose with an automatic default. Previously saved settings and locally cached assets are reused.

## 🏛️ Main Workspaces

### 🎬 Dashboard — Production Studio

The central workspace for turning a collection of voice-over folders into finished videos.

- **Folder Analysis:** Scan voice-over event directories, including supported `_cast2D` and `_cast3D` folders, and associate their audio tracks with the correct events.
- **Detected Pipeline:** Review recognized events, audio availability, missing images, and events that need a new mapping. Filter by **All**, **Errors**, **Pending**, or **Mapping**.
- **Selection and Search:** Search events, select individual entries or all currently visible events, and process the chosen selection.
- **Live Preview:** See the final HUD composition at **1920 × 1080**, with the selected event text and icon.
- **Quick Edit:** Correct the HUD text, icon category, or image lookup for an individual event and apply the changes.
- **Retry Icons:** Reattempt missing game artwork without repeating the audio folder scan.
- **Export Manifest:** Save an event analysis report as JSON without including source audio, transcripts, or absolute media paths.
- **Batch Rendering:** Prepare, review, and render videos with a global progress indicator and cancellation support.

#### Production workflow

| Stage | What it does |
| --- | --- |
| **1. Scan Folders** | Discovers audio and interprets event folder names |
| **2. Prepare Dialogues** | Resolves game icons, transcribes audio using Whisper when enabled, and prepares HUD images |
| **3. Review** | Opens the dialogue editor to correct, listen to, and validate each segment |
| **4. Render Videos** | Combines images and audio with FFmpeg and produces the finished MP4 files |

Audio-family merging is optional. When enabled, related tracks are joined into a cached WAV; when disabled, they remain separate tracks in sequence. Configurable silence padding is respected during rendering.

#### Dialogue Editor

The **Review** stage opens a dedicated editor with audio playback, editable transcripts, pending/validated status, and navigation between segments. Multi-audio events can be represented as separate dialogue parts, with each part receiving its own HUD frame and video segment. You can review or retranscribe pending content before rendering the final video.

### 🎨 Visual Design — HUD Studio

Customize the look of your video overlay while watching an interactive preview.

- **Background Artwork:** Select your own background or search available champion and skin splashes from the installed game files.
- **Typography:** Adjust text appearance, fonts, and visual hierarchy.
- **Icon Placement:** Change icon alignment, position, borders, and presentation.
- **Dialogue Bubble:** Configure the subtitle area, its size, position, opacity, and border styling.
- **Image Adjustments:** Tune brightness, contrast, and saturation.
- **Live Preview:** Inspect the composition at 1080p and expand the preview area for closer adjustments.

Design settings are saved and applied to subsequently generated HUD frames.

### 🧩 Event Mapping

Teach VideoGenerator how to interpret different League of Legends voice-over event names and which icon category to request.

| Mapping section | Purpose |
| --- | --- |
| **Event Rules** | Define actions, interactions, target behavior, HUD text keys, and icon categories |
| **Groups** | Organize champions into custom regions, classes, roles, and thematic groups |
| **Champions** | Manage displayed champion names and internal aliases; view official Riot class roles |
| **Monsters** | Recognize jungle camps and epic monsters, including personal entries |
| **Structures** | Recognize structures and map event keywords to the appropriate targets |

- **Ready-to-use mappings:** VideoGenerator comes with built-in rules and catalogs. Personal entries can be added alongside them; protected **BUILT-IN** records cannot be deleted from the relevant catalog lists.
- **Automatically updated champions:** With a configured League installation, Riot's local `champion-summary.json` fills in current champions, internal aliases, and available class roles. New champions can therefore be recognized without a manual application update.
- **Create a rule from an event:** A **Needs Mapping** event in Dashboard provides a shortcut to Event Mapping with suggested values already filled in. Rescan the affected folders after saving the rule to apply it.
- **Accurate image categories:** Recognize champions, items, monsters, structures, skins, and **skinlines** as distinct targets. A recognized event can still show **Missing Icon** when its actual image is unavailable.

Riot roles such as *fighter*, *mage*, and *tank* describe champion classes; they are not automatically treated as lane assignments such as ADC or Jungle.

### 🌐 Dictionary

Manage the text shown in the HUD across **English, Spanish, and Turkish**.

- **Multilingual Entries:** View and edit localized event text by dictionary key.
- **Search and Filters:** Find translations quickly and focus on a selected language.
- **Custom Text:** Add new keys or correct existing phrases without losing other languages.
- **Integrated Mapping:** Event Rules can reference dictionary keys so the same event receives the appropriate localized description.

Built-in translations are merged with personal changes, preserving existing custom text.

### ⚙️ Settings

Three dedicated sections keep game paths, media handling, and transcription controls organized.

- **General & Media:** Configure the local League installation, review Game/Plugins WAD availability, select media directories, and set dictionary preferences.
- **Processing:** Adjust silence handling and optional audio-family merging.
- **Transcription:** Enable or disable Whisper, choose language/model options, configure processing performance, and control batch retranscription or ambient-tag cleanup.

Settings persist between sessions and do not require editing configuration files manually.

## 🖼️ Local Game Assets and Automatic Updates

VideoGenerator extracts imagery and metadata directly from your League of Legends `.wad` / `.wad.client` archives using LeagueToolkit. Official caches support champion aliases, skins, skinlines, item IDs, and the associated icon lookups.

- **Champion portraits and skins:** Match champion identities and skin data to local artwork.
- **Thematic skinlines:** Recognize groups such as *True Damage*, *HEARTSTEEL*, and *Arcade* without confusing them with champion names.
- **Items and objectives:** Resolve object IDs, monsters, structures, and supported game-system icons using local data and verified asset paths.
- **Patch-aware refresh:** Detect changes to installed game archives and refresh the affected cached catalogs and in-memory lookups.
- **Honest missing-image handling:** If the required artwork cannot be found, report **Missing Icon** rather than displaying an unrelated image.

The game installation is the source of official metadata. Your custom event rules, aliases, groups, and other settings remain separate and editable.

## 💾 Files and Output

User configuration and cached game data live under `%LOCALAPPDATA%\VideoGenerator\`:

| Folder | Contains |
| --- | --- |
| `Config\` | Saved settings, translations, dialogue data, and editable Event Mapping catalogs |
| `Cache\` | Locally extracted Riot metadata and reusable assets |

Each Event Mapping section has one editable catalog, such as `event_rules.json`, `groups.json`, `champion_aliases.json`, `monsters.json`, or `structures.json`. Factory defaults and personal additions coexist in those files.

Generated HUD images and MP4 videos are organized in the application's `Generated/Images/` and `Generated/Media/` output folders, grouped by champion.

## ⌨️ Keyboard Shortcuts

| Shortcut | Action |
| --- | --- |
| **Ctrl + 1–5** | Switch between the five workspaces |
| **Ctrl + F** | Focus event search in Dashboard |
| **Ctrl + B** | Collapse or expand the navigation sidebar |

## 🛠️ Build from Source

The repository targets **.NET 10 / WPF** and references the sibling `LeagueToolkit-4.1.0-beta.53` source checkout. Keep that checkout beside the VideoGenerator project when compiling.

From the repository root:

```powershell
dotnet restore VideoGenerator/VideoGenerator.csproj
dotnet build VideoGenerator/VideoGenerator.csproj -c Release
dotnet run --project VideoGenerator/VideoGenerator.csproj -c Release
```

To run the test suite:

```powershell
dotnet test VideoGenerator.Tests/VideoGenerator.Tests.csproj -c Release
```

## 🤝 Contributing

Open an issue or submit a pull request with a clear description of the problem or feature. For new event-name patterns, include representative folder names and the expected HUD text and icon behavior.

See [CHANGELOG.md](CHANGELOG.md) for version notes.

## 📄 License

VideoGenerator is licensed under the [GNU General Public License v3.0](LICENSE). Bundled third-party dependencies retain their respective licenses.
