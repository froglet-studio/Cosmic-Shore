### example-w4-sky-stars: HyperSea sky: reject empty star cells before the per-cell maths (W4, re-run as the worked example) -> **PASS**

| API | A ms | B ms | B/A (p10-p90) | Speed-up | Pairs B faster | Identity |
|---|---|---|---|---|---|---|
| d3d11 | 34.310 | 31.606 | 0.9202 (0.902-0.938) | 1.09x | 100.0 % | bit-identical (0 px) |
| dx12 | 36.308 | 34.031 | 0.9373 (0.932-0.944) | 1.07x | 100.0 % | bit-identical (0 px) |
| vulkan | 32.560 | 30.334 | 0.9308 (0.925-0.937) | 1.07x | 100.0 % | bit-identical (0 px) |

Reasons:
- identity tier: bit-identical
