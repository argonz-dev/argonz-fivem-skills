---
name: blender-lowpoly-arch-imagegen
description: >-
  Workflow for building or redesigning optimized low-poly architectural 3D assets
  (<1000 tris, single mesh, no bevels) in Blender via MCP using seamless
  image-generated PBR textures for tiling, stone, slats, and fluted panels.
---

# Low-Poly Architectural Asset & Image-Gen Texture Workflow

Build game/scene-ready architectural assets (gates, perimeter walls, facades, stepping stones) in Blender via MCP with `<1000 tris`, zero bevels, seamless AI-generated textures, and single-mesh consolidation.

## Core Rules

1. **Under 1,000 Tris**: Build directly in `bmesh` with clean axis-aligned boxes/low-segment primitives (`8–12` segs max for cylinders). Omit buried bottom faces (`skip_bottom=True`).
2. **No Bevels**: Never add `BEVEL` modifiers or beveled edge geometry.
3. **Texture Over Geometry for Tiling**: Never stack multiple cubes for stone courses, louvers, wood slats, panel reveals, or wall flutes. Model 1 box/face and represent repetition with seamless textures.
4. **Image-Generated Seamless Textures**: Always generate dedicated seamless textures via `image-generator` subagents (in parallel) for each non-uniform surface material.
5. **Single-Mesh Consolidation & Safe Slot Edits**: Combine structural parts into cohesive multi-slot meshes, and **never** call `obj.data.materials.clear()` on existing multi-material meshes (wipes polygon `material_index` back to `0`). Swap slots in-place instead.

## Workflow

### 1. Inspect Reference & Scene Context
- Query target/neighbor objects (`get_scene_info`, `execute_blender_code`) for world location, rotation (`rotZ`), ground `min_z`, bounding dimensions, and collection membership.
- If redesigning a subset inside a combined mesh (e.g., marble stepping stones inside an exterior facade mesh), identify polygons strictly by local bounds + slot index so sibling elements keep their original materials and UVs.
- Check `space.local_view` in `VIEW_3D` before taking viewport checks.

### 2. Generate Seamless PBR Textures & DDS Ready Stems
Invoke `image-generator` subagents in parallel for each material needed:
- **Prompt requirements**: *"seamless, tileable 2D PBR albedo texture, flat top-down orthographic view, completely even diffuse lighting, no perspective distortion, no directional shadows, no borders"*.
- For architectural accents (fluted concrete, slats, tiled stone), explicitly enforce *"strictly uniform, perfectly straight, evenly spaced"* alignment.
- Always inspect generated textures with `read_file`. Regenerate immediately if groove spacing, color cast, or seams are off.
- For FiveM / Sollumz pipelines, also prepare matching `.dds` (albedo `-f DXT1`, normal `_n` & specular `_s` via `texconv.exe`, see `codewalker-cli-and-water`).

### 3. Build Single-Mesh Geometry & Tailored UVs (`bmesh`)
- **Material Slots**: Assign clear slots (e.g., Concrete Panel, Dark Coping, Stacked Stone, Marble Step, Warm LED Emission, Black Metal Sconce, Soil).
- **Shader & UV Node Hygiene**:
  - Load each generated texture, call `img.pack()` (or link `.dds`), set `extension = 'REPEAT'`, and route `RGBToBW -> Bump -> Principled BSDF Normal`.
  - Keep active UV layer named `"UVMap 0"` for clean Sollumz export, and verify `ShaderNodeUVMap.uv_map` does not reference a non-existent UV map name (such as `"Tile Repeat"` when the mesh only has `"UVMap 0"`), otherwise EEVEE/Cycles previews evaluate UV `(0, 0)` as a flat solid color.
- **UV Projection by Role**:
  - *World-scale box projection*: stone cladding, coping, dark concrete, stucco, pool paving.
  - *Per-slab unit fit (`0..1`)*: individual marble stepping stones or decorative slabs so veining/borders frame each slab cleanly without tiling seams.
  - *Panel reveal (`V` locked `[0..1]`)*: map `Z_bot..Z_top` to `V = 0..1` so center horizontal reveal grooves align uniformly across all wall heights; scale `U` by panel length.
  - *Fixed-count accents (e.g., 4 vertical flutes)*: map panel width strictly to `U = [0..1]` so grooves stay centered and symmetrical.

### 4. Verify Triangle Budget & Visual Look
- Evaluate `evaluated_get(depsgraph)` + `calc_loop_triangles()` and confirm `< 1000` tris.
- Render clean EEVEE/viewport previews (temporarily hiding `EXPORT*` and collision helper meshes, then restoring scene state cleanly without leaving dummy cameras/lights) and inspect every output image with `read_file`.
- Verify outer/back faces for unwanted pier protrusions (tuck non-protruding outer pier faces `2cm` inside outer wall planes).
