using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "Vessel3DCanvasSettings",
        menuName = "ScriptableObjects/UI/Vessel 3D Canvas Settings")]
    public class Vessel3DCanvasSettingsSO : ScriptableObject
    {
        [Header("Positioning")]
        [Tooltip("Local offset above the vessel pivot")]
        public Vector3 localOffset = new(0f, 2f, 0f);

        [Header("Canvas Size")]
        [Tooltip("World-space canvas width/height in units")]
        public Vector2 canvasSize = new(1f, 0.5f);

        [Tooltip("Pixels per unit for the canvas scaler")]
        public float pixelsPerUnit = 100f;

        [Header("Billboarding")]
        [Tooltip("When true the canvas always faces the active camera")]
        public bool billboard = true;

        [Tooltip("Lock the canvas upright (ignore camera pitch)")]
        public bool lockVerticalAxis = true;

        [Header("Sorting")]
        [Tooltip("Sorting order for the canvas")]
        public int sortingOrder = 10;
    }
}
