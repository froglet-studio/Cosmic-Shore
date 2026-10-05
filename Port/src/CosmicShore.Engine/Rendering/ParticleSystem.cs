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
    /// UnityEngine.ParticleSystem. The port simulates emission COUNTS (so particleCount,
    /// isPlaying and stop actions behave) while the particles themselves are drawn by the
    /// render arc. Modules are proxy structs over shared per-system state, as in the
    /// original: <c>var main = ps.main; main.startSize = 2;</c> writes through.
    /// </summary>
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
        float _time, _emitAccumulator, _count;
        bool _playing, _emitting, _paused;

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
        public int particleCount => (int)_count;
        public float time { get => _time; set => _time = value; }
        public float totalTime => _time;
        public uint randomSeed { get; set; }
        public bool useAutoRandomSeed { get; set; } = true;
        public bool proceduralSimulationSupported => true;

        public void Play() => Play(true);
        public void Play(bool withChildren)
        {
            _playing = true; _emitting = true; _paused = false;
            if (withChildren) foreach (var c in Children()) c.Play(false);
        }
        public void Stop() => Stop(true, ParticleSystemStopBehavior.StopEmitting);
        public void Stop(bool withChildren) => Stop(withChildren, ParticleSystemStopBehavior.StopEmitting);
        public void Stop(bool withChildren, ParticleSystemStopBehavior stopBehavior)
        {
            _emitting = false;
            if (stopBehavior == ParticleSystemStopBehavior.StopEmittingAndClear) { _count = 0; Finish(); }
            if (withChildren) foreach (var c in Children()) c.Stop(false, stopBehavior);
        }
        public void Pause(bool withChildren = true) { _paused = true; if (withChildren) foreach (var c in Children()) c.Pause(false); }
        public void Clear(bool withChildren = true) { _count = 0; if (withChildren) foreach (var c in Children()) c.Clear(false); }
        public bool IsAlive(bool withChildren = true) => _playing && (_emitting || _count > 0);
        public void Emit(int count) { _count = Math.Min(s.maxParticles, _count + count); if (!_playing) _playing = true; }
        public void Emit(EmitParams emitParams, int count) => Emit(count);
        public void Simulate(float t, bool withChildren = true, bool restart = true, bool fixedTimeStep = true) { if (restart) { _time = 0; _count = 0; } Step(t); }
        public int GetParticles(Particle[] particles) => 0;
        public int GetParticles(Particle[] particles, int size) => 0;
        public void SetParticles(Particle[] particles, int size) => _count = size;
        public void SetParticles(Particle[] particles) => _count = particles?.Length ?? 0;
        public void TriggerSubEmitter(int subEmitterIndex) { }

        IEnumerable<ParticleSystem> Children()
        {
            foreach (var c in GetComponentsInChildren<ParticleSystem>(true)) if (!ReferenceEquals(c, this)) yield return c;
        }

        void Awake() { if (s.playOnAwake) Play(false); }

        void Update()
        {
            if (!_playing || _paused) return;
            float dt = (s.useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) * s.simulationSpeed;
            Step(dt);
        }

        void Step(float dt)
        {
            _time += dt;
            float life = Math.Max(0.0001f, s.startLifetime.Evaluate(0f, 0.5f));
            _count = Math.Max(0f, _count - _count * Math.Min(1f, dt / life));
            if (_emitting && s.emissionEnabled)
            {
                _emitAccumulator += s.rateOverTime.Evaluate(Math.Clamp(_time / Math.Max(0.0001f, s.duration), 0f, 1f), 0.5f) * dt;
                float whole = MathF.Floor(_emitAccumulator);
                _emitAccumulator -= whole;
                _count = Math.Min(s.maxParticles, _count + whole);
            }
            if (_time >= s.duration)
            {
                if (s.loop) _time %= Math.Max(0.0001f, s.duration);
                else _emitting = false;
            }
            if (!_emitting && _count < 0.5f) Finish();
        }

        void Finish()
        {
            if (!_playing) return;
            _playing = false; _count = 0;
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
