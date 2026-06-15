VideoGenerator - Patch Notes | v1.2.1.2

MINOR UPDATE & LOCALIZATION FIXES
This version focuses on raw UTF-8 Unicode JSON serialization for localization files and dynamic translation resolution for spell hit/cast events.

New Features
  - Mapping Engine / Custom Icon Lookup in Event Rules: Added an optional `IconLookup` string property to the `EventRule` model, exposed via a new "Icon Lookup (Optional)" TextBox in the Event Rules registration panel, and integrated it into the parsing logic to allow overriding default icons with custom asset lookup names.

Improvements
  - Parser / Dynamic Translation Lookup for Spells: Integrated TranslationService in SpellOrAttackParser to prioritize resolving translated text for spell hit/cast events (e.g. event_EMissile_hit3D) from translations.json before falling back to automatic English title formatting.
  - Core / Raw Unicode JSON Serialization: Configured TranslationService and Dictionary View saving to write unescaped Unicode characters (á, í, ö, ü, ş, ç) directly to translations.json instead of converting them to hexadecimal escape sequences (\u00E1, \u00ED, etc.), ensuring translations are 100% readable and human-editable.

Bug Fixes
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