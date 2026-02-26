using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Models.Enums;
using CosmicShore.Game.Environment;
namespace CosmicShore.Game.Environment.MiniGameObjects
{
    public enum ItemType
    {
        None = 0,
        Buff = 1,
        Debuff = 2,
    }

    public abstract class CellItem : MonoBehaviour
    {
        public int Id { get; private set; }
        [FormerlySerializedAs("OwnTeam")] public Domains ownDomain = Domains.None;
        public ItemType ItemType = ItemType.Buff;

        // protected Cell cell;
        
        public void Initialize(int newId) // , Cell cell)
        {
            // this.cell = cell;
            Id = newId;
        }
    }
}

