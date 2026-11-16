---
name: argonz-fivem-skills
description: >-
  Unified ARGONZ Blender & GTA V / FiveM MLO production pipeline: low-poly
  architecture (<1000 tris, ImageGen PBR textures), Sollumz & szio.gta5 split
  export (.ydr/.ytd/.ytyp/.ymap), articulated rail-following sectional doors
  (.ycd + wall switches + Lua), CodeWalker CLI roundtrips, headless CodeWalker
  3D viewer (cw_agent.py), texconv DDS conversion, and water.xml tuning.
---

# ARGONZ FiveM & Blender Master Pipeline

Single unified skill covering the full **Blender -> Textures/DDS -> Sollumz/`szio.gta5` -> Animated Doors/Switches -> CodeWalker CLI & Headless 3D -> FiveM Stream** workflow.

---

## 1. Low-Poly Architecture & ImageGen PBR (`bmesh`)

1. **Under 1,000 Tris & Zero Bevels**: Build directly in `bmesh` with clean axis-aligned primitives (`8–12` segs max for cylinders); omit buried bottom faces (`skip_bottom=True`). Never add `BEVEL` modifiers.
2. **Texture Over Geometry**: Never stack geometry for stone courses, slats, louvers, or flutes—model 1 face/box and represent repetition with seamless textures.
3. **Image-Generated Seamless Textures**: Generate dedicated seamless textures via `image-generator` subagents in parallel (*"seamless, tileable 2D PBR albedo texture, flat top-down orthographic view, completely even diffuse lighting, no perspective distortion, no directional shadows, no borders"*). Inspect with `read_file`, then convert to GTA V `.dds` (`DXT1` opaque / `DXT5` alpha) via `texconv.exe`.
4. **UV & Shader Hygiene**:
   - Name the active UV layer `"UVMap 0"` and ensure `ShaderNodeUVMap.uv_map` matches an existing UV layer.
   - **Never call `obj.data.materials.clear()`** on multi-material meshes (wipes polygon `material_index` to `0`); edit slots in-place or use [preserve_mat_indices.py](./scripts/preserve_mat_indices.py).

---

## 2. Sollumz & `szio.gta5` Split Export Pipeline

Consult [szio-api-gotchas.md](./references/szio-api-gotchas.md) for exact `szio.gta5` constructors and readback fields.

1. **Split Architecture**:
   - Group fixed structural shells by zone (`*_building`, `*_driveway`, etc.) around local bounding centers.
   - Keep repeatable props/plants origin-centered in individual `.ydr`s and instance via `.ymap` (uniform `scale_xy` + `scale_z`, `Entity(rotation=rot.inverted())`).
   - Keep doors/sliding gates in their own `.ydr` (`flags=67239936`, `special_attribute=8` for linear doors, or `flags=525856`, `specialAttribute=0`, `clipDictionary=...` for `.ycd` animated doors).
2. **Vanilla GTA V / `SOLLUMZ_ASSET_LIBRARY` Assets = `0 KB`**:
   - **Never rename** or convert unmodified vanilla assets into custom `.ydr`s; register directly by exact GTA archetype name in `.ytyp` / `.ymap`. Only convert to custom `.ydr` if geometry/UVs/materials are intentionally edited.
3. **Shared `.ytd` Dictionaries (`0` Embedded Textures)**:
   - Set `nd.texture_properties.embedded = False` and clear `drw.shader_group.embedded_textures = {}`.
   - Keep every `.ytd` raw uncompressed DDS total safely under **16 MB** (`< 13.8 MB` target).
   - Use `create_shader` from `bl_ext.repo_sollumz_org.sollumz.ydr.shader_materials` (avoid `shader_materials_v2`).
   - For custom `DRAWABLE_MODEL` meshes built in Python, always register `mdl.sz_lods.get_lod(LODLevel.HIGH).mesh = mdl.data` or Sollumz reports `has no Sollumz materials!`.
4. **Static Wind-Free Plants, `UVMap 0`, Vertex Color, & Collisions**:
   - Strip `trees_shadow_proxy.sps` / `cpv_only.sps`; remap `trees*.sps` to `cutout*.sps`; auto-double-side single-sided foliage cards.
   - Enforce `"UVMap 0"` and `Color 1` (`BYTE_COLOR`, `CORNER`, `G = 0.0`, pink `(1, 0, 1, 1)`).
   - Always set `composite_flags1` (`MAP_WEAPON..MAP_VEHICLE`) and `composite_flags2` (`VEHICLE_NOT_BVH..GLASS`) on new `BOUND_BOX` / `BOUND_GEOMETRYBVH` bounds (Sollumz leaves them `0` by default).
   - Run [audit_stream.py](./scripts/audit_stream.py) after export to verify `0` embedded textures, `0` missing textures, `< 16 MB` `.ytd`s, and valid bounds.

---

## 3. Animated Sectional Doors (`.ydr` + `.ycd`) & Wall Switches

Read [animated-sectional-doors-and-switches.md](./references/animated-sectional-doors-and-switches.md) for full math, XML patches, and Lua script templates.

- **Identity Bone Axes**: Orient armature edit bones along **`+Y`, `roll = 0`** (never `-Z`) so `bone.matrix_local` has Identity 3×3 rotation, and set `bone.bone_properties.flags` to `RotX, RotY, RotZ, TransX, TransY, TransZ`.
- **Lintel Clearance & Chord Tracking**: Recess moving panels behind exterior concrete lintels (`+Y`), merge static L-rails into the root bone (`Tag = 0`), and solve exact chord intersections along the `90°` curve so hinges stay locked.
- **Multi-Clip `.ycd` Hash Rule**: Inject `<Hash><clip_name></Hash>` into every `<Clips><Item>` in `.ycd.xml` before `codewalker-cli xml import` (otherwise CodeWalker overwrites clips at `Hash = 0`).
- **Dual Switches & FiveM Script**: Place exterior switches in `.ymap` (avoids MLO portal culling) and interior switches in MLO room `attachedObjects`; drive via `anim@apt_trans@garage` (`gar_open_1_left`) + `PlayEntityAnim` (`stayInAnim = true`) + `SetEntityCollision`.

---

## 4. CodeWalker CLI, Headless 3D Viewer, `texconv`, & `water.xml`

Consult [water-xml-and-texconv.md](./references/water-xml-and-texconv.md) and [codewalker-headless-bridge.md](./references/codewalker-headless-bridge.md).

1. **CodeWalker CLI (`codewalker-cli.exe`)**:
   - Positional outputs only (no `-o`), and destination path must **not** pre-exist.
   - Run `xml export <asset> <out_dir> --names-dir <stream>` and `xml import <xml> <out_bin>` roundtrips on modified `.ydr`/`.ycd`/`.ytd`/`.ytyp`/`.ymap` files; check for unresolved `<Name>hash_...</Name>`.
2. **`texconv.exe`**:
   - Preview `.dds` with `texconv.exe -ft png -y -o <dir> <file.dds>` (Blender `img.save()` fails on loaded DDS buffers).
   - Build `.dds` with `-f DXT1 -l -y` (opaque) or `-f DXT5 -l -y` (alpha).
3. **Headless CodeWalker 3D (`cw_agent.py`)**:
   - Launch `CodeWalker.exe --background` or `py cw_agent.py launch --background --wait-world`, `load-files --files <stream>`, `look` / `fly` / `raycast` to verify visuals in DX11 off-screen.
4. **Coastal `water.xml`**:
   - Use `<CalmingQuads>` (`fDampening` ~`0.05`) to tame beach waves and align foam meshes to true `<WaterQuads>` `z`.
