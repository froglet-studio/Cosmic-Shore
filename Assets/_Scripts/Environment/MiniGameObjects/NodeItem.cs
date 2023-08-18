using UnityEngine;

public enum ItemType
{
    None = 0,
    Buff = 1,
    Debuff = 2,
}

public class NodeItem : MonoBehaviour
{
    protected int id;
    public Teams Team = Teams.None;
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
        NodeControlManager.Instance.AddItem(this);
    }

    public void RemoveSelfFromNode()
    {
        NodeControlManager.Instance.RemoveItem(this);
    }

    public void UpdateSelfWithNode()
    {
        NodeControlManager.Instance.UpdateItem(this);
    }
}