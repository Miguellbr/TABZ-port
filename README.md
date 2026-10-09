# TABZ Android Port — research project

This repository investigates whether Totally Accurate Battle Zombielator (TABZ) can be adapted to Android.

## Current stage

Stage 1: inspect and compare both community-modified TABZ archives linked from the public Google Drive folder. The workflow supports the `.7z` build and the `.zip` build, extracts each into its own directory, and creates separate inventories plus a comparison report.

Source folder: https://drive.google.com/drive/folders/1Y9vmCWr31-b1du0SFkKop7DODTL9Qvoc?usp=drive_link

## Run the analysis

1. Open the Actions tab.
2. Select Inventory TABZ builds.
3. Press Run workflow.
4. Open the completed run and download the `tabz-two-version-inventory` artifact.

The artifact contains:
- `source-archives.json`: maps version-1 and version-2 to their original archive filenames and formats;
- `version-1/` and `version-2/`: separate file inventories and reports;
- `COMPARISON.md` and `comparison.json`: files present in only one build and same-path files with different content.

Version labels are assigned by alphabetical archive filename order. Check `source-archives.json` to see which original archive corresponds to each label.

## Important limitations

- The workflow expects exactly two supported archives (`.zip` or `.7z`). It stops rather than silently mixing files if the Drive folder contents change.
- Downloads and extracted game files stay in the temporary runner; the original game binaries are not committed or included in the artifact.
- This inventory is metadata only: paths, sizes, extensions, SHA-256 hashes, and likely engine/build markers.
- A successful inventory is not proof that the game can be ported. Feasibility depends on the engine/version, native plugins, architecture, graphics APIs, input handling, and whether project/source files exist.

## Project plan

- [ ] Download and inventory both TABZ builds separately
- [ ] Identify engine, build target, managed assemblies, native libraries, and assets
- [ ] Determine whether source/project files exist or only compiled game builds
- [ ] Evaluate a realistic Android porting route
- [ ] Prototype only after the technical evidence supports it
