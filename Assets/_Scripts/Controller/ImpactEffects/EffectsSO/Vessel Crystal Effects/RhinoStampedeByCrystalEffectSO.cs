using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Rhino's OMNI-CRYSTAL ability, the <b>Stampede</b> (2026-11-12, replacing the slow blast it shared with the
    /// Swordfish): the omni crystal is absorbed into the hull (the fleet's absorb morph runs first) and for
    /// <see cref="stampedeSeconds"/> the Rhino charges - its throttle surges by the crystal's own speed buff and every
    /// gore takes at full Charge and steals (<see cref="RhinoGoreBySkimmerEffectSO"/>).
    ///
    /// <para><b>Where it runs.</b> Omni-crystal collection resolves on the server and replays on a remote pilot's own
    /// machine (<c>OmniCrystalImpactor.ReplayOnRemoteOwner</c>), so the stampede is known on the Rhino's owner, which is
    /// the one machine that decides a gore (<see cref="ElementalTransfer.IsDecidedHere"/>). The throttle surge is the
    /// owner's own motion and replicates with the hull. Nothing here is simulated per peer.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "RhinoStampedeByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/RhinoStampedeByCrystalEffectSO")]
    public sealed class RhinoStampedeByCrystalEffectSO : VesselCrystalEffectSO
    {
        [Header("Stampede")]
        [Tooltip("Seconds the stampede lasts: the throttle surge and the full-Charge stealing gore.")]
        [SerializeField, Min(0.1f)] float stampedeSeconds = 8f;

        [Tooltip("Multiplier on the crystal's own speed buff for the surge. 1 = the crystal's buff as authored; 0 = no " +
                 "surge, the stampede only sharpens the gore.")]
        [SerializeField, Min(0f)] float throttleBuffScale = 1f;

        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            var status = vesselImpactor && vesselImpactor.Vessel != null ? vesselImpactor.Vessel.VesselStatus : null;
            if (status == null) return;

            RhinoStampede.Begin(status, stampedeSeconds);
            var transformer = status.VesselTransformer;
            if (transformer && throttleBuffScale > 0f && data.SpeedBuffAmount > 0f)
                transformer.ModifyThrottle(data.SpeedBuffAmount * throttleBuffScale, stampedeSeconds);
        }
    }

    /// <summary>
    /// Which Rhinos are stampeding on THIS machine, and until when. Written by the omni crystal on the machine that
    /// resolves it, read by the gore on the machine that decides it - the Rhino's owner both times.
    /// </summary>
    public static class RhinoStampede
    {
        static readonly Dictionary<IVesselStatus, float> _until = new();
        static readonly List<IVesselStatus> _expired = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _until.Clear();

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
