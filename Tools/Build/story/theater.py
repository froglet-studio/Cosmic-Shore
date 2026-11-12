#!/usr/bin/env python3
"""Read (and, for tests, write) the game's match recordings: `.cstheater` frames and `.replay.json` summaries.

The format is Will's Theater format (`Assets/_Scripts/Utility/Theater/TheaterRecording.cs`, magic 'CSTH', v1): a
header, then chunks (id, byte length, payload) so a reader steps over what it does not know. Two streams are decoded:
the vessels (id 1, every hull's pose at the recorder's rate) and the world (id 2, every prism's birth, death,
restoration and move: `TheaterWorld.cs`), which is where creation and destruction are seen. The action stream (button
presses, id 3) is skipped by length.

    python3 Tools/Build/story/theater.py FILE.cstheater        # print what is in it
    python3 Tools/Build/story/theater.py --self-test

Nothing here imports Unity or the port: the C# reader is the authority, and the self-test writes a file the way
`TheaterRecording.Write` does and reads it back.
"""
from __future__ import annotations

import io
import json
import math
import os
import re
import struct
import sys

MAGIC = 0x48545343
VERSION = 1
STREAM_VESSELS, STREAM_WORLD, STREAM_ACTIONS = 1, 2, 3
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))


# ---- the game's enums, read from the C# source so a new member never needs a copy here ----

def _enum(path: str, name: str) -> dict[int, str]:
    try:
        src = open(os.path.join(ROOT, path), encoding="utf-8").read()
    except OSError:
        return {}
    body = src[src.find("enum " + name):]
    body = body[body.find("{") + 1: body.find("}")]
    out = {}
    for m in re.finditer(r"^\s*([A-Za-z_]\w*)\s*=\s*(-?\d+)", body, re.M):
        out.setdefault(int(m.group(2)), m.group(1))
    return out


ENUMS_JSON = os.path.join(os.path.dirname(os.path.abspath(__file__)), "enums.json")


def _enums():
    """From the C# source when this runs in a checkout; else from enums.json beside this file (a copy of the tools
    outside the repo, e.g. C:\\Claude\\story\\tools on Gbox). `--write-enums` refreshes the JSON."""
    v = _enum("Assets/_Scripts/Data/Enums/VesselClassType.cs", "VesselClassType")
    m = _enum("Assets/_Scripts/Data/Enums/GameModes.cs", "GameModes")
    if not v and os.path.exists(ENUMS_JSON):
        data = json.load(open(ENUMS_JSON, encoding="utf-8"))
        v = {int(k): n for k, n in data.get("vessels", {}).items()}
        m = {int(k): n for k, n in data.get("modes", {}).items()}
    return v, m


VESSELS, MODES = _enums()


def vessel_name(v: int) -> str:
    return VESSELS.get(v, f"vessel{v}")


def mode_name(m: int) -> str:
    return MODES.get(m, f"mode{m}")


# ---- .NET BinaryReader/BinaryWriter primitives ----

class _Reader:
    def __init__(self, data: bytes):
        self.b = io.BytesIO(data)

    def _take(self, n):
        chunk = self.b.read(n)
        if len(chunk) != n:
            raise EOFError("truncated recording")
        return chunk

    def u8(self): return self._take(1)[0]
    def u16(self): return struct.unpack("<H", self._take(2))[0]
    def u32(self): return struct.unpack("<I", self._take(4))[0]
    def i32(self): return struct.unpack("<i", self._take(4))[0]
    def i64(self): return struct.unpack("<q", self._take(8))[0]
    def f32(self): return struct.unpack("<f", self._take(4))[0]
    def bool(self): return self._take(1) != b"\x00"
    def bytes(self, n): return self._take(n)

    def string(self):
        n, shift = 0, 0
        while True:
            byte = self._take(1)[0]
            n |= (byte & 0x7F) << shift
            if byte < 0x80:
                break
            shift += 7
        return self._take(n).decode("utf-8")


def _w_string(out: io.BytesIO, s: str):
    data = s.encode("utf-8")
    n = len(data)
    while n >= 0x80:
        out.write(bytes([(n & 0x7F) | 0x80]))
        n >>= 7
    out.write(bytes([n]))
    out.write(data)


# ---- the recording ----

class Track:
    """One hull: who flew it and every pose (t, x, y, z, qx, qy, qz, qw, speed)."""

    def __init__(self, name, vessel, domain, is_ai, poses):
        self.name, self.vessel, self.domain, self.is_ai, self.poses = name, vessel, domain, is_ai, poses

    @property
    def vessel_name(self):
        return vessel_name(self.vessel)

    def to_json(self):
        return {"name": self.name, "vessel": self.vessel_name, "domain": self.domain, "ai": self.is_ai,
                "samples": len(self.poses)}


class Recording:
    def __init__(self):
        self.scene = self.cell_config = ""
        self.game_mode = 0
        self.sample_hz = 30.0
        self.utc_ticks = 0
        self.tracks: list[Track] = []
        # prism events: (t, kind, id, x, y, z, domain); kind BORN/GONE/RESTORED/MOVED. A GONE event carries the
        # position the prism last had, so destruction can be placed in the world.
        self.world: list[tuple] = []
        self.has_world = self.has_actions = False
        self.summary: dict = {}
        self.path = ""
        self.time_offset = 0.0

    @property
    def mode_name(self):
        return self.summary.get("GameModeName") or mode_name(self.game_mode)

    @property
    def duration(self):
        ends = [t.poses[-1][0] for t in self.tracks if t.poses]
        return max(ends) if ends else 0.0

    @property
    def humans(self):
        return [t for t in self.tracks if not t.is_ai]

    @property
    def match_id(self):
        return self.summary.get("MatchId", "")


def read(path: str) -> Recording:
    with open(path, "rb") as f:
        rec = parse(f.read())
    rec.path = path
    side = os.path.splitext(path)[0] + ".replay.json"
    if os.path.exists(side):
        try:
            rec.summary = json.load(open(side, encoding="utf-8-sig"))
        except ValueError:
            rec.summary = {}
    return rec


def parse(data: bytes) -> Recording:
    r = _Reader(data)
    if r.u32() != MAGIC:
        raise ValueError("not a theater recording (bad magic)")
    version = r.u16()
    if version != VERSION:
        raise ValueError(f"recording is format v{version}, this reader reads v{VERSION}")
    rec = Recording()
    rec.scene, rec.cell_config = r.string(), r.string()
    rec.game_mode, rec.sample_hz, rec.utc_ticks = r.i32(), r.f32(), r.i64()
    for _ in range(r.u16()):
        cid, length = r.u16(), r.i32()
        if length < 0:
            raise ValueError("corrupt chunk length")
        payload = r.bytes(length)
        if cid == STREAM_VESSELS:
            rec.tracks = _vessels(payload)
            # Pose times are the game clock at capture (seconds since the app started); the story wants seconds into
            # the recording, the same clock the replay's playback scrubs.
            starts = [t.poses[0][0] for t in rec.tracks if t.poses]
            rec.time_offset = min(starts) if starts else 0.0
            if rec.time_offset:
                for t in rec.tracks:
                    t.poses = [(p[0] - rec.time_offset,) + tuple(p[1:]) for p in t.poses]
        elif cid == STREAM_WORLD:
            rec.has_world = True
            rec.world = _world(payload)
        elif cid == STREAM_ACTIONS:
            rec.has_actions = True
    if rec.time_offset and rec.world:   # the same game clock as the poses
        rec.world = [(e[0] - rec.time_offset,) + e[1:] for e in rec.world]
    return rec


BORN, GONE, RESTORED, MOVED = 0, 1, 2, 3


def _world(payload: bytes) -> list[tuple]:
    """`TheaterWorld.Write`: a count, then per event kind byte, time, id, and the kind's own fields."""
    r = _Reader(payload)
    out, where = [], {}
    for _ in range(r.i32()):
        kind, t, pid = r.u8(), r.f32(), r.i32()
        if kind == GONE:
            x, y, z, dom = where.get(pid, (0.0, 0.0, 0.0, 0))
            out.append((t, GONE, pid, x, y, z, dom))
            continue
        x, y, z = struct.unpack("<3f", r.bytes(12))
        r.bytes(16)   # rotation
        if kind == MOVED:
            dom = where.get(pid, (0, 0, 0, 0))[3]
        else:
            r.bytes(12)   # scale
            dom = r.u8()
            r.u8()        # flags
        where[pid] = (x, y, z, dom)
        out.append((t, kind, pid, x, y, z, dom))
    return out


def _vessels(payload: bytes) -> list[Track]:
    r = _Reader(payload)
    tracks = []
    for _ in range(r.i32()):
        name, vessel, domain, is_ai = r.string(), r.i32(), r.i32(), r.bool()
        n = r.i32()
        raw = r.bytes(36 * n)
        poses = list(struct.iter_unpack("<9f", raw))
        tracks.append(Track(name, vessel, domain, is_ai, poses))
    return tracks


def write(rec: Recording) -> bytes:
    """The bytes `TheaterRecording.Write` produces: the vessel stream, and the world stream when there are events."""
    out = io.BytesIO()
    out.write(struct.pack("<IH", MAGIC, VERSION))
    _w_string(out, rec.scene)
    _w_string(out, rec.cell_config)
    out.write(struct.pack("<ifq", rec.game_mode, rec.sample_hz, rec.utc_ticks))
    out.write(struct.pack("<H", 2 if rec.world else 1))
    body = io.BytesIO()
    body.write(struct.pack("<i", len(rec.tracks)))
    for t in rec.tracks:
        _w_string(body, t.name)
        body.write(struct.pack("<ii?i", t.vessel, t.domain, t.is_ai, len(t.poses)))
        for p in t.poses:
            body.write(struct.pack("<9f", *p))
    data = body.getvalue()
    out.write(struct.pack("<Hi", STREAM_VESSELS, len(data)))
    out.write(data)
    if rec.world:
        w = io.BytesIO()
        w.write(struct.pack("<i", len(rec.world)))
        for t, kind, pid, x, y, z, dom in rec.world:
            w.write(struct.pack("<Bfi", kind, t, pid))
            if kind == GONE:
                continue
            w.write(struct.pack("<3f4f", x, y, z, 0.0, 0.0, 0.0, 1.0))
            if kind != MOVED:
                w.write(struct.pack("<3fBB", 1.0, 1.0, 1.0, dom, 0))
        data = w.getvalue()
        out.write(struct.pack("<Hi", STREAM_WORLD, len(data)))
        out.write(data)
    return out.getvalue()


def find(folders) -> list[str]:
    """Every .cstheater under the folders, newest first."""
    found = []
    for folder in folders:
        for dirpath, _, files in os.walk(folder):
            found += [os.path.join(dirpath, f) for f in files if f.endswith(".cstheater")]
    return sorted(found, key=lambda p: os.path.getmtime(p), reverse=True)


# ---- self-test ----

def synthetic(seconds=20.0, hz=30.0, mode=56) -> Recording:
    """Two hulls on crossing loops plus an AI that trails one of them: a near pass every lap."""
    rec = Recording()
    rec.scene, rec.cell_config, rec.game_mode, rec.sample_hz = "MinigameRegatta", "Regatta Cell", mode, hz
    n = int(seconds * hz)
    def loop(phase, radius, height, ai, name, vessel, domain):
        poses = []
        for i in range(n):
            t = i / hz
            a = t * 0.9 + phase
            yaw = a + math.pi / 2
            poses.append((t, radius * math.cos(a), height, radius * math.sin(a),
                          0.0, math.sin(yaw / 2), 0.0, math.cos(yaw / 2), 60.0 + 20 * math.sin(t)))
        return Track(name, vessel, domain, ai, poses)
    rec.tracks = [loop(0.0, 200, 0, False, "Garrett", 11, 1),
                  loop(0.15, 205, 6, True, "Ava", 2, 2),
                  loop(math.pi, 200, 400, True, "Kai", 3, 3)]
    # the world: Garrett lays a dense trail from 4 to 7 s; Ava's wall stands from the start and Garrett tears
    # through 40 of its prisms around 12 s; Kai, far away, loses prisms nobody is near (no one's destruction).
    world, pid = [], 0
    g = rec.tracks[0].poses
    for i in range(int(4 * hz), int(7 * hz)):
        p = g[i]
        world.append((p[0], BORN, pid, p[1], p[2], p[3], 1)); pid += 1
    ava = []
    for k in range(40):
        p = g[int(12 * hz) + (k % 6) - 3]
        world.append((0.0, BORN, pid, p[1] + (k % 5) * 3, p[2], p[3] + (k % 7) * 3, 2)); ava.append(pid); pid += 1
    for k, q in enumerate(ava):
        world.append((12.0 + 0.01 * k, GONE, q, 0, 0, 0, 0))
    far = rec.tracks[2].poses[int(15 * hz)]
    for k in range(40):
        world.append((0.0, BORN, pid, far[1] + 2000, far[2], far[3], 3))
        world.append((15.0, GONE, pid, 0, 0, 0, 0)); pid += 1
    rec.world = sorted(world, key=lambda e: e[0])
    return rec


def self_test() -> int:
    rec = synthetic()
    back = parse(write(rec))
    assert back.scene == rec.scene and back.game_mode == 56 and abs(back.sample_hz - 30) < 1e-6
    assert [t.name for t in back.tracks] == ["Garrett", "Ava", "Kai"]
    assert back.tracks[0].vessel_name == "Sparrow" or not VESSELS, back.tracks[0].vessel_name
    assert len(back.tracks[1].poses) == len(rec.tracks[1].poses)
    assert abs(back.tracks[0].poses[10][1] - rec.tracks[0].poses[10][1]) < 1e-3
    assert len(back.humans) == 1 and back.duration > 19
    assert back.has_world and len(back.world) == len(rec.world)
    gone = [e for e in back.world if e[1] == GONE]
    assert gone and all(e[3] or e[5] for e in gone), "a death carries where the prism stood"
    late = synthetic()
    for t in late.tracks:
        t.poses = [(p[0] + 1000.0,) + tuple(p[1:]) for p in t.poses]
    late.world = [(e[0] + 1000.0,) + e[1:] for e in late.world]
    late = parse(write(late))
    assert min(e[0] for e in late.world) == 0.0
    assert late.time_offset == 1000.0 and late.tracks[0].poses[0][0] == 0.0 and late.duration < 21
    # a reader steps over a stream it does not know, and refuses a wrong magic or version
    data = bytearray(write(rec))
    try:
        parse(b"XXXX" + bytes(data[4:]))
        raise AssertionError("bad magic accepted")
    except ValueError:
        pass
    try:
        parse(bytes(data[:4]) + struct.pack("<H", 9) + bytes(data[6:]))
        raise AssertionError("future version accepted")
    except ValueError:
        pass
    print("theater: self-test OK")
    return 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    if "--write-enums" in argv:
        json.dump({"vessels": VESSELS, "modes": MODES}, open(ENUMS_JSON, "w", encoding="utf-8"), indent=0, sort_keys=True)
        print(f"wrote {ENUMS_JSON}: {len(VESSELS)} vessels, {len(MODES)} modes")
        return 0
    for path in argv:
        rec = read(path)
        print(json.dumps({"file": os.path.basename(path), "mode": rec.mode_name, "scene": rec.scene,
                          "seconds": round(rec.duration, 1), "hz": rec.sample_hz, "match_id": rec.match_id,
                          "tracks": [t.to_json() for t in rec.tracks]}, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
