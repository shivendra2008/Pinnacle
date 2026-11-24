using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The case file laid open on a surface: physical evidence put in front of a witness one piece at a time. It is the
/// compass's partner, not a second compass: the compass carries conversational intentions (four bearings), the folio
/// carries things Arjun can show, as many as he has (the Reddy interview can lay out five or six).
///
/// Two separate acts, never one input doing both:
///  - INSPECT: left / right (arrows, A / D, d-pad, either stick, the Arduino's right stick, sideways mouse or
///    trackpad travel, the scroll wheel) or a number key moves the focus. The focused piece lifts off the surface and
///    turns to the camera, and what it shows is written under the row.
///  - PRESENT: a separate press (E, Enter, Space, click, the pad's south button, the Arduino's D4 or right button)
///    commits the focused piece. A press in the same moment the focus moved (0.2 s), or in the first 0.25 s after the
///    folio opens, does nothing, so the key that skipped the line before cannot present anything.
/// BACK (Backspace, right click, the pad's east button, the Arduino's left button) puts the focused piece down again.
/// Locked pieces (already accepted) can be focused and read but not presented. Paused, nothing is read or drawn.
/// The story owns what each key means: the folio only reports the index committed.
/// </summary>
public sealed class EvidenceFolio
{
    public sealed class Item
    {
        public string key;          // the story's id for the piece ("id", "signout", ...)
        public string name;         // short label on the row
        public string shows;        // what it shows, in Arjun's words, under the row when focused
        public Transform obj;       // the thing on the surface (optional)
        public bool locked;         // on the table already: readable, not presentable
        public string lockedNote;   // why it cannot be presented now
        // the pose it was laid down in
        public Vector3 basePos; public Quaternion baseRot; public bool posed;
    }

    const float ArmDelay = 0.25f;
    const float FocusSettle = 0.2f;        // a commit needs the focus to have rested this long (inspect, then present)
    const float MouseStep = 64f;           // points of sideways mouse / trackpad travel per step
    const float Lift = 0.035f;

    readonly List<Item> items = new List<Item>();
    bool open;
    int focus = -1, openedFrame, committed = -1;
    float openedAt, focusAt, mouseAcc, hintAt = -10f, committedAt = -10f;
    bool stickHeld, ardHeld;
    string heading, claim;
    Transform viewer;

    public AudioSource sfx;
    static AudioClip slideClip, liftClip, putClip;
    GUIStyle sName, sShows, sHint, sCaps, sClaim;
    static Texture2D backTex;

    public bool IsOpen => open;
    public int Focused => open ? focus : -1;
    public int LastCommitted => committed;
    public float OpenedAt => openedAt;
    public IList<Item> Items => items;
    public string Heading => heading;
    public string Claim => claim;

    /// <summary>Opens the folio over the pieces (the list is shared with the story: pieces keep their state between rounds).</summary>
    public void Open(IList<Item> pieces, string heading, string claim, Transform viewer)
    {
        items.Clear(); items.AddRange(pieces);
        foreach (var it in items)
            if (it.obj != null && !it.posed) { it.basePos = it.obj.position; it.baseRot = it.obj.rotation; it.posed = true; }
        this.heading = heading; this.claim = claim; this.viewer = viewer;
        open = true; focus = -1; committed = -1;
        openedFrame = Time.frameCount; openedAt = Time.unscaledTime; focusAt = -10f; mouseAcc = 0f;
        stickHeld = true; ardHeld = true;        // a stick already pushed when the folio opens must come back first
    }

    /// <summary>Re-records where a piece rests (after the story has moved it).</summary>
    public static void Repose(Item it)
    {
        if (it == null || it.obj == null) return;
        it.basePos = it.obj.position; it.baseRot = it.obj.rotation; it.posed = true;
    }

    public void Close() { open = false; focus = -1; }

    bool Available(int i) => i >= 0 && i < items.Count && !items[i].locked;

    void SetFocus(int i)
    {
        if (i == focus) return;
        focus = i; focusAt = Time.unscaledTime;
        if (sfx != null && i >= 0) sfx.PlayOneShot(Lifted(), 0.45f);
        if (sfx != null && i < 0) sfx.PlayOneShot(Put(), 0.4f);
    }

    void Step(int dir)
    {
        if (items.Count == 0) return;
        if (focus < 0) { SetFocus(dir > 0 ? 0 : items.Count - 1); return; }
        SetFocus(Mathf.Clamp(focus + dir, 0, items.Count - 1));
    }

    /// <summary>Reads input once per frame; returns the presented index, or -1.</summary>
    public int Poll()
    {
        if (!open || GameState.Paused || Time.frameCount == openedFrame) return -1;
        var kb = Keyboard.current; var pad = Gamepad.current; var mouse = Mouse.current;
        int step = 0;
        if (kb != null)
        {
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) step = -1;
            else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) step = 1;
        }
        if (pad != null)
        {
            if (pad.dpad.left.wasPressedThisFrame) step = -1;
            else if (pad.dpad.right.wasPressedThisFrame) step = 1;
            float sx = pad.leftStick.ReadValue().x; if (Mathf.Abs(pad.rightStick.ReadValue().x) > Mathf.Abs(sx)) sx = pad.rightStick.ReadValue().x;
            if (!stickHeld && Mathf.Abs(sx) > 0.6f) { step = sx > 0 ? 1 : -1; stickHeld = true; }
            else if (Mathf.Abs(sx) < 0.3f) stickHeld = false;
        }
        else stickHeld = false;
        float ax = GameInput.Look.x;                          // the Arduino's right stick
        if (!ardHeld && Mathf.Abs(ax) > 0.6f) { step = ax > 0 ? 1 : -1; ardHeld = true; }
        else if (Mathf.Abs(ax) < 0.3f) ardHeld = false;
        if (mouse != null)
        {
            mouseAcc += mouse.delta.ReadValue().x;
            if (Mathf.Abs(mouseAcc) >= MouseStep) { step = mouseAcc > 0 ? 1 : -1; mouseAcc = 0f; }
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.5f) step = wheel > 0 ? -1 : 1;
        }
        if (step != 0) Step(step);
        if (kb != null)
        {
            for (int i = 0; i < Mathf.Min(items.Count, 9); i++)
            {
                var k = kb[Key.Digit1 + i];
                if (k.wasPressedThisFrame) SetFocus(i);          // a number focuses (inspects); it never presents
            }
        }
        if (Time.unscaledTime - openedAt < ArmDelay) return -1;
        bool back = (kb != null && kb.backspaceKey.wasPressedThisFrame) || (mouse != null && mouse.rightButton.wasPressedThisFrame)
                    || (pad != null && pad.buttonEast.wasPressedThisFrame) || (!GameState.Paused && GameInput.JumpPressed);
        if (back && focus >= 0) { SetFocus(-1); return -1; }
        bool confirm = GameInput.InteractPressed || GameInput.OptionPressed || (mouse != null && mouse.leftButton.wasPressedThisFrame)
                       || (pad != null && pad.buttonSouth.wasPressedThisFrame)
                       || (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame));
        if (!confirm) return -1;
        if (focus < 0) { hintAt = Time.unscaledTime; return -1; }                 // nothing picked up yet: say so
        if (Time.unscaledTime - focusAt < FocusSettle) return -1;                 // the focus moved this moment: inspect first
        if (!Available(focus)) { hintAt = Time.unscaledTime; return -1; }
        committed = focus; committedAt = Time.unscaledTime;
        if (sfx != null) sfx.PlayOneShot(Slide(), 0.55f);
        open = false;
        return committed;
    }

    /// <summary>Moves the pieces: the focused one lifts and turns to the viewer, the others rest. Call every frame.</summary>
    public void Animate(float dt)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it.obj == null || !it.posed) continue;
            bool up = open && i == focus;
            Vector3 targetP = it.basePos + (up ? Vector3.up * Lift : Vector3.zero);
            Quaternion targetR = it.baseRot;
            if (up && viewer != null)
            {
                Vector3 to = viewer.position - it.basePos; to.y = 0f;
                if (to.sqrMagnitude > 1e-4f)
                {
                    // tip it up a little towards the eyes, as a hand would hold a paper to be read
                    Vector3 axis = Vector3.Cross(Vector3.up, to.normalized);
                    targetR = Quaternion.AngleAxis(-14f, axis) * it.baseRot;
                }
            }
            float k = 1f - Mathf.Exp(-14f * dt);
            it.obj.position = Vector3.Lerp(it.obj.position, targetP, k);
            it.obj.rotation = Quaternion.Slerp(it.obj.rotation, targetR, k);
        }
    }

    // ================================================================== drawing
    void Styles()
    {
        if (sName != null) return;
        sName = new GUIStyle(GUI.skin.label) { font = UiKit.Body, fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
        sShows = new GUIStyle(GUI.skin.label) { font = UiKit.Body, fontSize = 21, alignment = TextAnchor.UpperCenter, wordWrap = true };
        sClaim = new GUIStyle(sShows) { fontSize = 19, fontStyle = FontStyle.Italic };
        sHint = new GUIStyle(GUI.skin.label) { font = UiKit.Body, fontSize = 15, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
        sCaps = new GUIStyle(sHint) { fontSize = 14, fontStyle = FontStyle.Bold };
    }

    static void Write(Rect r, string t, GUIStyle s, Color c) { s.normal.textColor = c; UiKit.Text(r, t, s); }

    static Texture2D Back()
    {
        if (backTex != null) return backTex;
        const int N = 64;
        backTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "FolioBacking" };
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                float d = Mathf.Max(Mathf.Abs(u) * 0.9f, Mathf.Abs(v));
                float a = Mathf.Clamp01((1f - d) / 0.65f); a = a * a * (3f - 2f * a);
                px[y * N + x] = new Color(0f, 0f, 0f, a);
            }
        backTex.SetPixels(px); backTex.Apply(false, true);
        return backTex;
    }

    /// <summary>Draws the row in the story scripts' GUI space (the screen scaled to 900 units high).</summary>
    public void Draw(float w, float h)
    {
        if (!open) return;
        Styles();
        float appear = Mathf.Clamp01((Time.unscaledTime - openedAt) / 0.2f);
        var old = GUI.color;
        float rowY = h * 0.835f;            // under the counter's pieces (they sit near 0.7 of the frame), above the bottom bar
        float gap = 18f;
        // the labels' widths, then the row centred
        var widths = new float[items.Count];
        float total = 0f;
        for (int i = 0; i < items.Count; i++) { widths[i] = sName.CalcSize(new GUIContent(items[i].name)).x + 30f; total += widths[i] + (i > 0 ? gap : 0f); }
        float scale = total > w * 0.92f ? (w * 0.92f) / total : 1f;
        float x = w / 2f - total * scale / 2f;
        string shows = focus >= 0 && focus < items.Count ? (items[focus].locked && !string.IsNullOrEmpty(items[focus].lockedNote) ? items[focus].lockedNote : items[focus].shows) : "";
        float textW = Mathf.Min(w * 0.64f, 860f);
        float showsH = string.IsNullOrEmpty(shows) ? 22f : sShows.CalcHeight(new GUIContent(shows), textW);
        float claimH = string.IsNullOrEmpty(claim) ? 0f : sClaim.CalcHeight(new GUIContent(claim), textW);
        float top = rowY - 34f - claimH;
        GUI.color = new Color(1f, 1f, 1f, 0.66f * appear);
        GUI.DrawTexture(new Rect(w / 2f - Mathf.Max(total * scale, textW) * 0.62f, top - 24f, Mathf.Max(total * scale, textW) * 1.24f, rowY + 40f + showsH + 30f - (top - 24f)), Back(), ScaleMode.StretchToFill, true);
        GUI.color = new Color(1f, 1f, 1f, appear);
        // the witness and what she has just claimed, on one line: the question is not read out again
        if (claimH > 0f) Write(new Rect(w / 2f - textW / 2f, rowY - 30f - claimH, textW, claimH + 2f), (string.IsNullOrEmpty(heading) ? "" : heading + ":  ") + claim, sClaim, new Color(UiKit.Cream.r, UiKit.Cream.g, UiKit.Cream.b, 0.85f));
        else Write(new Rect(0, rowY - 46f, w, 20f), UiKit.Track(heading ?? "Present evidence"), sCaps, new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.9f));
        for (int i = 0; i < items.Count; i++)
        {
            float bw = widths[i] * scale;
            var r = new Rect(x, rowY - 17f, bw, 34f);
            bool f = i == focus, lk = items[i].locked;
            UiKit.Fill(r, new Color(0f, 0f, 0f, f ? 0.55f : 0.32f));
            if (f) UiKit.Fill(new Rect(r.x, r.yMax, r.width, 2f), lk ? UiKit.Muted : UiKit.Gold);
            Color c = lk ? new Color(0.6f, 0.58f, 0.55f, 0.95f) : f ? UiKit.Gold : UiKit.Cream;
            Write(r, items[i].name, sName, c);
            Write(new Rect(r.x - 4f, r.y - 15f, 24f, 16f), (i + 1).ToString(), sHint, new Color(c.r, c.g, c.b, 0.6f));
            if (lk) Write(new Rect(r.x, r.yMax + 3f, r.width, 16f), "on the table", sHint, UiKit.Muted);
            x += bw + gap * scale;
        }
        if (!string.IsNullOrEmpty(shows))
            Write(new Rect(w / 2f - textW / 2f, rowY + 30f, textW, showsH + 4f), shows, sShows, focus >= 0 && items[focus].locked ? UiKit.Muted : UiKit.Cream);
        else
        {
            bool nudge = Time.unscaledTime - hintAt < 1.6f;
            Write(new Rect(w / 2f - textW / 2f, rowY + 30f, textW, 24f), nudge ? "Pick something up first: left or right" : "Choose what to put in front of her",
                  sShows, nudge ? UiKit.Gold : new Color(UiKit.Cream.r, UiKit.Cream.g, UiKit.Cream.b, 0.7f));
        }
        GUI.color = old;
        Write(new Rect(0, h - 32f, w, 24f),
            "Left / right, stick or mouse to pick up   ·   E / Enter / click / A to present   ·   Backspace / right click / B to put it down", sHint,
            new Color(UiKit.Cream.r, UiKit.Cream.g, UiKit.Cream.b, 0.66f));
    }

    // ================================================================== sounds (paper and card on wood)
    static AudioClip Noise(string name, float len, float lp, float decay, float body, int seed)
    {
        const int SR = 44100; int n = (int)(SR * len);
        var d = new float[n]; var r = new System.Random(seed); float a = 0f, b = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR, wv = (float)(r.NextDouble() * 2 - 1);
            a += (wv - a) * lp; b += (a - b) * 0.5f;
            float env = Mathf.Clamp01(t / 0.008f) * Mathf.Exp(-t * decay);
            d[i] = (b * 2.2f + Mathf.Sin(2f * Mathf.PI * body * t) * 0.25f * Mathf.Exp(-t * 40f)) * env;
        }
        float m = 0.0001f; foreach (var v in d) m = Mathf.Max(m, Mathf.Abs(v));
        for (int i = 0; i < n; i++) d[i] *= 0.6f / m;
        var c = AudioClip.Create(name, n, 1, SR, false); c.SetData(d, 0); return c;
    }
    public static AudioClip Slide() { return slideClip != null ? slideClip : (slideClip = Noise("FolioSlide", 0.42f, 0.18f, 7f, 120f, 31)); }
    static AudioClip Lifted() { return liftClip != null ? liftClip : (liftClip = Noise("FolioLift", 0.12f, 0.35f, 30f, 900f, 32)); }
    public static AudioClip Put() { return putClip != null ? putClip : (putClip = Noise("FolioPut", 0.16f, 0.25f, 26f, 180f, 33)); }
}
