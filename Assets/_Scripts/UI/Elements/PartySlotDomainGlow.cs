using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The animated halo drawn BEHIND a party slot's avatar in that pilot's domain colour -
    /// the party panel's answer to "who is flying for whom", in the one channel the platform
    /// always answers that question with.
    ///
    /// <para><b>Structural, not authored.</b> It is ensured by <see cref="FriendInfoSlot"/>
    /// rather than wired per slot (<see cref="EnsureFor"/> builds the GameObject, the sprite
    /// and the rect on first use), so every slot in every panel that draws one gets it, in
    /// Menu_Main and in any future party surface, with no scene edit and nothing to forget.
    /// The same reasoning the ability lockup records: a signal every instance must carry is
    /// ensured in the one method they all route through.</para>
    ///
    /// <para><b>The halo sprite is generated once and shared.</b> A soft radial falloff on a
    /// white texture, tinted at runtime - so one 128px texture serves every domain and every
    /// slot, and re-colouring is a vertex tint rather than an asset swap.</para>
    ///
    /// <para><b>Nothing pops.</b> Show, hide and a mid-party domain change are all eased
    /// (continuity of existence applies to UI too), with the ONE exception of the first colour
    /// a slot is given - lerping that from an unset black would read as a slot briefly on the
    /// wrong team, which is worse than a snap.</para>
    ///
    /// <para><b>It draws only what it knows.</b> A pilot whose domain cannot be resolved gets
    /// NO halo rather than a default-coloured one: an unresolved player painted Jade is
    /// confident misinformation, and a missing halo reads as "not known yet".</para>
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class PartySlotDomainGlow : MonoBehaviour
    {
        /// <summary>Name of the generated child, also how <see cref="EnsureFor"/> re-finds it.</summary>
        public const string GlowObjectName = "DomainGlow";

        [Header("Pulse")]
        [Tooltip("Full pulse cycles per second.")]
        [SerializeField] float pulseCyclesPerSecond = 0.55f;

        [Tooltip("Halo alpha at the bottom of the pulse.")]
        [SerializeField, Range(0f, 1f)] float minAlpha = 0.35f;

        [Tooltip("Halo alpha at the top of the pulse.")]
        [SerializeField, Range(0f, 1f)] float maxAlpha = 0.85f;

        [Tooltip("Halo size (as a multiple of the avatar rect) at the bottom of the pulse.")]
        [SerializeField] float minScale = 1.45f;

        [Tooltip("Halo size (as a multiple of the avatar rect) at the top of the pulse.")]
        [SerializeField] float maxScale = 1.72f;

        [Header("Transitions")]
        [Tooltip("How fast the halo eases to a new domain colour, and fades in/out. " +
                 "Higher is snappier; it is an exponential rate, not a duration.")]
        [SerializeField] float easeRate = 7f;

        RectTransform _rect;
        RectTransform _avatarRect;
        Image _image;

        Color _currentColour = Color.white;
        Color _targetColour  = Color.white;
        bool  _hasColour;

        float _visibility;        // eased 0..1
        float _targetVisibility;  // 0 or 1

        // Own clock rather than Time.unscaledTime: that grows without bound for the life of the
        // session, and a menu left open long enough loses the float precision the sine needs -
        // the pulse degrades into a stutter with nothing to point at.
        float _phase;

        // ─────────────────────────────────────────────────────────────────────
        // Build
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the halo for this avatar, building it if it does not exist yet. The halo is
        /// a SIBLING inserted immediately before the avatar - a child would draw on top of it,
        /// and UGUI's draw order is sibling order.
        /// </summary>
        public static PartySlotDomainGlow EnsureFor(Image avatar)
        {
            if (avatar == null) return null;

            var avatarRect = avatar.rectTransform;
            var parent     = avatarRect.parent as RectTransform;
            if (parent == null) return null;

            // Re-find rather than re-create: Awake can run more than once over a panel's life
            // (enable/disable, a prefab re-instantiation), and a second halo would double the
            // alpha of the first.
            for (int i = 0; i < parent.childCount; i++)
            {
                var existing = parent.GetChild(i).GetComponent<PartySlotDomainGlow>();
                if (existing != null)
                {
                    existing.Bind(avatarRect);
                    return existing;
                }
            }

            var go = new GameObject(GlowObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.SetSiblingIndex(avatarRect.GetSiblingIndex());

            var image = go.GetComponent<Image>();
            image.sprite        = HaloSprite();
            image.type          = Image.Type.Simple;
            image.raycastTarget = false;   // decoration must never eat the slot's own clicks
            image.color         = new Color(1f, 1f, 1f, 0f);

            var glow = go.AddComponent<PartySlotDomainGlow>();
            glow.Bind(avatarRect);
            return glow;
        }

        void Bind(RectTransform avatarRect)
        {
            _avatarRect = avatarRect;
            _rect  = _rect  != null ? _rect  : (RectTransform)transform;
            _image = _image != null ? _image : GetComponent<Image>();
            SyncRectToAvatar();
        }

        void Awake()
        {
            _rect  = (RectTransform)transform;
            _image = GetComponent<Image>();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Drive
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Shows the halo in <paramref name="domainColour"/>. Safe to call every frame - a
        /// repeat of the colour already showing is a no-op.
        /// </summary>
        public void Show(Color domainColour)
        {
            domainColour.a = 1f;
            _targetColour  = domainColour;

            // First colour snaps: easing from the unset white/black would read as this pilot
            // briefly flying for another domain.
            if (!_hasColour)
            {
                _currentColour = domainColour;
                _hasColour     = true;
            }

            _targetVisibility = 1f;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        /// <summary>
        /// Fades the halo out. It stays alive (and stops drawing once faded) so an empty slot
        /// that fills again eases back in rather than popping.
        ///
        /// <para>It also FORGETS its colour, which is the half that matters when a slot changes
        /// occupant: the fade is what keeps the transition continuous, but easing the colour
        /// across would show the incoming pilot wearing the outgoing pilot's domain on the way
        /// in. Continuity of presence, snap of identity.</para>
        /// </summary>
        public void Hide()
        {
            _targetVisibility = 0f;
            _hasColour        = false;
        }

        void Update()
        {
            if (_image == null) return;

            SyncRectToAvatar();

            // Unscaled: the menu legitimately runs at a modified timeScale during the
            // freestyle camera transitions, and a decoration must not stall with it.
            float dt   = Time.unscaledDeltaTime;
            float ease = 1f - Mathf.Exp(-easeRate * dt);

            _visibility    = Mathf.Lerp(_visibility, _targetVisibility, ease);
            _currentColour = Color.Lerp(_currentColour, _targetColour, ease);

            if (_targetVisibility <= 0f && _visibility < 0.004f)
            {
                _visibility = 0f;
                _image.color = new Color(_currentColour.r, _currentColour.g, _currentColour.b, 0f);
                gameObject.SetActive(false);
                return;
            }

            _phase += dt * Mathf.Max(0f, pulseCyclesPerSecond);
            if (_phase >= 1f) _phase -= Mathf.Floor(_phase);

            float phase = 0.5f + 0.5f * Mathf.Sin(_phase * 2f * Mathf.PI);

            float alpha = Mathf.Lerp(minAlpha, maxAlpha, phase) * _visibility;
            _image.color = new Color(_currentColour.r, _currentColour.g, _currentColour.b, alpha);

            float scale = Mathf.Lerp(minScale, maxScale, phase);
            _rect.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>
        /// Copies the avatar's rect every frame instead of parenting to it. The halo has to be
        /// an earlier SIBLING to draw behind, so it cannot inherit the avatar's rect - and the
        /// avatar's rect is not constant (a layout pass, a panel resize, a different aspect on
        /// another device all move it). The pulse is applied as localScale ON TOP of the copy,
        /// so the two never fight.
        /// </summary>
        void SyncRectToAvatar()
        {
            if (_avatarRect == null || _rect == null) return;

            _rect.anchorMin       = _avatarRect.anchorMin;
            _rect.anchorMax       = _avatarRect.anchorMax;
            _rect.pivot           = _avatarRect.pivot;
            _rect.anchoredPosition = _avatarRect.anchoredPosition;
            _rect.sizeDelta       = _avatarRect.sizeDelta;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Shared halo sprite
        // ─────────────────────────────────────────────────────────────────────

        static Sprite _sharedSprite;

        /// <summary>Radius (0..1 of the half-extent) the falloff starts at.</summary>
        const float HALO_CORE = 0.22f;

        const int HALO_SIZE = 128;

        static Sprite HaloSprite()
        {
            if (_sharedSprite != null) return _sharedSprite;

            var tex = new Texture2D(HALO_SIZE, HALO_SIZE, TextureFormat.RGBA32, false)
            {
                name       = "PartySlotDomainGlow_Halo",
                hideFlags  = HideFlags.HideAndDontSave,
                wrapMode   = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var px     = new Color32[HALO_SIZE * HALO_SIZE];
            float half = (HALO_SIZE - 1) * 0.5f;

            for (int y = 0; y < HALO_SIZE; y++)
            {
                for (int x = 0; x < HALO_SIZE; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float r  = Mathf.Sqrt(dx * dx + dy * dy);

                    // 1 inside the core, easing to 0 at the rect edge. Squared so the visible
                    // band sits close in around the avatar rather than washing the whole cell.
                    float a = 1f - SmoothStep01(HALO_CORE, 1f, r);
                    a *= a;

                    px[y * HALO_SIZE + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, false);

            _sharedSprite = Sprite.Create(
                tex, new Rect(0, 0, HALO_SIZE, HALO_SIZE), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect);
            _sharedSprite.name      = "PartySlotDomainGlow_Halo";
            _sharedSprite.hideFlags = HideFlags.HideAndDontSave;

            return _sharedSprite;
        }

        /// <summary>
        /// The GLSL-style smoothstep. Unity's <c>Mathf.SmoothStep(from, to, t)</c> interpolates
        /// BETWEEN two values with a smoothed t - it is not the edge0/edge1 gate this needs.
        /// </summary>
        static float SmoothStep01(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / Mathf.Max(1e-5f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
