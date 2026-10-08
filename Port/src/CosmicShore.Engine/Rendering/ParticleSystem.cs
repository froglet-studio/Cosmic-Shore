using System;
using System.Collections.Generic;

namespace CosmicShore.Engine
{
    public enum ParticleSystemSimulationSpace { Local = 0, World = 1, Custom = 2 }
    public enum ParticleSystemScalingMode { Hierarchy = 0, Local = 1, Shape = 2 }
    public enum ParticleSystemStopAction { None = 0, Disable = 1, Destroy = 2, Callback = 3 }
    public enum ParticleSystemStopBehavior { StopEmittingAndClear = 0, StopEmitting = 1 }
    public enum ParticleSystemCurveMode { Constant = 0, Curve = 1, TwoCurves = 2, TwoConstants = 3 }
    public enum ParticleSystemGradientMode { Color = 0, Gradient = 1, TwoColors = 2, TwoGradients = 3, RandomColor = 4 }
    public enum ParticleSystemShapeType { Sphere = 0, Hemisphere = 2, Cone = 4, Box = 5, Mesh = 6, ConeVolume = 8, Circle = 10, SingleSidedEdge = 12, MeshRenderer = 13, SkinnedMeshRenderer = 14, BoxShell = 15, BoxEdge = 16, Donut = 17, Rectangle = 18, Sprite = 19, SpriteRenderer = 20 }
    public enum ParticleSystemRenderMode { Billboard = 0, Stretch = 1, HorizontalBillboard = 2, VerticalBillboard = 3, Mesh = 4, None = 5 }
    public enum ParticleSystemEmitterVelocityMode { Transform = 0, Rigidbody = 1, Custom = 2 }
    public enum ParticleSystemNoiseQuality { Low = 0, Medium = 1, High = 2 }

    /// <summary>
    /// UnityEngine.ParticleSystem, simulated: each particle is born from the shape module with the
    /// main module's start values, moves under gravity and velocity over lifetime (limited and
    /// damped), and ages through colour, size and rotation over lifetime; emission is rate over
    /// time and distance plus bursts. The renderer reads the live particles through
    /// <see cref="GetRenderParticles"/> and draws them as billboards. Modules are proxy structs over
    /// shared per-system state, as in the original: <c>var main = ps.main; main.startSize = 2;</c>
    /// writes through. Not simulated: noise, collision, sub-emitters, trails, texture-sheet frames.
    /// Adding one adds its ParticleSystemRenderer, as the original does (AstroLeagueBall builds
    /// its sparks with AddComponent and styles GetComponent&lt;ParticleSystemRenderer&gt;()).
    /// </summary>
    [RequireComponent(typeof(ParticleSystemRenderer))]
    public class ParticleSystem : Component
    {
        [Serializable]
        public struct MinMaxCurve
        {
            public ParticleSystemCurveMode mode;
            public float constantMin, constantMax, curveMultiplier;
            public AnimationCurve curveMin, curveMax;

            public MinMaxCurve(float constant) { this = default; mode = ParticleSystemCurveMode.Constant; constantMin = constantMax = constant; curveMultiplier = 1f; }
            public MinMaxCurve(float min, float max) { this = default; mode = ParticleSystemCurveMode.TwoConstants; constantMin = min; constantMax = max; curveMultiplier = 1f; }
            public MinMaxCurve(float multiplier, AnimationCurve curve) { this = default; mode = ParticleSystemCurveMode.Curve; curveMultiplier = multiplier; curveMax = curve; }
            public MinMaxCurve(float multiplier, AnimationCurve min, AnimationCurve max) { this = default; mode = ParticleSystemCurveMode.TwoCurves; curveMultiplier = multiplier; curveMin = min; curveMax = max; }

            public float constant { get => constantMax; set => constantMin = constantMax = value; }
            public AnimationCurve curve { get => curveMax; set => curveMax = value; }

            public float Evaluate(float time, float lerpFactor = 1f) => mode switch
            {
                ParticleSystemCurveMode.Constant => constantMax,
                ParticleSystemCurveMode.TwoConstants => constantMin + (constantMax - constantMin) * lerpFactor,
                ParticleSystemCurveMode.Curve => (curveMax?.Evaluate(time) ?? 0f) * curveMultiplier,
                _ => ((curveMin?.Evaluate(time) ?? 0f) + ((curveMax?.Evaluate(time) ?? 0f) - (curveMin?.Evaluate(time) ?? 0f)) * lerpFactor) * curveMultiplier,
            };

            public static implicit operator MinMaxCurve(float constant) => new(constant);
        }

        [Serializable]
        public struct MinMaxGradient
        {
            public ParticleSystemGradientMode mode;
            public Color colorMin, colorMax;
            public Gradient gradientMin, gradientMax;

            public MinMaxGradient(Color color) { this = default; mode = ParticleSystemGradientMode.Color; colorMin = colorMax = color; }
            public MinMaxGradient(Color min, Color max) { this = default; mode = ParticleSystemGradientMode.TwoColors; colorMin = min; colorMax = max; }
            public MinMaxGradient(Gradient gradient) { this = default; mode = ParticleSystemGradientMode.Gradient; gradientMax = gradient; }
            public MinMaxGradient(Gradient min, Gradient max) { this = default; mode = ParticleSystemGradientMode.TwoGradients; gradientMin = min; gradientMax = max; }

            public Color color { get => colorMax; set => colorMin = colorMax = value; }
            public Gradient gradient { get => gradientMax; set => gradientMax = value; }

            public Color Evaluate(float time, float lerpFactor = 1f) => mode switch
            {
                ParticleSystemGradientMode.Color => colorMax,
                ParticleSystemGradientMode.TwoColors => Color.Lerp(colorMin, colorMax, lerpFactor),
                ParticleSystemGradientMode.Gradient => gradientMax?.Evaluate(time) ?? Color.white,
                ParticleSystemGradientMode.RandomColor => gradientMax?.Evaluate(lerpFactor) ?? Color.white,
                _ => Color.Lerp(gradientMin?.Evaluate(time) ?? Color.white, gradientMax?.Evaluate(time) ?? Color.white, lerpFactor),
            };

            public static implicit operator MinMaxGradient(Color color) => new(color);
            public static implicit operator MinMaxGradient(Gradient gradient) => new(gradient);
        }

        public struct Burst
        {
            public float time;
            public MinMaxCurve count;
            public int cycleCount;
            public float repeatInterval;
            public float probability;
            public Burst(float time, short count) { this.time = time; this.count = count; cycleCount = 1; repeatInterval = 0.01f; probability = 1f; }
            public Burst(float time, float count) { this.time = time; this.count = count; cycleCount = 1; repeatInterval = 0.01f; probability = 1f; }
            public Burst(float time, MinMaxCurve count) { this.time = time; this.count = count; cycleCount = 1; repeatInterval = 0.01f; probability = 1f; }
            public short minCount { get => (short)count.constantMin; set => count.constantMin = value; }
            public short maxCount { get => (short)count.constantMax; set => count.constantMax = value; }
        }

        public struct Particle
        {
            public Vector3 position { get; set; }
            public Vector3 velocity { get; set; }
            public float remainingLifetime { get; set; }
            public float startLifetime { get; set; }
            public Color32 startColor { get; set; }
            public float startSize { get; set; }
            public Vector3 startSize3D { get; set; }
            public float rotation { get; set; }
            public uint randomSeed { get; set; }
        }

        public struct EmitParams
        {
            public Vector3 position { get; set; }
            public Vector3 velocity { get; set; }
            public float startLifetime { get; set; }
            public float startSize { get; set; }
            public Color32 startColor { get; set; }
            public bool applyShapeToPosition { get; set; }
        }

        // ── shared module state ──────────────────────────────────────────────
        internal sealed class State
        {
            public float duration = 5f, simulationSpeed = 1f, gravityModifierMultiplier = 1f, startDelayMultiplier = 1f;
            public bool loop = true, playOnAwake = true, prewarm, useUnscaledTime, startSize3D, startRotation3D;
            public MinMaxCurve startLifetime = 5f, startSpeed = 5f, startSize = 1f, startSizeX = 1f, startSizeY = 1f, startSizeZ = 1f, startRotation = 0f, gravityModifier = 0f, startDelay = 0f;
            public MinMaxGradient startColor = Color.white;
            public int maxParticles = 1000;
            public ParticleSystemSimulationSpace simulationSpace;
            public ParticleSystemScalingMode scalingMode = ParticleSystemScalingMode.Local;
            public ParticleSystemStopAction stopAction;
            public Transform customSimulationSpace;
            public ParticleSystemEmitterVelocityMode emitterVelocityMode;

            public bool emissionEnabled = true;
            public MinMaxCurve rateOverTime = 10f, rateOverDistance = 0f;
            public readonly List<Burst> bursts = new();

            public bool shapeEnabled = true;
            public ParticleSystemShapeType shapeType = ParticleSystemShapeType.Cone;
            public float radius = 1f, angle = 25f, arc = 360f, radiusThickness = 1f, length = 5f, donutRadius = 0.2f, randomDirectionAmount, sphericalDirectionAmount;
            public Vector3 boxThickness;
            public Vector3 shapePosition, shapeRotation, shapeScale = Vector3.one;
            public Mesh shapeMesh;
            public MeshRenderer shapeMeshRenderer;
            public SkinnedMeshRenderer shapeSkinnedMeshRenderer;

            public bool colorOverLifetimeEnabled; public MinMaxGradient colorOverLifetime = Color.white;
            public bool sizeOverLifetimeEnabled, sizeSeparateAxes; public MinMaxCurve sizeOverLifetime = 1f, sizeX = 1f, sizeY = 1f, sizeZ = 1f; public float sizeMultiplier = 1f;
            public bool velocityEnabled; public MinMaxCurve velX = 0f, velY = 0f, velZ = 0f, orbitalX = 0f, orbitalY = 0f, orbitalZ = 0f, radial = 0f, speedModifier = 1f; public ParticleSystemSimulationSpace velSpace;
            public bool noiseEnabled, noiseSeparateAxes; public MinMaxCurve noiseStrength = 1f, scrollSpeed = 0f; public float frequency = 0.5f; public int octaveCount = 1; public ParticleSystemNoiseQuality noiseQuality = ParticleSystemNoiseQuality.High; public MinMaxCurve positionAmount = 1f;
            public bool trailsEnabled; public MinMaxCurve trailLifetime = 1f, widthOverTrail = 1f; public float ratio = 1f; public MinMaxGradient colorOverTrail = Color.white;
            public bool limitVelocityEnabled; public MinMaxCurve limit = 1f; public float dampen = 1f;
            public bool rotationOverLifetimeEnabled; public MinMaxCurve rotationZ = 0f;
            public bool collisionEnabled, textureSheetEnabled, lightsEnabled, subEmittersEnabled, inheritVelocityEnabled, forceOverLifetimeEnabled, externalForcesEnabled, triggerEnabled, customDataEnabled;
        }

        internal readonly State s = new();
        float _time, _emitAccumulator, _distanceAccumulator, _delayLeft;
        bool _playing, _emitting, _paused, _hasLastPos;
        Vector3 _lastEmitterPos;
        readonly List<int> _burstCyclesDone = new();
        System.Random _rng;

        /// <summary>One live particle. Positions and velocities are in the simulation space (the emitter's local space, or world).</summary>
        struct Live
        {
            public Vector3 Pos, Vel, Size0;
            public float Age, Life, Rot0, RotZ;
            public Color Col0;
            public float R0, R1; // per-particle randoms for two-curve / two-colour lerps over lifetime
        }
        Live[] _p = new Live[16];
        int _n;

        public MainModule main => new(this);
        public EmissionModule emission => new(this);
        public ShapeModule shape => new(this);
        public ColorOverLifetimeModule colorOverLifetime => new(this);
        public SizeOverLifetimeModule sizeOverLifetime => new(this);
        public VelocityOverLifetimeModule velocityOverLifetime => new(this);
        public NoiseModule noise => new(this);
        public TrailModule trails => new(this);
        public LimitVelocityOverLifetimeModule limitVelocityOverLifetime => new(this);
        public RotationOverLifetimeModule rotationOverLifetime => new(this);
        public ToggleModule collision => new(this, 0);
        public ToggleModule textureSheetAnimation => new(this, 1);
        public ToggleModule lights => new(this, 2);
        public ToggleModule subEmitters => new(this, 3);
        public ToggleModule inheritVelocity => new(this, 4);
        public ToggleModule forceOverLifetime => new(this, 5);
        public ToggleModule externalForces => new(this, 6);
        public ToggleModule trigger => new(this, 7);
        public ToggleModule customData => new(this, 8);

        public bool isPlaying => _playing && !_paused;
        public bool isEmitting => _emitting && !_paused;
        public bool isStopped => !_playing;
        public bool isPaused => _paused;
        public int particleCount => _n;
        public float time { get => _time; set => _time = value; }
        public float totalTime => _time;
        public uint randomSeed { get; set; }
        public bool useAutoRandomSeed { get; set; } = true;
        public bool proceduralSimulationSupported => true;

        public void Play() => Play(true);
        public void Play(bool withChildren)
        {
            if (!_playing || !_emitting)
            {
                if (!_playing) { _time = 0; _emitAccumulator = 0; _distanceAccumulator = 0; _burstCyclesDone.Clear(); _hasLastPos = false; }
                _delayLeft = s.startDelay.Evaluate(0f, Rand()) * s.startDelayMultiplier;
            }
            _playing = true; _emitting = true; _paused = false; _justPlayed = true;
            if (s.prewarm && s.loop && _n == 0) Prewarm();
            if (withChildren) foreach (var c in Children()) c.Play(false);
        }
        public void Stop() => Stop(true, ParticleSystemStopBehavior.StopEmitting);
        public void Stop(bool withChildren) => Stop(withChildren, ParticleSystemStopBehavior.StopEmitting);
        public void Stop(bool withChildren, ParticleSystemStopBehavior stopBehavior)
        {
            _emitting = false;
            if (stopBehavior == ParticleSystemStopBehavior.StopEmittingAndClear) { _n = 0; Finish(); }
            if (withChildren) foreach (var c in Children()) c.Stop(false, stopBehavior);
        }
        public void Pause(bool withChildren = true) { _paused = true; if (withChildren) foreach (var c in Children()) c.Pause(false); }
        public void Clear(bool withChildren = true) { _n = 0; if (withChildren) foreach (var c in Children()) c.Clear(false); }
        public bool IsAlive(bool withChildren = true) => _playing && (_emitting || _n > 0);
        public void Emit(int count)
        {
            for (int i = 0; i < count; i++) Spawn(null);
            if (!_playing) { _playing = true; _emitting = false; }
        }
        public void Emit(EmitParams emitParams, int count)
        {
            for (int i = 0; i < count; i++) Spawn(emitParams);
            if (!_playing) { _playing = true; _emitting = false; }
        }
        public void Simulate(float t, bool withChildren = true, bool restart = true, bool fixedTimeStep = true)
        {
            if (restart) { _time = 0; _n = 0; _emitAccumulator = 0; _burstCyclesDone.Clear(); _playing = true; _emitting = true; }
            const float step = 1f / 30f;
            for (float done = 0; done < t; done += step) Step(Math.Min(step, t - done));
            _paused = true;
        }
        public int GetParticles(Particle[] particles) => GetParticles(particles, particles?.Length ?? 0);
        public int GetParticles(Particle[] particles, int size)
        {
            if (particles == null) return 0;
            int n = Math.Min(Math.Min(size, particles.Length), _n);
            for (int i = 0; i < n; i++)
            {
                ref var q = ref _p[i];
                particles[i] = new Particle
                {
                    position = q.Pos, velocity = q.Vel, remainingLifetime = q.Life - q.Age, startLifetime = q.Life,
                    startColor = q.Col0, startSize = q.Size0.x, startSize3D = q.Size0, rotation = (q.Rot0 + q.RotZ) * Mathf.Rad2Deg,
                    randomSeed = (uint)(q.R0 * uint.MaxValue),
                };
            }
            return n;
        }
        public void SetParticles(Particle[] particles) => SetParticles(particles, particles?.Length ?? 0);
        public void SetParticles(Particle[] particles, int size)
        {
            if (particles == null) { _n = 0; return; }
            int n = Math.Min(size, particles.Length);
            Ensure(n);
            for (int i = 0; i < n; i++)
            {
                var src = particles[i];
                float life = Math.Max(1e-4f, src.startLifetime);
                _p[i] = new Live
                {
                    Pos = src.position, Vel = src.velocity, Life = life, Age = Math.Clamp(life - src.remainingLifetime, 0f, life),
                    Col0 = src.startColor, Size0 = src.startSize3D == Vector3.zero ? new Vector3(src.startSize, src.startSize, src.startSize) : src.startSize3D,
                    Rot0 = src.rotation * Mathf.Deg2Rad, R0 = 0.5f, R1 = 0.5f,
                };
            }
            _n = n;
        }
        public void TriggerSubEmitter(int subEmitterIndex) { }

        IEnumerable<ParticleSystem> Children()
        {
            foreach (var c in GetComponentsInChildren<ParticleSystem>(true)) if (!ReferenceEquals(c, this)) yield return c;
        }

        // ParticleSystem is not a MonoBehaviour, so no Awake/Update reach it: the game loop ticks every
        // live system (GameLoop, after the animators). A system that was not ticked last frame has
        // just become active - Unity's OnEnable - and plays when playOnAwake; one that went inactive
        // lost its particles.
        static readonly List<ParticleSystem> s_tick = new();
        static int s_tickId;
        int _lastTick = -2;

        /// <summary>Advances every active particle system by this frame's delta time.</summary>
        public static void TickAll()
        {
            s_tickId++;
            LiveComponents<ParticleSystem>.CollectActive(s_tick);
            foreach (var ps in s_tick)
            {
                try { ps.Tick(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        void Tick()
        {
            bool everTicked = _lastTick >= 0, activated = _lastTick != s_tickId - 1;
            _lastTick = s_tickId;
            if (activated)
            {
                // Back from inactive: Unity cleared it then (an explicit Play since keeps its particles).
                if (everTicked && !_justPlayed) { _n = 0; _playing = false; }
                if (s.playOnAwake && !_playing) Play(false);
            }
            _justPlayed = false;
            if (!_playing || _paused) return;
            float dt = (s.useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) * s.simulationSpeed;
            Step(dt);
        }

        bool _justPlayed;

        float Rand() => (float)(_rng ??= useAutoRandomSeed ? new System.Random() : new System.Random((int)randomSeed)).NextDouble();

        void Ensure(int n) { if (_p.Length < n) Array.Resize(ref _p, Math.Max(n, _p.Length * 2)); }

        void Prewarm()
        {
            // One whole cycle already run, as Unity's prewarm (sampled at 30 Hz).
            float d = Math.Max(0.0001f, s.duration);
            const float step = 1f / 30f;
            for (float t = 0; t < d; t += step) Step(step);
        }

        void Step(float dt)
        {
            if (dt <= 0f) return;
            bool world = s.simulationSpace == ParticleSystemSimulationSpace.World;
            // Age, move and retire.
            var gravity = Physics.gravity * (s.gravityModifier.Evaluate(Norm(), 0.5f) * s.gravityModifierMultiplier);
            if (!world) gravity = Quaternion.Inverse(transform.rotation) * gravity;
            for (int i = 0; i < _n; i++)
            {
                ref var q = ref _p[i];
                q.Age += dt;
                if (q.Age >= q.Life) { _p[i] = _p[--_n]; i--; continue; }
                float t = q.Age / q.Life;
                q.Vel += gravity * dt;
                if (s.limitVelocityEnabled)
                {
                    float lim = s.limit.Evaluate(t, q.R0), sp = q.Vel.magnitude;
                    if (sp > lim && sp > 1e-6f) q.Vel = Vector3.Lerp(q.Vel, q.Vel * (lim / sp), s.dampen);
                }
                var v = q.Vel;
                if (s.velocityEnabled)
                {
                    var extra = new Vector3(s.velX.Evaluate(t, q.R1), s.velY.Evaluate(t, q.R1), s.velZ.Evaluate(t, q.R1));
                    if (s.velSpace != s.simulationSpace)
                        extra = world ? transform.TransformDirection(extra) : transform.InverseTransformDirection(extra);
                    v = (v + extra) * s.speedModifier.Evaluate(t, q.R1);
                }
                q.Pos += v * dt;
                if (s.rotationOverLifetimeEnabled) q.RotZ += s.rotationZ.Evaluate(t, q.R1) * dt;
            }

            if (_delayLeft > 0f) { _delayLeft -= dt; if (_delayLeft > 0f) { CheckFinished(); return; } }
            float prev = _time;
            _time += dt;
            if (_emitting && s.emissionEnabled)
            {
                float rate = s.rateOverTime.Evaluate(Norm(), Rand());
                _emitAccumulator += rate * dt;
                // Rate over distance: what the emitter travelled in world space this step.
                var here = transform.position;
                if (_hasLastPos && (s.rateOverDistance.constantMax > 0f || s.rateOverDistance.mode != ParticleSystemCurveMode.Constant))
                    _distanceAccumulator += (here - _lastEmitterPos).magnitude * s.rateOverDistance.Evaluate(Norm(), Rand());
                _lastEmitterPos = here; _hasLastPos = true;
                int whole = (int)MathF.Floor(_emitAccumulator) + (int)MathF.Floor(_distanceAccumulator);
                _emitAccumulator -= MathF.Floor(_emitAccumulator);
                _distanceAccumulator -= MathF.Floor(_distanceAccumulator);
                for (int i = 0; i < whole; i++) Spawn(null);
                Bursts(prev, _time);
            }
            if (_time >= s.duration)
            {
                if (s.loop) { _time %= Math.Max(0.0001f, s.duration); _burstCyclesDone.Clear(); }
                else _emitting = false;
            }
            CheckFinished();
        }

        float Norm() => Math.Clamp(_time / Math.Max(0.0001f, s.duration), 0f, 1f);

        void Bursts(float from, float to)
        {
            while (_burstCyclesDone.Count < s.bursts.Count) _burstCyclesDone.Add(0);
            for (int b = 0; b < s.bursts.Count; b++)
            {
                var burst = s.bursts[b];
                int cycles = burst.cycleCount <= 0 ? int.MaxValue : burst.cycleCount;
                while (_burstCyclesDone[b] < cycles)
                {
                    float at = burst.time + _burstCyclesDone[b] * Math.Max(0.0001f, burst.repeatInterval);
                    if (at > to || at >= s.duration) break;
                    _burstCyclesDone[b]++;
                    if (at < from && from > 0f) continue; // already passed before this step (only from the start of a cycle)
                    if (Rand() > burst.probability) continue;
                    int count = (int)MathF.Round(burst.count.Evaluate(Norm(), Rand()));
                    for (int i = 0; i < count; i++) Spawn(null);
                }
            }
        }

        void CheckFinished() { if (!_emitting && _n == 0 && _delayLeft <= 0f) Finish(); }

        void Spawn(EmitParams? param)
        {
            if (_n >= s.maxParticles) return;
            Ensure(_n + 1);
            float t = Norm();
            var (pos, dir) = s.shapeEnabled ? ShapePoint() : (Vector3.zero, Vector3.forward);
            float speed = s.startSpeed.Evaluate(t, Rand());
            var vel = dir * speed;
            bool world = s.simulationSpace == ParticleSystemSimulationSpace.World;
            if (world)
            {
                var scale = s.scalingMode switch
                {
                    ParticleSystemScalingMode.Hierarchy => transform.lossyScale,
                    ParticleSystemScalingMode.Local => transform.localScale,
                    _ => Vector3.one,
                };
                pos = transform.position + transform.rotation * Vector3.Scale(pos, scale);
                vel = transform.rotation * vel;
            }
            float r = Rand();
            var size = s.startSize3D
                ? new Vector3(s.startSizeX.Evaluate(t, r), s.startSizeY.Evaluate(t, r), s.startSizeZ.Evaluate(t, r))
                : Vector3.one * s.startSize.Evaluate(t, r);
            var live = new Live
            {
                Pos = pos, Vel = vel, Size0 = size,
                Life = Math.Max(1e-4f, s.startLifetime.Evaluate(t, Rand())),
                Rot0 = s.startRotation.Evaluate(t, Rand()),
                Col0 = s.startColor.Evaluate(t, Rand()),
                R0 = Rand(), R1 = Rand(),
            };
            if (param is { } e)
            {
                if (e.applyShapeToPosition) live.Pos += e.position; else if (e.position != Vector3.zero) live.Pos = e.position;
                if (e.velocity != Vector3.zero) live.Vel = e.velocity;
                if (e.startLifetime > 0f) live.Life = e.startLifetime;
                if (e.startSize > 0f) live.Size0 = Vector3.one * e.startSize;
                if (e.startColor.a != 0 || e.startColor.r != 0 || e.startColor.g != 0 || e.startColor.b != 0) live.Col0 = e.startColor;
            }
            _p[_n++] = live;
        }

        /// <summary>A birth point and direction in the emitter's local space, from the shape module.</summary>
        (Vector3 pos, Vector3 dir) ShapePoint()
        {
            Vector3 pos, dir;
            float radius = s.radius, thick = Math.Clamp(s.radiusThickness, 0f, 1f);
            float arc = s.arc * Mathf.Deg2Rad;
            Vector3 RandomUnit()
            {
                float z = Rand() * 2f - 1f, a = Rand() * MathF.PI * 2f, rr = MathF.Sqrt(Math.Max(0f, 1f - z * z));
                return new Vector3(rr * MathF.Cos(a), rr * MathF.Sin(a), z);
            }
            float Shell(float cubeRoot) => radius * (1f - thick * (1f - cubeRoot));
            switch (s.shapeType)
            {
                case ParticleSystemShapeType.Sphere:
                    dir = RandomUnit(); pos = dir * Shell(MathF.Cbrt(Rand())); break;
                case ParticleSystemShapeType.Hemisphere:
                    dir = RandomUnit(); if (dir.z < 0) dir.z = -dir.z; pos = dir * Shell(MathF.Cbrt(Rand())); break;
                case ParticleSystemShapeType.Cone:
                case ParticleSystemShapeType.ConeVolume:
                {
                    float a = Rand() * arc, rr = Shell(MathF.Sqrt(Rand())) / Math.Max(radius, 1e-6f);
                    var ring = new Vector3(MathF.Cos(a), MathF.Sin(a), 0f);
                    float angle = Math.Clamp(s.angle, 0f, 90f) * Mathf.Deg2Rad;
                    dir = new Vector3(ring.x * rr * MathF.Sin(angle), ring.y * rr * MathF.Sin(angle), MathF.Cos(angle)).normalized;
                    pos = ring * rr * radius;
                    if (s.shapeType == ParticleSystemShapeType.ConeVolume) pos += dir * (Rand() * s.length);
                    break;
                }
                case ParticleSystemShapeType.Box:
                case ParticleSystemShapeType.BoxShell:
                case ParticleSystemShapeType.BoxEdge:
                    pos = new Vector3(Rand() - 0.5f, Rand() - 0.5f, Rand() - 0.5f); dir = Vector3.forward; break;
                case ParticleSystemShapeType.Circle:
                case ParticleSystemShapeType.Donut:
                {
                    float a = Rand() * arc;
                    dir = new Vector3(MathF.Cos(a), MathF.Sin(a), 0f);
                    pos = dir * Shell(MathF.Sqrt(Rand()));
                    break;
                }
                case ParticleSystemShapeType.SingleSidedEdge:
                    pos = new Vector3((Rand() * 2f - 1f) * radius, 0f, 0f); dir = Vector3.up; break;
                case ParticleSystemShapeType.Rectangle:
                    pos = new Vector3(Rand() - 0.5f, Rand() - 0.5f, 0f); dir = Vector3.forward; break;
                case ParticleSystemShapeType.Mesh:
                case ParticleSystemShapeType.MeshRenderer:
                case ParticleSystemShapeType.SkinnedMeshRenderer:
                {
                    var mesh = s.shapeMesh ?? s.shapeMeshRenderer?.GetComponent<MeshFilter>()?.sharedMesh ?? s.shapeSkinnedMeshRenderer?.sharedMesh;
                    var verts = mesh?.vertices;
                    if (verts is { Length: > 0 })
                    {
                        int k = Math.Min(verts.Length - 1, (int)(Rand() * verts.Length));
                        pos = verts[k];
                        var normals = mesh.normals;
                        dir = normals is { Length: > 0 } && k < normals.Length ? normals[k] : pos.normalized;
                    }
                    else { dir = RandomUnit(); pos = Vector3.zero; }
                    break;
                }
                default:
                    dir = RandomUnit(); pos = Vector3.zero; break;
            }
            if (s.randomDirectionAmount > 0f) dir = Vector3.Slerp(dir, RandomUnit(), s.randomDirectionAmount);
            if (s.sphericalDirectionAmount > 0f && pos.sqrMagnitude > 1e-10f) dir = Vector3.Slerp(dir, pos.normalized, s.sphericalDirectionAmount);
            var rot = Quaternion.Euler(s.shapeRotation);
            pos = rot * Vector3.Scale(pos, s.shapeScale) + s.shapePosition;
            dir = (rot * dir).normalized;
            return (pos, dir);
        }

        /// <summary>One particle as drawn: world position, size (x, y), colour and rotation (radians) after the over-lifetime modules.</summary>
        public struct RenderParticle
        {
            public Vector3 Position, Velocity;
            public Vector2 Size;
            public Color Color;
            public float Rotation;
        }

        /// <summary>
        /// Port hook for the renderer: the live particles in WORLD space with colour, size and
        /// rotation over lifetime applied. Returns the count written (grows <paramref name="buffer"/>).
        /// </summary>
        public int GetRenderParticles(ref RenderParticle[] buffer)
        {
            if (buffer == null || buffer.Length < _n) buffer = new RenderParticle[Math.Max(_n, 16)];
            bool world = s.simulationSpace == ParticleSystemSimulationSpace.World;
            var scale = s.scalingMode switch
            {
                ParticleSystemScalingMode.Hierarchy => transform.lossyScale,
                ParticleSystemScalingMode.Local => transform.localScale,
                _ => Vector3.one,
            };
            var rotation = transform.rotation;
            var origin = transform.position;
            float sizeScale = s.scalingMode == ParticleSystemScalingMode.Shape ? 1f : Math.Abs(scale.x);
            for (int i = 0; i < _n; i++)
            {
                ref var q = ref _p[i];
                float t = q.Age / q.Life;
                var col = q.Col0;
                if (s.colorOverLifetimeEnabled) col *= s.colorOverLifetime.Evaluate(t, q.R1);
                var size = new Vector2(q.Size0.x, q.Size0.y);
                if (s.sizeOverLifetimeEnabled)
                {
                    if (s.sizeSeparateAxes) size = new Vector2(size.x * s.sizeX.Evaluate(t, q.R1), size.y * s.sizeY.Evaluate(t, q.R1));
                    else size *= s.sizeOverLifetime.Evaluate(t, q.R1) * s.sizeMultiplier;
                }
                buffer[i] = new RenderParticle
                {
                    Position = world ? q.Pos : origin + rotation * Vector3.Scale(q.Pos, scale),
                    Velocity = world ? q.Vel : rotation * q.Vel,
                    Size = world ? size : size * sizeScale,
                    Color = col,
                    Rotation = q.Rot0 + q.RotZ,
                };
            }
            return _n;
        }

        void Finish()
        {
            if (!_playing) return;
            _playing = false; _n = 0;
            switch (s.stopAction)
            {
                case ParticleSystemStopAction.Disable: gameObject.SetActive(false); break;
                case ParticleSystemStopAction.Destroy: Object.Destroy(gameObject); break;
                case ParticleSystemStopAction.Callback: SendMessage("OnParticleSystemStopped"); break;
            }
        }

        // ── modules ───────────────────────────────────────────────────────────
        public readonly struct MainModule
        {
            readonly State s;
            internal MainModule(ParticleSystem ps) { s = ps.s; }
            public float duration { get => s.duration; set => s.duration = value; }
            public bool loop { get => s.loop; set => s.loop = value; }
            public bool prewarm { get => s.prewarm; set => s.prewarm = value; }
            public bool playOnAwake { get => s.playOnAwake; set => s.playOnAwake = value; }
            public MinMaxCurve startDelay { get => s.startDelay; set => s.startDelay = value; }
            public float startDelayMultiplier { get => s.startDelayMultiplier; set => s.startDelayMultiplier = value; }
            public MinMaxCurve startLifetime { get => s.startLifetime; set => s.startLifetime = value; }
            public float startLifetimeMultiplier { get => s.startLifetime.curveMultiplier; set { var c = s.startLifetime; c.curveMultiplier = value; s.startLifetime = c; } }
            public MinMaxCurve startSpeed { get => s.startSpeed; set => s.startSpeed = value; }
            public float startSpeedMultiplier { get => s.startSpeed.curveMultiplier; set { var c = s.startSpeed; c.curveMultiplier = value; s.startSpeed = c; } }
            public bool startSize3D { get => s.startSize3D; set => s.startSize3D = value; }
            public MinMaxCurve startSize { get => s.startSize; set => s.startSize = value; }
            public float startSizeMultiplier { get => s.startSize.curveMultiplier; set { var c = s.startSize; c.curveMultiplier = value; s.startSize = c; } }
            public MinMaxCurve startSizeX { get => s.startSizeX; set => s.startSizeX = value; }
            public MinMaxCurve startSizeY { get => s.startSizeY; set => s.startSizeY = value; }
            public MinMaxCurve startSizeZ { get => s.startSizeZ; set => s.startSizeZ = value; }
            public bool startRotation3D { get => s.startRotation3D; set => s.startRotation3D = value; }
            public MinMaxCurve startRotation { get => s.startRotation; set => s.startRotation = value; }
            public MinMaxGradient startColor { get => s.startColor; set => s.startColor = value; }
            public MinMaxCurve gravityModifier { get => s.gravityModifier; set => s.gravityModifier = value; }
            public float gravityModifierMultiplier { get => s.gravityModifierMultiplier; set => s.gravityModifierMultiplier = value; }
            public ParticleSystemSimulationSpace simulationSpace { get => s.simulationSpace; set => s.simulationSpace = value; }
            public Transform customSimulationSpace { get => s.customSimulationSpace; set => s.customSimulationSpace = value; }
            public float simulationSpeed { get => s.simulationSpeed; set => s.simulationSpeed = value; }
            public bool useUnscaledTime { get => s.useUnscaledTime; set => s.useUnscaledTime = value; }
            public ParticleSystemScalingMode scalingMode { get => s.scalingMode; set => s.scalingMode = value; }
            public int maxParticles { get => s.maxParticles; set => s.maxParticles = value; }
            public ParticleSystemStopAction stopAction { get => s.stopAction; set => s.stopAction = value; }
            public ParticleSystemEmitterVelocityMode emitterVelocityMode { get => s.emitterVelocityMode; set => s.emitterVelocityMode = value; }
        }

        public readonly struct EmissionModule
        {
            readonly State s;
            internal EmissionModule(ParticleSystem ps) { s = ps.s; }
            public bool enabled { get => s.emissionEnabled; set => s.emissionEnabled = value; }
            public MinMaxCurve rateOverTime { get => s.rateOverTime; set => s.rateOverTime = value; }
            public float rateOverTimeMultiplier { get => s.rateOverTime.constantMax; set { var c = s.rateOverTime; c.constantMax = value; if (c.mode == ParticleSystemCurveMode.Constant) c.constantMin = value; s.rateOverTime = c; } }
            public MinMaxCurve rateOverDistance { get => s.rateOverDistance; set => s.rateOverDistance = value; }
            public float rateOverDistanceMultiplier { get => s.rateOverDistance.constantMax; set { var c = s.rateOverDistance; c.constantMax = value; s.rateOverDistance = c; } }
            public int burstCount { get => s.bursts.Count; set { while (s.bursts.Count < value) s.bursts.Add(new Burst(0f, 30f)); while (s.bursts.Count > value) s.bursts.RemoveAt(s.bursts.Count - 1); } }
            public void SetBursts(Burst[] bursts) { s.bursts.Clear(); if (bursts != null) s.bursts.AddRange(bursts); }
            public void SetBursts(Burst[] bursts, int size) { s.bursts.Clear(); for (int i = 0; i < size && i < bursts.Length; i++) s.bursts.Add(bursts[i]); }
            public int GetBursts(Burst[] bursts) { int n = Math.Min(bursts.Length, s.bursts.Count); for (int i = 0; i < n; i++) bursts[i] = s.bursts[i]; return n; }
            public void SetBurst(int index, Burst burst) => s.bursts[index] = burst;
            public Burst GetBurst(int index) => s.bursts[index];
        }

        public readonly struct ShapeModule
        {
            readonly State s;
            internal ShapeModule(ParticleSystem ps) { s = ps.s; }
            public bool enabled { get => s.shapeEnabled; set => s.shapeEnabled = value; }
            public ParticleSystemShapeType shapeType { get => s.shapeType; set => s.shapeType = value; }
            public float radius { get => s.radius; set => s.radius = value; }
            public float radiusThickness { get => s.radiusThickness; set => s.radiusThickness = value; }
            public float angle { get => s.angle; set => s.angle = value; }
            public float arc { get => s.arc; set => s.arc = value; }
            public float length { get => s.length; set => s.length = value; }
            public float randomDirectionAmount { get => s.randomDirectionAmount; set => s.randomDirectionAmount = value; }
            public float sphericalDirectionAmount { get => s.sphericalDirectionAmount; set => s.sphericalDirectionAmount = value; }
            public Vector3 position { get => s.shapePosition; set => s.shapePosition = value; }
            public Vector3 rotation { get => s.shapeRotation; set => s.shapeRotation = value; }
            public Vector3 scale { get => s.shapeScale; set => s.shapeScale = value; }
            public Vector3 boxThickness { get => s.boxThickness; set => s.boxThickness = value; }
            public float donutRadius { get => s.donutRadius; set => s.donutRadius = value; }
            public Mesh mesh { get => s.shapeMesh; set => s.shapeMesh = value; }
            public MeshRenderer meshRenderer { get => s.shapeMeshRenderer; set => s.shapeMeshRenderer = value; }
            public SkinnedMeshRenderer skinnedMeshRenderer { get => s.shapeSkinnedMeshRenderer; set => s.shapeSkinnedMeshRenderer = value; }
        }

        public readonly struct ColorOverLifetimeModule
        {
            readonly State s;
            internal ColorOverLifetimeModule(ParticleSystem ps) { s = ps.s; }
            public bool enabled { get => s.colorOverLifetimeEnabled; set => s.colorOverLifetimeEnabled = value; }
            public MinMaxGradient color { get => s.colorOverLifetime; set => s.colorOverLifetime = value; }
        }

        public readonly struct SizeOverLifetimeModule
        {
            readonly State s;
            internal SizeOverLifetimeModule(ParticleSystem ps) { s = ps.s; }
            public bool enabled { get => s.sizeOverLifetimeEnabled; set => s.sizeOverLifetimeEnabled = value; }
            public MinMaxCurve size { get => s.sizeOverLifetime; set => s.sizeOverLifetime = value; }
            public float sizeMultiplier { get => s.sizeMultiplier; set => s.sizeMultiplier = value; }
            public bool separateAxes { get => s.sizeSeparateAxes; set => s.sizeSeparateAxes = value; }
            public MinMaxCurve x { get => s.sizeX; set => s.sizeX = value; }
            public MinMaxCurve y { get => s.sizeY; set => s.sizeY = value; }
            public MinMaxCurve z { get => s.sizeZ; set => s.sizeZ = value; }
        }

        public readonly struct VelocityOverLifetimeModule
        {
            readonly State s;
            internal VelocityOverLifetimeModule(ParticleSystem ps) { s = ps.s; }
            public bool enabled { get => s.velocityEnabled; set => s.velocityEnabled = value; }
            public MinMaxCurve x { get => s.velX; set => s.velX = value; }
            public MinMaxCurve y { get => s.velY; set => s.velY = value; }
            public MinMaxCurve z { get => s.velZ; set => s.velZ = value; }
            public float xMultiplier { get => s.velX.constantMax; set => s.velX = value; }
            public float yMultiplier { get => s.velY.constantMax; set => s.velY = value; }
            public float zMultiplier { get => s.velZ.constantMax; set => s.velZ = value; }
            public MinMaxCurve orbitalX { get => s.orbitalX; set => s.orbitalX = value; }
            public MinMaxCurve orbitalY { get => s.orbitalY; set => s.orbitalY = value; }
            public MinMaxCurve orbitalZ { get => s.orbitalZ; set => s.orbitalZ = value; }
            public MinMaxCurve radial { get => s.radial; set => s.radial = value; }
            public MinMaxCurve speedModifier { get => s.speedModifier; set => s.speedModifier = value; }
            public float speedModifierMultiplier { get => s.speedModifier.constantMax; set => s.speedModifier = value; }
            public ParticleSystemSimulationSpace space { get => s.velSpace; set => s.velSpace = value; }
        }

        public readonly struct NoiseModule
        {
            readonly State s;
            internal NoiseModule(ParticleSystem ps) { s = ps.s; }
            public bool enabled { get => s.noiseEnabled; set => s.noiseEnabled = value; }
            public bool separateAxes { get => s.noiseSeparateAxes; set => s.noiseSeparateAxes = value; }
            public MinMaxCurve strength { get => s.noiseStrength; set => s.noiseStrength = value; }
            public float strengthMultiplier { get => s.noiseStrength.constantMax; set => s.noiseStrength = value; }
            public float frequency { get => s.frequency; set => s.frequency = value; }
            public MinMaxCurve scrollSpeed { get => s.scrollSpeed; set => s.scrollSpeed = value; }
            public float scrollSpeedMultiplier { get => s.scrollSpeed.constantMax; set => s.scrollSpeed = value; }
            public int octaveCount { get => s.octaveCount; set => s.octaveCount = value; }
            public ParticleSystemNoiseQuality quality { get => s.noiseQuality; set => s.noiseQuality = value; }
            public MinMaxCurve positionAmount { get => s.positionAmount; set => s.positionAmount = value; }
        }

        public readonly struct TrailModule
        {
            readonly State s;
            internal TrailModule(ParticleSystem ps) { s = ps.s; }
            public bool enabled { get => s.trailsEnabled; set => s.trailsEnabled = value; }
            public float ratio { get => s.ratio; set => s.ratio = value; }
            public MinMaxCurve lifetime { get => s.trailLifetime; set => s.trailLifetime = value; }
            public MinMaxCurve widthOverTrail { get => s.widthOverTrail; set => s.widthOverTrail = value; }
            public MinMaxGradient colorOverTrail { get => s.colorOverTrail; set => s.colorOverTrail = value; }
        }

        public readonly struct LimitVelocityOverLifetimeModule
        {
            readonly State s;
            internal LimitVelocityOverLifetimeModule(ParticleSystem ps) { s = ps.s; }
            public bool enabled { get => s.limitVelocityEnabled; set => s.limitVelocityEnabled = value; }
            public MinMaxCurve limit { get => s.limit; set => s.limit = value; }
            public float dampen { get => s.dampen; set => s.dampen = value; }
        }

        public readonly struct RotationOverLifetimeModule
        {
            readonly State s;
            internal RotationOverLifetimeModule(ParticleSystem ps) { s = ps.s; }
            public bool enabled { get => s.rotationOverLifetimeEnabled; set => s.rotationOverLifetimeEnabled = value; }
            public MinMaxCurve z { get => s.rotationZ; set => s.rotationZ = value; }
            public float zMultiplier { get => s.rotationZ.constantMax; set => s.rotationZ = value; }
        }

        /// <summary>The remaining modules the game only toggles.</summary>
        public readonly struct ToggleModule
        {
            readonly State s; readonly int which;
            internal ToggleModule(ParticleSystem ps, int which) { s = ps.s; this.which = which; }
            public bool enabled
            {
                get => which switch { 0 => s.collisionEnabled, 1 => s.textureSheetEnabled, 2 => s.lightsEnabled, 3 => s.subEmittersEnabled, 4 => s.inheritVelocityEnabled, 5 => s.forceOverLifetimeEnabled, 6 => s.externalForcesEnabled, 7 => s.triggerEnabled, _ => s.customDataEnabled };
                set { switch (which) { case 0: s.collisionEnabled = value; break; case 1: s.textureSheetEnabled = value; break; case 2: s.lightsEnabled = value; break; case 3: s.subEmittersEnabled = value; break; case 4: s.inheritVelocityEnabled = value; break; case 5: s.forceOverLifetimeEnabled = value; break; case 6: s.externalForcesEnabled = value; break; case 7: s.triggerEnabled = value; break; default: s.customDataEnabled = value; break; } }
            }
        }
    }

    /// <summary>Renders a ParticleSystem (UnityEngine.ParticleSystemRenderer).</summary>
    public class ParticleSystemRenderer : Renderer
    {
        public ParticleSystemRenderMode renderMode { get; set; }
        public Mesh mesh { get; set; }
        public Material trailMaterial { get; set; }
        public float lengthScale { get; set; } = 2f;
        public float velocityScale { get; set; }
        public float minParticleSize { get; set; }
        public float maxParticleSize { get; set; } = 0.5f;
        public int sortingOrder { get; set; }
    }
}
