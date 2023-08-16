using UnityEngine;

public class CellItem : MonoBehaviour
{
    protected int id;

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