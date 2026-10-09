# TABZ Android Port — research project

This repository is for investigating whether **Totally Accurate Battle Zombielator (TABZ)** can be adapted to Android.

## Current stage

**Stage 1: inspect the community-modified builds.** The workflow attempts to download the public ZIPs linked from the community folder, extracts them temporarily in a GitHub Actions runner, and publishes a file inventory as an artifact. The game files themselves are **not committed or redistributed** by this repository.

Source folder: [TABZ modded builds public](https://drive.google.com/drive/folders/1Y9vmCWr31-b1du0SFkKop7DODTL9Qvoc?usp=drive_link)

## Run the inventory

1. Open the **Actions** tab.
2. Select **Inventory TABZ builds**.
3. Press **Run workflow** (or wait for the scheduled/manual trigger).
4. Open the completed run and download the `tabz-inventory` artifact.

The inventory includes paths, file sizes, extensions, SHA-256 hashes, and a summary of likely engine/build markers. It does not include the original game binaries.

## Important limitations

- This workflow depends on the public Google Drive folder remaining accessible and on its download page not changing.
- A successful inventory is not proof that the game can be ported.
- Android feasibility depends on the actual engine, Unity version (if applicable), native plugins, architecture, graphics APIs, input handling, and available project/source files.
- Do not publish copyrighted game files without permission. This repository is intended to store analysis tools and notes, not game assets.

## Project plan

- [ ] Download and inventory both public ZIP builds
- [ ] Identify engine, build target, managed assemblies, native libraries, and assets
- [ ] Determine whether source/project files exist or only compiled game builds
- [ ] Evaluate a realistic Android porting route
- [ ] Prototype only after the technical evidence supports it
