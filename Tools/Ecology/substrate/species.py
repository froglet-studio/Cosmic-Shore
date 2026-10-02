"""Species = parameter sets of the ONE substrate (core.py). No species owns code.

  grazer  - a cute school: curious about pilots (springs to a comfort ring, retreats when crowded/afraid),
            follows food scent, aligns, never aggressive (hunt/ring weights 0 in BOTH regimes)
  locust  - ONE parameter set, two regimes: solitary (shy, curious, slow, green) and gregarious (fast,
            convergent, coherent, black-yellow), flipped by the quorum s = density x hunger with hysteresis
  pack    - a hunter: stalks on a ring AHEAD of the pilot (a cut-off) while solitary; the same quorum
            (pack together around the pilot + hungry) flips it to strike (hunt, burst speed)
  leviathan - the grazer school with a body plan: sated or collectively frightened members condense into a
            creature (slots on a manta-like body) and dissolve again when hungry and calm
"""
from __future__ import annotations

from dataclasses import replace

import numpy as np

from .core import BodyPlan, Regime, SpeciesParams


def grazer(n0=180, seed=0) -> SpeciesParams:
    r = Regime(speed=34, burst=2.4, turn=2.6, accel=60, w_food=1.0, w_coh=0.35, w_align=0.5, w_sep=0.8,
               w_wander=0.35, w_curious=1.2, comfort=70, w_flee=1.6, w_alarm=0.8, w_threat=0.25,
               w_home=0.15, size=2.5, color=(0.45, 0.95, 1.0))
    return SpeciesParams(name="grazer", n0=n0, capacity=int(n0 * 2), solitary=r, gregarious=r, nbr_r=25,
                         dens_norm=6, metabolism=0.012, eat_r=7, eat_hunger=0.2, hunger_per_vol=0.025,
                         fear_gain=1.2, fear_decay=0.6, sense=200, curiosity_rate=0.4, birth_stock=80,
                         deposit_alarm=0.6, frac_k=4, attn_r=150, attn_urg=0.6, seed=seed)


def locust(n0=400, seed=0, hunger0=None) -> SpeciesParams:
    # solitary end tuned against Direction C's emotion probe (cute 0.86 / terrifying 0.95 on held-out seeds,
    # results/emotion.json); the gregarious end is untouched. Before the tune: speed 26, burst 2.0, comfort
    # 90, w_curious 1.0, no gait, aspect 1.5 -> read majestic 0.45 / cute 0.14.
    sol = Regime(speed=31.4, burst=2.44, turn=2.2, accel=50, w_food=1.0, w_coh=0.05, w_align=0.05, w_sep=1.4,
                 w_wander=0.5, w_curious=2.43, comfort=68.6, w_flee=1.4, w_hunt=0.0, w_alarm=0.6, w_threat=0.3,
                 crowd=0.5, gait_hz=2.15, gait_amp=27.6, aspect=1.06, size=2.2, color=(0.45, 0.95, 0.35))
    gre = Regime(speed=95, burst=1.5, turn=4.0, accel=160, w_food=0.6, w_coh=0.9, w_align=1.2, w_sep=0.6,
                 w_wander=0.08, w_curious=0.0, comfort=90, w_flee=0.0, w_hunt=1.8, w_alarm=0.0, w_threat=0.0,
                 crowd=2.0, aspect=2.2, size=3.2, color=(1.0, 0.82, 0.1))
    return SpeciesParams(name="locust", n0=n0, capacity=int(n0 * 1.6), solitary=sol, gregarious=gre, nbr_r=30,
                         dens_norm=5, metabolism=0.02, eat_r=7, eat_hunger=0.15, hunger_per_vol=0.02,
                         fear_gain=1.45, fear_decay=1.27, sense=220, curiosity_rate=0.33,
                         q_up=0.55, q_down=0.30, q_width=0.06, q_rate=0.5, q_contagion=0.6,
                         bite_r=8, bite_cool=1.2, birth_stock=90, deposit_alarm=0.3, frac_k=4, attn_r=150,
                         attn_urg=0.6, seed=seed)


def pack(n0=8, seed=0) -> SpeciesParams:
    stalk = Regime(speed=105, burst=1.25, turn=2.8, accel=140, w_food=0.0, w_coh=0.2, w_align=0.3, w_sep=1.0,
                   w_wander=0.15, w_curious=0.0, w_flee=0.0, w_hunt=0.25, w_ring=1.4, ring_r=110,
                   w_alarm=0.0, w_threat=0.0, size=5.0, color=(0.95, 0.45, 0.85))
    strike = replace(stalk, speed=150, burst=1.6, turn=3.6, accel=260, w_hunt=2.0, w_ring=0.3, ring_r=40,
                     w_sep=0.4, size=5.5, color=(1.0, 0.2, 0.25))
    return SpeciesParams(name="pack", n0=n0, capacity=n0 * 2, solitary=stalk, gregarious=strike, nbr_r=150,
                         dens_norm=3.0, metabolism=0.03, eat_r=8, eat_hunger=0.9, hunger_per_vol=0.01,
                         sense=900, q_up=0.75, q_down=0.35, q_width=0.08, q_rate=0.8, q_contagion=0.5,
                         bite_r=10, bite_cool=2.0, birth_stock=1e9, ring_roles=n0, deposit_threat=0.4,
                         deposit_alarm=0.0, frac_k=2, attn_r=250, attn_urg=0.6, starve_s=1e9, seed=seed, n_dirs=26)


def manta_slots(K=96, length=60.0, span=80.0, rng=None):
    """Slots on a manta-ish body: a flat diamond wing (most slots), a thick spine and a tail whip."""
    rng = rng or np.random.default_rng(3)
    nw, ns, nt = int(K * 0.6), int(K * 0.25), K - int(K * 0.6) - int(K * 0.25)
    u = rng.uniform(-1, 1, nw); v = rng.uniform(0, 1, nw) ** 0.7
    wing = np.stack([u * span / 2 * (1 - np.abs(u) ** 0.5 * 0.0) * v, rng.normal(0, 1.5, nw),
                     (1 - np.abs(u)) * length * 0.5 * (1 - v) - length * 0.1], 1)
    t = np.linspace(-0.3, 0.6, ns)
    spine = np.stack([rng.normal(0, 3, ns), rng.normal(0, 3, ns) + 3, t * length], 1)
    s = np.linspace(0, 1, nt)
    tail = np.stack([np.zeros(nt), np.zeros(nt), -length * 0.3 - s * length * 0.9], 1)
    return np.concatenate([wing, spine, tail])


def leviathan(n0=160, seed=0) -> SpeciesParams:
    """Assembly on SATIETY only. A fear trigger (attach_on_f ~0.3) works - the body forms ~6 s after a charge
    arrives - but against a pilot that kills on contact the condensed body is a pinata (139 of 160 members
    rammed in 8 s, assembly_demo charge run). Fear-assembly needs an armoured/dangerous body (Direction B)."""
    P = grazer(n0, seed)
    P = replace(P, name="leviathan", body=BodyPlan(slots=manta_slots(n0), scale=1.0, well=3.0, speed=40),
                attach_rate=0.8, attach_on_h=0.36, attach_off_h=0.55, attach_on_f=2.0, metabolism=0.012,
                eat_r=12, hunger_per_vol=0.03)
    P.solitary = replace(P.solitary, color=(0.6, 0.75, 1.0)); P.gregarious = P.solitary
    return P


def lurker(n0=12, seed=0) -> SpeciesParams:
    """An ambusher: sits on its own home (near-still), and the SAME quorum rule reading pilot PROXIMITY
    instead of density flips it to a lunge. Puffing up (size 2.5 -> 7) over the flip is the telegraph."""
    sit = Regime(speed=4, burst=1.0, turn=1.0, accel=20, w_food=0.0, w_coh=0.0, w_align=0.0, w_sep=0.5,
                 w_wander=0.05, w_curious=0.0, w_flee=0.0, w_hunt=0.0, w_alarm=0.0, w_threat=0.0, w_home=3.0,
                 size=2.5, color=(0.35, 0.45, 0.4))
    lunge = replace(sit, speed=230, burst=1.0, turn=5.0, accel=900, w_hunt=3.0, w_home=0.0, w_sep=0.2,
                    size=7.0, color=(1.0, 0.35, 0.1))
    return SpeciesParams(name="lurker", n0=n0, capacity=n0 * 2, solitary=sit, gregarious=lunge, nbr_r=40,
                         metabolism=0.004, eat_r=10, eat_hunger=0.2, sense=300, aggr_base=1.0, q_w_dens=0.0, q_w_prox=1.0,
                         q_hunger=0.0, q_up=0.5, q_down=0.25, q_width=0.04, q_rate=1.6, q_contagion=0.0,
                         bite_r=12, bite_cool=2.5, birth_stock=1e9, starve_s=1e9, deposit_alarm=0.0,
                         frac_k=1, seed=seed)


def stampede(n0=220, seed=0) -> SpeciesParams:
    """A herd: calm grazers whose quorum reads ALARM (hunger-independent). Frighten a few and the alarm field
    spreads the flip: the herd goes gregarious = fast, tightly aligned, and it TRAMPLES what it runs through."""
    calm = Regime(speed=22, burst=2.0, turn=2.0, accel=50, w_food=1.0, w_coh=0.4, w_align=0.4, w_sep=0.9,
                  w_wander=0.3, w_curious=0.3, comfort=150, w_flee=1.2, w_alarm=0.6, w_threat=0.3, size=4.0,
                  color=(0.85, 0.8, 0.55))
    run = replace(calm, speed=110, burst=1.2, turn=1.6, accel=120, w_coh=1.0, w_align=2.5, w_sep=0.5,
                  w_wander=0.02, w_curious=0.0, w_flee=0.4, w_alarm=1.5, trample=1.0, size=4.5,
                  color=(1.0, 0.55, 0.25))
    return SpeciesParams(name="stampede", n0=n0, capacity=int(n0 * 1.5), solitary=calm, gregarious=run,
                         nbr_r=35, dens_norm=6, metabolism=0.008, eat_r=9, sense=260, fear_gain=1.6,
                         q_w_dens=0.0, q_w_alarm=1.0, q_hunger=0.0, q_up=0.35, q_down=0.08, q_width=0.04,
                         q_rate=0.9, q_contagion=0.7, bite_r=9, bite_cool=1.0, birth_stock=120,
                         deposit_alarm=1.2, frac_k=4, attn_r=150, attn_urg=0.6, seed=seed)


ANCHOR = dict(lurker="mass")

SPECIES = dict(grazer=grazer, locust=locust, pack=pack, leviathan=leviathan, lurker=lurker, stampede=stampede)
