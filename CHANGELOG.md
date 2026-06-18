VideoGenerator - Patch Notes | v1.2.2.1

MINOR UPDATE
This version adds user-configurable options for the Whisper Speech-to-Text model size and target audio language to resolve translation misidentifications.

New Features
  - Settings / Whisper Model Selector: Added UI ComboBox under Settings to select Whisper model size ("tiny", "base", "small"). The default model is upgraded to "base" for higher out-of-the-box accuracy.
  - Settings / Whisper Language Selector: Added UI ComboBox to force Whisper transcriptions into a specific target language (e.g. Turkish, Spanish, English, etc.) rather than relying on automatic detection, resolving wrong language detection issues.

Improvements
  - Core / Dynamic Whisper Download: Modified `TranscriptionService` to dynamically construct URLs and paths, automatically downloading the user-selected model from Hugging Face on demand.
  - UI / Conditional Enablement: Whisper Model and Language settings are reactively disabled if Speech-to-Text transcriptions are toggled OFF.
  - UI / Global ScrollViewer: Added a ScrollViewer around the main ContentArea in `MainWindow.xaml` to satisfy the central scrolling requirement and prevent options from overflowing.

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