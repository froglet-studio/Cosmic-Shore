using UnityEngine;
using System.Collections.Generic;
using CosmicShore.Game.Arcade;
using CosmicShore.Game.Assemblers;
using CosmicShore.Game.Environment;
using CosmicShore.Game.FX;
using CosmicShore.Game.IO;
using CosmicShore.Game.ImpactEffects;
using CosmicShore.Game.Managers;
using CosmicShore.Game.Multiplayer;
using CosmicShore.Game.Player;
using CosmicShore.Game.Prisms;
using CosmicShore.Game.Projectiles;
using CosmicShore.Game.Ship;
using CosmicShore.Game.UI;
using CosmicShore.Integrations.Playfab;
using CosmicShore.MinigameHUD.Controller;
using CosmicShore.Models;
using CosmicShore.Models.Enums;
using CosmicShore.Models.ScriptableObjects;
using CosmicShore.UI.Elements;
using CosmicShore.UI.Modals;
using CosmicShore.UI.Views;
using CosmicShore.Utility;
using CosmicShore.Utility.DataContainers;
using CosmicShore.Utility.Effects;
using CosmicShore.Utility.SOAP;
using CosmicShore.Utility.Tools;
using CosmicShore.VesselHUD.Controller;
using CosmicShore.VesselHUD.Interfaces;
using CosmicShore.VesselHUD.View;


namespace CosmicShore.Game.Environment
{
    /// <summary>
    /// Manages a group of <see cref="Worm"/> creatures.
    /// Handles spawning, periodic growth, and target updates.
    /// Extends Fauna for domain/goal propagation from the spawning system (LSP-compliant:
    /// lifecycle methods use base defaults instead of throwing NotImplementedException).
    /// </summary>
    public class WormManager : Fauna
    {
        [Header("Worm Prefabs")]
        [SerializeField] Worm wormPrefab;
        [SerializeField] Worm emptyWormPrefab;

        [Header("Spawn Settings")]
        [SerializeField] int initialWormCount = 3;
        [SerializeField] float spawnRadius = 50f;

        [Header("Behavior Settings")]
        [SerializeField] float growthInterval = 10f;
        [SerializeField] float targetUpdateInterval = 5f;

        Vector3 headSpacing;
        Vector3 tailSpacing;
        Vector3 middleSpacing;

        readonly List<Worm> activeWorms = new();
        float growthTimer;
        float targetUpdateTimer;

        protected override void Start()
        {
            base.Start();
            CacheSegmentSpacing();
            SpawnInitialWorms();
        }

        void CacheSegmentSpacing()
        {
            var segments = wormPrefab.initialSegments;
            headSpacing = segments[0].transform.position - segments[1].transform.position;
            tailSpacing = segments[segments.Count - 1].transform.position - segments[segments.Count - 2].transform.position;
            middleSpacing = segments[2].transform.position - segments[1].transform.position;
        }

        void Update()
        {
            ManageWormGrowth();
            UpdateWormTargets();
        }

        void SpawnInitialWorms()
        {
            for (int i = 0; i < initialWormCount; i++)
            {
                Vector3 spawnPosition = transform.position + Random.insideUnitSphere * spawnRadius;
                CreateWorm(spawnPosition);
            }
        }

        void ManageWormGrowth()
        {
            growthTimer += Time.deltaTime;
            if (growthTimer >= growthInterval)
            {
                growthTimer = 0f;
                foreach (Worm worm in activeWorms)
                    worm.AddSegment();
            }
        }

        void UpdateWormTargets()
        {
            targetUpdateTimer += Time.deltaTime;
            if (targetUpdateTimer >= targetUpdateInterval)
            {
                targetUpdateTimer = 0f;
                Vector3 highDensityPosition = cell.GetExplosionTarget(domain);
                foreach (Worm worm in activeWorms)
                    worm.SetTarget(highDensityPosition);
            }
        }

        public Worm CreateWorm(Vector3 position, Worm newWormPrefab = null)
        {
            Worm newWorm = Instantiate(newWormPrefab ? newWormPrefab : wormPrefab, position, Quaternion.identity);
            newWorm.Manager = this;
            newWorm.Domain = domain;
            newWorm.transform.parent = transform;
            newWorm.headSpacing = headSpacing;
            newWorm.tailSpacing = tailSpacing;
            newWorm.middleSpacing = middleSpacing;
            activeWorms.Add(newWorm);
            return newWorm;
        }

        public Worm CreateWorm(List<BodySegmentFauna> segments)
        {
            if (segments.Count == 0) return null;

            Worm newWorm = CreateWorm(segments[0].transform.position, emptyWormPrefab);
            newWorm.initialSegments = segments;
            newWorm.InitializeWorm();

            return newWorm;
        }

        public void RemoveWorm(Worm worm)
        {
            activeWorms.Remove(worm);
            Destroy(worm.gameObject);
        }
    }
}
