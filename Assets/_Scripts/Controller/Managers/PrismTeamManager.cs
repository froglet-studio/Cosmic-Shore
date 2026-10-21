using UnityEngine;
using System;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    public class PrismTeamManager : MonoBehaviour
    {
        [Header("Data Containers")]
        [SerializeField] ThemeManagerDataContainerSO _themeManagerData;

        [SerializeField] private ScriptableEventPrismStats onPrismStolen;

        private static readonly int BrightColorId = Shader.PropertyToID("_BrightColor");

        private Prism prism;
        private MaterialPropertyAnimator materialAnimator;
        private Domains currentDomain = Domains.Blue;

        // The length of the NEXT team change's colour grade. A steal that sheds (PrismInteractionLod) sets it
        // to 0 for its one change — the skin is the transition — and HandleTeamChange restores the default.
        private float nextGradeSeconds = MaterialPropertyAnimator.DefaultTransitionSeconds;

        public Domains Domain
        {
            get => currentDomain;
            private set
            {
                if (currentDomain != value)
                {
                    var oldDomain = currentDomain;
                    currentDomain = value;
                    HandleTeamChange(oldDomain, currentDomain);
                    OnTeamChanged?.Invoke(oldDomain, value);
                }
            }
        }

        public event Action<Domains, Domains> OnTeamChanged;

        private void Awake()
        {
            prism = GetComponent<Prism>();
            materialAnimator = GetComponent<MaterialPropertyAnimator>();
        }

        public void SetInitialTeam(Domains domain)
        {
            if (currentDomain == Domains.Blue)
            {
                Domain = domain;
                materialAnimator.UpdateMaterial(
                    _themeManagerData.GetTeamTransparentBlockMaterial(domain),
                    _themeManagerData.GetTeamBlockMaterial(domain)
                );
            }
        }

        public void ResetToNeutralForReuse()
        {
            if (!prism) prism = GetComponent<Prism>();
            if (!materialAnimator) materialAnimator = GetComponent<MaterialPropertyAnimator>();

            currentDomain = Domains.Blue;

            if (!TryResolveMaterials(Domains.Blue, out var trans, out var opaque))
            {
                CSDebug.LogError("No Blue materials for environment-pool reuse", this);
                return;
            }
            materialAnimator.BindMaterialsImmediate(trans, opaque);
        }

        public void ChangeTeam(Domains newDomain)
        {
            if (Domain != newDomain)
            {
                Domain = newDomain;
            }
        }

        public void Steal(string playerName, Domains newDomain, bool superSteal)
        {
            if (Domain == newDomain)
                return;

            // Super-shielded prisms are fully invulnerable: no team change,
            // no shield decay. Ways to break super-shields will be added
            // later as targeted opt-in mechanics. The refusal is still SEEN: the
            // deflection jiggle every other hit on super-shielded mass gets, so a
            // steal attempt never reads as passing through (Docs/LIT.md, "dynamic
            // contact never does nothing"). One place covers every steal source -
            // hull, skimmer, Urchin spike, black hole, the Thresher's Mass Mode.
            if (prism.AbsorbSuperShieldHit(0f)) return;

            if (!superSteal && prism.prismProperties.IsShielded)
            {
                prism.DeactivateShields();
                return;
            }

            playerName ??= "No name";

            // Capture the payload BEFORE the flip (AttackerName is the PREVIOUS owner), but
            // RAISE it after: the raise runs its listeners inline, so a throwing listener - or
            // an unwired event slot on a prism variant - used to abort the steal itself, and
            // the caller's whole effect chain with it (the Urchin's chain volley runs AFTER the
            // steal in the same effect list). The steal is gameplay; the event is reporting.
            // Reporting must never be able to veto gameplay.
            var stolenStats = new PrismStats
            {
                OwnName = playerName,
                Volume = prism.Volume,
                AttackerName = prism.PlayerName
            };

            // The steal's DETAIL LEVEL (Docs/PRISM_ANIMATION.md §4.12): close to the viewer and within the
            // budget, the old colours SHED off the prism as a skin and the prism takes its new colour at
            // once; otherwise the ordinary grade. Squinted at, the two are the same transition.
            if (TryShed(newDomain)) nextGradeSeconds = 0f;

            ChangeTeam(newDomain);

            onPrismStolen.Raise(stolenStats);
        }

        private void HandleTeamChange(Domains oldDomain, Domains newDomain)
        {
            float gradeSeconds = nextGradeSeconds;
            nextGradeSeconds = MaterialPropertyAnimator.DefaultTransitionSeconds;
            if (!TryResolveMaterials(newDomain, out var trans, out var opaque))
            {
                CSDebug.LogError($"No materials found for team {newDomain}", this);
                return;
            }
            materialAnimator.UpdateMaterial(trans, opaque, gradeSeconds);
        }

        // A shed needs a solid, standing prism on screen, the colours it shows now, and the colour it is about
        // to wear; any gap and the steal grades as usual.
        bool TryShed(Domains newDomain)
        {
            if (!prism || !materialAnimator || prism.destroyed || !prism.isActiveAndEnabled) return false;
            if (prism.prismProperties == null || prism.prismProperties.IsTransparent) return false;
            if (!TryResolveMaterials(newDomain, out _, out var opaque) || !opaque.HasProperty(BrightColorId)) return false;
            if (!materialAnimator.TryGetDisplayedColors(out var oldBright, out var oldDark)) return false;

            var t = transform;
            return PrismInteractionLod.TryShed(t.position, t.rotation, t.lossyScale, oldBright, oldDark,
                opaque.GetColor(BrightColorId), MaterialPropertyAnimator.DefaultTransitionSeconds);
        }

        bool TryResolveMaterials(Domains domain, out Material transparent, out Material opaque)
        {
            transparent = null;
            opaque = null;
            if (!_themeManagerData) return false;
            if (!prism) prism = GetComponent<Prism>();
            if (!prism || prism.prismProperties == null) return false;

            var props = prism.prismProperties;
            if (props.IsDangerous)
            {
                transparent = _themeManagerData.GetTeamTransparentDangerousBlockMaterial(domain);
                opaque = _themeManagerData.GetTeamDangerousBlockMaterial(domain);
            }
            else if (props.IsShielded)
            {
                transparent = _themeManagerData.GetTeamTransparentShieldedBlockMaterial(domain);
                opaque = _themeManagerData.GetTeamShieldedBlockMaterial(domain);
            }
            else if (props.IsSuperShielded)
            {
                transparent = _themeManagerData.GetTeamTransparentSuperShieldedBlockMaterial(domain);
                opaque = _themeManagerData.GetTeamSuperShieldedBlockMaterial(domain);
            }
            else
            {
                transparent = _themeManagerData.GetTeamTransparentBlockMaterial(domain);
                opaque = _themeManagerData.GetTeamBlockMaterial(domain);
            }
            return transparent != null && opaque != null;
        }
    }
}
