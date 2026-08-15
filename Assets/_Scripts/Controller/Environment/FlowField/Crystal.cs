using CosmicShore.Core;
using CosmicShore.Gameplay;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Unity.Collections;
using UnityEngine;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    [System.Serializable]
    public class CrystalModelData
    {
        public GameObject model;
        public Material defaultMaterial;
        public Material explodingMaterial;
        public Material inactiveMaterial;
        public SpaceCrystalAnimator spaceCrystalAnimator;
    }

    public class Crystal : CellItem
    {
        [Inject] AudioSystem audioSystem;

        #region Inspector Fields
        [SerializeField]
        CellRuntimeDataSO cellData;
        
        [SerializeField] 
        public CrystalProperties crystalProperties;
        
        [SerializeField] float sphereRadius = 100;
        public float SphereRadius => sphereRadius;

        [SerializeField] protected GameObject SpentCrystalPrefab;

        [SerializeField] protected List<CrystalModelData> crystalModels;
        [SerializeField] protected bool allowVesselImpactEffect = true;
        [SerializeField] bool allowRespawnOnImpact;

        [Header("Data Containers")]
        [SerializeField] protected ThemeManagerDataContainerSO _themeManagerData;

        #endregion
        
        public List<CrystalModelData> CrystalModels => crystalModels;

        public CrystalManager CrystalManager { get; protected set; }
        public bool IsExploding { get; private set; }

        // ── Embedded lifeform heart ──────────────────────────────────────────
        // While a lifeform is alive its elemental crystal rides INSIDE the body (the heart).
        // SetEmbeddedIn enables the crystal's collider so a vessel can JOUST the heart (the
        // Squirrel's joust: destroys opposing-domain lifeforms when moving faster; its Space
        // level-5 upgrade levels up allies instead), while the impact chain gates on IsEmbedded
        // so an embedded heart is never skim-collected or treated as a free-floating pickup.
        // ActivateCrystal (death) clears it - the crystal then drops as the normal collectible
        // powerup (mass conserved).

        /// <summary>The living lifeform (flora or fauna) this crystal is embedded in; null once dropped/free.</summary>
        public ILifeFormEntity EmbeddedIn { get; private set; }

        /// <summary>True while this crystal is a living lifeform's heart (not yet dropped).</summary>
        public bool IsEmbedded => EmbeddedIn != null;

        // The embedded heart's trigger is INFLATED so a vessel passing through the creature
        // reliably clips it at flight speed (the authored radius is a pickup hitbox, tiny and
        // buried inside the body). Restored to the authored radius when the crystal drops.
        const float EmbeddedHeartRadiusMultiplier = 2.5f;
        float _authoredColliderRadius = -1f;

        /// <summary>
        /// Marks this crystal as a living lifeform's heart and enables its collider (inflated,
        /// so the joust reliably lands) so vessels can joust it. Called by lifeforms right
        /// after LifeFormCrystal.EnsureElementalCrystal.
        /// </summary>
        public void SetEmbeddedIn(ILifeFormEntity owner)
        {
            EmbeddedIn = owner;
            var col = GetComponent<SphereCollider>();
            if (!col) return;
            if (_authoredColliderRadius < 0f) _authoredColliderRadius = col.radius;
            col.radius = _authoredColliderRadius * (owner != null ? EmbeddedHeartRadiusMultiplier : 1f);
            col.enabled = owner != null;
            ApplyColorSetTint(); // heart = blue-white neutral while it lives
        }

        // ── Active-crystal registry ──────────────────────────────────────────
        // Lets systems (e.g. HexRaceObjectiveProvider) enumerate live crystals without a
        // per-call FindObjectsByType scene scan. Maintained via OnEnable/OnDisable so it
        // works for both pooled (SetActive) and Instantiate/Destroy lifecycles.
        static readonly List<Crystal> s_active = new();

        /// <summary>Live crystals currently enabled in the scene. Read-only - do not mutate.</summary>
        public static IReadOnlyList<Crystal> Active => s_active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() => s_active.Clear();

        // The models' bloom (FadeIn) writes the SAME per-renderer property block the
        // collectability tint lives on, and clears it when it finishes - a block cannot drop a
        // single key. So claim co-ownership and re-assert the tint on the frame the bloom ends.
        // Without this every crystal settled onto its authored MATERIAL colour a few seconds
        // after spawning, which is why a living lifeform's heart read as the lime free-pickup
        // (Charge and Time author lime materials) instead of the neutral blue it resolves to.
        readonly List<FadeIn> _tintCoOwnedFades = new();

        protected virtual void Awake()
        {
            // Only claim the block when a tint can actually be resolved - an unthemed crystal
            // writes nothing, and co-ownership costs FadeIn a read-back per frame.
            if (!_themeManagerData || crystalModels == null) return;

            foreach (var modelData in crystalModels)
            {
                if (modelData?.model == null) continue;
                if (!modelData.model.TryGetComponent<FadeIn>(out var fade)) continue;
                fade.FadeCompleted += HandleModelFadeCompleted;
                _tintCoOwnedFades.Add(fade);
            }
        }

        protected virtual void OnDestroy()
        {
            foreach (var fade in _tintCoOwnedFades)
                if (fade) fade.FadeCompleted -= HandleModelFadeCompleted;
            _tintCoOwnedFades.Clear();
        }

        void HandleModelFadeCompleted() => ApplyColorSetTint();

        protected virtual void OnEnable()
        {
            if (!s_active.Contains(this)) s_active.Add(this);
        }

        protected virtual void OnDisable()
        {
            s_active.Remove(this);
        }

        protected virtual void Start()
        {
            crystalProperties.crystalValue = crystalProperties.fuelAmount * transform.lossyScale.x;
            ApplyColorSetTint();
        }

        // ── Color set (single source: SO_ColorSet via the theme container) ───
        // Crystal COLOR signals WHO can collect it (element identity is shape, never color):
        //   • domain crystal (Jade/Ruby/Gold)  → that domain's crystal colors - only it collects
        //   • embedded lifeform heart          → blue-white neutral (BlueColors) - nobody collects
        //   • free pickup (drop / omni / cell) → lime CTA (EnvironmentColors) - anyone collects
        // Applies to omni and all four elemental crystals. Colors come LIVE from the theme's
        // ColorSet, per-renderer via MaterialPropertyBlock - never renderer.material clones.

        static MaterialPropertyBlock s_tintBlock;

        /// <summary>Tints all crystal models from the theme ColorSet by collectability state
        /// (see comment above). No-op when the theme container or color set is unwired.</summary>
        protected void ApplyColorSetTint()
        {
            if (!_themeManagerData || _themeManagerData.ColorSet == null) return;
            var colors = _themeManagerData.ColorSet;

            Color bright, dull;
            bool domainOwned = ownDomain is Domains.Jade or Domains.Ruby or Domains.Gold;
            if (domainOwned && colors.TryGetColorSetByDomain(ownDomain, out var domainSet) && domainSet != null)
            {
                bright = domainSet.BrightCrystalColor;
                dull = domainSet.DullCrystalColor;
            }
            else if (IsEmbedded)
            {
                // A living lifeform's heart: the blue-white neutral range - no domain can take it.
                if (!colors.TryGetColorSetByDomain(Domains.Blue, out var neutralSet) || neutralSet == null) return;
                bright = neutralSet.BrightCrystalColor;
                dull = neutralSet.DullCrystalColor;
            }
            else
            {
                // Free collectible: the lime CTA - any domain can collect it now.
                if (colors.EnvironmentColors == null) return;
                bright = colors.EnvironmentColors.BrightCTA;
                dull = colors.EnvironmentColors.DarkCTA;
            }

            s_tintBlock ??= new MaterialPropertyBlock();
            foreach (var modelData in crystalModels)
            {
                if (modelData?.model == null || !modelData.model.TryGetComponent<Renderer>(out var renderer)) continue;
                var mat = renderer.sharedMaterial;
                if (!mat) continue;
                var props = FindColorPropertyNames(mat);
                if (props.bright == null) continue;

                renderer.GetPropertyBlock(s_tintBlock);
                s_tintBlock.SetColor(props.bright, bright);
                s_tintBlock.SetColor(props.dull, dull);
                renderer.SetPropertyBlock(s_tintBlock);
            }
        }

        /// <summary>Clears the tint override on one model so a material color lerp is visible.</summary>
        static void ClearColorSetTint(GameObject model)
        {
            if (model && model.TryGetComponent<Renderer>(out var renderer))
                renderer.SetPropertyBlock(null);
        }

        public void InjectDependencies(CrystalManager cm) => CrystalManager = cm;

        public bool CanBeCollected(Domains shipDomain) => ownDomain == Domains.Blue || ownDomain == shipDomain;

        /// <summary>
        /// Paints this crystal in <paramref name="domain"/>'s crystal colours immediately, with
        /// none of the material lerp <see cref="ChangeDomain"/> plays. Used by the deploy PREVIEW
        /// (the Dolphin's ghost crystal) so it announces whose it will be the moment it blooms,
        /// instead of reading as the lime free-for-all pickup it is not going to be.
        ///
        /// Safe while the component is disabled: the tint rides a MaterialPropertyBlock over the
        /// shared material, so it needs no coroutine and clones nothing.
        /// </summary>
        public void ApplyDomainPreview(Domains domain)
        {
            ownDomain = domain;
            ApplyColorSetTint();
        }

        public struct ExplodeParams
        {
            public Vector3 Course;
            public float Speed;
            public FixedString64Bytes PlayerName;
        }

        public void NotifyManagerToExplodeCrystal(ExplodeParams explodeParams) =>
            CrystalManager.ExplodeCrystal(Id, explodeParams);
        
        public void Respawn()
        {
            // A manager-less mint (e.g. the freestyle conveyor toy's local pickups) has no manager
            // to respawn through - collect once and destroy.
            if (!allowRespawnOnImpact || CrystalManager == null)
            {
                DestroyCrystal();
                return;
            }

            CrystalManager.RespawnCrystal(Id);
        }

        public void DestroyCrystal()
        {
            if (cellData) cellData.TryRemoveItem(this);
            Destroy(gameObject);
        }
        
        public void DeactivateModels()
        {
            foreach (var model in crystalModels)
            {
                model.model.SetActive(true);
                model.model.GetComponent<FadeIn>().StartFadeIn();
            }
        }

        public void MoveToNewPos(Vector3 newPos)
        {
            transform.SetPositionAndRotation(newPos, Quaternion.identity);
        }

        public void Vacuum(Vector3 newPosition, float vaccumAmount)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                newPosition,
                vaccumAmount * Time.deltaTime / transform.lossyScale.x);
        }

        //the following is a public method that can be called to grow the crystal
        public void GrowCrystal(float duration, float targetScale)
        {
            StartCoroutine(Grow(duration, targetScale));
        }

        // the following grow coroutine is used to grow the crystal when it changes size
        IEnumerator Grow(float duration, float targetScale)
        {
            float elapsedTime = 0.0f;
            Vector3 startScale = transform.localScale;
            Vector3 targetScaleVector = new Vector3(targetScale, targetScale, targetScale);

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / duration);

                transform.localScale = Vector3.Lerp(startScale, targetScaleVector, t);

                yield return null;
            }

            transform.localScale = targetScaleVector;
        }
        
        public void Explode(ExplodeParams explodeParams)
        {
            if (IsExploding)
                return;           
            
            IsExploding = true;
            WaitForImpact().Forget();

            var playerName = explodeParams.PlayerName.ToString();
            foreach (var modelData in crystalModels)
            {
                var model = modelData.model;

                // Pooled husk checkout — no Instantiate or material clone in the
                // pickup frame. Impact animates its shader state per-renderer via
                // MaterialPropertyBlock over the shared exploding material.
                var impact = SpentCrystalPoolManager.GetPooledOrInstantiate(
                    SpentCrystalPrefab, transform.position, transform.rotation);
                if (!impact) continue;

                impact.transform.localScale = transform.lossyScale;

                if (crystalProperties.Element == Element.Space && modelData.spaceCrystalAnimator != null)
                {
                    var spentAnimator = impact.GetComponent<SpaceCrystalAnimator>();
                    var thisAnimator = model.GetComponent<SpaceCrystalAnimator>();
                    if (spentAnimator && thisAnimator)
                        spentAnimator.timer = thisAnimator.timer;
                }

                impact.HandleImpact(
                    explodeParams.Course * explodeParams.Speed, modelData.explodingMaterial, playerName);
            }

            PlayExplosionAudio();
        }

        void PlayExplosionAudio()
        {
            if (audioSystem != null)
                audioSystem.PlayGameplaySFX(GameplaySFXCategory.CrystalCollect, transform.position);
        }

        /// <summary>
        /// Moves this heart out of its dying owner and onto the cell WITHOUT freeing it: it stays
        /// <see cref="IsEmbedded"/>, so it remains uncollectable and keeps the neutral heart tint.
        ///
        /// Used by a progressive wither, where the heart is the LAST thing standing and only
        /// becomes collectable when the wither reaches the core (Docs/ECOSYSTEM.md §26). Re-homing
        /// it at the START of the death is what makes that deferral safe: a crystal still parented
        /// to the husk cannot be rescued once the husk is destroyed, so an interrupted wither
        /// (a cell drain, a manager removing the husk) would silently lose it and break the
        /// every-lifeform-drops-a-crystal invariant. Reparenting preserves world pose, and a
        /// withering creature holds still, so the heart does not appear to move.
        /// </summary>
        public void DetachHeartToCell()
        {
            if (cellData && cellData.Cell)
                transform.parent = cellData.Cell.transform;
        }

        public void ActivateCrystal()
        {
            EmbeddedIn = null; // no longer a living heart - it's a free collectible now
            transform.parent = cellData.Cell.transform;
            var dropCol = gameObject.GetComponent<SphereCollider>();
            if (_authoredColliderRadius > 0f) dropCol.radius = _authoredColliderRadius;
            dropCol.enabled = true;
            enabled = true;

            for (int i = 0; i < crystalModels.Count; i++)
            {
                var modelData = crystalModels[i];
                var model = modelData.model;

                model.GetComponent<Renderer>().material = modelData.inactiveMaterial;
                StartCoroutine(LerpCrystalMaterialCoroutine(model, ResolveActivationMaterial(modelData, i)));
            }

            // The state just flipped from "living heart" to "collectible", so repaint NOW - after
            // the lerps above, which drop the block on their way in. A lifeform's heart goes blue
            // → lime here, which is the signal that it can be picked up. Explicit rather than left
            // to Start (which only fires because a heart's Crystal component is authored disabled)
            // or to the material lerp's tail (which is skipped outright when a model has no target
            // material) - either way the crystal would otherwise stay blue while collectable.
            ApplyColorSetTint();
        }

        /// <summary>
        /// The material a crystal settles into as it activates. A DOMAIN-OWNED crystal settles into
        /// that domain's crystal material - the same look Skim Race's track crystals wear (they
        /// reach it via <see cref="SpawnableCrystal"/> → <see cref="ChangeDomain"/>) - because
        /// crystal colour is what tells a pilot who may collect it. An unowned crystal keeps its
        /// authored neutral material and stays free for anyone.
        /// </summary>
        Material ResolveActivationMaterial(CrystalModelData modelData, int index)
        {
            bool domainOwned = ownDomain is Domains.Jade or Domains.Ruby or Domains.Gold;
            if (!domainOwned || !_themeManagerData) return modelData.defaultMaterial;

            // Unity's implicit bool, not ?? - a destroyed material is FAKE-null, which ?? happily
            // passes through, and the lerp would then bail and strand the crystal on inactiveMaterial.
            var teamMaterial = _themeManagerData.GetTeamCrystalMaterial(ownDomain, index);
            return teamMaterial ? teamMaterial : modelData.defaultMaterial;
        }

        public void ChangeDomain(Domains newDomain, float duration = -1)
        {
            if (ownDomain == newDomain)
                return;
            if (newDomain == Domains.Blue)
            {
                for (int i = 0; i < crystalModels.Count; i++)
                {
                    StartCoroutine(LerpCrystalMaterialCoroutine(crystalModels[i].model, crystalModels[i].defaultMaterial, 1));
                }
                ownDomain = newDomain;
                return;
            }
            ownDomain = newDomain;
            for (int i = 0; i < crystalModels.Count; i++)
            {
                StartCoroutine(LerpCrystalMaterialCoroutine(crystalModels[i].model, _themeManagerData.GetTeamCrystalMaterial(ownDomain, i), 1));
            }
            if (duration != -1) StartCoroutine(DecayingTheftCoroutine(duration));
        }

        IEnumerator DecayingTheftCoroutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            ChangeDomain(Domains.Blue);
        }

        protected IEnumerator LerpCrystalMaterialCoroutine(GameObject model, Material targetMaterial, float lerpDuration = 2f)
        {
            Renderer renderer = model.GetComponent<Renderer>();
            if (renderer == null)
                yield break;

            // The theme's per-domain material sets are populated at runtime by ThemeManager; a
            // crystal minted before that (or in a scene with no theme) has nothing to lerp TO.
            // Keep the authored material rather than nulling the renderer out.
            if (targetMaterial == null)
                yield break;

            // The property-block tint would override the animated material colors - drop it for
            // the lerp; the current domain's tint is reapplied once the material settles.
            ClearColorSetTint(model);

            Material tempMaterial = new Material(renderer.material);
            renderer.material = tempMaterial;

            // Detect which color property names the source and target shaders use.
            // Regular crystal shaders use _BrightCrystalColor/_DullCrystalColor,
            // while InverseDynamicFresnelGraph uses _BrightColor/_DullColor.
            var srcProps = FindColorPropertyNames(tempMaterial);
            var dstProps = FindColorPropertyNames(targetMaterial);

            bool canLerp = srcProps.bright != null && dstProps.bright != null
                           && srcProps.bright == dstProps.bright;

            if (canLerp)
            {
                Color startBright = tempMaterial.GetColor(srcProps.bright);
                Color startDull = tempMaterial.GetColor(srcProps.dull);
                Color targetBright = targetMaterial.GetColor(dstProps.bright);
                Color targetDull = targetMaterial.GetColor(dstProps.dull);

                float elapsedTime = 0.0f;
                while (elapsedTime < lerpDuration)
                {
                    elapsedTime += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsedTime / lerpDuration);

                    tempMaterial.SetColor(srcProps.bright, Color.Lerp(startBright, targetBright, t));
                    tempMaterial.SetColor(srcProps.dull, Color.Lerp(startDull, targetDull, t));

                    yield return null;
                }
            }

            renderer.material = targetMaterial;

            // Update the explodingMaterial for the matching crystal model entry
            for (int i = 0; i < crystalModels.Count; i++)
            {
                if (crystalModels[i].model == model)
                {
                    crystalModels[i].explodingMaterial = targetMaterial;
                    break;
                }
            }

            Destroy(tempMaterial);
            ApplyColorSetTint();
        }

        private static (string bright, string dull) FindColorPropertyNames(Material mat)
        {
            if (mat.HasProperty("_BrightCrystalColor") && mat.HasProperty("_DullCrystalColor"))
                return ("_BrightCrystalColor", "_DullCrystalColor");
            if (mat.HasProperty("_BrightColor") && mat.HasProperty("_DullColor"))
                return ("_BrightColor", "_DullColor");
            return (null, null);
        }
        
        /// <summary>
        /// This is to forbid multiple impacts due to multiple vessel colliders
        /// </summary>
        async UniTask WaitForImpact()
        {
            await UniTask.WaitForSeconds(0.5f);
            IsExploding = false;
        }
    }
}