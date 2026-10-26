using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    // Part of AstroLeagueBall (Docs/REFACTOR_BACKLOG.md M13): this region moved here verbatim from AstroLeagueBall.cs.
    public partial class AstroLeagueBall
    {
        #region Per-frame visuals (every peer)

        void Update()
        {
            if (settings == null || ballRenderer == null) return;

            // Non-server peers free-spin the kinematic replica from the replicated angular velocity
            // (the server rigidbody owns the real rotation, interpolated natively). Purely cosmetic -
            // no gameplay reads client rotation, so any dead-reckoned drift is invisible.
            if (IsSpawned && !IsServer && !n_Frozen.Value && !n_Hidden.Value)
            {
                Vector3 w = n_AngularVelocity.Value;
                float wMag = w.magnitude;
                if (wMag > 1e-4f)
                    transform.rotation = Quaternion.AngleAxis(wMag * Mathf.Rad2Deg * Time.deltaTime, w / wMag) * transform.rotation;
            }

            // Impact pop: a fast swell that eases back over strikePopSeconds. Visual child only -
            // the root's scale is the ball's physical size and must not move (see _visual).
            if (_visual != null)
            {
                float pop = 1f;
                if (_popTimer > 0f)
                {
                    _popTimer -= Time.deltaTime;
                    float t = Mathf.Clamp01(_popTimer / Mathf.Max(0.0001f, settings.strikePopSeconds));
                    pop = 1f + settings.strikePopAmount * t * t;
                }

                // Birth bloom (continuity of existence): every peer's fresh instance GROWS in
                // over spawnBloomSeconds instead of popping into the court at full size. Runs
                // once per instance from Awake; composes with the pop as a multiplier.
                float bloom = 1f;
                if (_bloomTimer > 0f)
                {
                    _bloomTimer -= Time.deltaTime;
                    float u = 1f - Mathf.Clamp01(_bloomTimer / Mathf.Max(0.0001f, settings.spawnBloomSeconds));
                    bloom = u * u * (3f - 2f * u); // smoothstep 0→1, settles without a snap
                }

                Vector3 targetScale = Vector3.one * (pop * bloom);
                if (_visual.localScale != targetScale)
                    _visual.localScale = targetScale;
            }

            float speedRatio = Mathf.Clamp01(Velocity.magnitude / settings.speedForMaxVisuals);

            // Impact flash decay
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                float flashRatio = Mathf.Clamp01(flashTimer / settings.impactFlashDuration);
                currentEmissionBoost = Mathf.Lerp(1f, settings.impactFlashIntensity, flashRatio);
            }
            else
            {
                currentEmissionBoost = 1f;
            }

            // Color keys to the LAST-HIT domain (requirement 1): a Jade striker tints the ball Jade,
            // a Ruby striker Ruby. Before any strike the payload is neutral (Blue) and runs the
            // original three-way rainbow cycle so it reads as "unclaimed".
            Color emissionColor;
            Domains dom = n_LastHitDomain.Value;
            if (dom == Domains.Blue)
            {
                float phase = (Time.time * colorCycleSpeed) % 3f;
                emissionColor = phase < 1f
                    ? Color.Lerp(primaryColor, secondaryColor, phase)
                    : phase < 2f
                        ? Color.Lerp(secondaryColor, tertiaryColor, phase - 1f)
                        : Color.Lerp(tertiaryColor, primaryColor, phase - 2f);
            }
            else
            {
                emissionColor = DomainTint(dom);
            }

            float breath = 0.8f + 0.2f * Mathf.Sin(Time.time * 4f);
            float emissionIntensity = Mathf.Lerp(settings.minEmissionIntensity, settings.maxEmissionIntensity, speedRatio);
            float glow = emissionIntensity * breath * currentEmissionBoost;

            if (_usesFresnel)
            {
                // Drive the prism fresnel rim: bright HDR rim cycles + reacts to speed/flash,
                // dark base is a dim version of the same hue so the sphere reads as 3D. Cap the
                // flash boost so a thin rim doesn't blow out to pure white (full-surface emission
                // tolerates much higher HDR than a grazing-angle rim).
                float rim = emissionIntensity * breath * Mathf.Min(currentEmissionBoost, 4f);
                mpb.SetColor(BrightColorId, emissionColor * rim);
                mpb.SetColor(DarkColorId, emissionColor * 0.06f);
            }
            else
            {
                mpb.SetColor(EmissionColorId, emissionColor * glow);
            }
            ballRenderer.SetPropertyBlock(mpb);

            if (ballLight != null)
            {
                ballLight.color = emissionColor;
                float boost = currentEmissionBoost > 1f ? Mathf.Sqrt(currentEmissionBoost) : 1f;
                ballLight.intensity = baseLightIntensity * (1f + speedRatio * 2f) * breath * boost;
                ballLight.range = Mathf.Lerp(settings.minLightRange, settings.maxLightRange, speedRatio);
            }

            if (trail != null)
            {
                // The wake blooms in with the ball and swells with the strike pop (visual scale).
                float visualScale = _visual != null ? _visual.localScale.x : 1f;
                trail.startWidth = TrailHeadWidth(speedRatio) * visualScale;
                trail.time = Mathf.Lerp(settings.trailTimeAtRest, settings.trailTimeAtSpeed, speedRatio);
                if (_cometTrail) DriveCometTrail(emissionColor, speedRatio);
            }

            if (wakeDust != null)
            {
                float worldRadius = BallWorldRadius();
                var main = wakeDust.main;
                // Authored against the base 7-unit ball; re-stated whole (never via *Multiplier,
                // which does not scale both ends of a TwoConstants curve).
                float k = worldRadius / 7f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.8f * k, 2.4f * k);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f * k, 4f * k);
                main.startColor = emissionColor.maxColorComponent > 1e-4f
                    ? emissionColor / emissionColor.maxColorComponent
                    : Color.white;
                var shape = wakeDust.shape;
                shape.radius = worldRadius;
                var emission = wakeDust.emission;
                bool draw = ballRenderer.enabled;
                // A TELEPORT (kickoff, goal reset, hand-off) is not travel: shedding by distance
                // across it would lay a line of dust over the whole court. Skip the jump frame.
                Vector3 here = transform.position;
                float jump = Mathf.Max(settings.maxSpeed * Time.deltaTime * 3f, worldRadius * 4f);
                bool teleported = (here - _lastDustPos).sqrMagnitude > jump * jump;
                _lastDustPos = here;
                float shed = Mathf.InverseLerp(0.15f, 1f, speedRatio);
                emission.rateOverDistanceMultiplier = draw && !teleported
                    ? settings.wakeDustPerUnit * shed * shed / Mathf.Max(0.01f, k)
                    : 0f;
            }

            if (auraParticles != null)
            {
                var emission = auraParticles.emission;
                emission.rateOverTime = n_Hidden.Value ? 0f : Mathf.Lerp(8f, 45f, speedRatio);
            }
        }

        /// <summary>Base hue for a claimed (non-neutral) ball - matches the arena's per-domain palette
        /// (all three domains config-driven from <see cref="AstroLeagueSettingsSO"/>).</summary>
        Color DomainTint(Domains d)
        {
            switch (d)
            {
                case Domains.Jade: return settings.jadeGoalColor;
                case Domains.Ruby: return settings.rubyGoalColor;
                case Domains.Gold: return settings.goldGoalColor;
                default: return primaryColor;
            }
        }

        #endregion
    }
}
