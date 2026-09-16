#ifndef FAUNA_SKIN_INCLUDED
#define FAUNA_SKIN_INCLUDED

// Fauna skin — the two things a CREATURE's spindle does that a plant's does not.
//
// Both halves came off CreatureTextureGraph, the one-off shader the Clawfish wore
// and nothing else did (Docs/ECOSYSTEM.md §46). That graph was mostly dead nodes —
// an unconnected colour pair, an unconnected gradient, an unconnected wave subgraph —
// but the three things it DID render were the reason the creature read as alive
// rather than as a prop: a fresnel rim, a slowly scrolling surface pattern, and a
// slow brightness breath. All three are expressible over the spindle pipeline's own
// clock, so they cost what the Clawfish's bespoke shader cost: nothing per frame.
//
// THE CLOCK IS `_PrismClock`, NOT `_Time`. Docs/PRISM_ANIMATION.md's clock-material
// law: everything a prism animates on rides the platform clock, so a pause, a
// time-scale change or a replay moves the whole world together. The Clawfish's
// original used Unity's own `Sine Time` and was therefore the one thing in the cell
// that kept breathing while the game was paused.
//
// EVERY DIAL DEFAULTS TO A PROVABLE NO-OP. `RimStrength = 0` deletes the rim term
// outright; `Pulse = (1, 1)` makes the breath's lerp return 1 whatever the phase;
// `FlowSpeed = (0, 0)` leaves the UV offset exactly where it was. So
// FaunaSpindleGraph with no material authoring it is bit-identical to SpindleGraph,
// which is what makes the splice reviewable — the same argument `_SwayAmplitude`
// makes in SpindleSway.hlsl, and for the same reason: the blast radius is an
// authored list of materials rather than a side effect.

// Radians per second of the brightness breath. A platform constant rather than a
// dial because it is the one number a species has no reason to disagree about: this
// is "a creature is breathing", not "a creature is agitated" (that is the sway's
// Frequency, which IS per-material). ~4.4 s per breath, near the 2*pi-second period
// the Clawfish's `Sine Time` gave it.
#define FAUNA_SKIN_BREATH_RATE 1.42

// The scrolling pattern and the breath must not phase-lock, or a creature pulses in
// lockstep with its own skin and the two read as one effect. The flow input is a
// distance per second, so nothing here needs to know the ratio — this is only the
// reminder that FlowSpeed should not be authored at a rational multiple of the rate
// above.

// Slowly walks the cellular pattern across the body. The spindle's Voronoi UV already
// runs through a TilingAndOffset; this only adds to that node's authored offset, so a
// FlowSpeed of zero leaves the shipped framing untouched.
void FaunaSkinFlow_float(
    float2 BaseOffset,   // the node's authored offset — passed through when FlowSpeed is 0
    float  Clock,        // _PrismClock (PrismClock.Now)
    float2 FlowSpeed,    // UV units per second; (0,0) = no flow
    out float2 Out)
{
    Out = BaseOffset + Clock * FlowSpeed;
}

// Breath + rim, in that order, over the colour the Voronoi mix already produced.
//
// The rim is ADDITIVE and never a multiply, which is the one decision here worth
// defending. CreatureTextureGraph multiplied its fresnel INTO the base colour, which
// works on a transparent shell — the Clawfish is a hollow horn you see straight
// through — and would be wrong on a spindle: a multiply darkens the whole interior to
// black and leaves a creature readable only at grazing angles, on meshes that are
// already alpha-clipped into lace. Adding can only ever brighten, so it cannot make a
// creature harder to see, it needs no sort order, and it leaves ALPHA alone — the
// silhouette stays exactly the shape the Voronoi cut.
void FaunaSkinShade_float(
    float3 Base,         // the Dull<->Bright Voronoi mix
    float3 Rim,          // the bright neutral; the rim wears the palette's own colour
    float  Fresnel,      // 1 at grazing incidence, 0 face-on
    float  Clock,        // _PrismClock
    float  RimStrength,  // 0 = no rim
    float2 Pulse,        // (min, max) brightness; (1,1) = no breath
    out float3 Out)
{
    float breath = lerp(Pulse.x, Pulse.y, 0.5 * (sin(Clock * FAUNA_SKIN_BREATH_RATE) + 1.0));
    Out = Base * breath + Rim * (Fresnel * RimStrength);
}

#endif
