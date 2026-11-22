using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// One place that answers "what did the player just do?" for both the keyboard/mouse and the Arduino controller.
///
///  - InteractPressed : E key OR the Arduino right-stick button, true for exactly one frame per press.
///                      Asking it from several scripts in the same frame gives the same answer to all of them
///                      (just like Keyboard.eKey.wasPressedThisFrame), and pressing both at once is still one press.
///  - JumpPressed     : Arduino left-stick button, one frame per press.
///  - OptionPressed   : Arduino D4 button, one frame per press. Only the decision dial reads it, to confirm the
///                      highlighted option; it is not Interact, so it never opens doors, skips lines or closes cards.
///  - Move / Look     : Arduino sticks as -1..1 vectors (y positive = pushed up). Zero when there is no live Arduino.
///
/// Nothing here is read while the player is locked; the callers keep their existing Locked / phase checks.
/// If there is no ArduinoController in the scene, or no recent data from it, every Arduino value is neutral and
/// the game behaves exactly as it did with only the keyboard.
/// A hidden driver object is created automatically at startup - there is nothing to add to the scene.
/// </summary>
[DefaultExecutionOrder(-900)]      // after ArduinoController (-1000), before everything else
public class GameInput : MonoBehaviour
{
    const float StaleAfter = 0.5f;       // seconds without a packet before the Arduino counts as unplugged

    static GameInput inst;

    ArduinoController ard;
    bool primed, prevLeft, prevRight, prevOption, jumpDown, interactDown, optionDown;
    float lastLeftPress = -10f, lastRightPress = -10f, lastOptionPress = -10f;
    Vector2 move, look;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (inst != null) return;
        var go = new GameObject("GameInput") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(go);
        inst = go.AddComponent<GameInput>();
    }

    // ------------------------------------------------------------------ public API
    /// <summary>E on the keyboard, or the Arduino right button: one frame per press.</summary>
    public static bool InteractPressed
    {
        get
        {
            if (GameState.Paused) return false;          // nothing behind the pause menu reacts
            var kb = Keyboard.current;
            return (kb != null && kb.eKey.wasPressedThisFrame) || (inst != null && inst.interactDown);
        }
    }

    /// <summary>Arduino left button: one frame per press.</summary>
    public static bool JumpPressed { get { return inst != null && inst.jumpDown; } }

    /// <summary>Arduino D4 (option) button: one frame per press. Never true while paused.</summary>
    public static bool OptionPressed { get { return !GameState.Paused && inst != null && inst.optionDown; } }

    /// <summary>Left stick: x = right, y = forward (both -1..1).</summary>
    public static Vector2 Move { get { return inst != null ? inst.move : Vector2.zero; } }

    /// <summary>Right stick: x = turn right, y = look up (both -1..1).</summary>
    public static Vector2 Look { get { return inst != null ? inst.look : Vector2.zero; } }

    public static float LookSpeed { get { return inst != null && inst.ard != null ? inst.ard.lookSpeed : 120f; } }
    public static float JumpSpeed { get { return inst != null && inst.ard != null ? inst.ard.jumpSpeed : 5f; } }

    // ------------------------------------------------------------------ per-frame sampling
    void Update()
    {
        jumpDown = false; interactDown = false; optionDown = false;
        if (ard == null) ard = FindFirstObjectByType<ArduinoController>();

        bool live = ard != null && ard.isConnected && Time.unscaledTime - ard.lastPacketTime < StaleAfter;
        if (!live)
        {
            // unplugged / no data: neutral, and forget the button state so a button that is held
            // when the data comes back is not counted as a fresh press
            primed = false; move = Vector2.zero; look = Vector2.zero;
            return;
        }
        if (!primed) { prevLeft = ard.leftButton; prevRight = ard.rightButton; prevOption = ard.optionButton; primed = true; }

        float t = Time.unscaledTime;
        if (ard.leftButton && !prevLeft && t - lastLeftPress >= ard.pressLockout) { jumpDown = true; lastLeftPress = t; }
        if (ard.rightButton && !prevRight && t - lastRightPress >= ard.pressLockout) { interactDown = true; lastRightPress = t; }
        // D4 reads 0 while pressed (INPUT_PULLUP), so a press is the pin falling from 1 to 0: optionButton turning
        // true. Held down it stays one press; a bounce within pressLockout is ignored.
        if (ard.optionButton && !prevOption && t - lastOptionPress >= ard.pressLockout) { optionDown = true; lastOptionPress = t; }
        prevLeft = ard.leftButton; prevRight = ard.rightButton; prevOption = ard.optionButton;

        // sticks: ArduinoController gives -1..1 with the deadzone already applied.
        // Default assumption: pushing a stick UP reads negative; the invert toggles flip that.
        move = new Vector2(ard.leftX, ard.invertMoveY ? ard.leftY : -ard.leftY);
        look = new Vector2(ard.rightX, ard.invertLookY ? ard.rightY : -ard.rightY);
    }

    void OnDestroy() { if (inst == this) inst = null; }
}
