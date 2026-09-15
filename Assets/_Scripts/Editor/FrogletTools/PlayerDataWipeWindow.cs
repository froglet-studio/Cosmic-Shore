using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CosmicShore.Core;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>
    /// Wipe this machine's player data — every layer of it, in one place.
    ///
    /// <para><b>Why this exists.</b> "Delete my data" has FOUR answers in this project and three of
    /// them are invisible from the UGS dashboard. Clearing PlayerPrefs and deleting the player in
    /// Player Management leaves the game happily reading a player's progress back, because the
    /// repositories keep a last-known-good snapshot on local disk
    /// (<c>{persistentDataPath}/CloudCache/</c>, <see cref="LocalCloudDataCache"/>) that neither of
    /// those touches. The session token is a fourth: it lives in the Authentication SDK's own
    /// storage, so a client can keep re-authenticating as a player whose account is gone.</para>
    ///
    /// <para><b>Each layer is a separate switch, and that is the point.</b> A single "delete
    /// everything" button teaches nobody where the data was. The report says which layer held
    /// what, which is the thing that was actually missing when this was diagnosed by hand.</para>
    /// </summary>
    public class PlayerDataWipeWindow : EditorWindow
    {
        [MenuItem("FrogletTools/Services/Wipe Player Data", false, 20)]
        [FrogletTool(FrogletToolCategory.Services, Importance = 4,
            Description = "Clear PlayerPrefs, the local cloud snapshot, UGS Cloud Save and the " +
                          "session token — the four places player data actually lives.")]
        public static void Open() =>
            GetWindow<PlayerDataWipeWindow>(true, "Wipe Player Data").minSize = new Vector2(520f, 460f);

        [SerializeField] bool wipePlayerPrefs = true;
        [SerializeField] bool wipeLocalCache = true;
        [SerializeField] bool wipeCloudSave = true;
        [SerializeField] bool clearSessionToken = true;
        [SerializeField] bool deleteAccount;

        readonly List<string> _log = new();
        Vector2 _scroll;
        bool _running;

        void OnGUI()
        {
            FrogletEditorPalette.Banner("Wipe Player Data",
                "Every layer that can hand a player their progress back",
                FrogletEditorPalette.ColorFor(FrogletToolCategory.Services));

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Deleting a player in the UGS dashboard does NOT clear the two local layers. That " +
                "is why a \"deleted\" player can still launch straight into their own save.",
                MessageType.Info);

            EditorGUILayout.Space(4);

            wipePlayerPrefs = EditorGUILayout.ToggleLeft(
                new GUIContent("PlayerPrefs (all keys)",
                    "Settings, volumes, the return-to screen - and on some platforms the SDK's own " +
                    "scratch. Local to this machine."),
                wipePlayerPrefs);

            wipeLocalCache = EditorGUILayout.ToggleLeft(
                new GUIContent("Local cloud snapshot",
                    "The offline last-known-good copy of every Cloud Save key. THE one people miss."),
                wipeLocalCache);

            wipeCloudSave = EditorGUILayout.ToggleLeft(
                new GUIContent("UGS Cloud Save (this player's keys)",
                    "Deletes every key in UGSKeys for the SIGNED-IN player. Needs to be signed in."),
                wipeCloudSave);

            clearSessionToken = EditorGUILayout.ToggleLeft(
                new GUIContent("Sign out and clear the session token",
                    "Without this the next launch re-authenticates as the SAME player id, so a " +
                    "fresh anonymous account is never created and the data appears to come back."),
                clearSessionToken);

            EditorGUILayout.Space(2);
            deleteAccount = EditorGUILayout.ToggleLeft(
                new GUIContent("Also DELETE the UGS account",
                    "Irreversible, and it removes the player from Player Management outright. " +
                    "Leave off unless you mean it - clearing the session token already gives you " +
                    "a fresh player on the next launch."),
                deleteAccount);

            if (deleteAccount)
                EditorGUILayout.HelpBox(
                    "The account is deleted permanently. Cloud Save goes with it, so the Cloud " +
                    "Save switch above becomes redundant.", MessageType.Warning);

            EditorGUILayout.Space(8);
            DrawWhereItLives();

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(_running || !AnythingSelected()))
            {
                if (FrogletEditorPalette.ColorButton(
                        _running ? "Working…" : "Wipe", FrogletEditorPalette.Error, 140f, 28f))
                    Run();
            }

            DrawLog();
        }

        bool AnythingSelected() =>
            wipePlayerPrefs || wipeLocalCache || wipeCloudSave || clearSessionToken || deleteAccount;

        /// <summary>
        /// The paths and the signed-in id, SHOWN rather than described. A wipe tool that says
        /// "cleared the local cache" without saying where teaches the reader nothing the next time
        /// they have to check it by hand.
        /// </summary>
        void DrawWhereItLives()
        {
            EditorGUILayout.LabelField("Where it lives", FrogletEditorPalette.SectionLabel);

            EditorGUILayout.LabelField("PlayerPrefs", PlayerPrefsLocationHint(),
                                       FrogletEditorPalette.CardBody);
            EditorGUILayout.LabelField("Local snapshot",
                string.IsNullOrEmpty(LocalCloudDataCache.RootPath)
                    ? "(unavailable — persistentDataPath did not resolve)"
                    : LocalCloudDataCache.RootPath,
                FrogletEditorPalette.CardBody);
            EditorGUILayout.LabelField("Signed in as", SignedInId() ?? "(not signed in)",
                                       FrogletEditorPalette.CardBody);
        }

        static string PlayerPrefsLocationHint() =>
#if UNITY_EDITOR_WIN
            @"HKCU\Software\Unity\UnityEditor\<Company>\<Product>";
#elif UNITY_EDITOR_OSX
            "~/Library/Preferences/unity.<Company>.<Product>.plist";
#else
            "~/.config/unity3d/<Company>/<Product>";
#endif

        static string SignedInId()
        {
            try
            {
                return UnityServices.State == ServicesInitializationState.Initialized &&
                       AuthenticationService.Instance != null &&
                       AuthenticationService.Instance.IsSignedIn
                    ? AuthenticationService.Instance.PlayerId
                    : null;
            }
            catch { return null; }
        }

        void DrawLog()
        {
            if (_log.Count == 0) return;

            FrogletEditorPalette.HorizontalRule();
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(140f));
            foreach (string line in _log)
                EditorGUILayout.LabelField(line, EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();
        }

        // ── The wipe ───────────────────────────────────────────────────────────

        async void Run()
        {
            _running = true;
            _log.Clear();

            try
            {
                // ORDER MATTERS. Cloud Save needs a signed-in player, so it runs BEFORE the sign
                // out; and the account delete runs last because it invalidates everything above it.
                if (wipeCloudSave) await WipeCloudSaveAsync();
                if (wipeLocalCache) WipeLocalCache();
                if (wipePlayerPrefs) WipePlayerPrefs();
                if (deleteAccount) await DeleteAccountAsync();
                else if (clearSessionToken) ClearSession();

                _log.Add("");
                _log.Add("Done. Restart play mode (or the app) for the next launch to start clean.");
            }
            catch (Exception e)
            {
                _log.Add($"FAILED: {e.Message}");
            }
            finally
            {
                _running = false;
                Repaint();
            }
        }

        void WipePlayerPrefs()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            _log.Add("PlayerPrefs: all keys deleted.");
        }

        void WipeLocalCache()
        {
            int removed = LocalCloudDataCache.DeleteAll();
            _log.Add(removed > 0
                ? $"Local snapshot: {removed} file(s) deleted from {LocalCloudDataCache.RootPath}."
                : "Local snapshot: nothing to delete.");
        }

        async Task WipeCloudSaveAsync()
        {
            string id = SignedInId();
            if (id == null)
            {
                _log.Add("Cloud Save: SKIPPED - not signed in. Enter play mode and let the boot " +
                         "chain sign in, then run this again.");
                return;
            }

            var provider = new UGSCloudSaveProvider();
            int gone = 0, failed = 0;

            foreach (string key in CloudSaveKeys())
            {
                bool ok = await provider.DeleteAsync(key);
                if (ok) gone++; else failed++;
                _log.Add($"Cloud Save: {key} {(ok ? "deleted" : "FAILED")}");
            }

            _log.Add($"Cloud Save: {gone} key(s) gone, {failed} failed (player {id}).");
        }

        /// <summary>
        /// Every Cloud Save key, read off <see cref="UGSKeys"/> by REFLECTION rather than listed
        /// here. A hand-kept list in a wipe tool goes stale the first time somebody adds a key, and
        /// the failure is a wipe that quietly leaves data behind - which is the exact bug this tool
        /// exists to fix.
        /// </summary>
        static IEnumerable<string> CloudSaveKeys() =>
            typeof(UGSKeys)
                .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue())
                // Analytics EVENT names live in the same class and are not Cloud Save keys. The
                // keys are SCREAMING_SNAKE_CASE by the standard UGSKeys itself states; the events
                // are lower_snake_case. Filtering on the case is what keeps this honest without a
                // second list to maintain.
                .Where(k => !string.IsNullOrEmpty(k) && k == k.ToUpperInvariant())
                .Distinct();

        void ClearSession()
        {
            try
            {
                if (AuthenticationService.Instance == null)
                {
                    _log.Add("Session: SKIPPED - the Authentication service is not initialised.");
                    return;
                }

                if (AuthenticationService.Instance.IsSignedIn)
                    AuthenticationService.Instance.SignOut();

                AuthenticationService.Instance.ClearSessionToken();
                _log.Add("Session: signed out and token cleared - the next launch mints a NEW " +
                         "anonymous player.");
            }
            catch (Exception e)
            {
                _log.Add($"Session: FAILED - {e.Message}");
            }
        }

        async Task DeleteAccountAsync()
        {
            try
            {
                if (SignedInId() == null)
                {
                    _log.Add("Account: SKIPPED - not signed in.");
                    return;
                }

                await AuthenticationService.Instance.DeleteAccountAsync();
                _log.Add("Account: DELETED. The session token goes with it.");
            }
            catch (Exception e)
            {
                _log.Add($"Account: FAILED - {e.Message}");
            }
        }
    }
}
