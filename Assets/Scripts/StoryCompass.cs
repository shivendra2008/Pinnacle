using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The investigation's decision compass, used at every reachable decision in Acts I and II, plus the subtitles the
/// conversations speak through. A small brass dial sits low in the frame so the witness's eyes stay clear; short
/// intentions sit at its four bearings (index 0 left, 1 right, 2 up, 3 down: "leave" is always down), the needle
/// follows relative mouse or trackpad movement, the arrow keys, a stick or the d-pad, and the focused bearing previews
/// the line Arjun will actually say. Click, E, Enter, Space, the pad's south button or the Arduino's D4 button
/// commits the focused bearing; 1-4 commit directly. Story coroutines own the choice indices and their consequences:
/// this class only reports which index was committed, so every reply keeps its original index, line and trust result.
/// </summary>
public sealed class StoryCompass
{
    static readonly Vector2[] Directions = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
    const float ArmDelay = 0.22f;          // presses this soon after the dial opens are left over from skipping a line
    const float AfterglowTime = 0.5f;

    string[] labels, replies;
    bool[] locked, asked;
    string lockedHint;
    int selected = -1, opened;
    float openedAt;
    Vector2 pointer;

    // the last commitment, drawn briefly after the story has moved on so the decision reads as taken
    int committed = -1; float committedAt = -10f; string committedLabel; Vector2 committedFrom;

    // subtitles: phrases of a voiced line, shown in step with its audio
    readonly List<string> phrases = new List<string>();
    readonly List<float> phraseEnds = new List<float>();
    string lastText; AudioClip lastClip; int phrase;

    GUIStyle label, detail, hint, caps;
    static Texture2D dialTex, needleTex, glowTex;

    public bool IsOpen => labels != null;

    public void Open(string[] labels, string[] replies, bool[] locked = null, bool[] asked = null, string lockedHint = null)
    {
        this.labels = labels;
        this.replies = replies;
        this.locked = locked;
        this.asked = asked;
        this.lockedHint = lockedHint;
        selected = -1;
        pointer = Vector2.zero;
        opened = Time.frameCount;
        openedAt = Time.unscaledTime;
    }

    /// <summary>Closes the dial (it also closes itself when a bearing is committed).</summary>
    public void Close() { labels = null; }

    bool Available(int index) => labels != null && index >= 0 && index < labels.Length && (locked == null || index >= locked.Length || !locked[index]);
    bool Asked(int index) => asked != null && index >= 0 && index < asked.Length && asked[index];

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

    /// <summary>
    /// Reads every input device once per frame. Returns the committed index, or -1. Paused, or in the frame the dial
    /// opened, nothing is read. The gameplay cursor stays captured: mouse movement turns the needle like a stick.
    /// </summary>
    public int Poll()
    {
        if (labels == null || GameState.Paused || Time.frameCount == opened) return -1;
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
        if (pad != null)
        {
            var stick = pad.rightStick.ReadValue();
            if (stick.sqrMagnitude < 0.18f) stick = pad.leftStick.ReadValue();
            if (stick.sqrMagnitude >= 0.18f) { pointer = Vector2.ClampMagnitude(stick, 1f); Aim(stick); }
            Aim(pad.dpad.ReadValue());
        }
        var look = GameInput.Look;                        // the Arduino's right stick
        if (look.sqrMagnitude >= 0.18f) { pointer = Vector2.ClampMagnitude(look, 1f); Aim(look); }
        if (kb != null)
        {
            if (kb.leftArrowKey.wasPressedThisFrame) { Aim(Vector2.left); pointer = Vector2.left * 0.7f; }
            if (kb.rightArrowKey.wasPressedThisFrame) { Aim(Vector2.right); pointer = Vector2.right * 0.7f; }
            if (kb.upArrowKey.wasPressedThisFrame) { Aim(Vector2.up); pointer = Vector2.up * 0.7f; }
            if (kb.downArrowKey.wasPressedThisFrame) { Aim(Vector2.down); pointer = Vector2.down * 0.7f; }
        }
        if (Time.unscaledTime - openedAt < ArmDelay) return -1;
        if (kb != null)
        {
            int quick = kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame ? 0
                      : kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame ? 1
                      : kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame ? 2
                      : kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame ? 3 : -1;
            if (quick >= 0)
            {
                if (Available(quick)) return Commit(quick);
                selected = quick < labels.Length ? quick : selected;      // a locked or missing number only focuses it
            }
        }
        bool confirm = GameInput.InteractPressed || (mouse != null && mouse.leftButton.wasPressedThisFrame)
            || GameInput.OptionPressed                    // the Arduino's D4 button, which only ever confirms an option
            || (pad != null && pad.buttonSouth.wasPressedThisFrame)
            || (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame));
        return confirm && Available(selected) ? Commit(selected) : -1;
    }

    int Commit(int index)
    {
        committed = index; committedAt = Time.unscaledTime; committedLabel = labels[index];
        committedFrom = Directions[index];
        labels = null;
        return index;
    }

    // ================================================================== drawing
    void Styles()
    {
        if (label != null) return;
        label = new GUIStyle(GUI.skin.label) { font = UiKit.Body, fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
        detail = new GUIStyle(GUI.skin.label) { font = UiKit.Body, fontSize = 21, alignment = TextAnchor.UpperCenter, wordWrap = true };
        hint = new GUIStyle(GUI.skin.label) { font = UiKit.Body, fontSize = 14, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
        caps = new GUIStyle(hint) { fontSize = 13, fontStyle = FontStyle.Bold };
    }

    static void Write(Rect rect, string text, GUIStyle style, Color color)
    {
        style.normal.textColor = color;
        UiKit.Text(rect, text, style);
    }

    static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }

    /// <summary>Brass rim, a dark enamel face, four engraved notches and twelve minor ticks, made once.</summary>
    static Texture2D Dial()
    {
        if (dialTex != null) return dialTex;
        const int N = 256;
        dialTex = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "CompassDial", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
        var px = new Color[N * N];
        var brassHi = new Color(0.95f, 0.82f, 0.55f); var brass = new Color(0.72f, 0.56f, 0.30f); var brassLo = new Color(0.36f, 0.26f, 0.13f);
        var enamel = new Color(0.035f, 0.04f, 0.05f);
        float aa = 1.5f / (N * 0.5f);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v), ang = Mathf.Atan2(v, u);
                // light from the upper left falls across the rim
                float lit = 0.5f + 0.5f * Mathf.Cos(ang - 2.2f);
                Color rim = Color.Lerp(brassLo, Color.Lerp(brass, brassHi, lit), Smooth(0.86f, 0.93f, r) * (1f - Smooth(0.96f, 1f, r)) + 0.35f);
                Color groove = Color.Lerp(brassLo, brass, 0.25f + 0.3f * lit);
                Color face = Color.Lerp(enamel * 1.6f, enamel, Smooth(0f, 0.8f, r));
                Color c = r > 0.86f ? rim : r > 0.80f ? groove : face;
                float a = r < 0.80f ? 0.88f : 1f;
                // engraved marks on the face: four notches at the bearings, twelve fine ticks
                if (r > 0.58f && r < 0.79f)
                {
                    float deg = Mathf.Repeat(ang * Mathf.Rad2Deg, 360f);
                    float toCardinal = Mathf.Abs(Mathf.DeltaAngle(deg, Mathf.Round(deg / 90f) * 90f));
                    float toMinor = Mathf.Abs(Mathf.DeltaAngle(deg, Mathf.Round(deg / 30f) * 30f));
                    float halfWidthC = Mathf.Rad2Deg * (0.022f / Mathf.Max(r, 0.1f));
                    float halfWidthM = Mathf.Rad2Deg * (0.011f / Mathf.Max(r, 0.1f));
                    float notch = (1f - Smooth(halfWidthC * 0.6f, halfWidthC, toCardinal)) * Smooth(0.58f, 0.6f, r);
                    float tick = (1f - Smooth(halfWidthM * 0.5f, halfWidthM, toMinor)) * Smooth(0.68f, 0.7f, r);
                    c = Color.Lerp(c, Color.Lerp(brass, brassHi, lit), Mathf.Max(notch, tick * 0.8f));
                    a = Mathf.Max(a, Mathf.Max(notch, tick));
                }
                a *= 1f - Smooth(1f - aa * 2f, 1f, r);
                px[y * N + x] = new Color(c.r, c.g, c.b, a);
            }
        dialTex.SetPixels(px); dialTex.Apply(true, true);
        return dialTex;
    }

    /// <summary>A tapered brass needle pointing right, pivot at 22% of its length.</summary>
    static Texture2D Needle()
    {
        if (needleTex != null) return needleTex;
        const int W = 128, H = 24;
        needleTex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "CompassNeedle", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W, v = (y + 0.5f) / H * 2f - 1f;
                float half = u < 0.22f ? Mathf.Lerp(0.35f, 0.85f, u / 0.22f) : Mathf.Lerp(0.85f, 0.04f, (u - 0.22f) / 0.78f);
                float edge = Mathf.Abs(v);
                float a = 1f - Smooth(half - 0.12f, half + 0.02f, edge);
                Color c = Color.Lerp(new Color(0.98f, 0.88f, 0.62f), new Color(0.58f, 0.43f, 0.2f), Smooth(-0.3f, 0.9f, v));
                px[y * W + x] = new Color(c.r, c.g, c.b, a);
            }
        needleTex.SetPixels(px); needleTex.Apply(true, true);
        return needleTex;
    }

    /// <summary>A soft round falloff used behind text for legibility over bright scenery.</summary>
    static Texture2D Glow()
    {
        if (glowTex != null) return glowTex;
        const int N = 64;
        glowTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = "SoftBacking", wrapMode = TextureWrapMode.Clamp };
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                float a = 1f - Smooth(0.35f, 1f, Mathf.Max(Mathf.Abs(u) * 0.92f, Mathf.Abs(v)));
                px[y * N + x] = new Color(0f, 0f, 0f, a);
            }
        glowTex.SetPixels(px); glowTex.Apply(false, true);
        return glowTex;
    }

    static void Backing(Rect r, float alpha)
    {
        var old = GUI.color; GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.DrawTexture(new Rect(r.x - r.width * 0.12f, r.y - r.height * 0.45f, r.width * 1.24f, r.height * 1.9f), Glow(), ScaleMode.StretchToFill, true);
        GUI.color = old;
    }

    static void Check(Vector2 at, Color c)
    {
        var m = GUI.matrix;
        GUIUtility.RotateAroundPivot(45f, at);
        UiKit.Fill(new Rect(at.x - 1f, at.y - 7f, 2.4f, 10f), c);
        UiKit.Fill(new Rect(at.x - 4.6f, at.y + 0.6f, 4.6f, 2.4f), c);
        GUI.matrix = m;
    }

    static void Padlock(Vector2 at, Color c)
    {
        UiKit.Ring(new Rect(at.x - 4f, at.y - 9f, 8f, 9f), c, 1.6f);
        UiKit.Fill(new Rect(at.x - 5.5f, at.y - 3.5f, 11f, 8f), c);
    }

    Vector2 LabelPos(int i, Vector2 centre, float spread)
    {
        var d = Directions[i];
        return centre + new Vector2(d.x * spread, -d.y * 66f);
    }

    /// <summary>
    /// Draws the open dial. <paramref name="remaining"/> (0..1) draws the decision clock as brass marks that go out
    /// clockwise from twelve; leave it negative for questions that wait for the player.
    /// </summary>
    public void Draw(float w, float h, string speaker, float remaining = -1f)
    {
        if (labels == null) return;
        Styles();
        float appear = Smooth(0f, 0.18f, Time.unscaledTime - openedAt);
        var centre = new Vector2(w * 0.5f, h * 0.792f);
        float spread = Mathf.Min(176f, w * 0.2f);
        const float R = 34f;

        // the reply Arjun would give, above the dial and well below the witness's eyes
        string preview = selected >= 0 && replies != null && selected < replies.Length ? replies[selected] : "";
        if (selected >= 0 && !Available(selected) && !string.IsNullOrEmpty(lockedHint)) preview = lockedHint;
        float textWidth = Mathf.Min(w * 0.7f, 820f);
        float textH = string.IsNullOrEmpty(preview) ? 0f : detail.CalcHeight(new GUIContent(preview), textWidth);
        float topOfCluster = centre.y - 66f - 22f;
        var previewRect = new Rect((w - textWidth) / 2, topOfCluster - 14f - textH, textWidth, textH + 4f);
        var oldColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, appear);
        Backing(new Rect(w * 0.5f - Mathf.Max(textWidth, spread * 2f + 220f) / 2f, previewRect.y - 26f, Mathf.Max(textWidth, spread * 2f + 220f), centre.y + 96f - (previewRect.y - 26f)), 0.55f * appear);
        // only the timed decision carries a heading: over a witness it landed on their clothes, and the trust meter or
        // the scene already says who is being answered
        if (!string.IsNullOrEmpty(speaker) && remaining >= 0f)
            Write(new Rect(0, previewRect.y - 26f, w, 20f), UiKit.Track(speaker), caps, new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.85f * appear));
        if (textH > 0f)
            Write(previewRect, preview, detail, selected >= 0 && !Available(selected) ? UiKit.Muted : UiKit.Cream);

        // the dial
        var dial = new Rect(centre.x - R, centre.y - R, R * 2f, R * 2f);
        GUI.DrawTexture(dial, Dial(), ScaleMode.StretchToFill, true);
        if (remaining >= 0f)
        {
            // the decision clock: 24 brass marks round the rim go out clockwise from twelve
            int lit = Mathf.CeilToInt(24f * Mathf.Clamp01(remaining));
            bool urgent = remaining < 0.34f;
            float pulse = urgent ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) : 1f;
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI / 12f;
                var p = centre + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * (R + 7f);
                Color c = i < lit ? (urgent ? Color.Lerp(UiKit.Gold, UiKit.Danger, 0.55f) * pulse : UiKit.Gold) : new Color(1f, 1f, 1f, 0.12f);
                UiKit.Fill(new Rect(p.x - 1.6f, p.y - 1.6f, 3.2f, 3.2f), c);
            }
        }
        // where the mouse, trackpad or stick is pointing, and the needle on the bearing it picks
        if (pointer.sqrMagnitude > 0.0004f)
        {
            var p = centre + new Vector2(pointer.x, -pointer.y) * (R * 0.62f);
            UiKit.Fill(new Rect(p.x - 2f, p.y - 2f, 4f, 4f), new Color(UiKit.Cream.r, UiKit.Cream.g, UiKit.Cream.b, 0.55f));
        }
        if (selected >= 0)
        {
            Vector2 dir = Directions[selected];
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg, centre);
            float len = R * 1.05f;
            GUI.DrawTexture(new Rect(centre.x - len * 0.22f, centre.y - 5f, len, 10f), Needle(), ScaleMode.StretchToFill, true);
            GUI.matrix = m;
        }
        UiKit.Ring(new Rect(centre.x - 4f, centre.y - 4f, 8f, 8f), UiKit.Gold, 4f);

        // the bearings
        for (int i = 0; i < labels.Length; i++)
        {
            var p = LabelPos(i, centre, spread);
            bool avail = Available(i), seen = Asked(i), focus = i == selected;
            string text = labels[i];
            float tw = label.CalcSize(new GUIContent(text)).x;
            Color c = !avail ? new Color(0.5f, 0.48f, 0.45f, 0.9f) : seen ? new Color(0.72f, 0.70f, 0.66f, 0.95f) : focus ? UiKit.Gold : UiKit.Cream;
            if (focus && avail) UiKit.Fill(new Rect(p.x - tw / 2f - 10f, p.y + 13f, tw + 20f, 1.5f), new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.9f));
            Write(new Rect(p.x - 140f, p.y - 13f, 280f, 26f), text, label, c);
            // the shortcut number, small, ahead of the label
            Write(new Rect(p.x - tw / 2f - 30f, p.y - 10f, 18f, 20f), (i + 1).ToString(), hint, new Color(c.r, c.g, c.b, c.a * 0.7f));
            // asked: a tick after the label; gated: a padlock (and the reason when focused)
            if (!avail && !seen) Padlock(new Vector2(p.x + tw / 2f + 14f, p.y + 2f), c);
            else if (seen) Check(new Vector2(p.x + tw / 2f + 14f, p.y), c);
        }
        GUI.color = oldColor;
        Write(new Rect(0, h - 34f, w, 26f),
            "Point with mouse, trackpad, arrows or stick   ·   Click / E / A to commit   ·   1–" + labels.Length + " to answer directly", hint,
            new Color(UiKit.Cream.r, UiKit.Cream.g, UiKit.Cream.b, 0.62f));
    }

    /// <summary>For half a second after a commitment the chosen bearing stays lit in brass while the dial clears.</summary>
    public void Afterglow(float w, float h)
    {
        float t = Time.unscaledTime - committedAt;
        if (committed < 0 || t > AfterglowTime || labels != null || GameState.Paused) return;
        Styles();
        float k = 1f - Smooth(0.12f, AfterglowTime, t);
        var centre = new Vector2(w * 0.5f, h * 0.792f);
        var p = centre + new Vector2(committedFrom.x * Mathf.Min(176f, w * 0.2f), -committedFrom.y * 66f);
        var old = GUI.color; GUI.color = new Color(1f, 1f, 1f, k);
        GUI.DrawTexture(new Rect(centre.x - 34f, centre.y - 34f, 68f, 68f), Dial(), ScaleMode.StretchToFill, true);
        var m = GUI.matrix;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(-committedFrom.y, committedFrom.x) * Mathf.Rad2Deg, centre);
        GUI.DrawTexture(new Rect(centre.x - 34f * 1.05f * 0.22f, centre.y - 5f, 34f * 1.05f, 10f), Needle(), ScaleMode.StretchToFill, true);
        GUI.matrix = m;
        GUI.color = old;
        float grow = 1f + 0.08f * Smooth(0f, 0.15f, t);
        label.fontSize = Mathf.RoundToInt(18 * grow);
        Write(new Rect(p.x - 160f, p.y - 14f, 320f, 28f), committedLabel, label, new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, k));
        label.fontSize = 18;
    }

    // ================================================================== subtitles
    /// <summary>
    /// A voiced line as subtitles: split into readable phrases (sentences, long ones at a comma or a dash) and shown
    /// one at a time in step with the voice. With no audio the whole line shows.
    /// </summary>
    public void Speak(float w, float h, string speaker, string text, AudioSource source)
    {
        if (string.IsNullOrEmpty(text)) return;
        Styles();
        var clip = source != null ? source.clip : null;
        if (text != lastText || clip != lastClip)
        {
            lastText = text; lastClip = clip; phrase = 0;
            Split(text, clip);
        }
        string show = text;
        if (clip != null && phrases.Count > 1 && source != null)
        {
            // AudioSource.time returns to zero when playback ends: hold the last phrase reached
            float at = source.isPlaying ? Mathf.Clamp01(source.time / Mathf.Max(0.01f, clip.length)) : (phrase > 0 ? 1f : 0f);
            int i = 0;
            while (i < phraseEnds.Count - 1 && at > phraseEnds[i]) i++;
            phrase = Mathf.Max(phrase, i);
            show = phrases[phrase];
        }
        float width = Mathf.Min(w * 0.72f, 900f);
        float height = detail.CalcHeight(new GUIContent(show), width) + 4f;
        var r = new Rect((w - width) / 2, h * 0.915f - 22f - height, width, height);
        Backing(new Rect(r.x, r.y - 24f, r.width, r.height + 24f), 0.6f);
        if (!string.IsNullOrEmpty(speaker)) Write(new Rect(r.x, r.y - 24f, width, 20f), UiKit.Track(speaker), caps, UiKit.Gold);
        Write(r, show, detail, UiKit.Cream);
    }

    void Split(string text, AudioClip clip)
    {
        phrases.Clear(); phraseEnds.Clear();
        const int Max = 96;
        var parts = new List<string>();
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            bool end = (ch == '.' || ch == '?' || ch == '!') && (i + 1 == text.Length || text[i + 1] == ' ' || text[i + 1] == '"');
            if (end || i == text.Length - 1)
            {
                int stop = i + 1;
                while (stop < text.Length && (text[stop] == '"' || text[stop] == '.')) stop++;
                parts.Add(text.Substring(start, stop - start).Trim());
                start = stop; i = stop - 1;
            }
        }
        if (start < text.Length && text.Substring(start).Trim().Length > 0) parts.Add(text.Substring(start).Trim());
        // join short sentences, break long ones at a comma, a dash or a space
        var cur = "";
        foreach (var p in parts)
        {
            var piece = p;
            while (piece.Length > Max)
            {
                int cut = piece.LastIndexOfAny(new[] { ',', ';', '-' }, Max - 1);
                if (cut < Max / 3) cut = piece.LastIndexOf(' ', Max - 1);
                if (cut <= 0) cut = Max - 1;
                if (cur.Length > 0) { phrases.Add(cur); cur = ""; }
                phrases.Add(piece.Substring(0, cut + 1).Trim());
                piece = piece.Substring(cut + 1).Trim();
            }
            if (cur.Length > 0 && cur.Length + 1 + piece.Length > Max) { phrases.Add(cur); cur = ""; }
            cur = cur.Length == 0 ? piece : cur + " " + piece;
        }
        if (cur.Length > 0) phrases.Add(cur);
        if (phrases.Count == 0) phrases.Add(text);
        // a recorded line: each phrase ends where its last word ends in the recording
        if (TimedEnds(text, clip)) return;
        // otherwise each phrase gets the share of the line's length its characters take
        float total = 0; foreach (var p in phrases) total += p.Length + 6f;
        float acc = 0;
        foreach (var p in phrases) { acc += p.Length + 6f; phraseEnds.Add(acc / total); }
    }

    /// <summary>Phrase ends from the measured word timing of a recorded clip (VoTiming, made by
    /// Tools/arjun_recordings.py). False, with nothing set, when the clip has none or its subtitle has changed.</summary>
    bool TimedEnds(string text, AudioClip clip)
    {
        var timing = clip != null ? VoTiming.Get(clip.name) : null;
        if (timing == null || timing.text != text || timing.chars.Length == 0 || clip.length <= 0f) return false;
        int from = 0, k = 0;
        foreach (var p in phrases)
        {
            string tail = p.Length > 12 ? p.Substring(p.Length - 12) : p;
            int at = text.IndexOf(tail, from, System.StringComparison.Ordinal);
            if (at < 0) { phraseEnds.Clear(); return false; }
            from = at + tail.Length;
            while (k + 1 < timing.chars.Length && timing.chars[k + 1] <= from) k++;     // the phrase's last word
            phraseEnds.Add(Mathf.Clamp01(timing.times[k] / clip.length));
        }
        return true;
    }
}
