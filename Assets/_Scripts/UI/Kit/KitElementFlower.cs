using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The vessel HUD's element flower, for a menu: five copies of the element's petal sprite rotated 72 degrees
    /// apart, coloured by the level exactly as <see cref="ElementalBarsView"/> colours them in a match (the same
    /// <see cref="ElementalBarsConfigSO.DistributePetalValues"/> fill and the same tick colours: 1 lights one petal
    /// white, 5 all white, 6 one blue, 10 all blue). Rev 24 (Shombith, 2026-10-26: "In upgrades we do upgrade the
    /// elemental petals ... That same petal can be showed in the arcade game cards updated with the real value the
    /// players have"): the Hangar's UPGRADES and the arcade cards draw the player's bought level with it.
    ///
    /// <para>Still by construction: it writes its five colours when the level or the element changes, never per
    /// frame. The petals are authored in the prefab (laid out by <see cref="ElementalBarsView.ConfigurePetal"/>'s
    /// rule); the sprites come from the fleet's one <see cref="ElementalBarsConfigSO"/>, so a menu flower and a HUD
    /// flower cannot disagree on what a Mass petal looks like.</para>
    /// </summary>
    public class KitElementFlower : MonoBehaviour
    {
        [Tooltip("The element this flower draws. A card sets it at runtime (Set); the Hangar authors it per row.")]
        [SerializeField] private Element element = Element.Charge;

        [Tooltip("The five petals, first petal first (it lights first). Each stretches over the flower and is rotated 72 degrees from the last.")]
        [SerializeField] private Image[] petals = new Image[ElementalBarsConfigSO.PetalCount];

        [Tooltip("Petal sprites and tick colours. Loaded from Resources/ElementalBarsConfig when empty.")]
        [SerializeField] private ElementalBarsConfigSO config;

        static readonly int[] Scratch = new int[ElementalBarsConfigSO.PetalCount];
        static ElementalBarsConfigSO _shared;

        int _level;
        bool _spritesApplied;

        public Element Element => element;
        public int Level => _level;

        void Awake() => ApplySprites();

        /// <summary>Draws <paramref name="e"/> at <paramref name="level"/> (the HUD's integer level, -5..15).</summary>
        public void Set(Element e, int level)
        {
            if (e != element || !_spritesApplied)
            {
                element = e;
                ApplySprites();
            }
            SetLevel(level);
        }

        public void SetLevel(int level)
        {
            _level = level;
            var bars = Config();
            if (!bars) return;
            ElementalBarsConfigSO.DistributePetalValues(level, Scratch);
            for (int i = 0; i < petals.Length && i < Scratch.Length; i++)
                if (petals[i]) petals[i].color = bars.ColorForTick(Scratch[i]);
        }

        void ApplySprites()
        {
            var bars = Config();
            var sprite = bars ? bars.GetPetalSprite(element) : null;
            foreach (var petal in petals)
            {
                if (!petal) continue;
                petal.sprite = sprite;
                petal.enabled = sprite;
            }
            _spritesApplied = true;
        }

        ElementalBarsConfigSO Config()
        {
            if (config) return config;
            if (!_shared) _shared = Resources.Load<ElementalBarsConfigSO>(ElementalBarsConfigSO.ResourcePath);
            return _shared;
        }
    }
}
