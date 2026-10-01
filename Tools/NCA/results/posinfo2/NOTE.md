# posinfo2 - interim (20:25 UTC): 11/13 feasible at seed 7, LOSSLESS (0 self-deaths)

Run B step 400 (results/posinfo2/rule.pt): own 3.33 / 4.09 / 3.89 / 3.65 (whale / jelly / puffer / dragonfly),
switches 7/9 pass (mass->space 5.5, space->charge 3.8, charge->time 8.5 (1/3), time->mass 10.0 (2/3),
mass->charge 4.3, mass->time 8.0, space->time 2/3, charge->space 2/3, time->space 0/3).
Model: posinfo's learned rule (G2 + body-frame positional info) with the learned death channel MASKED,
a designed SCALED (element, domain) quota + molting homeostat (homeo 5), fine-tuned by BPTT with
yardstick-fair culls in training. Full write-up, scorecard (seeds 7/23/41), probe and rollout follow.
