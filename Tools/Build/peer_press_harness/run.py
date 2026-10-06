#!/usr/bin/env python3
"""Two-machine harness for vessel ability presses - the shipped R_VesselActionHandler, run as an
OWNER copy and a PEER copy of one vessel, with every shipped vessel's binding maps.

    python3 Tools/Build/peer_press_harness/run.py                 # the working tree; exit 1 on any failure
    python3 Tools/Build/peer_press_harness/run.py --rev <git-rev> # the handler as it was at <rev>
    python3 Tools/Build/peer_press_harness/run.py --self-test     # each fix mechanism removed must FAIL

Why it exists. A press replicates by RE-EXECUTION: owner -> server -> every machine, and each
machine resolves the pressed INPUT to actions itself, against a device-dependent map. Whether two
machines run the same actions is a property no single-machine test can observe, and the in-editor
check needs a two-client MPPM session. This harness answers it in seconds, from the shipped code:

  1. human presses: for every vessel x owner device x what the peer thinks the device is x bound
     input, press and release through OnButtonPressed/OnButtonReleased. The owner's copy must run
     exactly what its own device resolves to, and the peer's copy exactly what the owner's ran;
  2. the same through PerformShipControllerActionsReplicated (server-owned AI);
  3. a device switch MID-HOLD that has reached every copy: the release must stop what the press
     started, on every copy;
  4. the same on the non-networked single-machine path;
  5. a peer that never ran the press (joined mid-hold): what the release carries must be enough.

How. The REAL file is compiled with exactly two edits: the class is made `partial`, and its two
[ServerRpc] methods are cut out so a router can deliver the ClientRpc to every copy. Everything else
is shipped code; its Unity/Netcode/SOAP surface is stubbed in Stubs.cs (transcribed from the tree).
The shipped CarriedInputDeviceTests run too, against a small NUnit shim. A stub gap shows as a
compile error naming a type or member - add it to Stubs.cs from its real declaration; never edit
the handler to suit the harness.

What it does NOT prove: anything Netcode itself does (delivery, ordering, ownership checks), the
InputStatus NetworkVariable, or executor behaviour - actions are recorders. It is a statement about
what each machine RESOLVES and RUNS for a press, which is where the 2026-10 divergence lived.

Needs the .NET 8 SDK: $DOTNET_ROOT, ~/.dotnet or /usr/lib/dotnet (see the asset-surgery skill
section 4 for a per-user install). Exit 2 if none is found.
"""
import argparse
import glob
import os
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
assert os.path.isdir(os.path.join(ROOT, "Assets")), "ROOT must contain Assets/"
HERE = os.path.dirname(os.path.abspath(__file__))
HANDLER = "Assets/_Scripts/Controller/Vessel/R_VesselActionHandler.cs"
HANDLER_GUID = "ab3e795de1597af4f812bc7bbb73356d"
REAL_FILES = [
    "Assets/_Scripts/Data/Enums/InputEvents.cs",
    "Assets/_Scripts/Data/Enums/ResourceEvents.cs",
    "Assets/_Scripts/Data/Enums/InputDeviceType.cs",
]
# Run alongside the working-tree handler only: it names members an older handler (--rev) lacks.
SHIPPED_TESTS = ["Assets/_Scripts/Tests/Editor/CarriedInputDeviceTests.cs"]

# --self-test: each removes ONE mechanism of the fix and names the scenario that must catch it.
MUTATIONS = [
    ("peer resolves with its own device, not the carried one",
     "                StartPressedActions(ie, device);\n",
     "                StartPressedActions(ie, CurrentDevice());\n"),
    ("release ignores the device its press recorded",
     "            if (_heldInputs.TryGetValue(controlType, out var pressedWith)) device = pressedWith;\n",
     ""),
    ("release carries the current device, not the pressed one",
     "                SendButtonReleased_ServerRpc(ie, PressedDevice(ie));\n            }\n",
     "                SendButtonReleased_ServerRpc(ie, CurrentDevice());\n            }\n"),
]


def find_dotnet():
    for root in (os.environ.get("DOTNET_ROOT"), os.path.expanduser("~/.dotnet"), "/usr/lib/dotnet"):
        if root and os.path.isfile(os.path.join(root, "dotnet")) and glob.glob(os.path.join(root, "sdk/*/Roslyn/bincore/csc.dll")):
            return root
    sys.stderr.write("peer_press_harness: no .NET SDK found (DOTNET_ROOT, ~/.dotnet, /usr/lib/dotnet).\n")
    sys.exit(2)


def transform(src):
    decl = "public class R_VesselActionHandler : NetworkBehaviour"
    if src.count(decl) != 1:
        sys.exit("peer_press_harness: class declaration not found - update transform()")
    src = src.replace(decl, "public partial class R_VesselActionHandler : NetworkBehaviour")
    params = []
    for name in ("SendButtonPressed_ServerRpc", "SendButtonReleased_ServerRpc"):
        m = re.search(r"\n[ \t]*\[ServerRpc[^\]]*\]\s*\n[ \t]*(?:private\s+)?void\s+" + name + r"\s*\(([^)]*)\)", src)
        if not m:
            sys.exit("peer_press_harness: not found: " + name)   # HARD FAIL - the gate must not erode
        i, depth = src.index("{", m.end()), 0
        for j in range(i, len(src)):
            depth += {"{": 1, "}": -1}.get(src[j], 0)
            if depth == 0:
                break
        src = src[:m.start()] + src[j + 1:]
        params.append(m.group(1).strip())
    if params[0] != params[1]:
        sys.exit(f"peer_press_harness: press/release RPCs differ: {params}")
    return src, params[0]


def router(params):
    args = ", ".join(p.split()[-1] for p in params.split(","))
    return f"""using System.Collections.Generic;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{{
    public partial class R_VesselActionHandler
    {{
        // owner -> server -> every machine's copy of THIS vessel (the owner's own copy included)
        internal List<R_VesselActionHandler> Copies;
        internal static int PressRpcs, ReleaseRpcs;
        void SendButtonPressed_ServerRpc({params}) {{ PressRpcs++; foreach (var c in Copies) c.SendButtonPressed_ClientRpc({args}); }}
        void SendButtonReleased_ServerRpc({params}) {{ ReleaseRpcs++; foreach (var c in Copies) c.SendButtonReleased_ClientRpc({args}); }}
        internal void HarnessSetMaps(List<InputEventShipActionMapping> shared, List<InputEventShipActionMapping> touch,
            List<InputEventShipActionMapping> pad, CosmicShore.ScriptableObjects.ScriptableEventAbilityStats executed)
        {{
            _inputEventShipActions = shared; _touchActionOverrides = touch; _gamepadActionOverrides = pad;
            _resourceEventClassActions = new List<ResourceEventShipActionMapping>(); onAbilityExecuted = executed;
        }}
        internal void HarnessPress(InputEvents ie) => OnButtonPressed(ie);
        internal void HarnessRelease(InputEvents ie) => OnButtonReleased(ie);
        internal const string Signature = "{params}";
    }}
}}
"""


def fleet():
    """Every shipped vessel's three binding maps, read from the prefab YAML, as C#."""
    names = {}
    for meta in glob.glob(os.path.join(ROOT, "Assets/**/*.asset.meta"), recursive=True):
        g = re.search(r"guid: (\w+)", open(meta, encoding="utf-8").read())
        if g:
            names[g.group(1)] = os.path.basename(meta)[:-len(".asset.meta")]

    def parse(doc, field):
        m = re.search(r"\n  " + field + r":(.*?)(?=\n  \w)", doc, re.S)
        if not m or m.group(1).strip() == "[]":
            return []
        return [(int(e.group(1)), [names.get(g, g) for g in re.findall(r"guid: (\w+)", e.group(2))])
                for e in re.finditer(r"- InputEvent: (\d+)\n    ShipActions:(.*?)(?=\n  - InputEvent|\Z)", m.group(1), re.S)]

    def cs(entries):
        return "new (int, string[])[] {" + ", ".join(
            f'({ie}, new[] {{{", ".join(chr(34) + n + chr(34) for n in acts)}}})' for ie, acts in entries) + "}"

    rows = []
    for prefab in sorted(glob.glob(os.path.join(ROOT, "Assets/_Prefabs/Spacevessels/*.prefab"))):
        for doc in re.split(r"\n--- ", open(prefab, encoding="utf-8").read()):
            if HANDLER_GUID in doc and "_inputEventShipActions" in doc:
                rows.append(f'        new Fleet.Vessel("{os.path.basename(prefab)[:-7]}", {cs(parse(doc, "_inputEventShipActions"))}, '
                            f'{cs(parse(doc, "_touchActionOverrides"))}, {cs(parse(doc, "_gamepadActionOverrides"))}),')
    if len(rows) < 10:
        sys.exit(f"peer_press_harness: found only {len(rows)} vessel handlers - the prefab scan is broken")
    return "public static partial class Fleet { public static readonly Vessel[] All = {\n" + "\n".join(rows) + "\n    }; }\n"


def run(handler_src, label, dotnet, quiet=False, tests=True):
    out = os.path.join(tempfile.gettempdir(), "peer_press_harness", label)
    shutil.rmtree(out, ignore_errors=True)
    os.makedirs(out)
    src, params = transform(handler_src)
    for name, text in (("Handler.cs", src), ("Router.cs", router(params)), ("Fleet.cs", fleet())):
        with open(os.path.join(out, name), "w", encoding="utf-8") as f:
            f.write(text)
    csc = sorted(glob.glob(os.path.join(dotnet, "sdk/*/Roslyn/bincore/csc.dll")))[-1]
    refdir = sorted(glob.glob(os.path.join(dotnet, "packs/Microsoft.NETCore.App.Ref/*/ref/net8.0")))[-1]
    version = sorted(os.listdir(os.path.join(dotnet, "shared/Microsoft.NETCore.App")))[-1]
    sources = [os.path.join(HERE, "Stubs.cs"), os.path.join(HERE, "Driver.cs")] + \
              [os.path.join(out, n) for n in ("Handler.cs", "Router.cs", "Fleet.cs")] + \
              [os.path.join(ROOT, f) for f in REAL_FILES + (SHIPPED_TESTS if tests else [])]
    dll = os.path.join(out, "harness.dll")
    cmd = [os.path.join(dotnet, "dotnet"), csc, "-nologo", "-langversion:9.0", "-nostdlib", "-noconfig",
           "-nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0162",
           "-target:exe", "-main:Driver", "-out:" + dll] + \
          ["-r:" + r for r in sorted(glob.glob(os.path.join(refdir, "*.dll")))] + sources
    build = subprocess.run(cmd, capture_output=True, text=True)
    if build.returncode != 0:
        print(build.stdout + build.stderr)
        print(f"[{label}] COMPILE FAILED - a stub gap (add the member to Stubs.cs) or a real error in the handler.")
        return 1
    with open(os.path.join(out, "harness.runtimeconfig.json"), "w") as f:
        f.write('{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' % version)
    result = subprocess.run([os.path.join(dotnet, "dotnet"), dll], capture_output=True, text=True, cwd=ROOT)
    if not quiet:
        print(result.stdout + result.stderr, end="")
    return result.returncode


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--rev", help="run the handler as it was at this git revision")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    dotnet = find_dotnet()

    if a.rev:
        src = subprocess.run(["git", "show", f"{a.rev}:{HANDLER}"], capture_output=True, text=True, cwd=ROOT, check=True).stdout
        sys.exit(run(src, "rev", dotnet, tests=False))

    with open(os.path.join(ROOT, HANDLER), encoding="utf-8") as f:
        shipped = f.read()
    if not a.self_test:
        sys.exit(run(shipped, "shipped", dotnet))

    failures = 0
    if run(shipped, "shipped", dotnet, quiet=True) != 0:
        print("SELF-TEST: the shipped handler fails - fix that first")
        sys.exit(1)
    for i, (what, old, new) in enumerate(MUTATIONS):
        if shipped.count(old) != 1:
            print(f"SELF-TEST: mutation anchor moved: {what!r} - update MUTATIONS")
            failures += 1
            continue
        code = run(shipped.replace(old, new), f"mutation{i}", dotnet, quiet=True)
        print(f"SELF-TEST: {'caught' if code == 1 else 'MISSED'}: {what}")
        failures += code != 1
    sys.exit(1 if failures else 0)


if __name__ == "__main__":
    main()
