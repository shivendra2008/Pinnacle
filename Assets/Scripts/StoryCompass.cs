using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The investigation's decision compass, used at every reachable decision in Acts I-III (and the café in Act IV), plus
/// the subtitles the conversations speak through. A brass dial sits below the witness's eyeline so the face stays clear;
/// short intentions sit at its four bearings (index 0 left, 1 right, 2 up, 3 down: "leave" is always down). Its needle
/// rests on the empty lower-left diagonal when the dial opens (pointing at no answer) and swings, on a damped spring,
/// wherever relative mouse or trackpad movement, the arrow keys, a stick, the d-pad or the Arduino's right stick
/// point; the bearing it lands on is the candidate, lit in brass, and the full line Arjun will say is shown under
/// the dial. Click, E, Enter, Space, the pad's south button or the Arduino's D4 button commits the candidate; 1-4
/// commit directly. Story coroutines own the choice indices and their consequences: this class only reports which
/// index was committed, so every reply keeps its original index, line and result.
///
/// Held keys: only presses count (wasPressedThisFrame), never a key that is down, and no press counts in the first
/// 0.22 s after the dial opens, so the key that skipped the line before it cannot answer it.
/// </summary>
public sealed class StoryCompass
{
    static readonly Vector2[] Directions = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
    const float ArmDelay = 0.22f;          // presses this soon after the dial opens are left over from skipping a line
    const float AfterglowTime = 0.65f;
    const float MouseUnit = 36f;           // points of mouse or trackpad travel that swing the pointer across the dial's radius
    const float RestDegrees = 225f;        // where the needle rests: the empty lower-left diagonal (degrees, 0 = right, 90 = up)

    // layout, in the story scripts' GUI space (the screen scaled so its height is 900 units)
    const float Radius = 46f;              // the dial
    const float CentreY = 0.705f;          // the dial's centre, as a fraction of the height (the witness's eyes sit near 0.5)
    const float BearingGap = 28f;          // from the rim to the up / down labels

    string[] labels, replies;
    bool[] locked, asked;
    string lockedHint;
    int selected = -1, opened;
    float openedAt;
    Vector2 pointer;

    // the needle: an angle on a damped spring, so it swings and settles like a real one
    float needle = RestDegrees, needleVel; int needleFrame = -1;

    // the last commitment, drawn briefly after the story has moved on so the decision reads as taken
    int committed = -1; float committedAt = -10f; string committedLabel;

    /// <summary>Plays the soft tick when the candidate changes (optional; the story plays its own click on commit).</summary>
    public AudioSource sfx;
    static AudioClip tickClip;

    // subtitles: phrases of a voiced line, shown in step with its audio
    readonly List<string> phrases = new List<string>();
    readonly List<float> phraseEnds = new List<float>();
    string lastText; AudioClip lastClip; int phrase;

    GUIStyle label, detail, hint, caps, context;
    static Texture2D dialTex, needleTex, glowTex;

    public bool IsOpen => labels != null;
    /// <summary>The bearing the needle has picked (-1: none yet), for the play-test routes.</summary>
    public int Focused => labels != null ? selected : -1;
    /// <summary>The needle's angle in degrees (0 = right, 90 = up), for the play-test routes.</summary>
    public float NeedleDegrees => needle;
    public float OpenedAt => openedAt;
    public int LastCommitted => committed;

    public void Open(string[] labels, string[] replies, bool[] locked = null, bool[] asked = null, string lockedHint = null)
    {
        bool wasOpen = this.labels != null;
        this.labels = labels;
        this.replies = replies;
        this.locked = locked;
        this.asked = asked;
        this.lockedHint = lockedHint;
        selected = -1;
        pointer = Vector2.zero;
        opened = Time.frameCount;
        openedAt = Time.unscaledTime;
        if (!wasOpen && Time.unscaledTime - committedAt > AfterglowTime) { needle = RestDegrees; needleVel = 0f; }
    }

    /// <summary>Closes the dial (it also closes itself when a bearing is committed).</summary>
    public void Close() { labels = null; }

    bool Available(int index) => labels != null && index >= 0 && index < labels.Length && (locked == null || index >= locked.Length || !locked[index]);
    bool Asked(int index) => asked != null && index >= 0 && index < asked.Length && asked[index];
    static float DirDegrees(int i) => Mathf.Atan2(Directions[i].y, Directions[i].x) * Mathf.Rad2Deg;

    void Select(int index)
    {
        if (index == selected) return;
        selected = index;
        if (index >= 0 && sfx != null) sfx.PlayOneShot(Tick(), 0.32f);
    }

    /// <summary>The bearing a direction points at: the nearest one within 67 degrees, or none.</summary>
    void Aim(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.18f) return;
        float best = 0.38f; int pick = -1;
        for (int i = 0; i < labels.Length; i++)
        {
            float score = Vector2.Dot(direction.normalized, Directions[i]);
            if (score > best) { best = score; pick = i; }
        }
        Select(pick);
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
                pointer = Vector2.ClampMagnitude(pointer + delta / MouseUnit, 1f);
                Aim(pointer);
            }
        }
        if (pad != null)
        {
            var stick = pad.rightStick.ReadValue();
            if (stick.sqrMagnitude < 0.18f) stick = pad.leftStick.ReadValue();
            if (stick.sqrMagnitude >= 0.18f) { pointer = Vector2.ClampMagnitude(stick, 1f); Aim(stick); }
            var dp = pad.dpad.ReadValue();
            if (dp.sqrMagnitude >= 0.18f) { pointer = dp.normalized * 0.8f; Aim(dp); }
        }
        var look = GameInput.Look;                        // the Arduino's right stick
        if (look.sqrMagnitude >= 0.18f) { pointer = Vector2.ClampMagnitude(look, 1f); Aim(look); }
        if (kb != null)
        {
            Vector2 arrow = kb.leftArrowKey.wasPressedThisFrame ? Vector2.left : kb.rightArrowKey.wasPressedThisFrame ? Vector2.right
                          : kb.upArrowKey.wasPressedThisFrame ? Vector2.up : kb.downArrowKey.wasPressedThisFrame ? Vector2.down : Vector2.zero;
            if (arrow != Vector2.zero) { pointer = arrow * 0.8f; Aim(arrow); }
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
                if (quick < labels.Length) { Select(quick); pointer = Directions[quick] * 0.8f; }     // a locked number only focuses it
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
        selected = index;
        labels = null;
        return index;
    }

    // ================================================================== the needle
    void StepNeedle()
    {
        if (needleFrame == Time.frameCount) return;
        needleFrame = Time.frameCount;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        float now = Time.unscaledTime;
        float target;
        if (labels == null && committed >= 0 && now - committedAt <= AfterglowTime) target = DirDegrees(committed);
        else if (pointer.sqrMagnitude > 0.06f) target = Mathf.Atan2(pointer.y, pointer.x) * Mathf.Rad2Deg;
        else if (selected >= 0) target = DirDegrees(selected);
        else target = RestDegrees + 2.5f * Mathf.Sin(now * 1.6f);       // at rest it hangs and sways a little
        // a slightly under-damped spring (natural frequency 16 rad/s, damping 0.75): it swings, overshoots a touch, settles
        float err = Mathf.DeltaAngle(needle, target);
        needleVel += (260f * err - 24f * needleVel) * dt;
        needle = Mathf.Repeat(needle + needleVel * dt, 360f);
    }

    // ================================================================== drawing
    void Styles()
    {
        if (label != null) return;
        label = new GUIStyle(GUI.skin.label) { font = UiKit.Body, fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
        detail = new GUIStyle(GUI.skin.label) { font = UiKit.Body, fontSize = 21, alignment = TextAnchor.UpperCenter, wordWrap = true };
        context = new GUIStyle(detail) { fontSize = 20 };
        hint = new GUIStyle(GUI.skin.label) { font = UiKit.Body, fontSize = 15, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
        caps = new GUIStyle(hint) { fontSize = 14, fontStyle = FontStyle.Bold };
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
                float a = r < 0.80f ? 0.9f : 1f;
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

    /// <summary>A tapered brass needle pointing right with a short tail, pivot at 26% of its length.</summary>
    static Texture2D Needle()
    {
        if (needleTex != null) return needleTex;
        const int W = 160, H = 28;
        needleTex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "CompassNeedle", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W, v = (y + 0.5f) / H * 2f - 1f;
                float half = u < 0.26f ? Mathf.Lerp(0.3f, 0.82f, u / 0.26f) : Mathf.Lerp(0.82f, 0.03f, (u - 0.26f) / 0.74f);
                float edge = Mathf.Abs(v);
                float a = 1f - Smooth(half - 0.12f, half + 0.02f, edge);
                // a bright edge on the upper flank, a darker lower flank: it reads as a bevelled blade at any angle
                Color c = Color.Lerp(new Color(1f, 0.9f, 0.64f), new Color(0.55f, 0.4f, 0.18f), Smooth(-0.3f, 0.9f, v));
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

    /// <summary>A soft wooden-instrument tick, made once (22 ms: a damped 2.4 kHz ping over a click).</summary>
    static AudioClip Tick()
    {
        if (tickClip != null) return tickClip;
        const int SR = 44100; int n = (int)(SR * 0.022f);
        var d = new float[n];
        var rnd = new System.Random(7);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float env = Mathf.Exp(-t * 260f);
            d[i] = env * (0.55f * Mathf.Sin(2f * Mathf.PI * 2400f * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * 3900f * t))
                 + (i < 40 ? ((float)rnd.NextDouble() * 2f - 1f) * 0.35f * (1f - i / 40f) : 0f);
        }
        tickClip = AudioClip.Create("CompassTick", n, 1, SR, false);
        tickClip.SetData(d, 0);
        return tickClip;
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
        UiKit.Fill(new Rect(at.x - 1f, at.y - 8f, 2.6f, 11f), c);
        UiKit.Fill(new Rect(at.x - 5f, at.y + 0.6f, 5f, 2.6f), c);
        GUI.matrix = m;
    }

    static void Padlock(Vector2 at, Color c)
    {
        UiKit.Ring(new Rect(at.x - 4.5f, at.y - 10f, 9f, 10f), c, 1.8f);
        UiKit.Fill(new Rect(at.x - 6f, at.y - 4f, 12f, 9f), c);
    }

    /// <summary>The safe area in GUI units (the story scripts scale the GUI so the height is <paramref name="h"/>).</summary>
    static Rect Safe(float w, float h)
    {
        var sa = Screen.safeArea;
        float s = Screen.height > 0 ? Screen.height / h : 1f;
        if (sa.width <= 0f || sa.height <= 0f) return new Rect(0, 0, w, h);
        return new Rect(sa.x / s, (Screen.height - sa.yMax) / s, sa.width / s, sa.height / s);
    }

    Vector2 Centre(float w, float h)
    {
        var safe = Safe(w, h);
        return new Vector2(safe.center.x, Mathf.Min(h * CentreY, safe.yMax - 190f));
    }

    static float Spread(float w) => Mathf.Min(214f, w * 0.17f);

    Vector2 LabelPos(int i, Vector2 centre, float spread)
    {
        var d = Directions[i];
        return d.y == 0f ? centre + new Vector2(d.x * spread, 0f) : centre + new Vector2(0f, -d.y * (Radius + BearingGap));
    }

    void DrawNeedle(Vector2 centre, float alpha, bool active)
    {
        var old = GUI.color;
        // at rest it is a little duller than when it points at something, but plainly there
        GUI.color = new Color(active ? 1f : 0.9f, active ? 1f : 0.86f, active ? 1f : 0.8f, alpha * (active ? 1f : 0.92f));
        var m = GUI.matrix;
        GUIUtility.RotateAroundPivot(-needle, centre);
        float len = Radius * 1.22f;
        // a dark edge under the blade so it reads over the enamel and over bright scenery alike
        var o2 = GUI.color; GUI.color = new Color(0f, 0f, 0f, 0.55f * alpha);
        GUI.DrawTexture(new Rect(centre.x - len * 0.26f + 1.2f, centre.y - 8.5f + 1.6f, len, 17f), Needle(), ScaleMode.StretchToFill, true);
        GUI.color = o2;
        GUI.DrawTexture(new Rect(centre.x - len * 0.26f, centre.y - 8.5f, len, 17f), Needle(), ScaleMode.StretchToFill, true);
        GUI.matrix = m;
        GUI.color = old;
        UiKit.Ring(new Rect(centre.x - 5f, centre.y - 5f, 10f, 10f), new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, alpha), 5f);
    }

    /// <summary>
    /// Draws the open dial. <paramref name="remaining"/> (0..1) draws the decision clock as brass marks that go out
    /// clockwise from twelve; leave it negative for questions that wait for the player. <paramref name="heading"/> and
    /// <paramref name="situation"/> (optional) state the decision above the dial when no witness's face is in frame.
    /// </summary>
    public void Draw(float w, float h, string speaker, float remaining = -1f, string situation = null)
    {
        if (labels == null) return;
        Styles();
        StepNeedle();
        float appear = Smooth(0f, 0.2f, Time.unscaledTime - openedAt);
        var centre = Centre(w, h);
        float spread = Spread(w);
        var safe = Safe(w, h);

        // the reply Arjun would give, under the dial, where subtitles sit
        string preview = selected >= 0 && replies != null && selected < replies.Length ? replies[selected] : "";
        if (selected >= 0 && !Available(selected) && !string.IsNullOrEmpty(lockedHint)) preview = lockedHint;
        float textWidth = Mathf.Min(w * 0.62f, 860f);
        float bottomOfCluster = centre.y + (labels.Length > 3 ? Radius + BearingGap + 16f : Radius + 10f);
        float textH = string.IsNullOrEmpty(preview) ? 0f : detail.CalcHeight(new GUIContent(preview), textWidth);
        var previewRect = new Rect(safe.center.x - textWidth / 2f, bottomOfCluster + 14f, textWidth, textH + 4f);
        float topOfCluster = centre.y - (labels.Length > 2 ? Radius + BearingGap + 16f : Radius + 6f);
        float sitH = string.IsNullOrEmpty(situation) ? 0f : context.CalcHeight(new GUIContent(situation), textWidth);
        float headY = topOfCluster - 12f - sitH - (string.IsNullOrEmpty(speaker) || (remaining < 0f && sitH <= 0f) ? 0f : 24f);

        var oldColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, appear);
        float backW = Mathf.Max(textWidth, spread * 2f + 280f);
        float backTop = Mathf.Min(headY, topOfCluster) - 10f;
        // the backing stays inside the cluster's own band (the subtitle backing reaches up; this one must not reach the face)
        float backBottom = Mathf.Max(previewRect.yMax, bottomOfCluster) + 26f;
        var bo = GUI.color; GUI.color = new Color(1f, 1f, 1f, 0.62f * appear);
        GUI.DrawTexture(new Rect(safe.center.x - backW * 0.62f, backTop - 18f, backW * 1.24f, backBottom - backTop + 18f), Glow(), ScaleMode.StretchToFill, true);
        GUI.color = bo;

        // a heading only where it cannot land on a witness: the timed decision, or a decision with its situation stated
        if (!string.IsNullOrEmpty(speaker) && (remaining >= 0f || sitH > 0f))
            Write(new Rect(0, headY, w, 20f), UiKit.Track(speaker), caps, new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.9f * appear));
        if (sitH > 0f)
            Write(new Rect(safe.center.x - textWidth / 2f, topOfCluster - 10f - sitH, textWidth, sitH + 2f), situation, context, new Color(UiKit.Cream.r, UiKit.Cream.g, UiKit.Cream.b, 0.92f * appear));
        if (textH > 0f)
            Write(previewRect, preview, detail, selected >= 0 && !Available(selected) ? UiKit.Muted : UiKit.Cream);

        // the dial
        var dial = new Rect(centre.x - Radius, centre.y - Radius, Radius * 2f, Radius * 2f);
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
                var p = centre + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * (Radius + 8f);
                Color c = i < lit ? (urgent ? Color.Lerp(UiKit.Gold, UiKit.Danger, 0.55f) * pulse : UiKit.Gold) : new Color(1f, 1f, 1f, 0.12f);
                UiKit.Fill(new Rect(p.x - 1.8f, p.y - 1.8f, 3.6f, 3.6f), c);
            }
        }
        // where the mouse, trackpad or stick is pointing, then the needle
        if (pointer.sqrMagnitude > 0.0004f)
        {
            var p = centre + new Vector2(pointer.x, -pointer.y) * (Radius * 0.66f);
            UiKit.Fill(new Rect(p.x - 2.5f, p.y - 2.5f, 5f, 5f), new Color(UiKit.Cream.r, UiKit.Cream.g, UiKit.Cream.b, 0.5f * appear));
        }
        DrawNeedle(centre, appear, selected >= 0 || pointer.sqrMagnitude > 0.06f);

        // the bearings
        for (int i = 0; i < labels.Length; i++)
        {
            var p = LabelPos(i, centre, spread);
            bool avail = Available(i), seen = Asked(i), focus = i == selected;
            string text = labels[i];
            float tw = label.CalcSize(new GUIContent(text)).x;
            Color c = !avail ? new Color(0.52f, 0.5f, 0.47f, 0.92f) : focus ? UiKit.Gold : seen ? new Color(0.74f, 0.72f, 0.68f, 0.95f) : UiKit.Cream;
            // a side label grows away from the dial, so a long one never runs over it
            float cx = Directions[i].x < 0f ? Mathf.Min(p.x, centre.x - Radius - 26f - tw / 2f) : Directions[i].x > 0f ? Mathf.Max(p.x, centre.x + Radius + 26f + tw / 2f) : p.x;
            // every label sits on a soft dark pill (over a white kurta the cream lettering had no edge); the focused one darker
            UiKit.Fill(new Rect(cx - tw / 2f - 12f, p.y - 15f, tw + 24f, 30f), new Color(0f, 0f, 0f, focus && avail ? 0.5f : 0.3f));
            if (focus && avail)
            {
                UiKit.Fill(new Rect(cx - tw / 2f - 12f, p.y + 15f, tw + 24f, 2f), new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.95f));
            }
            Write(new Rect(cx - 170f, p.y - 15f, 340f, 30f), text, label, c);
            // the shortcut number, small, ahead of the label
            Write(new Rect(cx - tw / 2f - 34f, p.y - 11f, 20f, 22f), (i + 1).ToString(), hint, new Color(c.r, c.g, c.b, c.a * 0.75f));
            // asked: a tick after the label; gated: a padlock (and the reason when focused)
            if (!avail && !seen) Padlock(new Vector2(cx + tw / 2f + 16f, p.y + 3f), c);
            else if (seen) Check(new Vector2(cx + tw / 2f + 16f, p.y + 1f), c);
        }
        GUI.color = oldColor;
        Write(new Rect(0, Mathf.Min(h, safe.yMax) - 32f, w, 24f),
            "Point with mouse, trackpad, arrows or stick   ·   Click / E / Enter / A to commit   ·   1–" + labels.Length + " to answer directly", hint,
            new Color(UiKit.Cream.r, UiKit.Cream.g, UiKit.Cream.b, 0.66f));
    }

    /// <summary>For a moment after a commitment the chosen bearing stays lit in brass, a ring goes out from the dial,
    /// and the dial clears: the decision reads as taken.</summary>
    public void Afterglow(float w, float h)
    {
        float t = Time.unscaledTime - committedAt;
        if (committed < 0 || t > AfterglowTime || labels != null || GameState.Paused) return;
        Styles();
        StepNeedle();
        float k = 1f - Smooth(0.15f, AfterglowTime, t);
        var centre = Centre(w, h);
        var dir = Directions[committed];
        var p = dir.y == 0f ? centre + new Vector2(dir.x * Spread(w), 0f) : centre + new Vector2(0f, -dir.y * (Radius + BearingGap));
        var old = GUI.color; GUI.color = new Color(1f, 1f, 1f, k);
        GUI.DrawTexture(new Rect(centre.x - Radius, centre.y - Radius, Radius * 2f, Radius * 2f), Dial(), ScaleMode.StretchToFill, true);
        GUI.color = old;
        DrawNeedle(centre, k, true);
        // the commit cue: a brass ring that goes out from the rim and fades
        float ring = Smooth(0f, 0.45f, t);
        float rr = Radius * (1.05f + 0.55f * ring);
        UiKit.Ring(new Rect(centre.x - rr, centre.y - rr, rr * 2f, rr * 2f), new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.7f * (1f - ring)), 2.5f);
        float grow = 1f + 0.08f * Smooth(0f, 0.15f, t);
        label.fontSize = Mathf.RoundToInt(22 * grow);
        float tw = label.CalcSize(new GUIContent(committedLabel)).x;
        float cx = dir.x < 0f ? Mathf.Min(p.x, centre.x - Radius - 26f - tw / 2f) : dir.x > 0f ? Mathf.Max(p.x, centre.x + Radius + 26f + tw / 2f) : p.x;
        Write(new Rect(cx - 190f, p.y - 16f, 380f, 32f), committedLabel, label, new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, k));
        label.fontSize = 22;
    }

    // ================================================================== subtitles
    /// <summary>
    /// A voiced line as subtitles: split into readable phrases (sentences, long ones at a comma or a dash) and shown
    /// one at a time in step with the voice. With no audio the whole line shows.
    /// </summary>
    public void Speak(float w, float h, string speaker, string text, AudioSource source)
    {
        if (string.IsNullOrEmpty(text) || text.Trim().Length == 0) return;
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
    /// Tools/vo_recordings.py). False, with nothing set, when the clip has none or its subtitle has changed.</summary>
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
