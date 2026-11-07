#!/usr/bin/env python3
"""The AI nose lead (AIPilot.NoseLeadGain, PR #1786) as ONE shared piece every offline race model imports.

The game's AI steers by its NOSE, but the nose follows the stick's COMMAND through a first-order lag
(VesselTransformer.LERP_AMOUNT 1.5/s, a 0.67 s time constant, every AI hull): judged from the nose, the pilot keeps
winding the command on while the nose is still catching up, the command overshoots, then the nose does, and the hull
crosses the ring plane beside the mouth. AIPilot.NoseLeadGain is the fix (Tools/Build/scarab_nose_lead_sim.py is the
reference model, and this module is its lead code lifted out so every model leads the same way):

- with a gain g > 0 the pilot JUDGES its error from the command, not the nose;
- the WANTED direction is the nose turned toward the aim by g times its error: g > 1 carries on past the aim, at most
  NoseLeadMaxExtraDegrees past it (and never past 170 deg from the nose), so the lagging nose is PULLED from beyond
  the aim and settles in about 0.67 / g s instead of overshooting;
- the gain is stated at the fleet lag and scaled by the hull's own nose rate (AIPilot.EffectiveNoseLeadGain:
  max(1, g * 1.5 / rate)), and it is 0 on a nose with no lag;
- it is OFF while the hull drifts, slings, boosts through a mode that does not steer by the nose, or has no nose lag
  in the model at all (a heading that turns at once has nothing to lead).

Every model takes the lead as a knob that is OFF by default (a NoseLead with gain 0, or None), so today's numbers
do not move until a job or a flag switches it on:

    from ai_nose_lead import NoseLead, judge_and_want
    lead = NoseLead.from_config(job_config)            # {"nose_lead": 2.5} or {"nose_lead": {"gain": 2.5, "max_extra_deg": 40}}
    g = lead.effective(nose_rate)                      # once per hull: 0 when off
    judge, want = judge_and_want(fwd, cmd, steer, g, lead.max_extra_deg)   # every frame, in place of (fwd, steer)
    s = ai_stick(ang(judge, want))                     # the model's own stick law, unchanged

Heading-angle models (a 2D heading in degrees instead of a direction vector) use lead_turn_deg(err_deg, g, extra)
for the turn the nose would be asked for, judged from the command's heading.

    python3 Tools/Build/ai_nose_lead.py --self-test
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

NOSE_FOLLOW = 1.5                 # VesselTransformer.LERP_AMOUNT: the fleet nose lag (1/s)
DEFAULT_MAX_EXTRA_DEG = 40.0      # AIPilot.NoseLeadMaxExtraDegrees as #1786 ships it
MAX_TURN_DEG = 170.0              # AIPilot.NoseLeadDirection never asks for a turn past 170 deg from the nose
CONFIG_KEY = "nose_lead"          # the job / evaluator config key every model reads
# The ROLL LAW while the lead is on (AIPilot: the roll stick YDiff of a dual-stick hull with no belly target), which
# NoseLeadSteering.RollStick decides in the game:
#   "zero"  (AIPilot as of the roll decoupling, 2026-11-07, and the default): no roll stick while the lead steers; the
#           lead steers yaw and pitch alone. VesselTransformer.Roll turns the COMMAND about the NOSE's forward, and the
#           lead holds the command far from the nose by design, so ANY roll stick swings it round the nose: the
#           corkscrew of PRs #1829 (Manta), #1831 (Urchin), #1832 (Serpent).
#   "led"   (#1786 as first built; reproduces PR #1826's lead-on numbers): roll = the led yaw stick, pinned full.
#   "unled" (a what-if, measured and rejected): roll = the lead-off yaw stick (judged from the nose to the aim). A lagging
#           nose is off the aim for most of every turn, so it still whirls the command (Manta, Redline, 2.5 / 40: 39 of
#           48 fresh races finish, I2-I4 medians 620-835 s; "zero" finishes 48 of 48 at 130-455 s).
# A single-stick hull (SingleStickVesselTransformer: roll = -x, the yaw stick itself) cannot take a separate roll from
# its pilot, so no law applies there: its lead is the led stick, roll included, under every law.
ROLL_LAWS = ("zero", "led", "unled")
DEFAULT_ROLL_LAW = "zero"


def _norm(v):
    return math.sqrt(sum(c * c for c in v))


def _unit(v):
    m = _norm(v)
    return [c / m for c in v] if m > 1e-12 else list(v)


def _dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def _ang(a, b):
    """Degrees between two directions (0 for a degenerate vector)."""
    ma, mb = _norm(a), _norm(b)
    if ma < 1e-12 or mb < 1e-12:
        return 0.0
    return math.degrees(math.acos(max(-1.0, min(1.0, _dot(a, b) / (ma * mb)))))


class NoseLead:
    """The lead knob: gain 0 is OFF (today's steering); max_extra_deg caps how far past the aim the nose is asked."""

    __slots__ = ("gain", "max_extra_deg", "roll")

    def __init__(self, gain=0.0, max_extra_deg=DEFAULT_MAX_EXTRA_DEG, roll=DEFAULT_ROLL_LAW):
        self.gain = float(gain or 0.0)
        self.max_extra_deg = float(max_extra_deg if max_extra_deg is not None else DEFAULT_MAX_EXTRA_DEG)
        self.roll = DEFAULT_ROLL_LAW if roll is None else str(roll)
        if self.roll not in ROLL_LAWS:
            raise ValueError(f"nose_lead roll law {roll!r}: one of {ROLL_LAWS}")

    @property
    def off(self):
        return self.gain <= 0.0

    def effective(self, nose_rate=NOSE_FOLLOW):
        """The gain this hull steers with: AIPilot.EffectiveNoseLeadGain."""
        return effective_gain(self.gain, nose_rate)

    @classmethod
    def from_config(cls, cfg, key=CONFIG_KEY):
        """The knob a job's config carries, or OFF: cfg may be None, a NoseLead, a number (the gain), a dict with
        "gain" / "max_extra_deg", or a dict holding one of those under key."""
        if cfg is None:
            return cls()
        if isinstance(cfg, NoseLead):
            return cfg
        if isinstance(cfg, dict) and key in cfg:
            return cls.from_config(cfg[key], key)
        if isinstance(cfg, dict):
            if "gain" in cfg or "max_extra_deg" in cfg or "roll" in cfg:
                return cls(cfg.get("gain", 0.0), cfg.get("max_extra_deg", DEFAULT_MAX_EXTRA_DEG),
                           cfg.get("roll", DEFAULT_ROLL_LAW))
            return cls()
        if isinstance(cfg, bool):
            return cls(0.0)
        if isinstance(cfg, (int, float)):
            return cls(float(cfg))
        raise ValueError(f"nose_lead: not a gain, a dict or a NoseLead: {cfg!r}")

    def to_config(self):
        return {"gain": self.gain, "max_extra_deg": self.max_extra_deg, "roll": self.roll}

    @classmethod
    def from_cli(cls, text):
        """The `--nose-lead G[,EXTRA[,ROLL]]` flag every model's command line takes: the gain stated at the fleet lag,
        after a comma how far past the aim the command may be placed (degrees; DEFAULT_MAX_EXTRA_DEG unless given), and
        after a second comma the roll law (ROLL_LAWS; DEFAULT_ROLL_LAW unless given). None (the flag absent) stays
        None: off, today's steering."""
        if text is None:
            return None
        parts = str(text).split(",")
        extra = parts[1].strip() if len(parts) > 1 else ""
        roll = parts[2].strip() if len(parts) > 2 else ""
        return cls(float(parts[0]), float(extra) if extra else DEFAULT_MAX_EXTRA_DEG, roll or DEFAULT_ROLL_LAW)

    def __repr__(self):
        if self.off:
            return "NoseLead(off)"
        return f"NoseLead(gain={self.gain:g}, max_extra_deg={self.max_extra_deg:g}, roll={self.roll})"


OFF = NoseLead()


def effective_gain(gain, rate=NOSE_FOLLOW):
    """AIPilot.EffectiveNoseLeadGain: the gain is stated at the fleet lag, never under 1, 0 on a nose with no lag."""
    if gain is None or gain <= 0 or rate is None or rate <= 0 or math.isinf(rate) or math.isnan(rate) or math.isnan(gain):
        return 0.0
    return max(1.0, gain * NOSE_FOLLOW / rate)


def lead_share(lead_s, rate=NOSE_FOLLOW):
    """AIPilot.NoseLeadShare: the share of the nose-to-command gap a first-order nose closes in lead_s seconds."""
    return 0.0 if lead_s <= 0 or rate <= 0 else 1.0 - math.exp(-rate * lead_s)


def lead_turn_deg(err_deg, gain, extra_deg=DEFAULT_MAX_EXTRA_DEG):
    """The signed turn (degrees) the nose is asked for when its error to the aim is err_deg: gain times the error,
    at most extra_deg past the aim, never past MAX_TURN_DEG. The scalar form of slerp_beyond for heading models.
    With gain 0 it is the error itself (today's steering)."""
    if gain <= 0:
        return err_deg
    a = abs(err_deg)
    turn = min(gain * a, a + extra_deg, MAX_TURN_DEG)
    return math.copysign(turn, err_deg) if err_deg else 0.0


def slerp_beyond(f, d, t, extra_deg=DEFAULT_MAX_EXTRA_DEG):
    """f turned toward d by t times the angle between them (t > 1 carries on PAST d along the same great circle),
    never more than extra_deg past d nor past MAX_TURN_DEG from f, as AIPilot.NoseLeadDirection does. Works for any
    vector length (2D headings included); returns a unit vector."""
    f = _unit(f)
    a = math.radians(_ang(f, d))
    if a < 1e-9:
        return list(f)
    if math.sin(a) < 1e-6:                    # d is straight behind: no plane to turn in
        return _unit(d)
    turn = min(t * a, a + math.radians(extra_deg), math.radians(MAX_TURN_DEG))
    dd = _unit(d)
    k = _dot(f, dd)
    perp = _unit([x - k * y for x, y in zip(dd, f)])     # the unit vector in the f-d plane perpendicular to f, toward d
    c, s = math.cos(turn), math.sin(turn)
    return _unit([c * x + s * y for x, y in zip(f, perp)])


def judge_and_want(fwd, cmd, steer, gain_eff, extra_deg=DEFAULT_MAX_EXTRA_DEG):
    """What the pilot judges its error FROM and the direction it wants, this frame.

    Today (gain_eff 0): judged from the nose, wanting the steer direction: (fwd, steer).
    With the lead: judged from the COMMAND, wanting the nose turned toward the steer direction by gain_eff times its
    error (past it, capped): (cmd, slerp_beyond(fwd, steer, gain_eff, extra_deg)).
    The caller keeps its own stick law and its own command / nose update; only these two inputs change."""
    if gain_eff <= 0:
        return fwd, steer
    return cmd, slerp_beyond(fwd, _unit(steer), gain_eff, extra_deg)


def roll_stick(led_yaw, unled_yaw, law=DEFAULT_ROLL_LAW):
    """AIPilot's roll stick (YDiff) for a dual-stick hull with no belly target WHILE THE LEAD IS ON
    (NoseLeadSteering.RollStick): none ("zero", the game's law and the default), the led yaw stick ("led", #1786 as first
    built) or the yaw stick of the unled error ("unled", a rejected what-if). With the lead off the roll stick is the yaw
    stick under every law: a model asks this only while leading."""
    if law == "unled":
        return unled_yaw
    if law == "led":
        return led_yaw
    if law == "zero":
        return 0.0
    raise ValueError(f"roll law {law!r}: one of {ROLL_LAWS}")


def self_test():
    bad = []
    # the share a first-order nose closes
    if abs(lead_share(2 / 3) - (1 - math.exp(-1.0))) > 1e-9: bad.append("lead_share")
    if lead_share(0) != 0 or lead_share(1, 0) != 0: bad.append("lead_share at 0")
    # the effective gain: stated at the fleet lag, scaled by the hull's rate, never under 1, 0 with no lag
    if effective_gain(2.5) != 2.5: bad.append("effective_gain at the fleet lag")
    if abs(effective_gain(2.5, 9.0) - 1.0) > 1e-9: bad.append("effective_gain floors at 1 on a fast nose")
    if abs(effective_gain(2.5, 0.75) - 5.0) > 1e-9: bad.append("effective_gain doubles on a nose twice as slow")
    if effective_gain(0.0) != 0 or effective_gain(2.5, 0) != 0 or effective_gain(2.5, float("inf")) != 0 \
            or effective_gain(2.5, float("nan")) != 0 or effective_gain(float("nan"), 1.5) != 0:
        bad.append("effective_gain off cases (0, no lag, an infinite or NaN rate, a NaN gain: AIPilot returns 0)")
    # slerp_beyond: carries past the aim by the gain, capped at extra and at 170 deg
    f, d = [1.0, 0.0, 0.0], [math.cos(math.radians(10)), math.sin(math.radians(10)), 0.0]
    if abs(_ang(f, slerp_beyond(f, d, 2.5)) - 25.0) > 1e-6: bad.append("slerp_beyond 2.5 x 10 deg = 25 deg")
    if abs(_ang(f, slerp_beyond(f, d, 1.0)) - 10.0) > 1e-6: bad.append("slerp_beyond gain 1 lands on the aim")
    d60 = [math.cos(math.radians(60)), math.sin(math.radians(60)), 0.0]
    if abs(_ang(f, slerp_beyond(f, d60, 2.5, 40.0)) - 100.0) > 1e-6: bad.append("slerp_beyond capped at aim + 40")
    d150 = [math.cos(math.radians(150)), math.sin(math.radians(150)), 0.0]
    if abs(_ang(f, slerp_beyond(f, d150, 3.0, 80.0)) - 170.0) > 1e-6: bad.append("slerp_beyond capped at 170")
    w = slerp_beyond(f, d60, 2.5, 40.0)
    if w[1] <= 0 or abs(w[2]) > 1e-12: bad.append("slerp_beyond turns toward d in the f-d plane")
    if slerp_beyond(f, f, 2.5) != f: bad.append("slerp_beyond on the aim stays")
    if abs(_norm(slerp_beyond(f, d60, 2.5)) - 1.0) > 1e-9: bad.append("slerp_beyond returns a unit vector")
    f2, d2 = [1.0, 0.0], [math.cos(math.radians(10)), math.sin(math.radians(10))]
    if abs(_ang(f2, slerp_beyond(f2, d2, 2.0)) - 20.0) > 1e-6: bad.append("slerp_beyond in 2D")
    # the scalar form agrees with the vector form
    for err, g, ex in ((10, 2.5, 40), (60, 2.5, 40), (150, 3.0, 80), (-10, 2.0, 40), (0, 2.5, 40)):
        dd = [math.cos(math.radians(err)), math.sin(math.radians(err)), 0.0]
        want = _ang(f, slerp_beyond(f, dd, g, ex))
        if abs(abs(lead_turn_deg(err, g, ex)) - want) > 1e-6:
            bad.append(f"lead_turn_deg({err}, {g}, {ex}) = {lead_turn_deg(err, g, ex)} vs vector {want}")
    if lead_turn_deg(-10, 2.5) != -25.0 or lead_turn_deg(10, 0) != 10 or lead_turn_deg(0, 2.5) != 0.0:
        bad.append("lead_turn_deg sign / off / zero")
    # judge_and_want: off is today's (nose, steer); on judges from the command
    cmd = [0.0, 1.0, 0.0]
    if judge_and_want(f, cmd, d60, 0.0) != (f, d60): bad.append("judge_and_want off is (fwd, steer)")
    j, wv = judge_and_want(f, cmd, [c * 5 for c in d60], 2.5, 40.0)
    if j is not cmd or abs(_ang(f, wv) - 100.0) > 1e-6: bad.append("judge_and_want on judges from cmd, wants the lead")
    # the knob and its config forms
    for cfg, gain, extra in ((None, 0, 40), (2.5, 2.5, 40), ({"gain": 2.0, "max_extra_deg": 20}, 2.0, 20),
                             ({"nose_lead": 2.5}, 2.5, 40), ({"nose_lead": {"gain": 3}}, 3.0, 40), ({}, 0, 40),
                             ({"other": 1}, 0, 40), (NoseLead(1.5, 30), 1.5, 30), (False, 0, 40), (0, 0, 40)):
        n = NoseLead.from_config(cfg)
        if n.gain != gain or n.max_extra_deg != extra: bad.append(f"from_config({cfg!r}) -> {n!r}")
    if not NoseLead().off or NoseLead(2.5).off or OFF.effective(1.5) != 0 or NoseLead(2.5).effective(1.5) != 2.5:
        bad.append("NoseLead.off / effective")
    try:
        NoseLead.from_config("2.5"); bad.append("from_config accepted a string")
    except ValueError:
        pass
    if NoseLead.from_config(NoseLead(2.5, 40).to_config()).gain != 2.5: bad.append("to_config round trip")
    for text, gain, extra in (("2.5", 2.5, 40), ("2,20", 2.0, 20), ("3.0,", 3.0, 40), (" 1.5 , 60 ", 1.5, 60)):
        n = NoseLead.from_cli(text)
        if n.gain != gain or n.max_extra_deg != extra: bad.append(f"from_cli({text!r}) -> {n!r}")
    if NoseLead.from_cli(None) is not None: bad.append("from_cli(None) is not None (off)")
    # the roll law: the decoupled default flies no roll while leading; "led" is #1786's; the knob carries and parses it
    if NoseLead(2.5).roll != "zero" or NoseLead.from_config(2.5).roll != "zero": bad.append("default roll law is zero")
    if roll_stick(0.9, 0.2) != 0.0 or roll_stick(0.9, 0.2, "led") != 0.9 or roll_stick(0.9, 0.2, "unled") != 0.2:
        bad.append("roll_stick laws")
    if NoseLead.from_config({"gain": 2.5, "roll": "led"}).roll != "led": bad.append("from_config roll")
    if NoseLead.from_cli("2.5,40,zero").roll != "zero" or NoseLead.from_cli("2.5,,led").max_extra_deg != 40:
        bad.append("from_cli roll")
    if NoseLead.from_config(NoseLead(2.5, 20, "led").to_config()).roll != "led": bad.append("to_config roll round trip")
    try:
        NoseLead(2.5, 40, "sideways"); bad.append("NoseLead accepted an unknown roll law")
    except ValueError:
        pass
    # negative control: a model that forgot the law (roll = the led stick) is told apart from the decoupled default
    if roll_stick(0.9, 0.2) == 0.9 or roll_stick(-1.0, -1.0) != 0.0:
        bad.append("negative control: the default law returned a roll stick while leading")
    print("ai_nose_lead self-test: " + ("ok" if not bad else "FAILED " + repr(bad)))
    return bad


if __name__ == "__main__":
    if "--self-test" in sys.argv:
        sys.exit(1 if self_test() else 0)
    print(__doc__)
