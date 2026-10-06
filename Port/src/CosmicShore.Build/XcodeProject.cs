using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CosmicShore.Build
{
    /// <summary>
    /// Writes the Xcode project an iOS export ships with — the port's counterpart of the
    /// "Unity-iPhone.xcodeproj" Unity writes on Build Settings ▸ iOS ▸ Build. Open it on a Mac,
    /// choose the signing Team, then Build/Run on a device or Product ▸ Archive for an .ipa.
    ///
    /// The project has one iOS application target with no compiled sources: its single build
    /// phase runs <c>build-dotnet.sh</c>, which compiles the game with the .NET iOS toolchain
    /// (the port's IL2CPP), then copies the app's contents into the bundle Xcode is building.
    /// Xcode does everything after that itself — Info.plist, entitlements, code signing, archive,
    /// export and upload — exactly as for a native app, with the team and profile chosen in its UI.
    /// </summary>
    public static class XcodeProject
    {
        public sealed record Settings(
            string ProductName,          // executable / bundle name (the .NET AssemblyName)
            string DisplayName,          // home-screen name (Player Settings productName)
            string BundleId,
            string Version,              // CFBundleShortVersionString (bundleVersion)
            string Build,                // CFBundleVersion (buildNumber.iPhone)
            string MinimumOs,
            string RepoPath);            // the clone that was exported (overridable on the Mac)

        public static string Write(string outDir, Settings s)
        {
            var proj = Path.Combine(outDir, s.ProductName + ".xcodeproj");
            Directory.CreateDirectory(Path.Combine(proj, "project.xcworkspace"));
            Directory.CreateDirectory(Path.Combine(proj, "xcshareddata", "xcschemes"));
            File.WriteAllText(Path.Combine(proj, "project.pbxproj"), Pbxproj(s));
            File.WriteAllText(Path.Combine(proj, "project.xcworkspace", "contents.xcworkspacedata"),
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Workspace version = \"1.0\">\n   <FileRef location = \"self:\"></FileRef>\n</Workspace>\n");
            File.WriteAllText(Path.Combine(proj, "xcshareddata", "xcschemes", s.ProductName + ".xcscheme"), Scheme(s));
            File.WriteAllText(Path.Combine(outDir, "Info.plist"), InfoPlist(s));
            var script = Path.Combine(outDir, "build-dotnet.sh");
            File.WriteAllText(script, BuildScript(s).Replace("\r\n", "\n"));
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(script, (UnixFileMode)0b111_101_101);
            File.WriteAllText(Path.Combine(outDir, "README.txt"), Readme(s));
            return proj;
        }

        // ---------------------------------------------------------------- pbxproj

        static string Id(string name)
        {
            var h = SHA1.HashData(Encoding.UTF8.GetBytes("CosmicShore.Xcode." + name));
            return Convert.ToHexString(h, 0, 12);
        }

        static string Q(string v) => "\"" + v.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        static string Pbxproj(Settings s)
        {
            string root = Id("project"), main = Id("mainGroup"), products = Id("products"), app = Id("app.ref"),
                target = Id("target"), phase = Id("phase.dotnet"), plist = Id("plist.ref"), script = Id("script.ref"),
                data = Id("data.ref"), projList = Id("project.configs"), tgtList = Id("target.configs"),
                pDebug = Id("project.debug"), pRelease = Id("project.release"), tDebug = Id("target.debug"), tRelease = Id("target.release");

            string Common(bool debug) => $@"
				ALWAYS_SEARCH_USER_PATHS = NO;
				CLANG_ENABLE_MODULES = YES;
				COPY_PHASE_STRIP = NO;
				DEBUG_INFORMATION_FORMAT = {(debug ? "dwarf" : "\"dwarf-with-dsym\"")};
				ENABLE_BITCODE = NO;
				ENABLE_USER_SCRIPT_SANDBOXING = NO;
				IPHONEOS_DEPLOYMENT_TARGET = {s.MinimumOs};
				ONLY_ACTIVE_ARCH = {(debug ? "YES" : "NO")};
				SDKROOT = iphoneos;
				VALIDATE_PRODUCT = {(debug ? "NO" : "YES")};";

            string Target() => $@"
				ARCHS = arm64;
				CODE_SIGN_STYLE = Automatic;
				CURRENT_PROJECT_VERSION = {Q(s.Build)};
				DEVELOPMENT_TEAM = """";
				GENERATE_INFOPLIST_FILE = NO;
				INFOPLIST_FILE = Info.plist;
				MARKETING_VERSION = {Q(s.Version)};
				PRODUCT_BUNDLE_IDENTIFIER = {Q(s.BundleId)};
				PRODUCT_NAME = {Q(s.ProductName)};
				SUPPORTED_PLATFORMS = ""iphoneos"";
				SUPPORTS_MACCATALYST = NO;
				TARGETED_DEVICE_FAMILY = ""1,2"";";

            return $@"// !$*UTF8*$!
{{
	archiveVersion = 1;
	classes = {{
	}};
	objectVersion = 56;
	objects = {{

/* Begin PBXFileReference section */
		{app} /* {s.ProductName}.app */ = {{isa = PBXFileReference; explicitFileType = wrapper.application; includeInIndex = 0; path = {Q(s.ProductName + ".app")}; sourceTree = BUILT_PRODUCTS_DIR; }};
		{plist} /* Info.plist */ = {{isa = PBXFileReference; lastKnownFileType = text.plist.xml; path = Info.plist; sourceTree = ""<group>""; }};
		{script} /* build-dotnet.sh */ = {{isa = PBXFileReference; lastKnownFileType = text.script.sh; path = ""build-dotnet.sh""; sourceTree = ""<group>""; }};
		{data} /* PlayerData */ = {{isa = PBXFileReference; lastKnownFileType = folder; path = PlayerData; sourceTree = ""<group>""; }};
/* End PBXFileReference section */

/* Begin PBXGroup section */
		{main} = {{
			isa = PBXGroup;
			children = (
				{plist} /* Info.plist */,
				{script} /* build-dotnet.sh */,
				{data} /* PlayerData */,
				{products} /* Products */,
			);
			sourceTree = ""<group>"";
		}};
		{products} /* Products */ = {{
			isa = PBXGroup;
			children = (
				{app} /* {s.ProductName}.app */,
			);
			name = Products;
			sourceTree = ""<group>"";
		}};
/* End PBXGroup section */

/* Begin PBXNativeTarget section */
		{target} /* {s.ProductName} */ = {{
			isa = PBXNativeTarget;
			buildConfigurationList = {tgtList};
			buildPhases = (
				{phase} /* Build with .NET */,
			);
			buildRules = (
			);
			dependencies = (
			);
			name = {Q(s.ProductName)};
			productName = {Q(s.ProductName)};
			productReference = {app} /* {s.ProductName}.app */;
			productType = ""com.apple.product-type.application"";
		}};
/* End PBXNativeTarget section */

/* Begin PBXProject section */
		{root} /* Project object */ = {{
			isa = PBXProject;
			attributes = {{
				BuildIndependentTargetsInParallel = 1;
				LastUpgradeCheck = 1600;
				TargetAttributes = {{
					{target} = {{
						CreatedOnToolsVersion = 16.0;
					}};
				}};
			}};
			buildConfigurationList = {projList};
			compatibilityVersion = ""Xcode 14.0"";
			developmentRegion = en;
			hasScannedForEncodings = 0;
			knownRegions = (
				en,
				Base,
			);
			mainGroup = {main};
			productRefGroup = {products} /* Products */;
			projectDirPath = """";
			projectRoot = """";
			targets = (
				{target} /* {s.ProductName} */,
			);
		}};
/* End PBXProject section */

/* Begin PBXShellScriptBuildPhase section */
		{phase} /* Build with .NET */ = {{
			isa = PBXShellScriptBuildPhase;
			alwaysOutOfDate = 1;
			buildActionMask = 2147483647;
			files = (
			);
			inputPaths = (
			);
			name = ""Build with .NET"";
			outputPaths = (
			);
			runOnlyForDeploymentPostprocessing = 0;
			shellPath = /bin/sh;
			shellScript = ""\""$SRCROOT/build-dotnet.sh\""\n"";
			showEnvVarsInLog = 0;
		}};
/* End PBXShellScriptBuildPhase section */

/* Begin XCBuildConfiguration section */
		{pDebug} /* Debug */ = {{
			isa = XCBuildConfiguration;
			buildSettings = {{{Common(true)}
			}};
			name = Debug;
		}};
		{pRelease} /* Release */ = {{
			isa = XCBuildConfiguration;
			buildSettings = {{{Common(false)}
			}};
			name = Release;
		}};
		{tDebug} /* Debug */ = {{
			isa = XCBuildConfiguration;
			buildSettings = {{{Target()}
			}};
			name = Debug;
		}};
		{tRelease} /* Release */ = {{
			isa = XCBuildConfiguration;
			buildSettings = {{{Target()}
			}};
			name = Release;
		}};
/* End XCBuildConfiguration section */

/* Begin XCConfigurationList section */
		{projList} = {{
			isa = XCConfigurationList;
			buildConfigurations = (
				{pDebug} /* Debug */,
				{pRelease} /* Release */,
			);
			defaultConfigurationIsVisible = 0;
			defaultConfigurationName = Release;
		}};
		{tgtList} = {{
			isa = XCConfigurationList;
			buildConfigurations = (
				{tDebug} /* Debug */,
				{tRelease} /* Release */,
			);
			defaultConfigurationIsVisible = 0;
			defaultConfigurationName = Release;
		}};
/* End XCConfigurationList section */
	}};
	rootObject = {root} /* Project object */;
}}
".Replace("\r\n", "\n");
        }

        static string Scheme(Settings s)
        {
            string target = Id("target");
            string Ref() => $@"<BuildableReference BuildableIdentifier = ""primary"" BlueprintIdentifier = ""{target}"" BuildableName = ""{s.ProductName}.app"" BlueprintName = ""{s.ProductName}"" ReferencedContainer = ""container:{s.ProductName}.xcodeproj""></BuildableReference>";
            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Scheme LastUpgradeVersion = ""1600"" version = ""1.7"">
   <BuildAction parallelizeBuildables = ""YES"" buildImplicitDependencies = ""YES"">
      <BuildActionEntries>
         <BuildActionEntry buildForTesting = ""YES"" buildForRunning = ""YES"" buildForProfiling = ""YES"" buildForArchiving = ""YES"" buildForAnalyzing = ""YES"">
            {Ref()}
         </BuildActionEntry>
      </BuildActionEntries>
   </BuildAction>
   <LaunchAction buildConfiguration = ""Release"" selectedDebuggerIdentifier = ""Xcode.DebuggerFoundation.Debugger.LLDB"" selectedLauncherIdentifier = ""Xcode.DebuggerFoundation.Launcher.LLDB"" launchStyle = ""0"" useCustomWorkingDirectory = ""NO"" ignoresPersistentStateOnLaunch = ""NO"" debugDocumentVersioning = ""YES"" debugServiceExtension = ""internal"" allowLocationSimulation = ""YES"">
      <BuildableProductRunnable runnableDebuggingMode = ""0"">
         {Ref()}
      </BuildableProductRunnable>
   </LaunchAction>
   <ProfileAction buildConfiguration = ""Release"" shouldUseLaunchSchemeArgsEnv = ""YES"" savedToolIdentifier = """" useCustomWorkingDirectory = ""NO"" debugDocumentVersioning = ""YES"">
      <BuildableProductRunnable runnableDebuggingMode = ""0"">
         {Ref()}
      </BuildableProductRunnable>
   </ProfileAction>
   <AnalyzeAction buildConfiguration = ""Debug""></AnalyzeAction>
   <ArchiveAction buildConfiguration = ""Release"" revealArchiveInOrganizer = ""YES""></ArchiveAction>
</Scheme>
".Replace("\r\n", "\n");
        }

        static string InfoPlist(Settings s) => $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
  <key>CFBundleDevelopmentRegion</key><string>en</string>
  <key>CFBundleDisplayName</key><string>{Xml(s.DisplayName)}</string>
  <key>CFBundleExecutable</key><string>$(EXECUTABLE_NAME)</string>
  <key>CFBundleIdentifier</key><string>$(PRODUCT_BUNDLE_IDENTIFIER)</string>
  <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
  <key>CFBundleName</key><string>$(PRODUCT_NAME)</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>$(MARKETING_VERSION)</string>
  <key>CFBundleVersion</key><string>$(CURRENT_PROJECT_VERSION)</string>
  <key>LSRequiresIPhoneOS</key><true/>
  <key>MinimumOSVersion</key><string>{s.MinimumOs}</string>
  <key>UIDeviceFamily</key>
  <array><integer>1</integer><integer>2</integer></array>
  <key>UIRequiredDeviceCapabilities</key>
  <array><string>arm64</string><string>opengles-3</string></array>
  <key>UISupportedInterfaceOrientations</key>
  <array><string>UIInterfaceOrientationLandscapeLeft</string><string>UIInterfaceOrientationLandscapeRight</string></array>
  <key>UISupportedInterfaceOrientations~ipad</key>
  <array><string>UIInterfaceOrientationLandscapeLeft</string><string>UIInterfaceOrientationLandscapeRight</string></array>
  <key>UIStatusBarHidden</key><true/>
  <key>UIRequiresFullScreen</key><true/>
  <key>UILaunchScreen</key><dict/>
  <key>ITSAppUsesNonExemptEncryption</key><false/>
</dict>
</plist>
".Replace("\r\n", "\n");

        static string Xml(string v) => v.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        static string BuildScript(Settings s) => $@"#!/bin/sh
# Xcode's ""Build with .NET"" phase. Compiles the game with the .NET iOS toolchain, then puts
# the app's contents into the bundle Xcode is building; Xcode signs and archives it after.
# Needs on the Mac: the .NET 10 SDK (https://dot.net) - the 'ios' workload installs itself here.
set -e
REPO=""${{COSMIC_SHORE_REPO:-{s.RepoPath.Replace('\\', '/')}}}""
PROJECT=""$REPO/Port/src/CosmicShore.Mobile/CosmicShore.Mobile.csproj""
if [ ! -f ""$PROJECT"" ]; then
  echo ""error: the Cosmic Shore repository was not found at $REPO."" >&2
  echo ""error: clone it on this Mac and set COSMIC_SHORE_REPO (Xcode > Product > Scheme > Edit Scheme is NOT used for build phases; set it in ~/.zprofile or edit REPO= in build-dotnet.sh)."" >&2
  exit 1
fi
DOTNET=""$(command -v dotnet || true)""
[ -z ""$DOTNET"" ] && [ -x ""$HOME/.dotnet/dotnet"" ] && DOTNET=""$HOME/.dotnet/dotnet""
[ -z ""$DOTNET"" ] && [ -x /usr/local/share/dotnet/dotnet ] && DOTNET=/usr/local/share/dotnet/dotnet
if [ -z ""$DOTNET"" ]; then echo ""error: the .NET 10 SDK is not installed (https://dot.net)."" >&2; exit 1; fi
""$DOTNET"" workload list 2>/dev/null | grep -q '^ios ' || ""$DOTNET"" workload install ios

CONFIG=Release
[ ""$CONFIGURATION"" = ""Debug"" ] && CONFIG=Debug
FMOD=""$REPO/Assets/Plugins/FMOD/platforms""
FMODPROP=""""
[ -f ""$FMOD/ios/lib/libfmodstudiounityplugin.a"" ] && [ ""$(head -c 7 ""$FMOD/ios/lib/libfmodstudiounityplugin.a"")"" = '!<arch>' ] && FMODPROP=""-p:CsFmodRoot=$FMOD""

# Xcode signs the bundle afterwards, with the Team picked in its UI: .NET only compiles here.
""$DOTNET"" build ""$PROJECT"" -f net10.0-ios -c ""$CONFIG"" -r ios-arm64 -nologo -v:minimal \
  -p:ApplicationId=""$PRODUCT_BUNDLE_IDENTIFIER"" -p:ApplicationTitle={Q(s.DisplayName)} \
  -p:ApplicationDisplayVersion=""$MARKETING_VERSION"" -p:ApplicationVersion=""$CURRENT_PROJECT_VERSION"" \
  -p:CsPlayerData=""$SRCROOT/PlayerData"" $FMODPROP \
  -p:EnableCodeSigning=false -p:CodesignRequireProvisioningProfile=false

APP=""$(find ""$REPO/Port/src/CosmicShore.Mobile/bin/$CONFIG/net10.0-ios/ios-arm64"" -maxdepth 1 -name '*.app' -type d | head -1)""
if [ -z ""$APP"" ]; then echo ""error: .NET produced no .app"" >&2; exit 1; fi
DEST=""$TARGET_BUILD_DIR/$WRAPPER_NAME""
mkdir -p ""$DEST""
# Everything but the files Xcode owns (its Info.plist, signature, provisioning profile).
(cd ""$APP"" && find . -mindepth 1 -maxdepth 1 ! -name Info.plist ! -name _CodeSignature ! -name embedded.mobileprovision -exec cp -R {{}} ""$DEST/"" \;)
if [ ""$(basename ""$(ls ""$APP"" | grep -x ""$PRODUCT_NAME"" || true)"")"" != ""$PRODUCT_NAME"" ]; then
  echo ""error: the .NET app's executable is not named $PRODUCT_NAME"" >&2; exit 1
fi
# Nested frameworks/dylibs must carry the same signature as the app.
if [ -n ""$EXPANDED_CODE_SIGN_IDENTITY"" ]; then
  find ""$DEST"" \( -name '*.framework' -o -name '*.dylib' \) -prune -print | while read -r f; do
    codesign --force --sign ""$EXPANDED_CODE_SIGN_IDENTITY"" --preserve-metadata=identifier,entitlements ""$f""
  done
fi
echo ""Cosmic Shore built with .NET into $DEST""
";

        static string Readme(Settings s) => $@"Cosmic Shore - iOS Xcode project (Prisma)

Like Unity's iOS build, this folder is an Xcode project. Finish it on a Mac:

  1. Install Xcode 16 and the .NET 10 SDK (https://dot.net). Clone the repository on the Mac.
  2. Open {s.ProductName}.xcodeproj.
  3. Select the {s.ProductName} target > Signing & Capabilities > choose your Team.
  4. Plug in an iPhone and press Run, or Product > Archive for an .ipa (Distribute App).

The repository path is {s.RepoPath}. If the Mac's clone is elsewhere, set COSMIC_SHORE_REPO
(e.g. in ~/.zprofile) or edit the REPO= line in build-dotnet.sh.

Bundle id {s.BundleId}, version {s.Version} ({s.Build}), iOS {s.MinimumOs}+.
PlayerData/ is the game's packaged content (scenes, art, audio) - the same set Unity ships.
";
    }
}
