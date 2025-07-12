using UnityEngine;

public enum ItemType
{
    None = 0,
    Buff = 1,
    Debuff = 2,
}

public abstract class CellItem : MonoBehaviour
{
    protected int id;
    public Teams OwnTeam = Teams.None;
    public ItemType ItemType = ItemType.Buff;

    public int GetID()
    {
        return id;
    }

    public void SetID(int id)
    {
        this.id = id;
    }

    public void AddSelfToNode()
    {
        if (CellControlManager.Instance == null)
        {
            Debug.LogWarningFormat("{0} - {1} - {2} is not instantiated yet.", nameof(CellItem), nameof(AddSelfToNode), nameof(CellControlManager));
        }
        else
        {
            CellControlManager.Instance.AddItem(this);
        }
        
    }

    public void RemoveSelfFromNode() // TODO: consider calling OnDestroy()
    {
        if (CellControlManager.Instance == null)
        {
            Debug.LogWarningFormat("{0} - {1} - {2} is not instantiated yet.", nameof(CellItem), nameof(RemoveSelfFromNode), nameof(CellControlManager));
        }
        else
        {
            CellControlManager.Instance.RemoveItem(this);
        }
        
    }

    public void UpdateSelfWithNode()
    {
        if (CellControlManager.Instance == null)
        {
            Debug.LogWarningFormat("{0} - {1} - {2} is not instantiated yet.", nameof(CellItem), nameof(UpdateSelfWithNode), nameof(CellControlManager));
        }
        else
        {
            CellControlManager.Instance.UpdateItem(this);
        }
        
    }
}