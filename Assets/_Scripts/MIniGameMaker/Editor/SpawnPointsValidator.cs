using CosmicShore.Tools.MiniGameMaker;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

sealed class SpawnPointsValidator : IValidator
{
    public string Name => "SpawnPoints (1 or 2)";
    int DesiredCount => Mathf.Clamp(EditorPrefs.GetInt("CS_MGM_SpawnCount", 2), 1, 2); // read what view used (optional)

    public (Severity,string) Check()
    {
        var game = SceneUtil.Find("Game");
        if (!game) return (Severity.Fail, "Game missing");
        var sp = game.transform.Find("SpawnPoints");
        if (!sp) return (Severity.Fail, "SpawnPoints missing");
        var has1 = sp.Find("1") != null;
        var has2 = sp.Find("2") != null;
        if (DesiredCount == 1) return has1 ? (Severity.Pass, "OK (1)") : (Severity.Fail, "Child '1' missing");
        return (has1 && has2) ? (Severity.Pass, "OK (2)") : (Severity.Fail, "Children '1'/'2' missing");
    }

    public void Fix()
    {
        var game = SceneUtil.Find("Game") ?? new GameObject("Game");
        var sp = game.transform.Find("SpawnPoints")?.gameObject ?? new GameObject("SpawnPoints");
        sp.transform.SetParent(game.transform, false);
        if (!sp.transform.Find("1")) new GameObject("1").transform.SetParent(sp.transform, false);
        if (DesiredCount == 2 && !sp.transform.Find("2"))
        {
            var sp2 = new GameObject("2"); sp2.transform.SetParent(sp.transform, false);
            sp2.transform.position = new Vector3(5, 0, 0);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
}