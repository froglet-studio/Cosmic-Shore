using System;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The pilot in this vessel's seat and the artifact in its slot, for the match (Docs/Pilots/CALLSIGNS.md,
    /// Docs/Pilots/ARTIFACTS.md). The pilot gives the hull its sight on LB (<see cref="PilotSight"/> reads
    /// <see cref="Pilot"/>), and the artifact loads every payload the pilot takes from the Ark
    /// (<see cref="PayloadCarrier"/> reads <see cref="Artifact"/>). Neither changes the vessel's kit or numbers.
    ///
    /// <para><b>Who decides.</b> The owning machine of the LOCAL HUMAN's vessel reads the player's Hangar choice
    /// (<see cref="PilotLoadoutStore"/>) and publishes it on <see cref="R_VesselActionHandler.NetPilot"/> /
    /// <see cref="R_VesselActionHandler.NetArtifacts"/>; every other peer reads those. Other players see your pilot
    /// (its callsign plate, CALLSIGNS.md §4.2), so the seat is shared even though the sight is local. An AI flies with
    /// no pilot and no artifact, unless it is seated in a deployed squad (<see cref="SquadDeployment"/>): then it flies
    /// its slot's pilot and artifact, as every seated vessel does. A human's vessel always has a pilot (an unset seat flies the hull's first offered pilot);
    /// an arena match loads no artifact (<see cref="ArtifactsAllowedHere"/>). Off the network (a menu vessel, an un-spawned test) the local choice is used directly.</para>
    ///
    /// <para><b>2026-10-27 (Garrett):</b> built on Shombith's loadout; the ability rewrites (<c>Is</c>, <c>Tuning</c>)
    /// and the artifact stat arithmetic (<c>Apply</c>, ranks) are gone with the pilots and artifacts that needed them.</para>
    ///
    /// <para><b>2026-11-07: unique pilots and artifacts</b> (Docs/Pilots/INVENTORY.md §5). The seat holds a person
    /// (<see cref="Person"/>, local only) whose callsign is <see cref="Pilot"/>; <see cref="R_VesselActionHandler.NetPilot"/>
    /// still carries the callsign's catalog index plus one, since only the callsign plays. The slot holds one unique
    /// artifact, and <see cref="R_VesselActionHandler.NetArtifacts"/> carries it whole (<see cref="ArtifactWire"/>: its kind,
    /// its attuned element and all four levels), so every peer fires the same payload without its record.</para>
    ///
    /// <para>Lives on the vessel root, created lazily by <see cref="VesselStatus.PilotLoadout"/>, so no prefab wiring is
    /// needed.</para>
    /// </summary>
    public class VesselPilotLoadout : MonoBehaviour
    {
        IVesselStatus _status;
        R_VesselActionHandler _net;
        PilotArtifactCatalogSO _catalog;
        VesselClassType _class = VesselClassType.Any;
        bool _initialized;

        ushort _localPilotCode;
        ulong _localArtifactCode;
        int _deploymentVersion = -1;
        ushort _pilotCode;
        ulong _artifactCode;
        PilotSO _pilot;
        ArtifactSO _artifact;
        ArtifactLevels _levels;
        PilotRecord _person;

        /// <summary>The seat or the slot changed (a peer's published loadout arrived, or a vessel swap).</summary>
        public event Action Changed;

        public bool IsInitialized => _initialized;

        /// <summary>The seated pilot; null only for an AI (no sight).</summary>
        public PilotSO Pilot => _pilot;

        /// <summary>The slotted artifact's KIND (its payload transformation), or null for plain payloads.</summary>
        public ArtifactSO Artifact => _artifact;

        /// <summary>The slotted artifact's four track levels (what its transform reads); zero with no artifact.</summary>
        public ArtifactLevels ArtifactLevels => _levels;

        /// <summary>The unique person in the seat, on the machine whose crew they belong to; null elsewhere and for an AI
        /// with no deployed seat.</summary>
        public PilotRecord Person => _person;

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

        /// <summary>The local human's Hangar choice for this hull, encoded; an AI (or anyone else's vessel) has none.</summary>
        void ResolveLocalChoice()
        {
            _class = _status.VesselType;
            _deploymentVersion = SquadDeployment.Version;
            _localPilotCode = 0;
            _localArtifactCode = 0;
            _person = null;
            if (!_catalog || _status.Player == null) return;

            // A deployed squad's seat flies its SLOT's cards, the AI wingmates' included (Docs/Voyages/SLICE.md §2):
            // every vessel needs a pilot, and each brings its own artifact.
            if (SquadDeployment.TryGetSeat(_status, out var slot, out _))
            {
                if (!_catalog.TryGetPilot(slot.PilotId, out var seated)) seated = _catalog.FirstPilotFor(_class, null);
                if (seated) _localPilotCode = (ushort)(IndexOf(_catalog.Pilots, seated) + 1);
                PilotLoadoutStore.TryGetPilot(slot.PilotId, out _person);
                _localArtifactCode = Encode(slot.ArtifactId);
                return;
            }

            if (!_status.Player.IsLocalPilot || _status.IsInitializedAsAI) return;
            if (!VesselRequirement.IsConcrete(_class)) return;

            // Every vessel needs a pilot (Garrett, 2026-10-27): an unset or retired Hangar seat flies the first pilot
            // the hull is offered.
            string seat = PilotLoadoutStore.GetPilot(_class);
            if (!_catalog.TryGetPilot(seat, out var pilot)
                || pilot.Requirement == null || !pilot.Requirement.IsMetBy(_class, null))
            {
                pilot = _catalog.FirstPilotFor(_class, null);
                seat = pilot ? pilot.Id : null;   // the crew's person on that callsign, if any
            }
            if (pilot) _localPilotCode = (ushort)(IndexOf(_catalog.Pilots, pilot) + 1);
            PilotLoadoutStore.TryGetPilot(seat, out _person);

            string slotted = PilotLoadoutStore.GetSlottedArtifact(_class);
            if (_catalog.TryGetArtifact(slotted, out var kind) && kind.Requirement != null && kind.Requirement.IsMetBy(_class, null))
                _localArtifactCode = Encode(slotted);
        }

        /// <summary>The held artifact <paramref name="artifactId"/> names, packed whole for the network; 0 when the hoard
        /// does not hold it (lost, or a stale id).</summary>
        ulong Encode(string artifactId)
        {
            if (!PilotLoadoutStore.TryGetArtifact(artifactId, out var held)) return 0;
            if (!_catalog.TryGetArtifact(held.Kind(), out var kind)) return 0;
            return ArtifactWire.Pack(IndexOf(_catalog.Artifacts, kind), held);
        }

        /// <summary>False in an arena match (the card's <c>ArenaRules</c>): no artifact loads a payload there.</summary>
        static bool ArtifactsAllowedHere => CrewStations.GameData == null || !CrewStations.GameData.IsArenaMatch;

        static int IndexOf<T>(System.Collections.Generic.IReadOnlyList<T> list, T item) where T : class
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == item) return i;
            return -1;
        }

        // The owner re-asserts every frame (a vessel's first frames can come before its NetworkObject spawns, when an
        // owner write is not possible yet); a settled vessel costs two compares and no network traffic.
        void LateUpdate()
        {
            if (!_initialized || _status == null) return;
            if (_status.VesselType != _class || _deploymentVersion != SquadDeployment.Version)
                ResolveLocalChoice();   // the Vessel Changer swapped the hull, or a squad was deployed or recalled
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
            if (pilotCode == _pilotCode && artifactCode == _artifactCode
                && (_pilot || pilotCode == 0) && (_artifact || artifactCode == 0)) return;
            _pilotCode = pilotCode;
            _artifactCode = artifactCode;

            _pilot = _catalog && pilotCode > 0 && pilotCode <= _catalog.Pilots.Count ? _catalog.Pilots[pilotCode - 1] : null;
            // Arena modes do not support artifacts; voyages support them without requiring one (Garrett, 2026-10-27).
            // The card's arena rule reaches every peer, so each one drops the artifact the same way.
            if (!ArtifactsAllowedHere) artifactCode = 0;
            _artifact = null;
            _levels = default;
            if (_catalog && ArtifactWire.TryUnpack(artifactCode, out int kindIndex, out _, out var levels, out _)
                && kindIndex < _catalog.Artifacts.Count)
            {
                _artifact = _catalog.Artifacts[kindIndex];
                _levels = levels;
            }
            Changed?.Invoke();
        }
    }
}
