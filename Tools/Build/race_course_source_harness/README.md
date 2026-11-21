# race_course_source_harness

Compiles the SHIPPED `RaceCourseSource` and every per-mode source, plus the course generators
they call, against a UnityEngine stub, and RUNS `Driver.cs`:

    Tools/Build/race_course_source_harness/run.sh

- **Pure move.** For Switchback, Headlong, Redline, Breakwater and Waystation, the source's course
  is compared bit for bit against the pre-extraction controller bodies (copied verbatim into
  `Driver.cs`) over 40 seeds x 4 intensities x {no cell, the shipped nucleus}. Laps, lead-in,
  authored target and shell are compared too.
- **Arena-built sources** (Skein, Regatta) are exercised on their failure path: no config
  must report a sentence and build nothing.
- `GateRaceController`'s two static fold methods and the overrides' `Default*` constants are
  EXTRACTED from the shipped files into `Generated.cs` by `run.sh`, never retyped.

Negative controls run when this landed: nudging Switchback's first-gate distance 620 -> 621
failed 320 pairs; changing Breakwater's inner fallback 420 -> 480 failed 160.

What it does NOT prove: `GateRaceController` and its subclasses are NetworkBehaviours and cannot
compile here - their edit is small (each now returns a source from `CreateCourseSource`) and is
held by `RaceCourseSourceTests` in the editor.
