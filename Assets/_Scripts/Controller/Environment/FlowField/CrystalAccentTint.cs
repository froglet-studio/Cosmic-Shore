using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Paints this renderer's <c>_BrightColor</c> from the crystal's BODY (crystalModels[0]) material,
    /// so an overlay that is not itself a crystal model still wears the crystal's colour: lime while
    /// the omni is a free pickup, the domain pair once a domain owns it (Skim Race track crystals, the
    /// Dolphin's TeamCrystal). Used by the omni crystal's charge edges, whose bolt halo is the crystal's
    /// own bright colour exactly as on the charge crystal (Docs/PALETTE.md §2.10).
    ///
    /// Event-driven, nothing per frame: it syncs once on enable and again on
    /// <see cref="Crystal.ModelMaterialSettled"/>, which fires the moment the body's material is
    /// swapped — the same frame the Fresnel body itself snaps to its new colour. It reads the body's
    /// SHARED material, which is where the omni's colour lives (the collectability tint never reaches
    /// the Fresnel family).
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    [DisallowMultipleComponent]
    public class CrystalAccentTint : MonoBehaviour
    {
        [Tooltip("The crystal whose body colour this renderer wears. Resolved from the parents when left empty.")]
        [SerializeField] Crystal crystal;

        static readonly int BrightColorId = Shader.PropertyToID("_BrightColor");
        static MaterialPropertyBlock s_block;

        Renderer _renderer;

        void Awake()
        {
            _renderer = GetComponent<Renderer>();
            if (!crystal) crystal = GetComponentInParent<Crystal>();
        }

        void OnEnable()
        {
            if (!crystal) return;
            crystal.ModelMaterialSettled += Sync;
            Sync();
        }

        void OnDisable()
        {
            if (crystal) crystal.ModelMaterialSettled -= Sync;
        }

        void Sync()
        {
            var models = crystal.CrystalModels;
            var body = models is { Count: > 0 } ? models[0]?.model : null;
            if (!body || !body.TryGetComponent<Renderer>(out var bodyRenderer)) return;

            var material = bodyRenderer.sharedMaterial;
            if (!material || !material.HasProperty(BrightColorId)) return;

            s_block ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(s_block);
            s_block.SetColor(BrightColorId, material.GetColor(BrightColorId));
            _renderer.SetPropertyBlock(s_block);
        }
    }
}
