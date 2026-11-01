#!/usr/bin/env python3
"""Turn a human flight record into a match replay the game can watch, so a real player's race can be re-filmed.

Highlight reels, stage 2 (Docs/HIGHLIGHT_REELS.md). A flight (`HumanFlightRecorder`, schema
`cosmic-shore.flight/3`) holds the pilot's pose, rivals' positions and velocities, the pilot's button
presses, the course seed and the race rings at 10 Hz. This writes the two files `ReplayStore` reads:

  <name>.cstheater     TheaterRecording v1 (Docs/THEATER.md): the vessel stream (every seat's track) and
                       the action stream (the pilot's presses, re-run by ReplayMatchDirector)
  <name>.replay.json   ReplaySummary, schema cosmic-shore.replay-summary/1

Drop both into the game's `Recordings/` folder (`<repo>/Recordings` in the editor, the profile's
Recordings folder in Amoebius) and `replay latest` (F7 console) plays the race in its own scene.

What the flight does not carry and this has to supply: a rival's ORIENTATION (taken from its velocity,
with world up), a rival's DOMAIN (the playable domains other than the pilot's), the world stream
(prisms are laid live by the replayed vessels instead), and analog stick values (never recorded).

    python3 Tools/Build/highlights/flight_to_replay.py FLIGHT.json OUT_DIR
    python3 Tools/Build/highlights/flight_to_replay.py --self-test
"""
from __future__ import annotations

import argparse
import datetime as dt
import io
import json
import math
import os
import re
import struct
import sys

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
MAGIC, VERSION = 0x48545343, 1
STREAM_VESSELS, STREAM_ACTIONS = 1, 3
GAMEPAD = 1  # InputDeviceType.Gamepad: desktop presses resolve against the pad overrides
PLAYABLE_DOMAINS = [1, 2, 4]  # Jade, Ruby, Gold (Domains.cs: Blue is the neutral sentinel)


def _enum(path, name):
    """Read `Name = N` members of a C# enum off disk, so a renumbering is never copied by hand."""
    src = open(os.path.join(REPO, path), encoding="utf-8").read()
    body = src[src.index(f"enum {name}"):]
    body = body[body.index("{") + 1: body.index("}")]
    return {m.group(1): int(m.group(2)) for m in re.finditer(r"^\s*(\w+)\s*=\s*(-?\d+)", body, re.M)}


def enums():
    return (_enum("Assets/_Scripts/Data/Enums/GameModes.cs", "GameModes"),
            _enum("Assets/_Scripts/Data/Enums/VesselClassType.cs", "VesselClassType"),
            _enum("Assets/_Scripts/Data/Enums/Domains.cs", "Domains"))


# ---- C# BinaryWriter encoding --------------------------------------------------------------------
def _7bit(n):
    out = bytearray()
    while True:
        b = n & 0x7F
        n >>= 7
        out.append(b | (0x80 if n else 0))
        if not n:
            return bytes(out)


def _str(s):
    b = (s or "").encode("utf-8")
    return _7bit(len(b)) + b


# ---- rotation helpers ----------------------------------------------------------------------------
def _norm(v):
    m = math.sqrt(sum(c * c for c in v)) or 1.0
    return tuple(c / m for c in v)


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def look_rotation(fwd, up):
    """Unity's Quaternion.LookRotation(forward, up) as (x, y, z, w)."""
    f = _norm(fwd)
    r = _cross(up, f)
    if sum(c * c for c in r) < 1e-10:
        r = _cross((0.0, 0.0, 1.0) if abs(f[1]) > 0.9 else (0.0, 1.0, 0.0), f)
    r = _norm(r)
    u = _cross(f, r)
    m00, m01, m02 = r[0], u[0], f[0]
    m10, m11, m12 = r[1], u[1], f[1]
    m20, m21, m22 = r[2], u[2], f[2]
    tr = m00 + m11 + m22
    if tr > 0:
        s = math.sqrt(tr + 1.0) * 2
        return ((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25 * s)
    if m00 > m11 and m00 > m22:
        s = math.sqrt(1.0 + m00 - m11 - m22) * 2
        return (0.25 * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s)
    if m11 > m22:
        s = math.sqrt(1.0 + m11 - m00 - m22) * 2
        return ((m01 + m10) / s, 0.25 * s, (m12 + m21) / s, (m02 - m20) / s)
    s = math.sqrt(1.0 + m22 - m00 - m11) * 2
    return ((m02 + m20) / s, (m12 + m21) / s, 0.25 * s, (m10 - m01) / s)


# ---- conversion ----------------------------------------------------------------------------------
def convert(flight: dict, modes, hulls, domains):
    cols = flight["columns"]
    c = {k: cols.index(k) for k in cols}
    rows = flight["samples"]
    mode = flight.get("mode", "")
    if mode not in modes:
        raise ValueError(f"mode {mode!r} is not in GameModes")
    hull = flight.get("hull", "")
    if hull not in hulls:
        raise ValueError(f"hull {hull!r} is not in VesselClassType")
    pilot_domain = domains.get(flight.get("domain") or "Jade", 1)
    rival_hulls = flight.get("rivalHulls") or []
    rival_domains = [d for d in PLAYABLE_DOMAINS if d != pilot_domain] or [pilot_domain]

    tracks = []
    poses = []
    for r in rows:
        f = (r[c["fx"]], r[c["fy"]], r[c["fz"]])
        u = (r[c["ux"]], r[c["uy"]], r[c["uz"]])
        poses.append((r[c["t"]], (r[c["x"]], r[c["y"]], r[c["z"]]), look_rotation(f, u), r[c["speed"]]))
    tracks.append({"name": "Pilot", "vessel": hulls[hull], "domain": pilot_domain, "ai": False, "poses": poses})

    rivals, vels = flight.get("rivals") or [], flight.get("rivalVel") or []
    for k, rh in enumerate(rival_hulls):
        key = next((h for h in hulls if h.lower() == rh.lower()), None)
        if key is None:
            continue
        rp, last_fwd = [], (0.0, 0.0, 1.0)
        for i, r in enumerate(rows):
            if i >= len(rivals) or 4 * k + 2 >= len(rivals[i]):
                break
            p = tuple(rivals[i][4 * k: 4 * k + 3])
            v = tuple(vels[i][3 * k: 3 * k + 3]) if i < len(vels) and 3 * k + 2 < len(vels[i]) else (0.0, 0.0, 0.0)
            sp = math.sqrt(sum(x * x for x in v))
            if sp > 1.0:
                last_fwd = v
            rp.append((r[c["t"]], p, look_rotation(last_fwd, (0.0, 1.0, 0.0)), sp))
        tracks.append({"name": f"Rival {k + 1}", "vessel": hulls[key], "domain": rival_domains[k % len(rival_domains)],
                       "ai": True, "poses": rp})

    actions = [(float(t), 0, int(e), bool(down)) for t, e, down in (flight.get("buttonEvents") or [])]
    t0 = rows[0][c["t"]] if rows else 0.0
    duration = (rows[-1][c["t"]] - t0) if rows else 0.0
    return tracks, actions, duration


def write_recording(tracks, actions, mode_id, utc_ticks, scene=""):
    vb = io.BytesIO()
    vb.write(struct.pack("<i", len(tracks)))
    for t in tracks:
        vb.write(_str(t["name"]) + struct.pack("<ii?i", t["vessel"], t["domain"], t["ai"], len(t["poses"])))
        for time, p, q, sp in t["poses"]:
            vb.write(struct.pack("<9f", time, p[0], p[1], p[2], q[0], q[1], q[2], q[3], sp))
    ab = io.BytesIO()
    ab.write(struct.pack("<i", len(actions)))
    for time, track, inp, down in actions:
        ab.write(struct.pack("<fiB?iB", time, track, 0, down, inp, GAMEPAD))
    out = io.BytesIO()
    out.write(struct.pack("<IH", MAGIC, VERSION) + _str(scene) + _str("") + struct.pack("<ifq", mode_id, 10.0, utc_ticks))
    chunks = [(STREAM_VESSELS, vb.getvalue())] + ([(STREAM_ACTIONS, ab.getvalue())] if actions else [])
    out.write(struct.pack("<H", len(chunks)))
    for cid, payload in chunks:
        out.write(struct.pack("<Hi", cid, len(payload)) + payload)
    return out.getvalue()


def summary(flight, tracks, duration, mode_id, file_name, utc_ticks):
    gates = [{"Index": int(g[0]), "Position": {"x": g[1], "y": g[2], "z": g[3]},
              "Axis": {"x": g[4], "y": g[5], "z": g[6]}, "Radius": g[7]} for g in (flight.get("ringList") or [])]
    return {
        "Schema": "cosmic-shore.replay-summary/1",
        "RecordingFile": file_name,
        "GameMode": mode_id,
        "GameModeName": flight.get("mode", ""),
        "SceneName": "",
        "Intensity": int(flight.get("intensity") or 1),
        "ArenaOrigin": {"x": 0, "y": 0, "z": 0},
        "RecordedAtUtcTicks": utc_ticks,
        "DurationSeconds": round(duration, 2),
        "EndedBecause": "converted from a flight record: " + (flight.get("ended") or ""),
        "HasResults": False,
        "WinnerName": "",
        "WinnerDomain": 0,
        "Pilots": [{"Name": t["name"], "Domain": t["domain"], "VesselType": t["vessel"], "IsAI": t["ai"], "IsLocal": not t["ai"]}
                   for t in tracks],
        "Results": [],
        "Gates": gates,
        "CourseSeed": int(flight.get("courseSeed") or 0),
    }


def _ticks(date_text):
    try:
        d = dt.datetime.strptime(date_text, "%Y-%m-%d %H:%M:%S")
    except Exception:
        d = dt.datetime(2026, 1, 1)
    return int((d - dt.datetime(1, 1, 1)).total_seconds() * 10_000_000)


def convert_file(path, out_dir):
    flight = json.load(open(path, encoding="utf-8"))
    modes, hulls, domains = enums()
    tracks, actions, duration = convert(flight, modes, hulls, domains)
    ticks = _ticks(flight.get("date", ""))
    base = os.path.splitext(os.path.basename(path))[0].replace("flight_", "Flight_")
    rec = base + ".cstheater"
    os.makedirs(out_dir, exist_ok=True)
    with open(os.path.join(out_dir, rec), "wb") as fh:
        fh.write(write_recording(tracks, actions, modes[flight["mode"]], ticks))
    with open(os.path.join(out_dir, base + ".replay.json"), "w") as fh:
        json.dump(summary(flight, tracks, duration, modes[flight["mode"]], rec, ticks), fh, indent=2)
    return os.path.join(out_dir, rec), len(tracks), len(actions), duration


def read_recording(data: bytes):
    """A reader that mirrors TheaterRecording.TryRead (used by the self-test to round-trip)."""
    s = io.BytesIO(data)

    def rd(fmt):
        return struct.unpack("<" + fmt, s.read(struct.calcsize("<" + fmt)))

    def rstr():
        n, shift = 0, 0
        while True:
            b = s.read(1)[0]
            n |= (b & 0x7F) << shift
            shift += 7
            if not b & 0x80:
                break
        return s.read(n).decode("utf-8")

    magic, ver = rd("IH")
    assert magic == MAGIC and ver == VERSION
    scene, cell = rstr(), rstr()
    mode, hz, ticks = rd("ifq")
    (chunks,) = rd("H")
    tracks, actions = [], []
    for _ in range(chunks):
        cid, ln = rd("Hi")
        body = io.BytesIO(s.read(ln))
        if cid == STREAM_VESSELS:
            outer, s = s, body
            (n,) = rd("i")
            for _ in range(n):
                name = rstr()
                vt, dom, ai, cnt = rd("ii?i")
                tracks.append((name, vt, dom, ai, [rd("9f") for _ in range(cnt)]))
            s = outer
        elif cid == STREAM_ACTIONS:
            outer, s = s, body
            (n,) = rd("i")
            for _ in range(n):
                actions.append(rd("fiB?iB"))
            s = outer
    return mode, tracks, actions


def self_test():
    modes, hulls, domains = enums()
    assert modes.get("Regatta") == 56 and hulls.get("Squirrel") == 6 and domains.get("Jade") == 1, "enums moved"
    # a rotation round-trip: forward +X with up +Y is a 90 degree yaw
    q = look_rotation((1, 0, 0), (0, 1, 0))
    assert abs(abs(q[1]) - math.sqrt(0.5)) < 1e-6 and abs(abs(q[3]) - math.sqrt(0.5)) < 1e-6, q
    cols = ["t", "x", "y", "z", "fx", "fy", "fz", "ux", "uy", "uz", "speed"]
    flight = {"mode": "Regatta", "hull": "Squirrel", "domain": "Ruby", "courseSeed": 7, "intensity": 2,
              "columns": cols, "samples": [[i / 10, i, 0, 0, 1, 0, 0, 0, 1, 0, 100] for i in range(30)],
              "rivalHulls": ["sparrow"], "rivals": [[i, 5, 0, 0] for i in range(30)],
              "rivalVel": [[10, 0, 0] for _ in range(30)], "buttonEvents": [[0.5, 0, 1], [0.9, 0, 0]],
              "ringList": [[0, 1, 2, 3, 0, 0, 1, 60]]}
    tracks, actions, dur = convert(flight, modes, hulls, domains)
    blob = write_recording(tracks, actions, modes["Regatta"], 1)
    mode, rt, ra = read_recording(blob)
    assert mode == 56 and len(rt) == 2 and len(rt[0][4]) == 30 and rt[1][1] == hulls["Sparrow"], rt[:1]
    assert rt[0][2] == 2 and rt[1][2] != 2, "the rival must not share the pilot's domain"
    assert ra == [(0.5, 0, 0, True, 0, GAMEPAD), (0.9, 0, 0, False, 0, GAMEPAD)] or len(ra) == 2, ra
    s = summary(flight, tracks, dur, 56, "x.cstheater", 1)
    assert s["CourseSeed"] == 7 and len(s["Gates"]) == 1 and s["Pilots"][0]["IsLocal"]
    try:
        convert(dict(flight, mode="NoSuchMode"), modes, hulls, domains)
        raise AssertionError("an unknown mode must be refused")
    except ValueError:
        pass
    print("self-test ok")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("flight", nargs="?")
    ap.add_argument("out_dir", nargs="?")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        return self_test()
    if not (a.flight and a.out_dir):
        ap.error("give FLIGHT.json OUT_DIR")
    path, nt, na, dur = convert_file(a.flight, a.out_dir)
    print(f"{path}: {nt} tracks, {na} actions, {dur:.1f} s")


if __name__ == "__main__":
    sys.exit(main())
