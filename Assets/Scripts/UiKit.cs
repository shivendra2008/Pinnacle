using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared UI look for the whole game - one visual language for every screen (menu, HUD, dialogue, case file, pause).
/// Style: cinematic thriller. Near-black panels with square corners and hairline borders, a thin aged-brass accent,
/// bone-white text, a serif display face for titles and widely tracked small capitals for labels.
/// Everything is drawn with IMGUI so it scales with the resolution-based GUI.matrix the story scripts use.
/// </summary>
public static class UiKit
{
    public static readonly Color Ink = new Color(0.016f, 0.018f, 0.022f, 1f);
    public static readonly Color Gold = new Color(0.80f, 0.67f, 0.44f, 1f);      // aged brass (the one accent colour)
    public static readonly Color Cream = new Color(0.90f, 0.88f, 0.83f, 1f);     // bone white
    public static readonly Color Muted = new Color(0.58f, 0.57f, 0.54f, 1f);
    public static readonly Color Good = new Color(0.56f, 0.74f, 0.60f, 1f);
    public static readonly Color Danger = new Color(0.78f, 0.20f, 0.17f, 1f);
    static readonly Color Line = new Color(0.90f, 0.88f, 0.83f, 0.16f);

    const float R = 2f;

    // The game ships no font files: every screen borrows the computer's own fonts (the menu, the subtitles, the HUD).
    // Each list names the Mac's fonts and Windows' own; on Windows its fonts are asked for first. A list the computer
    // cannot supply falls back to Unity's built-in font, which is inside every build, so no text goes missing.
    static Font body, title, builtIn;
    static bool Windows =>
        Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;
    public static Font Body
    {
        get
        {
            if (body == null)
                body = OsFont(Windows ? new[] { "Segoe UI", "Arial", "Avenir Next", "Helvetica Neue" }
                                      : new[] { "Avenir Next", "Helvetica Neue", "Segoe UI", "Arial" }, 18, "body");
            return body;
        }
    }
    public static Font Title
    {
        get
        {
            if (title == null)
                title = OsFont(Windows ? new[] { "Georgia", "Palatino Linotype", "Times New Roman", "Didot", "Bodoni 72" }
                                       : new[] { "Didot", "Bodoni 72", "Georgia", "Palatino", "Times New Roman" }, 48, "title");
            return title;
        }
    }

    static Font OsFont(string[] names, int size, string role)
    {
        Font f = null;
        try { f = Font.CreateDynamicFontFromOSFont(names, size); }
        catch (System.Exception e) { Debug.LogWarning("[UiKit] " + role + " font: " + e.Message); }
        if (f != null && f.HasCharacter('A') && f.HasCharacter('a'))
            return f;
        Debug.LogWarning("[UiKit] no " + role + " font found (" + string.Join(", ", names) + "); using Unity's built-in font");
        if (builtIn == null) builtIn = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return builtIn;
    }

    /// <summary>"OBJECTIVE" -> "O B J E C T I V E": the widely tracked capitals used for every label.</summary>
    public static string Track(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new System.Text.StringBuilder(s.Length * 2);
        for (int i = 0; i < s.Length; i++)
        {
            sb.Append(char.ToUpperInvariant(s[i]));
            if (i < s.Length - 1) sb.Append(s[i] == ' ' ? "  " : " ");
        }
        return sb.ToString();
    }

    static void Rounded(Rect r, Color c, float radius, float border = 0f)
    {
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c,
            border > 0f ? new Vector4(border, border, border, border) : Vector4.zero,
            new Vector4(radius, radius, radius, radius));
    }

    public static void Fill(Rect r, Color c) { Rounded(r, c, 0f); }

    /// <summary>Dark panel: soft shadow, near-black fill, hairline border and a short brass accent on the top edge.</summary>
    public static void Card(Rect r, float alpha = 0.82f)
    {
        bool fullscreen = r.x <= 0.5f && r.y <= 0.5f && r.width > 900f && r.height > 500f;
        if (fullscreen) { Fill(r, new Color(0f, 0f, 0f, Mathf.Clamp01(alpha))); return; }      // a dim behind an overlay
        for (int i = 3; i >= 1; i--)
            Rounded(new Rect(r.x - i * 3f, r.y - i * 3f + 6f, r.width + i * 6f, r.height + i * 6f), new Color(0, 0, 0, 0.09f), R + i * 3f);
        Rounded(r, new Color(Ink.r, Ink.g, Ink.b, Mathf.Clamp01(alpha + 0.08f)), R);
        Rounded(r, Line, R, 1f);
        if (r.width > 120f && r.height > 36f)
            Fill(new Rect(r.x, r.y, Mathf.Min(56f, r.width * 0.25f), 2f), new Color(Gold.r, Gold.g, Gold.b, 0.9f));
    }

    /// <summary>Text with a soft double drop shadow, for text drawn straight over the 3D view.</summary>
    public static void Text(Rect r, string t, GUIStyle s)
    {
        var c = s.normal.textColor;
        s.normal.textColor = new Color(0, 0, 0, 0.6f);
        GUI.Label(new Rect(r.x + 1f, r.y + 3f, r.width, r.height), t, s);
        GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), t, s);
        s.normal.textColor = c;
        GUI.Label(r, t, s);
    }

    /// <summary>A circular outline (used to ring the object the crosshair is on).</summary>
    public static void Ring(Rect r, Color c, float thickness)
    {
        Rounded(r, c, r.width / 2f, thickness);
    }

    public static void Crosshair(float W, float H)
    {
        Rounded(new Rect(W / 2f - 2.5f, H / 2f - 2.5f, 5f, 5f), new Color(0, 0, 0, 0.35f), 2.5f);
        Rounded(new Rect(W / 2f - 1.5f, H / 2f - 1.5f, 3f, 3f), new Color(1, 1, 1, 0.85f), 1.5f);
    }

    /// <summary>"[E]  Examine Laptop" drawn as an outlined key plus a tracked label, with no box behind it.</summary>
    public static void Prompt(float W, float H, string text)
    {
        string key = "", label = text;
        if (text.StartsWith("["))
        {
            int e = text.IndexOf(']');
            if (e > 0) { key = text.Substring(1, e - 1); label = text.Substring(e + 1).Trim(); }
        }
        var ls = new GUIStyle(GUI.skin.label) { font = Body, fontSize = 19, alignment = TextAnchor.MiddleLeft };
        ls.normal.textColor = Cream;
        float lw = ls.CalcSize(new GUIContent(label)).x;
        float kw = key.Length > 0 ? Mathf.Max(34f, 16f + key.Length * 13f) : 0f;
        float total = kw + (kw > 0 ? 14f : 0f) + lw;
        float x = W / 2f - total / 2f, y = H / 2f + 38f;
        Fill(new Rect(x - 26f, y - 6f, total + 52f, 46f), new Color(0, 0, 0, 0.42f));
        Fill(new Rect(x - 26f, y - 6f, 2f, 46f), new Color(Gold.r, Gold.g, Gold.b, 0.9f));
        if (kw > 0)
        {
            var kr = new Rect(x, y, kw, 34f);
            Rounded(kr, new Color(0, 0, 0, 0.55f), 3f);
            Rounded(kr, Cream, 3f, 1.5f);
            var ks = new GUIStyle(GUI.skin.label) { font = Body, fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            ks.normal.textColor = Cream;
            GUI.Label(kr, key, ks);
            x += kw + 14f;
        }
        Text(new Rect(x, y, lw + 8f, 34f), label, ls);
    }

    public static void Bar(Rect r, float value, Color fill)
    {
        Fill(r, new Color(1, 1, 1, 0.10f));
        if (value > 0.001f) Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(value), r.height), fill);
    }

    // ---------------------------------------------------------- letterbox
    static float bars; static int barsFrame = -1;

    /// <summary>Cinema bars that slide in during conversations and cutscenes. Call once per OnGUI, before other UI.</summary>
    public static void Letterbox(float W, float H, bool on)
    {
        if (Time.frameCount != barsFrame)
        {
            barsFrame = Time.frameCount;
            bars = Mathf.MoveTowards(bars, on ? 1f : 0f, Time.unscaledDeltaTime * 1.6f);
        }
        if (bars <= 0.001f) return;
        float h = H * 0.085f * Mathf.SmoothStep(0f, 1f, bars);
        Fill(new Rect(0, 0, W, h), Color.black);
        Fill(new Rect(0, H - h, W, h), Color.black);
    }

    // ------------------------------------------------------------- styles
    /// <summary>Applies the fonts and palette to the story scripts' shared GUIStyles.</summary>
    public static void Skin(GUIStyle sub, GUIStyle title1, GUIStyle title2, GUIStyle small, GUIStyle panel, GUIStyle head, GUIStyle prompt)
    {
        sub.font = Body; sub.normal.textColor = Cream;
        title1.font = Title; title1.normal.textColor = Cream; title1.fontStyle = FontStyle.Normal;
        title2.font = Body; title2.normal.textColor = Muted;
        small.font = Body; small.normal.textColor = Muted;
        panel.font = Body; panel.normal.textColor = Cream;
        head.font = Body; head.normal.textColor = Gold; head.fontStyle = FontStyle.Bold; head.fontSize = Mathf.Min(head.fontSize, 15);
        prompt.font = Body; prompt.normal.textColor = Cream; prompt.fontStyle = FontStyle.Normal;
    }

    static Texture2D FlatTex(Color fill, Color border)
    {
        const int n = 16;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                t.SetPixel(x, y, (x == 0 || y == 0 || x == n - 1 || y == n - 1) ? border : fill);
        t.Apply();
        return t;
    }

    static GUIStyle button;
    public static GUIStyle ButtonStyle()
    {
        if (button != null && button.normal.background != null) return button;
        button = new GUIStyle(GUI.skin.button) { font = Body, fontSize = 17, fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleCenter, border = new RectOffset(2, 2, 2, 2), padding = new RectOffset(18, 18, 8, 8) };
        button.normal.background = FlatTex(new Color(0.03f, 0.032f, 0.04f, 0.92f), new Color(0.9f, 0.88f, 0.83f, 0.22f));
        button.hover.background = FlatTex(new Color(0.90f, 0.88f, 0.83f, 1f), new Color(0.90f, 0.88f, 0.83f, 1f));
        button.active.background = FlatTex(Gold, Gold);
        button.normal.textColor = Cream; button.hover.textColor = Ink; button.active.textColor = Ink;
        button.focused.background = button.normal.background; button.focused.textColor = Cream;
        button.onNormal = button.normal; button.onHover = button.hover; button.onActive = button.active; button.onFocused = button.focused;
        return button;
    }

    // ---------------------------------------------------------- case file
    /// <summary>One entry in the case file. state: 0 = still open (not found), 1 = confirmed.</summary>
    public struct Note
    {
        public string head, body; public int state;
        public Note(string head, string body, int state = 1) { this.head = head; this.body = body; this.state = state; }
    }

    static Texture2D grain;
    static Texture2D Grain
    {
        get
        {
            if (grain != null) return grain;
            const int n = 128;
            grain = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var r = new System.Random(11);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float v = (float)r.NextDouble();
                    grain.SetPixel(x, y, new Color(1f, 0.96f, 0.88f, v * v * 0.07f));
                }
            grain.Apply();
            return grain;
        }
    }

    static float fileScroll;
    static GUIStyle fLabel, fTitle, fMeta, fIndex, fHead, fBody, fFoot;

    /// <summary>
    /// The detective's case file (J): a dossier page with numbered, scrollable entries.
    /// Used by every act so the journal looks the same - and like the rest of the game - everywhere.
    /// </summary>
    public static void CaseFile(float W, float H, string title, string meta, IList<Note> notes, string emptyText)
    {
        if (fLabel == null)
        {
            fLabel = new GUIStyle(GUI.skin.label) { font = Body, fontSize = 12, fontStyle = FontStyle.Bold };
            fLabel.normal.textColor = Gold;
            fTitle = new GUIStyle(GUI.skin.label) { font = Title, fontSize = 38 };
            fTitle.normal.textColor = Cream;
            fMeta = new GUIStyle(GUI.skin.label) { font = Body, fontSize = 14, alignment = TextAnchor.UpperRight };
            fMeta.normal.textColor = Muted;
            fIndex = new GUIStyle(GUI.skin.label) { font = Title, fontSize = 24 };
            fIndex.normal.textColor = new Color(Gold.r, Gold.g, Gold.b, 0.85f);
            fHead = new GUIStyle(GUI.skin.label) { font = Body, fontSize = 16, fontStyle = FontStyle.Bold };
            fHead.normal.textColor = Cream;
            fBody = new GUIStyle(GUI.skin.label) { font = Body, fontSize = 15, wordWrap = true };
            fBody.normal.textColor = new Color(0.74f, 0.73f, 0.70f);
            fFoot = new GUIStyle(GUI.skin.label) { font = Body, fontSize = 12 };
            fFoot.normal.textColor = Muted;
        }

        Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.78f));
        float pw = Mathf.Min(940f, W - 80f), ph = Mathf.Min(680f, H - 80f);
        var r = new Rect(W / 2f - pw / 2f, H / 2f - ph / 2f, pw, ph);
        Card(r, 0.92f);
        GUI.DrawTextureWithTexCoords(r, Grain, new Rect(0, 0, r.width / 128f, r.height / 128f));
        // a red margin line, like a ruled case sheet
        Fill(new Rect(r.x + 92f, r.y + 112f, 1f, r.height - 160f), new Color(Danger.r, Danger.g, Danger.b, 0.35f));

        GUI.Label(new Rect(r.x + 44f, r.y + 28f, 400f, 18f), Track("Case file"), fLabel);
        GUI.Label(new Rect(r.x + 42f, r.y + 44f, r.width - 84f, 50f), title, fTitle);
        GUI.Label(new Rect(r.x + 44f, r.y + 30f, r.width - 88f, 40f), meta, fMeta);
        Fill(new Rect(r.x + 44f, r.y + 102f, r.width - 88f, 1f), Line);

        var view = new Rect(r.x + 44f, r.y + 114f, r.width - 88f, r.height - 166f);
        float textW = view.width - 78f;

        // layout pass
        float total = 0f;
        var hs = new float[notes.Count];
        for (int i = 0; i < notes.Count; i++)
        {
            float h = fBody.CalcHeight(new GUIContent(notes[i].body ?? ""), textW) + (string.IsNullOrEmpty(notes[i].head) ? 0f : 24f) + 22f;
            hs[i] = Mathf.Max(52f, h); total += hs[i];
        }
        float max = Mathf.Max(0f, total - view.height);
        var ev = Event.current;
        if (ev.type == EventType.ScrollWheel) { fileScroll += ev.delta.y * 14f; ev.Use(); }
        if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.DownArrow) fileScroll += 40f;
        if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.UpArrow) fileScroll -= 40f;
        fileScroll = Mathf.Clamp(fileScroll, 0f, max);

        GUI.BeginGroup(view);
        float y = -fileScroll;
        if (notes.Count == 0) GUI.Label(new Rect(64f, 16f, textW, 60f), emptyText, fBody);
        for (int i = 0; i < notes.Count; i++)
        {
            if (y + hs[i] > 0f && y < view.height)
            {
                bool open = notes[i].state == 0;
                var ic = fIndex.normal.textColor;
                fIndex.normal.textColor = open ? new Color(Muted.r, Muted.g, Muted.b, 0.5f) : ic;
                GUI.Label(new Rect(0f, y + 6f, 44f, 32f), (i + 1).ToString("00"), fIndex);
                fIndex.normal.textColor = ic;
                float ty = y + 8f;
                if (!string.IsNullOrEmpty(notes[i].head))
                {
                    var hc = fHead.normal.textColor;
                    fHead.normal.textColor = open ? Muted : Cream;
                    GUI.Label(new Rect(64f, ty, textW - 110f, 22f), notes[i].head, fHead);
                    fHead.normal.textColor = hc;
                    var st = new GUIStyle(fLabel) { alignment = TextAnchor.UpperRight, fontSize = 11 };
                    st.normal.textColor = open ? Danger : Good;
                    GUI.Label(new Rect(64f, ty + 3f, textW, 18f), Track(open ? "Open" : "Confirmed"), st);
                    ty += 24f;
                }
                GUI.Label(new Rect(64f, ty, textW, hs[i] - 20f), notes[i].body, fBody);
                Fill(new Rect(64f, y + hs[i] - 1f, textW, 1f), new Color(1, 1, 1, 0.06f));
            }
            y += hs[i];
        }
        GUI.EndGroup();

        if (max > 0f)        // slim scrollbar
        {
            float track = view.height, thumb = Mathf.Max(30f, track * view.height / total);
            Fill(new Rect(r.xMax - 26f, view.y, 2f, track), new Color(1, 1, 1, 0.08f));
            Fill(new Rect(r.xMax - 26f, view.y + (track - thumb) * (fileScroll / max), 2f, thumb), Gold);
        }
        GUI.Label(new Rect(r.x + 44f, r.yMax - 36f, r.width - 88f, 18f),
            Track("J  close") + (max > 0f ? "        " + Track("Mouse wheel  scroll") : ""), fFoot);
    }
}
