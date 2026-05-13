"""Collect Unity asset GUID dependencies reachable from Assets/FightGame."""
import os
import re
from collections import deque

ASSETS = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", "Assets"))
GUID_RE = re.compile(r"guid: ([a-f0-9]{32})(?:,|$)")


def build_guid_index():
    guid_to_meta = {}
    for dirpath, _, filenames in os.walk(ASSETS):
        for fn in filenames:
            if not fn.endswith(".meta"):
                continue
            fp = os.path.join(dirpath, fn)
            try:
                with open(fp, encoding="utf-8", errors="replace") as f:
                    lines = f.readlines()[:6]
                for line in lines:
                    if line.startswith("guid: "):
                        g = line.split()[1].strip()
                        guid_to_meta[g] = fp
                        break
            except OSError:
                pass
    return guid_to_meta


def asset_path_from_meta(meta_fp):
    return meta_fp[:-5] if meta_fp.endswith(".meta") else meta_fp


def guids_in_file(path):
    try:
        with open(path, encoding="utf-8", errors="replace") as f:
            txt = f.read()
    except OSError:
        return set()
    skip = {
        "0000000000000000f000000000000000",
        "0000000000000000e000000000000000",
    }
    return {m.group(1) for m in GUID_RE.finditer(txt) if m.group(1) not in skip}


def main():
    index = build_guid_index()
    seeds = []
    fight = os.path.join(ASSETS, "FightGame")
    exts = (
        ".unity",
        ".prefab",
        ".asset",
        ".controller",
        ".overrideController",
        ".anim",
        ".mat",
        ".shader",
        ".physicsMaterial2D",
        ".inputactions",
        ".spriteatlas",
    )
    if os.path.isdir(fight):
        for dirpath, _, filenames in os.walk(fight):
            for fn in filenames:
                if fn.endswith(exts):
                    seeds.append(os.path.join(dirpath, fn))
    res = os.path.join(ASSETS, "Resources")
    if os.path.isdir(res):
        for dirpath, _, filenames in os.walk(res):
            for fn in filenames:
                if fn.endswith((".prefab", ".asset", ".wav", ".mp3", ".ogg")):
                    seeds.append(os.path.join(dirpath, fn))

    seen = set()
    q = deque()
    for fp in seeds:
        for g in guids_in_file(fp):
            if g not in seen:
                seen.add(g)
                q.append(g)

    while q:
        g = q.popleft()
        meta = index.get(g)
        if not meta:
            continue
        asset = asset_path_from_meta(meta)
        if os.path.isfile(asset):
            for ng in guids_in_file(asset):
                if ng not in seen:
                    seen.add(ng)
                    q.append(ng)

    prefixes = tuple(
        os.path.normpath(os.path.join(ASSETS, "FightGame", name))
        for name in ("Bandits - Pixel Art", "Hero Knight - Pixel Art")
    )
    needed = []
    for g in seen:
        meta = index.get(g)
        if not meta:
            continue
        ap = os.path.normpath(asset_path_from_meta(meta))
        for pfx in prefixes:
            if ap.startswith(pfx + os.sep) or ap == pfx:
                needed.append(ap)
                break

    for p in sorted(set(needed)):
        print(p)


if __name__ == "__main__":
    main()
