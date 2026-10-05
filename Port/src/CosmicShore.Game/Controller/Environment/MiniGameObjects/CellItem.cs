using CosmicShore.Engine.Networking;
using CosmicShore.Engine;
using CosmicShore.Data;
using CosmicShore.Gameplay;
namespace CosmicShore.Gameplay
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
        [FormerlySerializedAs("OwnTeam")] public Domains ownDomain = Domains.Blue;
        public ItemType ItemType = ItemType.Buff;

        // protected Cell cell;
        
        public void Initialize(int newId) // , Cell cell)
        {
            // this.cell = cell;
            Id = newId;
        }
    }
}

