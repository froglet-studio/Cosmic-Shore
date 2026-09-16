# Gibbon tether harness

Drives the **shipped** `Assets/_Scripts/Controller/Vessel/TetherSolver.cs` — compiled from its real
path by `gibbon.csproj`, never copied — through Unity-shaped stubs with real vector maths, and
asserts the swing.

```
dotnet run -c Release --project Tools/Build/gibbon_tether_harness
```

Exits non-zero on any failing assertion, so it is usable as a build gate.

## Why this exists

A swing that explodes on a hitch frame, ratchets a line longer instead of shorter, gains free
energy, or flies a different arc at 30 fps than at 120 is **invisible in code review and obvious in
one run**. The previous attempt at this vessel recorded three such defects that its own harness
caught and that review had missed. This one caught a fourth on its first run: `T9`, the
rest-length floor paying a line back OUT — the exact chatter bug that branch documented — which is
why `TetherSolver.ApplyReel` exists as a named function instead of a one-liner at the call site.

## What each assertion is protecting

| | Claim |
|---|---|
| T1 | A rope is silent while slack — force exists only past the rest length |
| T2 | **No reel means no free energy**: a pure rope redirects, it cannot accelerate |
| T3 | Reeling a swing genuinely beats flying speed |
| T4 | 30 Hz and 120 Hz fly the same arc (the exponential damper, not `v - c*v*dt`) |
| T5 | The speed cap holds across a long reel |
| T6 | A 4 fps hitch frame cannot explode the explicit spring |
| T7 | Overloading a line breaks it rather than producing unbounded tension |
| T8 | Two lines compose by **vector addition** and stay bounded |
| T9 | The rest-length floor STOPS a reel and never pays a line out |
| T10 | A taut line does no tangential work — this is what conserves the swing |
| T11 | The cap binds when the geometric floor does not |
| LADDER | Four alternating grabs from cruise clear 2x cruise, each grab shorter than the last |

`T2` and `T10` are the negative controls: they fail if anyone ever "helpfully" damps the full
velocity vector or adds a bonus to the swing.
