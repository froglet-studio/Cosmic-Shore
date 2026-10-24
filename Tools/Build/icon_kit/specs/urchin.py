"""The Urchin's four ability icons (Resources/ElementalAbilityMaps/Urchin.asset), angular, one object each.

  Charge  Chain Spikes     the volley: the spiked ball, two spikes already flying off it (kept close to the
                           shipped star, which read; redrawn angular so the family matches)
  Mass    Trail Rider      the Urchin ON the ribbon: the ball sitting on a notched trail band
  Space   Track Projector  the rail thrown out ahead: a track receding to the horizon off the hull's nose
  Time    Slip             letting go: the ball lifted off the band, its ghost still on it

Urchin_ChargeRing.png is NOT an ability icon - it is the hold-charge GAUGE ring the HUD draws round the
Charge card (author_urchin_hud.py) - and stays as it is.
Before (author_urchin_hud.py placeholders, 2026-10): a 12-point star, a ball on a dotted line, a ladder,
three overlapping circles.
"""
from canvas import disc, fill, rect, shift
from motifs import hull, rail, spiked_ball

HULL = "Urchin"


def _trail_band(y0=96, y1=114, notches=4):
    m = rect(6, y0, 122, y1)
    w = (122 - 6) / notches
    for i in range(1, notches):
        x = 6 + i * w
        m &= ~fill([(x - 6, y0), (x + 6, y0), (x, y0 + 9)])
    return m


def chain_spikes():
    ball = spiked_ball(56, 72, 28, spikes=12, spike=0.6, rot=15, width=0.4)
    # Two spikes already flying: darts leaving the ball up and to the right.
    s1 = fill([(92, 36), (122, 8), (104, 44)])
    s2 = fill([(100, 60), (121, 48), (110, 72)])
    return ball | s1 | s2


def trail_rider():
    band = _trail_band()
    ball = spiked_ball(64, 58, 24, spikes=12, spike=0.58, rot=15, width=0.4)
    return band | ball


def track_projector():
    # The track as a road to the horizon off the nose: one solid taper, the single track's groove cut down it.
    nose = hull(64, 110, 18, heading=-90)
    track = fill([(64 - 36, 92), (64 + 36, 92), (64 + 9, 8), (64 - 9, 8)])
    for y0, y1, w in ((72, 88, 9), (48, 60, 7), (28, 37, 5.5)):
        track &= ~rect(64 - w / 2, y0, 64 + w / 2, y1)
    return nose | track


def slip():
    band = _trail_band(y0=100, y1=116, notches=4)
    ghost = spiked_ball(38, 76, 19, spikes=12, spike=0.58, rot=15, width=0.4)
    lifted = spiked_ball(86, 40, 19, spikes=12, spike=0.58, rot=15, width=0.4)
    return [(band | lifted, 1.0), (ghost, 0.5)]


ICONS = [
    dict(name="Urchin_ChainSpikes", element="Charge", act="Chain Spikes", draw=chain_spikes),
    dict(name="Urchin_TrailRider", element="Mass", act="Trail Rider", draw=trail_rider),
    dict(name="Urchin_TrackProjector", element="Space", act="Track Projector", draw=track_projector),
    dict(name="Urchin_Slip", element="Time", act="Slip", draw=slip),
]
