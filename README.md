# VideoGenerator - HUD Event Video Composer

**VideoGenerator** is a high-performance desktop application built in WPF (C# / .NET 10) that parses League of Legends voiceover/HUD interaction folder structures, applies a dynamic Event Mapping Engine, resolves translations, and batches-compiles them into professional 1080p MP4 showcase videos using an integrated FFmpeg engine.

---

## 🚀 Key Features

### 1. Dual-Phase Interactive Workflow
* **Phase 1 (Directory Analysis):** Processes the selected media folders. The engine scans folder names, resolves rules, parses champion aliases, and populates a pipeline with real-time status indicators:
  * `READY`: Event is fully matched and has valid icons and audio (generic events do not require icons and correctly show `READY`).
  * `MISSING ICON`: The event is missing its visual asset (attempts dynamic downloader).
  * `NO AUDIO`: No audio files were detected in the source folder.
* **Phase 2 (Cinema Live Preview):** Click any event in the queue to render an instant, live 1920x1080 preview frame in the HUD UI.
* **Batch Encoder:** Processes all verified pipeline events and builds high-quality MP4 videos.

### 2. Resizable & Customizable Pipeline
* **GridSplitter Sidebar:** Seamlessly adjust the width of the `DETECTED PIPELINE` panel by dragging its border.
* **Event Deletion:** Instantly remove events from the queue (via a trash icon) to prevent them from being compiled, automatically updating the pipeline and preview.
* **Layout Stretching & Smooth Scrolling:** Re-engineered list items span the full width of the container (`Stretch`) with right-aligned status badges and delete buttons, operating on pixel-based (`Pixel`) scrolling for smooth, high-fidelity navigation.

### 3. Mapping & Event Rule Inspector (`rules.json`)
Manage parsing keywords and associate them with translation keys and icon types (`generic`, `champion`, `item`, `monster`).
* **Rule Priority Engine:** The parsing engine sorts dynamic rules by keyword length descending, ensuring longer/more specific rules (like `RecallFast`) always run before shorter ones (`Recall`).
* **Universal Suffix Resolution:** Automatically appends localized "in General" / "en General" suffixes to all simple rules (e.g., `Joke`, `Taunt`, `Laugh`) that end with `General` or `inGeneral`.
* **Dimension Suffix Robustness:** The matching engine automatically strips dimensional indicators (`2D` or `3D`) from folder names and rule keywords (e.g., `Attack2D` or `Joke3D`), making the parsing system immune to League of Legends dimension variations.

### 4. Dynamic Dictionary & Autocomplete (`translations.json`)
The application supports multi-language translations (English `EN`, Spanish `ES`, Turkish `TR`) for event logs.
* **Edit-in-Place Table:** A borderless inline editing table in the Dictionary tab highlights cells on hover and focus for a premium editing experience.
* **Smart Language Locks:** The Target Lang selector locks once folders are analyzed to prevent state conflicts, and the Icon Lookup text box is disabled when the Icon Type is set to generic.

### 5. Background Design Studio
Fine-tune HUD layouts in real-time. Includes safe-area indicators, grid overlays, and visual adjustment knobs (Text Vertical Offsets, custom backgrounds) that synchronize instantly with the preview composer.

---

## 📁 System Requirements & Setup

1. **Runtime:** .NET 10.0 Windows SDK (WPF).
2. **Media Engine:** FFmpeg (extracted automatically to temp directories on startup).
3. **Build command for developers:**
   ```bash
   dotnet build
   ```
4. **Run command for developers:**
   ```bash
   dotnet run
   ```
