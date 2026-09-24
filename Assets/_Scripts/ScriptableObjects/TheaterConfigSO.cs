using System;
using System.IO;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The ONLY tuning surface for theater recording and playback — sample rate, where recordings
    /// land, and which subsystems are recorded at all.
    ///
    /// <para>Zero-wire by default: with no asset at <c>Resources/TheaterConfig</c> the director
    /// runs on these field defaults, so the feature works in a fresh clone with nothing authored.
    /// Create the asset (right-click ▸ Create ▸ ScriptableObjects ▸ Utility ▸ Theater Config) to
    /// point recordings at your own folder or to change the rate.</para>
    ///
    /// <para><b>The toggles are the file size.</b> Each <c>record*</c> flag removes a whole
    /// recorder, and the ones that are not yet implemented are listed here rather than hidden,
    /// because which subsystem costs what is the decision this config exists to expose. Fauna is
    /// the dominant term by an order of magnitude at every population the game actually ships —
    /// it is the one toggle that changes a recording's size category rather than its size.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "TheaterConfig",
        menuName = "ScriptableObjects/Utility/Theater Config", order = 41)]
    public class TheaterConfigSO : ScriptableObject
    {
        /// <summary>Where <see cref="Resolve"/> looks before falling back to code defaults.</summary>
        public const string ResourcePath = "TheaterConfig";

        /// <summary>The folder name every clone's recordings land in, at the repository root.</summary>
        public const string DefaultFolderName = "Recordings";

        [Header("Output")]
        [Tooltip("Folder recordings are written to. Leave EMPTY for the repo's own git-ignored " +
                 "Recordings folder (in a player build, the app's persistent data path). An " +
                 "absolute path is used as given; a relative one hangs off that same default root.")]
        public string outputFolder = string.Empty;

        [Header("Puppets")]
        [Tooltip("OPTIONAL. Drag 'Vessel Prefab Container' here and playback builds ghosts from the " +
                 "real hulls (harvested off the prefab asset - never instantiated, so no controller " +
                 "runs and there is no stray NetworkObject). Leave EMPTY and ghosts are a flat " +
                 "domain-coloured wedge instead, which still states position, heading and roll.")]
        public VesselPrefabContainer vesselPrefabs;

        [Header("Capture")]
        [Tooltip("Vessel poses sampled per second. 30 matches the fidelity a director needs; 20 " +
                 "is visually indistinguishable for orbit and follow shots and costs a third less.")]
        [Range(5f, 60f)] public float vesselSampleHz = 30f;

        [Tooltip("Stop recording at this many minutes so a session left running overnight cannot " +
                 "fill a disk. The recording is KEPT, not discarded - it simply stops growing.")]
        [Range(1f, 120f)] public float maxRecordMinutes = 30f;

        [Header("Subsystems")]
        [Tooltip("Vessel poses. This is all of P0; with it off there is nothing to record.")]
        public bool recordVessels = true;

        [Tooltip("RESERVED for P1. Prism births and deaths - the trail, the arena, every " +
                 "destruction. Events, not samples, so it is far cheaper than it looks: roughly " +
                 "4% of a fauna-heavy recording.")]
        public bool recordPrisms = false;

        [Tooltip("RESERVED for P2. Fauna root poses. THE dominant term - 80-90% of a fauna-heavy " +
                 "recording. Turning this off is what takes a 5 MB file under 1 MB.")]
        public bool recordFauna = false;

        [Tooltip("RESERVED for P2. Projectile LAUNCHES (flight is analytic, so launches are ~5x " +
                 "cheaper than sampling the rounds and exactly as faithful).")]
        public bool recordProjectiles = false;

        [Tooltip("RESERVED for P2. Score, toasts and phase changes - the stream chapter markers " +
                 "are derived from. Costs almost nothing.")]
        public bool recordAnnotations = false;

        [Header("Playback")]
        [Tooltip("Seconds the broadcast camera takes to orbit the recorded action once.")]
        [Range(4f, 120f)] public float orbitSeconds = 40f;

        [Tooltip("How far back the orbit camera sits, as a multiple of the radius of everything " +
                 "it is framing. 1 puts it on the action; 2.5 is a comfortable establishing shot.")]
        [Range(1f, 6f)] public float framingMargin = 2.5f;

        [Tooltip("How high the orbit camera rides, as a fraction of its distance.")]
        [Range(-1f, 1f)] public float orbitElevation = 0.35f;

        [Tooltip("Distance a FOLLOW shot sits behind the vessel it is following, as a multiple of " +
                 "that hull's own measured radius - so one number frames every ship in the fleet.")]
        [Range(2f, 40f)] public float followDistance = 9f;

        [Tooltip("Ghosts wear their ship's OWN authored materials instead of a flat domain fill. " +
                 "Off by default: a vessel's real materials are dark unlit theme shaders that read " +
                 "as a black blob out of their lit context, and the theater's stage is a dark void " +
                 "- the flat fill is what makes four ghosts tellable apart at orbit distance.")]
        public bool liveHullMaterials = false;

        [Header("Stage")]
        [Tooltip("What the recording area is drawn against once the live world is masked off the " +
                 "camera. Dark enough that domain fills carry, not black - a pure black stage and " +
                 "a camera pointed at nothing look identical.")]
        public Color stageBackground = new Color(0.03f, 0.04f, 0.06f, 1f);

        [Header("Free camera")]
        [Tooltip("Top speed of the free camera, in multiples of the framed action's own radius per " +
                 "second - so one number crosses a 200-unit skirmish and a 3,000-unit arena in the " +
                 "same few seconds. The shoulders/Shift/Ctrl gear it 4x and 0.25x.")]
        [Range(0.05f, 4f)] public float freeCameraSpeed = 0.9f;

        [Tooltip("Degrees per second the free camera turns at full stick. The mouse is a delta and " +
                 "is not scaled by this.")]
        [Range(20f, 400f)] public float freeCameraLookSpeed = 140f;

        /// <summary>
        /// The config in use — the authored asset if there is one, otherwise a code-default
        /// instance minted once. Never returns null, so no caller needs a null branch.
        /// </summary>
        public static TheaterConfigSO Resolve()
        {
            var asset = Resources.Load<TheaterConfigSO>(ResourcePath);
            if (asset != null) return asset;

            if (_fallback == null)
            {
                _fallback = CreateInstance<TheaterConfigSO>();
                _fallback.name = "TheaterConfig (defaults)";
            }
            return _fallback;
        }

        static TheaterConfigSO _fallback;

        /// <summary>
        /// The folder recordings are written to, as an absolute path. Never throws — a path the OS
        /// rejects falls back to the default root, because losing a recording to an unwritable
        /// folder is worse than putting it somewhere unexpected.
        /// </summary>
        public string ResolveOutputFolder()
        {
            string root = DefaultRoot();
            if (string.IsNullOrWhiteSpace(outputFolder)) return root;

            string expanded = Environment.ExpandEnvironmentVariables(outputFolder.Trim());
            try
            {
                return Path.IsPathRooted(expanded) ? expanded : Path.Combine(root, expanded);
            }
            catch (ArgumentException)
            {
                return root;
            }
        }

        /// <summary>
        /// Where recordings go when nothing is authored: <c>&lt;repo&gt;/Recordings</c>, resolved
        /// per machine off <c>Application.dataPath</c>'s parent — the same default, and the same
        /// reasoning, as the screenshot director's.
        ///
        /// <para><b>Recordings are never pushed.</b> <c>/Recordings</c> is already in
        /// <c>.gitignore</c>, so a recording is private to the machine that made it. Do not remove
        /// that ignore rule without moving this default with it.</para>
        /// </summary>
        static string DefaultRoot()
        {
            if (Application.isEditor)
            {
                try
                {
                    var repoRoot = Directory.GetParent(Application.dataPath);
                    if (repoRoot != null) return Path.Combine(repoRoot.FullName, DefaultFolderName);
                }
                catch (Exception)
                {
                    // An unexpected dataPath shape; the persistent path always exists.
                }
            }
            return Path.Combine(Application.persistentDataPath, DefaultFolderName);
        }
    }
}
