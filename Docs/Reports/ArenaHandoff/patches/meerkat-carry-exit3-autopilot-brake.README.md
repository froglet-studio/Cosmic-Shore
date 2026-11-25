# meerkat-carry-exit3-autopilot-brake.patch (unmerged prototype, 2026-11-23)

This is the patch to apply to bleeding-edge 910c2778a with `git apply`. It holds the night-3 "ride's speed carries" prototype plus two changes:

1. **A bug fix in the night-3 prototype** (`meerkat-ride-carry.patch`):
   - **The bug:** `TrackedExitSpeed` caps a ride's way out at exit x the THROTTLE TARGET, and a carry raises that target. So a ride taken during a carry left at exit² x cruise: 9x at exit 3, with the Time-5 rush filling the gap.
   - **The fix:** `LeaveRide` now caps the way out at exit x plain cruise while a carry is on.
   - **What it changes:** the night-3 Regatta numbers (1.33x card / 1.45x L5) included that compounding. The capped, honest number is about 1.7x.
2. **An AI-only brake:**
   - **The rule:** while the ring the autopilot is after is within `aiCarryBrakeDistance` (250 u) and more than `aiCarryBrakeAngle` (45 degrees) off the nose, the autopilot flies plain cruise instead of the carry. That approach is the orbit shape that made Burrow Run I2-I4 time out. A human pilot always keeps the whole carry.
   - **The new fields:** `MeerkatSolitonConfigSO.aiCarryBrakeDistance` and `aiCarryBrakeAngle`. With both at 0 the brake is off.

Kit values: `SolitonTunnel.exitMultiplier` 3, `rideCarrySeconds` 10, `boostOut` 1.

Measured:
- Burrow Run I1-I4: 61 / 63 / 85 / 131 s (seeds 11-12, every race finishes).
- Regatta shuffle: 116.9 s card (1.66x), 105.7 s L5 (1.69x).

Not in the patch: the local Port fix (Shadow : UIBehaviour) that the judge needed on this trunk. Plan step: time-arcade. Not run in Unity.
