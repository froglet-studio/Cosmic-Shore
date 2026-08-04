using UnityEngine;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The CPU half of the camera↔vessel prism occlusion corridor: prisms sitting between
    /// the player's camera and the player's vessel dissolve so the ship is never hidden
    /// (Docs/PRISM_ANIMATION.md §5 C1).
    ///
    /// It publishes exactly TWO global shader uniforms once per frame and does nothing
    /// else. There is no per-prism work of any kind — no trigger volumes, no material
    /// swaps, no per-instance overrides, no tracking dictionary. The corridor test runs
    /// per fragment in <c>PrismOcclusionCorridor.hlsl</c>, wired into BlockGraph.
    ///
    /// This is the sibling of <see cref="PrismClock"/> and it earns its per-frame write
    /// the same way: occlusion is camera-relative LIVE data (PRISM_ANIMATION.md §1,
    /// "animation vs. live gameplay data"), so it can never be a per-prism stamp — but a
    /// single O(1) global uniform write is explicitly the law's allowed shape, because it
    /// is not per-prism.
    ///
    /// History: the retired <c>ClearPrisms</c> component did the opposite of all of this.
    /// It grew a physics capsule per vessel, swapped each entered prism's sharedMaterial
    /// to the team transparent material on OnTriggerEnter, and wrote a MaterialPropertyBlock
    /// per tracked prism per physics tick. It also could not work: prisms draw through
    /// companion entities (instanced rendering is ON), so a GameObject MaterialPropertyBlock
    /// never reaches the batch — and its capsule sat on layer TrailBlockOcclusion while
    /// prisms sit on Default, which the collision matrix does not pair, so the triggers
    /// never fired either.
    ///
    /// Only ONE target is ever published — the local player's vessel. The camera end of
    /// the corridor is read on the GPU (<c>_WorldSpaceCameraPos</c>), so it is always
    /// exactly the camera that is rendering and never needs to be resolved or published.
    /// </summary>
    public static class PrismOcclusionCorridor
    {
        static readonly int TargetId = Shader.PropertyToID("_PrismOcclusionTarget");
        static readonly int ParamsId = Shader.PropertyToID("_PrismOcclusionParams");

        const string ConfigResourcePath = "PrismOcclusionConfig";

        static Transform _target;
        static PrismOcclusionConfigSO _config;
        static bool _configResolved;
        static bool _publishedActive;

        /// <summary>The vessel the corridor currently opens onto, or null when it is off.</summary>
        public static Transform Target => _target;

        /// <summary>True while the corridor is publishing a live capsule.</summary>
        public static bool IsActive => _publishedActive;

        /// <summary>
        /// Tuning (radius / feather / core alpha). Falls back to the SO's own defaults when
        /// no <c>Resources/PrismOcclusionConfig</c> asset exists, so the feature works with
        /// no authoring.
        /// </summary>
        public static PrismOcclusionConfigSO Config
        {
            get
            {
                if (!_configResolved)
                {
                    _config = Resources.Load<PrismOcclusionConfigSO>(ConfigResourcePath);
                    if (_config == null)
                        _config = ScriptableObject.CreateInstance<PrismOcclusionConfigSO>();
                    _configResolved = true;
                }
                return _config;
            }
        }

        /// <summary>
        /// Point the corridor at the thing the local player's camera is watching — their
        /// vessel. Called from <c>CameraManager</c> wherever a follow target is installed,
        /// which is the one place in the codebase that already means "this is the local
        /// player's ship". Passing null turns the corridor off.
        /// </summary>
        public static void SetTarget(Transform target) => _target = target;

        /// <summary>
        /// Turn the corridor off, but only if <paramref name="target"/> is still the one in
        /// force — so a late teardown from an old vessel cannot cancel a newer one.
        /// </summary>
        public static void ClearTarget(Transform target)
        {
            if (_target == target)
                _target = null;
        }

        /// <summary>Unconditional off (scene teardown, returning to a vessel-less camera).</summary>
        public static void ClearTarget() => _target = null;

        /// <summary>Drop the cached config so the next frame re-reads the asset (editor tooling).</summary>
        public static void InvalidateConfig()
        {
            _config = null;
            _configResolved = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InstallPublisher()
        {
            _target = null;
            // Shader globals survive play-mode exit in the editor, so a stale corridor from
            // the previous session would fade prisms around a vessel that no longer exists.
            // Publish the off state before anything renders.
            PublishOff();

            // HideInHierarchy (NOT HideAndDontSave — that exempts the object from play-mode-exit
            // cleanup), the same pattern PrismClock's publisher uses.
            var go = new GameObject("[PrismOcclusionCorridor]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Publisher>();
        }

        static void PublishOff()
        {
            Shader.SetGlobalVector(TargetId, Vector4.zero);
            Shader.SetGlobalVector(ParamsId, Vector4.zero); // x <= 0 is the shader's "off" sentinel
            _publishedActive = false;
        }

        static void Publish()
        {
            var config = Config;
            bool active = _target != null && _target.gameObject.activeInHierarchy
                          && config.Enabled && config.OuterRadius > 0f;

            if (!active)
            {
                if (_publishedActive)
                    PublishOff();
                return;
            }

            Vector3 p = _target.position;
            Shader.SetGlobalVector(TargetId, new Vector4(p.x, p.y, p.z, 0f));
            Shader.SetGlobalVector(ParamsId, config.PackedParams);
            _publishedActive = true;
        }

        /// <summary>
        /// LateUpdate so the corridor is published after the vessel has moved and after
        /// Cinemachine has posed the camera for this frame.
        /// </summary>
        sealed class Publisher : MonoBehaviour
        {
            void LateUpdate() => Publish();
            void OnDisable() => PublishOff();
        }
    }
}
