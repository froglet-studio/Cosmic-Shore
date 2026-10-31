using System.Collections.Generic;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Where voyages set out from (<see cref="VoyageToyDefinitionSO"/>). It stands in freestyle with the toybox but is
    /// no toy: nothing to fly through, nothing in the Toy Box. The app shell's Voyage window asks it for the jobs on offer
    /// (<see cref="Board"/>) and sets sail on the one the player picks (<see cref="SetSail"/>), which enters freestyle
    /// and hands the job to the run (<see cref="VoyageRun"/>), a sibling under the toybox root.
    ///
    /// <para>Each job is a voyage of its own (Garrett, 2026-10-28: "the jobs should be the different voyages"): the run
    /// ends when the player leaves freestyle, anchored at the job's destination once it got there, and the window then
    /// offers the jobs from that spot.</para>
    /// </summary>
    public sealed class VoyageHarbour : MonoBehaviour
    {
        const float FreestyleHandoffSeconds = 8f;

        static VoyageHarbour s_current;

        VoyageToyDefinitionSO _def;
        ToyContext _context;
        VoyageRun _run;
        ArkEscortRun _escort;
        CancellationTokenSource _handoff;
        float _nextListen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_current = null;
            Changed = null;
        }

        /// <summary>The harbour standing now, or null (no toybox built yet, or a cell swap tore it down).</summary>
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
            var content = Content;
            if (!content || content.Content == null || IsUnderway) return new List<VoyageJob>();
            state = VoyageRun.LoadOrBegin(content.Content, LiveDomain());
            return VoyageRules.Board(state, content.Content, VoyageRun.SeatedPilots(VoyageRun.ActiveSquad(_context)));
        }

        /// <summary>
        /// Set sail on <paramref name="job"/>: into freestyle first (the menu closes as it opens), then the squad deploys
        /// and the Ark sails for the job's destination.
        /// </summary>
        public void SetSail(VoyageJob job, MenuCrystalClickHandler freestyle)
        {
            if (job == null || IsUnderway) return;
            CancelHandoff();
            if (!freestyle || freestyle.IsInFreestyle)
            {
                Sail(job);
                return;
            }
            _handoff = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            freestyle.ToggleTransition();
            SailOnceFlying(job, freestyle, _handoff.Token).Forget();
        }

        async UniTaskVoid SailOnceFlying(VoyageJob job, MenuCrystalClickHandler freestyle, CancellationToken ct)
        {
            float deadline = Time.unscaledTime + FreestyleHandoffSeconds;
            while (freestyle && (!freestyle.IsInFreestyle || freestyle.IsTransitioning))
            {
                if (Time.unscaledTime > deadline)
                {
                    CSDebug.LogWarning($"[VoyageHarbour] Freestyle did not settle within {FreestyleHandoffSeconds:0}s - " +
                                       "the voyage did not set sail.");
                    return;
                }
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            if (!ct.IsCancellationRequested) Sail(job);
        }

        /// <summary>
        /// Set sail on the Ark escort at <paramref name="difficulty"/> (1-9) with <paramref name="squad"/> (null: the
        /// active squad): into freestyle first, then the squad deploys and the Ark sails the escort's worlds.
        /// <paramref name="aiLocal"/> hands the local seat to the AI as well (the escort sim).
        /// </summary>
        public void SetSailEscort(int difficulty, MenuCrystalClickHandler freestyle, SavedSquad squad = null,
            bool aiLocal = false) => SetSailEscort(difficulty, freestyle, squad, aiLocal, null);

        /// <summary>As above, following the host's <paramref name="course"/> when given (a friend's machine).</summary>
        public void SetSailEscort(int difficulty, MenuCrystalClickHandler freestyle, SavedSquad squad, bool aiLocal,
            VoyageCourse course)
        {
            if (IsUnderway || !_def || !_def.ArkEscort) return;
            // A friend never starts an escort of its own while in a host's session: the host's course sails it.
            if (course == null && !VoyageCourseLink.IsLeader)
            {
                CSDebug.LogWarning("[VoyageHarbour] Only the host sets sail on an Ark escort; friends follow its course.");
                return;
            }
            CancelHandoff();
            if (!freestyle || freestyle.IsInFreestyle)
            {
                SailEscort(difficulty, squad, aiLocal, course);
                return;
            }
            _handoff = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            freestyle.ToggleTransition();
            EscortOnceFlying(difficulty, squad, aiLocal, course, freestyle, _handoff.Token).Forget();
        }

        async UniTaskVoid EscortOnceFlying(int difficulty, SavedSquad squad, bool aiLocal, VoyageCourse course,
            MenuCrystalClickHandler freestyle, CancellationToken ct)
        {
            float deadline = Time.unscaledTime + FreestyleHandoffSeconds;
            while (freestyle && (!freestyle.IsInFreestyle || freestyle.IsTransitioning))
            {
                if (Time.unscaledTime > deadline)
                {
                    CSDebug.LogWarning($"[VoyageHarbour] Freestyle did not settle within {FreestyleHandoffSeconds:0}s - " +
                                       "the escort did not set sail.");
                    return;
                }
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            if (!ct.IsCancellationRequested) SailEscort(difficulty, squad, aiLocal, course);
        }

        void SailEscort(int difficulty, SavedSquad squad, bool aiLocal, VoyageCourse course)
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
            _escort.SetSail(vessel, squad ?? VoyageRun.ActiveSquad(_context), difficulty, aiLocal, course);
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
            switch (course.Kind)
            {
                case VoyageCourseKind.EscortSail:
                    // The host set sail on an Ark escort: sail the same one in the seat it gave this machine.
                    if (IsUnderway) return;
                    SetSailEscort(course.Difficulty,
                        FindAnyObjectByType<MenuCrystalClickHandler>(FindObjectsInactive.Include), null, false, course);
                    return;
                case VoyageCourseKind.EscortEnd:
                    if (IsEscorting && _escort.IsFollowing) _escort.GiveUp("the host's escort ended");
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

        void CancelHandoff()
        {
            if (_handoff == null) return;
            _handoff.Cancel();
            _handoff.Dispose();
            _handoff = null;
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
            CancelHandoff();
            VoyageCourseLink.Stop(OnCourse);
            if (s_current == this) s_current = null;
        }
    }
}
