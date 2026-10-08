## Whole-cell fidelity (cell_js.js vs cell_py.py)

### Levels: 93/93 agree

| pilot | metric | class | JS | Python | diff | tol | |
|---|---|---|---:|---:|---:|---:|---|
| wander | eat grazer<-env | shared | 22140.83 | 20503.99 | 1636.84 | 7749.29 | ok |
| wander | eat leviathan<-env | shared | 380.11 | 342.69 | 37.42 | 1085.59 | ok |
| wander | eat leviathan<-wake | shared | 215.0 | 175.55 | 39.45 | 153.09 | ok |
| wander | eat locust<-env | shared | 5810.53 | 7383.79 | -1573.26 | 3458.03 | ok |
| wander | eat locust<-wake | shared | 1466.67 | 1887.22 | -420.55 | 876.97 | ok |
| wander | eat snaptrap<-env | shared | 471.79 | 137.06 | 334.73 | 440.54 | ok |
| wander | eat snaptrap<-wake | shared | 51.11 | 156.66 | -105.55 | 150.0 | ok |
| wander | eat stampede<-env | shared | 656.26 | 578.87 | 77.39 | 325.66 | ok |
| wander | eat stampede<-wake | shared | 172.22 | 161.11 | 11.11 | 150.0 | ok |
| wander | hits locust |  | 52.83 | 49.72 | 3.11 | 85.37 | ok |
| wander | hits lurker |  | 1.44 | 1.0 | 0.44 | 1.82 | ok |
| wander | hits mobber |  | 0.78 | 1.17 | -0.39 | 2.07 | ok |
| wander | hits pack |  | 4.28 | 4.11 | 0.17 | 1.33 | ok |
| wander | hits stampede |  | 8.22 | 8.61 | -0.39 | 5.12 | ok |
| wander | hits thief |  | 17.78 | 31.56 | -13.78 | 15.42 | ok |
| wander | hits total |  | 86.06 | 97.0 | -10.94 | 76.77 | ok |
| wander | move fortress<-env | shared | 44668.64 | 50024.73 | -5356.1 | 47142.33 | ok |
| wander | move fortress<-wake | shared | 35332.22 | 24501.66 | 10830.56 | 12366.28 | ok |
| wander | pop_end fortress |  | 48.0 | 48.0 | 0.0 | 7.2 | ok |
| wander | pop_end grazer |  | 1600.0 | 1600.0 | 0.0 | 240.0 | ok |
| wander | pop_end leviathan |  | 140.0 | 140.0 | 0.0 | 21.0 | ok |
| wander | pop_end locust |  | 360.0 | 360.0 | 0.0 | 54.0 | ok |
| wander | pop_end lurker |  | 16.0 | 16.0 | 0.0 | 3.0 | ok |
| wander | pop_end mobber |  | 50.0 | 50.0 | 0.0 | 7.5 | ok |
| wander | pop_end pack |  | 7.0 | 7.0 | 0.0 | 3.0 | ok |
| wander | pop_end snaptrap |  | 45.0 | 42.5 | 2.5 | 6.75 | ok |
| wander | pop_end stampede |  | 72.0 | 72.0 | 0.0 | 10.8 | ok |
| wander | pop_end thief |  | 18.0 | 18.0 | 0.0 | 3.0 | ok |
| wander | pop_mean fortress |  | 48.0 | 48.0 | 0.0 | 7.2 | ok |
| wander | pop_mean grazer |  | 1570.14 | 1571.09 | -0.94 | 235.66 | ok |
| wander | pop_mean leviathan |  | 140.0 | 140.0 | 0.0 | 21.0 | ok |
| wander | pop_mean locust |  | 244.15 | 262.53 | -18.38 | 40.36 | ok |
| wander | pop_mean lurker |  | 16.0 | 16.0 | 0.0 | 3.0 | ok |
| wander | pop_mean mobber |  | 50.0 | 50.0 | 0.0 | 7.5 | ok |
| wander | pop_mean pack |  | 7.0 | 7.0 | 0.0 | 3.0 | ok |
| wander | pop_mean snaptrap |  | 43.36 | 41.46 | 1.9 | 6.5 | ok |
| wander | pop_mean stampede |  | 72.0 | 72.0 | 0.0 | 10.8 | ok |
| wander | pop_mean thief |  | 18.0 | 18.0 | 0.0 | 3.0 | ok |
| wander | steal fortress<-env | shared | 790.56 | 805.0 | -14.44 | 1030.38 | ok |
| wander | steal fortress<-wake | shared | 521.11 | 351.67 | 169.44 | 182.39 | ok |
| hunter | crystals fortress |  | 9.94 | 8.06 | 1.89 | 3.05 | ok |
| hunter | crystals grazer |  | 0.89 | 2.83 | -1.94 | 4.54 | ok |
| hunter | crystals leviathan |  | 4.72 | 5.22 | -0.5 | 2.35 | ok |
| hunter | crystals locust |  | 9.94 | 20.5 | -10.56 | 20.72 | ok |
| hunter | crystals lurker |  | 2.06 | 1.5 | 0.56 | 1.08 | ok |
| hunter | crystals mobber |  | 1.0 | 2.44 | -1.44 | 5.56 | ok |
| hunter | crystals pack |  | 1.94 | 1.72 | 0.22 | 0.58 | ok |
| hunter | crystals thief |  | 0.61 | 1.5 | -0.89 | 2.04 | ok |
| hunter | eat grazer<-env | shared | 21802.69 | 21281.68 | 521.01 | 7630.94 | ok |
| hunter | eat leviathan<-env | shared | 143.22 | 283.01 | -139.78 | 674.03 | ok |
| hunter | eat leviathan<-wake | shared | 607.78 | 492.22 | 115.55 | 312.0 | ok |
| hunter | eat locust<-env | shared | 7026.51 | 6173.63 | 852.88 | 5199.21 | ok |
| hunter | eat locust<-locust | own | 286.21 | 873.11 | -586.9 | 925.86 | ok |
| hunter | eat locust<-wake | shared | 1185.56 | 1942.22 | -756.66 | 1725.61 | ok |
| hunter | eat snaptrap<-env | shared | 558.32 | 163.31 | 395.01 | 497.1 | ok |
| hunter | eat stampede<-env | shared | 597.29 | 792.47 | -195.18 | 577.01 | ok |
| hunter | eat stampede<-wake | shared | 318.33 | 260.0 | 58.33 | 351.43 | ok |
| hunter | hits fortress |  | 1.06 | 0.94 | 0.11 | 1.0 | ok |
| hunter | hits leviathan |  | 2.39 | 2.44 | -0.06 | 1.0 | ok |
| hunter | hits locust |  | 3.28 | 8.78 | -5.5 | 8.27 | ok |
| hunter | hits lurker |  | 1.17 | 0.89 | 0.28 | 1.13 | ok |
| hunter | hits mobber |  | 0.67 | 1.72 | -1.06 | 4.46 | ok |
| hunter | hits stampede |  | 7.56 | 7.28 | 0.28 | 8.64 | ok |
| hunter | hits thief |  | 14.33 | 23.06 | -8.72 | 21.55 | ok |
| hunter | hits total |  | 31.72 | 46.67 | -14.94 | 24.52 | ok |
| hunter | move fortress<-env | shared | 28264.27 | 32855.97 | -4591.7 | 39645.06 | ok |
| hunter | move fortress<-grazer | cross | 204.88 | 502.74 | -297.86 | 1044.45 | ok |
| hunter | move fortress<-leviathan | cross | 304.44 | 146.66 | 157.78 | 254.4 | ok |
| hunter | move fortress<-locust | cross | 3768.74 | 3204.92 | 563.82 | 9682.2 | ok |
| hunter | move fortress<-lurker | cross | 205.55 | 120.0 | 85.55 | 282.23 | ok |
| hunter | move fortress<-pack | cross | 648.0 | 330.67 | 317.33 | 1032.66 | ok |
| hunter | move fortress<-wake | shared | 50722.78 | 44680.56 | 6042.22 | 17752.97 | ok |
| hunter | pop_end fortress |  | 37.67 | 39.5 | -1.83 | 5.92 | ok |
| hunter | pop_end grazer |  | 1600.0 | 1600.0 | 0.0 | 240.0 | ok |
| hunter | pop_end leviathan |  | 126.33 | 124.5 | 1.83 | 18.95 | ok |
| hunter | pop_end locust |  | 360.0 | 360.0 | 0.0 | 54.0 | ok |
| hunter | pop_end lurker |  | 9.83 | 11.5 | -1.67 | 3.24 | ok |
| hunter | pop_end mobber |  | 47.0 | 42.67 | 4.33 | 16.67 | ok |
| hunter | pop_end snaptrap |  | 46.17 | 42.17 | 4.0 | 6.92 | ok |
| hunter | pop_end stampede |  | 71.83 | 71.67 | 0.17 | 10.77 | ok |
| hunter | pop_end thief |  | 16.17 | 13.5 | 2.67 | 6.13 | ok |
| hunter | pop_mean fortress |  | 42.02 | 42.69 | -0.66 | 6.4 | ok |
| hunter | pop_mean grazer |  | 1570.21 | 1571.1 | -0.89 | 235.67 | ok |
| hunter | pop_mean leviathan |  | 131.86 | 133.29 | -1.43 | 19.99 | ok |
| hunter | pop_mean locust |  | 254.81 | 244.45 | 10.35 | 53.29 | ok |
| hunter | pop_mean lurker |  | 12.51 | 13.77 | -1.25 | 3.0 | ok |
| hunter | pop_mean mobber |  | 47.86 | 44.91 | 2.95 | 12.56 | ok |
| hunter | pop_mean pack |  | 2.84 | 3.47 | -0.63 | 3.0 | ok |
| hunter | pop_mean snaptrap |  | 43.87 | 41.15 | 2.73 | 6.58 | ok |
| hunter | pop_mean stampede |  | 71.92 | 71.86 | 0.06 | 10.79 | ok |
| hunter | pop_mean thief |  | 17.16 | 16.16 | 0.99 | 3.0 | ok |
| hunter | steal fortress<-env | shared | 552.41 | 600.78 | -48.36 | 917.99 | ok |
| hunter | steal fortress<-wake | shared | 1240.0 | 1043.33 | 196.67 | 434.0 | ok |

