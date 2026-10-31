# Ark escort sim farm

N Windows players play Ark escorts with every seat on the AI, from a job folder, for tuning and for
co-evolving escort AI params against fauna params.

- `Assets/_Scripts/Editor/Build/SimFarmBuild.cs` builds the player: a Windows x64 development player, Mono, with the
  browser Toybox's scenes and `FROGLET_WEB_TOYBOX` (no UGS, in-memory transport). Product name
  "Cosmic Shore SimFarm", so its PlayerPrefs and persistent data stay apart from the game's. ProjectSettings are
  restored after the build.
- `Assets/_Scripts/Controller/Voyages/ArkEscortSim.cs` is the entry point: `-arkEscortSim @job.json`, `-arkEscortSeed`,
  `-arkEscortFixedDt`, `-arkEscortDrain` and `-arkEscortQuit` (writes a `{"done":true}` line, then quits).
- `farm.py` is the runner (slots, crash, stall and timeout handling, requeue, stats), `farmlib.py` the job folder,
  `coevo.py` the co-evolution driver, `pareto.py` the archive and the FIT/DIVERSIFY regime.

```
# build (the editor must not have the project open)
Unity.exe -batchmode -quit -nographics -buildTarget StandaloneWindows64 -projectPath <wt> ^
  -executeMethod CosmicShore.Editor.SimFarmBuild.Build -buildOutput D:\VoyageFarm\build -logFile D:\VoyageLogs\simfarm_build.log

python Tools/SimFarm/farm.py run --root D:/VoyageFarm -n 4 --mode nographics
python Tools/SimFarm/farm.py submit --root D:/VoyageFarm --plan "1:Sparrow,Sparrow,Sparrow" --jobs 4 --runs-per-job 2
python Tools/SimFarm/farm.py status|stats|report --root D:/VoyageFarm
python Tools/SimFarm/coevo.py --root D:/VoyageFarm --session s1 --space Tools/SimFarm/space.example.json --generations 40
python Tools/SimFarm/coevo.py --root D:/VoyageFarm --session dry --fake --generations 6   # no Unity
```

Params are opaque to the farm. A job's `params` dict is written to `params/<jobId>.json` and passed as
`-arkEscortParams`, which the escort run reads. The gene names in `space.example.json` must match that registry's keys.
