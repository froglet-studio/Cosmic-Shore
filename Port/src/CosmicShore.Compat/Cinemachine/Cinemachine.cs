using System;
using System.Collections.Generic;
using CosmicShore.Engine;

// Cinemachine 3 (Unity.Cinemachine — kept under its original namespace by the live-src sync).
//
// The game's gameplay and menu cameras are plain-transform rigs (CustomCameraController /
// MainMenuCameraController); Cinemachine survives only in tool scenes and as a couple of
// serialized references. So this is a STATE shim: virtual cameras register while enabled, the
// brain reports the live one by the original's rule (highest priority, ties to the most recently
// activated), and targets/lens are data. Nothing moves a Camera.
namespace Unity.Cinemachine
{
    /// <summary>What the brain and mixers see of a virtual camera.</summary>
    public interface ICinemachineCamera
    {
        string Name { get; }
        bool IsValid { get; }
    }

    /// <summary>A camera's priority, optionally disabled (then it counts as 0).</summary>
    [Serializable]
    public struct PrioritySettings
    {
        public bool Enabled;
        [SerializeField] int m_Value;

        public int Value
        {
            get => Enabled ? m_Value : 0;
            set { m_Value = value; Enabled = true; }
        }

        public static implicit operator int(PrioritySettings p) => p.Value;
        public static implicit operator PrioritySettings(int value) => new PrioritySettings { Enabled = true, m_Value = value };
    }

    /// <summary>Lens parameters a virtual camera would push onto the output Camera.</summary>
    [Serializable]
    public struct LensSettings
    {
        public float FieldOfView;
        public float OrthographicSize;
        public float NearClipPlane;
        public float FarClipPlane;
        public float Dutch;

        public static LensSettings Default => new LensSettings
        {
            FieldOfView = 40f,
            OrthographicSize = 10f,
            NearClipPlane = 0.1f,
            FarClipPlane = 5000f,
            Dutch = 0f,
        };
    }

    /// <summary>What a <see cref="CinemachineCamera"/> tracks and looks at.</summary>
    [Serializable]
    public struct CameraTarget
    {
        public Transform TrackingTarget;
        public Transform LookAtTarget;
        public bool CustomLookAtTarget;
    }

    /// <summary>Registry of enabled virtual cameras (the original's CinemachineCore bookkeeping).</summary>
    public static class CinemachineCore
    {
        static readonly List<CinemachineVirtualCameraBase> s_Active = new();
        static int s_ActivationStamp;

        public static ICinemachineCamera SoloCamera { get; set; }

        public static int VirtualCameraCount => s_Active.Count;
        public static CinemachineVirtualCameraBase GetVirtualCamera(int index) => s_Active[index];

        internal static void Register(CinemachineVirtualCameraBase vcam)
        {
            s_Active.Remove(vcam);
            vcam.ActivationStamp = ++s_ActivationStamp;
            s_Active.Add(vcam);
        }

        internal static void Unregister(CinemachineVirtualCameraBase vcam) => s_Active.Remove(vcam);

        /// <summary>The camera a brain shows: the solo camera if set, else highest priority, ties to the latest activated.</summary>
        public static CinemachineVirtualCameraBase GetLiveCamera()
        {
            if (SoloCamera is CinemachineVirtualCameraBase solo && solo) return solo;
            CinemachineVirtualCameraBase best = null;
            foreach (var v in s_Active)
            {
                if (!v || !v.isActiveAndEnabled) continue;
                if (best == null || v.Priority.Value > best.Priority.Value ||
                    (v.Priority.Value == best.Priority.Value && v.ActivationStamp > best.ActivationStamp))
                    best = v;
            }
            return best;
        }

        public static bool IsLive(ICinemachineCamera vcam) => vcam != null && ReferenceEquals(GetLiveCamera(), vcam);
    }

    /// <summary>Base of every Cinemachine virtual camera.</summary>
    public abstract class CinemachineVirtualCameraBase : MonoBehaviour, ICinemachineCamera
    {
        [SerializeField] public PrioritySettings Priority;
        [SerializeField] public int OutputChannel = 1;

        internal int ActivationStamp;

        public string Name => name;
        public virtual bool IsValid => this;
        public bool IsLive => CinemachineCore.IsLive(this);
        public bool PreviousStateIsValid { get; set; }

        public abstract Transform Follow { get; set; }
        public abstract Transform LookAt { get; set; }

        protected virtual void OnEnable() => CinemachineCore.Register(this);
        protected virtual void OnDisable() => CinemachineCore.Unregister(this);
        protected virtual void OnDestroy() => CinemachineCore.Unregister(this);

        /// <summary>Makes this camera the most recently activated at its priority.</summary>
        public void Prioritize()
        {
            if (isActiveAndEnabled) CinemachineCore.Register(this);
        }

        public virtual void OnTargetObjectWarped(Transform target, Vector3 positionDelta) { }
        public virtual void ForceCameraPosition(Vector3 pos, Quaternion rot) { transform.position = pos; transform.rotation = rot; }
        public virtual void InternalUpdateCameraState(Vector3 worldUp, float deltaTime) { }
    }

    /// <summary>The CM3 camera: a target, a lens, and (in the original) procedural components.</summary>
    public class CinemachineCamera : CinemachineVirtualCameraBase
    {
        [SerializeField] public CameraTarget Target;
        [SerializeField] public LensSettings Lens = LensSettings.Default;

        public override Transform Follow
        {
            get => Target.TrackingTarget;
            set => Target.TrackingTarget = value;
        }

        public override Transform LookAt
        {
            get => Target.CustomLookAtTarget ? Target.LookAtTarget : Target.TrackingTarget;
            set { Target.CustomLookAtTarget = value != Target.TrackingTarget; Target.LookAtTarget = value; }
        }
    }

    /// <summary>How the brain blends between cameras (data only — the port never blends).</summary>
    [Serializable]
    public struct CinemachineBlendDefinition
    {
        public enum Styles { Cut = 0, EaseInOut = 1, EaseIn = 2, EaseOut = 3, HardIn = 4, HardOut = 5, Linear = 6, Custom = 7 }

        public Styles Style;
        public float Time;

        public CinemachineBlendDefinition(Styles style, float time) { Style = style; Time = time; }
    }

    /// <summary>
    /// Sits on the output Camera and reports which virtual camera is live. Always a cut in the
    /// port (<see cref="IsBlending"/> is false) and it does not drive the Camera's transform.
    /// </summary>
    public class CinemachineBrain : MonoBehaviour
    {
        [SerializeField] public CinemachineBlendDefinition DefaultBlend = new(CinemachineBlendDefinition.Styles.EaseInOut, 2f);
        [SerializeField] public bool IgnoreTimeScale;

        public ICinemachineCamera ActiveVirtualCamera => CinemachineCore.GetLiveCamera();
        public bool IsBlending => false;
        public Camera OutputCamera => GetComponent<Camera>();

        public bool IsLiveChild(ICinemachineCamera vcam) => CinemachineCore.IsLive(vcam);
        public void ManualUpdate() { }
    }
}
