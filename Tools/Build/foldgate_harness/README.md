# `foldgate_harness` — the Butterfly's gate geometry, compiled and RUN

Compiles **`Assets/_Scripts/Controller/Vessel/R_VesselActions/FoldGateGeometry.cs` verbatim**
against a small `UnityEngine` shim (`Unity.cs` — `Vector3` and `Mathf` reimplemented faithfully,
not stubbed, because the harness is proving this file's answers) and flies a simulated pilot
through a pair of gates.

```
~/.dotnet/dotnet run --project Tools/Build/foldgate_harness
```

It exists for one reason. The **arming latch** — *a vessel may only be taken by a gate it has been
clear of* — is the piece of this ability where a mistake is not a nuance but "every fold teleports
you straight back", because the destination gate is laid AROUND the arriving pilot. That is
invisible to every textual gate in the repo and to a Roslyn type check, and it is a rule nobody
had watched work.

Six properties, four negative controls, and **every control must fire**:

| check | the property |
|---|---|
| 1 | a pilot flying out of their own arrival gate is not taken *(control: without the latch, that very first step crosses)* |
| 2 | having flown clear, flying back through IS taken |
| 3 | every exit lands inside the far gate's near zone — which is what makes disarming at the far end *sufficient* rather than approximately right *(control: one unit deeper lands outside)* |
| 4 | lateral offset is preserved — enter near the rim, leave near the rim |
| 5 | momentum reads through the gate — the side you were heading for is the side you emerge on |
| 6 | a pass just outside the rim is never taken *(control: the same flight just inside IS)* |
| 7 | a round trip is an involution |

`Sim` mirrors `FoldGate.Update`'s per-vessel bookkeeping exactly (arm latch, two-sample rule,
transit, re-seed, disarm) and calls the shipped geometry for every decision — so what it proves is
the shipped rule, not a transcription of it.

**What it does NOT prove:** anything about the MonoBehaviour around it — the domain filter, the
`IsNetworkOwner` gate, the ring's paint, the bloom, the retire, or the replication of either end's
position. Those are editor and multiplayer questions.

Design record: `_Scripts/Controller/Vessel/R_VesselActions/BUTTERFLY_FOLD.md`
§ "Every fold leaves a PAIR OF GATES standing".
