using System;
using System.IO;
using CosmicShore.Engine;
using Foundation;
using Silk.NET.Windowing.Sdl.iOS;

namespace CosmicShore.Mobile
{
    /// <summary>
    /// The iOS entry point. The player data ships unpacked inside the app bundle (read-only, as
    /// Unity's Data folder does), so nothing is extracted; saves go to Library/Application Support.
    /// SDL owns the UIKit application lifecycle (SilkMobile.RunApp).
    /// </summary>
    public static class Program
    {
        public static void Main(string[] args)
            => SilkMobile.RunApp(args, new Action<string[]>(_ =>
            {
                var bundle = NSBundle.MainBundle.ResourcePath;
                var support = NSSearchPath.GetDirectories(NSSearchPathDirectory.ApplicationSupportDirectory, NSSearchPathDomain.User)[0];
                MobileHost.Run(RuntimePlatform.IPhonePlayer, Path.Combine(bundle, "Data"), Path.Combine(support, "CosmicShore"));
            }));
    }
}
