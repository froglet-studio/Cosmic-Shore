using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Where a voyage sets out from: the runner of <c>Voyage.unity</c>, stood by <see cref="VoyageScene"/> with the
    /// voyage's settings (<see cref="VoyageToyDefinitionSO"/>). It sails the job or the Ark escort the app shell's Voyage
    /// window asked for (<see cref="VoyageLaunch"/>) and owns the run (<see cref="VoyageRun"/>, <see cref="ArkEscortRun"/>),
    /// a sibling under the scene's voyage root. Freestyle has no harbour: the two modes are not connected.
    ///
    /// <para>Each job is a voyage of its own (Garrett, 2026-10-28: "the jobs should be the different voyages"): the run
    /// ends when the player leaves the voyage, anchored at the job's destination once it got there, and the Voyage window
    /// then offers the jobs from that spot (<see cref="Board(VoyageToyDefinitionSO, SavedSquad, Domains, out VoyageState)"/>).</para>
    /// </summary>
    public sealed class VoyageHarbour : MonoBehaviour
    {
        static VoyageHarbour s_current;

        VoyageToyDefinitionSO _def;
        ToyContext _context;
        VoyageRun _run;
        ArkEscortRun _escort;
        float _nextListen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_current = null;
            Changed = null;
        }

        /// <summary>The harbour standing now (in the voyage scene), or null.</summary>
        public static VoyageHarbour Current => s_current;

        /// <summary>Raised when a voyage sets out, arrives, or ends, so an open window can redraw.</summary>
        public static event System.Action Changed;

        internal static void RaiseChanged() => Changed?.Invoke();

        public VoyageToyDefinitionSO Definition => _def;

        /// <summary>The map, crew, jobs and lines.</summary>
        public VoyageContentSO Content => _def ? _def.Content : VoyageContentSO.Load();

        /// <summary>True while a voyage is out (sailing, at a stop, or following the host's), an Ark escort included.</summary>
        public bool IsUnderway => _run && _run.IsActive || IsEscorting;

        /// <summary>True while an Ark escort is out.</summary>
        public bool IsEscorting => _escort && _escort.IsActive;

        /// <summary>The Ark escort's difficulty while one is out, else 0.</summary>
        public int EscortDifficulty => IsEscorting ? _escort.Difficulty : 0;

        /// <summary>The voyage the running escort rolled (its worlds and modifiers), or null.</summary>
        public ArkEscortVariety.RolledVoyage EscortVoyage => IsEscorting ? _escort.Voyage : null;

        /// <summary>The running escort's course on the galactic map (location ids, from first), or null.</summary>
        public IReadOnlyList<string> EscortCourse => IsEscorting ? _escort.Course : null;

        /// <summary>The job the running voyage sails, or null.</summary>
        public VoyageJob CurrentJob => _run && _run.IsActive ? _run.CurrentJob : null;

        public static VoyageHarbour Create(VoyageToyDefinitionSO definition, Transform parent, ToyContext context)
        {
            var go = new GameObject("VoyageHarbour");
            go.transform.SetParent(parent, false);
            var harbour = go.AddComponent<VoyageHarbour>();
            harbour._def = definition;
            harbour._context = context;
            // The voyage's own screen-space HUD: the objective box and the end-of-voyage scoreboard.
            go.AddComponent<CosmicShore.UI.VoyageObjectiveBox>().Configure(harbour);
            go.AddComponent<CosmicShore.UI.VoyageScoreboardView>().Configure(harbour);
            ArkEscortRun.Finished += harbour.OnEscortFinished;
            s_current = harbour;
            RaiseChanged();
            return harbour;
        }

        /// <summary>
        /// The jobs on offer where the squad is now: one from each pilot of the active squad, then the way home when
        /// the Ark is not there. <paramref name="state"/> is the galaxy they were drawn from (a new one, unsaved, before
        /// the first voyage). Empty while a voyage is out: its next job is chosen when it ends.
        /// </summary>
        public List<VoyageJob> Board(out VoyageState state)
        {
            state = null;
            if (IsUnderway) return new List<VoyageJob>();
            return Board(_def, VoyageRun.ActiveSquad(_context), LiveDomain(), out state);
        }

        /// <summary>The galaxy as this crew knows it: the running voyage's record, else the saved one (a new one, unsaved,
        /// before the first voyage). Null without content.</summary>
        public VoyageState GalaxyState
        {
            get
            {
                var content = Content;
                if (!content || content.Content == null) return null;
                if (_run && _run.IsActive && _run.State != null) return _run.State;
                return VoyageRun.LoadOrBegin(content.Content, LiveDomain());
            }
        }

        /// <summary>The galaxy as the crew knows it, for the Voyage window: the standing harbour's (the running voyage's
        /// record), else the saved one from the voyage settings (<paramref name="settings"/>, or the default ones). Null
        /// without content.</summary>
        public static VoyageState Galaxy(VoyageToyDefinitionSO settings)
        {
            if (s_current) return s_current.GalaxyState;
            if (!settings) settings = VoyageToyDefinitionSO.Load();
            var content = settings ? settings.Content : VoyageContentSO.Load();
            if (!content || content.Content == null) return null;
            return VoyageRun.LoadOrBegin(content.Content, VoyageRun.LiveDomain(VoyageLaunch.ResolveGameData()));
        }

        /// <summary>The jobs on offer for <paramref name="squad"/> from where the last voyage left the Ark (the Voyage
        /// window's board, read in the menu where no harbour stands).</summary>
        public static List<VoyageJob> Board(VoyageToyDefinitionSO settings, SavedSquad squad, Domains home,
            out VoyageState state)
        {
            state = null;
            var content = settings ? settings.Content : VoyageContentSO.Load();
            if (!content || content.Content == null) return new List<VoyageJob>();
            state = VoyageRun.LoadOrBegin(content.Content, home);
            // The board is the active squad's three pilots, one job each, and nobody else's (Shombith, 2026-11-16).
            return VoyageRules.Board(state, content.Content, VoyageRun.SeatedPilots(squad));
        }

        /// <summary>Set sail on <paramref name="job"/> with <paramref name="squad"/> (null: the active squad): the squad deploys
        /// and the Ark sails for the job's destination.</summary>
        public void SetSail(VoyageJob job, SavedSquad squad = null)
        {
            if (job == null || IsUnderway) return;
            Sail(job, squad);
        }

        /// <summary>
        /// Set sail on the Ark escort at <paramref name="difficulty"/> (1-9) with <paramref name="squad"/> (null: the
        /// active squad): the squad deploys and the Ark sails the escort's worlds. <paramref name="aiLocal"/> hands the
        /// local seat to the AI as well (the escort sim).
        /// </summary>
        public void SetSailEscort(int difficulty, SavedSquad squad = null, bool aiLocal = false)
        {
            if (IsUnderway || !_def || !_def.ArkEscort) return;
            // In a host's session only the host sets sail; a friend sails the host's escort (FollowEscort).
            if (VoyageCourseLink.IsFollower) return;
            SailEscort(difficulty, squad, aiLocal);
        }

        /// <summary>
        /// The host set sail on an escort (Docs/Voyages/PARTY.md): this friend's machine sails the same one, in the seat
        /// the host gave it, from its own lobby seat. A friend still holding at the end of the last escort ends it first.
        /// </summary>
        void FollowEscort(VoyageCourse course)
        {
            if (!_def || !_def.ArkEscort || course == null) return;
            if (_run && _run.IsActive) _run.GiveUp("the host set sail on an escort");
            if (_escort && _escort.IsActive) _escort.GiveUp("the host set sail again");
            StartCoroutine(WhenIdle(() =>
            {
                var squad = VoyageSeatRecord.Get(VoyageSeatRecord.PartyKey) ?? VoyageRun.ActiveSquad(_context);
                SailEscort(course.Difficulty, squad, false, course);
                // The last escort's board (a friend's has no choice to make) gives way to the new escort.
                if (IsEscorting && VoyageScoreboard.IsOpen) VoyageScoreboard.Hide();
            }));
        }

        /// <summary>A toast on whichever voyage is out (nothing when none is).</summary>
        public void Toast(ArkwayVoyageHud.Mood mood, string line)
        {
            if (IsEscorting) _escort.Toast(mood, line);
            else if (_run && _run.IsActive) _run.Toast(mood, line);
        }

        /// <summary>
        /// Try again after a lost Ark (the scoreboard's Try again): once the fallen voyage has ended, sail the job of
        /// <paramref name="templateId"/> the board offers now. False when the board offers none.
        /// </summary>
        public bool TryAgain(string templateId)
        {
            var content = Content ? Content.Content : null;
            if (content == null || string.IsNullOrEmpty(templateId)) return false;
            StartCoroutine(WhenIdle(() =>
            {
                foreach (var job in Board(out _))
                    if (job != null && job.TemplateId == templateId)
                    {
                        SetSail(job);
                        return;
                    }
            }));
            return true;
        }

        /// <summary>Try the Ark escort again at <paramref name="difficulty"/>, once the fallen one has ended.</summary>
        public void TryAgainEscort(int difficulty) =>
            StartCoroutine(WhenIdle(() => SetSailEscort(difficulty, _escortSquad, _escortAiLocal)));

        /// <summary>Run <paramref name="then"/> once no voyage is out (a fall settles before the sea ends), giving up after
        /// a few seconds.</summary>
        System.Collections.IEnumerator WhenIdle(System.Action then)
        {
            float until = Time.unscaledTime + 10f;
            while (IsUnderway && Time.unscaledTime < until) yield return null;
            if (!IsUnderway) then?.Invoke();
        }

        /// <summary>End whatever voyage is out (the player left the voyage scene).</summary>
        public void GiveUp(string reason)
        {
            if (_escort && _escort.IsActive) _escort.GiveUp(reason);
            if (_run && _run.IsActive) _run.GiveUp(reason);
        }

        void SailEscort(int difficulty, SavedSquad squad, bool aiLocal, VoyageCourse course = null)
        {
            var vessel = Alive(_context?.GameData?.LocalPlayer?.Vessel);
            if (vessel?.Transform == null)
            {
                CSDebug.LogWarning("[VoyageHarbour] No local vessel to escort the Ark with.");
                return;
            }
            if (!_escort)
            {
                var go = new GameObject("ArkEscortRun");
                go.transform.SetParent(transform.parent, false);
                _escort = go.AddComponent<ArkEscortRun>();
                _escort.Configure(_def, _def.ArkEscort, _context, transform.parent, RaiseChanged);
            }
            _escortSquad = squad ?? VoyageRun.ActiveSquad(_context);
            _escortAiLocal = aiLocal;
            _escort.SetSail(vessel, _escortSquad, difficulty, aiLocal, course);
            RaiseChanged();
        }

        void Sail(VoyageJob job, SavedSquad squad)
        {
            var vessel = Alive(_context?.GameData?.LocalPlayer?.Vessel);
            if (vessel?.Transform == null)
            {
                CSDebug.LogWarning("[VoyageHarbour] No local vessel to set sail with.");
                return;
            }
            EnsureRun();
            _run.SetSail(vessel, job, squad);
            RaiseChanged();
        }

        // A friend's machine follows the host's voyage (Docs/Voyages/SLICE.md §4 step 5). The session can start after the
        // harbour does, so the listener is re-armed now and then; it is a no-op once registered.
        void Update()
        {
            if (Time.unscaledTime < _nextListen) return;
            _nextListen = Time.unscaledTime + 1f;
            VoyageCourseLink.Listen(OnCourse);
        }

        void OnCourse(VoyageCourse course)
        {
            switch (course.Kind)
            {
                case VoyageCourseKind.EscortSail:
                    FollowEscort(course);
                    return;
                case VoyageCourseKind.EscortEnd:
                    if (_escort) _escort.EndWithHost(course);
                    return;
            }
            EnsureRun();
            _run.Follow(course);
        }

        void EnsureRun()
        {
            if (_run) return;
            var go = new GameObject("VoyageRun");
            go.transform.SetParent(transform.parent, false);
            _run = go.AddComponent<VoyageRun>();
            _run.Configure(_def, _context, transform.parent, RaiseChanged);
        }

        Domains LiveDomain() => VoyageRun.LiveDomain(_context);

        // ── The escort's end: the scoreboard (Docs/Voyages/CRYSTAL_RUN.md section 5) ──────────────────────────────────

        SavedSquad _escortSquad;
        bool _escortAiLocal;

        /// <summary>
        /// The Ark escort ended. Came through: its pay (crystals by difficulty) is owed in the galaxy's record, at stake
        /// until the Ark docks home, and the board offers Venture further (the same escort one difficulty up) or Finish
        /// (the Ark sails home with the squad escorting it; docking banks it). Fell: the board says so. Given up, or
        /// the escort sim: no board.
        /// </summary>
        void OnEscortFinished(ArkEscortReport report)
        {
            if (report == null || _escortAiLocal || !_def) return;
            if (report.Result is not (ArkEscortReport.Outcome.Survived or ArkEscortReport.Outcome.Fallen)) return;
            bool survived = report.Result == ArkEscortReport.Outcome.Survived;
            // A party friend's board: the host's outcome and the host's rows (every seat), and no choice of its own.
            bool following = report.Following;
            var content = Content ? Content.Content : null;

            int pay = 0;
            if (survived && content != null)
            {
                pay = Mathf.Max(0, report.Difficulty * _def.EscortCrystalsPerDifficulty);
                var state = VoyageStore.Load();
                if (state != null)
                {
                    state.Normalize();
                    VoyageRaid.Owe(state, _def.EscortRewardElement, pay);
                    state.SavedAtMs = UniqueId.NowMs();
                    VoyageStore.Save(state);
                }
            }

            var last = report.LegReports is { Count: > 0 } ? report.LegReports[^1] : null;
            var board = new VoyageScoreboard
            {
                Board = survived ? VoyageScoreboard.Kind.Escort : VoyageScoreboard.Kind.Lost,
                Succeeded = survived,
                To = !string.IsNullOrEmpty(report.To) && content?.Location(report.To) is { } place ? place.Name : last?.Name ?? string.Empty,
                Difficulty = report.Difficulty,
                Worlds = report.ReachedLegs,
                Legs = report.Legs,
                Seconds = Mathf.Max(0f, report.EndedAt - report.UnderwayAt),
                Hull = survived && last != null && report.HullTotal > 0 ? Mathf.Clamp01((float)last.HullAtExit / report.HullTotal) : 0f,
                Breach = report.BreachHull,
                Pay = pay,
                NextDifficulty = Mathf.Min(report.Difficulty + 1, ArkEscortSO.MaxDifficulty),
                Following = following,
            };
            string me = _context?.GameData?.LocalPlayer?.Vessel?.VesselStatus?.PlayerName;
            if (following && _escort && _escort.HostRows.Count > 0)
                foreach (var seat in _escort.HostRows)
                {
                    if (seat == null) continue;
                    board.Rows.Add(new VoyageScoreboard.Row
                    {
                        Name = seat.PlayerName, You = !string.IsNullOrEmpty(me) && seat.PlayerName == me,
                        Vessel = (VesselClassType)seat.Vessel, Kills = seat.Kills, Guarded = seat.Guarded,
                    });
                }
            else
                foreach (var seat in report.RunSeats)
                {
                    if (seat == null) continue;
                    System.Enum.TryParse(seat.Vessel, true, out VesselClassType hull);
                    board.Rows.Add(new VoyageScoreboard.Row
                    {
                        Name = seat.PlayerName, You = !string.IsNullOrEmpty(me) && seat.PlayerName == me, Vessel = hull,
                        Kills = seat.Kills,
                        Guarded = seat.KillsOnArk,
                    });
                }
            if (following)
            {
                VoyageScoreboard.Show(board);
                return;
            }
            if (survived)
            {
                if (report.Difficulty < ArkEscortSO.MaxDifficulty)
                    board.Venture = () => SetSailEscort(board.NextDifficulty, _escortSquad);
                board.Finish = SailHomeAfterEscort;
            }
            else
            {
                int again = report.Difficulty;
                board.TryAgain = () => TryAgainEscort(again);
            }
            VoyageScoreboard.Show(board);
        }

        /// <summary>Finish after an escort: the Ark sails home from where the escort left it (a job voyage home), or,
        /// already home, banks what is owed here and now.</summary>
        void SailHomeAfterEscort()
        {
            var content = Content ? Content.Content : null;
            if (content == null) return;
            var state = VoyageRun.LoadOrBegin(content, LiveDomain());
            if (!state.IsHome())
            {
                SetSail(VoyageRules.ReturnJob(state, content));
                return;
            }
            var x = VoyageRules.Extract(state, content);
            if (x != null)
                for (int e = 0; e < x.Crystals.Length; e++)
                    if (x.Crystals[e] > 0) PilotLoadoutStore.AddCrystals((Element)e, x.Crystals[e], "voyage");
            state.SavedAtMs = UniqueId.NowMs();
            VoyageStore.Save(state);
            var board = new VoyageScoreboard
            {
                Board = VoyageScoreboard.Kind.Docked, Succeeded = true, Banked = true,
                To = content.Location(state.At)?.Name ?? string.Empty,
            };
            if (x != null)
                for (int e = 0; e < x.Crystals.Length && e < board.Crystals.Length; e++) board.Crystals[e] = x.Crystals[e];
            VoyageScoreboard.Show(board);
        }

        static IVesselStatus Alive(IVessel vessel)
        {
            if (vessel == null) return null;
            if (vessel is Object uo && !uo) return null;
            return vessel.VesselStatus;
        }

        void OnDestroy()
        {
            ArkEscortRun.Finished -= OnEscortFinished;
            VoyageScoreboard.Hide();
            VoyageCourseLink.Stop(OnCourse);
            if (s_current == this) s_current = null;
        }
    }
}
