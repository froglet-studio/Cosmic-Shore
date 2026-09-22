using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Butterfly's <b>Spread Wings</b> — MASS. Hold the trigger and the wings open; the wake
    /// widens from a narrow line of keys into a broad ribbon, and it costs energy the whole time.
    /// Design record: <c>R_VesselActions/BUTTERFLY.md</c> §3.2.
    ///
    /// <para><b>The cost is the point.</b> A brush that is always at its widest is not a brush —
    /// it is a setting — so the wide wake is METERED: holding drains
    /// <see cref="ResourceIndex"/> at <see cref="EnergyPerSecond"/>, the meter refills only while
    /// the wings are shut, and running it dry closes them. What the pilot is actually composing is
    /// WHERE the broad strokes go, which is the whole craft this vessel exists for.</para>
    ///
    /// <para><b>MASS's continuous dial is not here.</b> It is the trail prism's VOLUME, authored as
    /// the <c>trailVolume</c> ElementalFloat on the vessel's own <c>VesselPrismController</c> (the
    /// Squirrel's mapping, reused rather than reinvented) — so Mass makes the wake bigger in two
    /// ways that do not overlap: the pilot spends energy to make it WIDER, and the element makes
    /// each key HEAVIER. Two dials on one element would be the double-dip the fleet convention
    /// exists to prevent; two different QUANTITIES, one authored per ability and one per prism,
    /// are not.</para>
    ///
    /// <para><b>MASS level-5 — "Mural": the spread costs nothing.</b> The wings stay open for as
    /// long as the pilot wants them open, and the one reason not to fly permanently wide goes
    /// away. That is deliberately the most desirable thing a painter could be given, and it is
    /// what the whole metered economy exists to make feel like a reward.</para>
    ///
    /// <para>The asset is SHARED by every Butterfly in a match and holds no per-vessel state.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "SpreadWingsAction",
        menuName = "ScriptableObjects/Vessel Actions/Butterfly Spread Wings")]
    public class SpreadWingsActionSO : ShipActionSO
    {
        [Header("Energy")]
        [Tooltip("Index into ResourceSystem.Resources of the wing-energy meter. Document it when " +
                 "you change it: meters are addressed by serialized index across the whole fleet " +
                 "and nothing catches a stale one.")]
        [SerializeField, Min(0)] int resourceIndex;

        [Tooltip("Energy drained per second while the wings are held open. Sized against the " +
                 "meter's own refill rate so a full meter buys a long stroke, not a permanent " +
                 "state — the ratio of the two IS the mechanic.")]
        [SerializeField, Min(0f)] float energyPerSecond = 0.22f;

        [Tooltip("MASS level-5 'Mural': when the Mass upgrade is live the spread is FREE and the " +
                 "wings never close on their own. Gated on the replicated unlock bit, per-frame " +
                 "at spend time, so a level lost mid-stroke starts charging for it again.")]
        [SerializeField] bool massUpgradeMakesSpreadFree = true;

        [Header("Wake")]
        [Tooltip("Normalized trail width while the wings are open — fed to " +
                 "VesselPrismController.SetNormalizedXScale, where 1 means the prism controller's " +
                 "own maxBlockScale. The controller lerps toward it, so the wake OPENS over about " +
                 "a second and a half rather than stepping, which is what makes a stroke read as " +
                 "a stroke.")]
        [SerializeField, Range(0f, 1f)] float openWidth01 = 1f;

        [Tooltip("Normalized trail width with the wings shut. Not zero: a Butterfly always lays a " +
                 "line, it is only ever a question of how wide.")]
        [SerializeField, Range(0f, 1f)] float shutWidth01;

        public int ResourceIndex => Mathf.Max(0, resourceIndex);
        public float EnergyPerSecond => Mathf.Max(0f, energyPerSecond);
        public float OpenWidth01 => openWidth01;
        public float ShutWidth01 => shutWidth01;

        /// <summary>
        /// Whether this stroke is free right now. Read per-frame at spend time rather than
        /// snapshotted at the press: an upgrade is a live state, and a Butterfly that lost Mass 5
        /// mid-stroke should start paying for the rest of it.
        ///
        /// Gates on <c>IsUpgradeActive</c> — the replicated bit — not a raw local level read,
        /// because what it decides is how much conserved mass ends up in the world.
        /// </summary>
        public bool IsSpreadFree(IVesselStatus status)
        {
            if (!massUpgradeMakesSpreadFree) return false;
            var abilities = status?.ElementalAbilityHandler;
            return abilities != null && abilities.IsUpgradeActive(Element.Mass);
        }

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<SpreadWingsActionExecutor>()?.Open(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<SpreadWingsActionExecutor>()?.Close(this, vesselStatus);
    }
}
