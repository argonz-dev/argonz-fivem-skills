# ARGONZ FiveM & Blender Pipeline Skills

Bundle skills Antigravity (Plugin + Git repo) untuk pipeline lengkap **desain 3D low-poly -> tekstur PBR & DDS -> export Sollumz / `szio.gta5` -> verifikasi `codewalker-cli` -> setup `water.xml`** untuk GTA V / FiveM.

## Isi Repository

```text
argonz-fivem-skills/
├── plugin.json
├── README.md
└── skills/
    ├── gta5-fivem-sollumz-export/
    │   ├── SKILL.md
    │   ├── references/szio-api-gotchas.md
    │   └── scripts/
    │       ├── audit_stream.py
    │       └── preserve_mat_indices.py
    ├── blender-lowpoly-arch-imagegen/
    │   └── SKILL.md
    └── codewalker-cli-and-water/
        ├── SKILL.md
        └── references/water-xml-and-texconv.md
```

## Ringkasan Skill

1. **`gta5-fivem-sollumz-export`**
   - Split drawable per zona/props, `0` embedded textures, shared `.ytd` `< 16 MB`, normalisasi `UVMap 0`, vertex color `Colour0` (`G=0`), static cutout foliage, BVH composite flags, selective `.ydr` rebuild via `mtime`, proteksi agar `material_index` tidak kolaps ke slot `0`, dan audit `stream/` lewat [audit_stream.py](skills/gta5-fivem-sollumz-export/scripts/audit_stream.py).
2. **`blender-lowpoly-arch-imagegen`**
   - Workflow desain ulang / pembuatan aset arsitektur `< 1000` tris tanpa bevel berbasis `bmesh`, tekstur PBR seamless via subagen `image-generator`, normalisasi `UVMap 0` / shader node, dan verifikasi render.
3. **`codewalker-cli-and-water`**
   - Verifikasi binary `.ydr`/`.ytd`/`.ytyp`/`.ymap` lewat `codewalker-cli.exe` (`xml export` + deteksi `hash_` + `xml import` roundtrip), konversi PNG <-> DDS via `texconv.exe`, serta konfigurasi `<CalmingQuads>`, `<WaveQuads>`, dan `<WaterQuads>` pada `water.xml`.

## Pemasangan ke Antigravity

Clone repo ini lalu tautkan (junction/symlink) atau salin ke direktori plugin global Antigravity (`~/.gemini/config/plugins/argonz-fivem-skills`):

```powershell
New-Item -ItemType Junction -Path "$env:USERPROFILE\.gemini\config\plugins\argonz-fivem-skills" -Target "D:\Blender\argonz-fivem-skills"
```
