using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.AI
{
    /// <summary>The Third Eye's three cameras, the Vessel Studio's camera vocabulary (/vessel-studio D16).</summary>
    public enum ThirdEyeCameraMode
    {
        Chase = 0,
        Follow = 1,
        Free = 2,
    }

    /// <summary>What the Free camera does: fly on its own, or orbit the watched hull.</summary>
    public enum ThirdEyeFreeMode
    {
        Fly = 0,
        Orbit = 1,
    }

    /// <summary>
    /// Machine-local settings for <see cref="ThirdEyeWindow"/>. Lives under <c>UserSettings/</c>
    /// (gitignored), per the Docs/TOOLING.md contract: tool config is a real ScriptableObject with
    /// tooltips, and one person's camera taste must not ride the branch.
    /// </summary>
    [FilePath("UserSettings/ThirdEyeSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class ThirdEyeSettings : ScriptableSingleton<ThirdEyeSettings>
    {
        [Header("Camera")]
        [SerializeField, Tooltip("Chase: close behind the watched hull, rolls with it. Follow: wider, world-up. " +
                                 "Free: detached; flies on its own or orbits the watched hull. C cycles them.")]
        ThirdEyeCameraMode cameraMode = ThirdEyeCameraMode.Chase;

        [SerializeField, Tooltip("Fly: drag to look, I J K L or W A S D to move, U O or Q E down / up, Shift fast. " +
                                 "Orbit: drag to turn round the watched hull, wheel to zoom.")]
        ThirdEyeFreeMode freeMode = ThirdEyeFreeMode.Fly;

        [SerializeField, Range(30f, 110f), Tooltip("Vertical field of view of the Third Eye camera, in degrees.")]
        float fieldOfView = 70f;

        [SerializeField, Range(0.01f, 5f), Tooltip("Near clip plane, in world units.")]
        float nearClip = 0.3f;

        [SerializeField, Range(1000f, 100000f), Tooltip("Far clip plane, in world units. Cells are a few thousand units across.")]
        float farClip = 20000f;

        [SerializeField, Range(0.25f, 1f), Tooltip("Render resolution as a fraction of the window's pixels. " +
                                                  "Lower it if the second render costs too much frame time.")]
        float resolutionScale = 1f;

        [Header("Chase")]
        [SerializeField, Range(5f, 200f), Tooltip("How far behind the hull the Chase camera sits.")]
        float chaseDistance = 30f;

        [SerializeField, Range(0f, 80f), Tooltip("How far above the hull (in the hull's own up) the Chase camera sits.")]
        float chaseHeight = 8f;

        [SerializeField, Range(0f, 200f), Tooltip("The Chase camera looks at this point ahead of the nose.")]
        float chaseLookAhead = 40f;

        [SerializeField, Range(0f, 30f), Tooltip("How fast the Chase OFFSET catches up with the hull's turn (per second). " +
                                                "The offset is smoothed, never the position, so a 5x boost does not leave the camera behind. 0 = rigid.")]
        float chaseSmoothing = 8f;

        [Header("Follow")]
        [SerializeField, Range(10f, 400f), Tooltip("How far behind the hull the Follow camera sits.")]
        float followDistance = 90f;

        [SerializeField, Range(0f, 200f), Tooltip("How far above the hull (world up) the Follow camera sits.")]
        float followHeight = 35f;

        [SerializeField, Range(0f, 30f), Tooltip("How fast the Follow offset catches up with the hull's heading (per second). 0 = rigid.")]
        float followSmoothing = 3f;

        [Header("Free")]
        [SerializeField, Range(5f, 2000f), Tooltip("Free-fly speed in units per second. The wheel changes it while flying.")]
        float freeSpeed = 120f;

        [SerializeField, Range(1f, 10f), Tooltip("Free-fly speed multiplier while Shift is held.")]
        float freeFastMultiplier = 4f;

        [SerializeField, Range(5f, 1000f), Tooltip("Orbit distance from the watched hull. The wheel changes it.")]
        float orbitDistance = 80f;

        [SerializeField, Range(0.05f, 1f), Tooltip("Degrees turned per pixel dragged, in Free fly and Orbit.")]
        float lookSensitivity = 0.25f;

        [Header("Overlay")]
        [SerializeField, Tooltip("Draw each AI's aim: a line from the hull to the point it is steering for, coloured by its state, " +
                                 "and a ring on the crystal it is really after.")]
        bool showThinking = true;

        [SerializeField, Tooltip("Label each pilot (name, hull, speed) and pin off-screen pilots to the edge of the view.")]
        bool showLabels = true;

        public ThirdEyeCameraMode CameraMode { get => cameraMode; set => cameraMode = value; }
        public ThirdEyeFreeMode FreeMode { get => freeMode; set => freeMode = value; }
        public float FieldOfView { get => fieldOfView; set => fieldOfView = Mathf.Clamp(value, 30f, 110f); }
        public float NearClip { get => nearClip; set => nearClip = Mathf.Clamp(value, 0.01f, 5f); }
        public float FarClip { get => farClip; set => farClip = Mathf.Clamp(value, 1000f, 100000f); }
        public float ResolutionScale { get => resolutionScale; set => resolutionScale = Mathf.Clamp(value, 0.25f, 1f); }
        public float ChaseDistance { get => chaseDistance; set => chaseDistance = Mathf.Clamp(value, 5f, 200f); }
        public float ChaseHeight { get => chaseHeight; set => chaseHeight = Mathf.Clamp(value, 0f, 80f); }
        public float ChaseLookAhead { get => chaseLookAhead; set => chaseLookAhead = Mathf.Clamp(value, 0f, 200f); }
        public float ChaseSmoothing { get => chaseSmoothing; set => chaseSmoothing = Mathf.Clamp(value, 0f, 30f); }
        public float FollowDistance { get => followDistance; set => followDistance = Mathf.Clamp(value, 10f, 400f); }
        public float FollowHeight { get => followHeight; set => followHeight = Mathf.Clamp(value, 0f, 200f); }
        public float FollowSmoothing { get => followSmoothing; set => followSmoothing = Mathf.Clamp(value, 0f, 30f); }
        public float FreeSpeed { get => freeSpeed; set => freeSpeed = Mathf.Clamp(value, 5f, 2000f); }
        public float FreeFastMultiplier { get => freeFastMultiplier; set => freeFastMultiplier = Mathf.Clamp(value, 1f, 10f); }
        public float OrbitDistance { get => orbitDistance; set => orbitDistance = Mathf.Clamp(value, 5f, 1000f); }
        public float LookSensitivity { get => lookSensitivity; set => lookSensitivity = Mathf.Clamp(value, 0.05f, 1f); }
        public bool ShowThinking { get => showThinking; set => showThinking = value; }
        public bool ShowLabels { get => showLabels; set => showLabels = value; }

        public void SaveNow() => Save(true);
    }
}
