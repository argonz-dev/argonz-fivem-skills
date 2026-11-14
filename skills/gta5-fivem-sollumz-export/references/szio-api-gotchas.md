# `szio.gta5` & Sollumz API Cheatsheet

Exact verified signatures and attribute names for Blender 5.3 + Sollumz (`szio.gta5`).

## 1. Shader Creation & Textures

```python
from bl_ext.repo_sollumz_org.sollumz.ydr.shader_materials import create_shader
import bl_ext.repo_sollumz_org.sollumz.ytd.ytdexport as ye
from szio.gta5 import AssetFormat, AssetTarget, AssetTextureDictionary, AssetVersion, EmbeddedTexture, save_asset
from szio.types import DataSource

# Canonical Sollumz material creation (do NOT use shader_materials_v2.create_shader)
mat = create_shader("normal_spec.sps")
for nd in mat.node_tree.nodes:
    if nd.type == "TEX_IMAGE":
        nd.image = loaded_dds_img   # Sets read-only nd.sollumz_texture_name automatically
        nd.texture_properties.embedded = False

# Un-embed existing .ydr in-place
drw.shader_group.embedded_textures = {}  # NOT .textures or .texture_dictionary

# Build shared .ytd
targets = [AssetTarget(AssetFormat.NATIVE, AssetVersion.GEN8)]
textures = {}
for stem in sorted(tex_stems):
    dds_p = dds_dir / f"{stem}.dds"
    raw = bytearray(dds_p.read_bytes())
    info = ye.DdsFile.from_buffer(raw)
    textures[stem] = EmbeddedTexture(stem, info.width, info.height, DataSource.create(bytes(raw), dds_p.name))
save_asset(AssetTextureDictionary(textures=textures), targets, out_dir, ytd_name)
```

## 2. Inspecting Exported `.ydr` / `.ytd` via `szio.gta5`

- `drw.models` is a `dict[LodLevel, list[Model]]` (`lod.name == "HIGH"`).
- `Geometry` uses `g.shader_index` (not `.shader`), `g.index_buffer` (`len(g.index_buffer) // 3` tris, not `.indices`), and structured numpy `g.vertex_buffer` with capitalized column names (`'Position'`, `'Normal'`, `'Colour0'`, `'TexCoord0'`, `'Tangent'`).
- `ShaderInst` uses `s.preset_filename` (not `.filename`) and `s.parameters`, where each `ShaderParameter` has `.name` and `.value` (not `.texture_name`).
- `AssetTextureDictionary.textures` is already a `dict[str, EmbeddedTexture]`, so iterate `ytd.textures.keys()`.

## 3. Static Cutout Foliage Shader Remap

- Strip entirely: `trees_shadow_proxy.sps`, `cpv_only.sps`
- Wind-to-static mapping:
  - `trees.sps` / `trees_tnt.sps` -> `cutout_tnt.sps` (or `cutout.sps`)
  - `trees_normal.sps` / `trees_normal_diffspec.sps` / `trees_normal_diffspec_tnt.sps` -> `normal_cutout.sps` / `normal_spec_cutout.sps` / `normal_spec_cutout_tnt.sps`

## 4. Exact `szio.gta5.Light` Fields

- Import `Light`, `LightFlashiness`, `LightType` strictly from **`szio.gta5`** (NOT `sollumz_properties.LightType`, which returns string enums).
- `flags` is plain `int` (there is no `LightFlags` class in `szio.gta5`).
- Field is `extent` (singular `Vector((1.0, 1.0, 1.0))` for point/spot; `X = length` for `LightType.CAPSULE`), plus `shadow_blur: int = 0` (no `unknown_45` / `unknown_46`).

```python
from mathutils import Vector
from szio.gta5 import Light, LightFlashiness, LightType

light = Light(
    position=Vector((0.0, 0.0, 0.09)),
    direction=Vector((0.0, 0.0, -1.0)),
    tangent=Vector((1.0, 0.0, 0.0)),
    extent=Vector((1.0, 1.0, 1.0)),
    color=(255, 178, 86),
    flashiness=LightFlashiness.CONSTANT,
    intensity=2.2,
    flags=0,
    time_flags=16777215,
    bone_id=0,
    group_id=0,
    light_hash=0,
    falloff=5.5,
    falloff_exponent=22.0,
    culling_plane_normal=Vector((0.0, 0.0, 0.0)),
    culling_plane_offset=0.0,
    volume_intensity=0.0,
    volume_size_scale=1.0,
    volume_outer_color=(255, 178, 86),
    volume_outer_intensity=0.0,
    volume_outer_exponent=0.0,
    corona_size=0.0,
    corona_intensity=0.0,
    corona_z_bias=0.1,
    projected_texture_hash="",
    light_fade_distance=0,
    shadow_fade_distance=0,
    specular_fade_distance=0,
    volumetric_fade_distance=0,
    shadow_near_clip=0.1,
    shadow_blur=0,
    cone_inner_angle=0.0,
    cone_outer_angle=0.0,
    light_type=LightType.POINT,
)
```

## 5. `AssetDrawable` Bounds, `.ytyp` & `.ymap`

- `AssetDrawable` has no `bounds_min/max/center/radius`; compute from `drw.models`:
  ```python
  pts = np.vstack([
      g.vertex_buffer["Position"]
      for models in drw.models.values()
      for m in models
      for g in m.geometries
      if len(g.vertex_buffer["Position"])
  ])
  mn, mx = pts.min(axis=0), pts.max(axis=0)
  bb_min = Vector((float(mn[0]), float(mn[1]), float(mn[2])))
  bb_max = Vector((float(mx[0]), float(mx[1]), float(mx[2])))
  bs_center = (bb_min + bb_max) * 0.5
  bs_radius = float(np.sqrt(((pts - np.array([bs_center.x, bs_center.y, bs_center.z])) ** 2).sum(axis=1)).max())
  has_bounds = drw.bounds is not None
  ```
- **Static archetype**: `flags=32`, `special_attribute=0`, `EntityFlags.IS_STATIC`.
- **Interactable archetype (sliding gate)**: `flags=67239936`, `special_attribute=8`, `EntityFlags(0)`.
- **Physics dict**: `physics_dictionary=arch_name if has_bounds else ""`.
- **BVH Composite Collision Flags (CRITICAL)**: Newly created `SollumType.BOUND_GEOMETRYBVH` objects default `composite_flags1` / `composite_flags2` to all `False` (`0`), causing players/vehicles to fall straight through! Always set static map flags on `bvh_empty`:
  - `composite_flags1` (`62`): `map_weapon`, `map_dynamic`, `map_animal`, `map_cover`, `map_vehicle` = `True`
  - `composite_flags2` (`133414592`): `vehicle_not_bvh`, `vehicle_bvh`, `ped`, `ragdoll`, `animal`, `animal_ragdoll`, `object`, `plant`, `projectile`, `explosion`, `forklift_forks`, `test_weapon`, `test_camera`, `test_ai`, `test_script`, `test_vehicle_wheel`, `glass` = `True`
- **Loose-vertex pruning**: After `bmesh.ops.delete(..., context="FACES")` (polygon subsets or skipping `skip_col` faces), delete orphan vertices with `not v.link_faces` (`context="VERTS"`) before converting to collision mesh so `calc_bound_bvh_extents` stays tight.
- **Entity transform (`ymapexport` parity)**: `rotation=rot.inverted()`, `scale_xy=math.sqrt(max(sc.x * sc.y, 1e-6))`, `scale_z=float(sc.z)`.
- **YMAP extents**: Set `AssetMapData.flags = old_ymap.flags` (`MapFlags(0)`), compute tight `entities_extents` over transformed local `bb_min..bb_max` corners, and pad `streaming_extents` by `±Vector((300, 300, 300))`.
