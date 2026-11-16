# CodeWalker Headless Agent Bridge (`ArgzAgentBridge` & `cw_agent.py`)

Reference for controlling the customized CodeWalker 3D world viewer in headless/background mode across sessions.

## 1. Architecture & Key Files

- **Modified CodeWalker Repo**: `D:\!MLO\ARGONZ\AI\CODEWALKER_BARU\UPDATE MAIN\CODEWALKER-IMPROVED-main`
  - [WorldForm.ArgzAgentBridge.cs](file:///D:/!MLO/ARGONZ/AI/CODEWALKER_BARU/UPDATE%20MAIN/CODEWALKER-IMPROVED-main/CodeWalker/WorldForm.ArgzAgentBridge.cs): TCP newline-delimited JSON server (`127.0.0.1:19889`, fallback `19889..19899`). Writes active connection metadata to `D:\!MLO\ARGONZ\AI\ARGZ_LIVE_BRIDGE\session\cw_agent_bridge.json`.
  - [DXManager.cs](file:///D:/!MLO/ARGONZ/AI/CODEWALKER_BARU/UPDATE%20MAIN/CODEWALKER-IMPROVED-main/CodeWalker/Rendering/DirectX/DXManager.cs): `ForceBackgroundRender` keeps DX11 rendering when unfocused/off-screen; `Resize()` guards `width <= 0 || height <= 0`; `RequestScreenshot()` copies directly from the DX11 swapchain backbuffer (works cleanly off-screen at `(-32000, -32000)` at `1600x900`).
  - [cw_agent.py](file:///D:/!MLO/ARGONZ/AI/CODEWALKER_BARU/UPDATE%20MAIN/CODEWALKER-IMPROVED-main/cw_agent.py): Python 3 CLI wrapper using discovery JSON or `--port`.

## 2. Launching & Building Safely

1. Build after C# changes without `--no-restore` (needed for `Microsoft.NETFramework.ReferenceAssemblies.net48`):
   ```powershell
   dotnet build "D:\!MLO\ARGONZ\AI\CODEWALKER_BARU\UPDATE MAIN\CODEWALKER-IMPROVED-main\CodeWalker\CodeWalker.csproj" -c Debug -v minimal
   ```
2. `run_command` wraps processes in a Windows Job Object when a command exits, so launch CodeWalker as a daemon (`IsDaemon: true` in `run_command`) using `-Wait`:
   ```powershell
   $bin = "D:\!MLO\ARGONZ\AI\CODEWALKER_BARU\UPDATE MAIN\CODEWALKER-IMPROVED-main\CodeWalker\bin\Debug\net48"
   Start-Process -FilePath "$bin\CodeWalker.exe" -ArgumentList "--background" -WorkingDirectory $bin -Wait
   ```
3. Wait until GTA V RPF cache + world finish initializing:
   ```powershell
   py "D:\!MLO\ARGONZ\AI\CODEWALKER_BARU\UPDATE MAIN\CODEWALKER-IMPROVED-main\cw_agent.py" launch --background --wait-world --timeout 60
   ```
   Supported CLI flags on `CodeWalker.exe`: `--background` (or `--headless`), `--agent-port=<int>`, `--agent-width=<int>`, `--agent-height=<int>`.

## 3. TCP JSON Protocol & CLI Actions

| CLI Subcommand | JSON `action` | Key Parameters & Behavior |
| --- | --- | --- |
| `status` | `status` | Returns `world_loaded`, `cache_inited`, `streaming_busy`, `rendered_frames`, `window_mode`, `camera`, `time_of_day`, `weather`. |
| `set-camera` | `set_camera` | `position` `[x,y,z]`, `look_at` `[tx,ty,tz]` or `direction` `[dx,dy,dz]` or `rotation_deg` `[yaw,pitch]`, `fov_deg`, `distance`. |
| `move` | `move_camera` | Relative `forward`, `right`, `up`, `yaw_delta_deg`, `pitch_delta_deg`; optional `out_path` to capture after move. |
| `look` | `look` | Moves camera, waits for initial `argzRcPending`/`argzFcPending` quiet (`wait_stream_ms`), calls `Renderer.RefreshInteriorTextures(false)`, settles (`settle_frames`), captures PNG to `out_path`. |
| `fly` | `fly_path` | Array of waypoints (`name`, `position`, `look_at`/`direction`, `fov_deg`, optional per-waypoint `wait_stream_ms`/`settle_frames`) saved as `01_<name>.png`, `02_<name>.png`, ... in `out_dir`. |
| `raycast` | `raycast` | Casts collision ray from `origin` along `direction` (`max_dist`); returns `hit`, `hit_position`, `hit_normal`, `hit_distance`, `hit_entity`, `hit_ymap`. |
| `env` | `set_environment` | `time_of_day` (`0..1440`), `weather` (`EXTRASUNNY`, `CLEAR`, ...), `collision` (`0`/`1`), `water` (`0`/`1`), `entities` (`0`/`1`). |
| `window` | `set_window_mode` | Switch live between `background` (`(-32000,-32000)`) and `normal` onscreen window. |
| `load-files` | `load_files` | Accepts file paths or folder paths in `files`; collects recursively, sorts `.ytd` -> `.ydr`/`.ydd`/`.yft` -> `.ybn` -> `.ytyp` -> `.ymap`, opens headless `ProjectForm`, bumps `ArgzSyncGen`, returns `loaded_count`. |

## 4. Finding Target Coordinates from `.ymap` & `.ytyp` Before Flying

Use `CodeWalker.Core.dll` + `SharpDX.Mathematics.dll` to inspect custom placement & MLO room world coordinates quickly in PowerShell:

```powershell
$bin = "D:\!MLO\ARGONZ\AI\CODEWALKER_BARU\UPDATE MAIN\CODEWALKER-IMPROVED-main\CodeWalker\bin\Debug\net48"
Add-Type -Path "$bin\CodeWalker.Core.dll"
Add-Type -Path "$bin\SharpDX.Mathematics.dll"
$streamDir = "D:\!MLO\ARGONZ\SERVER_ARGONZ\server1\resources\[assets]\vila_tepanni\stream"

# Resolve filenames into JenkIndex (use -LiteralPath for paths containing [brackets]!)
Get-ChildItem -LiteralPath $streamDir -File | ForEach-Object {
    $null = [CodeWalker.GameFiles.JenkIndex]::Ensure([IO.Path]::GetFileNameWithoutExtension($_.Name).ToLowerInvariant())
}

# Print YMAP entity world positions
$ymap = New-Object CodeWalker.GameFiles.YmapFile
$ymap.Load([IO.File]::ReadAllBytes((Join-Path $streamDir "vila_tepani_gate_ymap.ymap")))
$ymap.AllEntities | Select-Object -First 15 | ForEach-Object {
    $name = [CodeWalker.GameFiles.JenkIndex]::TryGetString($_.CEntityDef.archetypeName)
    "{0,-32} pos=({1:F2}, {2:F2}, {3:F2})" -f $name, $_.Position.X, $_.Position.Y, $_.Position.Z
}

# Compute world centers of MLO rooms from MILO YMAP + interior YTYP
$miloMap = New-Object CodeWalker.GameFiles.YmapFile
$miloMap.Load([IO.File]::ReadAllBytes((Join-Path $streamDir "vila_tepani_int_milo.ymap")))
$miloEnt = $miloMap.AllEntities[0]
$ytyp = New-Object CodeWalker.GameFiles.YtypFile
$ytyp.Load([IO.File]::ReadAllBytes((Join-Path $streamDir "vila_tepani_int.ytyp")))
$mlo = $ytyp.AllArchetypes | Where-Object { $_ -is [CodeWalker.GameFiles.MloArchetype] } | Select-Object -First 1
foreach ($r in $mlo.rooms) {
    $localCenter = ($r.BBMin + $r.BBMax) * 0.5
    $worldCenter = $miloEnt.Position + [CodeWalker.QuaternionExtension]::Multiply($miloEnt.Orientation, $localCenter)
    "{0,-20} center=({1:F2}, {2:F2}, {3:F2})" -f $r.Name, $worldCenter.X, $worldCenter.Y, $worldCenter.Z
}
```

## 5. Gotchas

- **Python command**: Invoke with `py`, not `python`.
- **Brackets in FiveM paths**: Always use PowerShell `-LiteralPath` when a resource folder includes `[assets]` or similar category brackets.
- **Occluded shots**: Inspect every captured PNG with `read_file`; outdoor parasols, palms, and walls near computed coordinates can block the camera — adjust camera offset toward open space/sand and re-capture with `cw_agent.py look`.
- **Port conflict**: Port `9889` is used by Blender; `ArgzAgentBridge` defaults to `19889` (`19889..19899`).
