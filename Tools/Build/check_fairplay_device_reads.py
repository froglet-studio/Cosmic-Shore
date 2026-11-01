#!/usr/bin/env python3
"""Fair-play gate (Threat Board R16): anti-cheat reads no more from the player's device than it needs.

    python3 Tools/Build/check_fairplay_device_reads.py            # scan, exit 1 on any finding
    python3 Tools/Build/check_fairplay_device_reads.py --check    # same (CI spelling)
    python3 Tools/Build/check_fairplay_device_reads.py --self-test

The rule (Docs/AntiCheat/README.md section 5; EU ePrivacy Art. 5(3), Apple 5.1.2 and fingerprinting, Google Play's
QUERY_ALL_PACKAGES policy): fair play watches the GAME - what the server and the host can see of play - plus the
platform's own attestation (Apple App Attest and DeviceCheck, Google Play Integrity). It never reads hardware serials
or device fingerprints, other processes, installed apps, files outside the game, the registry, or the network
adapters, and never ships a kernel driver.

Two scopes:
  1. FAIR-PLAY code (the same files check_fairplay_paid_content.py calls fair-play, so phase B's FairPlay*, Undertow*,
     TrustRecord* and AntiCheat* files are covered the day they are made) may not use any API in DEVICE_READS. Any
     native call (DllImport, AndroidJavaObject/Class, a Java bridge) is reported too: the platform attestation, when
     it is built, goes in ALLOW below, one reviewed row per file and API, with its reason.
  2. The WHOLE PROJECT may not request Android's QUERY_ALL_PACKAGES, call Android's installed-apps queries from
     runtime code, or carry a kernel-driver file (.sys) under Assets/ or Packages/.

Comments are removed before scanning; string literals stay (a Java method name is a string).

What it does NOT prove: what an SDK does inside its own binaries (analytics, crash reporting, ads). Those are
declared on the store privacy labels (Docs/Legal/STORE_PRIVACY_LABELS.md) and reviewed when added.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from check_fairplay_paid_content import ROOT, fair_play_files, rel, strip_comments  # noqa: E402

DEVICE_READS = [
    # Hardware and identity fingerprints
    r"SystemInfo\.deviceUniqueIdentifier", r"SystemInfo\.deviceName", r"SystemInfo\.deviceModel",
    r"SystemInfo\.graphicsDeviceID", r"SystemInfo\.graphicsDeviceVendorID", r"SystemInfo\.processorType",
    r"Environment\.MachineName", r"Environment\.UserName", r"Environment\.UserDomainName",
    # Other processes, the registry, WMI, network adapters
    r"Process\.GetProcess(?:es|esByName|ById)?\b", r"Process\.Start\b", r"\bMicrosoft\.Win32\b", r"\bRegistry\.",
    r"\bSystem\.Management\b", r"\bManagementObjectSearcher\b", r"\bNetworkInterface\b", r"\bGetPhysicalAddress\b",
    # Files outside the game
    r"\bDriveInfo\b", r"Directory\.(?:GetFiles|EnumerateFiles|GetDirectories|EnumerateDirectories|GetLogicalDrives)\b",
    r"Environment\.GetFolderPath\b",
    # Native code: reviewed one by one through ALLOW
    r"\[DllImport\b", r"\bAndroidJavaObject\b", r"\bAndroidJavaClass\b", r"\bAndroidJavaProxy\b",
]
DEVICE_RE = re.compile("|".join("(?:%s)" % p for p in DEVICE_READS))

# One reviewed row per (file, API) a fair-play file may use, with the reason. Empty today.
# Example for when Play Integrity is built:
#   ("Assets/_Scripts/System/FairPlay/PlayIntegrityAttestation.cs", "AndroidJavaClass",
#    "Google Play Integrity API: the platform's own verdict, the one device signal fair play uses (R16)"),
ALLOW = [
    ("Assets/_Scripts/System/PlatformSignIn/GameCenterPlatform.cs", "DllImport",
     "Game Center sign-in (T1): the native half asks GameKit only for the local player's identity-verification "
     "signature, which Apple issues for signing in to a game server (Assets/Plugins/iOS/GameCenter/GameCenterSignIn.mm)"),
    ("Assets/_Scripts/System/PlatformSignIn/PlayGamesPlatform.cs", "AndroidJavaClass",
     "Play Games sign-in (T1): calls only the Java bridge in Assets/Plugins/Android/PlayGamesSignIn.androidlib, which asks Play Games whether its own sign-in succeeded and for a server auth code"),
    ("Assets/_Scripts/System/PlatformSignIn/PlayGamesPlatform.cs", "AndroidJavaObject",
     "Play Games sign-in (T1): calls only the Java bridge in Assets/Plugins/Android/PlayGamesSignIn.androidlib, which asks Play Games whether its own sign-in succeeded and for a server auth code"),
    ("Assets/_Scripts/System/PlatformSignIn/PlayGamesPlatform.cs", "AndroidJavaProxy",
     "Play Games sign-in (T1): calls only the Java bridge in Assets/Plugins/Android/PlayGamesSignIn.androidlib, which asks Play Games whether its own sign-in succeeded and for a server auth code"),
]

INSTALLED_APPS = re.compile(r"getInstalledApplications|getInstalledPackages|queryIntentActivities|QUERY_ALL_PACKAGES")


def scan_fair_play_text(label, text):
    found = []
    allowed = {api for f, api, _ in ALLOW if f == label}
    for i, line in enumerate(strip_comments(text).split("\n"), 1):
        for m in DEVICE_RE.finditer(line):
            api = m.group(0).lstrip("[")
            if not any(a in api for a in allowed):
                found.append((label, i, "fair-play code reads the device: " + api))
    return found


def scan_project():
    found = []
    for top in ("Assets", "Packages", "ProjectSettings"):
        base = os.path.join(ROOT, top)
        for d, dirs, files in os.walk(base):
            dirs[:] = [x for x in dirs if x not in ("Library", "node_modules", ".git")]
            for f in files:
                p = os.path.join(d, f)
                low = f.lower()
                if low.endswith(".sys"):
                    found.append((rel(p), 0, "a kernel-driver file ships with the game"))
                elif low.endswith(".xml") and "manifest" in low:
                    with open(p, encoding="utf-8", errors="replace") as fh:
                        for i, line in enumerate(fh, 1):
                            if "QUERY_ALL_PACKAGES" in line:
                                found.append((rel(p), i, "requests QUERY_ALL_PACKAGES (seeing every installed app)"))
                elif low.endswith((".cs", ".java", ".kt", ".jslib", ".mm", ".m")) and "/Editor/" not in rel(p) \
                        and "/Tests/" not in rel(p) and top == "Assets":
                    with open(p, encoding="utf-8", errors="replace") as fh:
                        body = strip_comments(fh.read()) if low.endswith((".cs", ".java", ".kt", ".jslib")) else fh.read()
                    for i, line in enumerate(body.split("\n"), 1):
                        m = INSTALLED_APPS.search(line)
                        if m:
                            found.append((rel(p), i, "queries the installed apps: " + m.group(0)))
    return found


def scan():
    findings = []
    files = fair_play_files()
    for p in files:
        if not p.endswith(".cs"):
            continue
        with open(p, encoding="utf-8", errors="replace") as fh:
            findings += scan_fair_play_text(rel(p), fh.read())
    findings += scan_project()
    for f, api, why in ALLOW:
        if not os.path.exists(os.path.join(ROOT, f)) or not why.strip():
            findings.append((f, 0, "ALLOW row for %s names a missing file or gives no reason" % api))
    return findings, len(files)


def self_test():
    cases = [
        ("var id = SystemInfo.deviceUniqueIdentifier;", 1),
        ("foreach (var p in Process.GetProcesses()) {}", 1),
        ("using Microsoft.Win32; var k = Registry.CurrentUser;", 2),
        ("var mac = NetworkInterface.GetAllNetworkInterfaces();", 1),
        ("var files = Directory.GetFiles(\"C:/\");", 1),
        ("[DllImport(\"kernel32\")] static extern int X();", 1),
        ("using var pm = new AndroidJavaObject(\"android.content.pm.PackageManager\");", 1),
        ("// never read SystemInfo.deviceUniqueIdentifier here", 0),
        ("var mem = SystemInfo.systemMemorySize; var t = Time.time;", 0),   # coarse capability reads are not listed
        ("var path = Application.persistentDataPath;", 0),
    ]
    bad = 0
    for text, want in cases:
        got = len(scan_fair_play_text("case.cs", text))
        if got != want:
            bad += 1
            print("SELF-TEST FAIL: %r -> %d finding(s), expected %d" % (text, got, want))
    for text, want in [("<uses-permission android:name=\"android.permission.QUERY_ALL_PACKAGES\"/>", True),
                       ("pm.Call<AndroidJavaObject>(\"getInstalledApplications\", 0);", True),
                       ("<uses-permission android:name=\"android.permission.INTERNET\"/>", False)]:
        if bool(INSTALLED_APPS.search(text)) != want:
            bad += 1
            print("SELF-TEST FAIL (project rule): %r" % text)
    print("self-test: %s (%d cases)" % ("OK" if bad == 0 else "%d FAILED" % bad, len(cases) + 3))
    return 1 if bad else 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    findings, n = scan()
    for label, line, what in findings:
        print("%s:%d: %s" % (label, line, what))
    print("fair-play device-read check: %d PROBLEM(S) (%d fair-play files; whole project for permissions, "
          "installed-app queries and kernel drivers)" % (len(findings), n))
    return 1 if findings else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
