using UnityEngine;

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
        public Teams OwnTeam = Teams.None;
        public ItemType ItemType = ItemType.Buff;

        protected Cell cell;
        
        public void Initialize(int newId, Cell cell)
        {
            this.cell = cell;
            Id = newId;
        }
    }
}

