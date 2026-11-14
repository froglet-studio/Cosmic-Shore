using CosmicShore.Utility;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Rhino's OMNI-CRYSTAL ability, the <b>Stampede</b> (2026-11-12, replacing the slow blast it shared with the
    /// Swordfish): the omni crystal is absorbed into the hull (the fleet's absorb morph runs first) and the Rhino charges.
    /// For the stampede's length its throttle surges by the crystal's own speed buff, every gore needs only its Charge 10
    /// ram speed and steals (<see cref="RhinoGoreBySkimmerEffectSO"/>), and the Rhino is INVULNERABLE: its
    /// <see cref="VesselTimedElementalWard"/> (warding everything) stops danger prisms, blasts, gores, jousts and every
    /// scored hit, so it is neither drained, robbed nor silenced.
    ///
    /// <para><b>The length is EARNED by ploughing</b> (Garrett, 2026-11-12: "the duration should be based on the prisms
    /// ploughed since the last omnicrystal surge. this way they can't surge from crystal to crystal and they get rewarded
    /// for prisms ploughed"): <see cref="secondsPerPrism"/> for every prism the plough threw since the previous omni
    /// crystal (<see cref="RhinoStampede.CountPlough"/>), capped at <see cref="maxStampedeSeconds"/>. Every omni crystal
    /// resets the count, so a crystal taken with nothing ploughed since the last one is absorbed and does nothing
    /// else.</para>
    ///
    /// <para><b>Where it runs.</b> Omni-crystal collection resolves on the server and replays on a remote pilot's own
    /// machine (<c>OmniCrystalImpactor.ReplayOnRemoteOwner</c>), so the stampede is known on the Rhino's owner, which is
    /// the one machine that decides a gore (<see cref="ElementalTransfer.IsDecidedHere"/>) and the one whose
    /// <c>ResourceSystem</c> honours the ward. The plough runs on every machine that simulates the Rhino, so each counts
    /// its own ploughs. The throttle surge is the owner's own motion and replicates with the hull.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "RhinoStampedeByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/RhinoStampedeByCrystalEffectSO")]
    public sealed class RhinoStampedeByCrystalEffectSO : VesselCrystalEffectSO
    {
        [Header("Stampede")]
        [Tooltip("Seconds of stampede (surge, invulnerability, stealing gore) per prism the plough threw since the last " +
                 "omni crystal.")]
        [SerializeField, Min(0f)] float secondsPerPrism = 0.1f;

        [Tooltip("The longest a stampede lasts, however much was ploughed.")]
        [SerializeField, Min(0.1f)] float maxStampedeSeconds = 12f;

        [Tooltip("Multiplier on the crystal's own speed buff for the surge. 1 = the crystal's buff as authored; 0 = no " +
                 "surge, the stampede only sharpens the gore and wards the hull.")]
        [SerializeField, Min(0f)] float throttleBuffScale = 1f;

        /// <summary>The stampede <paramref name="ploughed"/> prisms buy, seconds (0 when nothing was ploughed).</summary>
        public static float SecondsFor(int ploughed, float secondsPerPrism, float maxSeconds) =>
            Mathf.Min(maxSeconds, Mathf.Max(0, ploughed) * Mathf.Max(0f, secondsPerPrism));

        static readonly HashSet<int> s_reportedNoWard = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetReported() => s_reportedNoWard.Clear();

    public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            var status = vesselImpactor && vesselImpactor.Vessel != null ? vesselImpactor.Vessel.VesselStatus : null;
            if (status == null) return;

            float seconds = SecondsFor(RhinoStampede.TakePloughCount(status), secondsPerPrism, maxStampedeSeconds);
            if (seconds <= 0f) return;

            RhinoStampede.Begin(status, seconds);

            if (!vesselImpactor.TryGetComponent(out VesselTimedElementalWard ward))
                ward = vesselImpactor.GetComponentInChildren<VesselTimedElementalWard>();
            if (ward) ward.Grant(seconds);
            else if (s_reportedNoWard.Add(vesselImpactor.GetInstanceID()))
                CSDebug.LogWarning($"[{nameof(RhinoStampedeByCrystalEffectSO)}] '{vesselImpactor.name}' carries no " +
                    $"{nameof(VesselTimedElementalWard)}, so its stampede is not invulnerable. Add one to the vessel root.",
                    vesselImpactor);

            var transformer = status.VesselTransformer;
            if (transformer && throttleBuffScale > 0f && data.SpeedBuffAmount > 0f)
                transformer.ModifyThrottle(data.SpeedBuffAmount * throttleBuffScale, seconds);
        }
    }

    /// <summary>
    /// Which Rhinos are stampeding on THIS machine, and until when. Written by the omni crystal on the machine that
    /// resolves it, read by the gore on the machine that decides it - the Rhino's owner both times. Also counts the prisms
    /// each Rhino's plough threw since its last omni crystal, which is what a stampede's length is bought with.
    /// </summary>
    public static class RhinoStampede
    {
        static readonly Dictionary<IVesselStatus, float> _until = new();
        static readonly List<IVesselStatus> _expired = new();
        static readonly Dictionary<IVesselStatus, int> _ploughed = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _until.Clear();
            _ploughed.Clear();
        }

        /// <summary>One prism thrown by <paramref name="rhino"/>'s plough (RhinoPloughPrismEffectSO).</summary>
        public static void CountPlough(IVesselStatus rhino)
        {
            if (rhino == null) return;
            _ploughed.TryGetValue(rhino, out int n);
            _ploughed[rhino] = n + 1;
        }

        /// <summary>The prisms <paramref name="rhino"/> ploughed since its last omni crystal, without resetting.</summary>
        public static int PloughCount(IVesselStatus rhino) =>
            rhino != null && _ploughed.TryGetValue(rhino, out int n) ? n : 0;

        /// <summary>The prisms ploughed since the last omni crystal, and the count starts again from zero.</summary>
        public static int TakePloughCount(IVesselStatus rhino)
        {
            int n = PloughCount(rhino);
            if (rhino != null) _ploughed.Remove(rhino);
            return n;
        }

        public static void Begin(IVesselStatus rhino, float seconds)
        {
            if (rhino == null || seconds <= 0f) return;
            Prune();
            _until[rhino] = Time.time + seconds;
        }

        public static bool IsActive(IVesselStatus rhino) =>
            rhino != null && _until.TryGetValue(rhino, out float until) && Time.time < until;

        static void Prune()
        {
            if (_until.Count < 8) return;
            float now = Time.time;
            _expired.Clear();
            foreach (var pair in _until)
                if (pair.Value <= now) _expired.Add(pair.Key);
            for (int i = 0; i < _expired.Count; i++) _until.Remove(_expired[i]);
            _expired.Clear();
        }
    }
}
