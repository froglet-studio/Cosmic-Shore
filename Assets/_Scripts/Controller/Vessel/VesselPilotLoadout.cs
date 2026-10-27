using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The pilot in this vessel's seat and the artifacts slotted on it, for the match (Docs/Pilots/PILOTS_AND_ARTIFACTS.md).
    /// Every pilot rewrites one ability and every artifact moves one number, so the vessel's own code asks this, at use
    /// time: <see cref="Is"/> ("is Tomahawk in the seat?"), <see cref="Apply"/> ("this stat, with my artifacts") and
    /// <see cref="Tuning{T}"/> (the seated pilot's own numbers, on its <see cref="PilotSO.Tuning"/> asset).
    ///
    /// <para><b>Who decides.</b> The owning machine of the LOCAL HUMAN's vessel reads the player's Hangar choice
    /// (<see cref="PilotLoadoutStore"/>) and publishes it on <see cref="R_VesselActionHandler.NetPilot"/> /
    /// <see cref="R_VesselActionHandler.NetArtifacts"/>; every other peer reads those, so all machines run the same pilot
    /// and the same numbers (a pilot changes outcomes every peer simulates). An AI flies its vessel's base kit with no
    /// artifacts. Off the network (a menu vessel, an un-spawned test) the local choice is used directly.</para>
    ///
    /// <para>Lives on the vessel root, created lazily by <see cref="VesselStatus.PilotLoadout"/> (the GetOrAdd pattern
    /// of the elemental handler), so no prefab wiring is needed. State lives here, never on a shared action SO.</para>
    /// </summary>
    public class VesselPilotLoadout : MonoBehaviour
    {
        /// <summary>Slots <see cref="R_VesselActionHandler.NetArtifacts"/> can carry (four 16-bit fields).</summary>
        public const int MaxSlots = 4;

        IVesselStatus _status;
        R_VesselActionHandler _net;
        PilotArtifactCatalogSO _catalog;
        VesselClassType _class = VesselClassType.Any;
        bool _initialized;

        // The local choice, encoded the way the network carries it; what queries see is decoded from the active codes.
        ushort _localPilotCode;
        ulong _localArtifactCode;
        ushort _pilotCode;
        ulong _artifactCode;
        PilotSO _pilot;
        readonly Dictionary<ArtifactStat, (ArtifactSO artifact, int rank)> _artifacts = new();
        readonly List<string> _slottedScratch = new();

        /// <summary>The seat or the slots changed (a peer's published loadout arrived, or a vessel swap).</summary>
        public event Action Changed;

        public bool IsInitialized => _initialized;

        /// <summary>The seated pilot, or null for the base kit.</summary>
        public PilotSO Pilot => _pilot;

        /// <summary>True when the pilot with this id (a <see cref="PilotIds"/> constant) is in the seat.</summary>
        public bool Is(string pilotId) => _pilot && _pilot.Id == pilotId;

        /// <summary>The seated pilot's tuning asset when it is a <typeparamref name="T"/>, else null.</summary>
        public T Tuning<T>() where T : ScriptableObject => _pilot ? _pilot.Tuning as T : null;

        /// <summary>The rank (1..3) of the slotted artifact that moves <paramref name="stat"/>, 0 when none does.</summary>
        public int Rank(ArtifactStat stat) => _artifacts.TryGetValue(stat, out var a) ? a.rank : 0;

        /// <summary><paramref name="baseValue"/> moved by the slotted artifact for <paramref name="stat"/>; unchanged when
        /// none is slotted. Elements still scale on top: apply this to the BASE, then the element.</summary>
        public float Apply(ArtifactStat stat, float baseValue) =>
            _artifacts.TryGetValue(stat, out var a) ? a.artifact.Apply(baseValue, a.rank) : baseValue;

        /// <summary><see cref="Apply"/> for a count (missiles, switches, charges), rounded to the nearest whole one.</summary>
        public int ApplyCount(ArtifactStat stat, int baseValue) => Mathf.RoundToInt(Apply(stat, baseValue));

        // ---- null-safe helpers for code that holds an IVesselStatus ----

        public static bool IsPilot(IVesselStatus status, string pilotId) =>
            status != null && status.PilotLoadout is { } l && l.Is(pilotId);

        public static T TuningOf<T>(IVesselStatus status) where T : ScriptableObject =>
            status != null && status.PilotLoadout is { } l ? l.Tuning<T>() : null;

        public static float Apply(IVesselStatus status, ArtifactStat stat, float baseValue) =>
            status != null && status.PilotLoadout is { } l ? l.Apply(stat, baseValue) : baseValue;

        public static int ApplyCount(IVesselStatus status, ArtifactStat stat, int baseValue) =>
            status != null && status.PilotLoadout is { } l ? l.ApplyCount(stat, baseValue) : baseValue;

        /// <summary>Idempotent: safe to call again on a vessel swap or re-init.</summary>
        public void Initialize(IVesselStatus status)
        {
            if (status == null) return;
            _status = status;
            _net = status.ActionHandler;
            _catalog = PilotArtifactCatalogSO.Load();
            _initialized = true;
            ResolveLocalChoice();
            Refresh();
        }

        bool ReadsFromNetwork => _net && _net.IsSpawned && !_net.IsOwner;

        /// <summary>The local human's Hangar choice for this hull, encoded; an AI (or anyone else's vessel) flies the base kit.</summary>
        void ResolveLocalChoice()
        {
            _class = _status.VesselType;
            _localPilotCode = 0;
            _localArtifactCode = 0;
            if (!_catalog || _status.Player == null || !_status.Player.IsLocalPilot || _status.IsInitializedAsAI) return;
            if (!VesselRequirement.IsConcrete(_class)) return;

            string pilotId = PilotLoadoutStore.GetPilot(_class);
            if (_catalog.TryGetPilot(pilotId, out var pilot) && !pilot.IsDefault
                && pilot.Requirement != null && pilot.Requirement.IsMetBy(_class, null))
                _localPilotCode = (ushort)(IndexOf(pilot) + 1);

            PilotLoadoutStore.GetSlotted(_class, _slottedScratch);
            int slots = Mathf.Min(_catalog.SlotsPerVessel, MaxSlots), used = 0;
            foreach (var id in _slottedScratch)
            {
                if (used >= slots) break;
                if (!_catalog.TryGetArtifact(id, out var artifact) || artifact.Requirement == null
                    || !artifact.Requirement.IsMetBy(_class, null)) continue;
                int rank = PilotLoadoutStore.FoundRank(artifact.Id);
                if (rank <= 0 || artifact.Stat == ArtifactStat.None) continue;
                _localArtifactCode |= (ulong)Encode(artifact.Stat, rank) << (16 * used);
                used++;
            }
        }

        int IndexOf(PilotSO pilot)
        {
            var pilots = _catalog.Pilots;
            for (int i = 0; i < pilots.Count; i++)
                if (pilots[i] == pilot) return i;
            return -1;
        }

        static ushort Encode(ArtifactStat stat, int rank) => (ushort)((((int)stat & 0xFFF) << 4) | (rank & 0xF));

        // The owner re-asserts every frame (a vessel's first frames can come before its NetworkObject spawns, when an
        // owner write is not possible yet); a settled vessel costs two compares and no network traffic.
        void LateUpdate()
        {
            if (!_initialized || _status == null) return;
            if (_status.VesselType != _class) ResolveLocalChoice();   // the Vessel Changer swapped the hull
            if (_net && _net.IsSpawned && _net.IsOwner)
            {
                if (_net.NetPilot.Value != _localPilotCode) _net.NetPilot.Value = _localPilotCode;
                if (_net.NetArtifacts.Value != _localArtifactCode) _net.NetArtifacts.Value = _localArtifactCode;
            }
            Refresh();
        }

        void Refresh()
        {
            ushort pilotCode = ReadsFromNetwork ? _net.NetPilot.Value : _localPilotCode;
            ulong artifactCode = ReadsFromNetwork ? _net.NetArtifacts.Value : _localArtifactCode;
            if (pilotCode == _pilotCode && artifactCode == _artifactCode && (_pilot || pilotCode == 0)) return;
            _pilotCode = pilotCode;
            _artifactCode = artifactCode;

            _pilot = null;
            if (_catalog && pilotCode > 0 && pilotCode <= _catalog.Pilots.Count) _pilot = _catalog.Pilots[pilotCode - 1];

            _artifacts.Clear();
            if (_catalog)
                for (int slot = 0; slot < MaxSlots; slot++)
                {
                    int field = (int)((artifactCode >> (16 * slot)) & 0xFFFF);
                    var stat = (ArtifactStat)(field >> 4);
                    int rank = field & 0xF;
                    if (rank == 0 || stat == ArtifactStat.None || _artifacts.ContainsKey(stat)) continue;
                    foreach (var a in _catalog.Artifacts)
                        if (a && a.Stat == stat) { _artifacts[stat] = (a, Mathf.Clamp(rank, 1, ArtifactSO.RankCount)); break; }
                }
            Changed?.Invoke();
        }
    }
}
