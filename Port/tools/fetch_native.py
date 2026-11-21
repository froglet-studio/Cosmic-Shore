#!/usr/bin/env python3
"""
Fetch the vendor native libraries the port calls directly (FMOD Studio) out of Git LFS.

The repository stores them as LFS objects, and a clone made without git-lfs only has the
pointer files. This reads each pointer under the project's Assets/, asks the remote's LFS
batch endpoint for a download URL, downloads the object, verifies its SHA-256 against the
pointer, and writes it under Port/.native/<platform>/ (gitignored). Assets/ is never written.

    python3 Port/tools/fetch_native.py            # Linux x86_64 (the default)
    python3 Port/tools/fetch_native.py --platform win-x64
    python3 Port/tools/fetch_native.py --check    # exit 1 unless every library is present and valid
"""
import argparse, hashlib, json, os, re, subprocess, sys, urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
assert os.path.isdir(os.path.join(ROOT, "Assets")), f"ROOT {ROOT} has no Assets/"
PORT = os.path.join(ROOT, "Port")

PLATFORMS = {
    "linux-x64": ("Assets/Plugins/FMOD/platforms/linux/lib/x86_64", ["libfmodstudio.so"]),
    "win-x64": ("Assets/Plugins/FMOD/platforms/win/lib/x86_64", ["fmodstudio.dll"]),
}

POINTER = re.compile(r"^version https://git-lfs\.github\.com/spec/v1\noid sha256:([0-9a-f]{64})\nsize (\d+)\n?$")


def read_pointer(path):
    with open(path, "rb") as f:
        head = f.read(400)
    try:
        m = POINTER.match(head.decode("ascii"))
    except UnicodeDecodeError:
        return None  # already the real binary
    return (m.group(1), int(m.group(2))) if m else None


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def lfs_endpoint():
    url = subprocess.check_output(["git", "-C", ROOT, "remote", "get-url", "origin"], text=True).strip()
    if not url.endswith(".git"):
        url += ".git"
    return url + "/info/lfs/objects/batch"


def download_urls(objects):
    body = json.dumps({"operation": "download", "transfers": ["basic"],
                       "objects": [{"oid": o, "size": s} for o, s in objects]}).encode()
    req = urllib.request.Request(lfs_endpoint(), data=body, method="POST", headers={
        "Accept": "application/vnd.git-lfs+json", "Content-Type": "application/vnd.git-lfs+json"})
    with urllib.request.urlopen(req, timeout=120) as r:
        reply = json.load(r)
    out = {}
    for o in reply.get("objects", []):
        if "error" in o:
            raise SystemExit(f"LFS refused {o['oid']}: {o['error']}")
        out[o["oid"]] = o["actions"]["download"]
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--platform", default="linux-x64", choices=sorted(PLATFORMS))
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args()
    src_dir, libs = PLATFORMS[a.platform]
    dest = os.path.join(PORT, ".native", a.platform)
    os.makedirs(dest, exist_ok=True)

    wanted = []
    for name in libs:
        pointer = os.path.join(ROOT, src_dir, name)
        ptr = read_pointer(pointer)
        target = os.path.join(dest, name)
        if ptr is None:
            # The clone has the real binary already (git-lfs was installed): copy it verbatim.
            if not a.check:
                with open(pointer, "rb") as s, open(target, "wb") as d:
                    d.write(s.read())
            continue
        oid, size = ptr
        if os.path.exists(target) and os.path.getsize(target) == size and sha256(target) == oid:
            print(f"ok      {a.platform}/{name}")
            continue
        wanted.append((name, oid, size, target))

    if a.check:
        for name, *_ in wanted:
            print(f"MISSING {a.platform}/{name}")
        return 1 if wanted else 0
    if not wanted:
        return 0

    actions = download_urls([(oid, size) for _, oid, size, _ in wanted])
    for name, oid, size, target in wanted:
        act = actions[oid]
        req = urllib.request.Request(act["href"], headers=act.get("header", {}))
        tmp = target + ".part"
        with urllib.request.urlopen(req, timeout=600) as r, open(tmp, "wb") as f:
            while chunk := r.read(1 << 20):
                f.write(chunk)
        got = sha256(tmp)
        if got != oid or os.path.getsize(tmp) != size:
            os.remove(tmp)
            raise SystemExit(f"{name}: downloaded object does not match its pointer ({got})")
        os.replace(tmp, target)
        print(f"fetched {a.platform}/{name} ({size} bytes, sha256 verified)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
