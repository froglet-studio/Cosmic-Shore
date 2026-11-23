# waystation_tower_harness

Compiles and RUNS the **shipped** Waystation Drop Tower C# outside the editor, so
`Tools/Build/waystation_tower_course.py --compile` compares the offline model against the code the game runs.

    python3 Tools/Build/waystation_tower_course.py --compile

`WaystationTower.cs`, `WaystationCourse.cs` (Garrett's, unedited) and `RaceCourseGeometry.cs` are
`<Compile Include="../../../Assets/...">`d verbatim. `Unity.cs` is Garrett's `waystation_harness/Unity.cs` plus
`ProjectOnPlane`, `Distance` and a `Quaternion` that only has to exist; `Stubs.cs` carries the two outside members
`WaystationTower.cs` reads (`RaceCourseSource.DefaultInnerRadiusNucleusFactor`, `TigerMothDropMath.Ease01`), which
`--compile` asserts against the real files. The csproj is written by `--compile` (the repo ignores `*.csproj`).
