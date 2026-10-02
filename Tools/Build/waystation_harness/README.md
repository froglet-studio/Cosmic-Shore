# waystation_harness

Compiles and RUNS the **shipped** course C# outside the editor, so
`Tools/Build/waystation_course.py --compile` can compare the offline model against the code the
game actually runs rather than against a transcription of it.

    python3 Tools/Build/waystation_course.py --compile

`Unity.cs` is a shim — just enough of `Vector3` and `Mathf` for two PURE files to compile. It is
not a reimplementation of anything: `WaystationCourse.cs` and `RaceCourseGeometry.cs` are
`<Compile Include="../../../Assets/...">`d verbatim, which is the whole point. Add a file to the
csproj only if it is also pulled straight out of `Assets/`.

Measured agreement over 4 intensities × 40 seeds (3,880 gates): **0.0059 u** of position and
**0.0005°** of axis — float32 against float64 on a course spanning a thousand units.
