using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A <b>crystal wormhole</b> (Docs/CRYSTAL_WORMHOLE.md): space crystals fold the HyperSea into an
    /// <b>attractor</b> and a <b>repulsor</b> joined by a wormhole — fly into the attractor and you come
    /// out of the repulsor. Nothing about it is a surface: each pole is a SMOOTH well (Plummer-softened
    /// gravity, a graded lens that never folds, softened tides — <see cref="BlackHole.IsSmooth"/>), each
    /// mouth is a seamless portal whose view dissolves into the world toward its edge, and both poles
    /// shrink every length a pilot observes (<see cref="WarpFieldRuntime"/>), so what a player sees is
    /// one smoothly graded warping of space.
    ///
    /// <para><b>Life.</b> One continuous animation of a single progress value <c>p</c> (0 = standing,
    /// 1 = gone), run backwards to FORM the pair out of nothing and forwards to ANNIHILATE it — the two
    /// poles attract each other like an electron and a hole: they spiral together while each one's
    /// amplitude beats against the other's (anti-phase, quickening), so the warp convolutes, and they
    /// meet as equal and opposite wells whose fields cancel — destructive interference into flat space.
    /// Every effect rides the poles' <see cref="BlackHole.Amplitude"/>: gravity, the felt pull, tides,
    /// the summed lens (where opposite wells cancel exactly) and each pole's warp depth; the mouths
    /// shrink with the envelope.</para>
    ///
    /// <para>The poles are <see cref="BlackHole"/>s — the gravity-well engine — but nothing here is a
    /// black hole: no horizon, no shadow, no ring.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrystalWormhole : MonoBehaviour
    {
        /// <summary>Every live crystal wormhole, oldest first (console, tests).</summary>
        public static readonly List<CrystalWormhole> Live = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Live.Clear();

        /// <summary>Everything a crystal wormhole is opened with, copied at <see cref="Open"/>.</summary>
        public struct Settings
        {
            /// <summary>Each pole's gravity (BlackHoleConfig: GM = strength × gmPerStrength).</summary>
            public float Strength;
            /// <summary>The throat: each mouth's radius, the wells' Plummer core and lens width, world units.</summary>
            public float ThroatRadius;
            /// <summary>Lens strength A (&lt; 1, so it never folds): the attractor magnifies by up to 1/(1−A).</summary>
            public float LensStrength;
            /// <summary>The felt pull on vessels: k, its ceiling (× cruise), its reach (throat radii).</summary>
            public float FeltStrength, FeltCap, FeltReach;
            /// <summary>Spin axis of the wells (they drag no frame; it orients nothing else today).</summary>
            public Vector3 SpinAxis;

            /// <summary>The seamless mouth material (WormholeSeamless.mat).</summary>
            public Material MouthMaterial;
            /// <summary>Whose vessels a mouth may carry.</summary>
            public IReadOnlyList<IPlayer> Players;
            public float MouthExactRange, MouthExactFadeBand, MouthExactRenderScale;
            public int MouthPanoramaFaceSize;

            /// <summary>Seconds to form out of nothing; seconds to annihilate back into it.</summary>
            public float FormSeconds, AnnihilateSeconds;
            /// <summary>Seconds the pair stands before it annihilates on its own; 0 = until its world retires.</summary>
            public float LifetimeSeconds;
            /// <summary>How many turns the poles spiral through on the way in, and how many amplitude beats.</summary>
            public float SpiralTurns, BeatCycles;
            /// <summary>How deep each beat swings the poles' amplitudes against each other (0..1).</summary>
            public float BeatDepth;
        }

        enum Phase { Forming, Standing, Annihilating, Gone }

        /// <summary>Annihilation when the world it stands in retires — kept inside the cell's suction.</summary>
        const float RetireAnnihilateSeconds = 0.9f;

        Settings _settings;
        BlackHole _attractor;
        BlackHole _repulsor;
        WormholeMouth _attractorMouth;
        WormholeMouth _repulsorMouth;
        Vector3 _midpoint;          // local
        Vector3 _halfSeparation;    // local: attractor = midpoint + half, repulsor = midpoint − half (at p = 0)
        Vector3 _spiralAxis;        // local: the axis the pair turns about as it spirals
        Phase _phase;
        float _phaseTime;
        float _phaseSeconds;
        float _standingTime;
        float _progress = 1f;
        Transform _adoptedBy;

        public BlackHole Attractor => _attractor;
        public BlackHole Repulsor => _repulsor;

        /// <summary>0 = standing at full strength and separation; 1 = annihilated (or not yet formed).</summary>
        public float Progress => _progress;

        public bool IsAnnihilating => _phase == Phase.Annihilating;
        public bool IsGone => _phase == Phase.Gone;

        /// <summary>
        /// Open a crystal wormhole on <paramref name="host"/> (its children move with it): the attractor at
        /// <paramref name="localAttractor"/>, the repulsor at <paramref name="localRepulsor"/>. The pair
        /// FORMS — grows out of nothing at the midpoint and spirals apart — over the settings' form time.
        /// Returns null (with the registry's warning) if the hole budget cannot take two more wells.
        /// </summary>
        public static CrystalWormhole Open(GameObject host, Vector3 localAttractor, Vector3 localRepulsor, Settings settings)
        {
            if (!host || !Application.isPlaying) return null;
            var wormhole = host.AddComponent<CrystalWormhole>();
            if (!wormhole.Build(localAttractor, localRepulsor, settings))
            {
                Destroy(wormhole);
                return null;
            }
            return wormhole;
        }

        bool Build(Vector3 localAttractor, Vector3 localRepulsor, Settings settings)
        {
            _settings = settings;
            _midpoint = (localAttractor + localRepulsor) * 0.5f;
            _halfSeparation = (localAttractor - localRepulsor) * 0.5f;
            var axisHint = Mathf.Abs(Vector3.Dot(_halfSeparation.normalized, Vector3.forward)) < 0.9f ? Vector3.forward : Vector3.right;
            _spiralAxis = Vector3.Cross(_halfSeparation, axisHint).normalized;

            float throat = Mathf.Max(1f, settings.ThroatRadius);
            _attractor = SpawnWell(localAttractor, HolePolarity.Sink, "[Attractor]", throat);
            if (!_attractor) return false;
            _repulsor = SpawnWell(localRepulsor, HolePolarity.Source, "[Repulsor]", throat);
            if (!_repulsor)
            {
                _attractor.BeginDespawn();
                return false;
            }
            _attractor.Throat = _repulsor;
            _repulsor.Throat = _attractor;

            if (settings.MouthMaterial)
            {
                _attractorMouth = BuildMouth(_attractor.transform, "Mouth", throat);
                _repulsorMouth = BuildMouth(_repulsor.transform, "Mouth", throat);
                WormholeMouth.Pair(_attractorMouth, _repulsorMouth);
            }
            else
            {
                CSDebug.LogWarning("[CrystalWormhole] No mouth material - the pair warps space but carries no one.");
            }
            _attractor.ThroatRadius = throat;
            _repulsor.ThroatRadius = throat;
            _attractor.ConfigureFeltVesselLaw(settings.FeltStrength, settings.FeltCap, settings.FeltReach);
            _repulsor.ConfigureFeltVesselLaw(settings.FeltStrength, settings.FeltCap, settings.FeltReach);

            // Both poles shape the cell's warp field, beating with their amplitudes.
            var attractor = _attractor;
            var repulsor = _repulsor;
            WarpFieldRuntime.AddPole(_attractor.transform, () => attractor ? attractor.Amplitude : 0f);
            WarpFieldRuntime.AddPole(_repulsor.transform, () => repulsor ? repulsor.Amplitude : 0f);

            BeginPhase(Phase.Forming, settings.FormSeconds);
            ApplyProgress(1f);   // nothing yet: both at the midpoint, amplitude zero
            if (!Live.Contains(this)) Live.Add(this);
            return true;
        }

        BlackHole SpawnWell(Vector3 localPosition, HolePolarity polarity, string wellName, float throat)
        {
            var world = transform.TransformPoint(localPosition);
            // Size = the transit core for prisms: a prism inside half the throat is carried through.
            var well = BlackHoleRegistry.Spawn(world, _settings.Strength, Vector3.zero, _settings.SpinAxis,
                throat * 0.5f, polarity);
            if (!well) return null;
            well.name = wellName;
            well.transform.SetParent(transform, true);
            well.ConfigureSmoothWell(throat, _settings.LensStrength);
            well.Amplitude = 0f;
            return well;
        }

        WormholeMouth BuildMouth(Transform pole, string mouthName, float radius)
        {
            var go = new GameObject(mouthName);
            go.transform.SetParent(pole, false);
            var mouth = go.AddComponent<WormholeMouth>();
            mouth.Build(new WormholeMouth.Settings
            {
                SurfaceMaterial = _settings.MouthMaterial,
                BloomSeconds = 0.05f,       // the envelope grows it; the bloom only has to not pop
                ExactRange = _settings.MouthExactRange,
                ExactFadeBand = _settings.MouthExactFadeBand,
                ExactRenderScale = _settings.MouthExactRenderScale,
                PanoramaFaceSize = _settings.MouthPanoramaFaceSize,
                RimTint = default,          // a seamless mouth draws no rim
                DomainTolled = false,       // a fold of the HyperSea belongs to nobody: every pilot rides
                Domain = CosmicShore.Data.Domains.Blue,
            }, _settings.Players, radius);
            go.transform.localScale = Vector3.zero;
            return mouth;
        }

        /// <summary>
        /// Annihilate the pair: the poles attract, spiral together and interfere into nothing over
        /// <paramref name="seconds"/> (negative = the settings' annihilation time). From wherever the pair
        /// is — a pair still forming turns round and collapses from there.
        /// </summary>
        public void Annihilate(float seconds = -1f)
        {
            if (_phase == Phase.Annihilating || _phase == Phase.Gone) return;
            BeginPhase(Phase.Annihilating, seconds >= 0f ? seconds : _settings.AnnihilateSeconds);
        }

        void BeginPhase(Phase phase, float seconds)
        {
            _phase = phase;
            _phaseSeconds = Mathf.Max(0f, seconds);
            // Start the new phase where the pair IS, so turning round mid-animation never jumps.
            _phaseTime = phase switch
            {
                Phase.Forming => (1f - _progress) * _phaseSeconds,
                Phase.Annihilating => _progress * _phaseSeconds,
                _ => 0f,
            };
            if (phase == Phase.Standing) _standingTime = 0f;
            CSDebug.LogVerbose(CSLogChannel.BlackHole, $"[CrystalWormhole] {phase} over {_phaseSeconds:F1}s.");
        }

        void Update()
        {
            switch (_phase)
            {
                case Phase.Forming:
                {
                    _phaseTime += Time.deltaTime;
                    float t = _phaseSeconds > 0f ? Mathf.Clamp01(_phaseTime / _phaseSeconds) : 1f;
                    ApplyProgress(1f - t);
                    if (t >= 1f) BeginPhase(Phase.Standing, 0f);
                    break;
                }
                case Phase.Standing:
                    _standingTime += Time.deltaTime;
                    if (_settings.LifetimeSeconds > 0f && _standingTime >= _settings.LifetimeSeconds) Annihilate();
                    break;
                case Phase.Annihilating:
                {
                    _phaseTime += Time.deltaTime;
                    float t = _phaseSeconds > 0f ? Mathf.Clamp01(_phaseTime / _phaseSeconds) : 1f;
                    ApplyProgress(t);
                    if (t >= 1f) Finish();
                    break;
                }
            }
        }

        /// <summary>
        /// Pose and amplitude at progress <paramref name="p"/> (0 standing, 1 gone) — pure, so the curve can
        /// be tested: separation <c>(1−p)^0.75</c>, spiral angle <c>2π·turns·p²</c>, envelope <c>(1−p)^1.5</c>,
        /// beat phase <c>2π·cycles·p²</c>; the attractor's amplitude is <c>env·(1 + depth·sin φ)</c> and the
        /// repulsor's <c>env·(1 − depth·sin φ)</c> — anti-phase, quickening, both zero at the end.
        /// </summary>
        public static void Curve(float p, float spiralTurns, float beatCycles, float beatDepth,
            out float separation, out float spiralAngle, out float envelope, out float attractorAmplitude,
            out float repulsorAmplitude)
        {
            p = Mathf.Clamp01(p);
            float q = 1f - p;
            separation = Mathf.Pow(q, 0.75f);
            spiralAngle = 2f * Mathf.PI * spiralTurns * p * p;
            envelope = q * Mathf.Sqrt(q);
            float beat = Mathf.Clamp01(beatDepth) * Mathf.Sin(2f * Mathf.PI * beatCycles * p * p);
            attractorAmplitude = envelope * (1f + beat);
            repulsorAmplitude = envelope * (1f - beat);
        }

        void ApplyProgress(float p)
        {
            _progress = Mathf.Clamp01(p);
            Curve(_progress, _settings.SpiralTurns, _settings.BeatCycles, _settings.BeatDepth,
                out float separation, out float angle, out float envelope, out float ampA, out float ampR);
            var half = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, _spiralAxis) * _halfSeparation * separation;
            if (_attractor)
            {
                _attractor.transform.localPosition = _midpoint + half;
                _attractor.Amplitude = ampA;
            }
            if (_repulsor)
            {
                _repulsor.transform.localPosition = _midpoint - half;
                _repulsor.Amplitude = ampR;
            }
            if (_attractorMouth) _attractorMouth.transform.localScale = Vector3.one * envelope;
            if (_repulsorMouth) _repulsorMouth.transform.localScale = Vector3.one * envelope;
        }

        void Finish()
        {
            _phase = Phase.Gone;
            // Space is already flat (every amplitude is 0); these only tidy up.
            if (_attractorMouth) _attractorMouth.Retire(0.05f);
            if (_repulsorMouth) _repulsorMouth.Retire(0.05f);
            if (_attractor && !_attractor.IsDespawning) _attractor.BeginDespawn();
            if (_repulsor && !_repulsor.IsDespawning) _repulsor.BeginDespawn();
            Live.Remove(this);
            CSDebug.LogVerbose(CSLogChannel.BlackHole, "[CrystalWormhole] annihilated.");
        }

        /// <summary>
        /// The host's FIRST parent is the cell that adopted it; a later re-parent is that cell retiring
        /// its world — the pair annihilates inside the suction.
        /// </summary>
        void OnTransformParentChanged()
        {
            var parent = transform.parent;
            if (!parent) return;
            if (!_adoptedBy)
            {
                _adoptedBy = parent;
                return;
            }
            if (parent != _adoptedBy) Annihilate(RetireAnnihilateSeconds);
        }

        void OnDestroy() => Live.Remove(this);
    }
}
