using System.Collections.Generic;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Which sign-in providers each platform offers, in what order, and which one signs the player in on its own
    /// (Docs/MENU_REDESIGN_B.md §54, the approved Rev 9 boards). Read by the sign-in scene's
    /// <see cref="SignInChooser"/> and the settings window's <see cref="AccountSettingsView"/>. A provider shows only
    /// when the build can use it (<see cref="IPlatformSignIn.IsAvailable"/>), except in the editor and development
    /// builds when <see cref="showUnavailableInDevBuilds"/> is on, so the screens can be tried before the providers work.
    /// </summary>
    [CreateAssetMenu(fileName = "SignInProviders", menuName = "ScriptableObjects/UI/Sign-In Providers")]
    public class SignInProvidersSO : ScriptableObject
    {
        public enum PlatformGroup
        {
            Desktop = 0,
            IPhone = 1,
            Android = 2,
            Web = 3,
        }

        public enum EditorPreview
        {
            Auto = 0,
            Desktop = 1,
            IPhone = 2,
            Android = 3,
            Web = 4,
        }

        [Header("Order per platform (top to bottom; Play as guest is always last)")]
        [SerializeField] List<SignInProvider> desktopOrder = new() { SignInProvider.Steam, SignInProvider.Google, SignInProvider.Apple };
        [SerializeField] List<SignInProvider> iPhoneOrder = new() { SignInProvider.Apple, SignInProvider.Google };
        [SerializeField] List<SignInProvider> androidOrder = new() { SignInProvider.Google, SignInProvider.Apple };
        [SerializeField] List<SignInProvider> webOrder = new() { SignInProvider.Google, SignInProvider.Apple };

        [Header("Signs in on its own")]
        [Tooltip("Desktop: the provider tried silently before the chooser shows (Steam: the game signs you in with Steam). " +
                 "None always shows the chooser.")]
        [SerializeField] SignInProvider desktopSilentProvider = SignInProvider.Steam;

        [Header("Marks")]
        [Tooltip("Each provider's mark on its buttons. The shipped sprites are stand-ins; the providers' brand rules ask for " +
                 "their official marks, which the login branch puts here.")]
        [SerializeField] Sprite steamMark;
        [SerializeField] Sprite googleMark;
        [SerializeField] Sprite appleMark;

        [Header("Testing")]
        [Tooltip("In the editor and development builds, show providers this build cannot use yet; pressing one answers " +
                 "\"not ready in this build\". Release builds never show them.")]
        [SerializeField] bool showUnavailableInDevBuilds = true;
        [Tooltip("Editor only: lay the sign-in screen out as this platform would.")]
        [SerializeField] EditorPreview editorPreview = EditorPreview.Auto;

        public PlatformGroup CurrentGroup
        {
            get
            {
#if UNITY_EDITOR
                if (editorPreview != EditorPreview.Auto)
                    return (PlatformGroup)((int)editorPreview - 1);
#endif
                return Application.platform switch
                {
                    RuntimePlatform.IPhonePlayer => PlatformGroup.IPhone,
                    RuntimePlatform.Android => PlatformGroup.Android,
                    RuntimePlatform.WebGLPlayer => PlatformGroup.Web,
                    _ => PlatformGroup.Desktop,
                };
            }
        }

        public IReadOnlyList<SignInProvider> OrderFor(PlatformGroup group) => group switch
        {
            PlatformGroup.IPhone => iPhoneOrder,
            PlatformGroup.Android => androidOrder,
            PlatformGroup.Web => webOrder,
            _ => desktopOrder,
        };

        public SignInProvider SilentProvider => CurrentGroup == PlatformGroup.Desktop ? desktopSilentProvider : SignInProvider.None;

        public Sprite MarkFor(SignInProvider provider) => provider switch
        {
            SignInProvider.Steam => steamMark,
            SignInProvider.Google => googleMark,
            SignInProvider.Apple => appleMark,
            _ => null,
        };

        /// <summary>A provider's button shows on this platform.</summary>
        public bool Shows(SignInProvider provider, IPlatformSignIn service)
        {
            if (!Offers(provider)) return false;
            if (service != null && service.IsAvailable(provider)) return true;
            return showUnavailableInDevBuilds && Debug.isDebugBuild;
        }

        /// <summary>This platform's order names the provider.</summary>
        public bool Offers(SignInProvider provider)
        {
            var order = OrderFor(CurrentGroup);
            for (int i = 0; i < order.Count; i++)
                if (order[i] == provider) return true;
            return false;
        }
    }
}
