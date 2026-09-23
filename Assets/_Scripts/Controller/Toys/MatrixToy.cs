using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A toy that is <b>one station until you fly it, then many</b>: a pass opens a MATRIX of
    /// choices out ahead of you, another pass closes it. This is the shared shape behind the
    /// Cell Selector, the Connect-the-Dots gallery, and the Vessel Changer - a toybox of a
    /// dozen permanently-visible stations is clutter, and a single toy that unfolds into its
    /// options reads as one thing you can play with.
    ///
    /// The matrix blooms <see cref="MatrixDistanceFactor"/> x <see cref="StationSpacing"/> from
    /// the toy along the OUTWARD radial (away from the cell centre - the toy faces the centre,
    /// so outward is -forward). You fly AT the toy and keep going: the choices are ahead, never
    /// back through where you came from, and each successive layer sits further out than the
    /// last. Rows/columns are a roughly-square grid in the toy's own right x up plane.
    ///
    /// Collider note: matrix stations are transient triggers built on a pass and torn down on
    /// the next, Menu_Main freestyle only - they never contribute to the per-cell budget.
    ///
    /// <para><b>ONE DECLARATION.</b> A matrix toy declares its choices exactly once, in
    /// <see cref="BuildOptions"/>. The fly-into matrix and the menu Toy Box window are two INPUTS
    /// to that one declaration: this class builds the stations from it AND answers
    /// <see cref="IToyShellSurface.BuildShellOptions"/> with it, and <see cref="CreateStation"/>
    /// wires the station's action to the option's own <see cref="ToyShellOption.Apply"/> - so a
    /// subclass has nowhere to put a second opinion about which choices exist or what one does.
    /// Presentation stays free (a station shows the real hull at arena range, a window shows a
    /// small preview); the OPTION SET and the ACTION do not.</para>
    ///
    /// <para>Before this, each toy wrote the two enumerations by hand and they agreed only by
    /// coincidence - the Lifeform bench's window really had silently lost a whole kingdom, and the
    /// Cell Selector kept two transcriptions of one rule that happened to still match. The only
    /// difference a toy may now declare is <see cref="WorldOmitsCurrentOption"/>, which is one
    /// named bool rather than a divergence nobody can see.</para>
    /// </summary>
    public abstract class MatrixToy : Toy, IToyShellSurface
    {
        GameObject _grid;

        /// <summary>
        /// The toy's choices, as declared by <see cref="BuildOptions"/> and resolved on the pass
        /// that opens the matrix. The SHELL gets its own freshly-built copy (it may be asked at
        /// any time, including while the matrix is shut), so this list is the world's view.
        /// </summary>
        protected readonly List<ToyShellOption> Options = new();

        /// <summary>
        /// The subset of <see cref="Options"/> the matrix actually builds a station for - all of
        /// them, unless <see cref="WorldOmitsCurrentOption"/>. Station <c>i</c> IS
        /// <c>WorldOptions[i]</c>.
        /// </summary>
        protected readonly List<ToyShellOption> WorldOptions = new();

        /// <summary>True while the matrix is unfolded.</summary>
        protected bool IsMatrixOpen => _grid;

        /// <summary>The open matrix's root, or null. Stations live under it and die with it.</summary>
        protected Transform MatrixRoot => _grid ? _grid.transform : null;

        /// <summary>
        /// Where the toy itself hangs (the toybox root). A station's side-effects that must
        /// OUTLIVE the matrix - a painting run in progress, say - belong here, not under
        /// <see cref="MatrixRoot"/>.
        /// </summary>
        protected Transform ToyboxRoot => transform.parent;

        // ── The one declaration ──────────────────────────────────────────────

        /// <summary>
        /// Fill <paramref name="into"/> (already cleared) with every choice this toy offers.
        /// <b>The only place a matrix toy names its choices.</b> Both surfaces read it, so an
        /// option omitted here is omitted from the world AND the window, and an option added here
        /// appears in both.
        ///
        /// <para>Set <see cref="ToyShellOption.Payload"/> to whatever the station needs to build
        /// its model - the config, the class, the definition. That is what lets one list serve a
        /// flat row and a flown-to station without a second list of subjects beside it.</para>
        /// </summary>
        protected abstract void BuildOptions(List<ToyShellOption> into);

        /// <summary>
        /// True when the matrix should build no station for the option the player is already in.
        ///
        /// <para>The ONE sanctioned difference between the two surfaces, and it is a difference in
        /// PRESENTATION rather than in what is on offer: the vessel changer's matrix is
        /// "everything except what you fly" because flying your own hull would swap it for itself,
        /// while the window still lists it so the player can see which one they are on. The cell
        /// selector declines it - choosing the world you are already in IS the freestyle reset, so
        /// its station is real and wears a halo.</para>
        ///
        /// <para>It is a named bool rather than a subclass filtering its own list because a
        /// declared exception can be read, reviewed and tested; a hand-filtered second enumeration
        /// is the drift this class exists to prevent.</para>
        /// </summary>
        protected virtual bool WorldOmitsCurrentOption => false;

        // ── Layout contract ──────────────────────────────────────────────────

        /// <summary>Centre-to-centre gap between adjacent stations, world units.</summary>
        protected abstract float StationSpacing { get; }

        /// <summary>Radius of one station's visual, world units.</summary>
        protected abstract float StationRadius { get; }

        /// <summary>How far out the matrix sits, in multiples of <see cref="StationSpacing"/>.</summary>
        protected abstract float MatrixDistanceFactor { get; }

        /// <summary>
        /// Build the station for <paramref name="option"/> under <paramref name="parent"/> at
        /// <paramref name="position"/>. The subclass owns the station's LOOK - its model, label
        /// and any halo - and takes the subject from <see cref="ToyShellOption.Payload"/>.
        ///
        /// <para>It does NOT own the action: <see cref="CreateStation"/> wires
        /// <see cref="ToyShellOption.Apply"/> for you. A station built some other way (a full
        /// <see cref="Toy"/> with its own bloom and exit-gated re-arm, as the painting gallery
        /// needs) must call that same <c>Apply</c> and nothing else, or the two surfaces stop
        /// doing the same thing.</para>
        /// </summary>
        protected abstract void BuildStation(ToyShellOption option, Transform parent, Vector3 position, float radius);

        /// <summary>Hook after every station is built (e.g. start streaming their contents).</summary>
        protected virtual void OnMatrixOpened() { }

        /// <summary>Hook just before the matrix is released (e.g. cancel that streaming).</summary>
        protected virtual void OnMatrixClosed() { }

        // ── Open / close ─────────────────────────────────────────────────────

        protected override void OnActivated(IVesselStatus localVessel)
        {
            if (_grid) CloseMatrix();
            else OpenMatrix();
        }

        protected void OpenMatrix()
        {
            if (_grid) return;

            if (!ResolveOptions()) return;
            int count = WorldOptions.Count;

            // Sibling of the toy, not a child: the matrix must not inherit the toy's own
            // bloom/flip scaling, and it is released independently.
            _grid = new GameObject($"{DisplayName}_Matrix");
            _grid.transform.SetParent(ToyboxRoot, true);

            // Floored, not trusted: a serialized field added after an asset was authored can
            // deserialize to 0, and a matrix at distance 0 would open inside the toy.
            float spacing = Mathf.Max(1f, StationSpacing);
            float radius = Mathf.Max(0.1f, StationRadius);
            float distance = Mathf.Max(0.5f, MatrixDistanceFactor);
            Vector3 origin = transform.position + (-transform.forward) * (spacing * distance);
            Vector3 right = transform.right;
            Vector3 up = transform.up;

            int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count)));
            int rows = Mathf.CeilToInt(count / (float)cols);

            for (int i = 0; i < count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                Vector3 position = origin
                                   + right * (spacing * (col - (cols - 1) * 0.5f))
                                   + up * (spacing * ((rows - 1) * 0.5f - row));
                BuildStation(WorldOptions[i], _grid.transform, position, radius);
            }

            OnMatrixOpened();
        }

        protected void CloseMatrix()
        {
            if (!_grid) return;
            var grid = _grid;
            _grid = null;               // cleared first: OnMatrixClosed may re-enter through a toggle
            OnMatrixClosed();
            ToyFactory.ScaleOutAndDestroy(grid, 0.8f).Forget();  // continuity law - it shrinks away
        }

        /// <summary>
        /// Re-read the declaration into <see cref="Options"/> and <see cref="WorldOptions"/>.
        /// False (with a warning) when the toy has nothing to offer, which keeps the matrix shut
        /// rather than blooming an empty grid.
        /// </summary>
        bool ResolveOptions()
        {
            Options.Clear();
            WorldOptions.Clear();
            BuildOptions(Options);

            foreach (var option in Options)
            {
                if (option == null) continue;
                if (WorldOmitsCurrentOption && option.IsCurrent) continue;
                WorldOptions.Add(option);
            }

            if (WorldOptions.Count > 0) return true;
            CSDebug.LogWarning($"[{GetType().Name}] '{DisplayName}' has nothing to offer - matrix not opened.");
            return false;
        }

        // ── App-shell face: the SAME declaration, never a second one ─────────

        ToyDefinitionSO IToyShellSurface.ShellDefinition => Definition;

        /// <summary>
        /// False while the toy cannot answer - mid cell-swap, mid vessel-swap. Default true;
        /// override where "what am I flying / where am I" has no settled answer for a moment.
        /// </summary>
        protected virtual bool ShellAvailable => true;

        bool IToyShellSurface.ShellAvailable => ShellAvailable;

        /// <summary>
        /// The window's option list is <see cref="BuildOptions"/> and nothing else - not a copy of
        /// <see cref="Options"/>, because the window may ask while the matrix is shut and the
        /// answer must be live. Deliberately NOT virtual: a subclass that could override this
        /// could reintroduce the second enumeration.
        /// </summary>
        void IToyShellSurface.BuildShellOptions(List<ToyShellOption> into) => BuildOptions(into);

        // ── Shared station construction ──────────────────────────────────────

        /// <summary>
        /// The light station: a trigger root facing the cell centre, a
        /// <see cref="ToyMatrixStation"/> bound to this toy's context, and the station's action
        /// already wired to <paramref name="option"/>'s own <see cref="ToyShellOption.Apply"/>.
        /// Callers add the visual and nothing else.
        ///
        /// <para>Wiring the action HERE rather than in the subclass is what makes "the station and
        /// the window do the same thing" structural instead of a convention.</para>
        /// </summary>
        protected ToyMatrixStation CreateStation(ToyShellOption option, Transform parent, Vector3 position,
                                                 string name, float triggerRadius)
        {
            var go = ToyFactory.CreateBareRoot(name, parent, position, transform.position, triggerRadius);
            // Every fly-through choice wears the same switch ring as the toy root that opened it -
            // one word for "thread this and something happens", at every level of the toybox. A
            // choice is NEUTRAL: it hands you a cell, a hull or a creature, never a domain, so it
            // is painted Blue and leaves the domain colours to the domain changer.
            ToyFactory.AddSwitchRing(go.transform, StationRingRadius(triggerRadius), ToyFactory.Theme(Context));
            var station = go.AddComponent<ToyMatrixStation>();
            station.Bind(Context);
            station.OnVesselPassed = option?.Apply;
            return station;
        }

        /// <summary>
        /// This matrix's switch ring radius for a station whose trigger is
        /// <paramref name="triggerRadius"/> - clamped against <see cref="StationSpacing"/> so
        /// adjacent rings never interpenetrate (see <see cref="ToyFactory.MaxRingSpacingFraction"/>).
        /// Subclasses that hang their own labels use it to set the height.
        /// </summary>
        protected float StationRingRadius(float triggerRadius)
            => ToyFactory.StationRingRadius(triggerRadius, StationSpacing);

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_grid) Destroy(_grid);
            _grid = null;
        }
    }
}
