"""Snapshot & restore polygon material_index maps across multi-material Blender meshes.

Prevents silent collapse to material_index == 0 when slots were cleared or rebuilt.
Run inside Blender:
  blender -b clean_backup.blend -P preserve_mat_indices.py -- --snapshot mats.json
  blender -b edited.blend       -P preserve_mat_indices.py -- --restore  mats.json --skip ObjA ObjB
"""

import argparse
import json
import sys
import bpy


def snapshot(out_path: str):
    """Dump polygon material_index arrays for all multi-material meshes."""
    data = {}
    for obj in bpy.data.objects:
        if obj.type != "MESH" or not obj.data.polygons or len(obj.material_slots) <= 1:
            continue
        idxs = [int(p.material_index) for p in obj.data.polygons]
        if len(set(idxs)) <= 1 and idxs[0] == 0:
            continue
        data[obj.name] = {
            "npolys": len(obj.data.polygons),
            "nverts": len(obj.data.vertices),
            "slots": [s.material.name if s.material else None for s in obj.material_slots],
            "idxs": idxs,
        }
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(data, f)
    print(f"SNAPSHOT_DONE meshes={len(data)} -> {out_path}")


def restore(in_path: str, skip_names: set[str], dry_run: bool = False):
    """Restore polygon material_index values on collapsed multi-material meshes."""
    with open(in_path, "r", encoding="utf-8") as f:
        bak = json.load(f)

    restored, skipped_ok, len_diff, oob = 0, 0, [], []
    for name, b in bak.items():
        if name in skip_names:
            continue
        obj = bpy.data.objects.get(name)
        if not obj or obj.type != "MESH" or not obj.data.polygons:
            continue
        if any(c.name.startswith("EXPORT") for c in obj.users_collection):
            continue
        me = obj.data
        if len({p.material_index for p in me.polygons}) > 1 or len(obj.material_slots) <= 1:
            skipped_ok += 1
            continue
        if len(me.polygons) != b["npolys"] or len(me.vertices) != b["nverts"]:
            len_diff.append((name, (len(me.polygons), len(me.vertices)), (b["npolys"], b["nverts"])))
            continue
        if max(b["idxs"]) >= len(obj.material_slots):
            oob.append((name, max(b["idxs"]), len(obj.material_slots)))
            continue
        if not dry_run:
            me.polygons.foreach_set("material_index", b["idxs"])
            me.update()
        restored += 1

    print(
        f"RESTORE_DONE restored={restored} non_collapsed={skipped_ok} "
        f"len_diff={len(len_diff)} oob={len(oob)} dry_run={dry_run}"
    )
    for row in len_diff[:15]:
        print("  LEN_DIFF:", row)
    for row in oob[:15]:
        print("  SLOT_OOB:", row)


def main():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    ap = argparse.ArgumentParser(description="Snapshot or restore Blender polygon material indices.")
    mode = ap.add_mutually_exclusive_group(required=True)
    mode.add_argument("--snapshot", metavar="JSON")
    mode.add_argument("--restore", metavar="JSON")
    ap.add_argument("--skip", nargs="*", default=[], help="Object names to leave untouched during restore")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args(argv)
    if args.snapshot:
        snapshot(args.snapshot)
    else:
        restore(args.restore, set(args.skip), dry_run=args.dry_run)


if __name__ == "__main__":
    main()
