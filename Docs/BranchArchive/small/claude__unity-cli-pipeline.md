# Branch archive: `claude/unity-cli-pipeline`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-08-21 by Shombith03
- **Unmerged commits:** 1
- **Forked from:** `44ffaa79e` (2026-08-20, Merge branch 'bleeding-edge' of https://github.com/froglet-studio/Cosmic-Shore)
- **Tip:** `adb01e57a`
- **Files touched (2):**
  - `Packages/manifest.json`
  - `Packages/packages-lock.json`

### `adb01e57a` — Add com.unity.pipeline for CLI-driven Editor verification

_Shombith03, 2026-08-21 21:36:22 +0530_

```text
 Packages/manifest.json      |  3 ++-
 Packages/packages-lock.json | 14 ++++++++++++++
 2 files changed, 16 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Packages/manifest.json b/Packages/manifest.json
index e779e9d6e..b7b71a5b5 100644
--- a/Packages/manifest.json
+++ b/Packages/manifest.json
@@ -75,7 +75,8 @@
     "com.unity.modules.video": "1.0.0",
     "com.unity.modules.vr": "1.0.0",
     "com.unity.modules.wind": "1.0.0",
-    "com.unity.modules.xr": "1.0.0"
+    "com.unity.modules.xr": "1.0.0",
+    "com.unity.pipeline": "0.5.0-exp.1"
   },
   "scopedRegistries": []
 }
diff --git a/Packages/packages-lock.json b/Packages/packages-lock.json
index 09bfa0713..7228bc6f8 100644
--- a/Packages/packages-lock.json
+++ b/Packages/packages-lock.json
@@ -333,6 +333,20 @@
       "dependencies": {},
       "url": "https://packages.unity.com"
     },
+    "com.unity.pipeline": {
+      "version": "0.5.0-exp.1",
+      "depth": 0,
+      "source": "registry",
+      "dependencies": {
+        "com.unity.test-framework": "1.1.33",
+        "com.unity.nuget.mono-cecil": "1.11.6",
+        "com.unity.modules.uielements": "1.0.0",
+        "com.unity.modules.jsonserialize": "1.0.0",
+        "com.unity.modules.screencapture": "1.0.0",
+        "com.unity.nuget.newtonsoft-json": "3.0.2"
+      },
+      "url": "https://packages.unity.com"
+    },
     "com.unity.profiling.core": {
       "version": "1.0.3",
       "depth": 1,
```

</details>
