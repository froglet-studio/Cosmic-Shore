# Thresher Plant Lab — parity gate

Holds the Thresher Plant Lab's JS chain solver (`Docs/Studios/ThresherPlantLab.html`) to the **shipped**
`Assets/_Scripts/Controller/Vessel/ThresherChainSolver.cs`, frame by frame. Write-up:
`Docs/Studios/THRESHER_PLANT_LAB.md`.

```
node Tools/Build/thresher_plant_lab/parity.cjs                 # builds the reference with trace.sh, then compares
node Tools/Build/thresher_plant_lab/parity.cjs --ref ref.json  # reuse a reference trace
bash Tools/Build/thresher_plant_lab/trace.sh ref.json          # the reference trace alone
```

- `trace.sh` compiles the shipped solver from its real path with the chain harness's Unity stubs
  (`Tools/Build/thresher_chain_harness/Stubs.cs`) and `TraceDriver.cs`, and runs `scenarios.json`: scripted
  plant cycles flown by the chain harness's stand-in pilot. Needs the per-user .NET 8 SDK
  (`Tools/Build/thresher_chain_harness/run.sh` prints the install line).
- `parity.cjs` opens the page in headless Chromium (Playwright via `NODE_PATH`, CommonJS only; see labmaker
  L-GEN-2) and runs the same scenarios through `window.__lab.parityTrace`, so it measures the code the page
  flies. Pass: the mode equal on every frame, positions within 0.02 u + 2·10⁻⁴ × the ship's distance from the
  origin (float32 vs float64 rounding, labmaker L-GEN-7). Three planted dial changes must fail. Exit 1 on any
  failure.

It covers the SHIPPED path only (every lab dial at its shipped value). The lab's approaches, the transformer
and the camera are lab code, re-implemented, not ported.
