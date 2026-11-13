### paper-burn-ember-skip: PrismPaperBurn skips the ember flicker noise and ember maths where the ember is exactly zero (outside the char band, or before ignition) -> **FAIL**

| API | A ms | B ms | B/A (p10-p90) | Speed-up | Pairs B faster | Identity |
|---|---|---|---|---|---|---|
| vulkan | 4.289 | 4.394 | 1.0156 (0.914-1.141) | 0.98x | 39.0 % | different (333239 px) |

Reasons:
- vulkan: output differs (333239 px at 8 bits, up to 1 levels): a picture change is decisive, no other measurement can rescue it
- only ['vulkan'] measured; a shader win is measured on dx12, vulkan and d3d11 (Vulkan is the phone path)
- an early-out needs both probes (probe_reach: return a constant always; probe_guard: only inside the guard). Without them a 'no gain' or an 'identical' may mean the bench never reached the state
- vulkan: not categorically faster (B won 39.0 % of pairs, p90 B/A 1.1413)
- vulkan state_convert: output differs in this state (21676 px)
- vulkan state_early: output differs in this state (46263 px)
