using UnityEngine;

namespace CosmicShore.Utilities
{
    [CreateAssetMenu(fileName = "TagSO", menuName = "ScriptableObjects/TagSO", order = 0)]
    public class TagSO : GuidSO
    {
        [SerializeField]
        private string m_Name;

        protected override void OnValidate()
        {
            if (string.IsNullOrEmpty(m_Name))
            {
                m_Name = name;
            }

            // The Guid creation is done after the name is set, so that the name can be used to generate the Guid.
            base.OnValidate();
        }
    }
}
