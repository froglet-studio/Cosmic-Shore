using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Core;
using CosmicShore.Gameplay.Audio;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Any vessel's half of the PAYLOAD (Docs/Pilots/ARTIFACTS.md §2): <b>RB</b> beside your squad's
    /// Ark takes a parcel of its hull, the parcel rides on your hull where everyone can see it, and
    /// <b>RB</b> again releases it: a burst that runs the payload's prism effects (the plain one
    /// cracks super-shielded and shielded prisms) and spends the parcel.
    ///
    /// <para><b>On every vessel, by the platform law.</b> <see cref="VesselController.Initialize"/>
    /// ensures one (<see cref="EnsureOn"/>), so no hull can fail to author it and no mode can switch
    /// it off. It needs nothing from the hull: no vessel class appears anywhere in the payload.</para>
    ///
    /// <para><b>The button is <see cref="InputEvents.PayloadAction"/></b> (pad RB, keyboard E), an honest
    /// name for the shoulder the legacy <c>FlipAction</c> used to describe (Docs/REFACTOR_BACKLOG.md S73).
    /// Only the local human pilot's presses are heard; an AI hull never grabs.</para>
    ///
    /// <para><b>The parcel is real mass.</b> Its prisms stay in the spatial index, wear the Ark's domain
    /// and honour the mover contract every frame (<see cref="Prism.NotifyPositionChanged"/>), as the
    /// Ark's own hull does. Their colliders are held off while carried (<see cref="Prism.SetCarried"/>)
    /// so the carrier never strikes its own cargo. The parcel's root is NOT a child of the vessel: a
    /// pooled prism destroyed with a vessel's hierarchy would corrupt its pool, so the root follows
    /// the hull instead.</para>
    ///
    /// <para><b>A loaded payload.</b> When the slot this pilot flies carries an artifact
    /// (<see cref="VesselPilotLoadout.Artifact"/>), every payload taken is loaded with it: RB then releases the
    /// artifact's transformation (<see cref="PayloadTransformSO"/>) at the pilot's track levels instead of the plain
    /// burst. The artifact itself never leaves the Ark; the payload carries only its effect.</para>
    ///
    /// <para><b>Loose artifacts.</b> With nothing aboard, RB takes a loose <see cref="ArtifactPickup"/>
    /// in reach (any squad's: a spilled artifact is nobody's) before it asks the Ark for a parcel. With
    /// an artifact aboard, RB beside your own Ark stows it in that hold; anywhere else it lets it go.
    /// A vessel carries one thing at a time.</para>
    ///
    /// <para><b>Silence.</b> A silenced carrier's burst harms nothing: the crack asks
    /// <see cref="Prism.DeflectsSilenced"/> on every rival prism (<see cref="SuperShieldCrack"/>), the
    /// one silence gate on prism harm. The parcel is still spent.</para>
    /// </summary>
    public sealed class PayloadCarrier : MonoBehaviour
    {
        const float CellRebindSeconds = 2.5f;

        IVesselStatus _status;
        ScriptableEventInputEvents _presses;
        ArkHold _source;
        Transform _root;
        readonly List<Prism> _parcel = new();
        readonly List<Vector3> _seatScale = new();
        readonly List<(Prism prism, float dueAt)> _withering = new();
        float _nextRebindAt;
        ArtifactPickup _artifact;
        ArtifactSO _loaded;
        ArtifactLevels _loadedLevels;

        /// <summary>True while a parcel rides this vessel.</summary>
        public bool IsCarrying => _parcel.Count > 0;

        /// <summary>The artifact the riding payload is loaded with, or null for a plain payload.</summary>
        public ArtifactSO Loaded => IsCarrying ? _loaded : null;

        /// <summary>The loose artifact this vessel carries, or null.</summary>
        public ArtifactPickup Artifact => _artifact ? _artifact : null;

        /// <summary>Prisms in the parcel riding now.</summary>
        public int ParcelCount => _parcel.Count;

        /// <summary>The hold the parcel came from, or null.</summary>
        public ArkHold Source => _source;

        /// <summary>Ensure <paramref name="vessel"/> carries one, bound to its status.</summary>
        public static PayloadCarrier EnsureOn(VesselController vessel)
        {
            if (!vessel) return null;
            if (!vessel.TryGetComponent(out PayloadCarrier carrier))
                carrier = vessel.gameObject.AddComponent<PayloadCarrier>();
            carrier.Bind(vessel.VesselStatus);
            return carrier;
        }

        internal void Bind(IVesselStatus status)
        {
            _status = status;
            Unlisten();
        }

        void Update()
        {
            EnsureListening();
            TickParcel();
            TickWither();
        }

        void OnDestroy()
        {
            Unlisten();
            // A vessel that dies with an artifact aboard lets it go where it is: still loose, still anyone's.
            if (_artifact) _artifact.Drop();
            _artifact = null;
            StrikeParcel();
            for (int i = 0; i < _withering.Count; i++) ReleasePrism(_withering[i].prism);
            _withering.Clear();
        }

        // ── Input ────────────────────────────────────────────────────────────

        // The local human pilot only. Subscribed lazily: a player's InputStatus can arrive after
        // Initialize (a seat change, a hull handed to another player), and an AI hull never has one
        // worth hearing. Listening costs one reference compare per frame.
        void EnsureListening()
        {
            var player = _status?.Player;
            bool local = player != null && player.IsLocalUser;
            var channel = local ? _status.InputStatus?.OnButtonPressed : null;
            if (channel == _presses) return;
            Unlisten();
            _presses = channel;
            if (_presses) _presses.OnRaised += OnPressed;
        }

        void Unlisten()
        {
            if (_presses) _presses.OnRaised -= OnPressed;
            _presses = null;
        }

        void OnPressed(InputEvents ie)
        {
            if (ie != InputEvents.PayloadAction) return;
            if (_status == null || _status.AutoPilotEnabled) return;
            if (Artifact) DeliverArtifact();
            else if (IsCarrying) Release();
            else if (!TryTakeArtifact()) TryGrab();
        }

        // ── Grab ─────────────────────────────────────────────────────────────

        /// <summary>Ask the squad's Ark for a parcel. Returns the hold's answer.</summary>
        public ArkHold.GrabResult TryGrab()
        {
            if (_status == null || IsCarrying) return ArkHold.GrabResult.AlreadyCarrying;
            var at = transform.position;
            var hold = ArkHold.FindFor(_status.Domain, at);
            // No Ark for this squad here (every mode without one): RB does nothing, and says nothing.
            if (!hold) return ArkHold.GrabResult.NoArk;

            var result = hold.TryGrant(this, at, _parcel);
            switch (result)
            {
                case ArkHold.GrabResult.Granted:
                    Seat(hold);
                    Load();
                    Notify(PayloadNotice.Grabbed, _loaded && !_loaded.LoadSound.IsNull ? _loaded.LoadSound : ArkHold.Config.GrabSound);
                    break;
                case ArkHold.GrabResult.OutOfReach:
                    Notify(PayloadNotice.OutOfReach, ArkHold.Config.RefusedSound);
                    break;
                case ArkHold.GrabResult.HoldEmpty:
                    Notify(PayloadNotice.HoldEmpty, ArkHold.Config.RefusedSound);
                    break;
            }
            return result;
        }

        void Seat(ArkHold hold)
        {
            _source = hold;
            var cfg = ArkHold.Config;
            if (!_root)
            {
                var go = new GameObject("PayloadParcel");
                // Beside the Ark in the hierarchy, never under the vessel (see the summary).
                go.transform.SetParent(hold.transform.parent, false);
                _root = go.transform;
            }
            FollowHull();

            _seatScale.Clear();
            for (int i = 0; i < _parcel.Count; i++)
            {
                var prism = _parcel[i];
                prism.transform.SetParent(_root, true);
                prism.SetCarried(true);
                var scale = prism.transform.localScale * cfg.CarryScale;
                _seatScale.Add(scale);
                prism.TargetScale = scale;
            }
        }

        // The slot's artifact and the pilot's levels on it, read once per payload taken.
        void Load()
        {
            _loaded = _status?.PilotLoadout?.Artifact;
            _loadedLevels = _loaded ? PilotLoadoutStore.Levels(_loaded.Id) : default;
            if (_loaded && !_loaded.Transform) _loaded = null;
        }

        // ── Carry ────────────────────────────────────────────────────────────

        void FollowHull()
        {
            if (!_root) return;
            var cfg = ArkHold.Config;
            var hull = transform;
            _root.SetPositionAndRotation(hull.TransformPoint(cfg.CarryOffset), hull.rotation);
        }

        void TickParcel()
        {
            if (!IsCarrying) return;
            // The Ark fell, or a parcel prism was taken off the hull by someone else's force: what
            // is left still rides; an emptied parcel is simply gone.
            for (int i = _parcel.Count - 1; i >= 0; i--)
            {
                var prism = _parcel[i];
                if (prism && !prism.destroyed && prism.gameObject.activeInHierarchy) continue;
                _parcel.RemoveAt(i);
                _seatScale.RemoveAt(i);
            }
            if (!IsCarrying) { EndParcel(); return; }

            FollowHull();
            var cfg = ArkHold.Config;
            float step = cfg.SeatSpeed * Time.deltaTime;
            for (int i = 0; i < _parcel.Count; i++)
            {
                var prism = _parcel[i];
                var seat = SeatOffset(i, _parcel.Count, cfg.CarrySpacing);
                var t = prism.transform;
                t.localPosition = Vector3.MoveTowards(t.localPosition, seat, step);
                t.localRotation = Quaternion.RotateTowards(t.localRotation, Quaternion.identity, 360f * Time.deltaTime);
                prism.NotifyPositionChanged();
            }

            // Coarse cadence, as the Ark's own hull: re-bind each prism to the cell it is in now.
            if (Time.time >= _nextRebindAt)
            {
                _nextRebindAt = Time.time + CellRebindSeconds;
                var index = PrismSpatialIndex.Instance;
                if (index != null)
                    for (int i = 0; i < _parcel.Count; i++)
                        if (_parcel[i].SpatialIndexId >= 0) index.NotifyCellChanged(_parcel[i].SpatialIndexId);
            }
        }

        /// <summary>
        /// Where prism <paramref name="index"/> of <paramref name="count"/> sits in the parcel, local
        /// to its root: a compact ring around the root, its first prism at the centre, so a parcel
        /// reads as one bundle of cargo whatever its size.
        /// </summary>
        public static Vector3 SeatOffset(int index, int count, float spacing)
        {
            if (index <= 0 || count <= 1) return Vector3.zero;
            int ring = count - 1;
            float a = 2f * Mathf.PI * (index - 1) / ring;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * spacing;
        }

        // ── Loose artifacts ──────────────────────────────────────────────────

        /// <summary>Take the nearest loose artifact in reach. False when there is none, or the claim
        /// was refused (someone else holds it).</summary>
        public bool TryTakeArtifact()
        {
            if (_status == null || IsCarrying || Artifact) return false;
            var pickup = ArtifactPickup.FindNear(transform.position, ArkHold.Config.PickupReach);
            if (!pickup || !pickup.TryClaim(this)) return false;
            _artifact = pickup;
            Notify(PayloadNotice.ArtifactTaken, ArkHold.Config.ArtifactTakenSound);
            return true;
        }

        /// <summary>Stow the carried artifact in your squad's Ark when it is in reach, otherwise let it
        /// go where it is. True when it was stowed.</summary>
        public bool DeliverArtifact()
        {
            var pickup = Artifact;
            if (!pickup || _status == null) return false;
            var cfg = ArkHold.Config;
            var at = transform.position;
            var home = ArkHold.FindFor(_status.Domain, at);
            bool inReach = home && (home.Ark.Position - at).sqrMagnitude <= cfg.GrabReach * cfg.GrabReach;
            _artifact = null;
            if (inReach && home.Stow(pickup.ArtifactId))
            {
                pickup.Strike();
                Notify(PayloadNotice.ArtifactStowed, cfg.ArtifactStowedSound);
                return true;
            }
            pickup.Drop();
            Notify(PayloadNotice.ArtifactDropped, default);
            return false;
        }

        // ── Release ──────────────────────────────────────────────────────────

        /// <summary>Release the parcel where it rides: the burst runs the payload's effects, then the
        /// parcel is spent. Returns how many prisms the burst reached.</summary>
        public int Release()
        {
            if (!IsCarrying || _status == null) return 0;
            var cfg = ArkHold.Config;
            var center = _root ? _root.position : transform.position;
            var release = new PayloadRelease(center, transform.forward, cfg.BurstRadius,
                _status.PlayerName, _status.Domain);

            if (_loaded) return ReleaseLoaded(release);

            int reached = PayloadBurst.Detonate(release, cfg.BurstEffects);
            Spend(release);
            Notify(PayloadNotice.Released, cfg.ReleaseSound);
            return reached;
        }

        // The artifact's transformation takes the parcel and its root, or refuses and the payload stays aboard.
        int ReleaseLoaded(in PayloadRelease release)
        {
            var artifact = _loaded;
            var home = ArkHold.FindFor(_status.Domain, transform.position);
            var launch = new PayloadLaunch(release, _parcel, _root, artifact, _loadedLevels, home);
            if (!artifact.Transform.Release(launch))
            {
                RaiseLine(artifact.Transform.RefusedLine);
                Notify(PayloadNotice.Refused, ArkHold.Config.RefusedSound);
                return 0;
            }
            // Still flagged carried: a thrown parcel must not strike its thrower; the spend clears the flag.
            _parcel.Clear();
            _seatScale.Clear();
            _root = null;               // the transformation owns the root now
            _loaded = null;
            EndParcel();
            Notify(PayloadNotice.Released, artifact.ReleaseSound.IsNull ? ArkHold.Config.ReleaseSound : artifact.ReleaseSound);
            return 1;
        }

        void RaiseLine(string line)
        {
            var player = _status?.Player;
            if (player == null || !player.IsLocalUser || string.IsNullOrEmpty(line)) return;
            ArkHold.RaiseLocalLine(line);
        }

        void Spend(in PayloadRelease release)
        {
            PayloadBurst.SpendParcel(_parcel, release, _root ? _root.parent : null);
            _seatScale.Clear();
            EndParcel();
        }

        /// <summary>
        /// The parcel's Ark is being retired: spend the parcel where it rides, with no burst. Its
        /// prisms wither and go back to the pool the way a retired hull does.
        /// </summary>
        public void StrikeParcel()
        {
            if (!IsCarrying) { EndParcel(); return; }
            float due = Time.time + ArkHold.Config.WitherSeconds;
            for (int i = 0; i < _parcel.Count; i++)
            {
                var prism = _parcel[i];
                if (!prism || prism.destroyed) continue;
                prism.TargetScale = new Vector3(0.02f, 0.02f, 0.02f);
                _withering.Add((prism, due));
            }
            _parcel.Clear();
            _seatScale.Clear();
            EndParcel(keepRoot: _withering.Count > 0);
        }

        void EndParcel(bool keepRoot = false)
        {
            if (!IsCarrying) _loaded = null;
            if (_source) _source.NotifyParcelGone(this);
            _source = null;
            if (!keepRoot && _root) { Destroy(_root.gameObject); _root = null; }
        }

        void TickWither()
        {
            if (_withering.Count == 0) return;
            for (int i = _withering.Count - 1; i >= 0; i--)
            {
                var (prism, dueAt) = _withering[i];
                if (prism && Time.time < dueAt) continue;
                _withering.RemoveAt(i);
                ReleasePrism(prism);
            }
            if (_withering.Count == 0 && !IsCarrying && _root) { Destroy(_root.gameObject); _root = null; }
        }

        static void ReleasePrism(Prism prism)
        {
            if (!prism || prism.destroyed) return;
            prism.SetCarried(false);
            prism.transform.SetParent(null, true);
            if (!EnvironmentPrismPool.TryRelease(prism)) Destroy(prism.gameObject);
        }

        void Notify(PayloadNotice notice, FMODUnity.EventReference sound)
        {
            var player = _status?.Player;
            if (player == null || !player.IsLocalUser) return;
            FMODOneShotVolumeHelper.PlaySFXOneShot(sound, transform.position, 1f);
            ArkHold.RaiseLocalNotice(notice);
        }
    }
}
