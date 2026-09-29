using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The halo a Manta pilot sees on their OWN planted bombs — the answer to "where are my
    /// bombs and how long have I got?", which is the question the whole Bloomrush loop is
    /// asking between a plant and a crystal.
    ///
    /// <para><b>It is drawn for the planter alone, and that costs no networking.</b> A
    /// <see cref="MantaBomb"/> only exists on the machine that simulates its planter, so a
    /// marker parented to the bomb's carrier is already local by construction; the extra gate
    /// is only that the planter is the LOCAL HUMAN (an AI's bombs are simulated on the host,
    /// and a host who sees every bot's markers is reading someone else's HUD). Same predicate
    /// the two haptic feels use: <c>IsLocalUser &amp;&amp; !AutoPilotEnabled</c>. The target
    /// still gets no indication whatsoever — the spec's silence is intact.</para>
    ///
    /// <para><b>The fuse IS the animation.</b> Colour crosses from the calm tint to the
    /// critical one and the pulse quickens as the fuse burns down, so "cash in now" is read
    /// off the rhythm rather than off a number. A bomb committed to a Kabloom cascade holds
    /// full critical while it waits its turn, which is what makes a cashed board read as
    /// fuses turning into explosions one after another instead of one flat bang.</para>
    ///
    /// <para>Reuses the Echo Sight halo outright (<c>Resources/EchoSightHalo</c>): a
    /// billboard-in-the-vertex-shader additive disc with <c>ZTest Always</c>, so a bomb stays
    /// findable through the reef, holds a constant angular size across the arena, and needs no
    /// per-frame transform write. One shared material, per-renderer MaterialPropertyBlock —
    /// never <c>renderer.material</c>, which would clone a project asset per bomb.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MantaBombMarker : MonoBehaviour
    {
        const string HaloMaterialResourcePath = "EchoSightHalo";

        static readonly int HaloColorId = Shader.PropertyToID("_HaloColor");
        static readonly int HaloIntensityId = Shader.PropertyToID("_Intensity");
        static readonly int HaloRadiusId = Shader.PropertyToID("_Radius");
        static readonly int HaloRingPosId = Shader.PropertyToID("_RingPos");

        static Material s_material;
        static Mesh s_quad;
        static bool s_unavailableReported;

        MantaStingConfigSO _config;
        MantaBomb _bomb;
        MeshRenderer _renderer;
        MaterialPropertyBlock _block;
        float _bloom;              // 0..1 continuity envelope
        bool _fading;
        float _phase;

        /// <summary>
        /// Attaches a marker to <paramref name="carrier"/> for <paramref name="bomb"/>.
        /// Returns null when the marker is switched off, the assets are missing, or the
        /// planter is not the local human — every one of which is a silent, valid no-op.
        /// </summary>
        public static MantaBombMarker Attach(GameObject carrier, MantaBomb bomb,
                                             MantaStingConfigSO config, bool localHumanPlanter)
        {
            if (!carrier || !bomb || config == null || !config.ShowFuseMarker) return null;
            if (!localHumanPlanter) return null;
            if (!TryResolveAssets()) return null;

            // Its own child object: the carrier is someone else's prefab (a rival's vessel, a
            // creature, a plant) and a transient view effect must never look like part of it.
            var go = new GameObject("MantaBombMarker") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(carrier.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var marker = go.AddComponent<MantaBombMarker>();
            marker._config = config;
            marker._bomb = bomb;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = s_quad;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = s_material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            marker._renderer = renderer;

            return marker;
        }

        /// <summary>
        /// The bomb resolved (bloomed, knocked off, carrier died). Fades the marker out and
        /// destroys it — nothing on this platform pops out of existence, view effects included.
        /// </summary>
        public void Retire()
        {
            if (_fading) return;
            _fading = true;
            _bomb = null;                                  // the bomb is already gone
        }

        void Update()
        {
            if (!_renderer) return;

            // _config is non-null for any live marker — Attach refuses to build one without it.
            float fade = Mathf.Max(0.01f, _config.MarkerFadeSeconds);
            _bloom = Mathf.MoveTowards(_bloom, _fading ? 0f : 1f, Time.deltaTime / fade);
            if (_fading && _bloom <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            // How close is this bomb to going off? A cascading bomb reads fully critical for
            // its whole wait — it is about to bloom, and the marker says so.
            float urgency = 1f;
            if (_bomb)
            {
                urgency = _bomb.IsCascading
                    ? 1f
                    : 1f - Mathf.Clamp01(_bomb.FuseRemaining /
                                         Mathf.Max(0.5f, _config.MarkerCriticalSeconds));
            }

            float hz = Mathf.Lerp(_config.MarkerCalmPulseHz, _config.MarkerCriticalPulseHz, urgency);
            _phase += Time.deltaTime * hz;
            // A rounded pulse rather than a strobe: bright on the beat, never fully dark, so
            // the marker is a heartbeat quickening instead of something blinking on and off.
            float pulse = 0.55f + 0.45f * Mathf.Sin(_phase * Mathf.PI * 2f);

            var color = Color.Lerp(_config.MarkerCalmColor, _config.MarkerCriticalColor, urgency);

            _block ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(HaloColorId, color);
            _block.SetFloat(HaloIntensityId, _bloom * pulse);
            _block.SetFloat(HaloRadiusId, _config.MarkerRadius);
            // The ring sits just inside the disc edge, so the glyph reads as a ring around the
            // target rather than a smear over it.
            _block.SetFloat(HaloRingPosId, 0.72f);
            _renderer.SetPropertyBlock(_block);
        }

        static bool TryResolveAssets()
        {
            if (s_material && s_quad) return true;

            if (!s_material)
            {
                // Resources rather than a serialized field: the marker is created at runtime on
                // an object nobody authored, and a Resources-loaded material also keeps the
                // shader out of the build stripper's reach.
                s_material = Resources.Load<Material>(HaloMaterialResourcePath);
                if (!s_material)
                {
                    if (!s_unavailableReported)
                    {
                        s_unavailableReported = true;
                        CSDebug.LogWarning(
                            "[MantaBombMarker] Resources/" + HaloMaterialResourcePath +
                            " is missing, so a Manta pilot cannot see their own planted bombs " +
                            "or read a fuse. Restore the material.");
                    }
                    return false;
                }
            }

            s_quad ??= BuildUnitQuad();
            return s_quad;
        }

        /// <summary>
        /// One quad for every marker in the application. The shader spreads its corners across
        /// the screen from the object origin, so the mesh carries no size and no orientation —
        /// which is exactly why it can be shared.
        /// </summary>
        static Mesh BuildUnitQuad()
        {
            var mesh = new Mesh { name = "MantaBombMarkerQuad", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(new[]
            {
                new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f),
                new Vector3(1f, 1f, 0f), new Vector3(-1f, 1f, 0f),
            });
            mesh.SetUVs(0, new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            // The vertex stage ignores object bounds entirely (it rebuilds the quad in clip
            // space), so the bounds only have to be big enough not to be frustum-culled while
            // the marker is on screen.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
            return mesh;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_material = null;
            s_quad = null;
            s_unavailableReported = false;
        }
    }
}
