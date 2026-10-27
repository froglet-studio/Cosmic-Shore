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
    /// no pilot and no artifact. Off the network (a menu vessel, an un-spawned test) the local choice is used directly.</para>
    ///
    /// <para><b>2026-10-27 (Garrett):</b> built on Shombith's loadout; the ability rewrites (<c>Is</c>, <c>Tuning</c>)
    /// and the artifact stat arithmetic (<c>Apply</c>, ranks) are gone with the pilots and artifacts that needed them.
    /// <see cref="R_VesselActionHandler.NetArtifacts"/> now carries the slotted artifact's catalog index plus one.</para>
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
        ushort _pilotCode;
        ulong _artifactCode;
        PilotSO _pilot;
        ArtifactSO _artifact;

        /// <summary>The seat or the slot changed (a peer's published loadout arrived, or a vessel swap).</summary>
        public event Action Changed;

        public bool IsInitialized => _initialized;

        /// <summary>The seated pilot, or null for an empty seat (no sight).</summary>
        public PilotSO Pilot => _pilot;

        /// <summary>The slotted artifact that loads this pilot's payloads, or null for plain payloads.</summary>
        public ArtifactSO Artifact => _artifact;

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
            _localPilotCode = 0;
            _localArtifactCode = 0;
            if (!_catalog || _status.Player == null || !_status.Player.IsLocalPilot || _status.IsInitializedAsAI) return;
            if (!VesselRequirement.IsConcrete(_class)) return;

            if (_catalog.TryGetPilot(PilotLoadoutStore.GetPilot(_class), out var pilot)
                && pilot.Requirement != null && pilot.Requirement.IsMetBy(_class, null))
                _localPilotCode = (ushort)(IndexOf(_catalog.Pilots, pilot) + 1);

            if (_catalog.TryGetArtifact(PilotLoadoutStore.GetSlottedArtifact(_class), out var artifact)
                && PilotLoadoutStore.Holds(artifact.Id)
                && artifact.Requirement != null && artifact.Requirement.IsMetBy(_class, null))
                _localArtifactCode = (ulong)(IndexOf(_catalog.Artifacts, artifact) + 1);
        }

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
            if (pilotCode == _pilotCode && artifactCode == _artifactCode
                && (_pilot || pilotCode == 0) && (_artifact || artifactCode == 0)) return;
            _pilotCode = pilotCode;
            _artifactCode = artifactCode;

            _pilot = _catalog && pilotCode > 0 && pilotCode <= _catalog.Pilots.Count ? _catalog.Pilots[pilotCode - 1] : null;
            _artifact = _catalog && artifactCode > 0 && artifactCode <= (ulong)_catalog.Artifacts.Count
                ? _catalog.Artifacts[(int)artifactCode - 1] : null;
            Changed?.Invoke();
        }
    }
}
