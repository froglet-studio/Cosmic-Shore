# Task-vector hybrids of the four single-plan specialists — a clear negative

`swarm_hybrid.py` (base G2 + sum of coefficient-weighted task vectors tau_k = specialist_k - G2, plus per-layer crossover). 16 of 24 recipes ran before a container restart; own-plan loss (default LossCfg) on each plan's own seeding:

```
 0 base           0/8 close  274.02 ma  13.9 sp  17.5 ch  15.3 ti  20.7
 1 sum x0.5       0/8 close  395.56 ma  32.3 sp  34.9 ch  39.2 ti  42.1
 2 sum x1.0       0/8 close  702.66 ma  81.8 sp  66.8 ch  72.2 ti  67.2
 3 mean           0/8 close  339.89 ma  32.8 sp  26.1 ch  24.6 ti  33.5
 4 only mass      0/8 close  355.49 ma   8.5 sp  38.6 ch  39.2 ti  64.9
 5 only space     0/8 close  388.89 ma  63.9 sp   9.5 ch  45.6 ti  52.5
 6 only charge    0/8 close  352.71 ma  54.4 sp  37.4 ch  13.4 ti  54.0
 7 only time      0/8 close  418.94 ma  57.0 sp  42.1 ch  55.3 ti  21.0
 8 all but mass   0/8 close  764.32 ma  86.6 sp  78.4 ch  95.3 ti  75.4
 9 all but space  0/8 close  580.66 ma  48.5 sp  53.7 ch  68.7 ti  53.3
10 all but charge 0/8 close  806.74 ma  86.6 sp  72.2 ch  90.0 ti  72.5
11 all but time   0/8 close  573.76 ma  46.9 sp  45.8 ch  57.8 ti  69.7
12 cross 12       0/8 close  403.57 ma  51.0 sp  40.3 ch  61.4 ti  17.2
13 cross 13       0/8 close  342.49 ma  46.1 sp  41.1 ch  14.4 ti  36.2
14 mix 14         0/8 close  309.86 ma  29.5 sp  29.2 ch  20.9 ti  20.3
15 mix 15         0/8 close  353.26 ma  40.9 sp  24.3 ch  27.1 ti  37.8
```

Every combination is far worse than its parts (sum x1.0: 67-82; mean: 25-34; G2 base 14-21). Each specialist alone ('only <plan>') is good on its own plan (whale 8.5, jelly 9.5, puffer 13.4, dragonfly 21.0) and bad elsewhere. The fine-tunes left the shared linear basin that task arithmetic needs, so the specialists cannot be blended; switching between them (ensemble_swarm.py, results/ensemble) is the way to combine them: own-plan 10.8 / 7.0 / 11.1 / 10.7, switches all fail.
