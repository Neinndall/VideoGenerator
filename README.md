# VideoGenerator

[![Latest Release](https://img.shields.io/github/v/release/Neinndall/VideoGenerator?color=yellow&logo=github&logoColor=white&label=Release&style=flat)](https://github.com/Neinndall/VideoGenerator/releases)
[![Downloads](https://img.shields.io/github/downloads/Neinndall/VideoGenerator/total?color=blue&logo=github&logoColor=white&label=Downloads&style=flat)](https://github.com/Neinndall/VideoGenerator/releases)
[![License](https://img.shields.io/github/license/Neinndall/VideoGenerator)](https://github.com/Neinndall/VideoGenerator/blob/main/LICENSE)

VideoGenerator is a Windows WPF application that turns League of Legends voice-over event folders into localized 1080p HUD showcase videos. The **v2.0.0.0 development build** uses LeagueToolkit and the user's installed WAD archives for game art. The version is set in `VideoGenerator/VideoGenerator.csproj` and displayed as **v2.0.0**; a nonzero fourth component is displayed (for example, `2.0.0.1` → `v2.0.0.1`). **This does not imply that a v2 release or installer has been published.**

## Production workflow

1. **Scan Folders** scans the selected media directory, parses event names, associates audio files/families and builds the Detected Pipeline (including supported `_cast2D` and `_cast3D` events).
2. **Prepare Dialogues** resolves icons from local WADs, transcribes pending audio with Whisper, stores dialogue segments and prepares HUD images.
3. **Review** opens the segment editor for playback, correction, validation and retranscription.
4. **Render Videos** combines prepared images/audio into MP4s; a successful render atomically publishes the output.

An event flagged **Needs Mapping** shows a button in Detected Pipeline. This already opens **Event Mapping → Event Rules** and pre-fills a suggested keyword/category/dictionary key. The user can complete and save the rule and its translations. This is an *existing workflow*, not a future wizard. Reanalyzing the affected folders applies the new recognition rule; automatic in-place reprocessing of the Pipeline after saving is **not yet implemented**.

After scanning, **Export Manifest** saves a lightweight JSON report with discovered event names, language, champion, mapping results, icon choices, and audio counts. The report omits audio files, dialogue transcripts, and absolute source paths, making it practical to compare large champion collections and report unknown events.

The global status bar uses deterministic work budgets for preparation, family merging, review, and rendering. Progress only reaches 100% after all planned work has completed or been explicitly skipped.

## Key features

- Dynamic event rules with simple, target and interaction behavior, plus editable Event Mapping lists for groups, champions/aliases, monsters and structures.
- English, Spanish, and Turkish translations with non-destructive local merging.
- Offline transcription through Whisper.net with selectable model, language, thread count, forced batch retranscription, and optional ambient-tag cleanup.
- Segmented multi-audio dialogue using `||`, with one HUD image and video segment per dialogue part.
- Optional audio-family merging. Bracketed family folders remain attached to their parent event:
  - **OFF:** source audios remain consecutive tracks.
  - **ON:** each family becomes one cached WAV track.
- Champion, skin, **skinline (thematic)**, item, monster, structure, region and system icon lookup. Event Mapping and Dashboard Quick Edit both expose `skinline` as an icon category. An event such as `FirstEncounter3DEternalAspects` resolves the official **The Eternal Aspects** theme and uses one of its real skin members rather than pretending it is a champion. A **Missing Icon** result is distinct from **Needs Mapping**; the app does not substitute an unrelated image to silence the warning.
- Offline artwork from installed `.wad`/`.wad.client` archives, using metadata paths → xxHash64 → chunk → PNG cache, and **RETRY ICONS** without rescanning audio. Verified WAD paths cover Baron, Dragon, Rift Herald, Elder Dragon, **Blue Sentinel, Red Brambleback, Gromp, Krugs, Razorbeak, Murk Wolf, Rift Scuttler and Atakhan**, Turret/Nexus and several ping textures (including `on_my_way`). Unknown names can be tested against conventional WAD paths but are **not guaranteed** to have a discoverable image.
- Riot patch freshness: a new game version refreshes official `skins.json`/skinline/item caches and invalidates affected in-memory lookups at startup or before Scan Folders/splash search. Custom rules and settings are preserved.
- The WAD reader computes xxHash64 locally. External CommunityDragon `hashes.game.txt`/`hashes.lcu.txt` synchronization and its Settings panel **were removed**; previously downloaded user catalogs are not deleted. A hash alone cannot reveal an unknown original filename.
- Canonical skinline matching across punctuation variants such as `KDA`, `K D A`, and `K/DA`.
- **Official thematic families:** Riot's local `skinlines.json` separates `Arcade: Heroes` and `Arcade: Battle Bosses`. A folder target `Arcade` now recognizes both official sub-lines (18 retail skins at validation time), while `ArcadeHeroes` and `ArcadeBattleBosses` keep their specific line. `EternalAspects` continues to resolve `The Eternal Aspects`. These map to `skinline` in Event Mapping/Quick Edit, then try actual member skin paths in local WADs.
- **New champion-qualified skinline VO patterns:** `FirstEncounter3DEkkoSkinlineTrueDamage` and corresponding Kayn/Qiyana/Senna/Yasuo interactions now keep both the champion and its official thematic line. `Hearsteel` normalizes to Riot's `HEARTSTEEL`; `TrueDamageX` can fall back to `True Damage` when no distinct official line exists. The icon reader **filters the theme's members by champion**, rather than guessing a nonexistent `EkkoTrueDamage` champion or selecting somebody else's skin. A retail-WAD smoke test extracted PNG tiles for all six reported cases.
- **Legacy rule icon audit (October 19):** of 71 saved event rules, 45 use `generic` (no icon requested); the 16 fixed-icon rules were rechecked against the installed WADs. **14/16 currently resolve**, including all existing ping rules after adding local All In/Bait/Hold/Push/Retreat textures. The two remaining entries are `Shop` and `ShopOpen`, both configured as `system:Gold`: a verified shop-currency icon was not identified, and unrelated gold-coloured graphics are intentionally rejected. All 10 target-driven rule families have representative parser/category tests, but actual icon availability depends on the individual target.
- Live 1920x1080 preview with quick editing, keyboard navigation, cached rendering, and cancellable background work.
- Background Design Studio with custom art, typography, icon alignment, dialogue bubble controls, brightness, contrast, and saturation.
- Deterministic FFmpeg render progress covering images, silence tracks, temporary clips, audio joins, encoding, and final concatenation.
- Quiet batch console with detailed diagnostics retained in local log files.
- Ambiguous targeted attack events remain pending manual mapping instead of being incorrectly labeled as monsters.
- **Less duplicated classification:** a shared `LeagueMonsterAssets` catalog supplies official monster aliases and their verified WAD icon paths, while `LeagueSkinlineIdentity` handles skinline aliases/families consistently in the parser, skin catalog and icon resolver. `EventNameParser` invokes the rule matching engine only once. Unknown custom monsters still use their local Mapping list and checked HUD path conventions.
- Workspace shortcuts (Ctrl+1–5), event search (Ctrl+F), and collapsible sidebar (Ctrl+B).
- Settings has three tabs: **General & Media** (game installation, media source, dictionary language), **Processing** (silence, audio-family merging), and **Transcription** (Whisper). Browsers, toggles, path inputs and hero headers share a midnight-navy/cyan/violet visual system. The game directory is **never filled with an automatic default**; an existing user-saved path is preserved.
- Reusable WPF styles for buttons, tabs, ComboBoxes, sliders, input focus and sidebar. The original six-card mockup is historical reference, not the current Settings layout.

## Requirements

- Windows x64.
- .NET 10 SDK for source builds.
- A locally installed League of Legends client, with its Game and Plugins WAD files, to obtain new game images and catalogs. Existing cached images and editable rules are preserved.
- A sibling `LeagueToolkit-4.1.0-beta.53` source checkout for development builds, as referenced by `VideoGenerator/VideoGenerator.csproj`.
- Optional internet access may be required to download a Whisper model. **Game icon/splash/catalog extraction and hash resolution are local**, with no remote image or hash-dictionary downloads.

FFmpeg binaries are embedded and extracted automatically when required. Whisper transcription runs locally after its model has been downloaded.

## Build and run

From the repository root (with the sibling LeagueToolkit checkout available):

```powershell
dotnet restore VideoGenerator/VideoGenerator.csproj
dotnet build VideoGenerator/VideoGenerator.csproj -c Release
dotnet run --project VideoGenerator/VideoGenerator.csproj -c Release
```

## Developer validation

Run deterministic tests with xUnit:

```powershell
dotnet test VideoGenerator.Tests/VideoGenerator.Tests.csproj -c Release
```

Regular xUnit tests cover parser, Mapping, rendering, cache refresh, event/icon resolution, and synthetic WAD fixtures without downloading game art. Optional retail-WAD smoke tests and separate WPF render harnesses are **not** substitutes for a live 100/125/150% DPI review. There are outstanding ImageSharp NuGet advisories to resolve before publishing.

## Configuration files

The application creates and maintains its editable configuration under `%LOCALAPPDATA%/VideoGenerator/Config/`, including:

- `settings.json`
- `event_rules.json`
- `translations.json`
- `dialogues.json`
- `groups.json`
- `champion_aliases.json`
- `skinlines.json`
- `monsters.json` and `structures.json`

**Mapping persistence:** `RuleManager.LoadRules()` now preserves saved custom settings even when their keyword coincides with an official rule, appends newly introduced default keywords, and leaves invalid user files intact rather than replacing them. **Migration caveat:** existing saved official-keyword entries cannot yet be distinguished from user-edited overrides, so updates to an already-saved official keyword are not auto-applied. A provenance-aware merge is a future migration task.

Historical investigation documents describe earlier CDN/Wiki behavior. They are not the active asset architecture: the live application uses installed Riot WADs and cached official metadata.
