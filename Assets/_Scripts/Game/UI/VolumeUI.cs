using System;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.UI
{
    [RequireComponent(typeof(Image))]
    public class VolumeUI : MonoBehaviour
    {
        [SerializeField] float upperBound = 300000f;

        [SerializeField] private ScriptableEventNoParam OnResetForReplay; 

        private Material _material;
        private Vector4 _colorRadii;

        void Awake()
        {
            var image = GetComponent<Image>();
            _material = new Material(image.material);
            image.material = _material;
        }

        void OnEnable()
        {
            OnResetForReplay.OnRaised += ResetForReplay;
            ResetForReplay();
        }

        void OnDisable()
        {
            OnResetForReplay.OnRaised -= ResetForReplay;
        }

        private void ResetForReplay()
        {
            _colorRadii = default;
            ApplyToMaterial();
        }

        /// <summary>
        /// Updates the fill radii based on the normalized team volumes.
        /// </summary>
        public void UpdateVolumes(Vector4 teamVolumes)
        {
            _colorRadii = new Vector4(
                teamVolumes.x / upperBound,
                teamVolumes.y / upperBound,
                teamVolumes.z / upperBound,
                teamVolumes.w / upperBound);

            ApplyToMaterial();
        }

        private void ApplyToMaterial()
        {
            if (!_material) return;

            _material.SetFloat("_Radius1", _colorRadii.x);
            _material.SetFloat("_Radius2", _colorRadii.y);
            _material.SetFloat("_Radius3", _colorRadii.z);
            _material.SetFloat("_Radius4", _colorRadii.w);
        }
    }
}