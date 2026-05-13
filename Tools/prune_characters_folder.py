"""Prune vendor art under Assets/FightGame: delete assets not reachable from game content."""
import os
import re
import shutil
from collections import deque

ASSETS = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", "Assets"))
FIGHT = os.path.normpath(os.path.join(ASSETS, "FightGame"))
VENDOR_DIRS = tuple(
    os.path.normpath(os.path.join(FIGHT, name))
    for name in ("Bandits - Pixel Art", "Hero Knight - Pixel Art")
)
GUID_RE = re.compile(r"guid: ([a-f0-9]{32})(?:,|$)")
SKIP_GUIDS = {
    "0000000000000000f000000000000000",
    "0000000000000000e000000000000000",
}


def under_vendor(dirpath):
    n = os.path.normpath(dirpath)
    for v in VENDOR_DIRS:
        if n == v or n.startswith(v + os.sep):
            return True
    return False


def build_guid_index():
    guid_to_meta = {}
    for dirpath, _, filenames in os.walk(ASSETS):
        for fn in filenames:
            if not fn.endswith(".meta"):
                continue
            fp = os.path.join(dirpath, fn)
            try:
                with open(fp, encoding="utf-8", errors="replace") as f:
                    lines = f.readlines()[:8]
                for line in lines:
                    if line.startswith("guid: "):
                        guid_to_meta[line.split()[1].strip()] = fp
                        break
            except OSError:
                pass
    return guid_to_meta


def asset_path_from_meta(meta_fp):
    return meta_fp[:-5]


def guids_in_file(path):
    try:
        with open(path, encoding="utf-8", errors="replace") as f:
            txt = f.read()
    except OSError:
        return set()
    return {m.group(1) for m in GUID_RE.finditer(txt) if m.group(1) not in SKIP_GUIDS}


def collect_closure(index):
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
    seeds = []
    for dirpath, _, filenames in os.walk(FIGHT):
        if under_vendor(dirpath):
            continue
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
    return seen


def read_guid(meta_fp):
    try:
        with open(meta_fp, encoding="utf-8", errors="replace") as f:
            for line in f.readlines()[:10]:
                if line.startswith("guid: "):
                    return line.split()[1].strip()
    except OSError:
        pass
    return None


def prune_tree(char_root, keep_guids):
    removed = []
    if not os.path.isdir(char_root):
        return removed
    for dirpath, _, filenames in os.walk(char_root):
        for fn in filenames:
            if fn.endswith(".meta"):
                continue
            asset_fp = os.path.join(dirpath, fn)
            if not os.path.isfile(asset_fp):
                continue
            meta_fp = asset_fp + ".meta"
            if not os.path.isfile(meta_fp):
                continue
            guid = read_guid(meta_fp)
            if not guid or guid in keep_guids:
                continue
            os.remove(asset_fp)
            os.remove(meta_fp)
            removed.append(asset_fp)

    for dirpath, dirnames, filenames in os.walk(char_root, topdown=False):
        for fn in list(filenames):
            if not fn.endswith(".meta"):
                continue
            meta_fp = os.path.join(dirpath, fn)
            folder = meta_fp[:-5]
            if os.path.isdir(folder) and not os.path.islink(folder):
                try:
                    if not os.listdir(folder):
                        os.rmdir(folder)
                        os.remove(meta_fp)
                except OSError:
                    pass
        try:
            if dirpath != char_root and not os.listdir(dirpath):
                os.rmdir(dirpath)
                m = dirpath + ".meta"
                if os.path.isfile(m):
                    os.remove(m)
        except OSError:
            pass

    if os.path.isdir(char_root) and not os.listdir(char_root):
        shutil.rmtree(char_root, ignore_errors=True)
        cm = char_root + ".meta"
        if os.path.isfile(cm):
            os.remove(cm)
    return removed


def main():
    index = build_guid_index()
    keep_guids = collect_closure(index)
    roots = [r for r in VENDOR_DIRS if os.path.isdir(r)]
    if not roots:
        print("No vendor folders under FightGame, skip")
        return
    all_removed = []
    for r in roots:
        all_removed.extend(prune_tree(r, keep_guids))
    for p in sorted(all_removed):
        print("removed:", p)


if __name__ == "__main__":
    main()
