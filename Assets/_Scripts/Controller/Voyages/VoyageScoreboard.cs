using System.Collections.Generic;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The end-of-voyage scoreboard (Docs/Voyages/CRYSTAL_RUN.md §4, the Milestone 1 board on Frogma): what the voyage
    /// did, a row per seat, the rewards, and the ways out. A run fills one with numbers only; the words are the authored
    /// <c>VoyageLines</c> the view fills (<see cref="CosmicShore.UI.VoyageScoreboardView"/>). One board is open at a
    /// time; while it is, the voyage scene waits for the pilot's choice before it returns to the menu.
    /// </summary>
    public sealed class VoyageScoreboard
    {
        public enum Kind
        {
            /// <summary>A Crystal Run reached its stop: Venture further or Finish.</summary>
            CrystalRun = 0,
            /// <summary>The Ark escort reached its last world: Venture further (one difficulty up) or Finish.</summary>
            Escort = 1,
            /// <summary>The Ark docked home and everything was banked: one way out.</summary>
            Docked = 2,
            /// <summary>The Ark fell and everything aboard with it: one way out.</summary>
            Lost = 3,
        }

        /// <summary>One seat's row.</summary>
        public sealed class Row
        {
            public string Name;
            public bool You;
            public VesselClassType Vessel;
            public int Crystals;
            public int Kills;
            /// <summary>Creatures felled near the Ark: the hull this seat saved.</summary>
            public int Guarded;
            /// <summary>Filled when the board is shown (<see cref="VoyageRaid.Score"/>).</summary>
            public int Score;
        }

        public Kind Board;
        /// <summary>The goal was met (a Crystal Run) or the Ark came through (an escort).</summary>
        public bool Succeeded;
        /// <summary>The place the voyage reached (a display name).</summary>
        public string To = string.Empty;

        public int Done;
        public int Goal;
        public int Cells;
        public int Difficulty;
        public int Worlds;
        public int Legs;
        public float Seconds;
        /// <summary>The Ark's hull as a share of full, and the breach line it sailed with (0 = none).</summary>
        public float Hull = 1f;
        public float Breach;

        public readonly List<Row> Rows = new();
        /// <summary>Crystals stolen (a Crystal Run) or banked (docked), by element (index = (int)Element).</summary>
        public readonly int[] Crystals = new int[(int)Element.Omni + 1];
        /// <summary>The job's or the escort's pay, owed until the Ark docks home.</summary>
        public int Pay;
        /// <summary>True once docked: nothing is at stake any more.</summary>
        public bool Banked;

        /// <summary>The next run if the squad ventures further: its goal and cells, or the escort's difficulty.</summary>
        public int NextGoal;
        public int NextCells;
        public int NextDifficulty;

        /// <summary>The buttons: null hides one. A board with neither Venture nor Finish shows its one way out (Close).</summary>
        public System.Action Venture;
        public System.Action Finish;
        public System.Action Close;
        /// <summary>A lost Ark's way back in: the same voyage with a fresh Ark (null hides it).</summary>
        public System.Action TryAgain;
        /// <summary>A party friend's board (Docs/Voyages/PARTY.md §5): the host chooses what next, so it offers no choice of
        /// its own, only a way to close it and keep flying; the host's next leg (or the end) reaches the voyage anyway.</summary>
        public bool Following;

        /// <summary>The crystals of every element on the board.</summary>
        public int CrystalTotal
        {
            get
            {
                int n = 0;
                foreach (var c in Crystals) n += c;
                return n;
            }
        }

        // ── The one open board ──────────────────────────────────────────────

        static VoyageScoreboard s_current;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_current = null;
            Changed = null;
        }

        /// <summary>The board on screen, or null.</summary>
        public static VoyageScoreboard Current => s_current;

        /// <summary>True while a board waits for the pilot's choice.</summary>
        public static bool IsOpen => s_current != null;

        /// <summary>A board was shown, or the open one closed (null).</summary>
        public static event System.Action<VoyageScoreboard> Changed;

        /// <summary>Put <paramref name="board"/> on screen (replacing any other).</summary>
        public static void Show(VoyageScoreboard board)
        {
            if (board == null) return;
            if (board.Venture == null && board.Finish == null && board.Close == null) board.Close = () => { };
            var def = CosmicShore.ScriptableObjects.VoyageToyDefinitionSO.Load();
            foreach (var row in board.Rows)
                row.Score = def ? VoyageRaid.Score(row.Crystals, row.Kills, row.Guarded, def.ScorePerCrystal, def.ScorePerKill,
                    def.ScorePerGuard) : 0;
            if (def) VoyageToasts.PlayUi(def.ScoreboardSound);
            s_current = board;
            Changed?.Invoke(board);
        }

        /// <summary>Take the open board down without choosing (the voyage scene is leaving).</summary>
        public static void Hide()
        {
            if (s_current == null) return;
            s_current = null;
            Changed?.Invoke(null);
        }

        /// <summary>The pilot chose: the board closes, then the choice runs (it may show the next board).</summary>
        public static void Choose(System.Action choice)
        {
            if (s_current == null) return;
            Hide();
            choice?.Invoke();
        }
    }

    /// <summary>
    /// What the in-voyage objective box shows (Docs/Voyages/CRYSTAL_RUN.md §3; Frogma Rev 113, Hud and Run): the goal,
    /// the crystal counter with its split by element (a Crystal Run), the route's stops calm or hostile, the Ark's hull
    /// against its breach line, where the Ark is, and the assault in progress. Numbers only; the words are authored
    /// (<c>VoyageLines</c>, filled by <see cref="CosmicShore.UI.VoyageObjectiveText"/>).
    /// </summary>
    public struct VoyageObjective
    {
        public enum Kind
        {
            None = 0,
            CrystalRun = 1,
            SailingHome = 2,
            Escort = 3,
        }

        public Kind Goal;
        /// <summary>Where the voyage ends (a display name): the run's stop, home, the escort's last world.</summary>
        public string To;
        public int Done;
        public int Target;
        public int[] Crystals;
        public float Hull;
        public float Breach;
        /// <summary>The stop the Ark is bound for or in, from 1, and the route's stops (0 while unknown).</summary>
        public int Cell;
        public int Cells;
        public bool Hostile;
        /// <summary>Each stop of the route, held against the squad or calm, in order (null while unknown).</summary>
        public bool[] HostileCells;
        /// <summary>Each stop's name (an escort's worlds); null or an empty entry: the stop is named by its number.</summary>
        public string[] Stops;
        /// <summary>Crystals owed and aboard, at stake until the Ark docks home.</summary>
        public int AtStake;
        /// <summary>The escort's difficulty, 1-9 (0 for a job voyage).</summary>
        public int Difficulty;

        /// <summary>The Ark is inside the stop's membrane; the name of the stop it is bound for or in.</summary>
        public bool Inside;
        public string World;
        /// <summary>The Ark may regrow plates now (inside the nucleus of a world the squad holds).</summary>
        public bool Healing;
        /// <summary>The hull lost a plate in the last few seconds.</summary>
        public bool UnderAttack;
        /// <summary>The assault on the Ark (<see cref="ArkAssaultClock"/>): its phase, the seconds left in it, and the
        /// kills that drive it off.</summary>
        public ArkAssaultPhase Assault;
        public float AssaultSeconds;
        public int AssaultKills;
        public int AssaultKillsNeeded;

        /// <summary>A Crystal Run's Ark is anchored at a cell (P1): the squad raids it until the window ends.</summary>
        public bool Anchored;
        /// <summary>Seconds before the anchored Ark sails.</summary>
        public float AnchorSeconds;
        /// <summary>The window is about to close (the horn has sounded).</summary>
        public bool AnchorClosing;
        /// <summary>Crystals the local pilot carries and has not banked at the Ark (P6).</summary>
        public int Carried;
        /// <summary>The Ark's hull is below its leak line: banked crystals are falling out of the hold.</summary>
        public bool Leaking;
        /// <summary>The hull share below which the hold leaks (for the strip's words).</summary>
        public float LeakLine;
        /// <summary>The goal is met and the pause window offers to complete the run here and set sail.</summary>
        public bool CanComplete;

        /// <summary>Copy the Ark's live state from an escort or a job voyage's <see cref="ArkEscortStatus"/>.</summary>
        public void TakeArk(in ArkEscortStatus s)
        {
            Inside = s.InsideWorld;
            Healing = s.Healing;
            UnderAttack = s.UnderAttack;
            Assault = s.Assault;
            AssaultSeconds = s.AssaultSecondsLeft;
            AssaultKills = s.AssaultKills;
            AssaultKillsNeeded = s.AssaultKillsNeeded;
        }

        /// <summary>Whatever is under way that has an objective to show (null when nothing is).</summary>
        public static IVoyageObjectiveSource Source { get; set; }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Source = null;
    }

    /// <summary>A voyage under way that the objective box can read.</summary>
    public interface IVoyageObjectiveSource
    {
        /// <summary>False when there is nothing to show right now (between jobs, before the Ark is underway).</summary>
        bool TryGetObjective(out VoyageObjective objective);
    }

    /// <summary>
    /// The voyage's toasts (Frogma Rev 113, Compare: picked by Shombith, 2026-11-16) go through the run's one banner path
    /// (<see cref="ArkwayRun.ShowBanner(string,float,ArkwayVoyageHud.Mood,string)"/>): a few seconds each, top centre,
    /// one at a time, a bold line and an optional line under it, an event, a threat or something good. This only adds the
    /// mood's sound (an empty slot on <see cref="CosmicShore.ScriptableObjects.VoyageToyDefinitionSO"/> plays nothing).
    /// The objective box holds what stays true; a toast marks what just changed. The words are authored.
    /// </summary>
    public static class VoyageToasts
    {
        public const float Seconds = 3.5f;

        /// <summary>Raise a toast on <paramref name="sea"/>; an empty line shows nothing (that is how a line is
        /// deleted).</summary>
        public static void Show(ArkwayRun sea, ArkwayVoyageHud.Mood mood, string line, string under = null,
            float seconds = Seconds)
        {
            if (!sea || string.IsNullOrEmpty(line)) return;
            sea.ShowBanner(line, seconds, mood, under);
            var def = CosmicShore.ScriptableObjects.VoyageToyDefinitionSO.Load();
            if (!def) return;
            var sound = mood switch
            {
                ArkwayVoyageHud.Mood.Threat => def.ToastThreatSound,
                ArkwayVoyageHud.Mood.Good => def.ToastGoodSound,
                _ => def.ToastEventSound,
            };
            PlayUi(sound);
        }

        /// <summary>A screen sound: a one-shot at the listener (an empty slot plays nothing).</summary>
        public static void PlayUi(FMODUnity.EventReference sound)
        {
            if (sound.IsNull) return;
            var cam = UnityEngine.Camera.main;
            CosmicShore.Gameplay.Audio.FMODOneShotVolumeHelper.PlaySFXOneShot(sound,
                cam ? cam.transform.position : UnityEngine.Vector3.zero, 1f);
        }
    }
}
