# converter_reel

Sources, data and gates for the **Converter Reel** lab (`Docs/Studios/ConverterReelLab.html`). The write-up, with the
measurements, is `Docs/Studios/CONVERTER_REEL_LAB.md`.

| File | What it is |
|---|---|
| `src/combineCore.js` | The converter's motion, ported from `OmniCombineCore` / `OmniCombineMotion` / `SquareTurnFit` / `OmniCadence` |
| `src/chargeArcs.js`, `src/chargeArcsShader.js` | The Charge light (`ChargeArcs.cs`) and `CrystalChargeArcsShader` |
| `src/depthPrisms.js`, `src/depthPrismsShader.js` | The depth prisms (`DepthRatchet`, `CrystalDepthPrisms`, the converter's columns) and `OmniDepthPrismShader` |
| `src/reel.js` | The loop: build, hold, FIFO pull-out, per-crystal clocks, framing |
| `src/reelExtras.js`, `src/reelCharge.js` | The stateful layers as `OmniCombinePlayer` wires them, baked over two loops |
| `src/render.js` | WebGL2 renderer: HDR + MSAA + supersampling, bloom, tonemap, the body shader |
| `src/app.js`, `page.html` | The lab page: the colour lab, the timeline, shared looks, the exporter, the `__lab` hooks |
| `build.py` | Builds the page from the shipped assets and `src/` (`--check` for staleness) |
| `extract_depth.py`, `extract_charge_arcs.py` | Read the depth / arc materials, profiles and meshes into `data/` (`--check`). `extract_depth.py` needs numpy |
| `parity_core/run.sh`, `parity_depth.cjs`, `run_parity_charge.sh` | The three gates against the shipped C# (.NET 8 at `$HOME/.dotnet`) |
| `render_headless.cjs` | Frame-exact headless render to ffmpeg (Playwright on `NODE_PATH`) |

The `harness.csproj` files are force-added (`git add -f`), because the repo gitignores `*.csproj`.

After changing anything in `src/` or `page.html`, run `python3 build.py`, then run the gate for the module you touched,
then `verify_lab.cjs`.
