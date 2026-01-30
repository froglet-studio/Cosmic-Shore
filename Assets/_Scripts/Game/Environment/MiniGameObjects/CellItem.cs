using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game
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
        
        public void Initialize(int newId, Domains domain = Domains.None) // , Cell cell)
        {
            // this.cell = cell;
            Id = newId;
            ownDomain = domain;
        }
    }
}

