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

        /// <summary>The job the running voyage sails, or null.</summary>
        public VoyageJob CurrentJob => _run && _run.IsActive ? _run.CurrentJob : null;

        public static VoyageHarbour Create(VoyageToyDefinitionSO definition, Transform parent, ToyContext context)
        {
            var go = new GameObject("VoyageHarbour");
            go.transform.SetParent(parent, false);
            var harbour = go.AddComponent<VoyageHarbour>();
            harbour._def = definition;
            harbour._context = context;
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

        /// <summary>The jobs on offer for <paramref name="squad"/> from where the last voyage left the Ark (the Voyage
        /// window's board, read in the menu where no harbour stands).</summary>
        public static List<VoyageJob> Board(VoyageToyDefinitionSO settings, SavedSquad squad, Domains home,
            out VoyageState state)
        {
            state = null;
            var content = settings ? settings.Content : VoyageContentSO.Load();
            if (!content || content.Content == null) return new List<VoyageJob>();
            state = VoyageRun.LoadOrBegin(content.Content, home);
            return VoyageRules.Board(state, content.Content, VoyageRun.SeatedPilots(squad));
        }

        /// <summary>Set sail on <paramref name="job"/>: the squad deploys and the Ark sails for the job's destination.</summary>
        public void SetSail(VoyageJob job)
        {
            if (job == null || IsUnderway) return;
            Sail(job);
        }

        /// <summary>
        /// Set sail on the Ark escort at <paramref name="difficulty"/> (1-9) with <paramref name="squad"/> (null: the
        /// active squad): the squad deploys and the Ark sails the escort's worlds. <paramref name="aiLocal"/> hands the
        /// local seat to the AI as well (the escort sim).
        /// </summary>
        public void SetSailEscort(int difficulty, SavedSquad squad = null, bool aiLocal = false)
        {
            if (IsUnderway || !_def || !_def.ArkEscort) return;
            SailEscort(difficulty, squad, aiLocal);
        }

        /// <summary>End whatever voyage is out (the player left the voyage scene).</summary>
        public void GiveUp(string reason)
        {
            if (_escort && _escort.IsActive) _escort.GiveUp(reason);
            if (_run && _run.IsActive) _run.GiveUp(reason);
        }

        void SailEscort(int difficulty, SavedSquad squad, bool aiLocal)
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
            _escort.SetSail(vessel, squad ?? VoyageRun.ActiveSquad(_context), difficulty, aiLocal);
            RaiseChanged();
        }

        void Sail(VoyageJob job)
        {
            var vessel = Alive(_context?.GameData?.LocalPlayer?.Vessel);
            if (vessel?.Transform == null)
            {
                CSDebug.LogWarning("[VoyageHarbour] No local vessel to set sail with.");
                return;
            }
            EnsureRun();
            _run.SetSail(vessel, job);
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

        static IVesselStatus Alive(IVessel vessel)
        {
            if (vessel == null) return null;
            if (vessel is Object uo && !uo) return null;
            return vessel.VesselStatus;
        }

        void OnDestroy()
        {
            VoyageCourseLink.Stop(OnCourse);
            if (s_current == this) s_current = null;
        }
    }
}
