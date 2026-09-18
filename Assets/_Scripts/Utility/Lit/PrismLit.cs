using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// <b>LIT</b> — the platform's answer to "whose force is reaching that mass?".
    ///
    /// A producer publishes a <see cref="LitVolume"/> and a domain colour; every prism standing
    /// inside one is drawn lit in that colour. That is the whole of the fundamental. It is a
    /// STATEMENT, never a query: nothing here walks prisms, tests prisms, tracks prisms or stores
    /// anything on a prism. The containment test runs on the GPU in
    /// <c>PrismDestructionSight.hlsl</c>, once per prism, off a handful of shader globals this
    /// class writes once per frame.
    ///
    /// <para><b>Producers.</b> Four today, each saying the same sentence at a different moment in
    /// a force's life — pending, armed, resolving, resolved:</para>
    /// <list type="bullet">
    ///   <item><b>Echo Sight</b> (Dolphin, Charge) — the volume its next crystal blast WOULD
    ///   sweep. The pilot's own is the <see cref="PublishAimed"/> channel; rivals' ride the bank.</item>
    ///   <item><b>Proximity fuze</b> (Sparrow skyburst) — the sphere an armed warhead will
    ///   detonate inside.</item>
    ///   <item><b>Cavitation plate</b> (Scarab juke dash) — the swept, optionally mirrored
    ///   cylinder the dash is claiming right now.</item>
    ///   <item><b>Explosion passthrough</b> — a blast that ARRIVED and spared what it touched.
    ///   This one REPLACED the 2-second temporary shield that used to stand in for it; see
    ///   <c>Docs/LIT.md</c> for why that swap also removed three gameplay side effects nobody had
    ///   designed (a food-web blackout, a targeting-grid churn, and one shield SFX per prism).</item>
    /// </list>
    ///
    /// <para><b>Why a global uniform and not a query.</b> "Is this prism inside that volume" is
    /// LIVE data: the answer changes every frame for every prism as a ship turns and its meters
    /// fill. So it can never be a per-prism stamp — and the clock-material law's escape hatch for
    /// exactly this case (<c>Docs/PRISM_ANIMATION.md</c> §1 "animation vs. live gameplay data";
    /// §4.7, the ONE sanctioned shape for a view-dependent prism visual) is a global uniform: an
    /// O(1) write per frame that every prism reads. This is the sibling of
    /// <see cref="PrismOcclusionCorridor"/> and earns its per-frame write the same way. The cost
    /// is O(1) per frame in TOTAL — not per light, and certainly not per prism. Running
    /// <c>PrismSpatialIndex</c>'s sweeps every frame just to tint would be the per-prism CPU pass
    /// the law exists to prevent.</para>
    ///
    /// <para><b>Two channels — mine, and everything else.</b> Every viewer has at most ONE volume
    /// they are personally aiming with, which is why <see cref="PublishAimed"/> stays a plain
    /// uniform set and why its code path is untouched by this generalisation: a prism your own
    /// aim covers is painted exactly as it was before the bank existed (proven bit-identical over
    /// the shipped shader by <c>Tools/Shaders/verify_prism_sight_composition.py</c>). Everything
    /// else rides <see cref="Slots"/> array slots, each carrying its owner's DOMAIN colour, so a
    /// lit patch of mass says WHO is acting on it. Your own aim always wins on any prism it
    /// covers — the instrument you are aiming with is never recoloured by a rival sweeping past.
    /// </para>
    ///
    /// <para><b>Continuity of existence is structural here, not per-producer.</b> A slot that
    /// stops being reported FADES rather than dropping (see <see cref="Flush"/>), so no light can
    /// pop out of existence and no producer has to remember to fade its own. That matters most
    /// for the shortest-lived producer: an explosion is <c>Destroy</c>ed the frame its sweep ends
    /// and cannot fade anything itself.</para>
    ///
    /// <para><b>Nothing reads this to decide an outcome, deliberately.</b> See
    /// <see cref="LitVolume"/> for the replication constraint any future combo has to respect.</para>
    ///
    /// Unlike the occlusion corridor and the speed tunnel this is NOT a platform law: no vessel is
    /// obliged to light anything. It is a fundamental in the other sense — one vocabulary that
    /// several systems evoke instead of each inventing its own highlight.
    /// </summary>
    public static class PrismLit
    {
        /// <summary>
        /// How many lights other than the viewer's own aim can be shown at once. Mirrors
        /// <c>PRISM_LIT_SLOTS</c> in <c>PrismDestructionSight.hlsl</c> — change both together,
        /// since the shader's arrays are declared at this length.
        ///
        /// Eight covers the widest roster the game ships (the ARENA cards seat up to 8 hulls) with
        /// one light each, and the realistic case is far smaller because arcade modes lock to one
        /// hull. <see cref="Flush"/> keeps the STRONGEST lights if it ever overflows, rather than
        /// whichever the dictionary happened to enumerate last — an arbitrary drop would be an
        /// invisible, machine-dependent difference in what each player sees.
        /// </summary>
        public const int Slots = 8;

        /// <summary>
        /// How long an abandoned light takes to fade out, when its producer does not name its own.
        /// Short: this is a continuity guarantee, not a lingering afterimage.
        /// </summary>
        public const float DefaultAfterglowSeconds = 0.35f;

        // --- the viewer's own aim (one per viewer, so plain uniforms) ---
        static readonly int ApexId = Shader.PropertyToID("_PrismSightApex");
        static readonly int AxisId = Shader.PropertyToID("_PrismSightAxis");
        static readonly int GapeId = Shader.PropertyToID("_PrismSightGape");
        static readonly int ParamsId = Shader.PropertyToID("_PrismSightParams");
        static readonly int StrengthId = Shader.PropertyToID("_PrismSightStrength");

        // --- every other light (a fixed bank of array slots) ---
        static readonly int BankOriginId = Shader.PropertyToID("_PrismSightPeerApex");
        static readonly int BankAxisId = Shader.PropertyToID("_PrismSightPeerAxis");
        static readonly int BankGapeId = Shader.PropertyToID("_PrismSightPeerGape");
        static readonly int BankTintId = Shader.PropertyToID("_PrismSightPeerTint");
        static readonly int BankShapeId = Shader.PropertyToID("_PrismSightPeerShape");
        static readonly int BankCountId = Shader.PropertyToID("_PrismSightPeerCount");

        static bool _publishedAimed;

        /// <summary>
        /// One light in the bank, as last reported.
        ///
        /// <see cref="Frame"/> is what makes the bank self-cleaning: a producer that stops
        /// reporting — because its vessel was destroyed, its explosion ended, its scene unloaded
        /// or its owner disconnected — has its slot faded out and dropped with nothing needing to
        /// have called <see cref="ClearLight"/>. A light that outlives the thing casting it is the
        /// one failure mode a registry like this actually has.
        /// </summary>
        struct Light
        {
            public LitVolume Volume;
            public float Strength;      // as reported, before any fade
            public Color Tint;
            public float Afterglow;     // seconds to fade over once abandoned
            public float Fade;          // 1 while reported, decaying once not
            public int Frame;
        }

        static readonly Dictionary<int, Light> _lights = new();
        static readonly List<int> _scratch = new();

        // Always sent at full length: Unity binds an array global at the length of its first
        // write, so a short write later would silently leave the tail of the previous frame's
        // bank live. Unused slots are zeroed and _PrismSightPeerCount is the real bound.
        static readonly Vector4[] _bankOrigin = new Vector4[Slots];
        static readonly Vector4[] _bankAxis = new Vector4[Slots];
        static readonly Vector4[] _bankGape = new Vector4[Slots];
        static readonly Vector4[] _bankTint = new Vector4[Slots];
        static readonly Vector4[] _bankShape = new Vector4[Slots];
        static int _publishedCount;

        /// <summary>
        /// The palette a light's DOMAIN tint is read from. Handed over by
        /// <c>ThemeManager.Awake</c>, exactly as it already hands the same asset to
        /// <c>GameToastAPI.ColorSet</c> — a static that needs one asset and cannot be injected is
        /// an existing, sanctioned shape here rather than a new mechanism.
        ///
        /// Null until then, which is a real state and not a failure: a light published before the
        /// theme is up falls back to white for those frames. <c>ThemeManager</c> is a Bootstrap DI
        /// singleton and wakes long before any vessel can fire, so in practice the only reader
        /// that ever sees null is a tool scene with no theme in it.
        /// </summary>
        public static SO_ColorSet ColorSet { get; set; }

        /// <summary>
        /// The colour a light belonging to <paramref name="domain"/> is drawn in: that domain's
        /// signal colour, the same accessor the HUD and the toast feed read
        /// (<see cref="SO_ColorSet.GetDomainSignalColor"/>).
        ///
        /// This lives here rather than in each producer so that "a light wears its owner's
        /// domain" is structural — a producer cannot resolve the colour a different way, and
        /// there is one place that reads the palette. <c>Domains.Blue</c> is the platform's "no
        /// team" sentinel and answers white, which is a correct-looking neutral mark rather than
        /// a wrong team's colour.
        /// </summary>
        public static Color DomainTint(Domains domain)
            => ColorSet != null ? ColorSet.GetDomainSignalColor(domain) : Color.white;

        /// <summary>True while any light — the viewer's own aim or another — is publishing.</summary>
        public static bool IsActive => _publishedAimed || _publishedCount > 0;

        // ---------------- The viewer's own aim ----------------

        /// <summary>
        /// Publish the volume the LOCAL pilot is personally aiming with. <paramref name="strength01"/>
        /// fades the highlight in and out so it never pops on.
        ///
        /// Written straight through rather than through <see cref="Flush"/> because there is
        /// exactly one of these per machine and therefore no bank to arbitrate: this is the same
        /// single-writer path, and the same uniforms, the Echo Sight has always used. It also
        /// keeps that arm of the shader bit-identical, which is the whole reason it stayed
        /// separate when the bank was generalised.
        ///
        /// Called every frame by the engaged producer; call <see cref="ClearAimed"/> on release.
        /// </summary>
        public static void PublishAimed(in LitVolume volume, float strength01)
        {
            strength01 = Mathf.Clamp01(strength01);
            if (!volume.IsValid || volume.Reach <= 0f || strength01 <= 0.001f)
            {
                ClearAimed();
                return;
            }

            // Three direction/point vectors plus one params vector. The scalars ride their own
            // vector rather than the others' w channels because the prism graphs carry Vector3
            // property donors and no Vector4 one — synthesising a property type neither graph
            // contains is exactly the hand-authored schema the asset-surgery protocol forbids.
            // (The bank below has no such constraint: its arrays are declared in the HLSL itself,
            // which is why they can pack four floats to a slot and carry a shape tag.)
            //   Params = (reach, param1, param2)
            //   reach <= 0 is the shader's "aim off" sentinel.
            //
            // The aimed channel is CONE-ONLY and that is not an oversight: it is one pilot's
            // aiming instrument, the only producer of it is the Echo Sight, and giving it a shape
            // tag would mean a fourth Vector3 property on every prism graph for a shape no
            // producer needs. A future non-cone instrument publishes into the bank instead.
            Shader.SetGlobalVector(ApexId, volume.Origin);
            Shader.SetGlobalVector(AxisId, volume.Axis);
            Shader.SetGlobalVector(GapeId, volume.GapeAxis);
            Shader.SetGlobalVector(ParamsId, volume.Params);

            // Its own scalar rather than Params' spare slot: a fade sharing a vector with the
            // volume's geometry reads fine today and gets misinterpreted six months from now.
            Shader.SetGlobalFloat(StrengthId, strength01);

            _publishedAimed = true;
        }

        /// <summary>Turn the viewer's own aim off. Idempotent — safe to call every frame while disengaged.</summary>
        public static void ClearAimed()
        {
            if (!_publishedAimed) return;
            PublishAimedOff();
        }

        // ---------------- Every other light ----------------

        /// <summary>
        /// Report a light. <paramref name="sourceId"/> identifies the producer (its component's
        /// instance id, or an explosion's) so one producer can only ever occupy one slot across a
        /// swap or a re-initialise.
        ///
        /// <paramref name="tint"/> is the owner's domain signal colour, read live rather than
        /// snapshotted — a domain change mid-flight re-colours their light. The shader pulls it
        /// toward white before adding it, so it reads as coloured light rather than as the prism
        /// having changed team.
        ///
        /// Call it every frame the light is up. A slot that stops being reported fades over
        /// <paramref name="afterglowSeconds"/> and is then dropped, so a producer that simply
        /// stops — or is destroyed — still leaves continuously.
        /// </summary>
        public static void PublishLight(int sourceId, in LitVolume volume, float strength01,
            Color tint, float afterglowSeconds = DefaultAfterglowSeconds)
        {
            strength01 = Mathf.Clamp01(strength01);
            if (!volume.IsValid || volume.Reach <= 0f || strength01 <= 0.001f)
            {
                ClearLight(sourceId);
                return;
            }

            _lights[sourceId] = new Light
            {
                Volume = volume,
                Strength = strength01,
                Tint = tint,
                Afterglow = Mathf.Max(0f, afterglowSeconds),
                Fade = 1f,
                Frame = Time.frameCount,
            };
        }

        /// <summary>
        /// Report a light belonging to <paramref name="domain"/>, resolving its tint through
        /// <see cref="DomainTint"/>. The overload every producer should reach for: it is one
        /// fewer thing to get wrong, and it is the only form available to a producer that cannot
        /// reach the palette itself (a pooled explosion injects nothing — see
        /// <see cref="ColorSet"/>).
        /// </summary>
        public static void PublishLight(int sourceId, in LitVolume volume, float strength01,
            Domains domain, float afterglowSeconds = DefaultAfterglowSeconds)
            => PublishLight(sourceId, volume, strength01, DomainTint(domain), afterglowSeconds);

        /// <summary>
        /// Stop reporting a light, beginning its fade. Idempotent, and not strictly required —
        /// the frame stamp in <see cref="Light"/> collects an abandoned slot anyway — but calling
        /// it on release starts the fade on this frame instead of the next one.
        ///
        /// It deliberately does NOT drop the slot: continuity of existence applies to a light as
        /// much as to mass, so there is no API here that can make one vanish. Only a scene
        /// teardown (<see cref="Driver.OnDisable"/>, <see cref="ResetOnLoad"/>) clears outright,
        /// because there the world it was drawn on is going away with it.
        /// </summary>
        public static void ClearLight(int sourceId)
        {
            if (!_lights.TryGetValue(sourceId, out var light)) return;
            if (light.Frame == int.MinValue) return; // already fading

            light.Frame = int.MinValue;
            _lights[sourceId] = light;
        }

        /// <summary>
        /// Pack this frame's lights into the shader's bank. Called once per frame from
        /// <see cref="Driver"/> in LateUpdate — after every producer's Update has reported, after
        /// the things those volumes hang off have moved, and before anything renders.
        ///
        /// The whole cost of showing every light in the game is this method: five array writes and
        /// a float, independent of how many producers are lighting and completely independent of
        /// how many prisms are on screen.
        /// </summary>
        public static void Flush()
        {
            int frame = Time.frameCount;
            float dt = Time.unscaledDeltaTime;

            // Snapshot the KEYS first and do every read/write by key lookup afterwards. The
            // dictionary cannot be mutated while it is being walked — and that includes an
            // indexer SET on an existing key, which bumps the enumerator's version on Mono even
            // though it changes no slot. Advancing a fade is a write to every abandoned entry, so
            // this loop would otherwise throw the moment two lights retired together.
            _scratch.Clear();
            foreach (var key in _lights.Keys)
                _scratch.Add(key);

            // Advance the fade on every light nobody reported this frame, and drop the ones that
            // have finished. unscaledDeltaTime so a light still retires while the game is paused
            // or slowed — a frozen mark around a ship that is gone is exactly the stale-light
            // failure the frame stamp exists to prevent.
            for (int i = 0; i < _scratch.Count; i++)
            {
                int key = _scratch[i];
                var light = _lights[key];
                if (light.Frame == frame) continue;

                light.Fade = light.Afterglow > 0f
                    ? light.Fade - dt / light.Afterglow
                    : 0f;

                if (light.Fade <= 0f)
                {
                    _lights.Remove(key);
                    continue;
                }

                // Mark it as fading so a later ClearLight is a no-op and cannot restart the fade.
                light.Frame = int.MinValue;
                _lights[key] = light;
            }

            int count = 0;
            foreach (var kv in _lights)
            {
                var light = kv.Value;
                if (count < Slots)
                {
                    Write(count++, light);
                    continue;
                }

                // Evict the weakest: the faintest light is the one whose absence is least
                // noticeable, and picking by strength makes the choice the same on every machine.
                int weakest = 0;
                for (int i = 1; i < Slots; i++)
                    if (_bankTint[i].w < _bankTint[weakest].w)
                        weakest = i;
                if (light.Strength * light.Fade > _bankTint[weakest].w)
                    Write(weakest, light);
            }

            for (int i = count; i < Slots; i++)
                _bankOrigin[i] = _bankAxis[i] = _bankGape[i] = _bankTint[i] = _bankShape[i] = Vector4.zero;

            // Nothing to say and nothing said last frame: skip the writes entirely, so a match
            // with no producer in it costs this system literally nothing per frame.
            if (count == 0 && _publishedCount == 0) return;

            Shader.SetGlobalVectorArray(BankOriginId, _bankOrigin);
            Shader.SetGlobalVectorArray(BankAxisId, _bankAxis);
            Shader.SetGlobalVectorArray(BankGapeId, _bankGape);
            Shader.SetGlobalVectorArray(BankTintId, _bankTint);
            Shader.SetGlobalVectorArray(BankShapeId, _bankShape);
            Shader.SetGlobalFloat(BankCountId, count);
            _publishedCount = count;
        }

        static void Write(int slot, in Light light)
        {
            var v = light.Volume;
            _bankOrigin[slot] = new Vector4(v.Origin.x, v.Origin.y, v.Origin.z, v.Params.x);
            _bankAxis[slot] = new Vector4(v.Axis.x, v.Axis.y, v.Axis.z, v.Params.y);
            _bankGape[slot] = new Vector4(v.GapeAxis.x, v.GapeAxis.y, v.GapeAxis.z, v.Params.z);
            _bankTint[slot] = new Vector4(light.Tint.r, light.Tint.g, light.Tint.b,
                                          light.Strength * light.Fade);
            // Only .x is read. The spare channels are left zero rather than packed with anything
            // else, so the next shape parameter has somewhere to go that cannot be confused with
            // a geometry field.
            _bankShape[slot] = new Vector4((int)v.Shape, 0f, 0f, 0f);
        }

        // ---------------- Lifecycle ----------------

        static void PublishAimedOff()
        {
            // Everything is zeroed so nothing stale survives into a later frame; Params.x <= 0 is
            // the sentinel the shader actually branches on.
            Shader.SetGlobalVector(ApexId, Vector4.zero);
            Shader.SetGlobalVector(AxisId, Vector4.zero);
            Shader.SetGlobalVector(GapeId, Vector4.zero);
            Shader.SetGlobalVector(ParamsId, Vector4.zero); // x <= 0 is the shader's "off" sentinel
            Shader.SetGlobalFloat(StrengthId, 0f);
            _publishedAimed = false;
        }

        /// <summary>
        /// Shader globals survive play-mode exit in the editor, so a light left up when play
        /// stopped would otherwise keep marking mass around a thing that no longer exists.
        /// Publish the off state before anything renders — the same guard the occlusion corridor
        /// installs — and install the driver that flushes the bank.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            _publishedAimed = true; // force PublishAimedOff to actually write
            PublishAimedOff();

            _lights.Clear();
            _scratch.Clear();
            for (int i = 0; i < Slots; i++)
                _bankOrigin[i] = _bankAxis[i] = _bankGape[i] = _bankTint[i] = _bankShape[i] = Vector4.zero;
            Shader.SetGlobalVectorArray(BankOriginId, _bankOrigin);
            Shader.SetGlobalVectorArray(BankAxisId, _bankAxis);
            Shader.SetGlobalVectorArray(BankGapeId, _bankGape);
            Shader.SetGlobalVectorArray(BankTintId, _bankTint);
            Shader.SetGlobalVectorArray(BankShapeId, _bankShape);
            Shader.SetGlobalFloat(BankCountId, 0f);
            _publishedCount = 0;

            // HideInHierarchy (NOT HideAndDontSave — that exempts the object from play-mode-exit
            // cleanup), the same pattern VesselSpeedTunnel's and the occlusion corridor's
            // publishers use.
            var go = new GameObject("[PrismLit]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        }

        /// <summary>
        /// LateUpdate so the bank is packed after every producer's Update has reported this
        /// frame's volume, and after the things those volumes hang off have moved — the same
        /// reasoning as the occlusion corridor's publisher.
        /// </summary>
        sealed class Driver : MonoBehaviour
        {
            void LateUpdate() => Flush();

            void OnDisable()
            {
                _lights.Clear();
                Flush();
            }
        }
    }
}
