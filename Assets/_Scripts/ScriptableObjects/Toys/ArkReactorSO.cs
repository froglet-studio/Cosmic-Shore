using FMODUnity;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The Ark's REACTOR (Garrett, 2026-10-28): the omni crystal aboard powers the Ark's prism creation. An Ark is the one
    /// body that can contain an omni crystal and harvest its power instead of morphing it into something else, so it is
    /// the one body whose hull REGROWS: each destroyed or taken hull prism grows back into its own slot at a FIXED RATE,
    /// one prism a beat, nearest the reactor first. The crystal is always one size: nothing about the reactor resizes it.
    ///
    /// The mass law's fence (Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md, the fourth sign-off): only an Ark, only into its
    /// own empty slots, at the fixed rate, each prism growing in; a fallen Ark never regrows.
    /// </summary>
    [CreateAssetMenu(fileName = "ArkReactor", menuName = "ScriptableObjects/Toys/Ark Reactor")]
    public sealed class ArkReactorSO : ScriptableObject
    {
        [Header("Regrowth")]
        [SerializeField, Min(0.05f), Tooltip("Seconds per regrown hull prism: the fixed rate. One prism a beat while any slot is " +
                                             "empty, each growing in on the prism's own clock. A beat with nothing to regrow is " +
                                             "not saved up.")]
        float regrowSeconds = 2f;

        [Header("Audio (shipped empty: an unwired slot is a visible TODO)")]
        [SerializeField, Tooltip("One hull prism regrowing into its socket.")]
        EventReference regrowSound;

        public float RegrowSeconds => regrowSeconds;
        public EventReference RegrowSound => regrowSound;

        /// <summary>A runtime copy of this reactor at another fixed rate (an Ark escort's difficulty), its sound kept.
        /// The caller owns the copy and destroys it.</summary>
        public ArkReactorSO WithRegrowSeconds(float seconds)
        {
            var copy = Instantiate(this);
            copy.name = $"{name} ({seconds:0.##}s)";
            copy.regrowSeconds = Mathf.Max(0.05f, seconds);
            return copy;
        }
    }
}
