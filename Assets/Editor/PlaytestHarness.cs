using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Entry point for the automated play-test (see Assets/Scripts/Playtest/PlaytestDriver.cs and Docs/PLAYTEST.md).
/// Run on a COPY of the project (the open Editor locks this one):
///   PINNACLE_PLAYTEST=1 PINNACLE_SAVE_DIR=/tmp/saves PINNACLE_PLAYTEST_OUT=/tmp/out \
///   Unity -batchmode -projectPath <copy> -executeMethod PlaytestHarness.Run -logFile log.txt
/// The driver plays, captures images and results, then quits the Editor with exit code 0 (pass) or 1 (failures).
/// </summary>
public static class PlaytestHarness
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorApplication.isPlaying = true;
    }
}
