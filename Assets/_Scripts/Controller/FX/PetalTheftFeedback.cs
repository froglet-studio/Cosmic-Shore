using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using FMODUnity;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// DRAWS A SCORED HIT AS THE THEFT IT IS (Docs/ElementalAbilitySystem/PETAL_THEFT_CUE.md; footage
    /// critique 2026-11-13, weak link 3: "PvP does not read on screen"). PvP is petals only, so every
    /// scored hit takes from one pilot and pays another. Before this a hit was a small star near the
    /// victim, and a muted replay could not say who hit whom. Now, in the world and in 3D:
    /// <list type="number">
    /// <item><b>Flash.</b> A real point light on the victim's hull in the colour set's DANGER colour,
    /// so the hull and the mass around it light up the moment it is hit.</item>
    /// <item><b>Tear-off.</b> Faceted shards in the VICTIM's domain ship material (the facing two-tone of
    /// its own domain band) tear off its skin and hang for a beat: pieces of that hull, in its colour.</item>
    /// <item><b>Stream.</b> They turn and stream to the ATTACKER, stretching with speed, steering onto it
    /// wherever it flies, so the line from victim to attacker draws itself.</item>
    /// <item><b>Pulse.</b> Each shard sinks into the attacker's hull, and the attacker's own light
    /// pulses in its domain colour as they land.</item>
    /// <item><b>Silence.</b> While the victim is silenced, shards in its domain's danger block orbit its
    /// hull, fewer as the silence drains, so every viewer sees who cannot strike back.</item>
    /// </list>
    ///
    /// <para><b>A picture, never a rule.</b> It listens to <see cref="CombatHitCue.Landed"/>, raised for
    /// every hit <see cref="CombatHitDrain.TryAdmit"/> admits, on every machine. It moves nothing, scores
    /// nothing and is not an outcome: the petals still eject or are stolen exactly as before
    /// (<see cref="ElementalTransfer"/>). The shards stand for what the hit was WORTH, which is what the
    /// attacker gained (the score); a fresh pilot holds no petals and is still visibly hit.</para>
    ///
    /// <para><b>Replays.</b> A replay runs the match's real scene and re-runs every recorded press, so
    /// its rounds land and its hits are admitted again: the cue draws in a replay with nothing recorded
    /// for it (Docs/REPLAYS.md §6.3). Every time is GAME time, so a slow-motion shot slows the cue.</para>
    ///
    /// <para>Self-installs once per play session, like <c>SilenceScreenFeedback</c>: a hidden,
    /// scene-independent object with nothing to wire. Every number is on <see cref="PetalTheftCueConfigSO"/>.</para>
    /// </summary>
    public sealed class PetalTheftFeedback : MonoBehaviour
    {
        const string ConfigResourcePath = "PetalTheftCueConfig";

        static PetalTheftFeedback s_instance;
        static Mesh s_shardMesh;

        PetalTheftCueConfigSO _config;

        // ── Shards in flight ────────────────────────────────────────────────────────────
        sealed class Shard
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public bool Live;
            public bool Held;             // a silence shard: placed by its silenced hull, never flown or recycled
            public Transform Target;      // the attacker's hull; null when it is gone
            public float TargetRadius;
            public Vector3 TargetLast;
            public Vector3 Position, Velocity;
            public Vector3 SpinAxis;
            public Quaternion Rotation;
            public float Delay, Age, Travel, Size, AbsorbAge;
            public bool Absorbing, Counted;
            public Glow Pulse;            // the attacker's light this shard feeds when it lands
            public float PulseShare;
            public int Born;
        }

        readonly List<Shard> _shards = new();
        int _born;

        // ── Hull lights ─────────────────────────────────────────────────────────────────
        sealed class Glow
        {
            public Transform Hull;
            public Light Light;
            public float Level;
            public float Radius;
        }

        readonly Dictionary<Transform, Glow> _glows = new();
        readonly List<Transform> _glowScratch = new();

        // ── Silenced hulls ──────────────────────────────────────────────────────────────
        sealed class Silenced
        {
            public string Pilot;
            public Transform Hull;
            public float Radius;
            public Material Material;
            public float Phase;
            public readonly List<Shard> Shards = new();
            public readonly List<float> Shown = new();
        }

        readonly Dictionary<string, Silenced> _silenced = new();
        readonly List<string> _silencedScratch = new();
        readonly Stack<Shard> _idle = new();

        // ── Hull sizes, measured once per hull ──────────────────────────────────────────
        readonly Dictionary<Transform, float> _radii = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (s_instance) return;
            var go = new GameObject("[Petal Theft Feedback]") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<PetalTheftFeedback>();
        }

        void Awake()
        {
            _config = Resources.Load<PetalTheftCueConfigSO>(ConfigResourcePath);
            if (_config) return;
            CSDebug.LogWarning($"[PetalTheftFeedback] No Resources/{ConfigResourcePath} asset - the petal theft cue " +
                               "runs on the PetalTheftCueConfigSO defaults. Create the asset (ScriptableObjects > " +
                               "Vessel > Petal Theft Cue Config).");
            _config = ScriptableObject.CreateInstance<PetalTheftCueConfigSO>();
        }

        void OnEnable()
        {
            CombatHitCue.Landed += OnLanded;
            VesselSilence.VesselSilenced += OnSilenced;
        }

        void OnDisable()
        {
            CombatHitCue.Landed -= OnLanded;
            VesselSilence.VesselSilenced -= OnSilenced;
        }

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        // ── The hit ─────────────────────────────────────────────────────────────────────

        void OnLanded(IVesselStatus victim, IVesselStatus attacker, float petals)
        {
            if (!_config.DrawCue) return;

            // NOT status.Transform: see ElementalCrystalEjector. That property throws rather than
            // answering null on a status whose vessel is gone.
            var victimHull = victim?.Vessel?.Transform;
            var attackerHull = attacker?.Vessel?.Transform;
            if (!victimHull || !attackerHull || victimHull == attackerHull) return;

            float victimRadius = RadiusOf(victimHull);
            float attackerRadius = RadiusOf(attackerHull);
            var palette = Palette;

            // 1. The flash, in the danger colour: the victim was harmed.
            float strength = PetalTheftShapes.FlashStrength(petals, _config.FlashFloor);
            if (_config.HullLights && palette)
                Flash(victimHull, victimRadius, DangerColor(palette, victim.Domain),
                      _config.VictimFlashIntensity * strength);

            PlayAt(_config.TearOffEvent, victimHull.position);

            // 2-4. The shards: the victim's own domain band, torn off and carried to the attacker.
            var material = ShipMaterialOf(victim.Domain);
            int count = PetalTheftShapes.ShardCount(petals, _config.ShardsPerPetal,
                                                    _config.MinShardsPerHit, _config.MaxShardsPerHit);
            if (!material || count <= 0) return;

            Glow pulse = null;
            if (_config.HullLights && palette)
            {
                pulse = GlowFor(attackerHull, attackerRadius);
                pulse.Light.color = SignalColor(palette, attacker.Domain);
            }

            float size = PetalTheftShapes.ShardSize(victimRadius, _config.ShardSizeOfHull,
                                                    _config.ShardMinSize, _config.ShardMaxSize);
            float distance = Vector3.Distance(victimHull.position, attackerHull.position);
            float travel = PetalTheftShapes.TravelSeconds(distance, _config.StreamCruiseSpeed,
                                                          _config.StreamMinSeconds, _config.StreamMaxSeconds);
            float burstSpeed = Mathf.Max(_config.BurstMinSpeed, victimRadius * _config.BurstSpeedOfHull);
            Vector3 away = (victimHull.position - attackerHull.position).normalized;
            float share = strength * _config.AttackerPulseIntensity / count;

            for (int i = 0; i < count; i++)
            {
                var shard = TakeShard(material);
                // Torn off the whole skin, leaning away from the attacker: the hit knocks them loose,
                // then the stream turns them back round onto whoever scored.
                Vector3 dir = (Random.onUnitSphere + away * 0.6f).normalized;
                shard.Position = victimHull.position + dir * (victimRadius * 0.6f);
                shard.Velocity = dir * burstSpeed * Random.Range(0.7f, 1.15f);
                shard.Rotation = Random.rotation;
                shard.SpinAxis = Random.onUnitSphere;
                shard.Size = size * Random.Range(0.75f, 1.2f);
                shard.Delay = i * _config.ShardStagger;
                shard.Age = 0f;
                shard.Travel = travel + Random.Range(-0.08f, 0.12f) * travel;
                shard.Target = attackerHull;
                shard.TargetRadius = attackerRadius;
                shard.TargetLast = attackerHull.position;
                shard.Absorbing = false;
                shard.AbsorbAge = 0f;
                shard.Counted = false;
                shard.Pulse = pulse;
                shard.PulseShare = share;
                shard.Renderer.enabled = false;
                shard.Transform.position = shard.Position;
            }
        }

        // ── Silence ─────────────────────────────────────────────────────────────────────

        void OnSilenced(IVesselStatus status)
        {
            if (!_config.DrawCue || !_config.SilenceShards || _config.SilenceShardCount <= 0) return;
            var hull = status?.Vessel?.Transform;
            if (!hull) return;
            string pilot = status.PlayerName;
            if (string.IsNullOrEmpty(pilot)) return;

            if (_silenced.TryGetValue(pilot, out var held) && held.Hull == hull) return;
            if (held != null) ReleaseSilenced(held);

            var material = DangerMaterialOf(status.Domain);
            if (!material) return;

            var entry = new Silenced
            {
                Pilot = pilot, Hull = hull, Radius = RadiusOf(hull), Material = material,
                Phase = Random.Range(0f, 360f),
            };
            for (int i = 0; i < _config.SilenceShardCount; i++)
            {
                var shard = TakeShard(material, recycle: false);
                if (shard == null) break;
                shard.Held = true;
                shard.SpinAxis = Random.onUnitSphere;
                shard.Rotation = Random.rotation;
                shard.Renderer.enabled = false;
                entry.Shards.Add(shard);
                entry.Shown.Add(0f);
            }
            _silenced[pilot] = entry;
        }

        // ── The frame ───────────────────────────────────────────────────────────────────

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            StepShards(dt);
            StepSilence(dt);
            StepGlows(dt);
        }

        void StepShards(float dt)
        {
            if (dt <= 0f) return;
            for (int i = 0; i < _shards.Count; i++)
            {
                var s = _shards[i];
                if (!s.Live || s.Held) continue;

                if (s.Delay > 0f)
                {
                    s.Delay -= dt;
                    if (s.Delay > 0f) continue;
                }

                if (!s.Absorbing)
                {
                    s.Age += dt;
                    if (s.Age <= _config.BurstSeconds)
                    {
                        // Hanging off the hull: the launch bleeds off and the shard tumbles.
                        s.Velocity *= Mathf.Exp(-_config.BurstDrag * dt);
                        s.Rotation = Quaternion.AngleAxis(_config.BurstSpinDegrees * dt, s.SpinAxis) * s.Rotation;
                    }
                    else if (!s.Target)
                    {
                        // The attacker is gone: nothing to carry it to, so it fades where it is.
                        s.Absorbing = true;
                    }
                    else
                    {
                        Vector3 target = s.Target.position;
                        Vector3 targetVelocity = (target - s.TargetLast) / dt;
                        s.TargetLast = target;
                        float left = s.Travel - (s.Age - _config.BurstSeconds);
                        var desired = PetalTheftShapes.DesiredVelocity(s.Position, target, targetVelocity, left);
                        s.Velocity = PetalTheftShapes.Steer(s.Velocity, desired, _config.StreamSteer, dt);

                        float arrive = s.TargetRadius * _config.ArriveOfHull;
                        if ((target - s.Position).sqrMagnitude <= arrive * arrive ||
                            s.Age > _config.BurstSeconds + s.Travel + 0.5f)
                        {
                            s.Absorbing = true;
                            Land(s);
                        }
                    }
                    s.Position += s.Velocity * dt;
                }
                else
                {
                    s.AbsorbAge += dt;
                    if (s.Target)
                    {
                        // Sinking in: ride the hull it landed on.
                        s.Position = Vector3.Lerp(s.Position, s.Target.position, 1f - Mathf.Exp(-12f * dt));
                    }
                    if (s.AbsorbAge >= _config.AbsorbSeconds)
                    {
                        ReturnShard(s);
                        continue;
                    }
                }

                Draw(s);
            }
        }

        void Draw(Shard s)
        {
            float speed = s.Velocity.magnitude;
            bool streaming = s.Age > _config.BurstSeconds && speed > 1e-3f;
            var rotation = streaming
                ? Quaternion.Slerp(s.Rotation, Quaternion.LookRotation(s.Velocity / speed), 0.85f)
                : s.Rotation;
            if (streaming) s.Rotation = rotation;

            float stretch = streaming ? Mathf.Min(_config.StretchMax, 1f + speed * _config.StretchPerSpeed) : 1f;
            float shrink = s.Absorbing ? 1f - Mathf.Clamp01(s.AbsorbAge / _config.AbsorbSeconds) : 1f;
            // Grow out of the skin over the first tenth of a second, so a shard tears off rather than pops.
            float grow = Mathf.Clamp01(s.Age / 0.1f);
            float girth = s.Size * shrink * grow;

            s.Transform.SetPositionAndRotation(s.Position, rotation);
            s.Transform.localScale = new Vector3(girth, girth, girth * stretch);
            if (!s.Renderer.enabled) s.Renderer.enabled = true;
        }

        void Land(Shard s)
        {
            if (s.Counted) return;
            s.Counted = true;
            if (s.Pulse != null && s.Pulse.Hull == s.Target) s.Pulse.Level += s.PulseShare;
            if (!_config.LandEvent.IsNull && s.Target) PlayAt(_config.LandEvent, s.Target.position);
        }

        void StepSilence(float dt)
        {
            if (_silenced.Count == 0) return;
            float max = VesselSilence.Settings.MaxSeconds;

            _silencedScratch.Clear();
            foreach (var pilot in _silenced.Keys) _silencedScratch.Add(pilot);

            for (int p = 0; p < _silencedScratch.Count; p++)
            {
                var entry = _silenced[_silencedScratch[p]];
                float remaining = entry.Hull ? VesselSilence.RemainingSeconds(entry.Pilot) : 0f;
                int shown = PetalTheftShapes.SilenceShardsShown(remaining, max, entry.Shards.Count);

                bool any = false;
                entry.Phase += _config.SilenceOrbitDegrees * dt;
                float size = entry.Radius * _config.SilenceShardOfHull;
                float orbit = entry.Radius * _config.SilenceOrbitOfHull;
                for (int i = 0; i < entry.Shards.Count; i++)
                {
                    var s = entry.Shards[i];
                    float target = i < shown ? 1f : 0f;
                    float show = Mathf.MoveTowards(entry.Shown[i], target, dt / 0.2f);
                    entry.Shown[i] = show;
                    if (show <= 0f || !entry.Hull)
                    {
                        if (s.Renderer.enabled) s.Renderer.enabled = false;
                        continue;
                    }
                    any = true;

                    // A tilted ring round the hull, in the hull's own frame, each shard on its own height.
                    float a = (entry.Phase + i * 360f / entry.Shards.Count) * Mathf.Deg2Rad;
                    var local = new Vector3(Mathf.Cos(a), Mathf.Sin(a * 2f) * 0.25f, Mathf.Sin(a)) * orbit;
                    s.Rotation = Quaternion.AngleAxis(90f * dt, s.SpinAxis) * s.Rotation;
                    s.Transform.SetPositionAndRotation(entry.Hull.position + entry.Hull.rotation * local, s.Rotation);
                    float g = size * show;
                    s.Transform.localScale = new Vector3(g, g, g * 1.6f);
                    if (!s.Renderer.enabled) s.Renderer.enabled = true;
                }

                if (!any && remaining <= 0f) ReleaseSilenced(entry);
            }
        }

        void ReleaseSilenced(Silenced entry)
        {
            for (int i = 0; i < entry.Shards.Count; i++) ReturnShard(entry.Shards[i]);
            entry.Shards.Clear();
            entry.Shown.Clear();
            _silenced.Remove(entry.Pilot);
        }

        void StepGlows(float dt)
        {
            if (_glows.Count == 0) return;
            float decay = dt > 0f ? Mathf.Exp(-dt / _config.FlashDecaySeconds) : 1f;

            _glowScratch.Clear();
            foreach (var pair in _glows)
            {
                var g = pair.Value;
                if (!g.Hull || !g.Light)
                {
                    if (g.Light) Destroy(g.Light.gameObject);
                    _glowScratch.Add(pair.Key);
                    continue;
                }

                g.Level *= decay;
                bool on = g.Level > 0.02f;
                if (g.Light.enabled != on) g.Light.enabled = on;
                if (!on) continue;
                g.Light.transform.position = g.Hull.position;
                g.Light.intensity = g.Level;
                g.Light.range = g.Radius * _config.FlashRangeOfHull;
            }
            for (int i = 0; i < _glowScratch.Count; i++) _glows.Remove(_glowScratch[i]);
        }

        // ── Pieces ──────────────────────────────────────────────────────────────────────

        void Flash(Transform hull, float radius, Color color, float intensity)
        {
            var g = GlowFor(hull, radius);
            g.Light.color = color;
            g.Level = Mathf.Max(g.Level, intensity);
        }

        Glow GlowFor(Transform hull, float radius)
        {
            if (_glows.TryGetValue(hull, out var g) && g.Light) return g;
            var go = new GameObject("PetalTheftLight") { hideFlags = HideFlags.HideInHierarchy };
            go.transform.SetParent(transform, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.enabled = false;
            g = new Glow { Hull = hull, Light = light, Radius = radius };
            _glows[hull] = g;
            return g;
        }

        Shard TakeShard(Material material, bool recycle = true)
        {
            Shard s = null;
            while (_idle.Count > 0 && s == null)
            {
                s = _idle.Pop();
                if (!s.Transform) s = null;
            }

            if (s == null)
            {
                int live = _shards.Count - _idle.Count;
                if (live >= _config.MaxLiveShards)
                {
                    if (!recycle) return null;
                    s = OldestFlying();
                    if (s == null) return null;
                }
                else
                {
                    var go = new GameObject("PetalTheftShard") { hideFlags = HideFlags.HideInHierarchy };
                    go.transform.SetParent(transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = ShardMesh;
                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    s = new Shard { Transform = go.transform, Renderer = renderer };
                    _shards.Add(s);
                }
            }

            s.Live = true;
            s.Born = ++_born;
            if (s.Renderer.sharedMaterial != material) s.Renderer.sharedMaterial = material;
            return s;
        }

        Shard OldestFlying()
        {
            Shard oldest = null;
            for (int i = 0; i < _shards.Count; i++)
            {
                var s = _shards[i];
                if (!s.Live || s.Held) continue;
                if (oldest == null || s.Born < oldest.Born) oldest = s;
            }
            return oldest;
        }

        void ReturnShard(Shard s)
        {
            if (!s.Live) return;
            s.Live = false;
            s.Held = false;
            s.Target = null;
            s.Pulse = null;
            s.Travel = 0f;
            if (s.Renderer) s.Renderer.enabled = false;
            _idle.Push(s);
        }

        float RadiusOf(Transform hull)
        {
            if (_radii.TryGetValue(hull, out float r) && r > 0f) return r;
            r = Mathf.Max(1f, PrismOcclusionCorridor.MeasureCircumscribedRadius(hull));
            if (_radii.Count > 64) PruneRadii();
            _radii[hull] = r;
            return r;
        }

        void PruneRadii()
        {
            _glowScratch.Clear();
            foreach (var key in _radii.Keys)
                if (!key) _glowScratch.Add(key);
            for (int i = 0; i < _glowScratch.Count; i++) _radii.Remove(_glowScratch[i]);
        }

        static Mesh ShardMesh => s_shardMesh ? s_shardMesh : s_shardMesh = PetalTheftShapes.BuildShardMesh();

        static SO_ColorSet Palette
        {
            get
            {
                var data = ThemeManager.Data;
                return data != null && data.ColorSet ? data.ColorSet : PrismLit.ColorSet;
            }
        }

        static SO_MaterialSet MaterialSetOf(Domains domain)
        {
            var sets = ThemeManager.Data != null ? ThemeManager.Data.TeamMaterialSets : null;
            return sets != null && sets.TryGetValue(domain, out var set) && set ? set : null;
        }

        /// <summary>The domain's ship material: the facing two-tone every hull's domain band wears, painted
        /// from the worn colour set (<see cref="ThemeManager"/> builds it), the material the Brittle Star's
        /// ring blast is cut from too.</summary>
        static Material ShipMaterialOf(Domains domain) => MaterialSetOf(domain)?.ShipMaterial;

        /// <summary>The domain's danger block: what a danger prism wears. Silence is the pilot's harm taken
        /// away, so its shards wear the harm colour.</summary>
        static Material DangerMaterialOf(Domains domain)
        {
            var set = MaterialSetOf(domain);
            if (!set) return null;
            return set.DangerousBlockMaterial ? set.DangerousBlockMaterial : set.ShipMaterial;
        }

        static Color SignalColor(SO_ColorSet palette, Domains domain)
        {
            var c = palette.GetDomainSignalColor(domain);
            c.a = 1f;
            return c;
        }

        static Color DangerColor(SO_ColorSet palette, Domains fallbackDomain)
        {
            var c = palette.GetDangerSignalColor();
            if (c.a <= 0f) c = palette.GetDomainSignalColor(fallbackDomain);
            c.a = 1f;
            return c;
        }

        static void PlayAt(EventReference reference, Vector3 at)
        {
            if (reference.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(reference, at);
        }
    }
}
