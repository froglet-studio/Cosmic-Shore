using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Voyage toy in the world (<see cref="VoyageToyDefinitionSO"/>): fly it to set out (the squad deploys and the
    /// job board stands), fly it again to give the voyage up. The run itself is <see cref="VoyageRun"/>, a sibling under
    /// the toybox root so the toy's bloom never scales the Ark or the board.
    ///
    /// <para>It is also an <see cref="IToyShellSurface"/>, so the Toy Box lists it and the Voyage window's third voyage
    /// opens it: one option, set sail (or give the voyage up while one is under way), which the shell plays after
    /// entering freestyle, exactly as flying the toy would.</para>
    /// </summary>
    public sealed class VoyageToy : Toy, IToyShellSurface
    {
        VoyageToyDefinitionSO _def;
        VoyageRun _run;

        float _nextListen;

        public void Configure(VoyageToyDefinitionSO definition) => _def = definition;

        // A friend's machine follows the host's voyage (Docs/Voyages/SLICE.md §4 step 5). The session can start after
        // the toy does, so the listener is re-armed now and then; it is a no-op once registered.
        protected override void Update()
        {
            base.Update();
            if (UnityEngine.Time.unscaledTime < _nextListen) return;
            _nextListen = UnityEngine.Time.unscaledTime + 1f;
            VoyageCourseLink.Listen(OnCourse);
        }

        void OnCourse(CosmicShore.Data.VoyageCourse course)
        {
            EnsureRun();
            _run.Follow(course);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            VoyageCourseLink.Stop(OnCourse);
        }

        protected override void OnActivated(IVesselStatus localVessel)
        {
            EnsureRun();
            if (_run.IsActive) _run.GiveUp("the pilot flew the voyage toy");
            else _run.Embark(localVessel);
            Rebloom();
        }

        // ── The app shell ────────────────────────────────────────────────────

        ToyDefinitionSO IToyShellSurface.ShellDefinition => Definition;

        bool IToyShellSurface.ShellAvailable => _def;

        void IToyShellSurface.BuildShellOptions(List<ToyShellOption> into)
        {
            if (!_def) return;
            bool live = _run && _run.IsActive;
            into.Add(new ToyShellOption
            {
                Label = live ? _def.ShellUnderwayLabel : _def.ShellLabel,
                Detail = live ? _def.ShellUnderwayDetail : _def.ShellDetail,
                Accent = _def.AccentColor,
                IsCurrent = live,
                // A voyage means nothing without the player at the stick: the shell enters freestyle first.
                RequiresFreestyle = true,
                CommitVerb = live ? _def.ShellGiveUpVerb : _def.ShellSetSailVerb,
                Apply = live ? GiveUpFromShell : SetSailFromShell,
            });
        }

        void SetSailFromShell()
        {
            var vessel = ResolveLocalVessel();
            if (vessel?.Transform == null)
            {
                CSDebug.LogWarning("[VoyageToy] No local vessel to set sail with.");
                return;
            }
            EnsureRun();
            if (!_run.IsActive) _run.Embark(vessel);
            Rebloom();
        }

        void GiveUpFromShell()
        {
            if (_run && _run.IsActive) _run.GiveUp("the pilot gave the voyage up from the menu");
            Rebloom();
        }

        void EnsureRun()
        {
            if (_run) return;
            var go = new UnityEngine.GameObject("VoyageRun");
            go.transform.SetParent(transform.parent, false);
            _run = go.AddComponent<VoyageRun>();
            _run.Configure(_def, Context, transform.parent, Rebloom);
        }
    }
}
