using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// The MASTER DEVELOPER UNLOCK — one switch that opens every entitlement in the game:
    /// all vessels, all game modes, every intensity tier, the Vessel Hangar feature, and the
    /// FTUE funnel's arcade restrictions.
    ///
    /// It DEFAULTS ON and is meant to stay on until the FTUE is designed. Progression is
    /// authored (the quest chain, the unlock list, the intensity ladder) but the experience
    /// that teaches it is not, so shipping the locks to the team before then hides the game
    /// from the people building it. Flipping <see cref="AllUnlocked"/> off is how a developer
    /// tests the real progression; when the FTUE is signed off, change
    /// <see cref="DefaultAllUnlocked"/> to false and the gate becomes an opt-in cheat instead
    /// of an opt-out lock.
    ///
    /// WHY IT GATES READS RATHER THAN UNLOCKING THINGS AT STARTUP. The obvious implementation —
    /// walk every SO_Vessel at boot and call Unlock() — mutates authored assets: isLocked is a
    /// serialized field and the Editor writes that mutation back into the .asset, so a session
    /// with the gate on would permanently re-author the shipped lock state. Gating the read
    /// leaves the authored state untouched and makes the switch reversible.
    ///
    /// THE PRICE OF GATING A READ IS THAT IT ALSO REACHES THE WRITE PATH'S GUARDS. Every
    /// intercepted read therefore keeps a RAW twin for the code that MANAGES entitlement —
    /// <see cref="SO_Vessel.IsLockedByEntitlement"/> is the worked example: with the gate on,
    /// IsLocked is false for everything, so VesselUnlockSystem's `if (!vessel.IsLocked) return
    /// false` would refuse every unlock and never persist one. Anything that grants, revokes or
    /// displays ENTITLEMENT reads the raw twin; anything that asks "may the player use this
    /// right now" reads the gated one.
    ///
    /// The six choke points, one per lock surface:
    ///   • SO_Vessel.IsLocked                                  — all vessels
    ///   • GameModeProgressionService.IsGameModeUnlocked       — all game modes
    ///   • GameModeProgressionService.GetMaxUnlockedIntensity  — all intensity tiers
    ///   • GameModeProgressionService.IsVesselHangarUnlocked   — the hangar feature
    ///   • QuestArcadeConstraints.Active                       — a stale, persisted FTUE funnel
    ///   • QuestGraphRunner.TryStart                           — the quest graph itself
    ///
    /// IT STANDS THE QUEST GRAPH DOWN ENTIRELY. Everything the graph can apply is a lock this
    /// gate exists to open — the arcade funnel, the nav-button lock, button interactability —
    /// so a graph that still ran would spend the session re-applying them behind a switch that
    /// says nothing is locked. TryStart is the one choke point both start paths funnel through
    /// (OnClientReady and FTUEEventManager.InitializeFTUE), so NOT STARTING is the whole
    /// implementation: no node runs, nothing needs undoing, and the runner's own teardown has
    /// nothing to clean.
    ///
    /// The consequence to know is that no dialogue, instruction or reward reveal plays either,
    /// so THE FTUE CAN ONLY BE PLAY-TESTED WITH THE GATE OFF. That is the design rather than a
    /// limitation: with every entitlement open there is nothing left for the quest to teach,
    /// and its progression WAITS would pass instantly anyway (QuestWaitForModeUnlockedNode asks
    /// IsGameModeUnlocked, QuestWaitForIntensityNode asks GetMaxUnlockedIntensity, and both are
    /// gated here) — so a running graph would race its own script.
    ///
    /// A constraint set persisted by an EARLIER session is a separate problem and is covered
    /// separately: QuestArcadeConstraints lives in PlayerPrefs and survives a restart, so it
    /// reads this gate too rather than relying on the runner never starting.
    ///
    /// It is deliberately NOT the same switch as <see cref="ProgressionBackendGate"/>: that one
    /// decides whether progression state is PERSISTED to the cloud, this one decides whether it
    /// is ENFORCED. They move independently — a developer may want real locks with local-only
    /// persistence, and the FTUE work needs cloud sync off while the locks stay open.
    /// </summary>
    public static class DeveloperUnlockGate
    {
        /// <summary>
        /// The shipped default. TRUE until the FTUE is designed — see the class summary.
        /// A player who has never touched the toggle gets this, in the Editor and in a build.
        /// </summary>
        public const bool DefaultAllUnlocked = true;

        const string PrefKey = "DEV_ALL_UNLOCKED";

        /// <summary>
        /// Raised when the gate is flipped, so live UI can re-read its lock state. The hangar
        /// grid, the arcade card grid and the configure modal all cache lock state when they
        /// build their rows, so without this a flip would not show until the next navigation.
        /// </summary>
        public static event System.Action OnChanged;

        static bool _loaded;
        static bool _allUnlocked = DefaultAllUnlocked;

        /// <summary>
        /// True while every entitlement is open. Persisted in PlayerPrefs so a developer's
        /// choice survives a domain reload, a play-mode exit and an app restart — and so it
        /// works in an internal build, where there is no Editor window to flip it.
        /// </summary>
        public static bool AllUnlocked
        {
            get
            {
                EnsureLoaded();
                return _allUnlocked;
            }
            set
            {
                EnsureLoaded();
                if (_allUnlocked == value) return;
                _allUnlocked = value;
                PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
                OnChanged?.Invoke();
            }
        }

        /// <summary>
        /// Forgets the developer's choice and returns to <see cref="DefaultAllUnlocked"/>.
        /// </summary>
        public static void ResetToDefault()
        {
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
            _loaded = false;
            EnsureLoaded();
            OnChanged?.Invoke();
        }

        static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _allUnlocked = PlayerPrefs.GetInt(PrefKey, DefaultAllUnlocked ? 1 : 0) == 1;
        }

        /// <summary>
        /// Says once per session that the gate is open. A switch that silently unlocks
        /// everything is indistinguishable from progression being broken, which is an
        /// expensive hour for whoever debugs it next — so it announces itself, loudly enough
        /// to find in a console and quietly enough not to be noise.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AnnounceOnce()
        {
            if (!AllUnlocked) return;
            CSDebug.LogWarning(
                "[DeveloperUnlockGate] ALL ENTITLEMENTS OPEN - every vessel, game mode, intensity " +
                "and the Vessel Hangar are unlocked, and the quest graph is stood down. This is the " +
                "shipped default until the FTUE is designed. Turn it off in the Froglet Toolbox " +
                "(Quest Debug or Vessel Unlock tab) to test real progression or the FTUE itself.");
        }
    }
}
