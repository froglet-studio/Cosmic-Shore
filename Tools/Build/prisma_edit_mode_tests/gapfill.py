#!/usr/bin/env python3
"""Fill the Prisma engine's API gaps that stop the EDIT-MODE TEST RUN from building - in a
THROWAWAY worktree only.

    python3 Tools/Build/prisma_edit_mode_tests/gapfill.py <worktree-root>

Port/CLAUDE.md's one rule is that the port never changes the Unity project, and the engine is
changed only in a port session on its own branch. So these gaps are NOT fixed in the committed
Port/ - run.sh applies them to a temporary `git worktree` it deletes afterwards. Every edit is
idempotent (skipped when the member already exists), so as the port closes a gap for real the
matching edit here simply stops doing anything. Each one is the API shape of the real Unity /
UGS / Soap member, never a behaviour the tests depend on beyond that shape - except
ScriptableList.TryAdd and SessionException's constructor, whose semantics are Soap's and the
SDK's own.

Gaps found 2026-10-08 (the first nine are game code that landed on bleeding-edge after the port
last built it; the last three are the request policy's and the party tests' SDK surface):
  Collider.attachedRigidbody, Physics.OverlapBoxNonAlloc, StudioEventEmitter.EventStopTrigger,
  Graphics.CopyTexture(6 args), SystemInfo.copyTextureSupport + Rendering.CopyTextureSupport,
  Rendering.CullMode, Rendering.RenderQueue, SessionError's full code list,
  SessionException(string, SessionError, Exception), ScriptableList<T>.TryAdd.
"""
import pathlib
import sys

ROOT = pathlib.Path(sys.argv[1]) / "Port" / "src" / "CosmicShore.Engine"
assert ROOT.is_dir(), f"not a worktree with Port/: {sys.argv[1]}"


def edit(rel, marker, anchor, insert_after):
    p = ROOT / rel
    t = p.read_text(encoding="utf-8-sig")
    if marker in t:
        print(f"  present  {rel}: {marker}")
        return
    assert t.count(anchor) == 1, f"{rel}: anchor not unique/found: {anchor[:60]!r}"
    p.write_text(t.replace(anchor, anchor + insert_after, 1), encoding="utf-8")
    print(f"  filled   {rel}: {marker}")


SESSION_CODES = ["AllocationNotFound", "NetworkManagerNotInitialized", "NetworkManagerStartFailed",
                 "NetworkSetupFailed", "SessionConflict", "LobbyAlreadyExists", "AllocationAlreadyExists",
                 "AlreadySubscribedToLobby", "NotAuthorized", "Forbidden", "InvalidParameter",
                 "InvalidOperation", "InvalidNetworkConfig", "InvalidSessionMetadata",
                 "InvalidCreateSessionOptions", "InvalidSessionIdentifier", "MissingAssembly",
                 "TransportComponentMissing", "TransportInvalid"]

p = ROOT / "Networking/ISession.cs"
t = p.read_text(encoding="utf-8-sig")
enum_start = t.index("public enum SessionError")
enum_body = t[enum_start:t.index("}", enum_start)]
missing = [c for c in SESSION_CODES if f" {c} " not in enum_body and f" {c}," not in enum_body]
if missing:
    anchor = "        RateLimitExceeded = 4,\n"
    assert t.count(anchor) == 1
    lines = "".join(f"        {c} = {100 + i},\n" for i, c in enumerate(missing))
    p.write_text(t.replace(anchor, anchor + "        // gap-fill (test run only)\n" + lines, 1), encoding="utf-8")
    print(f"  filled   Networking/ISession.cs: {len(missing)} SessionError code(s)")
else:
    print("  present  Networking/ISession.cs: SessionError codes")

edit("Networking/ISession.cs", "public SessionException(string message, SessionError error",
     "        public SessionError Error { get; }\n",
     "\n        public SessionException(string message, SessionError error, System.Exception innerException = null)\n"
     "            : base(message ?? error.ToString(), innerException) { Error = error; }\n")
edit("Compat/EngineCompat.cs", "attachedRigidbody",
     "    public class Collider : Behaviour\n    {\n",
     "        public Rigidbody attachedRigidbody => GetComponentInParent<Rigidbody>();\n")
edit("Rendering/Graphics.cs", "public static void CopyTexture(Texture src, int srcElement",
     "    public static class Graphics\n    {\n",
     "        public static void CopyTexture(Texture src, int srcElement, int srcMip, Texture dst, int dstElement, int dstMip) { }\n")
edit("Audio/FmodStudio.cs", "EventStopTrigger",
     "        public EmitterGameEvent StopEvent = EmitterGameEvent.None;\n",
     "        public EmitterGameEvent EventStopTrigger { get => StopEvent; set => StopEvent = value; }\n")
edit("Soap/ScriptableList.cs", "public bool TryAdd(T item)",
     "        public bool IsReadOnly => false;\n",
     "\n        public bool TryAdd(T item)\n        {\n            if (_list.Contains(item)) return false;\n"
     "            Add(item);\n            return true;\n        }\n")

extra = ROOT / "GapFillForEditModeTests.cs"
blocks = []
if "OverlapBoxNonAlloc" not in "".join(f.read_text(encoding="utf-8-sig") for f in ROOT.rglob("*.cs") if "/obj/" not in str(f) and f != extra):
    blocks.append("""namespace CosmicShore.Engine
{
    public static partial class Physics
    {
        public static int OverlapBoxNonAlloc(Vector3 center, Vector3 halfExtents, Collider[] results, Quaternion orientation = default,
            int layerMask = AllLayers, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            var hits = OverlapBox(center, halfExtents, orientation, layerMask, queryTriggerInteraction);
            int n = System.Math.Min(hits.Length, results.Length);
            System.Array.Copy(hits, results, n);
            return n;
        }
    }
}""")
all_src = "".join(f.read_text(encoding="utf-8-sig") for f in ROOT.rglob("*.cs") if "/obj/" not in str(f) and f != extra)
if "copyTextureSupport" not in all_src:
    blocks.append("""namespace CosmicShore.Engine
{
    public static partial class SystemInfo
    {
        public static CosmicShore.Engine.Rendering.CopyTextureSupport copyTextureSupport => CosmicShore.Engine.Rendering.CopyTextureSupport.None;
    }
}""")
rendering = []
if "enum CopyTextureSupport" not in all_src:
    rendering.append("    [System.Flags] public enum CopyTextureSupport { None = 0, Basic = 1, Copy3D = 2, DifferentTypes = 4, TextureToRT = 8, RTToTexture = 16 }")
if "enum CullMode" not in all_src:
    rendering.append("    public enum CullMode { Off = 0, Front = 1, Back = 2 }")
if "enum RenderQueue" not in all_src:
    rendering.append("    public enum RenderQueue { Background = 1000, Geometry = 2000, AlphaTest = 2450, GeometryLast = 2500, Transparent = 3000, Overlay = 4000 }")
if rendering:
    blocks.append("namespace CosmicShore.Engine.Rendering\n{\n" + "\n".join(rendering) + "\n}")
if blocks:
    extra.write_text("// gap-fill for the edit-mode test run only - written into a throwaway worktree.\n"
                     + "\n".join(blocks) + "\n", encoding="utf-8")
    print(f"  filled   GapFillForEditModeTests.cs: {len(blocks)} block(s)")
else:
    print("  present  OverlapBoxNonAlloc / copyTextureSupport / Rendering enums")
