# Wormholes — the Butterfly's fold pair

Every Butterfly **fold** leaves a wormhole: **two spheres that are one place**, one where the vessel
left and one where it arrived. Fly into either and you come out of the other, still flying the way
you were. Each sphere shows, from every side, what lies beyond the other one — so you see where you
are going before you go, and the transit itself has nothing on screen to give it away. The pair
replaced the fold's ring gates on 2026-10-08 (`BUTTERFLY_FOLD.md` § "The gates became wormholes";
playtested and confirmed the same day).

> The idea was prototyped as a standalone "Wormhole" Cell Selector world, which was retired once the
> fold carried it: there is no wormhole cell, and `Tools/Build/author_wormholes.py --check` asserts
> none is listed in Menu_Main.

Files:

| What | Where |
|---|---|
| The model (pure maths, tested) | `Assets/_Scripts/Controller/Environment/Wormhole/WormholeGeometry.cs` |
| A mouth: sphere, attached cameras, transit detection | `…/Wormhole/WormholeMouth.cs` |
| The per-frame driver: render budget, which camera is looking, the straddling ship | `…/Wormhole/WormholeView.cs` |
| Who lays a pair, and all tuning | `R_VesselActions/Executors/FoldActionExecutor.cs`, `Assets/_SO_Assets/VesselActions/Butterfly/ButterflyFoldAction.asset` |
| The surface shader / material | `Assets/_Graphics/Materials/Graphs/Wormhole.shader`, `Assets/_Graphics/Materials/Wormhole.mat` |
| Generator + gate (metas, material, the fold asset's wiring) | `Tools/Build/author_wormholes.py` (`--check`) |
| Tests | `Assets/_Scripts/Tests/Editor/WormholeGeometryTests.cs` |
| Camera carry | `CustomCameraController.CarryThroughSphere` |
| Ribbon cut + carry on every peer | `TeleportContinuity` → `WormholeMouth.TryResolveTransit` |

## 1. The model — two balls with one interior

The inside of mouth A **is** the inside of mouth B, displaced by a pure translation `Δ = B − A`.

- **A transit is a translation.** A vessel whose step enters ball A is moved by `Δ` and is then
  inside ball B at exactly the offset it had inside A. Rotation, heading and speed are untouched —
  a wormhole moves you, it does not fly you. A step that clips the ball and leaves again in one
  frame still went through; a step that *starts* inside never counts, which is the whole arming
  rule (a pilot just carried into B must fly out of it before B can take them back) — geometric,
  no clock.
- **The view through A** is the world seen from the viewer's own vantage carried by `Δ`, with
  everything between that vantage and ball B removed. The removal is one oblique near plane,
  tangent to ball B at its point nearest the vantage, so **all of ball B is kept**: a ship carried
  into B is visible through A on the very frame it moved. The approximation is a sliver beside B's
  near cap, outside the ball but beyond the plane — it only shows for something hugging the far
  mouth on the viewer's side.
- **It holds from every side** because nothing in the model has a facing: the view is re-derived
  from wherever the viewer is, every frame.

Why not "rays continue through the chord"? Because then the ball interiors are skipped, and a ship
entering A's front surface would reappear at B's *back* surface — through the window it visibly
jumps a whole diameter away from the camera (shrinks by 2–3× for a chase camera). The shared
interior keeps it continuous.

## 2. What the surface shows — two cameras on every mouth

The surface of A shows the place around **B**, so it is fed by B's cameras.

**The exact eye — the player-camera cheat, and the reason the transit is seamless.** Each frame,
for the nearest on-screen mouths, `WormholeView` renders the world from the gameplay camera's pose
carried through the pair, with that camera's own projection (field of view copied live, so the
speed tunnel narrows it too), clipped by the plane above, and **cropped to the sphere's footprint on
screen**. The shader samples that picture at its own screen position (the `FoldGatePortal`
arithmetic), so every pixel is exactly what the player would see if the two mouths were one place,
near things included. It is valid only for the camera it was rendered for, so the surface uses it
only while that camera is drawing (`_WormholeMainView`).

**The panorama eye — "viewing in all directions".** Each mouth's second camera sits at its centre
and captures its surroundings as six 90° faces into a six-slice texture array, one face per frame
while its partner is on screen. The partner's surface projects it back out in every direction: the
view ray is continued from the entry point as if it had come out of the far mouth, assumed to end on
a proxy sphere `_ProxyRadius` (material, default 600) from the capture point, and the panorama is
sampled in that direction. Any camera can use it — a spectator rig, a preview camera, the editor's
scene view — at any distance, at the cost of parallax for things nearer than the proxy.

The two crossfade by distance: exact inside the fold asset's `portalWindowRange` (2500), fading to
panorama across `portalWindowFadeBand` (600). The face table lives in **three** places — `WormholeGeometry.FaceRotation`
(the camera's pose), `FaceOf`/`FaceUV` (C#), and the shader's `SamplePanorama` — and
`WormholeGeometryTests.Panorama_FaceUV_IsWhereTheFaceCameraSeesTheDirection` is the contract between
them. The array is ours end to end, so no cubemap orientation convention is involved.

## 3. The transit, frame by frame

1. **Nose in.** The hull crosses A's sphere. A's surface hides the part inside the ball; the exact
   render draws the ship *carried through* for that one render, so the nose appears inside B through
   the window as it disappears into A. This is one case of a general rule: **during a mouth's exact
   render, every vessel it may carry that is in or cut by its ball is drawn at its mapped position on
   the far side** (`WormholeView.MoveInteriorVessels`) — the balls are one place, so a vessel inside
   either is seen through either.
2. **The jump.** The owner's detector (`WormholeMouth.Update`) sees the step enter the ball and
   writes the pose `+Δ` through `IVessel.SetPose`, which replicates. On every peer
   `VesselTransformer.SetPose` → `TeleportContinuity` resolves it as a wormhole transit
   (`WormholeMouth.TryResolveTransit`): ribbons are cut on the two spheres, and the active camera is
   told to `CarryThroughSphere`.
3. **The carry.** The camera keeps framing the ship *through* A (its follow point is the ship
   mapped back by `Δ`). While the ship's tail still sticks out of B's near face, the gameplay
   camera's render draws the ship mapped back too, so the tail is still in front of A.
4. **The hand-over.** When the camera itself reaches A — within `WormholeGeometry.Clearance` of the
   surface — it is moved by `Δ` with its smoothing state. The SAME clearance is where a mouth stops
   drawing for a camera (the shader clips it), so the camera lands just inside B's clearance, where B
   has just stopped drawing and the world beyond is seen directly — the picture it was already
   looking at through A.

If the ship turns so its framed point is no longer seen through A, or the carry runs past six
seconds, the camera is handed across at once (the ring gates' rules, kept).

## 4. Cost

Per frame, at most **two exact renders** (`WormholeView.MaxExactRendersPerFrame`), each the size of
its sphere's footprint at `portalWindowRenderScale` (0.75) of the screen — capped per device tier by
`PlatformProfileSO.FoldGateWindowMaxRenderScale`, the ring gates' window ceiling, kept by name — plus
**one panorama face per mouth** whose partner is on screen and not already fully exact (256², per
`panoramaFaceSize`). No shadows, no MSAA, no post on any of them: the sphere is composited into the
world and the gameplay camera's post runs over it once. A mouth nobody can see costs nothing, and
neither does a sealed one (§5). No colliders and no prisms.

## 5. The rim, the domain lock, and how a fold lays its pair

**The rim wears a domain's hue.** The only part of the surface that is the mouth itself is the
fresnel rim, and it is painted in the owning domain's colour — the theme's
`ToyFactory.DomainAccentColor`, passed per renderer (`_WormholeRimTint`) and lifted by the material's
`_DomainRimBoost` (2) so it blooms in that hue rather than reading as a pale wash, and repainted if
the owner changes domain. `_RimColor` is only the fallback for a mouth built with no tint. The transit
flare uses the same hue.

**Domain-locked.** Every fold pair is `WormholeMouth.Settings.DomainLocked`: it carries only vessels
of its `Domain`, which is its `Owner`'s (the fold's Butterfly's) LIVE domain, and the owner is always
carried and always sees through. To a viewer whose camera follows a pilot of another domain the mouth
is **sealed** (`WormholeView` decides per frame, `_WormholeSealed`): no view through, only a fresnel
shell in the domain colour, interior clipped in colour and depth, and that viewer's own ship flies
straight through it. Sealing is decided only on POSITIVE evidence of a rival viewer — an unresolvable
viewer sees the view (it first shipped failing closed, which drew a pilot's own pair as a bare ring).
A sealed mouth gets no exact render and asks for no panorama — it costs nothing. An unpaired mouth
(one withering away, `WormholeMouth.Retire`) is sealed too.

**Laying a pair.** The fold builds a domain-locked pair (radius `gateRadius` 55) at the two ends of
every fold — origin and destination, exactly where its ring gates used to stand — once the arrival
pose has replicated, and only when the two ends are at least `minGateSeparation` (300, never under
2.5 radii) apart; the previous pair withers out first (`BUTTERFLY_FOLD.md` § "The gates became
wormholes"). The arriving Butterfly sits at the centre of the destination mouth; it is drawn through
that mouth (the interior rule in §3), and it flies out without being taken back because a mouth only
takes a step that starts outside it.

## 6. Known limits

- **The exact view is for the player's camera only.** Every other camera (preview, scene view,
  spectator rigs that are not the active `CustomCameraController`) sees the panorama.
- **Only the followed ship's TAIL is drawn back after a transit.** Every carriable vessel inside a
  ball is seen through that ball, but the part of another pilot's hull still sticking out of the far
  mouth's near face is clipped from the near mouth's window until it is inside.
- **A ship that turns round inside the shared interior and leaves by the face it came in** comes out
  of B's near face — the model has one interior and two exteriors, and the exterior is the one of the
  ball you are physically in. The camera carry then hands over early.
- **Panorama parallax.** Things nearer the far mouth than the proxy slide against the background in
  the panorama; inside `exactRange` this never shows.
- **No recursion.** Every mouth is hidden inside every wormhole render (each samples targets those
  renders draw into), so A is never seen *through* B.
- **The transit sound ships empty** (`FoldActionExecutor.gateThreadEvent`) per the FMOD convention.

## 7. Follow-ups and recorded debt (ship pass, 2026-10-08 — rows, not fixes)

| # | Kind | What | Evidence | Blocking question |
|---|---|---|---|---|
| W1 | ~~verify~~ **closed** | The fold pair's first playtest reported rings with no view; the fix (`cf99303f2`: fail-open sealing, `Settings.Owner`) was playtested on 2026-10-08 and confirmed working. | `BUTTERFLY_FOLD.md` § "Playtest 1" | — |
| W2 | inconsistency (created) | `WormholeMouth.MakeEye` sets `allowMSAA = false` BEFORE `OffscreenCameraSetup.AdoptGameCameraImage`, which copies `Camera.main.allowMSAA` over it. No cost today (URP takes the MSAA count from the target texture, `antiAliasing = 1`), but the line does not do what it reads as. `ScopePipView` has the right order (Adopt, then `allowMSAA = false`). | `grep -n "allowMSAA" WormholeMouth.cs OffscreenCameraSetup.cs ScopePipView.cs` | none — swap the two lines |
| W3 | inconsistency (left) | `PlatformProfileSO.foldGateWindowMaxRenderScale` now caps the wormhole exact views; the name describes a retired system. Kept to avoid migrating the tier assets. | its tooltip says so | rename with `[FormerlySerializedAs]` + re-save the profile set, or leave? |
| W4 | dead surface (walked past) | `CustomCameraController.PendingPortalShift` is public with zero readers (it was before this branch too). | `grep -rn PendingPortalShift Assets` → the declaration only | delete? |
| W5 | incompleteness (report) | With no `CopyTextureSupport.DifferentTypes` the panorama never captures, so beyond exact range a mouth shows `_VoidColor`. No device in the shipped tiers is known to lack it — unmeasured. | `WormholeMouth.EnsurePanorama` | does any target device report no `DifferentTypes`? |
| W6 | incompleteness (report) | No HUD marker for a standing fold pair, and no AI uses one (carried over from the ring gates). | `BUTTERFLY_FOLD.md` § Follow-ups | design, not cleanup |

