using UnityEngine;
using UnityEngine.VFX;

public class VFXPropertyModifier : MonoBehaviour
{
    [SerializeField] VisualEffect visualEffect;
    [SerializeField] string propertyName = "PropertyName";
    [SerializeField] float propertyValue = 1.0f;

    private void Start()
    {
        if (visualEffect != null)
        {
            // Check if the Visual Effect component is assigned
            if (visualEffect.HasFloat(propertyName))
            {
                // Modify float property
                visualEffect.SetFloat(propertyName, propertyValue);
            }
            else if (visualEffect.HasInt(propertyName))
            {
                // Modify integer property
                visualEffect.SetInt(propertyName, (int)propertyValue);
            }
            else if (visualEffect.HasVector3(propertyName))
            {
                // Modify Vector3 property
                visualEffect.SetVector3(propertyName, new Vector3(propertyValue, propertyValue, propertyValue));
            }
            else if (visualEffect.HasTexture(propertyName))
            {
                // Modify texture property
                Texture texture = Resources.Load<Texture>("Textures/Example");
                visualEffect.SetTexture(propertyName, texture);
            }
            else
            {
                Debug.LogWarning("Property not found: " + propertyName);
            }

            // Reinitialize the Visual Effect component to apply the changes
            visualEffect.Reinit();
        }
    }
}
