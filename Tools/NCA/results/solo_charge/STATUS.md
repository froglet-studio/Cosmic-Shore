# solo_charge status

- updated: 2026-10-01T14:38:53Z
- step: 2999 / 3000
- rule_latest.pt <- rule_03000.pt

```
  mass    -> space   mass:  59.09  space:  47.23  charge:  28.73* time:  66.06   majority now charge, n=280 el=[139, 15, 76, 50]
  space   -> charge  mass:  58.73  space:  40.01  charge:  23.38* time:  60.38   majority now charge, n=280 el=[146, 1, 79, 54]  (could not switch: too few of the new element)
  charge  -> time    mass:  58.03  space:  52.41  charge:  13.62* time:  64.03   majority now charge, n=280 el=[174, 3, 39, 64]
  time    -> mass    mass:  72.58  space:  49.97  charge:  45.40* time:  72.12   majority now charge, n=265 el=[152, 5, 103, 5]
score 6366.5 (2/8 tests pass: own plan x4, switch x4); best so far 4341.8
```

## Finished (3000/3000, exit 0)

- `rule_latest.pt` = `rule_03000.pt` (the final genome, for the hybrid stage).
- Last scoring (step 3000): score 6366.5, 2/8 tests pass. The best scoring was at step 1000
  (4341.8, 4/8: own plan x4 = charge passing on every seeding) and is what was published to
  `Tools/NCA/results/swarm_coevo_solo_charge/rule.pt`.
- Charge loss on its own seeding at the end: ~10.7 (start ~15); max |state| stayed 5-9 throughout.
- `probe.json`: swarm_probe of rule_latest.pt (charge before 10.66, cut 18.58, recovered 15.13, heal 0.436).
