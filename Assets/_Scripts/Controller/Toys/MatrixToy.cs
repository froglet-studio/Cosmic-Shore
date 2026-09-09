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
    /// </summary>
    public abstract class MatrixToy : Toy
    {
        GameObject _grid;

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

        // ── Layout contract ──────────────────────────────────────────────────

        /// <summary>How many stations to build. 0 = nothing to offer (the toy logs and stays shut).</summary>
        protected abstract int StationCount { get; }

        /// <summary>Centre-to-centre gap between adjacent stations, world units.</summary>
        protected abstract float StationSpacing { get; }

        /// <summary>Radius of one station's visual, world units.</summary>
        protected abstract float StationRadius { get; }

        /// <summary>How far out the matrix sits, in multiples of <see cref="StationSpacing"/>.</summary>
        protected abstract float MatrixDistanceFactor { get; }

        /// <summary>
        /// Build station <paramref name="index"/> under <paramref name="parent"/> at
        /// <paramref name="position"/>. The subclass owns what makes the station interactive -
        /// a light <see cref="ToyMatrixStation"/> (see <see cref="CreateStation"/>) or a full
        /// <see cref="Toy"/> when the station needs its own bloom / exit-gated re-arm.
        /// </summary>
        protected abstract void BuildStation(int index, Transform parent, Vector3 position, float radius);

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

            int count = StationCount;
            if (count <= 0)
            {
                CSDebug.LogWarning($"[{GetType().Name}] '{DisplayName}' has nothing to offer - matrix not opened.");
                return;
            }

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
                BuildStation(i, _grid.transform, position, radius);
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

        // ── Shared station construction ──────────────────────────────────────

        /// <summary>
        /// The light station: a trigger root facing the cell centre plus a
        /// <see cref="ToyMatrixStation"/> already bound to this toy's context. Callers add the
        /// visual and set <see cref="ToyMatrixStation.OnVesselPassed"/>.
        /// </summary>
        protected ToyMatrixStation CreateStation(Transform parent, Vector3 position, string name, float triggerRadius)
        {
            var go = ToyFactory.CreateBareRoot(name, parent, position, transform.position, triggerRadius);
            // Every fly-through choice wears the same switch ring as the toy root that opened it -
            // one word for "thread this and something happens", at every level of the toybox. A
            // choice is NEUTRAL: it hands you a cell, a hull or a creature, never a domain, so it
            // is painted Blue and leaves the domain colours to the domain changer.
            ToyFactory.AddSwitchRing(go.transform, StationRingRadius(triggerRadius), ToyFactory.Theme(Context));
            var station = go.AddComponent<ToyMatrixStation>();
            station.Bind(Context);
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
