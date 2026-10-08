using System;
using CosmicShore.Content.Serialization;
using CosmicShore.Content.Yaml;
using CosmicShore.Engine;

namespace CosmicShore.Content.Scenes
{
    /// <summary>
    /// A serialized ParticleSystem (class 198) into the engine's modules, through their public
    /// API as game code would write them: the main module (lengthInSec, looping, InitialModule),
    /// emission (rate over time / distance, bursts), shape, colour / size / rotation / velocity over
    /// lifetime, limit velocity and noise. Unity's MinMaxCurve is minMaxState (0 constant, 1 curve,
    /// 2 two curves, 3 two constants), scalar (the constant, or the curve multiplier), minScalar,
    /// maxCurve, minCurve; MinMaxGradient is minMaxState (0 colour, 1 gradient, 2 two colours,
    /// 3 two gradients, 4 random colour), minColor, maxColor, minGradient, maxGradient.
    /// </summary>
    static class ParticleSystemReader
    {
        public static void Apply(ParticleSystem ps, YMap b, Func<YNode, Mesh> mesh)
        {
            var main = ps.main;
            main.duration = b.Float("lengthInSec", 5f);
            main.simulationSpeed = b.Float("simulationSpeed", 1f);
            main.stopAction = (ParticleSystemStopAction)b.Int("stopAction");
            main.loop = b.Int("looping", 1) != 0;
            main.prewarm = b.Int("prewarm") != 0;
            main.playOnAwake = b.Int("playOnAwake", 1) != 0;
            main.useUnscaledTime = b.Int("useUnscaledTime") != 0;
            main.startDelay = Curve(b["startDelay"], 0f);
            // moveWithTransform: 0 local, 1 world, 2 custom (ParticleSystemSimulationSpace).
            main.simulationSpace = (ParticleSystemSimulationSpace)b.Int("moveWithTransform");
            main.scalingMode = (ParticleSystemScalingMode)b.Int("scalingMode", 1);
            main.emitterVelocityMode = (ParticleSystemEmitterVelocityMode)b.Int("emitterVelocityMode");
            ps.useAutoRandomSeed = b.Int("autoRandomSeed", 1) != 0;
            ps.randomSeed = (uint)b.Int("randomSeed");

            if (b["InitialModule"] is YMap init)
            {
                main.startLifetime = Curve(init["startLifetime"], 5f);
                main.startSpeed = Curve(init["startSpeed"], 5f);
                main.startColor = Gradient(init["startColor"]);
                main.startSize3D = init.Int("size3D") != 0;
                main.startSize = Curve(init["startSize"], 1f);
                main.startSizeX = Curve(init["startSize"], 1f);
                main.startSizeY = Curve(init["startSizeY"], 1f);
                main.startSizeZ = Curve(init["startSizeZ"], 1f);
                main.startRotation3D = init.Int("rotation3D") != 0;
                main.startRotation = Curve(init["startRotation"], 0f);
                main.gravityModifier = Curve(init["gravityModifier"], 0f);
                main.maxParticles = init.Int("maxNumParticles", 1000);
            }

            if (b["EmissionModule"] is YMap em)
            {
                var emission = ps.emission;
                emission.enabled = em.Int("enabled", 1) != 0;
                emission.rateOverTime = Curve(em["rateOverTime"], 10f);
                emission.rateOverDistance = Curve(em["rateOverDistance"], 0f);
                var bursts = em["m_Bursts"]?.Items;
                if (bursts != null)
                {
                    var list = new ParticleSystem.Burst[bursts.Count];
                    for (int i = 0; i < bursts.Count; i++)
                    {
                        var bu = bursts[i] as YMap;
                        list[i] = new ParticleSystem.Burst(bu?.Float("time") ?? 0f, Curve(bu?["countCurve"], 30f))
                        {
                            cycleCount = bu?.Int("cycleCount", 1) ?? 1,
                            repeatInterval = bu?.Float("repeatInterval", 0.01f) ?? 0.01f,
                            probability = bu?.Float("probability", 1f) ?? 1f,
                        };
                    }
                    emission.SetBursts(list);
                }
            }

            if (b["ShapeModule"] is YMap sh)
            {
                var shape = ps.shape;
                shape.enabled = sh.Int("enabled", 1) != 0;
                shape.shapeType = (ParticleSystemShapeType)sh.Int("type", 4);
                shape.angle = sh.Float("angle", 25f);
                shape.length = sh.Float("length", 5f);
                shape.boxThickness = V3(sh["boxThickness"], 0f);
                shape.radiusThickness = sh.Float("radiusThickness", 1f);
                shape.donutRadius = sh.Float("donutRadius", 0.2f);
                shape.position = V3(sh["m_Position"], 0f);
                shape.rotation = V3(sh["m_Rotation"], 0f);
                shape.scale = V3(sh["m_Scale"], 1f);
                shape.randomDirectionAmount = sh.Float("randomDirectionAmount");
                shape.sphericalDirectionAmount = sh.Float("sphericalDirectionAmount");
                // radius and arc are MultiModeParameters: { value, mode, spread, speed }.
                shape.radius = (sh["radius"] as YMap)?.Float("value", 1f) ?? 1f;
                shape.arc = (sh["arc"] as YMap)?.Float("value", 360f) ?? 360f;
                if (sh["m_Mesh"] != null) shape.mesh = mesh(sh["m_Mesh"]);
            }

            if (b["ColorModule"] is YMap col)
            {
                var c = ps.colorOverLifetime;
                c.enabled = col.Int("enabled") != 0;
                c.color = Gradient(col["gradient"]);
            }
            if (b["SizeModule"] is YMap size)
            {
                var s = ps.sizeOverLifetime;
                s.enabled = size.Int("enabled") != 0;
                s.separateAxes = size.Int("separateAxes") != 0;
                s.size = Curve(size["curve"], 1f);
                s.x = Curve(size["curve"], 1f);
                s.y = Curve(size["y"], 1f);
                s.z = Curve(size["z"], 1f);
            }
            if (b["RotationModule"] is YMap rot)
            {
                var r = ps.rotationOverLifetime;
                r.enabled = rot.Int("enabled") != 0;
                r.z = Curve(rot["curve"], 0f);
            }
            if (b["VelocityModule"] is YMap vel)
            {
                var v = ps.velocityOverLifetime;
                v.enabled = vel.Int("enabled") != 0;
                v.x = Curve(vel["x"], 0f);
                v.y = Curve(vel["y"], 0f);
                v.z = Curve(vel["z"], 0f);
                v.orbitalX = Curve(vel["orbitalX"], 0f);
                v.orbitalY = Curve(vel["orbitalY"], 0f);
                v.orbitalZ = Curve(vel["orbitalZ"], 0f);
                v.radial = Curve(vel["radial"], 0f);
                v.speedModifier = Curve(vel["speedModifier"], 1f);
                v.space = vel.Int("inWorldSpace") != 0 ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            }
            if (b["ClampVelocityModule"] is YMap lim)
            {
                var l = ps.limitVelocityOverLifetime;
                l.enabled = lim.Int("enabled") != 0;
                l.limit = Curve(lim["magnitude"], 1f);
                l.dampen = lim.Float("dampen", 1f);
            }
            if (b["NoiseModule"] is YMap noise)
            {
                var n = ps.noise;
                n.enabled = noise.Int("enabled") != 0;
                n.strength = Curve(noise["strength"], 1f);
                n.frequency = noise.Float("frequency", 0.5f);
                n.scrollSpeed = Curve(noise["scrollSpeed"], 0f);
                n.octaveCount = noise.Int("octaves", 1);
                n.positionAmount = Curve(noise["positionAmount"], 1f);
            }
            var toggles = new (string Key, ParticleSystem.ToggleModule Module)[]
            {
                ("CollisionModule", ps.collision), ("UVModule", ps.textureSheetAnimation), ("LightsModule", ps.lights),
                ("SubModule", ps.subEmitters), ("InheritVelocityModule", ps.inheritVelocity), ("ForceModule", ps.forceOverLifetime),
                ("ExternalForcesModule", ps.externalForces), ("TriggerModule", ps.trigger), ("CustomDataModule", ps.customData),
            };
            foreach (var (key, module) in toggles)
                if (b[key] is YMap m) { var t = module; t.enabled = m.Int("enabled") != 0; }
            if (b["TrailModule"] is YMap trail) { var t = ps.trails; t.enabled = trail.Int("enabled") != 0; }
        }

        public static void ApplyRenderer(ParticleSystemRenderer r, YMap b, Func<YNode, Mesh> mesh)
        {
            r.renderMode = (ParticleSystemRenderMode)b.Int("m_RenderMode");
            r.lengthScale = b.Float("m_LengthScale", 2f);
            r.velocityScale = b.Float("m_VelocityScale");
            r.minParticleSize = b.Float("m_MinParticleSize");
            r.maxParticleSize = b.Float("m_MaxParticleSize", 0.5f);
            if (b["m_Mesh"] != null) r.mesh = mesh(b["m_Mesh"]);
        }

        static Vector3 V3(YNode n, float d) => n is YMap m ? new Vector3(m.Float("x", d), m.Float("y", d), m.Float("z", d)) : new Vector3(d, d, d);

        /// <summary>A serialized MinMaxCurve; <paramref name="fallback"/> when the node is missing.</summary>
        public static ParticleSystem.MinMaxCurve Curve(YNode node, float fallback)
        {
            if (node is not YMap m) return new ParticleSystem.MinMaxCurve(fallback);
            int state = m.Int("minMaxState");
            float scalar = m.Float("scalar", fallback), minScalar = m.Float("minScalar", scalar);
            return state switch
            {
                1 => new ParticleSystem.MinMaxCurve(scalar, SerializedReader.ReadCurve(m["maxCurve"])),
                2 => new ParticleSystem.MinMaxCurve(scalar, SerializedReader.ReadCurve(m["minCurve"]), SerializedReader.ReadCurve(m["maxCurve"])),
                3 => new ParticleSystem.MinMaxCurve(minScalar, scalar),
                _ => new ParticleSystem.MinMaxCurve(scalar),
            };
        }

        /// <summary>A serialized MinMaxGradient (white when missing).</summary>
        public static ParticleSystem.MinMaxGradient Gradient(YNode node)
        {
            if (node is not YMap m) return new ParticleSystem.MinMaxGradient(Color.white);
            Color C(string key) => m[key] is YMap c ? new Color(c.Float("r", 1f), c.Float("g", 1f), c.Float("b", 1f), c.Float("a", 1f)) : Color.white;
            return m.Int("minMaxState") switch
            {
                1 => new ParticleSystem.MinMaxGradient(SerializedReader.ReadGradient(m["maxGradient"])),
                2 => new ParticleSystem.MinMaxGradient(C("minColor"), C("maxColor")),
                3 => new ParticleSystem.MinMaxGradient(SerializedReader.ReadGradient(m["minGradient"]), SerializedReader.ReadGradient(m["maxGradient"])),
                4 => new ParticleSystem.MinMaxGradient(SerializedReader.ReadGradient(m["maxGradient"])) { mode = ParticleSystemGradientMode.RandomColor },
                _ => new ParticleSystem.MinMaxGradient(C("maxColor")),
            };
        }
    }
}
