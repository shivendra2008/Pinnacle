using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>The investigation's four bearings. Story coroutines own the choice indices and consequences.</summary>
public sealed class StoryCompass
{
    static readonly Vector2[] Directions = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
    readonly List<string> captions = new List<string>();
    string lastCaption;
    int captionPage;
    string[] labels, replies;
    bool[] locked, asked;
    int selected, opened;
    Vector2 pointer;
    GUIStyle label, detail, hint;

    public void Open(string[] labels, string[] replies, bool[] locked = null, bool[] asked = null)
    {
        this.labels = labels;
        this.replies = replies;
        this.locked = locked;
        this.asked = asked;
        lastCaption = null;
        selected = -1;
        pointer = Vector2.zero;
        opened = Time.frameCount;
    }

    bool Available(int index) => index >= 0 && index < labels.Length && (locked == null || !locked[index]);

    void Aim(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.18f) return;
        float best = 0.38f;
        for (int i = 0; i < labels.Length; i++)
        {
            float score = Vector2.Dot(direction.normalized, Directions[i]);
            if (score > best) { best = score; selected = i; }
        }
    }

    // The gameplay cursor stays captured. Mouse movement points the needle just like the right stick.
    public int Poll()
    {
        if (GameState.Paused || Time.frameCount == opened) return -1;
        var kb = Keyboard.current;
        var pad = Gamepad.current;
        var mouse = Mouse.current;
        if (mouse != null)
        {
            var delta = mouse.delta.ReadValue();
            if (delta.sqrMagnitude > 0.05f)
            {
                pointer = Vector2.ClampMagnitude(pointer + delta / 28f, 1f);
                Aim(pointer);
            }
        }
        if (pad != null) { Aim(pad.rightStick.ReadValue()); Aim(pad.dpad.ReadValue()); }
        Aim(GameInput.Look);
        if (kb != null)
        {
            if (kb.leftArrowKey.wasPressedThisFrame) Aim(Vector2.left);
            if (kb.rightArrowKey.wasPressedThisFrame) Aim(Vector2.right);
            if (kb.upArrowKey.wasPressedThisFrame) Aim(Vector2.up);
            if (kb.downArrowKey.wasPressedThisFrame) Aim(Vector2.down);
            if (kb.digit1Key.wasPressedThisFrame && Available(0)) return 0;
            if (kb.digit2Key.wasPressedThisFrame && Available(1)) return 1;
            if (kb.digit3Key.wasPressedThisFrame && Available(2)) return 2;
            if (kb.digit4Key.wasPressedThisFrame && Available(3)) return 3;
        }
        bool confirm = GameInput.InteractPressed || (mouse != null && mouse.leftButton.wasPressedThisFrame)
            || (pad != null && pad.buttonSouth.wasPressedThisFrame)
            || (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame));
        return confirm && Available(selected) ? selected : -1;
    }

    void Styles()
    {
        if (label != null) return;
        label = new GUIStyle(GUI.skin.label) { font = UiKit.Body, fontSize = 19, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter, wordWrap = true };
        detail = new GUIStyle(label) { fontStyle = FontStyle.Normal, fontSize = 21 };
        hint = new GUIStyle(detail) { fontSize = 15 };
    }

    void Write(Rect rect, string text, GUIStyle style, Color color)
    {
        style.normal.textColor = color;
        UiKit.Text(rect, text, style);
    }

    public void Draw(float w, float h, string speaker, float remaining = -1f)
    {
        if (labels == null) return;
        Styles();
        var centre = new Vector2(w * 0.5f, h * 0.66f);
        float spread = Mathf.Min(185f, w * 0.23f);
        float sideWidth = Mathf.Min(230f, w * 0.36f);
        UiKit.Ring(new Rect(centre.x - 30, centre.y - 30, 60, 60), UiKit.Cream, 1.4f);
        UiKit.Ring(new Rect(centre.x - 24, centre.y - 24, 48, 48), UiKit.Gold, 1f);
        if (remaining >= 0f)
        {
            // Twenty-four marks around the dial count down clockwise from twelve.
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI / 12f;
                var point = centre + new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * 37f;
                UiKit.Fill(new Rect(point.x - 1.5f, point.y - 1.5f, 3, 3),
                    i < Mathf.CeilToInt(24 * remaining) ? UiKit.Gold : UiKit.Muted * 0.55f);
            }
        }
        if (selected >= 0)
        {
            Vector2 dir = Directions[selected];
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg, centre);
            UiKit.Fill(new Rect(centre.x - 7f, centre.y - 1.5f, 30f, 3f), UiKit.Gold);
            GUI.matrix = matrix;
        }
        for (int i = 0; i < labels.Length; i++)
        {
            var p = centre + new Vector2(Directions[i].x * spread, -Directions[i].y * 72f);
            bool seen = asked != null && i < asked.Length && asked[i];
            string state = !Available(i) ? (seen ? "  ·  asked" : "  ·  locked") : seen ? "  ·  asked" : "";
            var color = !Available(i) ? UiKit.Muted : i == selected ? UiKit.Gold : UiKit.Cream;
            Write(new Rect(p.x - sideWidth / 2, p.y - 21, sideWidth, 42), (i + 1) + "   " + labels[i] + state, label, color);
        }
        Write(new Rect(w * 0.12f, centre.y - 127, w * 0.76f, 25), speaker, hint, UiKit.Gold);
        string preview = selected >= 0 ? replies[selected] : "";
        float textWidth = w * 0.82f;
        float textY = centre.y + (labels.Length == 4 ? 100f : 52f);
        float textH = detail.CalcHeight(new GUIContent(preview), textWidth) + 5f;
        Write(new Rect((w - textWidth) / 2, textY, textWidth, textH), preview, detail, UiKit.Cream);
        Write(new Rect(w * 0.04f, h - 38, w * 0.92f, 33),
            "Point with mouse, arrows or stick   ·   Click / E / A to choose   ·   1–" + labels.Length + " quick select", hint, UiKit.Cream);
    }

    public void Speak(float w, float h, string speaker, string text, AudioSource source)
    {
        Styles();
        if (text != lastCaption)
        {
            lastCaption = text;
            captionPage = 0;
            captions.Clear();
            // Page a long voiced answer at word boundaries so a witness's face stays visible.
            int start = 0;
            while (start < text.Length)
            {
                int end = Mathf.Min(start + 110, text.Length);
                if (end < text.Length)
                {
                    int cut = text.LastIndexOf(' ', end - 1, end - start);
                    if (cut > start) end = cut + 1;
                }
                captions.Add(text.Substring(start, end - start));
                start = end;
            }
        }
        if (source != null && source.clip != null && captions.Count > 1)
        {
            int offset = Mathf.FloorToInt(Mathf.Clamp01(source.time / source.clip.length) * text.Length);
            int i = 0;
            while (i < captions.Count - 1 && offset >= captions[i].Length)
            {
                offset -= captions[i].Length;
                i++;
            }
            // AudioSource.time returns to zero when playback ends. Hold the last visible caption.
            captionPage = Mathf.Max(captionPage, i);
            text = captions[captionPage].Trim();
        }
        float width = Mathf.Min(w * 0.83f, 960f);
        float height = detail.CalcHeight(new GUIContent(text), width) + 8f;
        var r = new Rect((w - width) / 2, h - 63f - height, width, height);
        Write(new Rect(r.x, r.y - 24, width, 22), speaker, hint, UiKit.Gold);
        Write(r, text, detail, UiKit.Cream);
    }
}
