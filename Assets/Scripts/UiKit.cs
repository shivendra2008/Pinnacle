using UnityEngine;

/// <summary>
/// Shared UI look: one palette, one pair of fonts, rounded cards with a soft drop shadow and a thin gold border,
/// key-cap prompts, progress bars and buttons. All drawn with GUI.DrawTexture's rounded-rect support,
/// so it scales cleanly with the resolution-based GUI.matrix the story scripts already use.
/// </summary>
public static class UiKit
{
    public static readonly Color Ink = new Color(0.045f, 0.05f, 0.075f, 1f);
    public static readonly Color Gold = new Color(0.95f, 0.77f, 0.38f, 1f);
    public static readonly Color Cream = new Color(0.97f, 0.94f, 0.86f, 1f);
    public static readonly Color Muted = new Color(0.78f, 0.75f, 0.69f, 1f);
    public static readonly Color Good = new Color(0.5f, 0.9f, 0.6f, 1f);
    public static readonly Color Danger = new Color(0.95f, 0.35f, 0.3f, 1f);

    const float R = 12f;

    static Font body, title;
    public static Font Body
    {
        get
        {
            if (body == null) body = Font.CreateDynamicFontFromOSFont(new[] { "Avenir Next", "Helvetica Neue", "Segoe UI", "Arial" }, 18);
            return body;
        }
    }
    public static Font Title
    {
        get
        {
            if (title == null) title = Font.CreateDynamicFontFromOSFont(new[] { "Didot", "Georgia", "Palatino", "Times New Roman" }, 48);
            return title;
        }
    }

    static void Rounded(Rect r, Color c, float radius, float border = 0f)
    {
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c,
            border > 0f ? new Vector4(border, border, border, border) : Vector4.zero,
            new Vector4(radius, radius, radius, radius));
    }

    /// <summary>Dark glass card: soft shadow, rounded fill, thin gold border.</summary>
    public static void Card(Rect r, float alpha = 0.82f)
    {
        for (int i = 3; i >= 1; i--)
            Rounded(new Rect(r.x - i * 2f, r.y - i * 2f + 5f, r.width + i * 4f, r.height + i * 4f), new Color(0, 0, 0, 0.07f), R + i * 2f);
        Rounded(r, new Color(Ink.r, Ink.g, Ink.b, Mathf.Clamp01(alpha + 0.06f)), R);
        Rounded(r, new Color(Gold.r, Gold.g, Gold.b, 0.35f), R, 1.5f);
    }

    /// <summary>Text with a soft double drop shadow, for text drawn straight over the 3D view.</summary>
    public static void Text(Rect r, string t, GUIStyle s)
    {
        var c = s.normal.textColor;
        s.normal.textColor = new Color(0, 0, 0, 0.55f);
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
        Rounded(new Rect(W / 2f - 9f, H / 2f - 9f, 18f, 18f), new Color(1, 1, 1, 0.18f), 9f, 1.2f);
        Rounded(new Rect(W / 2f - 2f, H / 2f - 2f, 4f, 4f), new Color(1, 1, 1, 0.9f), 2f);
    }

    /// <summary>"[E]  Examine Laptop" drawn as a key cap plus a label.</summary>
    public static void Prompt(float W, float H, string text)
    {
        string key = "", label = text;
        if (text.StartsWith("["))
        {
            int e = text.IndexOf(']');
            if (e > 0) { key = text.Substring(1, e - 1); label = text.Substring(e + 1).Trim(); }
        }
        var ls = new GUIStyle(GUI.skin.label) { font = Body, fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        ls.normal.textColor = Cream;
        float lw = ls.CalcSize(new GUIContent(label)).x;
        float kw = key.Length > 0 ? Mathf.Max(42f, 18f + key.Length * 14f) : 0f;
        float total = kw + (kw > 0 ? 14f : 0f) + lw + 28f;
        var box = new Rect(W / 2f - total / 2f, H / 2f + 30f, total, 46f);
        Card(box, 0.72f);
        float x = box.x + 14f;
        if (kw > 0)
        {
            var kr = new Rect(x, box.y + 7f, kw, 32f);
            Rounded(kr, Gold, 7f);
            var ks = new GUIStyle(GUI.skin.label) { font = Body, fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            ks.normal.textColor = Ink;
            GUI.Label(kr, key, ks);
            x += kw + 14f;
        }
        GUI.Label(new Rect(x, box.y, lw + 6f, box.height), label, ls);
    }

    public static void Bar(Rect r, float value, Color fill)
    {
        Rounded(r, new Color(1, 1, 1, 0.12f), r.height / 2f);
        if (value > 0.001f) Rounded(new Rect(r.x, r.y, Mathf.Max(r.height, r.width * Mathf.Clamp01(value)), r.height), fill, r.height / 2f);
    }

    // ------------------------------------------------------------- styles
    /// <summary>Applies the fonts and palette to the story scripts' shared GUIStyles.</summary>
    public static void Skin(GUIStyle sub, GUIStyle title1, GUIStyle title2, GUIStyle small, GUIStyle panel, GUIStyle head, GUIStyle prompt)
    {
        sub.font = Body; sub.normal.textColor = Cream;
        title1.font = Title; title1.normal.textColor = Gold; title1.fontStyle = FontStyle.Bold;
        title2.font = Body; title2.normal.textColor = Muted;
        small.font = Body; small.normal.textColor = Muted;
        panel.font = Body; panel.normal.textColor = Cream;
        head.font = Body; head.normal.textColor = Gold; head.fontStyle = FontStyle.Bold;
        prompt.font = Body; prompt.normal.textColor = Gold;
    }

    static Texture2D roundTex(Color c)
    {
        const int n = 48; const float rad = 14f;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Max(rad - x, 0f, x - (n - 1 - rad)), dy = Mathf.Max(rad - y, 0f, y - (n - 1 - rad));
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(rad - d + 0.5f);
                t.SetPixel(x, y, new Color(c.r, c.g, c.b, c.a * a));
            }
        t.Apply();
        return t;
    }

    static GUIStyle button;
    public static GUIStyle ButtonStyle()
    {
        if (button != null) return button;
        button = new GUIStyle(GUI.skin.button) { font = Body, fontSize = 20, fontStyle = FontStyle.Bold, border = new RectOffset(16, 16, 16, 16), padding = new RectOffset(16, 16, 8, 8) };
        button.normal.background = roundTex(new Color(0.12f, 0.13f, 0.18f, 0.95f));
        button.hover.background = roundTex(new Color(0.96f, 0.78f, 0.4f, 1f));
        button.active.background = roundTex(new Color(0.75f, 0.58f, 0.25f, 1f));
        button.normal.textColor = Cream; button.hover.textColor = Ink; button.active.textColor = Ink;
        button.focused.background = button.normal.background; button.focused.textColor = Cream;
        return button;
    }
}
