using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Tuning for PRISM MOVEMENT, the fundamental (<c>PrismMotion</c>, Docs/PRISM_MOTION.md): the
    /// three-level LADDER a mover climbs as it moves more prisms.
    ///
    /// <para><b>The ladder.</b> The collision RULE never changes — a moving prism that runs into another
    /// destroys both. What a mover can afford to spend on it does:</para>
    /// <list type="table">
    /// <item><b>Level 1, SHOWCASE</b> (≤ <see cref="ShowcaseMaxMovers"/> moving): exact oriented-box
    /// contact, and each destroyed prism may come apart at a finer DETAIL LEVEL (shatter, glitter) when the
    /// viewer is close and the budget has room (<c>PrismInteractionLodConfigSO</c>, game-wide).</item>
    /// <item><b>Level 2, PLAIN</b> (≤ <see cref="BoxMaxMovers"/>): exact boxes, the ordinary explosion.</item>
    /// <item><b>Level 3, BULK</b> (above): the spheres of the prisms' volumes, in Burst; the ordinary
    /// explosion.</item>
    /// </list>
    /// <para>Each boundary is crossed upward at its value and back downward only at three quarters of it,
    /// so a count hovering at a line does not flip every frame.</para>
    ///
    /// Place the asset at <c>Resources/PrismMotionConfig</c>. With no asset the ladder uses the defaults
    /// below.
    /// </summary>
    [CreateAssetMenu(fileName = "PrismMotionConfig", menuName = "ScriptableObjects/Rendering/Prism Motion Config")]
    public class PrismMotionConfigSO : ScriptableObject
    {
        public const string ResourcePath = "PrismMotionConfig";

        /// <summary>Level 1 ceiling with no asset.</summary>
        public const int DefaultShowcaseMaxMovers = 64;

        /// <summary>Level 2 ceiling with no asset.</summary>
        public const int DefaultBoxMaxMovers = 256;

        [Header("The ladder")]
        [Tooltip("LEVEL 1 ceiling. While a mover moves this many prisms or fewer, its collisions use exact boxes " +
                 "AND may draw the finer destruction detail levels (viewer distance and budget permitting). Above " +
                 "it, level 2: the plain explosion. Back to level 1 at three quarters of it. 0 = never level 1.")]
        [Min(0)]
        [SerializeField] int showcaseMaxMovers = DefaultShowcaseMaxMovers;

        [Tooltip("LEVEL 2 ceiling. Up to this many moving prisms, contact is the exact oriented box (main " +
                 "thread); above it, LEVEL 3: the sphere of each prism's volume, in Burst. Back to boxes at three " +
                 "quarters of it (256 -> 192). 0 = always spheres.")]
        [Min(0)]
        [SerializeField] int boxMaxMovers = DefaultBoxMaxMovers;

        public int ShowcaseMaxMovers => Mathf.Max(0, showcaseMaxMovers);
        public int BoxMaxMovers => Mathf.Max(0, boxMaxMovers);

        /// <summary>The ladder is ordered.</summary>
        public bool IsSane => ShowcaseMaxMovers <= BoxMaxMovers;

        static PrismMotionConfigSO s_instance;
        static bool s_resolved;

        /// <summary><c>Resources/PrismMotionConfig</c>, resolved once and cached; null when absent.</summary>
        public static PrismMotionConfigSO Instance
        {
            get
            {
                if (!s_resolved)
                {
                    s_instance = Resources.Load<PrismMotionConfigSO>(ResourcePath);
                    s_resolved = true;
                }
                return s_instance;
            }
        }

        /// <summary>Editor tooling: forget the cached asset so the next ask re-reads it.</summary>
        public static void InvalidateInstance()
        {
            s_instance = null;
            s_resolved = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => InvalidateInstance();
    }
}
