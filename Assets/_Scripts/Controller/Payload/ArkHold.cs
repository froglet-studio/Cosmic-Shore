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
    ///
    /// <para><b>The hold also carries the voyage's artifacts</b> (ARTIFACTS.md §5), by artifact id
    /// (the id <c>SquadSlot.ArtifactId</c> names). When the Ark falls the hold settles them in one
    /// step (<see cref="ArkFall"/>): all of them to the credited squad's Ark, or, when fauna took
    /// the last hull prism, spilled as <see cref="ArtifactPickup"/>s anyone can take with RB.</para>
    /// </summary>
    public sealed class ArkHold : MonoBehaviour
    {
        const string ConfigResourcePath = "PayloadConfig";

        static readonly List<ArkHold> s_holds = new();
        static PayloadConfigSO s_config;
        static bool s_configResolved;

        Ark _ark;
        readonly List<PayloadCarrier> _carriers = new();
        readonly List<string> _artifacts = new();
        bool _settled;

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

        /// <summary>The artifacts aboard, by id.</summary>
        public IReadOnlyList<string> Artifacts => _artifacts;

        internal void Bind(Ark ark)
        {
            if (_ark) _ark.HullDestroyed -= OnHullDestroyed;
            _ark = ark;
            _settled = false;
            if (!s_holds.Contains(this)) s_holds.Add(this);
            if (!_ark) return;
            _ark.HullDestroyed += OnHullDestroyed;
            // Until squad slots load their artifacts into the hold (phase 3), a tester can put
            // cargo aboard every Ark from the config (it ships empty).
            var cargo = Config.TestCargo;
            for (int i = 0; i < cargo.Length; i++) Stow(cargo[i]);
        }

        /// <summary>Put an artifact aboard. Refused off the authority, for an empty id, or once the
        /// Ark has fallen (its hold has been settled).</summary>
        public bool Stow(string artifactId)
        {
            if (!IsAuthority || _settled || string.IsNullOrEmpty(artifactId)) return false;
            if (_ark && _ark.IsHullLost) return false;
            _artifacts.Add(artifactId);
            return true;
        }

        /// <summary>Move every artifact aboard into <paramref name="into"/> and empty the hold.
        /// Returns how many moved.</summary>
        internal int TakeAll(List<string> into)
        {
            int n = _artifacts.Count;
            into.AddRange(_artifacts);
            _artifacts.Clear();
            return n;
        }

        void OnHullDestroyed()
        {
            if (_settled || !IsAuthority) return;
            _settled = true;
            ArkFall.Settle(this);
        }

        /// <summary>True once the fallen Ark's hold has been settled.</summary>
        public bool IsSettled => _settled;

        void OnEnable()
        {
            if (!s_holds.Contains(this)) s_holds.Add(this);
        }

        void OnDisable() => s_holds.Remove(this);

        void OnDestroy()
        {
            if (_ark) _ark.HullDestroyed -= OnHullDestroyed;
            StrikeParcels();
        }

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

        /// <summary>The hold of <paramref name="domain"/>'s standing Ark, wherever it is, or null.
        /// A probe: the credited squad may have no Ark here.</summary>
        public static ArkHold FindHome(Domains domain, ArkHold except = null)
        {
            for (int i = 0; i < s_holds.Count; i++)
            {
                var hold = s_holds[i];
                if (!hold || hold == except || !hold._ark || hold._ark.IsHullLost || hold.Domain != domain) continue;
                return hold;
            }
            return null;
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
        ArtifactTaken = 4,
        ArtifactStowed = 5,
        ArtifactDropped = 6,
    }
}
