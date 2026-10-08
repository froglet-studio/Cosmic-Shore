# Branch archive: `claude/add-friday-build-notify-fhh6q`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-02 by Claude
- **Unmerged commits:** 1
- **Forked from:** `af1c0c5a4` (2026-03-02, Merge pull request #329 from froglet-studio/claude/add-drift-easing-i8LJN)
- **Tip:** `6c899029e`
- **Files touched (1):**
  - `.github/workflows/friday-build-notify.yml`

### `6c899029e` — Add Friday build & Discord notification workflow

_Claude, 2026-03-02 17:55:29 +0000_

```text
Triggers a Unity Cloud Build via the DevOps Build Automation API
every Friday (and on manual dispatch), polls build status every 60s
with a 60-minute timeout, then posts a Discord embed on success
(with download link) or failure.
```

```text
 .github/workflows/friday-build-notify.yml | 200 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 200 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 206 lines)</summary>

```diff
diff --git a/.github/workflows/friday-build-notify.yml b/.github/workflows/friday-build-notify.yml
new file mode 100644
index 000000000..19750bb8f
--- /dev/null
+++ b/.github/workflows/friday-build-notify.yml
@@ -0,0 +1,200 @@
+name: Friday Build & Notify
+
+on:
+  schedule:
+    - cron: '0 18 * * 5' # Every Friday at 6 PM UTC
+  workflow_dispatch:
+
+jobs:
+  build-and-notify:
+    runs-on: ubuntu-latest
+    timeout-minutes: 65
+
+    env:
+      UCB_API_BASE: https://build-api.cloud.unity3d.com/api/v1
+
+    steps:
+      - name: Trigger Unity Cloud Build
+        id: trigger
+        env:
+          UCB_KEY_ID: ${{ secrets.UCB_KEY_ID }}
+          UCB_SECRET_KEY: ${{ secrets.UCB_SECRET_KEY }}
+          UCB_ORG_ID: ${{ secrets.UCB_ORG_ID }}
+          UCB_PROJECT_ID: ${{ secrets.UCB_PROJECT_ID }}
+          UCB_BUILD_TARGET_ID: ${{ secrets.UCB_BUILD_TARGET_ID }}
+        run: |
+          AUTH=$(echo -n "${UCB_KEY_ID}:${UCB_SECRET_KEY}" | base64 -w 0)
+          ENDPOINT="${UCB_API_BASE}/orgs/${UCB_ORG_ID}/projects/${UCB_PROJECT_ID}/buildtargets/${UCB_BUILD_TARGET_ID}/builds"
+
+          RESPONSE=$(curl -s -w "\n%{http_code}" \
+            -X POST \
+            -H "Authorization: Basic ${AUTH}" \
+            -H "Content-Type: application/json" \
+            -d '{"clean":false,"platform":"standalonewindows64"}' \
+            "${ENDPOINT}")
+
+          HTTP_CODE=$(echo "${RESPONSE}" | tail -1)
+          BODY=$(echo "${RESPONSE}" | sed '$d')
+
+          echo "HTTP Status: ${HTTP_CODE}"
+
+          if [[ "${HTTP_CODE}" -lt 200 || "${HTTP_CODE}" -ge 300 ]]; then
+            echo "trigger_error=Failed to trigger build (HTTP ${HTTP_CODE})" >> "${GITHUB_OUTPUT}"
+            echo "::error::Failed to trigger build. HTTP ${HTTP_CODE}: ${BODY}"
+            exit 1
+          fi
+
+          BUILD_NUMBER=$(echo "${BODY}" | jq -r '.[0].build')
+          if [[ -z "${BUILD_NUMBER}" || "${BUILD_NUMBER}" == "null" ]]; then
+            echo "trigger_error=Could not parse build number from API response" >> "${GITHUB_OUTPUT}"
+            echo "::error::Could not extract build number. Response: ${BODY}"
+            exit 1
+          fi
+
+          echo "build_number=${BUILD_NUMBER}" >> "${GITHUB_OUTPUT}"
+          echo "Build #${BUILD_NUMBER} triggered successfully."
+
+      - name: Poll Build Status
+        id: poll
+        env:
+          UCB_KEY_ID: ${{ secrets.UCB_KEY_ID }}
+          UCB_SECRET_KEY: ${{ secrets.UCB_SECRET_KEY }}
+          UCB_ORG_ID: ${{ secrets.UCB_ORG_ID }}
+          UCB_PROJECT_ID: ${{ secrets.UCB_PROJECT_ID }}
+          UCB_BUILD_TARGET_ID: ${{ secrets.UCB_BUILD_TARGET_ID }}
+          BUILD_NUMBER: ${{ steps.trigger.outputs.build_number }}
+        run: |
+          AUTH=$(echo -n "${UCB_KEY_ID}:${UCB_SECRET_KEY}" | base64 -w 0)
+          ENDPOINT="${UCB_API_BASE}/orgs/${UCB_ORG_ID}/projects/${UCB_PROJECT_ID}/buildtargets/${UCB_BUILD_TARGET_ID}/builds/${BUILD_NUMBER}"
+
+          MAX_POLLS=60
+          POLL=0
+
+          while [[ ${POLL} -lt ${MAX_POLLS} ]]; do
+            POLL=$((POLL + 1))
+            echo "--- Poll ${POLL}/${MAX_POLLS} ---"
+
+            BODY=$(curl -s -H "Authorization: Basic ${AUTH}" "${ENDPOINT}")
+            STATUS=$(echo "${BODY}" | jq -r '.buildStatus')
+            echo "Status: ${STATUS}"
+
+            case "${STATUS}" in
+              success)
+                DOWNLOAD_URL=$(echo "${BODY}" | jq -r '.links.download_primary.href // empty')
+                TARGET_NAME=$(echo "${BODY}" | jq -r '.buildTargetName // "Unknown"')
+                TOTAL_SECS=$(echo "${BODY}" | jq -r '.totalTimeInSeconds // 0')
+                MINS=$((TOTAL_SECS / 60))
+                SECS=$((TOTAL_SECS % 60))
+
+                echo "build_status=success" >> "${GITHUB_OUTPUT}"
+                echo "build_number=${BUILD_NUMBER}" >> "${GITHUB_OUTPUT}"
+                echo "build_target_name=${TARGET_NAME}" >> "${GITHUB_OUTPUT}"
+                echo "build_time=${MINS}m ${SECS}s" >> "${GITHUB_OUTPUT}"
+                {
+                  echo "download_url<<DLEOF"
+                  echo "${DOWNLOAD_URL}"
+                  echo "DLEOF"
+                } >> "${GITHUB_OUTPUT}"
+                echo "Build succeeded in ${MINS}m ${SECS}s"
+                exit 0
+                ;;
+              failure|canceled)
+                FAIL_MSG=$(echo "${BODY}" | jq -r '(.failureDetails // []) | join("; ")' 2>/dev/null || echo "No details available")
+                echo "build_status=${STATUS}" >> "${GITHUB_OUTPUT}"
+                echo "build_number=${BUILD_NUMBER}" >> "${GITHUB_OUTPUT}"
+                {
+                  echo "failure_details<<FDEOF"
+                  echo "${FAIL_MSG}"
+                  echo "FDEOF"
+                } >> "${GITHUB_OUTPUT}"
+                echo "::error::Build ${STATUS}: ${FAIL_MSG}"
+                exit 1
+                ;;
+              created|queued|sentToBuilder|started|restarted)
+                sleep 60
+                ;;
+              *)
+                echo "::warning::Unexpected status '${STATUS}', continuing to poll..."
+                sleep 60
+                ;;
+            esac
+          done
+
+          echo "build_status=timeout" >> "${GITHUB_OUTPUT}"
+          echo "build_number=${BUILD_NUMBER}" >> "${GITHUB_OUTPUT}"
+          echo "::error::Build timed out after 60 minutes"
+          exit 1
+
+      - name: Discord Notification — Success
+        if: steps.poll.outputs.build_status == 'success'
+        env:
+          DISCORD_WEBHOOK_URL: ${{ secrets.DISCORD_WEBHOOK_URL }}
+          DOWNLOAD_URL: ${{ steps.poll.outputs.download_url }}
+          BUILD_NUMBER: ${{ steps.poll.outputs.build_number }}
+          BUILD_TARGET: ${{ steps.poll.outputs.build_target_name }}
+          BUILD_TIME: ${{ steps.poll.outputs.build_time }}
+        run: |
+          if [[ -n "${DOWNLOAD_URL}" ]]; then
+            LINK_LINE="[**Download Build**](${DOWNLOAD_URL})"
+          else
+            LINK_LINE="_No download link available._"
+          fi
+
+          PAYLOAD=$(jq -n \
+            --arg title "Build Succeeded" \
```

</details>
