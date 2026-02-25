using System.Collections;
using CosmicShore.Game;
using CosmicShore.Soap;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Utility;
using CosmicShore.Models.Enums;

namespace CosmicShore
{
    /// <summary>
    /// Abstract base for animal-like lifeforms and their managers.
    /// Uses virtual methods instead of abstract to satisfy LSP - subclasses only
    /// override what they need, and managers don't have to throw NotImplementedException.
    /// </summary>
    public abstract class Fauna : MonoBehaviour, ILifeFormEntity
    {
        [Header("Data References")]
        [FormerlySerializedAs("miniGameData")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] protected CellRuntimeDataSO cellData;

        [Header("Team & Goals")]
        [FormerlySerializedAs("Team")]
        public Domains domain;
        [SerializeField] float goalUpdateInterval = 5f;
        public Vector3 Goal;

        // --- ILifeFormEntity ---
        public Domains Domain => domain;
        public GameObject GetGameObject() => gameObject;

        protected Cell cell => cellData.Cell;

        protected virtual void Start()
        {
            if (domain == Domains.Unassigned)
                CSDebug.LogWarning($"{name}: Population domain is Unassigned. Assign it before spawning, or set it on the prefab.");

            StartCoroutine(UpdateGoalCoroutine());
        }

        /// <summary>
        /// Initialize this fauna with its parent cell. Override in subclasses that need
        /// setup beyond the default. Default implementation is intentionally empty -
        /// this satisfies LSP so managers and stubs don't need to throw NotImplementedException.
        /// </summary>
        public virtual void Initialize(Cell cell) { }

        /// <summary>
        /// Handle this fauna's death. Default is empty - override in subclasses
        /// that have meaningful death behavior.
        /// </summary>
        protected virtual void Die(string killerName = "") { }

        public void SetTeam(Domains domain)
        {
            this.domain = domain;
        }

        IEnumerator UpdateGoalCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(goalUpdateInterval);
                Goal = cell.GetExplosionTarget(domain);
            }
        }
    }
}
