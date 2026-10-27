using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Ark's HOLD (Docs/Pilots/ARTIFACTS.md §2, §8.1): where a squad's payloads come from. A
    /// payload is a parcel of the Ark's own hull, so the hold hands out real hull prisms, moved off
    /// the ship and never minted (<see cref="Ark.TakeHullPrisms"/>). Every payload taken thins the
    /// hull that protects everything aboard: that is the payload's whole cost.
    ///
    /// <para><b>The hold decides every grab.</b> A grab is a transfer of the squad's hull, so it is
    /// arbitrated in one place: the machine that holds the Ark. Today every Ark is the local
    /// freestyle toy's (the Arkway, the Wander toy's "With Ark"), so the holder is this machine and
    /// <see cref="IsAuthority"/> is true; a networked Ark (the voyage mode, phase 6) makes the hold a
    /// server object and the grab a request to it. Nothing else may move a parcel.</para>
    ///
    /// <para><b>One Ark per squad.</b> A squad is a domain here, as it is everywhere a vessel's side is
    /// asked: a vessel may take a payload only from the Ark of its own domain.</para>
    /// </summary>
    public sealed class ArkHold : MonoBehaviour
    {
        const string ConfigResourcePath = "PayloadConfig";

        static readonly List<ArkHold> s_holds = new();
        static PayloadConfigSO s_config;
        static bool s_configResolved;

        Ark _ark;
        readonly List<PayloadCarrier> _carriers = new();

        /// <summary>Why a grab was refused, or <see cref="Granted"/>.</summary>
        public enum GrabResult
        {
            Granted = 0,
            NoArk = 1,
            OutOfReach = 2,
            HoldEmpty = 3,
            AlreadyCarrying = 4,
            NotAuthority = 5,
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_holds.Clear();
            s_config = null;
            s_configResolved = false;
            LocalNotice = null;
        }

        /// <summary>The live payload tuning (the Resources asset, or its defaults when missing).</summary>
        public static PayloadConfigSO Config
        {
            get
            {
                if (!s_configResolved)
                {
                    s_config = Resources.Load<PayloadConfigSO>(ConfigResourcePath);
                    if (s_config == null)
                    {
                        CSDebug.LogWarning($"[ArkHold] No Resources/{ConfigResourcePath} asset - the payload runs " +
                                           "on PayloadConfigSO defaults and its burst carries no effect. Run " +
                                           "Tools/Build/author_payload_assets.py.");
                        s_config = ScriptableObject.CreateInstance<PayloadConfigSO>();
                    }
                    s_configResolved = true;
                }
                return s_config;
            }
        }

        public Ark Ark => _ark;
        public Domains Domain => _ark ? _ark.Domain : default;

        /// <summary>True on the machine that decides this hold's grabs (see the summary).</summary>
        public bool IsAuthority => true;

        /// <summary>Payloads off this hold riding a carrier right now.</summary>
        public int ParcelsOut => _carriers.Count;

        internal void Bind(Ark ark) => _ark = ark;

        void OnEnable()
        {
            if (!s_holds.Contains(this)) s_holds.Add(this);
        }

        void OnDisable() => s_holds.Remove(this);

        void OnDestroy() => StrikeParcels();

        /// <summary>The hold of <paramref name="domain"/>'s Ark nearest <paramref name="position"/>,
        /// or null. A probe: no squad having an Ark here is the ordinary case, not a fault.</summary>
        public static ArkHold FindFor(Domains domain, Vector3 position)
        {
            ArkHold best = null;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < s_holds.Count; i++)
            {
                var hold = s_holds[i];
                if (!hold || !hold._ark || hold._ark.IsHullLost || hold.Domain != domain) continue;
                float d = (hold._ark.Position - position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = hold; }
            }
            return best;
        }

        /// <summary>
        /// Decide a grab: hand <paramref name="carrier"/> a parcel of hull prisms (into
        /// <paramref name="into"/>), or say why not. In reach means the carrier's position is within
        /// the configured reach of the Ark's centre.
        /// </summary>
        public GrabResult TryGrant(PayloadCarrier carrier, Vector3 carrierPosition, List<Prism> into)
        {
            if (!IsAuthority) return GrabResult.NotAuthority;
            if (!_ark || _ark.IsHullLost) return GrabResult.NoArk;
            if (!carrier || carrier.IsCarrying || _carriers.Contains(carrier)) return GrabResult.AlreadyCarrying;

            var cfg = Config;
            if ((carrierPosition - _ark.Position).sqrMagnitude > cfg.GrabReach * cfg.GrabReach)
                return GrabResult.OutOfReach;

            int taken = _ark.TakeHullPrisms(cfg.ParcelPrisms, carrierPosition, cfg.HullReserve, into);
            if (taken <= 0) return GrabResult.HoldEmpty;

            _carriers.Add(carrier);
            return GrabResult.Granted;
        }

        /// <summary>A carrier's parcel is gone (released, or struck): it no longer rides off this hold.</summary>
        internal void NotifyParcelGone(PayloadCarrier carrier) => _carriers.Remove(carrier);

        /// <summary>
        /// The Ark is being retired (the voyage ended): every parcel still riding a carrier is spent
        /// where it is, without a burst. It is hull the Ark no longer holds, so its retire would never
        /// reach it.
        /// </summary>
        public void StrikeParcels()
        {
            for (int i = _carriers.Count - 1; i >= 0; i--)
            {
                var carrier = _carriers[i];
                if (carrier) carrier.StrikeParcel();
            }
            _carriers.Clear();
        }

        /// <summary>Test seam: forget every registered hold.</summary>
        internal static void ClearRegistryForTests() => s_holds.Clear();

        /// <summary>Test seam: run on a given config.</summary>
        internal static void SetConfigForTests(PayloadConfigSO config)
        {
            s_config = config;
            s_configResolved = config != null;
        }

        /// <summary>The holds standing now (diagnostics).</summary>
        public static int Count => s_holds.Count;

        /// <summary>Raised on this machine whenever a local pilot's payload changes, for a HUD.</summary>
        public static event Action<PayloadNotice> LocalNotice;

        internal static void RaiseLocalNotice(PayloadNotice notice) => LocalNotice?.Invoke(notice);
    }

    /// <summary>What just happened to the local pilot's payload (a HUD reads the authored line for it).</summary>
    public enum PayloadNotice
    {
        Grabbed = 0,
        Released = 1,
        OutOfReach = 2,
        HoldEmpty = 3,
    }
}
