# drill_harness

Compiles the SHIPPED microgame drill data layer (`Assets/_Scripts/Controller/Arcade/Preview/Drill/`)
plus the real `ElementalAbilityMapSO`, `ControlGlyphSetSO`, `InputHintBindingMap` and
`DrillProgressCloudData` against a small UnityEngine stub (`Unity.cs`), and runs `Driver.cs`:
token resolution, conditions, Lesson and Mentor composition, the first-time skip rule, the progress
store's mirror/cloud merge, and hull facts. `HintBinding` is extracted from the shipped
`InputDeviceIconSetSwitcher.cs` at build time rather than retyped.

    bash Tools/Build/drill_harness/run.sh     # exit 0 = pass

Needs a dotnet 8 SDK (see `../race_course_source_harness/run.sh` for a per-user install line).
What it does NOT prove: anything about Unity serialization of the `[SerializeReference]`
conditions, or the assets loading - that needs the editor.
