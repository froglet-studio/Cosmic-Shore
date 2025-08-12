using UnityEngine;

namespace Obvious.Soap
{
    [CreateAssetMenu(fileName = "scriptable_variable_vector2Int", menuName = "Soap/ScriptableVariables/vector2Int")]
    public class Vector2IntVariable : ScriptableVariable<Vector2Int>
    {
        public override void Save()
        {
            PlayerPrefs.SetInt(Guid + "_x", Value.x);
            PlayerPrefs.SetInt(Guid + "_y", Value.y);
            base.Save();
        }

        public override void Load()
        {
            var x = PlayerPrefs.GetInt(Guid + "_x", DefaultValue.x);
            var y = PlayerPrefs.GetInt(Guid + "_y", DefaultValue.y);
            Value = new Vector2Int(x,y);
            base.Load();
        }
    }
}
