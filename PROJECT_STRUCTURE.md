# WinToLin — Project Structure

This document describes the main folders and important files of the WinToLin project so contributors can quickly find relevant code and assets.

## Top-level overview

- `WinToLin.sln` — Visual Studio solution.
- `WinToLin/` — Main application project (WPF).
- `LICENSE`, `README.md` — repository metadata.

## Directory Structure

```
📁 WinToLin
├── App.xaml, App.xaml.cs            # WPF application entry
├── AssemblyInfo.cs
├── WinToLin.csproj                  # Project file
├── bin/                             # Compiled binaries for Debug/Release
├── obj/                             # Build intermediate files
├── Data/                            # Static data and package lists
├── Fonts/                           # Fonts used by the UI
├── Img/                             # Images and icons
├── Logic/                           # Application logic (ViewModels, helpers, repos)
├── Migrator/                        # Migration logic (installers/steps)
├── Views/                           # XAML Views and windows
└── ...
```

### Data
- Path: `WinToLin/Data`
- Purpose: Contains JSON files that enumerate supported distros and per-distro package collections used by the app.
- Notable files:
  - `distros.json` — Contains the currently supported distros.
  - `distros_togo.json` — Distros targeted for early support.
  - `fedora_packages.json` / `ubuntu_packages.json` — Package collections for each distro (mappings of applications to native alternatives or package names).
  - `software_alternatives_db.json` — Database of suggested alternative applications.

### Fonts
- Path: `WinToLin/Fonts`
- Purpose: Stores font files used by the UI for consistent typography.

### Img
- Path: `WinToLin/Img`
- Purpose: App imagery and icons.
- Subfolders and examples:
  - `background/` — Background images for the application UI.
  - `distros/` — Logos/icons for Linux distributions.
  - `icon_big.png`, `icon_small.png` — Application icons.

### Logic
- Path: `WinToLin/Logic`
- Purpose: Core non-UI logic and supporting code used by Views.
- Key subfolders:
  - `DataModels/` — DTOs and models used across app and UI bindings.
  - `Enums/` — Shared enum types.
  - `Helper/` — Utility helpers and extension methods.
  - `Manager/` — Business-logic managers that orchestrate flows (e.g., selection, migration orchestration).
  - `Repositories/` — Data access and JSON-loading helpers.
  - `UIHelper/` — Helpers specific to UI behavior and binding.

### Migrator
- Path: `WinToLin/Migrator`
- Purpose: Implements migration steps and the flow to migrate Windows software choices to Linux equivalents.
- Contents:
  - `Migrator.cs` — Top-level migration orchestration.
  - `DistroDependent/` — Migration steps that vary by distro (install commands, package names).
  - `DistroIndependent/` — Steps common to all distros.

### Views
- Path: `WinToLin/Views`
- Purpose: All XAML UI views and windows.
- Typical layout:
  - `Steps/` — User-facing step views for the custom migration path or guided flow.
  - `Windows/` — Window-level XAMLs such as the main shell and single-step windows.
    - `MainWindow.xaml` — Main application window; often contains top/bottom bars and the primary navigation.
    - `OneStepWindow.xaml` — Window template used to present a single step UI.
  - `SelectMode.xaml` — Entry view where the user picks an operation mode.

### Root files of the WPF project
- `App.xaml` / `App.xaml.cs` — Application startup, resources, global styles and resource dictionaries.
- `WinToLin.csproj` — Project references, NuGet packages, build configuration.

## Migrator implementation notes
- Migration steps are separated into distro-dependent and distro-independent implementations so the flow can pick platform-specific installers (APT, DNF, etc.) while reusing generic logic.
- Repositories load JSON data from `Data/` and present a normalized model to `Manager` classes.

## How pieces fit together (high level)
- The Views bind to DataModels and use Managers from `Logic/Manager` to drive UI state.
- Managers consult `Repositories` for persisted data (the JSON files in `Data/`) and call into `Migrator` to execute migration/install steps.
- `Img/` and `Fonts/` provide static assets referenced by XAML resource dictionaries.

## Contributing / Extending
- Add new distro support by:
  1. Extending `WinToLin/Data/distros.json` and `distros_togo.json` as appropriate.
  2. Adding distro-specific package JSON (e.g., `arch_packages.json`) and a new implementation under `Migrator/DistroDependent` for package installation logic.
  3. Registering the new distro in the repository-loading logic under `Logic/Repositories` and adding UI entries as needed.

## Quick file pointers
- App entry: `WinToLin/App.xaml` and `WinToLin/App.xaml.cs`
- Main project file: `WinToLin/WinToLin.csproj`
- Migration orchestration: `WinToLin/Migrator/Migrator.cs`
- JSON data: `WinToLin/Data/*.json`
- Views: `WinToLin/Views/**.xaml`
- Logic: `WinToLin/Logic/**`

---

If you want, I can:
- Add this content into the repository `README.md` instead, or
- Expand sections with examples (e.g., sample migration flow), or
- Generate a diagram showing how Managers, Repositories, Views, and Migrator interact.

Tell me which you prefer and I will update the file accordingly.