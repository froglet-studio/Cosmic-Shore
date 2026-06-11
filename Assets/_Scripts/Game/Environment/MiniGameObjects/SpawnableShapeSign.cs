using CosmicShore.Game.ShapeDrawing;
using UnityEngine;

/// <summary>
/// Plugs a ShapeSign prefab into the SegmentSpawner system.
///
/// Add one of these per shape to SegmentSpawner.spawnableSegments,
/// each with a low weight (~0.05) so signs appear rarely among normal segments.
///
/// Inspector setup per entry:
///   - signPrefab      : your ShapeSign prefab
///   - shapeDefinition : one of the 5 ShapeDefinition SO assets
/// </summary>
public class SpawnableShapeSign : MonoBehaviour
{
    [SerializeField] GameObject signPrefab;
    [SerializeField] ShapeDefinition shapeDefinition;

    public GameObject Spawn()
    {
        if (!signPrefab)
        {
            Debug.LogError("[SpawnableShapeSign] signPrefab is not assigned!", this);
            return new GameObject("MissingShapeSign");
        }

        var go = Instantiate(signPrefab, transform.position, transform.rotation);

        // Inject the shape definition so the sign knows what it represents
        var sign = go.GetComponent<ShapeSign>();
        if (sign)
            sign.Initialize(shapeDefinition);
        else
            Debug.LogWarning("[SpawnableShapeSign] signPrefab has no ShapeSign component.", this);

        return go;
    }
}