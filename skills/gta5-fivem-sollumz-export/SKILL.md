---
name: gta5-fivem-sollumz-export
description: >-
  End-to-end Blender to GTA V / FiveM asset conversion and optimization pipeline
  using Sollumz and szio.gta5 (.ydr, .ytd, .ytyp, .ymap). Use when exporting
  custom scenes/props/MLOs for FiveM, splitting monolithic drawables into
  individual archetypes, making static wind-free foliage, packing shared texture
  dictionaries under 16 MB, preserving multi-material indices, fixing UVs/vertex
  colors/collisions/lights, or auditing stream output.
---

# GTA V / FiveM Sollumz & `szio.gta5` Export Pipeline

Convert Blender architectural zones, static foliage, and custom props into optimized, instanced FiveM `.ydr` / `.ytd` / `.ytyp` / `.ymap` stream assets with zero embedded textures and verified in-game shading/collisions.

## 1. Split Architecture & Optimization Rules

1. **Structural Zone Shells (`vila_*` / zone-level `.ydr`)**:
   - Group fixed static terrain, walls, buildings, roads, decals, and zone-local hardscape by area around each shell's local bounding center.
   - **Interactable Parts Separate**: Keep doors/sliding gates in their own `.ydr` with `.ytyp` `flags=67239936`, `special_attribute=8`, and `.ymap` `EntityFlags(0)` (instead of static `flags=32` / `EntityFlags.IS_STATIC`).
2. **Separate Archetypes for Plants & Repeatable Props**:
   - **Never merge plants or repeatable props into one giant `.ydr`**.
   - Keep each distinct species/prop (`<prefix>_pl_*`, `<prefix>_pr_*`) origin-centered in its own `.ydr`, deduplicated by geometry/material signature (`verts`, `polys`, `local_bb`, `material_keys`), and instanced via `.ymap`.
   - Bake non-uniform XY scaled props/cliffs into structural terrain shells instead of `.ymap` instances (`.ymap` only supports uniform `scale_xy` + `scale_z`).
3. **Vanilla GTA V Props = `0 KB`**:
   - Reference native GTA props (`prop_*`, `h4_prop_*`, `xm_rock_*`, etc.) directly by archetype name in `.ymap`; never re-export them as custom `.ydr`s.
4. **Shared `.ytd` Dictionaries (Zero Embedded Textures)**:
   - Set `nd.texture_properties.embedded = False` on shaders and clear `drw.shader_group.embedded_textures = {}` before final `.ydr` save.
   - Pack `.dds` textures into shared `.ytd`s mapped via `Archetype.texture_dictionary` (`*_plants.ytd`, `*_shared.ytd`, optionally split `*_beach.ytd` if combined raw DDS approaches `13.8 MB` so every `.ytd` stays safely under GTA V/FiveM's **16 MB physical limit**).
5. **Selective Shell Rebuild (`mtime` Reuse)**:
   - When iterating on specific structural `.ydr`s (e.g., 1–2 edited shells out of 60+ archetypes), compute `cutoff = min(p.stat().st_mtime for p in exp_ydrs) - 10.0`, backdate only the target `.ydr` files via `os.utime(...)` before passing `reuse_since_ts=cutoff` to the split builder, and preserve `.ydr` lights from the previous stream asset.

## 2. Materials, Multi-Material Protection, Static Foliage & Vertex Prep

1. **Protect Per-Face `material_index` Assignments**:
   - **Never call `obj.data.materials.clear()`** on multi-material meshes: clearing slots resets every polygon's `material_index` to `0` (`collapse`), wiping split gravel/curb/fascia/tread mappings across rebuilt `.ydr`s.
   - Always modify slots in-place (`obj.material_slots[i].material = ...`) or snapshot/restore polygon indices using [preserve_mat_indices.py](./scripts/preserve_mat_indices.py) when editing or auditing against a backup `.blend`.
   - Before exporting edited shells, verify every active slot (`{p.material_index for p in obj.data.polygons}`) resolves cleanly and check `CUSTOM_MAT_SPECS` so distinct material roles (e.g., horizontal deck paving vs vertical dark fascia) do not accidentally point to the same DDS stem.
2. **Use Canonical Sollumz Shader Creator**:
   - Import `create_shader` from `bl_ext.repo_sollumz_org.sollumz.ydr.shader_materials` (avoid `shader_materials_v2.create_shader`, which raises `UnboundLocalError` on shaders such as `normal_spec_reflect.sps` and `terrain_cb_w_4lyr_2tex_blend_pxm_spm.sps`).
   - Do **not** assign `nd.sollumz_texture_name` (read-only property derived from `nd.image`); assign `nd.image` and set `nd.texture_properties.embedded = False`.
3. **Static Wind-Free Plants**:
   - Strip `trees_shadow_proxy.sps` and `cpv_only.sps` geometry completely.
   - Remap wind/sway `trees*.sps` shaders to static `cutout*.sps` equivalents (`cutout.sps`, `cutout_tnt.sps`, `normal_cutout.sps`, `normal_spec_cutout.sps`, `normal_spec_cutout_tnt.sps`).
   - Auto-double-side single-sided cutout foliage cards: if opposing normal pairs at rounded face centroids are `< max(2, 5% of cutout faces)`, duplicate those faces with reversed vertex order and copied loop UVs/colors.
4. **Mandatory `"UVMap 0"` Normalization**:
   - `ydrexport` requires exact layer name `"UVMap 0"` (drops `TexCoord0` silently if named `"UVMap"`, `"Atlas"`, `"Tile Repeat"`, `"Foliage Atlas"`, `"Float2"`).
   - If `"UVMap 0"` is missing on a part, detect any `ShaderNodeUVMap.uv_map` present on its mesh (else `active_render` UV or slot `0`) and rename it on the temporary `bpy.types.Mesh` (`tmp_m.uv_layers[pref_uv].name = "UVMap 0"`) **before** merging via `combined_bm.from_mesh(tmp_m)` (`BMLayerItem.name` in `BMesh` is read-only).
5. **Pink/Red Vertex Color (`Colour0` / `Color 1`)**:
   - Enforce `Color 1` (`BYTE_COLOR`, `CORNER`) with `G = 0.0` (convert pure white `~(1,1,1)` to `(1.0, 0.0, 1.0, 1.0)` and zero green on custom paints; `water_poolenv.sps` strips unused CPV automatically).
6. **Selective BVH Collisions & Required Composite Flags**:
   - Skip BVH bounds on small cutout plants, grass/leaf cards, decals, water planes, and tiny glassware; set `Archetype.physics_dictionary = arch_name` **only** when `drw.bounds is not None`, else `""`.
   - Always set static collision flags on newly created `SollumType.BOUND_GEOMETRYBVH` empties (`composite_flags1` -> `62` incl. `map_dynamic`/`map_vehicle`; `composite_flags2` -> `133414592` incl. `ped`/`vehicle_bvh`/`test_*`); Sollumz leaves them `0` by default (non-solid ghost collision).
   - Purge loose vertices (`not v.link_faces`) after deleting polygon subsets or `skip_col` faces so BVH bounding boxes stay tight.

## 3. Lights, Transforms & `szio.gta5` Packing

- Consult [szio-api-gotchas.md](./references/szio-api-gotchas.md) for exact `szio.gta5` constructors, readback fields (`ShaderInst.preset_filename`, `ShaderParameter.value`, `vertex_buffer["TexCoord0"]`), and bounds extraction.
- **Linear LED Strips**: Use `LightType.CAPSULE` with capsule length along `extent.x`.
- **Repeatable Lamp Props**: Attach `1` local `POINT` light inside the shared prop `.ydr` (e.g., bollard) instead of duplicating per-instance world lights inside static shells.
- **Origin Light Bug Guard**: When re-centering shell lights (`lw = old_pos + l.position; l.position = lw - new_center`), filter out stray `(0, 0, 0)` world lights so `.ymap` `entities_extents` does not bloat toward map origin.
- **Entity Transform**: Always store `Entity(rotation=rot.inverted(), scale_xy=math.sqrt(sc.x * sc.y), scale_z=sc.z)` when converting from Blender `matrix_world.decompose()`.
- **Keep Stream Clean**: Delete replaced monolithic `.ydr`/`.ydr.xml` files from export and `stream/`, while leaving unrelated `*_grass_*.ymap` files untouched.

## 4. MCP Batching & Mandatory Stream Verification

1. **Prevent MCP 3-Minute Timeouts**:
   - Print exceptions explicitly with `traceback.print_exc(file=sys.stdout)` (Blender MCP only captures `stdout`).
   - Pre-index existing `bpy.data.images` filepaths once in an O(1) dict before shader creation.
   - For large scenes (`40+` `.ydr`s), split export execution into sliced phases (`run_stage2` + `export_range` slices + `finalize`) so each call finishes in `< 60s`.
   - For DDS visual preview, use `texconv.exe -ft png` (see skill `codewalker-cli-and-water`), not Blender `img.save()`.
2. **Run Post-Deploy Stream Audit + CodeWalker CLI Check**:
   - Execute [audit_stream.py](./scripts/audit_stream.py) in Blender against the target `stream/` and `dds/` folders and confirm:
     - `0` embedded textures across all `.ydr`s
     - `0` `trees*.sps`, `trees_shadow_proxy.sps`, or `cpv_only.sps` shaders
     - `0` missing `TexCoord0` on textured geometries and `0` empty texture samplers (outside `water_poolenv.sps`)
     - `0` missing textures referenced by `.ydr` shaders inside their assigned `.ytd`
     - Every `.ytd` raw uncompressed DDS total `< 16 MB`
     - `0` `physics_dictionary` mismatches, `0` zero-flag (`type=0`/`include=0`) BVH composite bounds, and `0` stray origin lights
     - Tight `.ymap` `entities_extents`, 100% MD5 parity between `EXPORT/` and `stream/`, and clean `codewalker-cli` `xml export`/`xml import` roundtrip (see `codewalker-cli-and-water`).
