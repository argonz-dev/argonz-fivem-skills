---
name: codewalker-cli-and-water
description: >-
  Verify GTA V / FiveM stream assets (.ydr, .ytd, .ytyp, .ymap) with CodeWalker
  CLI XML export/import roundtrips, convert PNG <-> DDS textures via texconv.exe,
  and edit water.xml (WaterQuads, CalmingQuads, WaveQuads) for MLO beaches and
  pools. Use when validating exported FiveM assets, inspecting DDS textures
  visually, or adjusting coastal waves and calming zones.
---

# CodeWalker CLI Verification, `texconv` DDS, & `water.xml`

Run post-export CodeWalker roundtrips, inspect or build `.dds` textures, and configure GTA V / FiveM ocean/shoreline behavior in `water.xml`.

## 1. CodeWalker CLI (`codewalker-cli.exe`) Verification

Always check actual `codewalker-cli.exe help` before scripting flags. Key verified rules:
- **Positional outputs, NO `-o`**: Syntax is `codewalker-cli.exe <command> <subcommand> INPUT OUTPUT ...`.
- **Output must NOT pre-exist**: `codewalker-cli` rejects any existing destination file or directory (`Existing outputs are rejected`). Delete or pick a fresh staging path first.
- **Exit codes**: `0` = success, `1` = operation error, `2` = wrong usage.

```powershell
$cw = "codewalker-cli.exe"  # e.g. ...\CodeWalkerCLI\codewalker-cli.exe
$rt = ".\scratch\cw_roundtrip"
Remove-Item $rt -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path "$rt\imp" | Out-Null

# 1. XML Export into non-existing subfolders with --names-dir for hash resolution
& $cw xml export "$stm\my_building.ydr" "$rt\bld" --names-dir $stm
& $cw xml export "$stm\my_shared.ytd"   "$rt\ytd" --names-dir $stm

# 2. Check unresolved <Name>hash_...</Name> (shader <FileName>hashes are expected without --game-dir)
Get-ChildItem $rt -Recurse -Filter "*.xml" | Select-String -Pattern "<Name>.*hash_|hash_.*</Name>"

# 3. Roundtrip XML Import into clean targets to confirm structural validity
& $cw xml import "$rt\bld\my_building.ydr.xml" "$rt\imp\my_building.ydr"
& $cw xml import "$rt\ytd\my_shared.ytd.xml"   "$rt\imp\my_shared.ytd"
```

- Pass `--game-dir "<GTA V path>"` when resolving vanilla archetype/shader hashes across game RPFs.
- Supported XML types: legacy `YMAP`, `YTYP`, `YDR`, `YDD`, `YFT`, `YTD`, `YBN`, and `YCD`.

## 2. `texconv.exe` Workflow (PNG <-> DDS)

Blender `img.save()` raises `Could not write image` on loaded `.dds` buffers. Use `texconv.exe` for both `.dds` generation and visual inspection:

- **Convert `.dds` -> `.png` preview** (for `read_file` visual inspection):
  ```powershell
  & texconv.exe -ft png -y -o ".\dds_prev" ".\EXPORT\dds\my_tex.dds"
  ```
- **Convert `.png` -> GTA V `.dds`** (DXT1 opaque / DXT5 alpha, lowercase `-l`, overwrite `-y`):
  ```powershell
  & texconv.exe -f DXT1 -l -y -o ".\EXPORT\dds" ".\Textures\albedo.png"
  & texconv.exe -f DXT5 -l -y -o ".\EXPORT\dds" ".\Textures\cutout_alpha.png"
  ```
- Consult [water-xml-and-texconv.md](./references/water-xml-and-texconv.md) for `texconv` flags and `water.xml` schemas.

## 3. Coastal Water, `water.xml` & Shoreline Foam

1. **Tame Beach Waves (`<CalmingQuads>`)**:
   - Use `<CalmingQuads>` around coastal MLO beaches/docks to dampen ocean waves without flattening the open sea.
   - Each `<Item>` uses `<minX value="..."/>`, `<maxX value="..."/>`, `<minY value="..."/>`, `<maxY value="..."/>`, and `<fDampening value="0.05"/>` (`0.0` = maximum calm, higher values allow more wave motion; check neighbor vanilla quads in `water.xml`).
2. **Wave Swells (`<WaveQuads>`)**:
   - Uses `<minX>`, `<maxX>`, `<minY>`, `<maxY>`, `<Amplitude>`, `<XDirection>`, `<YDirection>`.
3. **Foam / Shoreline Mesh Alignment**:
   - Verify actual ocean waterline elevation in `water.xml` (`<WaterQuads>` `<z value="..."/>`) and align beach foam meshes slightly above wet sand at the true in-game waterline.
   - Re-export changed foam/beach shells to `stream/` and inspect both in Blender and CodeWalker/in-game screenshots.
