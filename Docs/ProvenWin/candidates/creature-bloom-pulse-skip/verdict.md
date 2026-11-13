### creature-bloom-pulse-skip: Creature bodies skip the bloom pulse's Newton solve when openness is 0 (nearer than ~313 u, farther than ~693 u) -> **HOLD**

| API | A ms | B ms | B/A (p10-p90) | Speed-up | Pairs B faster | Identity |
|---|---|---|---|---|---|---|
| d3d11 | 1.563 | 1.581 | 1.0105 (0.959-1.070) | 0.99x | 32.5 % | bit-identical (0 px) |
| dx12 | 1.729 | 1.729 | 1.0008 (0.930-1.077) | 1.00x | 48.5 % | bit-identical (0 px) |
| vulkan | 1.701 | 1.218 | 0.7296 (0.668-0.819) | 1.37x | 100.0 % | bit-identical (0 px) |

Reasons:
- dx12: null control is off (B/A 0.9730, B won 55.0 %): the desktop is busy or the instrument is biased; re-run when the machine is idle
- d3d11: the negative control was NOT caught: the comparison cannot see this kind of error
- dx12: the negative control was NOT caught: the comparison cannot see this kind of error
- d3d11: probe_guard changed nothing: the guarded state never occurs in the bench (fix the bench state: game-state floats, globals, camera distance), so the measurement says nothing
- dx12: probe_guard changed nothing: the guarded state never occurs in the bench (fix the bench state: game-state floats, globals, camera distance), so the measurement says nothing
- d3d11: not categorically faster (B won 32.5 % of pairs, p90 B/A 1.0695)
- d3d11 state_open: output differs in this state (22500185 px)
- d3d11 state_open: the change COSTS 3.1 % in this state (B slower in 84.0 % of pairs)
- dx12: not categorically faster (B won 48.5 % of pairs, p90 B/A 1.0773)
- dx12 state_open: output differs in this state (22463172 px)
- vulkan state_open: output differs in this state (22497623 px)
