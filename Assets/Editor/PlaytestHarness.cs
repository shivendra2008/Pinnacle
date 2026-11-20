using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Entry point for the automated play-test (see Assets/Scripts/Playtest/PlaytestDriver.cs and Docs/PLAYTEST.md).
/// Run on a COPY of the project (the open Editor locks this one):
///   PINNACLE_PLAYTEST=1 PINNACLE_SAVE_DIR=/tmp/saves PINNACLE_PLAYTEST_OUT=/tmp/out \
///   Unity -batchmode -projectPath <copy> -executeMethod PlaytestHarness.Run -logFile log.txt
/// The driver plays, captures images and results, then quits the Editor with exit code 0 (pass) or 1 (failures).
/// The same entry point runs the Curator capture (PINNACLE_CAPTURE) and the rig probe (PINNACLE_PROBE).
/// </summary>
public static class PlaytestHarness
{
    const string Pending = "Pinnacle.PlaytestPending";
    static int quietFrames;

    public static void Run()
    {
        RocketboxImport.EnsureNow();         // importer settings applied before play (a fresh copy may not have them yet)
        // A copy of the project can still recompile shortly after it opens (packages adding define symbols). A
        // recompile during Play reloads the scripts and drops the recorder's open files, so wait until the editor has
        // been idle for a while before pressing Play. The wait survives a script reload (SessionState + the hook below).
        SessionState.SetBool(Pending, true);
        quietFrames = 0;
        EditorApplication.update -= WaitThenPlay;
        EditorApplication.update += WaitThenPlay;
    }

    [InitializeOnLoadMethod]
    static void ResumeAfterReload()
    {
        if (!SessionState.GetBool(Pending, false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        quietFrames = 0;
        EditorApplication.update -= WaitThenPlay;
        EditorApplication.update += WaitThenPlay;
    }

    static void WaitThenPlay()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { quietFrames = 0; return; }
        if (++quietFrames < 600) return;
        EditorApplication.update -= WaitThenPlay;
        SessionState.SetBool(Pending, false);
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorApplication.isPlaying = true;
    }
}
