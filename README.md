# VideoGenerator - HUD Event Video Composer

**VideoGenerator** is a high-performance desktop application built in WPF (C# / .NET 10) that parses League of Legends voiceover/HUD interaction folder structures, applies a dynamic Event Mapping Engine, resolves translations, and batches-compiles them into professional 1080p MP4 showcase videos using an integrated FFmpeg engine.

---

## 🚀 Key Features

### 1. Dual-Phase Interactive Workflow
* **Phase 1 (Directory Analysis):** Processes the selected media folders. The engine scans folder names, resolves rules, parses champion aliases, and populates a pipeline with real-time status indicators:
  * `READY`: Event is fully matched and has valid icons and audio.
  * `MISSING ICON`: The event is missing its visual asset (attempts dynamic downloader).
  * `NO AUDIO`: No audio files were detected in the source folder.
* **Phase 2 (Cinema Live Preview):** Click any event in the queue to render an instant, live 1920x1080 preview frame in the HUD UI.
* **Batch Encoder:** Processes all verified pipeline events and builds high-quality MP4 videos.

### 2. Mapping & Event Rule Inspector (`rules.json`)
Manage parsing keywords and associate them with translation keys and icon types (`generic`, `champion`, `item`, `monster`).
* **Rule Priority Engine:** The parsing engine sorts dynamic rules by keyword length descending, ensuring longer/more specific rules (like `RecallFast`) always run before shorter ones (`Recall`). Specialized core rules (like monster attacks or shop purchases) are prioritized first.
* **Dimension Suffix Robustness:** The matching engine automatically strips dimensional indicators (`2D` or `3D`) from folder names and rule keywords (e.g., `Attack2D` or `Joke3D`), making the parsing system immune to League of Legends dimension variations.

### 3. Dynamic Dictionary & Autocomplete (`translations.json`)
The application supports multi-language translations (English `EN`, Spanish `ES`, Turkish `TR`) for event logs.
* **Edit-in-Place Table:** A borderless inline editing table in the Dictionary tab highlights cells on hover and focus for a premium editing experience.
* **Event Key Autocomplete:** The Event Key textbox is an editable ComboBox that automatically loads keys from active rules, reloading in real-time on tab load.

### 4. Background Design Studio
Fine-tune HUD layouts in real-time. Includes safe-area indicators, grid overlays, and visual adjustment knobs (Text Vertical Offsets, custom backgrounds) that synchronize instantly with the preview composer.

---

## 📁 System Requirements & Setup

1. **Runtime:** .NET 10.0 Windows SDK (WPF).
2. **Media Engine:** FFmpeg (extracted automatically to temp directories on startup).
3. **Build Command:**
   ```bash
   dotnet build -c Release
   ```
4. **Run Command:**
   ```bash
   dotnet run
   ```
