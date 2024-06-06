using UnityEngine;

namespace CosmicShore
{
    public class ParticleEffectModifier : MonoBehaviour
    {
        [SerializeField] ParticleSystem targetParticleSystem;
        [SerializeField] string propertyName = "PropertyName";
        [SerializeField] float propertyValue = 1.0f;

        private void Start()
        {
            if (targetParticleSystem != null)
            {
                // Check if the Particle System component is assigned
                var main = targetParticleSystem.main;

                if (propertyName == "StartLifetime")
                {
                    main.startLifetime = propertyValue;
                }
                else if (propertyName == "StartSpeed")
                {
                    main.startSpeed = propertyValue;
                }
                else if (propertyName == "StartSize")
                {
                    main.startSize = propertyValue;
                }
                else if (propertyName == "StartColor")
                {
                    main.startColor = new Color(propertyValue, propertyValue, propertyValue, 1.0f);
                }
                else if (propertyName == "GravityModifier")
                {
                    main.gravityModifier = propertyValue;
                }
                else if (propertyName == "SimulationSpeed")
                {
                    main.simulationSpeed = propertyValue;
                }
                else if (propertyName == "MaxParticles")
                {
                    main.maxParticles = (int)propertyValue;
                }
                else
                {
                    Debug.LogWarning("Property not found: " + propertyName);
                }
            }
        }
    }
}
