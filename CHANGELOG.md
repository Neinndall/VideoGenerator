VideoGenerator - Patch Notes | v1.2.5.0

MEDIUM UPDATE
This version introduces a real-time event pipeline search bar, reorders event parser execution priorities to fix localization hijackings, cleans up Spanish translation templates, adds explicit mapping overrides for generic item/ward names, and solves multiple rule synchronization and icon category bugs.

New Features
  - UI / Live Pipeline Search Bar: Integrated a real-time search box into the Dashboard header (adjacent to the Character Filter). Features a custom purple magnifying glass icon and filters the Detected Pipeline list on the fly as the user types, matching against both folder names and parsed display texts.
  - Core / Quest & Tiered Skins Support (e.g., Hall of Legends): Added dynamic flattening of nested quest skin tiers in `DataFetcher`'s skins deserialization logic. This exposes nested tiered skins (such as Skin 86 - Immortalized Legend Ahri) as top-level skins in the application database, allowing full resolution of their localized names and centered tile icon assets.
  - UI / Role Group Category Support: Added a new `"Role"` category to `DefaultGroups.cs`, pre-configuring standard champion rosters for `ADC`, `APC`, `Support`, and `Jungle` roles. Added `"Role"` as a selectable option in the Category dropdown within `EventRulesView.xaml` to allow visual management of role-based groups.

Improvements & Visual Polish
  - Translation / Clean Spanish Templates: Cleaned up the Spanish translation strings for `event_use_item` and `event_buy_item` in `translations.json` to remove the redundant word "objeto", resulting in cleaner HUD outputs like "Usar Guardián invisible" or "Comprar Filo Infinito". Added an automatic migration routine in `TranslationService` to sync this change to the user's local AppData config.

Bug Fixes & Refinements
  - Core / Parser Execution Reordering: Reordered event parser registration in `NameParser` to evaluate specialized parsers (like `ItemEventParser`, `MonsterEventParser`, and `SkinInteractionParser`) before the catch-all `DynamicRuleParser`. This prevents generic rules from hijacking and displaying un-localized item names (e.g., "Ward") instead of their database-resolved names (e.g., "Guardián invisible").
  - Core / Item Map Language Unification: Forced `GetCommunityItemNameToIdMapAsync` in `DataFetcher` to construct its name-to-id cache using the English ("EN") items catalog, ensuring that English keywords parsed from folder names successfully resolve to their official IDs even when the user runs the app in Spanish/Turkish.
  - Core / Explicit Ward Translation Mapping: Added an explicit mapping override for the generic `"Ward"` keyword to the official Warding Totem ID `3340` in `DataFetcher` to guarantee correct resolution and download of the modern yellow ward icon.
  - Core / General Item Event Support: Added special handling to `ItemEventParser` to catch generic item folders like `BuyItem2DGeneral` and `UseItem2DGeneral`, correctly mapping them as `"generic"` events with a `"Generic"` icon and `"Comprar en General"` HUD text instead of failing with missing icon errors.
  - Core / Structure Icon & Rule Synchronization: Added `IconLookup = "Turret"` to the `KillTurret` rule in `DefaultRules.cs` and fixed the merge logic in `RuleManager` to correctly sync `IconLookup` and `Section` properties from base code to the local `event_rules.json` on disk.
  - Core / Structure Category Override Fix: Prevented `DynamicRuleParser` from accidentally overriding `"structure"` icon types to `"generic"` for general events, restoring the turret structure icon for turret takedowns.
  - Core / Role Acronyms & Spanish Contractions: Prevented acronyms (like "ADC" or "APC") from being split into spaced letters ("A D C"). Added support for target translations (`target_adc`, `target_apc`, `target_support`, `target_jungle`) in `translations.json` and integrated a dynamic Spanish helper in the parser to rewrite "Matar a" as "Matar al" for game roles, producing clean HUD outputs like "Matar al ADC" or "Matar al Jungla".
  - Core / Monster Steal Event Rules: Added dedicated rules for `KillBaronSteal`, `KillDragonSteal`, and `KillElderSteal` in `DefaultRules.cs` with their own translation keys and monster icon lookups. Previously, the generic `Kill` rule extracted "BaronSteal" as a target name, producing incorrect text like "Matar a Baron Steal". Now displays correctly as "Robo de Barón", "Robo de Dragón", and "Robo de Dragón Anciano" in Spanish.
  - Core / Explicit Rule Icon Type Preservation: Fixed `DynamicRuleParser` CASE B logic to trust rules that already define a specific `IconType` (`monster`, `structure`, `system`) with an `IconLookup`, skipping the heuristic reclassification cascade that would incorrectly downgrade them to `"generic"`. Also added 2D/3D infix stripping for Simple rule matching so keywords like `KillBaronSteal` correctly match folders like `Kill3DBaronSteal`.
  - Core / Multikill Event Rules: Added dedicated Simple rules for `KillDouble`, `KillTriple`, and `KillQuadra` in `DefaultRules.cs` and translated them correctly across all locales ("Doble Asesinato", "Triple Asesinato", etc.). Previously, `KillDouble` was processed as a generic kill action targeting "Double", resulting in "Matar a Double".
  - Core / Move Proximity Event Rules: Added dedicated rules for `MoveAllyNear` and `MoveEnemyNear` to successfully translate movements near allies and enemies. Previously, folders like `Play_vo_MissFortuneSkin69_Move2DAllyNear` did not match any rule and fell back to raw folder names.
  - UI / Searcher Layout Enhancements: Added a clear button ("X") on the right side of the search box that only appears when there is text, allowing users to quickly clear the filter.
  - Core / Skin Interaction Event Detection: Fixed `SkinInteractionParser` to use the cleaned `workingFolder` (with `2D`/`3D` stripped) instead of the raw `folderName` when detecting event types. This ensures `Move2DFirst` correctly matches `MoveFirst`. Added `"MoveFirst"` to the prefix stripping list. Changed `interaction_move_first_target` translations from "towards"/"hacia" to "with"/"con" across EN/ES, and added an automatic migration rule in `TranslationService` to update cached local copies.
  - Core / Unified Respawn Event Rules: Restored `"Respawn"` rule in `DefaultRules.cs` as a single Simple rule. Renamed `event_respawn` Spanish translation to "Reaparecer" for natural phrasing ("Reaparecer en General"), integrated a "Reaspawn" typo fallback in normalization, and implemented automatic local database cleanup for obsolete duplicates.
  - Core / Game End Event Rules: Added dedicated rules for `GameEndDefeat` and `GameEndVictory` in `DefaultRules.cs` to handle victory and defeat system events, along with their translations across all locales ("Bozgun", "Zafer", "Derrota", "Victoria", etc.).
  - Core / Level Up Event Rule: Added dedicated `"LevelUp"` Simple rule categorized under `"ABILITIES"` section. Translated as "Subir de Nivel", "Level Up", and "Seviye Atlama" across ES/EN/TR.
  - Core / First Blood Team Event Rules: Added dedicated `"KillFirstAllyTeam"` and `"KillFirstEnemyTeam"` rules under `"COMBAT"` to translate first blood occurrences for teams, preventing them from falling back to generic kill rules with literal target names (e.g. "Matar a First Ally Team").
  - Core / Professional Players Unique Emotes: Added dynamic `"Unique"` target rule under `"OTHER"` section. Implemented post-extraction cleanup in `DynamicRuleParser` to strip any trailing "Emote" suffix (e.g. `_Unique3DFakerEmote` -> extracts "Faker" as target), resulting in clean translations like "Gesto Único de Faker" or "Unique Faker Emote".
  - Core / Character Unique Transformations: Added dedicated rules for `"UniqueTransformAhead"`, `"UniqueTransformBehinf"`, and `"UniqueTransformGeneral"` (robustly handling Riot's "Behinf" spelling typo) under `"OTHER"` section, along with complete localizations across all supported languages.
  - UI / Visual Design Settings: Added new "Background Brightness", "Background Contrast", and "Background Saturation" settings under Visual Design. Users can adjust these background parameters dynamically via new sliders (rango 0.0 - 2.0) with real-time viewport updates and configuration persistence.


>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>

VideoGenerator - Patch Notes | v1.2.4.3

MINOR UPDATE
This version optimizes champion skin icons by shifting from full widescreen splash arts to centered character tiles for the HUD rendering process. It also resolves case-sensitive CDN download issues for champions with special characters, introduces dynamic database localization for ES/TR/EN locales, and integrates a Community Dragon fallback channel for PBE/custom skin assets.

New Features
  - Core / Localized Databases Support (ES, TR, EN): Configured the Community Dragon data URLs and cache paths in `AppConfig` to dynamically adapt based on the selected `DefaultDictionaryLanguage` setting (mapping "ES" to "es_es", "TR" to "tr_tr", and others to "default"). Language-specific cached files are stored separately (e.g., `skins_data_tr_tr.json`) to prevent cross-language cache collisions, allowing translation lookups for skins, skinlines, and items in the user's preferred language.

Improvements & Visual Polish
  - Asset Pipeline / Centered Character Tiles: Switched the champion skin image downloader in `IconManager` to fetch from League of Legends DDragon `tiles` CDN rather than `splash` assets. This guarantees that generated HUD circle icons are centered on the champion's face/body automatically, improving visual rendering quality.
  - Core / Community Dragon Fallback: Added a fallback download channel in `IconManager.GetTileUrlAsync` to query the Community Dragon skins database for skin `tilePath` properties and download the correct, centered square tile if DDragon fails, including automatic lowercase conversion and path normalization for case-sensitive CDragon requests.
  - UI / Dialogue Editor Focus Preservation: When opening the dialogue editor (Review Dialogues) from the dashboard, the editor now automatically focuses and scrolls to the event that was currently selected in the dashboard's pipeline list, eliminating the need to search for it manually.
  - UI / Dialogue Editor Champion Icon Border: Applied a fixed, elegant Hextech Gold border (`HextechGoldBrush`, #C89B3C) with a subtle 1.5px thickness to the resolved champion/event icon in the Dialogue Editor header.

Bug Fixes & Refinements
  - Core / PBE Data Support: Changed `SkinsDataUrl`, `SkinLinesUrl`, and `ItemsDataUrl` endpoints in `AppConfig` from `latest` to `pbe`, allowing the application to successfully load upcoming/unreleased champion skins (e.g., T1 Yunara) for audio and video processing.
  - Core / Case Normalization for Champion Aliases: Updated `AliasManager.GetInternalName` to clean names of apostrophes, hyphens, and dots, ensuring that names parsed from event folders (e.g. "KaiSa") successfully map to official internal representations (e.g. "Kaisa") in `DefaultAliases.cs` to prevent case-sensitive DDragon CDN URL download failures.
  - Core / Default Aliases Expansion: Added default aliases mapping for `Kai'Sa` -> `Kaisa`, `Cho'Gath` -> `Chogath`, `Kog'Maw` -> `KogMaw`, `Nunu & Willump` -> `Nunu`, and `Renata Glasc` -> `Renata`.
  - Audio / Dialogue Editor Playback State: Hooked into the `MediaPlayer.MediaEnded` event to automatically reset the segment playback button icon back to the Play state (`>`) when the track finishes playing, instead of remaining stuck as a Stop square.

>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>


VideoGenerator - Patch Notes | v1.2.4.2

MINOR UPDATE
This version introduces a redesigned global status footer with real-time progress tracking and task cancellation, adds the Whisper "medium" model to the transcription engine settings, unifies input border styling, and applies extensive layout and visual polish across all views.

New Features
  - UI / Global Footer Status Bar: Relocated the engine status panel from the sidebar to a full-width footer bar at the bottom of the window. Displays a redesigned layout with a left-aligned grouped status indicator, a centered auto-stretching progress bar, a static sync icon during active operations (optimized to avoid GPU overhead), and a compact CANCEL (ESC) button visible only when processing.
  - UI / Task Cancellation System: Any running batch operation (folder analysis, transcription, HUD rendering, video compilation) is now fully cancellable. Press the `Escape` key or click the Cancel button to abort immediately. Cancelled operations display "CANCELED - TASK ANNULLED" in orange warning state (`#F97316`).
  - Core / Centralized Task Cancellation Service: Centralized all cancellation tokens and tracking within a new DI-registered `TaskCancellationService` class, decoupling CancellationTokenSource lifecycles from view code-behinds.
  - Settings / Whisper Medium Model: Added the "medium" model size to the Whisper Speech-to-Text model selector (tiny, base, small, medium). The medium model offers significantly higher transcription accuracy at the cost of a larger download and increased processing time.

Improvements & Visual Polish
  - UI / Sidebar Navigation Polish: 
    * Increased lateral margin of navigation items to `10px` to create a more inset, centered, and clean sidebar layout.
    * Height of the selected accent line indicator increased to `20px` and margin adjusted to `6px` to align perfectly with the border radius.
    * Changed selected item foreground to `AccentBrushLight` (#A78BFA) for a softer, more luminous purple glow, which transitions to a glowing pastel lilac (#E9D5FF) on hover instead of turning white.
    * Optimized status bar performance & layout: Redesigned the footer layout to group the status icon and label together on the left inside a fixed-width column (`340px`) to prevent any layout shifting when the status text changes length. The progress bar stretches dynamically in the center, and the progress percentage and cancel controls align on the right. Removed the infinite spinning `DoubleAnimation` on the sync icon to eliminate continuous GPU redraws, dramatically lowering GPU usage during operations. Font weight adjusted to a cleaner Bold, and text color tuned for maximum professional readability.
  - UI / Hover Accent Gradients: Updated the `ModernSecondaryButton` style so that hovering over secondary buttons (like "Prepare Dialogues" and "Review") smoothly transitions the background to the modern violet accent gradient (from `AccentColorLight` to `AccentColor`) and makes the text white, matching primary action highlights.
  - UI / Centralized Input Borders: Unified all text box (`ModernTextBox`, `ModernTextBoxWithClear`, `ModernSearchTextBox`) and combo box (`ModernComboBoxStyle`) borders to a consistent `1.5px` default and active focus thickness, configured directly within the global theme styles.
  - UI / Sidebar Refinements: Streamlined sidebar layout with refined logo size, reduced title text (15px) and version label (10px), wider navigation selector with minimal padding (4px), and tighter vertical item spacing (2px gap) for a more compact look.
  - UI / Dashboard Layout Polish: Slimmed down the configuration bar (Media Source Directory + Lang + Character filter) with reduced padding and element sizes. PROCESS FOLDERS button now auto-stretches vertically to match the config bar height. Engine Console logger expands dynamically to fill remaining vertical space with symmetric 12px spacing above and below.
  - UI / Event Mapping Headers: Separated RULES REPOSITORY title header (SurfaceBrush background) from column headers (KEYWORD, DICT KEY, ICON/LOOKUP, TYPE) which now sit on the dark background without icons, creating a cleaner visual hierarchy.
  - UI / Dictionary Headers: Applied the same header pattern — TRANSLATIONS DICTIONARY title on SurfaceBrush, column labels (LANGUAGE, EVENT KEY, DISPLAY TEXT) on dark background without icons, with corrected alignment margins to match data rows.
  - UI / View Margins & Alignment: Unified all view margins to align the rightmost edge of content with the status bar. Eliminated dead spacing between the logger and status bar. Symmetric outer margins throughout the shell.
  - UI / Dialogue Action Buttons: Restyled PREPARE DIALOGUES and REVIEW buttons to use the subtle `ModernSecondaryButton` style with `SurfaceBrush` background, reserving the vibrant purple gradient exclusively for the final RENDER VIDEOS action.
  - UI / Engine Console Corners: Fixed the bottom rounded corners of the log viewer with a custom RichTextBox template (`CornerRadius: 0,0,11,11`) that aligns perfectly inside the outer ModernContainer.
  - UI / List Styling: Removed zebra striping from non-selectable list items. Rows now use a uniform dark background with subtle hover highlights.
  - Core / Dynamic Progress Reporting: Refactored progress calculations to pre-count audio files and update linearly (e.g., "Transcribing 3/12 (filename.ogg)") instead of hardcoded percentage steps.

>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>

VideoGenerator - Patch Notes | v1.2.4.1

MINOR UPDATE
This version introduces an automatic audio padding filter during Speech-to-Text conversion to prevent Whisper from skipping short audio files and optimizes transcription speed via model factory caching and user-configurable CPU thread allocation.

Improvements
  - Performance / Whisper Model Factory Caching: Implemented caching for the loaded `WhisperFactory` instance. The Whisper model is now loaded from disk into memory exactly once per batch instead of re-reading and reconstructing the 150MB+ neural network weights for every single audio track. This dramatically decreases CPU overhead and disk I/O, resulting in massive speed gains during batch transcription.
  - Settings / Whisper Thread Count Selector: Added a custom slider under the Transcription Engine settings tab, allowing the user to dynamically adjust the number of CPU threads (from 1 up to their PC's maximum logical core count) utilized for Whisper speech-to-text inference. Defaults dynamically to half of the available cores to optimize performance while preserving system responsiveness.

Bug Fixes & Refinements
  - Audio / Short Audio Transcription Padding: Configured the audio conversion process to pad any short audio files (less than 3 seconds) with silence at the end using the FFmpeg `apad=whole_dur=3` filter. This ensures Whisper has enough duration to transcribe short audio fragments successfully instead of returning empty results.

>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>

VideoGenerator - Patch Notes | v1.2.4.0

MEDIUM UPDATE
This version introduces a new Dual-Phase Batch Rendering pipeline and a dedicated Dialogue Segment Editor Dialog to offer absolute, granular control over every individual audio segment and subtitle transcription.

New Features
  - UI / Dialogue Segment Editor Window: A premium HUD-styled modal dialog that displays a left pane with the event queue and a right pane showing every individual audio segment (part) of the selected event. Users can play individual audio files and edit their transcripts in independent text boxes, bypassing manual "||" separators.
  - UI / Validation Status: Added validation checkboxes within the editor to mark event dialogue segments as verified, turning on a checkmark icon in the Dashboard pipeline list.

Improvements
  - Core / Dual-Phase Pipeline Split: Divided the batch process into two distinct stages:
    * Step 1: "Prepare Dialogues" resolves icons, downloads files, runs Whisper, cleans ambient noise tags, and pre-renders HUD frame PNGs.
    * Step 2: "Render Videos" compiles the final `.mp4` using FFmpeg in seconds, utilizing the verified dialogues and pre-rendered frames.
  - UI / Dashboard Button layout: Updated the action panel with three dedicated buttons (1. Prepare Dialogues, Review, 2. Render Videos).

Bug Fixes & Refinements
  - Audio / Dialogue Playback Engine: Added on-the-fly conversion of `.ogg` audio files to temporary `.wav` files via FFmpeg inside the Dialogue Editor Window to enable native playback of voiceover files using WPF's MediaPlayer.
  - UI / Dialogue Editor XAML Fixes: Resolved crash on window startup by correcting GridSplitter placement outside the ColumnDefinitions collection and swapping the invalid `MaterialDesignAccentCheckBox` style for the standard `MaterialDesignCheckBox`.
  - UI / Dashboard Layout Responsiveness: Rearranged the Quick Edit inspector card into 3 vertical rows to prevent text and button clipping at smaller window widths (<=1200px). Added text wrapping to final action buttons and resolved horizontal text ellipsis cropping on pipeline list items.
  - Icons / Danger Ping Wiki Resolution: Configured `IconManager` to map the `"Danger"` ping to the updated `"Retreat ping.png"` filename to match the League of Legends Wiki naming convention, resolving download failures.

>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>

VideoGenerator - Patch Notes | v1.2.3.1

MINOR UPDATE
This version adds segment-by-segment dialogue video rendering for multi-audio events, automated output grouping by champion, automated dialogue cache cleanup and updates, a toggle to force Whisper re-transcription during Batch Rendering, and reorganizes the settings panel into sections using a tabbed layout.

New Features
  - Audio / Segmented Multi-Image Rendering: When an event has multiple audios, joining segment transcriptions with `" || "` automatically splits them into separate images during Batch Rendering. Video clips are generated for each audio-dialogue pair and concatenated losslessly, displaying dynamic subtitles changing precisely per audio file.
  - Settings / Force Batch Re-transcription: Added a toggle switch in settings to bypass existing cached transcriptions in `dialogues.json` during Batch Rendering and force Whisper to re-transcribe all audio files.
  - UI / Tabbed Settings Panel: Organized the settings panel into tabs ("General & Media" and "Transcription Engine") using the premium `LoreEngineTabControl` and `LoreEngineTabItem` styles for better usability.

Improvements
  - Core / Champion Folder Output Organization: Reorganized Batch Render outputs to save generated images (in `Generated/Images/<ChampionName>/`) and videos (in `Generated/Media/<ChampionName>/`) inside subdirectories named after the respective champion, preventing assets from getting mixed together.
  - Core / Automated Dialogue Cache Cleanup: Dialogue text loaded from `dialogues.json` is now automatically post-processed to remove bracketed noise tags (hallucinations) on-the-fly when ambient tag cleaning is enabled. The cleaned text is automatically written back to the dialogues.json cache during both folder analysis and batch rendering, correcting old cache entries permanently.
  - UI / Real-Time Progress and Status: Added real-time progress calculation to the Batch Rendering process. Includes micro-step reporting (10% start, 30% transcribe, 55% image render, 80% video build) and decimal formatting (`F1` percentage) to show continuous visual progress.
  - UI / Auto-Selection and Tab Sync: Dashboard now automatically selects the first event when applying character/status filters and hooks into the WPF `Loaded` event to instantly force-refresh the live preview and custom background when switching tabs.

Bug Fixes
  - Icons / Ascended Darkin Resolution: Added the `"Ascended Darkin"` region group to `DefaultGroups.cs` to prevent the app from failing to resolve icon paths, automatically mapping it to the official `"Darkin_icon.png"` asset crest.

>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>

VideoGenerator - Patch Notes | v1.2.3.0

MEDIUM UPDATE
This version adds user-configurable options for the Whisper Speech-to-Text model size, target audio language, advanced dialogue bubble layout/border customization, and automatic cleanup of Whisper transcription ambient tags.

New Features
  - Settings / Whisper Model Selector: Added UI ComboBox under Settings to select Whisper model size ("tiny", "base", "small"). The default model is upgraded to "base" for higher out-of-the-box accuracy.
  - Settings / Whisper Language Selector: Added UI ComboBox to force Whisper transcriptions into a specific target language (e.g. Turkish, Spanish, English, etc.) rather than relying on automatic detection, resolving wrong language detection issues.
  - Design Studio / Dialogue Bubble Customization: Added real-time sliders in Background Design to adjust Dialogue Bubble Width (500px to 1400px) and Horizontal Offset (-300px to 300px).
  - Design Studio / Customizable Border Styling: Added inputs to configure custom Border Colors and Border Thicknesses (0 to 5px range) for both HUD Icon and Dialogue Speech Bubbles.
  - UI / Color Dropdown Presets: Added Dropdown (ComboBox) selectors containing only "Hextech Gold" and "White" options for a unified aesthetic.
  - UI / Design Studio Descriptions: Updated the descriptions in Background Design to explicitly state that only Gold and White borders are supported.
  - Audio / Transcription Hallucination Cleanup: Added post-processing cleanup in `TranscriptionService` to strip Whisper-generated non-speech tags in square brackets (e.g. `[BLANK_AUDIO]`, `[MUSIC]`, etc.) arising from silence or background tracks.
  - Settings / Clean Ambient Audio Tags Toggle: Added a ToggleSwitch under the transcription settings panel to enable/disable the automatic removal of non-speech bracketed tags.

Improvements
  - Core / Dynamic Whisper Download: Modified `TranscriptionService` to dynamically construct URLs and paths, automatically downloading the user-selected model from Hugging Face on demand.
  - UI / Conditional Enablement: Whisper Model and Language settings are reactively disabled if Speech-to-Text transcriptions are toggled OFF.
  - UI / Settings ScrollViewer: Added a ScrollViewer specifically around the Settings panel to handle overflow cleanly without breaking the Dashboard viewport layout.
  - UI / Real-Time Dashboard Sync: Updated the Dashboard preview to listen to all speech bubble customization properties (text size, height, opacity, offsets, width, colors, thicknesses) for immediate visual updates.
  - UI / Test Transcribe Button: Renamed the confusing "AUTO-TRANSCRIBE" button in the Quick Edit panel to "TEST TRANSCRIBE" and updated its tooltip to clarify that it transcribes the selected event for testing and editing.
  - UI / Bubble Vertical Offset Limits: Adjusted the BUBBLE VERTICAL OFFSET slider range in Background Design (now -350px to 0px) to allow raising the bubble higher on screen while removing positive offsets (moving it lower).
  - UI / Bubble Height Limits: Increased the BUBBLE HEIGHT slider maximum limit in Background Design from 240px to 400px to accommodate taller custom text boxes.
  - Core / Smart Bubble Positioning: Implemented collision prevention in `ImageGenerator` so that wider dialogue bubbles expand inward, avoiding overlapping the champion icon when right-aligned.
  - Core / Border Rendering Engine: Refactored `ImageGenerator.cs` to dynamically parse and draw custom border colors and thicknesses on dialogue bubbles, triangle pointer tails, and framed champion/item icons.
  - UI / XML Syntax Clean Up: Cleaned up duplicate nested `Grid.RowDefinitions` elements in `BackgroundDesignView.xaml` preventing layout compile errors.
  - Core / Color Normalization Clean Up: Cleaned up unused color switch cases in `AppSettings.cs` to strictly align with the Gold/White border policy.

>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>

VideoGenerator - Patch Notes | v1.2.2.0

MEDIUM UPDATE
This version focuses on parser robustness, icon resolution reliability, and analysis performance with new big stuff.

New Features
  - Core / Skinline Manager: Introduced `SkinlineManager` to load and cache official skinlines dynamically from CommunityDragon, keeping regions/classes separate from thematic skin collections.
  - Audio / Speech-to-Text Transcription (Whisper): Integrated fully offline Whisper speech-to-text to automatically transcribe champion voice lines. Includes a global `EnableTranscriptions` toggle in Settings that, when turned OFF, reactively disables the dialogue edit textboxes and Auto-Transcribe buttons in the Dashboard to prevent accidental edits.
  - Subtitles / Hextech Dialogue Bubble: Renders a beautifully styled Hextech speech bubble overlay on HUD designs and final videos. Auto-scales and centers when no icon is present, with customizable layout alignment. If transcription settings are OFF, the bubbles are automatically hidden on both the live preview canvas and final video renders.
  - Subtitles / Dialogues Database: Decoupled transcriptions from UI localization strings into a dedicated `dialogues.json` database. Includes legacy data migration and instantaneous auto-saving.
  - Dashboard / Non-Obstructive Preview Maximization: Clicking on the live preview canvas in the Dashboard now toggles the visibility (Width) of the event pipeline sidebar and grid splitter, expanding the preview canvas to maximum scale within the viewport for a larger view without obstructing editing flows, mirroring the Background Design maximize mechanism.
  - Design Studio / Dialogue Speech Bubble Customization: Added a new section inside the Background Design tab to customize the subtitle speech bubble's text size, container height, background opacity (with 5% stepping steps: 0.1, 0.15, 0.2, etc.), and vertical offsets, with instant real-time live preview rendering and slider snapping.
  - Design Studio / Non-Obstructive Preview Maximization: Clicking on the live preview canvas in the Background Design studio now toggles the visibility (Width) of the inspector sidebar column, expanding the preview canvas to maximum scale within the tab viewport for a larger cinematic view without obstructing editing flows.
  - Mapping Engine / Custom Icon Lookup in Event Rules: Added an optional `IconLookup` string property to the `EventRule` model, exposed via a new "Icon Lookup (Optional)" TextBox in the Event Rules registration panel, and integrated it into the parsing logic to allow overriding default icons with custom asset lookup names.

Improvements
  - UI / Simplified Form Labels: Renamed Quick Edit and Event Rules fields for clarity ("Text shown in HUD", "Icon name or ID", "Icon category", "Behavior category", "Rule type").
  - UI / Event Category Tooltips: Added descriptive tooltips to every behavior category in the Event Rules dropdown so users know what each category means.
  - UI / Auto-Detect Event Category: Event Rules now automatically suggests a behavior category (COMBAT, EMOTES, MOVEMENT, ITEMS, PINGS, ABILITIES, INTERACTIONS, SYSTEM, OTHER) based on the folder keyword while typing.
  - Performance / Deferred Icon Resolution: Folder analysis no longer blocks on icon downloads. Icons are resolved concurrently in the background after the folder scan completes, drastically improving perceived analysis speed.
  - Icons / Skinline Icons Use Thematic Skins: `SkinlineManager` now stores the specific skin ID for each champion in a skinline. Events like `Kill3DAnimaSquad` now display the champion wearing the actual Anima Squad skin instead of the base splash art.
  - Core / Centralized Data Synchronization: Moved all CommunityDragon downloads (skins, skinlines, items) into `DatabaseBuilder`, leaving `DataFetcher` as a pure cache reader. Both services now share a single `HttpClient` instance.
  - Core / CommunityDragon Item Cache: Item name-to-ID resolution now uses a locally cached `items_data.json` from CommunityDragon (with `If-Modified-Since` refresh) before falling back to the DDragon database, reducing API calls and improving item lookup reliability.
  - Rules / DeathHuman: Added official `DeathHuman` rule to `DefaultRules.cs` with `event_death_human` translation key (EN: "Death (Human)", ES: "Muerte (Humano)", TR: "Ölüm (İnsan)") so all users automatically receive it on update via the non-destructive merge strategy.
  - Performance / UI thread responsiveness: Moved CPU-intensive ImageSharp rendering operations to thread pool tasks using `Task.Run` to prevent freezing the main WPF UI thread.
  - Performance / CancellationToken Debouncing: Added event preview request cancellation tokens (50ms debounce) to discard obsolete image rendering requests immediately during rapid selection changes.
  - Performance / Image Rendering RAM Pipeline: Rewrote the preview renderer to load image bytes directly into a frozen WPF `BitmapImage` instead of executing heavy disk writes/reads.
  - Performance / Memory Asset Cache: Implemented local dictionary caches for custom backgrounds, fonts, and cropped icons inside `ImageGenerator` to avoid repetitive file reads.
  - Performance / LoL Version Caching: Cached LoL version queries in memory to avoid repetitive API requests.
  - Parser / Dynamic Translation Lookup for Spells: Integrated TranslationService in SpellOrAttackParser to prioritize resolving translated text for spell hit/cast events (e.g. event_EMissile_hit3D) from translations.json before falling back to automatic English title formatting.
  - Core / Raw Unicode JSON Serialization: Configured TranslationService and Dictionary View saving to write unescaped Unicode characters (á, í, ö, ü, ş, ç) directly to translations.json instead of converting them to hexadecimal escape sequences (\u00E1, \u00ED, etc.), ensuring translations are 100% readable and human-editable.

Bug Fixes
  - Parser / Prefixed General Suffix Resolution: Fixed `DynamicRuleParser` to match Simple rules when the folder has an extra prefix before the keyword (e.g. `Dragon_JokeGeneral`), as long as the keyword appears as a bounded word and the folder ends with "General"/"inGeneral". The regex lookahead now accepts `(?=_|$|General|inGeneral)` to handle cases where "General" is directly appended without an underscore separator. Additionally, the matched prefix (e.g. "Dragon", "MegaGnar") is now automatically prepended to the display text, producing `"Dragon: Joke in General"` instead of just `"Joke in General"`. Folders without a prefix (e.g. `Joke3DGeneral`) remain unchanged.
  - Icons / Generic Dragon Target: Fixed `Attack2DDragon` resolving to `Elder Dragon`; generic "Dragon" / "Drake" targets now keep their lookup name so `IconManager` downloads the generic `DragonSquare.png` asset from Fandom instead of the late-game epic objective.
  - UI / Engine Status Progress Bar: Fixed progress reporting so the bar reaches 100% smoothly and remains visible briefly before returning to idle.
  - UI / Event Rules Column Alignment: Fixed vertical misalignment of ICON / LOOKUP column items (now horizontal inline) so all columns share the same baseline.
  - UI / Dragon Icon Hardcode Removed: Replaced hardcoded elemental drake preference order in `IconManager.ResolveMonsterName` with the generic `DragonSquare.png` asset from Fandom Wiki.
  - Icons / Sticky Resolution Block: Assigned `"MISSING"` flags to unresolved icons to prevent recurring lookup retries and background network requests on subsequent element clicks.
  - UI / White Line Scaling Artifact Removal: Added an edge-masking operation to `CreateImageBytesAsync` in `ImageGenerator.cs` that draws a 2px inner black border on the canvas bounds, and updated WPF `Image` controls to use `Stretch="Uniform"`, `UseLayoutRounding="True"`, and `SnapsToDevicePixels="True"` to eliminate thin white border lines appearing when maximized.
  - Design Studio / Default Background Ribbon Alignment: Rebuilt and aligned `DefaultBackground.png` to position its pre-designed white ribbon borders at Y=898 and Y=1018, matching the code's layout coordinates and correcting the text/design vertical offset misalignment when no custom splash art is loaded.
  - Icons / Monster Icon Resolution: Fixed the Fandom Wiki filename mapping for Murk Wolf to use "Greater_Murk_Wolf" (matching the wiki's `Greater_Murk_WolfSquare.png`) instead of the incorrect "Greater_Murkwolf". Also added mapping for the generic "Drake" keyword to resolve to "Dragon" (mapping to `DragonSquare.png`), and added automatic mapping for generic `EpicMonster` / `Epic_Monster` targets to resolve to `Baron_NashorSquare.png`.
  - Icons / Region Emblem Crest Resolution: Re-routed regional thematic groups (like Void, Demacia, Noxus, Shurima) in IconManager to automatically resolve to their official wiki crest file name (e.g. `Void_Crest_icon.png`) instead of selecting a random champion from the group.
  - Localization / JSON Unescaping for HTML-sensitive Characters: Swapped the standard Unicode JavaScriptEncoder for `UnsafeRelaxedJsonEscaping` in both TranslationService and TranslationsView to ensure apostrophes (`'`), ampersands (`&`), and other HTML-sensitive symbols write raw instead of escaping as `\u0027`, `\u0026`, etc.

>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>

VideoGenerator - Patch Notes | v1.2.1.1

MINOR UPDATE & DESIGN REFINEMENT
This version focuses on a clean visual redesign of the Background Design studio, properties alignment, global font settings integration, and extended event rule mapping support.

New Features
  - Design Studio / Figma-Style Properties Inspector: Redesigned the properties panels in Background Design to align labels (e.g. search, font, alignment) on the left (110px) and control inputs on the right in a clean grid.
  - Design Studio / Separated Card Headers: Enclosed property section titles in styled header bars with a distinct background (SurfaceBrush) and bottom separator lines.
  - Design Studio / Proportional Height Distribution: Distributed the property cards vertically using equal proportional row definitions (*) to seamlessly cover the full height of the 1080p Live Preview canvas.
  - Design Studio / Inline Explanatory Guides: Added clear description text in each configuration panel explaining Riot API searching, custom uploading, typography vertical offsets, and icon alignments.
  - Core / Dynamic Item Name Resolution: Added dynamic name-to-ID resolution using DDragon data inside Quick Edit and IconManager, allowing users to type standard item names (e.g. "Infinity" or "Boots") and automatically resolve them to official Riot IDs and icons.
  - Design Studio / Customizable Icon Layout: Added controls in Background Design to align the overlay icon (Left or Right) and adjust its vertical position via relative offset coordinates, updating both the live designer canvas and the image generator.

Improvements
  - Core / Global Typography Settings: Moved the Font Family selector from the Dashboard directly into the Typography panel of the Background Design tab, saving configuration parameters dynamically to settings.json.
  - Core / Expanded Event Rule Mappings: Added native event rule mappings for `KillAheadAllyTeam` and `KillBehindAllyTeam` to map correctly to generic "Ally Team Ahead / Behind" translations, preventing wrong champion lookup matches.
  - Icons / Flexible Champion Skin Resolution: Extended GetChampionIconAsync to recognize both AatroxSkin1 and Aatrox_1 formats, with automatic leading-zero normalization (e.g. Aatrox_01 -> Aatrox_1).

Bug Fixes
  - UI / Layout Alignment: Expanded the sidebar width from 340px to 380px to provide better breathing room, eliminating text wrapping and control crowding.

>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>

VideoGenerator - Patch Notes | v1.2.1.0

MEDIUM UPDATE
This version focuses on dynamic monster and structure visual mappings, modular event parser architecture, localization robustness, and UI reliability.

New Features
  - Mapping Engine / Monsters & Structures Tabs: Added dedicated graphical management tables in the Event Rules tab to easily add, delete, and save custom monsters and structure lookup names in persistent configuration JSON files.
  - Core / Dynamic Mapping Integration: Re-routed monster and structure checks in the parsing engine to dynamically load configurations from local `monsters.json` and `structures.json` with fallback defaults.

Improvements
  - Parser / Modular Architecture: Refactored `NameParser.cs` into specialized event sub-parsers (`IEventParser`, `ItemEventParser`, `MonsterEventParser`, `SkinInteractionParser`, `GroupInteractionParser`, `SpellOrAttackParser`, `DynamicRuleParser`) inside `Services/Parsers` to ensure cleanly isolated parsing logic.
  - Parser / Case Normalization: Added case-insensitive normalization to map `Darking` correctly to the `Darkin` region.
  - Localization / Non-destructive Translation Merging: Improved `TranslationService` to load embedded default translations and safely merge any missing keys to the local `translations.json` file without overwriting existing user-customized translations.
  - Icons / Structure Visual Resolution: Mapped structure icons to standard match history icons on League Fandom Wiki (`Blue_Turret_icon.png`, `Blue_Inhibitor_icon.png`, `Blue_Nexus_icon.png`) to ensure reliable downloads and visual clarity.
  - UI / Structure Type Selection: Enabled selecting "structure" in the Dashboard Quick Edit dropdown to correctly update structure types.

Bug Fixes
  - Parser / Fixed Prefix Stripping in Rules Matching: Resolved a bug where champion prefixes (e.g. `Play_vo_AhriSkin89_`) prevented general actions (such as `Shop2DOpen`, `Recall3DGeneral`, `Death3D`) from matching rules in `DynamicRuleParser`, forcing them to fall back to raw folder names.
  - UI / Reverted Generic Event Icons: Reverted generic fallback icon path assignment to `null` so actions classified as generic do not display a champion's face icon in the dashboard list or video compilation.
  - Core / Expanded Champion Roster: Added missing roster champions (`KSante`, `BelVeth`, `Hwei`, `Ambessa`, `Mel`, etc.) to DefaultGroups and AliasManager dictionaries.

>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>

VideoGenerator - Patch Notes | v1.2.0.0

MAJOR UPDATE
This version focuses on layout customization, pipeline filtering logic, live preview optimization, and UX improvements.

New Features
  - Dashboard / Resizable Pipeline Panel: added standard drag-to-resize support (GridSplitter) for the Detected Pipeline sidebar.
  - Dashboard / Event Deletion: added delete button (trash icon) to easily remove specific events from the pipeline in real-time, preventing them from being compiled.
  - Dashboard / Live Background Synchronization: preview automatically refreshes instantly when the user changes or removes the background in the Design tab.
  - Core / Intelligent Control Disabling: the Target Lang selector is locked once the folders are analyzed to avoid inconsistent state, and the Icon Lookup textbox is disabled when the Icon Type is set to generic.

Improvements
  - Core / Live Preview Cache: live preview renders directly to memory and is written as a temp file in AppData/Cache instead of polluting the production OutputImagesDir.
  - Core / Automatic Rule Sync on Load: updated RuleManager to automatically synchronize and propagate code updates of default system rules directly into the user's local event_rules.json upon startup, preserving custom user rules.
  - Parser / Universal General Suffix Resolver: unified NameParser to dynamically append 'in General' / 'en General' suffixes to all simple rules (e.g. Joke, Taunt, Laugh) ending in General/inGeneral.
  - Parser / Offline Champion Validation: added validation of extracted targets against a built-in dictionary of all 168+ champions (including PBE additions like Locke, Zaahen, and Yunara) to prevent generic suffixes (like "First" or "Ally") from forcing champion icon lookups.
  - Parser / Expanded Default Rules: added specific rules for KillFirst (First Blood), KillPenta (Pentakill), KillAllyAhead, and KillAllyBehind for accurate automatic translation mapping.
  - UI / List Layout Tuning: ListBox items now span the full container width (Stretch) with right-aligned status badges and delete buttons for a clean, professional aesthetic.
  - UI / Smooth Pixel Scrolling: changed ListBox scrolling unit from Item to Pixel for a smooth, high-fidelity experience.

Bug Fixes
  - Core / Fixed incorrect 'Missing Icon' status for generic events (generic events do not require icons and now correctly show 'READY').

>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>

VideoGenerator - Patch Notes | v1.1.0.0

MAJOR UPDATE
This version delivers a complete visual and functional overhaul, transforming the generator into a professional production suite. Featuring a brand new Interactive Dual-Phase Workflow, a dedicated Cinematic Design Studio, and a high-performance Dictionary engine with intelligent autocomplete, VideoGenerator now provides a seamless and high-fidelity experience for automated video creation.

New Features
  - Dashboard / Redesigned Interactive Dual-Phase Workflow: separate Analysis and Live Preview phases for precise pipeline control.
  - Dashboard / Real-time Status Badges: immediate visual feedback (READY, ICON MISSING, NO AUDIO) for all detected events.
  - Dashboard / 1080p Live Preview: instant high-definition frame composition before starting batch rendering.
  - Design Studio / Cinematic Layout Overhaul: new technician-style grid overlay and professional safe-area guides.
  - Settings / Global Configuration Hub: centralized management for audio engine properties, language preferences, and directories.
  - Dictionary / Intelligent Inline Autocomplete: fast, non-intrusive suggestion system that accepts matches via Tab, Enter, or Right Arrow.
  - UI / Modern HUD Progress Bar: rebuilt with a vibrant violet accent and rounded container for clear status tracking.
  - Mapping Engine / Redesigned Lore Interface: compact professional inspector maximizing data visibility with abbreviated technical headers.
  - Monster Engine / MD5 Hashing Downloader: secured high-resolution asset delivery by bypassing network blocks with direct CDN URL generation.
  - Monster Engine / Expanded League Coverage: added precise mappings for all Jungle Camps, Drakes, Baron Nashor, and Voidgrubs.

Improvements
  - UI / Eye-Friendly HUD Palette: standardized soft gray typography and highlights to reduce visual fatigue during long sessions.
  - UI / Stability Overhaul: eliminated layout jitter and sub-pixel movement during list selection and button interaction.
  - Dictionary / Refined Table Interface: borderless "Edit-in-Place" design reduces visual clutter and highlights on focus.
  - Settings / Intelligent Text Inputs: added custom clear buttons that appear only when text is present.
  - Performance / Global Virtualization: significant speed increase and memory reduction in all lists through visible-only rendering.
  - Performance / Non-Blocking Dictionary: background data parsing ensures instant navigation to the localization module.

Bug Fixes
  - Core / Fixed critical application crashes related to color assignment and XAML resource definitions.
  - Settings / Re-engineered persistence engine for 100% reliable configuration saving and loading.
  - UI / Fixed layout issues including header rounding bleed, text clipping, and column alignment in dense tables.
  - UI / Removed unsupported typographic properties (LetterSpacing) to ensure perfect rendering stability.

>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>

VideoGenerator - Patch Notes | v1.0.0.0

INITIAL RELEASE
Initial release of the VideoGenerator desktop application, migrating the original core from Python to a professional C# environment for enhanced accessibility and performance.