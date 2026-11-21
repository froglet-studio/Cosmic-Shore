using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// THE SNAP TRAP (round 11c, Docs/THREAT_FLORA.md §2) - one Venus-flytrap lifeform: 27 prisms (a 3-prism stalk,
    /// two 8-plate lobes, 8 danger teeth on the lips) and ONE heart crystal that sits inside the jaws. It is a thin
    /// shell over a row of <see cref="SnapTrapCore"/>, which every trap in the cell shares through the cell's
    /// <see cref="ThreatGrove"/>: the core owns the state machine (OPEN, PRIMING, ARMED, CLOSING, SHUT), the
    /// heliotropic turn and the colony's rhizome; this class only lays and forgets prisms when the grove tells it to.
    ///
    /// <para><b>Ecology.</b> One crystal per lifeform. A seeded trap brings its own body into the rhizome; every
    /// slot is laid FROM the rhizome and a lost slot is re-laid only from it, so a trap never re-grows from nothing.
    /// A daughter is budded only while the rhizome holds a whole body, through the platform's one offspring path
    /// (<see cref="Flora.TrySpawnOneOffspring"/>, so the cell's cap and the Frenzy freeze apply) and inherits the
    /// parent's domain. A jousted trap leaves its standing body as a skeleton; a trap grazed to nothing withers
    /// and drops its heart. Nothing is timed out: there is no TTL anywhere in the trap.</para>
    ///
    /// <para><b>Stakes.</b> The teeth are ordinary danger prisms (an opposing-domain vessel that touches one burns
    /// petals), and the lobes are danger prisms from the moment the trap fires until the jaws are shut. The
    /// telegraph is the lobes' glow, which covers the strike volume (<see cref="SnapTrapCore.GlowRadius"/>).</para>
    /// </summary>
    public class SnapTrapFlora : Flora
    {
        [Header("Threat grove")]
        [Tooltip("The grove this trap belongs to (shared with the cell's physarum network). Its clumps say where a " +
                 "seeded trap is planted; its leaves size the stalk, lobe and tooth prisms.")]
        [SerializeField] ThreatGroveConfigSO grove;

        ThreatGrove _grove;
        int _trap = -1;
        Spindle _limb;

        // set on a daughter by its parent (ConfigureOffspring) before Initialize
        bool _budded;

        // the parent's pending bud (TryBud -> TryResolveOffspringPlacement / ConfigureOffspring)
        Vector3 _pendingBudAt, _pendingBudAxis;

        protected override int PrismBudget => SnapTrapCore.SlotCount;

        /// <summary>The geometry is a measured layout in absolute units (snaptrap.py _layout), scaled only by the
        /// element's GeometryScale, so a per-cell leaf scale must not resize it (see Flora.PrismSizeFixedByGrowthRule).</summary>
        protected override bool PrismSizeFixedByGrowthRule => true;

        public int TrapIndex => _trap;

        public override void Plant()
        {
            _grove = ThreatGrove.For(cell, grove);
            if (!_grove)
            {
                CSDebug.LogWarning($"{name}: SnapTrapFlora needs a cell and a ThreatGroveConfigSO; it was planted without one and will not grow.");
                return;
            }

            Vector3 heart, axis;
            if (TryGetPlantPositionOverride(out var pinned))
            {
                // a daughter (or a pinned plant) roots at the rim like a seeded one and is planted facing the cell
                // centre - the heading its heliotropism cone is about; her parent's traffic turns her from there
                heart = _grove.RootAtRim(pinned);
                axis = (ToU(_grove.Shape.CellCentre) - heart).normalized;
            }
            else
            {
                heart = _grove.PickTrapSite(out axis);
            }
            if (axis.sqrMagnitude < 1e-6f) axis = Vector3.up;
            transform.SetPositionAndRotation(heart, Quaternion.LookRotation(axis.normalized));
            _trap = _grove.RegisterTrap(this, heart, axis.normalized, _budded);
        }

        /// <summary>Growth is the grove's: the core blooms each trap's slots over its GrowSeconds and repairs them
        /// from the rhizome. A plant tick has nothing to add.</summary>
        public override void Grow() { }

        /// <summary>Lay one slot's prism (called by the grove for a core Lay event). Null if the pool is empty.</summary>
        public HealthPrism LaySlotPrism(Vector3 position, Quaternion rotation, Vector3 leaf, bool tooth)
        {
            if (IsDying || !healthPrism) return null;
            if (!_limb)
            {
                _limb = AddSpindle();
                _limb.transform.SetPositionAndRotation(transform.position, transform.rotation);
            }

            HealthPrism prism = EnvironmentPrismPool.Get(healthPrism, position, rotation);
            if (!prism) return null;
            AddHealthBlock(prism);
            prism.AdmitTargetScale(leaf);
            prism.TargetScale = leaf;
            prism.transform.SetParent(_limb.transform, true);
            prism.LifeForm = this;
            if (tooth) prism.MakeDangerous();
            prism.Initialize("flora");
            NotifyGrew();
            return prism;
        }

        /// <summary>The heart sits in the jaws: re-placed on every pose keyframe (never per frame).</summary>
        public void PlaceHeart(Vector3 position)
        {
            if (crystal) crystal.transform.position = position;
        }

        /// <summary>The rhizome can pay a daughter at <paramref name="at"/>, facing <paramref name="axis"/>.</summary>
        public void TryBud(Vector3 at, Vector3 axis)
        {
            if (IsDying) return;
            _pendingBudAt = at;
            _pendingBudAxis = axis;
            TrySpawnOneOffspring();
        }

        protected override bool TryResolveOffspringPlacement(out Vector3 position, out Quaternion rotation, out Vector3? up)
        {
            position = _grove ? _grove.RootAtRim(_pendingBudAt) : _pendingBudAt;
            rotation = _pendingBudAxis.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(_pendingBudAxis) : transform.rotation;
            up = null;
            return true;
        }

        protected override void ConfigureOffspring(Flora child)
        {
            if (child is not SnapTrapFlora daughter) return;
            daughter._budded = true;
        }

        public override void RemoveHealthBlock(HealthPrism healthPrism, string killerName = "")
        {
            // book the loss BEFORE the base call, which may decide the trap is dead
            if (_grove && _trap >= 0) _grove.TrapSlotRemoved(_trap, healthPrism);
            base.RemoveHealthBlock(healthPrism, killerName);
        }

        protected override void Die(string killerName = "")
        {
            // the standing body leaves the colony's ledger as cell mass before the death style decides what it
            // becomes (a jousted skeleton or ordinary debris), so the per-prism removals that follow book nothing
            if (_grove && _trap >= 0) _grove.TrapDied(_trap, this);
            base.Die(killerName);
        }

        protected override void OnDestroy()
        {
            if (_grove && _trap >= 0) _grove.TrapGone(_trap, this);
            _trap = -1;
            base.OnDestroy();
        }

        static Vector3 ToU(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
    }
}
