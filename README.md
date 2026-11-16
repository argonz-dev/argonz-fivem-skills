# ARGONZ FiveM & Blender Pipeline Skills

Bundle skills Antigravity (Plugin + Git repo) yang menyatukan seluruh pipeline **desain 3D low-poly -> tekstur PBR & DDS -> export Sollumz / `szio.gta5` -> pintu/gerbang animasi rel (`.ydr` + `.ycd` + tombol + Lua) -> verifikasi `codewalker-cli` & headless 3D -> setup `water.xml`** ke dalam **1 skill terpadu (`argonz-fivem-skills`)**.

## Isi Repository

```text
argonz-fivem-skills/
├── plugin.json
├── README.md
├── tools/
│   └── CodeWalkerCLI/
│       ├── codewalker-cli.exe
│       ├── CodeWalker.Core.dll
│       ├── SharpDX.dll / SharpDX.Mathematics.dll
│       ├── Program.cs / Build.ps1 / strings.txt
└── skills/
    └── argonz-fivem-skills/
        ├── SKILL.md
        ├── references/
        │   ├── animated-sectional-doors-and-switches.md
        │   ├── codewalker-headless-bridge.md
        │   ├── szio-api-gotchas.md
        │   └── water-xml-and-texconv.md
        └── scripts/
            ├── audit_stream.py
            └── preserve_mat_indices.py
```

## Cakupan dalam 1 Perintah (`/argonz-fivem-skills`)

1. **Low-Poly Arch & ImageGen PBR (`bmesh`)**: `< 1000` tris tanpa bevel, tekstur seamless via `image-generator`, normalisasi `UVMap 0`, proteksi `material_index`.
2. **Sollumz & `szio.gta5` Split Export**: Split `.ydr` per zona/props, `0` embedded textures, shared `.ytd` `< 16 MB`, vertex color `Color 1` (`G=0`), static cutout foliage, `CompositeFlags1`/`2`, proteksi asset vanila GTA (`0 KB`), dan audit stream lewat [audit_stream.py](skills/argonz-fivem-skills/scripts/audit_stream.py).
3. **Animated Sectional Doors & Switches**: Rig bone identity (`+Y`, `roll=0`), `RotX..TransZ` flags, lintel clearance + chord tracking, dual open/close `.ycd` + injeksi `<Hash>` clip untuk `codewalker-cli`, tombol exterior (`.ymap`) + interior (`.ytyp` MLO), dan script FiveM Lua ([animated-sectional-doors-and-switches.md](skills/argonz-fivem-skills/references/animated-sectional-doors-and-switches.md)).
4. **CodeWalker CLI, Headless 3D (`cw_agent.py`), `texconv`, & `water.xml`**: Roundtrip `xml export`/`xml import`, DX11 headless screenshots, PNG ↔ DDS, dan `<CalmingQuads>` ([codewalker-headless-bridge.md](skills/argonz-fivem-skills/references/codewalker-headless-bridge.md), [water-xml-and-texconv.md](skills/argonz-fivem-skills/references/water-xml-and-texconv.md)).

## Pemasangan ke Antigravity

Salin isi repo (`plugin.json`, `README.md`, `skills/`) ke direktori plugin global Antigravity (`~/.gemini/config/plugins/argonz-fivem-skills`):

```powershell
$dst = "$env:USERPROFILE\.gemini\config\plugins\argonz-fivem-skills"
New-Item -ItemType Directory -Force -Path $dst | Out-Null
Copy-Item "D:\Blender\argonz-fivem-skills\plugin.json","D:\Blender\argonz-fivem-skills\README.md" $dst -Force
Remove-Item "$dst\skills" -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item "D:\Blender\argonz-fivem-skills\skills" $dst -Recurse -Force
```
