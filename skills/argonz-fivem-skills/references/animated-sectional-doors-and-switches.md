# Animated Sectional Doors (Rail-Following `.ydr` + `.ycd`) & Wall Switches

End-to-end production workflow for articulated overhead/sectional garage doors following a curved L-rail in Blender + Sollumz, paired with exterior/interior wall switch props and FiveM `PlayEntityAnim` scripts.

---

## 1. Blender Rigging & Skinning (`vt_gar_door` Pattern)

1. **Separate Static Frame/Rails vs. Moving Panels**:
   - Keep outer jambs, top header, and curved L-shaped guide rails bound (`weight = 1.0`) to the **root bone** (`Tag = 0`).
   - Bind each horizontal panel (+ bottom seal) to its own child bone (`seg_1`..`seg_N`, `use_connect = False`) with unique `bone.bone_properties.tag` (`1001`, `1002`, ...).
2. **CRITICAL — Identity Bone Rest Axes**:
   - **Never** point edit bones straight down (`-Z`), because Blender aligns bone `+Y` to `head -> tail`, which rotates bone local axes (`+Y_bone = -Z_armature`) and causes pose `location` keyframes to drop panels through the floor.
   - Always orient every edit bone along **`+Y` with `roll = 0`** (`b.tail = b.head + Vector((0.0, 0.25, 0.0))`) so `bone.matrix_local` has an exact **Identity 3×3 rotation** (`+X` right, `+Y` inward, `+Z` up).
3. **CRITICAL — Sollumz Bone Flags**:
   - Sollumz leaves `bone.bone_properties.flags` empty (`Unk0`) by default.
   - Populate `RotX, RotY, RotZ, TransX, TransY, TransZ` on **every** bone before export so GTA V animates both translation and rotation at runtime.
4. **Lintel Clearance & Chord-Tracked Rail Kinematics**:
   - Raycast/measure the concrete header lintel above the doorway and recess closed panels slightly behind its inner face (e.g. `local Y = +0.19` behind a lintel ending at `+0.17`) so rising panels never clip through exterior concrete.
   - Define an L-curve rail `eval_rail(s)`: vertical at `Y = PANEL_Y` up to `Z_arc_start` → `90°` circular arc (`R = 0.45–0.50m`) → horizontal ceiling rail (safely below ceiling slab and ceiling lights).
   - Chain panels top-down (`seg_1` → `seg_N`) using Euclidean chord solving (`||eval_rail(s_top) - eval_rail(s_bot)|| == seg_len`) and `alpha = atan2(y_bot - y_top, -(z_bot - z_top))` around `+X` so hinges stay locked without gaps on the curve.
5. **Skinned `.ydr` Export & Collision**:
   - Export as `SollumType.DRAWABLE` (`ARMATURE` root with `.model` and `.col` direct children, `sz_lods.get_lod(LODLevel.HIGH).mesh = data`, `HasSkin = 1`, `0` embedded textures).
   - Set `composite_flags1` (`MAP_WEAPON, MAP_DYNAMIC, MAP_ANIMAL, MAP_COVER, MAP_VEHICLE`) and `composite_flags2` (`VEHICLE_NOT_BVH..GLASS`) on `col_box`, and align `col_box` vertices to `PANEL_Y`.

---

## 2. Dual Open/Close `.ycd` & CodeWalker Clip Hash Gotcha

1. **Create Both Actions & Clips**:
   - Keyframe `*_open_action` (e.g. frames `0..60`, dense 2-frame steps) and reverse-sample `*_close_action` (`f -> FRAMES - f`).
   - Add both `ANIMATION` and `CLIP` objects under `CLIP_DICTIONARY` (`Animations` and `Clips`).
   - Set `clip_properties.name = "<name>.clip"` (do **not** prefix `pack:/`; Sollumz prepends `pack:/` automatically).
   - If recreating an `Action` via Python, re-link `anim_obj.animation_properties.action = action` before calling `ycdexport.export_ycd()`.
2. **CRITICAL — Inject `<Hash>` on `<Clips><Item>` Before `codewalker-cli xml import`**:
   - Sollumz `ycdexport.py` omits `<Hash>` inside `<Clips><Item>`. When compiling a `.ycd` with 2+ clips via `codewalker-cli.exe xml import`, CodeWalker keys `ClipMap` by `<Hash>` (`0` if omitted), causing the second clip to overwrite the first.
   - Always inject `<Hash><clip_name></Hash>` (matching `<AnimationHash>`) into every `<Clips><Item>` in the `.ycd.xml` before `codewalker-cli xml import`, then verify with `codewalker-cli xml export` that all clips survive roundtrip.

---

## 3. `.ytyp` / `.ymap` & Wall Switch Props (Exterior + Interior)

1. **Door Archetype in MLO `.ytyp`**:
   - Set `<clipDictionary>` to the `.ycd` name, `<flags value="525856" />` (`HAS_ANIM`), `<specialAttribute value="0" />` (prevents GTA V's native linear garage-door `specialAttribute="5"` from fighting `PlayEntityAnim`).
   - Expand `bbMin`/`bbMax`/`bsCentre`/`bsRadius` to encompass the merged ceiling rails.
2. **Wall Switch Prop (`vt_gar_button`)**:
   - Build 1 shared low-poly drawable (`sollumz_drawable`, `sz_lods.get_lod(LODLevel.HIGH).mesh = data`, `UVMap 0`, `Color 1`, non-embedded shared `.ytd` shaders, full `CompositeFlags1`/`2`).
   - **Exterior button**: Raycast the exterior building mesh (`*_building.model`) in world space to mount flush at shoulder height (`~1.20m` above ground), and place it in the exterior `.ymap` (avoids MLO portal culling when the garage door is closed).
   - **Interior button**: Mount flush on the interior garage wall inside the MLO room (`CMloArchetypeDef -> entities` + room `<attachedObjects>`).
   - **Rotation**: Both `.ytyp` and `.ymap` `CEntityDef` store `rot.inverted()` relative to Blender world rotation.

---

## 4. FiveM Lua Script Pattern

1. **Wall-Press Ped Animation**:
   - `TaskTurnPedToFaceCoord(ped, btnPos.x, btnPos.y, btnPos.z, 350)` → `Wait(350)`
   - `TaskPlayAnim(ped, "anim@apt_trans@garage", "gar_open_1_left", 8.0, -8.0, 1200, 48, 0.0, false, false, false)` → `Wait(650)`
   - Fallback alternatives: `mp_doorbell` / `ring_bell_b`, `anim@heists@keycard@` / `exit`.
2. **Door Playback & Collision Sync**:
   - Broadcast state via server `GlobalState` + NetEvent:
     `PlayEntityAnim(door, isOpen and openClip or closeClip, animDict, 8.0, false, true, false, 0.0, 0)` (`stayInAnim = true` holds the final open/closed pose).
   - Toggle `SetEntityCollision(door, not isOpen, true)` so vehicles pass cleanly when open and are blocked when closed.
   - On client stream-in (`< 55m`), apply `GlobalState` with `startPhase = 0.99` and `blendSpeed = 1000.0` for late-joiners.
