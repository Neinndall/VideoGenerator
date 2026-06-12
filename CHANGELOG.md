VideoGenerator - Patch Notes v1.1.0.0
=======================================
✨ NEW FEATURES & VISUAL OVERHAUL
---------------------------------
- [PRODUCTION DASHBOARD] Redesigned from a monolithic generator to an Interactive Dual-Phase Workflow.
    * **Phase 1 (Analysis):** "Process Folders" scans media directories, applies Lore Engine rules, and populates a "Detected Pipeline" list.
    * **Dynamic Status Badges:** Real-time UI feedback (READY, ICON MISSING, NO AUDIO) for each detected event.
    * **Phase 2 (Live Preview):** Clicking any event in the pipeline generates an instant, real-time 1920x1080 preview of the composed frame before encoding.
    * **Execution:** "Start Batch Rendering" now strictly processes the validated queue.

- [BACKGROUND DESIGN STUDIO] Complete cinematic overhaul for v1.1.0.0.
    * **Live HUD Preview:** Added technician-style grid overlay and safe-area guides for professional 1080p previewing.
    * **Property Inspector:** Redesigned as a sleek left-sidebar inspector, freeing up space for the cinema canvas.
    * **Real-time Sync:** All design changes (offsets, backgrounds) now synchronize instantly with the global engine.

- [ENGINE CONFIGURATION] Introduced a comprehensive global settings hub (`SettingsView`).
    * **Audio Engine Properties:** Configurable silence duration between audio tracks.
    * **Dictionary Preferences:** Set the default startup language for the localization module.

- [DICTIONARY] Improved loading logic and compact layout.
    * **Anti-Flicker Loading:** Default language is now pre-selected before data fetch to eliminate UI transition jitters.
    * **Compact Sidebar:** Reduced width (110px) and abbreviated titles (LANGS) for optimized screen utilization.

- [MAPPING ENGINE] Complete redesign of the "Mapping & Lore Engine" (EventRulesView).
    * **Compact Inspector:** Reduced panel width (300px) to maximize data table visibility.
    * **HUD Abbreviations:** Simplified technical headers (DICT KEY, INT NAME, REPO) for a cleaner professional look.

🎨 UI/UX REFINEMENTS (GLOBAL THEME)
----------------------------------
- [COLOR PALETTE] Implemented eye-friendly HUD theme.
    * **Soft Text:** Standardized #D4D4D8 (Soft Gray) for base text to reduce eye strain.
    * **Soft Highlights:** New `TextHighlightBrush` (#FAFAFA) for hover/selection states, eliminating harsh pure whites.

- [STABILITY] Eliminated layout jitter and "moving content" during interaction.
    * **Fixed Font Shifts:** Removed `FontWeight="Bold"` from selection triggers to prevent text metrics from pushing layout.
    * **Refined Buttons:** Stabilized hover states by removing shadows and harsh highlights that caused sub-pixel movement.

- [INTERACTION] Refined selection logic.
    * **Non-Selectable Lists:** Disabled visual selection on purely informational lists (Rules, Themes, Aliases) to clean up the UX.

🐛 BUG FIXES & CRITICAL RESOLUTIONS
-----------------------------------
- [CRITICAL] Fixed unhandled exception: "#FFA78BFA" (Color) assignment to Foreground property (Brush).
- [PERSISTENCE] Resolved AppSettings loading bug; re-engineered serialization using public properties for 100% reliable save/load.
- [LAYOUT] Fixed header corner rounding bleed and column alignment in high-density data tables.
- [SETTINGS] Relocated Text Vertical Offset to the Design Studio for better UX with real-time preview.

    * Integrated into the HUD UI (Top-Left corner).
    * New AssemblyVersion utility class for centralized version management.

- [ANIMATIONS] Introduced global animation system (Theme/Animations.xaml).
    * Added modern spinning loader icon during Dictionary data fetching.
    * Smooth visibility transitions for status messages.

🎨 UI/UX REFINEMENTS (GLOBAL THEME)
----------------------------------
- [INPUTS] Centralized input architecture (Theme/InputStyles.xaml).
    * Standardized ModernTextBox & ModernComboBoxStyle (52px height).
    * New "CompactModernTextBox" (42px) optimized for high-density lists.
    * Eliminated purple text tints; enforced professional White/Neutral palette.

- [BUTTONS] Re-engineered button interactions (Theme/ButtonStyles.xaml).
    * Dynamic gradient brightening on hover (replaces dull gray effect).
    * Added glow/shadow effects and 97% scale-down click feedback.
    * Fixed "Layout Jitter" by standardizing RenderTransformOrigins.

- [CONSOLE] Polished RichLog Console (Theme/RichTextBoxStyles.xaml).
    * Increased internal padding (8px) for terminal-like readability.
    * Removed unwanted focus/hover borders for a cleaner HUD aesthetic.

⚡ PERFORMANCE OPTIMIZATIONS
---------------------------
- [VIRTUALIZATION] Global Virtualized ListBox architecture (Theme/ListStyles.xaml).
    * Drastic performance increase in Dictionary and Event Mapping views.
    * UI now only renders visible elements (VirtualizingStackPanel + Recycling).
    * Significant reduction in memory overhead during view navigation.

- [ASYNC LOADING] Implemented background data parsing in TranslationsView.
    * Navigation to Dictionary is now instant (Non-blocking UI thread).
    * Status bar tracks loading progress in real-time.

🐛 BUG FIXES & CRITICAL RESOLUTIONS
-----------------------------------
- Resolved critical "StaticResourceExtension" runtime crashes during view navigation.
- Fixed XAML Compilation Errors: MC3000 (Mismatched tags), MC3074 (Invalid Definitions).
- Fixed "Text Clipping" in multiple views by correcting internal padding/height ratios.
- Removed unsupported properties (LetterSpacing) that caused XAML engine failures.
- Fixed missing Padding bindings in custom RichTextBox ControlTemplates.

---------------------------------------
VideoGenerator - Patch Notes v1.0.0.0
---------------------------------------

✨ INITIAL RELEASE
---------------------------------

The app has been developed by changing its language from Python to a desktop application, making it more accessible to users.