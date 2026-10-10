# Thresher chain harness

Compiles and RUNS the **shipped** `Assets/_Scripts/Controller/Vessel/ThresherChainSolver.cs` and the
**shipped** edit-mode suite `Assets/_Scripts/Tests/Editor/ThresherChainSolverTests.cs` — both from
their real paths, never copied — against Unity-shaped stubs with real vector maths (`Stubs.cs`,
including the dozen lines of NUnit the suite uses), then prints the feel table recorded in
`R_VesselActions/THRESHER.md` §4 (chain physics) and §5a (camera distances and spin rates).

```
bash Tools/Build/thresher_chain_harness/run.sh
```

Exit 1 on any failing test. Needs a per-user .NET 8 SDK under `$DOTNET_ROOT` or `~/.dotnet`
(`run.sh` prints the one-line install if it is missing).

## What it proves, and what it does not

It proves the solver's rules — rope, floor, spin conservation, crack, lock, plough — and the four
behaviours the vessel was specified against (a lazy turn stays under smash speed, a snap plus
reel-in crack exceeds it, the ship is never dragged below `minShip`, the lock never stalls), on a
stand-in pilot that eases toward cruise the way the vector flight model's nose step does. It also
runs the planted-steering rules (`SteerPivot`) and the pure camera maths in the same file
(`ThresherCameraFraming`: the smallest chase distance that frames the ball, checked against an
independently built look-at frustum; the reach envelope; the out-fast/in-slow ease; the spectate
vantage). It does not run `ThresherVesselTransformer`, `ThresherExecutor` or
`CustomCameraController`; those are compiled by `Tools/Build/unity_refcompile` and verified in the
editor.

The feel table is the thing to re-read before retuning: it is where "the reel is the crack, not the
snap" was measured rather than guessed — and where the 2026-10-09 chain lengthening (62.6 → 140 u)
showed a let-out ball now falls slack in any steady turn (0.38–0.54× smash) instead of staying hot.
