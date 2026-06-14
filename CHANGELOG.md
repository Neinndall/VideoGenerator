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