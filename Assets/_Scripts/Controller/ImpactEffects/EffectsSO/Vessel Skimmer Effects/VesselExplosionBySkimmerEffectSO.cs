using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using Obvious.Soap;
using UnityEngine;
using CosmicShore.UI;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Joust scoring effect. When a faster vessel's skimmer sweeps past a slower
    /// vessel - overtaking it - the faster vessel earns a joust point
    /// (<see cref="OnJoustCollision"/>) and an AOE explosion spawns at the impact.
    /// Points are scored only by overtaking an OPPONENT: overtaking a same-domain
    /// teammate produces no point, no explosion, and no game-feed post (the
    /// teammate is buffed instead by VesselOvertakeBySkimmerEffectSO).
    ///
    /// Networked flow (single authority per joust): physics detects the overlap
    /// independently on every machine, so Execute fires wherever it is observed -
    /// but only the machine that OWNS the scoring (impactee) vessel confirms the
    /// joust. It reports through NetworkVesselImpactor (ServerRpc → ClientRpc) and
    /// every machine then runs <see cref="ExecuteConfirmed"/> in lockstep: the
    /// server's OnJoustCollision raise is the one StatsManager records, so the
    /// game-feed toast can never appear without the score moving (and vice versa).
    /// </summary>
    [CreateAssetMenu(
        fileName = "VesselExplosionBySkimmer",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Skimmer/VesselExplosionBySkimmerEffectSO")]
    public class VesselExplosionBySkimmerEffectSO : VesselSkimmerEffectsSO
    {
        [SerializeField]
        ScriptableEventString OnJoustCollision;

        [Header("Explosion Settings")]
        [SerializeField] private AOEExplosion[] _aoePrefabs;
        [SerializeField] private float _minExplosionScale;
        [SerializeField] private float _maxExplosionScale;
        [SerializeField] private int _resourceIndex;
        [SerializeField] private Material _aoeExplosionMaterial;
        [SerializeField] private Vector3 _spawnOffset = new Vector3(0, 0, -5f);

        [Header("Anti-Spam")]
        [Tooltip("Minimum time between explosions from the same vessel hitting a skimmer.")]
        [SerializeField] private float _explosionCooldown = 0.15f;

        // Keyed by instance ID (not the impactor reference) so destroyed vessel
        // impactors are never retained by this static dictionary across scene loads.
        private static readonly Dictionary<int, float> _lastExplosionTimeByImpactor
            = new();

        // Instance-ID keys get reused across play sessions while Time.time restarts at 0 — a
        // stale stamp reads as a negative delta and suppresses the explosion outright.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _lastExplosionTimeByImpactor.Clear();

        public override void Execute(VesselImpactor impactor, SkimmerImpactor impactee)
        {
            if (impactor == null || impactor.Vessel == null)
                return;

            if (impactee == null || impactee.Skimmer.VesselStatus.Vessel == null)
                return;

            var impactorVessel = impactor.Vessel;
            var impacteeVessel = impactee.Skimmer.VesselStatus.Vessel;

            // Only trigger if the skimmer's vessel is faster than the impactor
            if (impacteeVessel.VesselStatus.Speed <= impactorVessel.VesselStatus.Speed)
                return;

            // Joust points are scored only by overtaking an opponent. Overtaking a
            // same-domain teammate buffs them (VesselOvertakeBySkimmerEffectSO) but
            // never scores - skip the explosion, joust point, and feed post.
            if (impacteeVessel.VesselStatus.Domain == impactorVessel.VesselStatus.Domain)
                return;

            var impacteeVesselImpactor = impacteeVessel.Transform.GetComponent<VesselImpactor>();
            if (impacteeVesselImpactor == null)
                return;

            // Networked session: only the impactee's owner machine may confirm the joust.
            // Its own vessel and skimmer are at their true (non-interpolated) positions
            // locally, making it the most reliable observer of its own sweep - and
            // exactly-once semantics fall out of ownership. Everyone else observed a
            // replica overlap and must stay silent; the confirmed effects reach them via
            // the ClientRpc broadcast instead.
            var impacteeNetImpactor = impacteeVessel.Transform.GetComponent<NetworkVesselImpactor>();
            if (impacteeNetImpactor != null && impacteeNetImpactor.IsSpawned)
            {
                if (!impacteeNetImpactor.IsOwner)
                    return;

                if (!TryPassCooldown(impacteeVesselImpactor))
                    return;

                var impactorNetImpactor = impactor.GetComponent<NetworkVesselImpactor>();
                if (impactorNetImpactor == null || !impactorNetImpactor.IsSpawned)
                    return;

                impacteeNetImpactor.ReportJoust(impactorNetImpactor);
                return;
            }

            // Offline / unspawned path - confirm directly on this machine.
            if (!TryPassCooldown(impacteeVesselImpactor))
                return;

            ExecuteConfirmed(impactor, impacteeVesselImpactor);
        }

        /// <summary>
        /// The confirmed half of the joust: AOE explosion, scoring raise, audio, and the
        /// game-feed post. In a networked session this runs on EVERY machine (server
        /// included) via NetworkVesselImpactor.ExecuteJoust_ClientRpc after the impactee's
        /// owner reported the joust; the server's OnJoustCollision raise is the one
        /// StatsManager records (single-writer), and RoundStats replication fans the count
        /// back out to all HUDs. Offline it runs directly from Execute.
        /// </summary>
        public void ExecuteConfirmed(VesselImpactor impactor, VesselImpactor impacteeVesselImpactor)
        {
            if (impactor == null || impactor.Vessel == null)
                return;

            if (impacteeVesselImpactor == null || impacteeVesselImpactor.Vessel == null)
                return;

            var impactorVessel = impactor.Vessel;
            var impacteeVessel = impacteeVesselImpactor.Vessel;

            ExplosionHelper.CreateExplosion(
                _aoePrefabs,
                impacteeVesselImpactor,
                _minExplosionScale,
                _maxExplosionScale,
                _aoeExplosionMaterial,
                _resourceIndex,
                _spawnOffset);

            OnJoustCollision.Raise(impacteeVessel.VesselStatus.PlayerName);

            // Play audio for the local player - skimmer owner scored, impactor received.
            if (impacteeVessel.VesselStatus.IsLocalUser)
                AudioSystem.Instance?.PlayGameplaySFX(GameplaySFXCategory.JoustScored);
            else if (impactorVessel.VesselStatus.IsLocalUser)
                AudioSystem.Instance?.PlayGameplaySFX(GameplaySFXCategory.JoustReceived);

            // Post the joust situation to the in-game toast feed. Copy + points formatting
            // live in the Joust mode's GameToastConfigSO, resolved by GameToastController.
            GameToastAPI.PostJoust(
                impacteeVessel.VesselStatus.PlayerName,
                impacteeVessel.VesselStatus.Domain,
                impactorVessel.VesselStatus.PlayerName,
                impactorVessel.VesselStatus.Domain);
        }

        /// <summary>
        /// Anti-spam gate, keyed per impactee vessel. Runs only on the machine that is
        /// about to confirm the joust (the impactee's owner, or the sole machine when
        /// offline) - the broadcast path needs no cooldown because a single authority
        /// already guarantees one confirmation per pass.
        /// </summary>
        bool TryPassCooldown(VesselImpactor impacteeVesselImpactor)
        {
            var now = Time.time;
            int impacteeId = impacteeVesselImpactor.GetInstanceID();
            if (_lastExplosionTimeByImpactor.TryGetValue(impacteeId, out var lastTime)
                && now - lastTime < _explosionCooldown)
                return false;

            _lastExplosionTimeByImpactor[impacteeId] = now;
            return true;
        }
    }
}