# Android builds — a signed App Bundle on Google Play internal testing

Status (2026-10-25):

- **The release build has run.** Build **412826** (version 0.2.0, built 2026-10-14 16:26 UTC from
  FrogletTools > Build > Android App Bundle (Release)) is on Play **internal testing** for
  `com.FrogletGames.CosmicShore`. It is the newest upload on record; Play Console's Internal
  testing page is the authority if someone uploaded since.
- **That build is 64-bit only.** One tester installed it; two (a Moto G Power and a Samsung
  Galaxy M, which run 32-bit Android) got "Item not found". 32-bit (ARMv7) ships again from
  2026-10-15 (§0). **Upload a new build** so those phones can install it (§6), then check them in
  Play Console's Device catalog.
- **Development builds run on phones.** Garrett passed the phone round on an Android development
  build on 2026-10-20 (`Docs/UNITY_VERIFICATION_CHECKLIST.md`, Phone round). §8 covers the
  development APK and the switch that makes it play like release.
- **Not yet checked in the Editor:** the build's refusal dialogs, batchmode exit codes and
  `AndroidBundleCheckTests` in the Test Runner (`Docs/UNITY_VERIFICATION_CHECKLIST.md`, Android
  release build). The check's parsing was proven outside Unity against manifests linked by
  Google's own `aapt2` and against the repo's FMOD and Nice Vibrations libraries (§4).

Internal testing reaches up to 100 testers within minutes of a rollout, and needs no store listing,
content rating or review first. That is the target here. Closed and open testing and production
come later (the rollout tracker's P4 and Later groups).

---

## 0. What is already set

Every Android Player Setting below is on bleeding-edge (`Docs/PLATFORM_UNIFICATION.md` §3.9). The
build checks the ones Play rejects on, and stops rather than changing them back.

| Setting | Value | Why |
|---|---|---|
| Package | `com.FrogletGames.CosmicShore` | Fixed forever by the first upload |
| Target API | 36 | Play's minimum for new apps and updates since 31 Aug 2026 |
| Minimum API | 28 (Android 9) | |
| Architectures | ARMv7 + ARM64, IL2CPP | Play requires 64-bit. 32-bit was dropped on 2026-10-08 and **added back on 2026-10-15**: two team phones (a Moto G Power and a Samsung Galaxy M) run 32-bit Android on 64-bit chips, and Play showed them "Item not found". Play splits a bundle per ABI, so a phone downloads only its own libraries |
| R8 minify | On | Unity Ads, the cause of the old R8 launch crash, is removed |
| Unity IAP | Removed | So no outdated Play Billing library ships |
| Audio | FMOD's Desktop banks (36 MB) | No platform overrides a bank folder, so Android and iOS ship the same banks |
| Application Category | `game`, forced per build | Android 16 exempts only games from forced large-screen resizing (§3) |

## 1. The upload key (once, by whoever builds)

Play signs what players install with a key Google keeps (Play App Signing). Builds are signed with
an **upload key** that we keep. If it is lost, Play Console can reset it, but that takes days. So:

- **Keep the `.keystore` file and both passwords in the password manager.** Never commit them, and
  never paste them into chat.
- Keep the file outside the repo, for example `C:\CosmicShoreSigning\cosmicshore-upload.keystore`
  (the folder the iOS certificate already uses).
- Use the alias **`cosmicshore-upload`**. The build assumes it when none is given.

**Option A, in Unity (no command line):**

1. Player Settings > Android tab > Publishing Settings > **Keystore Manager**.
2. Keystore… > **Create New** > Anywhere. Save it as `C:\CosmicShoreSigning\cosmicshore-upload.keystore`.
3. Fill in the keystore password twice. Then, under New Key Values:
   - Alias `cosmicshore-upload`, with its own password twice (or the same one);
   - Validity 50 years;
   - your name, Organization `Froglet Games`, and the two-letter country code.
4. Add Key. When Unity asks whether to use it as the project keystore, say **yes**.

**Option B, with keytool** (Unity's JDK, in Git Bash or a terminal):

```
"/c/Program Files/Unity/Hub/Editor/6000.3.17f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/keytool.exe" \
  -genkeypair -v -keystore /c/CosmicShoreSigning/cosmicshore-upload.keystore \
  -alias cosmicshore-upload -keyalg RSA -keysize 2048 -validity 10000
```

**Selecting the key writes its path into `ProjectSettings.asset`** (`AndroidKeystoreName`,
`AndroidKeyaliasName`, `androidUseCustomKeystore`). Don't commit those three lines: the path is your
machine's file. Discard them in your git client. The static CI job fails on them
(`Tools/Build/check_android_signing_settings.py`), because bleeding-edge once carried a path to a
file on one developer's PC that belonged to another game.

**For CI, or to keep the path out of Player Settings entirely**, set these environment variables
instead. The build reads them, signs, and puts Player Settings back:

| Variable | Value |
|---|---|
| `CS_ANDROID_KEYSTORE` | Path to the `.keystore` |
| `CS_ANDROID_KEYSTORE_PASS` | Keystore password |
| `CS_ANDROID_KEY_ALIAS` | Optional, default `cosmicshore-upload` |
| `CS_ANDROID_KEY_PASS` | Optional, default the keystore password |

## 2. Install Android Build Support (once per PC)

1. **Unity Hub** > Installs > `6000.3.17f1` > the gear > **Add modules**. Then tick:
   - **Android Build Support**;
   - **OpenJDK**;
   - **Android SDK & NDK Tools**.
2. Open the project. **File > Build Profiles > Android > Switch Platform.** It reimports every
   texture once, which takes a while. Stay on Android afterwards to skip that next time.
3. If Unity says the Android SDK lacks API 36, accept its update prompt. Or open Preferences > External
   Tools and let it install the platform.

## 3. Build

1. Player Settings > Android > Publishing Settings. Tick **Custom Keystore**, select the keystore, and
   type **both passwords**. Unity forgets the passwords whenever the editor restarts, and the build
   says so if they are missing. Skip this step if you use the environment variables.
2. **FrogletTools > Build > Android App Bundle (Release).**
3. When it finishes, a dialog says either **Ready to upload** or exactly what Play would reject.
   The output folder then opens:
   - `Builds/Android/CosmicShore-<version>-<code>.aab` is the file to upload;
   - next to it is `…aab.check.txt`, the full check report. Send that to Claude if anything looks off.

What the build does (`Assets/_Scripts/Editor/Build/CosmicShoreBuildPipeline.Android.cs`):

- It stops before building if:
  - Android Build Support is missing;
  - the editor is not switched to Android;
  - IL2CPP, ARM64 or API 36 is off;
  - there is no upload key.
- **Version code:** the minutes since 1 Jan 2026 UTC, so every build is higher than the last
  without anyone editing Player Settings (about 410,000 in Oct 2026). The version name is Player
  Settings' Version (`0.2.0`).
- **Application Category:** set to `game` for the build. If Player Settings says otherwise, the log
  warns and asks for it to be fixed there.
- **Native debug symbols:** the symbol table goes into the bundle (Debug Symbols: Symbol Table,
  Include In Bundle). Play reads it on upload, so crash and ANR reports name the functions, and
  the "you've not uploaded debug symbols" warning does not appear. A `….symbols.zip` is also
  written beside the `.aab` for a manual upload. Symbols are never delivered to phones, so the
  download size is unchanged.
- **Player Settings are put back afterwards:** version, version code, keystore fields, category,
  debug-symbol settings and the App Bundle switch. So a build leaves the repo clean, apart from a keystore you selected
  yourself (§1).
- It runs the check in §4 on the result.

**Command line** (the same path; exits non-zero when the build or the check fails):

```
Unity -quit -batchmode -nographics -buildTarget Android -projectPath <proj> \
      -executeMethod CosmicShore.Editor.CosmicShoreBuildPipeline.BuildAndroidRelease \
      -buildOutput Builds/Android [-buildVersion 0.2.1] [-buildVersionCode 412345] -logFile -
```

**FrogletTools > Build > Check Android App Bundle…** runs the same check on any `.aab`.

## 4. What the check checks

`Assets/_Scripts/Editor/Build/AndroidBundleCheck.cs` opens the `.aab` itself. It needs no bundletool
and no Java.

| Severity | Check | How |
|---|---|---|
| Error | Signed, and not with the Android **debug** key | `META-INF/*.RSA`/`.EC`/`.DSA`, and the certificate's "Android Debug" name |
| Error | Package is `com.FrogletGames.CosmicShore` | Manifest |
| Error | Target SDK ≥ 36 | Manifest |
| Error | Not debuggable | Manifest |
| Error | Has `arm64-v8a` libraries | `base/lib/<abi>/` |
| Warning | Has `armeabi-v7a` libraries too (32-bit phones) | `base/lib/<abi>/` |
| Error | Version code between 1 and 2,100,000,000 | Manifest |
| Error | Base module ≤ 200 MB compressed | Zip entry sizes |
| Warning | Every 64-bit library is 16 KB page-aligned | Each `.so`'s ELF LOAD segments |
| Warning | Application category is `game` | Manifest |
| Warning | No `com.android.vending.BILLING` permission | Manifest |
| Warning | Base module ≤ 150 MB | Unity's own validation threshold |
| Warning | Native debug symbols are in the bundle | `BUNDLE-METADATA/com.android.tools.build.debugsymbols/` |

The manifest inside a bundle is aapt2's protobuf `XmlNode`, not XML. The check reads it with a
small wire-format reader. **How it was proven:**

- **Manifests:** two manifests linked by `aapt2` 8.13.2 against the API 36 `android.jar`, one
  clean and one wrong in every checked field.
- **Libraries:** the repo's FMOD library (LOAD alignment `0x4000`, matching `readelf`) and Nice
  Vibrations' `liblofelt_sdk.so` (`0x1000`).
- **Signatures:** `jarsigner` signatures with a release-named key and with the standard debug name.
- **Verdicts:** every case gave the right verdict, and the bundle with no 64-bit libraries caught a
  real bug in the first version of the check.
- **Tests:** `AndroidBundleCheckTests` carries those manifests. It ran 9/9 under NUnit on .NET 8, and
  a deliberately reintroduced bug made it fail.

**Expect one warning today:** `liblofelt_sdk.so` (Nice Vibrations haptics) is 4 KB-aligned. Google
Play blocks **updates** without 16 KB support from **1 Feb 2027**
([developer.android.com/guide/practices/page-sizes](https://developer.android.com/guide/practices/page-sizes)).
Internal testing works now. Replacing or rebuilding that library is on the rollout tracker's Later
list.

## 5. The first upload to internal testing (by hand)

The Play Developer API cannot make an app's first upload, so this one goes through Play Console.

1. **Check the account type**: Settings > Developer account > Account details.
   - An **Organization** account has no extra rule.
   - A **Personal** account created after 13 Nov 2023 must run a closed test with 12 testers for
     14 days before production. Internal testing is unaffected either way.
2. **Testers**: Test and release > Testing > **Internal testing** > Testers tab. Create an email list
   (up to 100 Google accounts), tick it, and Save.
3. **Release**: Internal testing > **Create new release**.
   - On the first release Play asks how the app is signed. Keep **Google-generated key** (Play App
     Signing). Your upload key is registered from this first upload's signature.
   - Upload the `.aab`. The release name fills itself in as `<code> (<version>)`. Add a line of
     release notes.
   - **Next > Save > Go to overview > Send for review / Start rollout to Internal testing.**
4. **Install**: back on the Testers tab, **Copy link**. Each tester opens it on their phone, signed
   in with the Google account on the list, accepts, and installs from the Play Store page it links
   to. The first upload can show a temporary app name for up to 48 hours.
5. **Test on two phones**: the 4 GB Samsung (the MobileLow tier, `Docs/PLATFORM_UNIFICATION.md`
   §3.4) and one recent flagship.
   - Every device in a cross-platform match must run the same commit, because nothing checks versions
     (`Docs/IOS_BUILD.md` §3).
   - Play's **pre-launch report** runs each upload on test devices. Read it for crashes.

## 6. Later uploads

Build again, then Internal testing > Create new release > upload. The version code is already
higher. A tester's Play Store updates the app like any other.

Automating the upload is possible once the first one is in. It needs:

- a Google Cloud service account invited in Play Console with release rights;
- its JSON key as a GitHub secret;
- somewhere to build. Nothing builds Unity in CI yet (`Docs/BUILD_AND_DELIVERY.md` §10.2).

Not set up; it is a follow-up.

**Native debug symbols travel inside the bundle** (§3), so later uploads need no separate symbol
step. A bundle built before 2026-10-14 has none, and Play shows "This App Bundle contains native
code, and you've not uploaded debug symbols". That is a recommendation, not a block: roll the
release out. To symbolicate that build's crashes, rebuild it and upload the `….symbols.zip` under
App bundle explorer > the version > Downloads > Native debug symbols.

## 7. When something goes wrong

| Symptom | Fix |
|---|---|
| Dialog: "No upload key" | §1, then select the key in Publishing Settings, or set `CS_ANDROID_KEYSTORE` |
| Dialog: "Publishing Settings has the keystore but not its passwords" | Retype both passwords; Unity forgets them on restart |
| Dialog: "The editor is not on Android" | File > Build Profiles > Android > Switch Platform |
| Play: "signed in debug mode" | The check fails on this first. Select the upload key and rebuild |
| Play: "signed with the wrong key" | Later uploads must use the key the first upload registered. If it is lost: Play Console > Test and release > Setup > App signing > **Request upload key reset** |
| A tester taps "Download it on Google Play" and gets **"Item not found"**, while the app installs for others on the same list | **First, is their phone 32-bit?** Play Console > Reach and devices > Device catalog > search the model; "Unsupported" with an ABI reason means their Android is 32-bit. Bundles before 2026-10-15 were 64-bit only and hide the app from those phones; rebuild and upload. **Otherwise it is the account:** The Play Store app on their phone is signed into a different Google account than the email on the list. Most often the list has a work address and the phone uses a personal Gmail. Play Store > profile icon shows the active account. Either add THAT account to the list (Testers > the email list > add > Save; no new release needed), or switch Play Store to the listed account. Then open the opt-in link in Chrome, signed into the same account, tap **Accept invite**, and only then **Download it on Google Play**. If it persists, clear the Play Store's cache and wait 10-15 minutes. A Google Workspace account whose admin restricts Play cannot install test apps; use a personal Gmail |
| Play: "Version code … has already been used" | Build again (a new minute), or pass `-buildVersionCode` |
| Play warning: "contains native code, and you've not uploaded debug symbols" | Only a recommendation; the release can go out. The bundle was built without the menu (or before 2026-10-14). The check warns about this too. Rebuild with FrogletTools > Build > Android App Bundle (Release) |
| Gradle fails during the build | Most Android Gradle failures trace to `Assets/Plugins/Android/*.gradle` and the EDM4U block; see `Docs/PLATFORM_UNIFICATION.md` §2.1. Send the Console error to Claude |
| The check says the base module is over 200 MB | Player Settings > Android > Publishing Settings > **Split Application Binary**, then rebuild |

## 8. A development build that plays like release (Patch And Run)

Android's **Patch** and **Patch And Run** (Build Profiles > Android) only work on a Development
Build: they push the changed scripts and assets to the installed app instead of a full rebuild and
reinstall. A development build normally carries the diagnostics HUD, the AI-training flight
recorder, Unity's red on-screen error console and the info logs. **`DevDiagnostics`**
(`Assets/_Scripts/Utility/DevDiagnostics.cs`) switches all of them off together, so the patched
build plays like a release build.

1. Build Profiles > Android: tick **Development Build**. Leave **Autoconnect Profiler**, **Deep
   Profiling Support** and **Script Debugging** unticked: they slow the build down and are not
   covered by the switch.
2. Build And Run once, then **Patch And Run** for every change after that.
3. On the phone, turn the diagnostics **off**: press **Off** on the diagnostics HUD's command row, or
   hold **four fingers** on the screen for **two seconds**. The HUD disappears.
4. The choice is saved on the phone (PlayerPrefs `CosmicShore.DevDiagnostics`). It survives
   relaunches and every Patch, until the app is uninstalled or its data is cleared.
5. To get the diagnostics back, hold four fingers for two seconds again (Shift+F7 on a keyboard).

**To start a fresh install with the diagnostics off**, add `CS_DEV_DIAGNOSTICS_OFF` to the Android
Build Profile's **Scripting Defines**. A choice made on the phone still wins over it.

**A development APK without the Build Profiles window:** **FrogletTools > Build > Android APK
(Development)**, or from the command line (the editor must not have the project open):

```
Unity -quit -batchmode -nographics -buildTarget Android -projectPath <proj> \
      -executeMethod CosmicShore.Editor.CosmicShoreBuildPipeline.BuildAndroidDevelopment \
      -buildOutput Builds/Android [-diagnosticsOff] [-buildVersion 0.2.1] -logFile <log>
```

It writes `Builds/Android/CosmicShore-<version>-<code>-dev.apk`, signed with Unity's debug key, and
puts Player Settings back afterwards. `-diagnosticsOff` builds it with `CS_DEV_DIAGNOSTICS_OFF`.
Install it with `adb install -r <apk>`. A phone that holds the Play (upload-key) install must
uninstall that first, because Android refuses an update signed with a different key.

What the switch does not change:

- Unity's **"Development Build"** watermark in the corner. Unity has no setting for it.
- Asset-authored test shortcuts: the Episode Token config's unverified grants and the Weekly
  Challenge catalogue's `test` block. Each is switched on its own asset.
- Warnings and errors still reach logcat, as they do in a release build.
- A development build still runs a little slower than release: the profiler hooks and development
  checks stay compiled in. Take performance numbers from a release build
  (`Docs/PERFORMANCE_OPTIMIZATION.md`).

