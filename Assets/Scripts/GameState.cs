using UnityEngine;

/// <summary>
/// The one owner of the game's global state while playing: paused or not, and the time scale, audio pause,
/// cursor and player lock that go with it. Each story script used to change these by itself.
///
/// Pausing sets Time.timeScale to 0, but coroutines that wait with "yield return null" keep running every frame,
/// so a dialogue loop that reads the keyboard would still accept a choice or a skip behind the pause menu. Every
/// such loop checks GameState.Paused (and GameInput.InteractPressed returns false while paused).
/// </summary>
public static class GameState
{
    public static bool Paused { get; private set; }
    static bool savedLocked;

    public static void SetPaused(bool p)
    {
        if (p == Paused) return;
        Paused = p;
        if (p)
        {
            savedLocked = PriyaRoomPlayer.Locked;
            PriyaRoomPlayer.Locked = true;
            Time.timeScale = 0f; AudioListener.pause = true;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        else
        {
            PriyaRoomPlayer.Locked = savedLocked;
            Time.timeScale = 1f; AudioListener.pause = false;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }
    }

    /// <summary>
    /// Called whenever an act, an interlude or the main menu takes over: nothing paused, time running, audio on.
    /// (Leaving a paused game through "Quit to main menu" used to depend on each script undoing this itself.)
    /// </summary>
    public static void ResetForNewScene()
    {
        Paused = false;
        Time.timeScale = 1f; AudioListener.pause = false;
        PriyaRoomPlayer.LookOnly = false;          // a seat or a hiding place does not outlive its scene
    }
}
