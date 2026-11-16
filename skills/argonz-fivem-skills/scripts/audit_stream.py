"""Verify GTA V / FiveM stream folder (.ydr, .ytd, .ytyp, .ymap) via szio.gta5."""

from pathlib import Path
import sys
import traceback
import szio.gta5 as g5


def audit_stream(stream_dir: str | Path, dds_dir: str | Path | None = None):
    stream = Path(stream_dir)
    dds = Path(dds_dir) if dds_dir else None

    ytd_map: dict[str, set[str]] = {}
    for p in sorted(stream.glob("*.ytd")):
        td = g5.try_load_asset(p)
        ytd_map[p.stem] = set(td.textures.keys())
        raw_str = ""
        if dds and dds.exists():
            raw_mb = sum((dds / f"{t}.dds").stat().st_size for t in td.textures if (dds / f"{t}.dds").exists()) / 1048576.0
            raw_str = f", raw_dds={raw_mb:.2f} MB (<16MB={raw_mb < 16.0})"
        print(f"YTD {p.name:26s}: disk={p.stat().st_size / 1024:.1f} KB, tex={len(td.textures)}{raw_str}")

    ytyp_files = sorted(stream.glob("*.ytyp"))
    ymap_files = [p for p in sorted(stream.glob("*.ymap")) if "_grass_" not in p.name]
    assert ytyp_files, f"No .ytyp found in {stream}"
    assert ymap_files, f"No non-grass .ymap found in {stream}"

    ytyp = g5.try_load_asset(ytyp_files[0])
    ymap = g5.try_load_asset(ymap_files[0])
    print(f"YTYP={ytyp_files[0].name} ({len(ytyp.archetypes)} archs), YMAP={ymap_files[0].name} ({len(ymap.entities)} ents)")

    errors = []
    for a in ytyp.archetypes:
        yp = stream / f"{a.name}.ydr"
        if not yp.exists():
            errors.append(f"Missing YDR: {yp.name}")
            continue
        drw = g5.try_load_asset(yp)
        if drw.shader_group.embedded_textures:
            errors.append(f"Embedded textures in {a.name}: {len(drw.shader_group.embedded_textures)}")
        has_b = drw.bounds is not None
        if (has_b and a.physics_dictionary != a.name) or (not has_b and a.physics_dictionary != ""):
            errors.append(f"Physics dict mismatch {a.name}: dict={a.physics_dictionary!r} has_bounds={has_b}")
        if has_b:
            for ci, c in enumerate(getattr(drw.bounds, "children", [])):
                if (
                    int(getattr(c, "composite_collision_type_flags", 0)) == 0
                    or int(getattr(c, "composite_collision_include_flags", 0)) == 0
                ):
                    errors.append(f"Zero collision flags on {a.name} bound[{ci}]")

        txd_set = ytd_map.get(a.texture_dictionary)
        if txd_set is None:
            errors.append(f"Missing YTD {a.texture_dictionary!r} for {a.name}")
            txd_set = set()

        for sh in drw.shader_group.shaders:
            pf = sh.preset_filename
            if "trees" in pf or pf in ("trees_shadow_proxy.sps", "cpv_only.sps"):
                errors.append(f"Forbidden wind/proxy shader in {a.name}: {pf}")
            for prm in sh.parameters:
                v = getattr(prm, "value", None)
                if isinstance(v, str) and v and v not in txd_set:
                    errors.append(f"Missing texture {v!r} in {a.texture_dictionary} (used by {a.name})")

        for models in drw.models.values():
            for m in models:
                for gi, g in enumerate(m.geometries):
                    vb = g.vertex_buffer
                    names = vb.dtype.names
                    sh_obj = drw.shader_group.shaders[g.shader_index]
                    pf = sh_obj.preset_filename
                    if "Colour0" in names:
                        if int(vb["Colour0"][:, 1].max()) > 1:
                            errors.append(f"Non-zero Green vertex color in {a.name}[{gi}] ({pf})")
                    elif pf != "water_poolenv.sps":
                        errors.append(f"Missing Colour0 in {a.name}[{gi}] ({pf})")
                    uses_tex = any(isinstance(getattr(pr, "value", None), str) and pr.value for pr in sh_obj.parameters)
                    if not uses_tex and pf != "water_poolenv.sps":
                        errors.append(f"Empty texture samplers in {a.name}[{gi}] ({pf})")
                    elif uses_tex and "TexCoord0" not in names:
                        errors.append(f"Missing TexCoord0 in {a.name}[{gi}] ({pf})")

    custom_names = {a.name for a in ytyp.archetypes}
    vanilla_cnt = sum(1 for e in ymap.entities if e.archetype_name not in custom_names)
    print(f"Custom entities: {len(ymap.entities) - vanilla_cnt}, Vanilla entities: {vanilla_cnt}")
    print(f"YMAP entities_extents: {ymap.entities_extents}")
    print(f"Errors ({len(errors)}):", errors[:15])
    return errors


if __name__ == "__main__":
    try:
        if len(sys.argv) >= 2:
            audit_stream(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else None)
    except Exception:
        traceback.print_exc(file=sys.stdout)
