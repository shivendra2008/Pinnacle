using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Paper things Arjun handles, built the same way wherever they appear, so the player can recognise them again:
/// the Record Room's sign-out sheet with Inspector Reddy's signature (Act III, in the cage; Act IV, on the chai-stall
/// counter), her ID card, Priya's newspaper cuttings, printouts and Arjun's notebook page.
/// Printed text is depth-tested world text (GameKit.Solid); handwriting is drawn into a texture from pen strokes, so a
/// signature has one shape everywhere.
/// </summary>
public static class PaperKit
{
    static Texture2D signatureTex;
    static Material inkMat, paperMat, cardMat;
    public static readonly Color Ink = new Color(0.08f, 0.1f, 0.24f);           // blue-black ballpoint
    public static readonly Color Print = new Color(0.12f, 0.11f, 0.1f);

    // ------------------------------------------------------------------ handwriting
    /// <summary>
    /// Inspector Reddy's signature: a tall looped S, a dot, a sharp-shouldered R, "eddy" run together with the two d's
    /// rising high, and an underline that sweeps back under the whole name. Strokes in a 512 x 160 canvas, y up.
    /// </summary>
    static readonly Vector2[][] ReddyStrokes =
    {
        new[] { new Vector2(80,126), new Vector2(62,140), new Vector2(40,133), new Vector2(37,113), new Vector2(57,98), new Vector2(80,84), new Vector2(85,61), new Vector2(67,44), new Vector2(43,41), new Vector2(27,54) },
        new[] { new Vector2(101,49), new Vector2(104,52), new Vector2(105,47), new Vector2(101,49) },
        new[]
        {
            new Vector2(127,43), new Vector2(131,88), new Vector2(136,131), new Vector2(160,135), new Vector2(173,120), new Vector2(166,104), new Vector2(146,97),
            new Vector2(161,79), new Vector2(177,60), new Vector2(191,48), new Vector2(205,57), new Vector2(214,68), new Vector2(205,75), new Vector2(196,63),
            new Vector2(205,50), new Vector2(221,46), new Vector2(236,58), new Vector2(229,72), new Vector2(218,64), new Vector2(224,51), new Vector2(238,49),
            new Vector2(244,92), new Vector2(247,128), new Vector2(241,131), new Vector2(238,98), new Vector2(241,60), new Vector2(248,46), new Vector2(263,58),
            new Vector2(256,72), new Vector2(245,64), new Vector2(250,51), new Vector2(264,49), new Vector2(271,92), new Vector2(274,128), new Vector2(268,131),
            new Vector2(265,98), new Vector2(268,60), new Vector2(274,46), new Vector2(286,61), new Vector2(291,72), new Vector2(294,60), new Vector2(299,48),
            new Vector2(300,26), new Vector2(294,8), new Vector2(281,4), new Vector2(276,15), new Vector2(291,29), new Vector2(330,40), new Vector2(390,51),
            new Vector2(426,45), new Vector2(392,30), new Vector2(262,21), new Vector2(150,22), new Vector2(96,29),
        },
    };

    static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    /// <summary>Pen strokes drawn into an alpha texture: the nib thins on fast curves and thickens where it slows.</summary>
    public static Texture2D DrawStrokes(Vector2[][] strokes, int w, int h, float pen)
    {
        var a = new float[w * h];
        foreach (var s in strokes)
        {
            if (s.Length < 2) continue;
            var pts = new List<Vector2>();
            for (int i = 0; i < s.Length - 1; i++)
            {
                Vector2 p0 = s[Mathf.Max(0, i - 1)], p1 = s[i], p2 = s[i + 1], p3 = s[Mathf.Min(s.Length - 1, i + 2)];
                int n = Mathf.Max(4, Mathf.CeilToInt(Vector2.Distance(p1, p2) / 1.2f));
                for (int k = 0; k < n; k++) pts.Add(CatmullRom(p0, p1, p2, p3, k / (float)n));
            }
            pts.Add(s[s.Length - 1]);
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 p = pts[i];
                float curve = i > 0 && i < pts.Count - 1 ? Vector2.Angle(pts[i] - pts[i - 1], pts[i + 1] - pts[i]) : 0f;
                float r = pen * (0.85f + 0.35f * Mathf.Clamp01(curve / 40f)) * (i < 6 ? 0.6f + i * 0.07f : 1f);      // a lighter touch where the pen lands
                int x0 = Mathf.FloorToInt(p.x - r - 1), x1 = Mathf.CeilToInt(p.x + r + 1), y0 = Mathf.FloorToInt(p.y - r - 1), y1 = Mathf.CeilToInt(p.y + r + 1);
                for (int y = Mathf.Max(0, y0); y <= Mathf.Min(h - 1, y1); y++)
                    for (int x = Mathf.Max(0, x0); x <= Mathf.Min(w - 1, x1); x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), p);
                        float cov = Mathf.Clamp01(r - d + 0.5f);
                        if (cov > a[y * w + x]) a[y * w + x] = cov;
                    }
            }
        }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
        var px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(Ink.r, Ink.g, Ink.b, a[i] * 0.94f);
        tex.SetPixels(px); tex.Apply(true, true);
        return tex;
    }

    public static Texture2D ReddySignature()
    {
        if (signatureTex == null) { signatureTex = DrawStrokes(ReddyStrokes, 512, 160, 3.4f); signatureTex.name = "ReddySignature"; }
        return signatureTex;
    }

    static Material InkMaterial()
    {
        if (inkMat != null) return inkMat;
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Ink" };
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent - 20;
        m.SetFloat("_Smoothness", 0.35f); m.SetFloat("_Metallic", 0f);
        m.SetTexture("_BaseMap", ReddySignature()); m.mainTexture = ReddySignature();
        m.SetColor("_BaseColor", Color.white);
        inkMat = m;
        return m;
    }

    /// <summary>Reddy's signature on a surface: a thin quad, its face up the paper's normal, width in metres.</summary>
    public static GameObject Signature(Transform parent, Vector3 localPos, Quaternion localRot, float width)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "ReddySignature";
        Object.Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(parent, false);
        q.transform.localPosition = localPos; q.transform.localRotation = localRot;
        q.transform.localScale = new Vector3(width, width * 160f / 512f, 1f);
        var r = q.GetComponent<Renderer>(); r.sharedMaterial = InkMaterial(); r.shadowCastingMode = ShadowCastingMode.Off;
        return q;
    }

    static Material Paper()
    {
        if (paperMat == null) paperMat = ProcTex.Mat(ProcTex.Get("paper"), new Color(0.9f, 0.86f, 0.74f), new Vector2(1.2f, 1.6f), 0.12f, 0.25f);
        return paperMat;
    }

    static GameObject Sheet(Transform parent, string name, Vector3 pos, Vector2 size, Material m)
    {
        var go = GameKit.Box(parent, name, pos, new Vector3(size.x, 0.0012f, size.y), m);
        var r = go.GetComponent<Renderer>(); r.shadowCastingMode = ShadowCastingMode.On;
        return go;
    }

    /// <summary>Printed text lying on a sheet (its top towards local +z), depth-tested, 0.6 mm above the paper.</summary>
    static TextMesh Printed(Transform parent, string text, Vector3 local, float size, Color c, TextAnchor anchor = TextAnchor.MiddleLeft)
    {
        var tm = GameKit.Solid(GameKit.Text(parent, text, local + Vector3.up * 0.0012f, Quaternion.Euler(90f, 0f, 0f), size, c));
        tm.anchor = anchor;
        return tm;
    }

    /// <summary>
    /// The Record Room's sign-out sheet for Block C / 14 / 1942, as a thing on a surface (A4-ish, its top edge towards
    /// local +z): a printed, ruled form, three entries in three hands, the last "S. REDDY" with her signature.
    /// Returns its root, lying flat at the parent's origin.
    /// </summary>
    public static Transform SignOutSheet(Transform parent, Vector3 pos, float yaw)
    {
        var root = new GameObject("SignOutSheet").transform;
        root.SetParent(parent, false); root.position = pos; root.rotation = Quaternion.Euler(0f, yaw, 0f);
        const float W = 0.21f, H = 0.29f;
        Sheet(root, "SheetPaper", Vector3.zero, new Vector2(W, H), Paper());
        // the printed form: a heading, column heads, ruled lines
        Printed(root, "CHANDNIPUR MUNICIPAL LIBRARY - RECORD ROOM", new Vector3(0f, 0f, 0.125f), 0.0062f, Print, TextAnchor.MiddleCenter);
        Printed(root, "SIGN-OUT REGISTER    Block C / Shelf 14 / 1942", new Vector3(0f, 0f, 0.111f), 0.0055f, Print, TextAnchor.MiddleCenter);
        float[] cols = { -0.094f, -0.044f, 0.01f, 0.054f };
        string[] heads = { "Date", "Name", "Purpose", "Signature" };
        for (int i = 0; i < 4; i++) Printed(root, heads[i], new Vector3(cols[i], 0f, 0.094f), 0.005f, Print);
        var rule = GameKit.Lit(new Color(0.35f, 0.4f, 0.5f), 0.1f);
        for (int i = 0; i < 9; i++)
        {
            var ln = GameKit.Box(root, "Rule", new Vector3(0f, 0.0007f, 0.087f - i * 0.019f), new Vector3(W - 0.02f, 0.0002f, 0.0006f), rule);
            ln.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        // the entries, in different hands (colours and sizes), the last in a blue ballpoint
        var pencil = new Color(0.3f, 0.3f, 0.3f); var black = new Color(0.1f, 0.1f, 0.1f);
        Printed(root, "14.3.98", new Vector3(cols[0], 0f, 0.078f), 0.0055f, pencil);
        Printed(root, "R. Joshi", new Vector3(cols[1], 0f, 0.078f), 0.0058f, pencil);
        Printed(root, "Audit", new Vector3(cols[2], 0f, 0.078f), 0.0055f, pencil);
        Printed(root, "R.J.", new Vector3(cols[3], 0f, 0.078f), 0.006f, pencil);
        Printed(root, "2.7.2011", new Vector3(cols[0], 0f, 0.059f), 0.0055f, black);
        Printed(root, "M. Iyer", new Vector3(cols[1], 0f, 0.059f), 0.0058f, black);
        Printed(root, "Court copy", new Vector3(cols[2], 0f, 0.059f), 0.0052f, black);
        Printed(root, "M. Iyer", new Vector3(cols[3], 0f, 0.059f), 0.0058f, black);
        Printed(root, "8 Nov", new Vector3(cols[0], 0f, 0.040f), 0.006f, Ink);
        Printed(root, "S. REDDY", new Vector3(cols[1], 0f, 0.040f), 0.0062f, Ink);
        Printed(root, "-", new Vector3(cols[2] + 0.01f, 0f, 0.040f), 0.006f, Ink);
        Signature(root, new Vector3(cols[3] + 0.021f, 0.0016f, 0.0405f), Quaternion.Euler(90f, 0f, 0f), 0.052f);
        return root;
    }

    /// <summary>Her Crime Branch identity card (photo side up): 8.6 x 5.4 cm.</summary>
    public static Transform IdCard(Transform parent, Vector3 pos, float yaw)
    {
        var root = new GameObject("ReddyIdCard").transform;
        root.SetParent(parent, false); root.position = pos; root.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (cardMat == null) cardMat = GameKit.Lit(new Color(0.86f, 0.85f, 0.8f), 0.55f);
        var card = GameKit.Box(root, "CardBody", Vector3.zero, new Vector3(0.086f, 0.0008f, 0.054f), cardMat);
        card.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
        var band = GameKit.Box(root, "CardBand", new Vector3(0f, 0.0005f, 0.019f), new Vector3(0.086f, 0.0002f, 0.014f), GameKit.Lit(new Color(0.14f, 0.2f, 0.42f), 0.4f));
        band.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        var photo = GameKit.Box(root, "CardPhoto", new Vector3(-0.026f, 0.0005f, -0.006f), new Vector3(0.022f, 0.0002f, 0.028f), GameKit.Lit(new Color(0.36f, 0.3f, 0.27f), 0.3f));
        photo.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        Printed(root, "CHANDNIPUR POLICE - CRIME BRANCH", new Vector3(0f, 0f, 0.019f), 0.0036f, new Color(0.95f, 0.93f, 0.85f), TextAnchor.MiddleCenter);
        Printed(root, "SONAL REDDY", new Vector3(-0.01f, 0f, 0.002f), 0.0048f, Print);
        Printed(root, "Inspector", new Vector3(-0.01f, 0f, -0.008f), 0.0036f, Print);
        Signature(root, new Vector3(0.016f, 0.0009f, -0.019f), Quaternion.Euler(90f, 0f, 0f), 0.03f);
        // the back: a plain magnetic strip, for when she turns it over
        var back = GameKit.Box(root, "CardStrip", new Vector3(0f, -0.0006f, 0.012f), new Vector3(0.086f, 0.0002f, 0.011f), GameKit.Lit(new Color(0.05f, 0.05f, 0.06f), 0.6f));
        back.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        return root;
    }

    /// <summary>Priya's newspaper cuttings, the ones joined with red string on her wall: three overlapping cuttings.</summary>
    public static Transform Cuttings(Transform parent, Vector3 pos, float yaw)
    {
        var root = new GameObject("NewspaperCuttings").transform;
        root.SetParent(parent, false); root.position = pos; root.rotation = Quaternion.Euler(0f, yaw, 0f);
        var news = ProcTex.Mat(ProcTex.Get("paper"), new Color(0.82f, 0.8f, 0.72f), new Vector2(0.6f, 0.6f), 0.1f, 0.25f);
        Vector3[] at = { new Vector3(-0.03f, 0f, 0.02f), new Vector3(0.035f, 0.0013f, -0.01f), new Vector3(-0.005f, 0.0026f, -0.035f) };
        Vector2[] sz = { new Vector2(0.09f, 0.12f), new Vector2(0.1f, 0.08f), new Vector2(0.11f, 0.06f) };
        float[] rot = { -8f, 11f, -3f };
        string[] head = { "500 FAMILIES FACE EVICTION", "RENEWAL PROJECT CLEARED", "BASTI NAGAR: WHO OWNS THE LAND?" };
        for (int i = 0; i < 3; i++)
        {
            var c = new GameObject("Cutting").transform; c.SetParent(root, false); c.localPosition = at[i]; c.localRotation = Quaternion.Euler(0f, rot[i], 0f);
            Sheet(c, "CuttingPaper", Vector3.zero, sz[i], news);
            Printed(c, head[i], new Vector3(0f, 0f, sz[i].y * 0.36f), 0.0048f, Print, TextAnchor.MiddleCenter);
            var grey = GameKit.Lit(new Color(0.45f, 0.44f, 0.42f), 0.1f);
            for (int l = 0; l < 5; l++)
            {
                var ln = GameKit.Box(c, "Column", new Vector3(0f, 0.0007f, sz[i].y * 0.2f - l * sz[i].y * 0.12f), new Vector3(sz[i].x * 0.82f, 0.0002f, 0.0035f), grey);
                ln.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }
        // the red string that joined them on her wall, still knotted round a pin
        var red = GameKit.Lit(new Color(0.6f, 0.05f, 0.04f), 0.2f);
        var s1 = GameKit.Box(root, "RedString", new Vector3(0.002f, 0.0035f, 0.005f), new Vector3(0.075f, 0.0012f, 0.0012f), red);
        s1.transform.localRotation = Quaternion.Euler(0f, 24f, 0f);
        return root;
    }

    /// <summary>A few printed pages in a clear sleeve: Arjun's copies of Priya's files (the 1965 order beside the 1942 deed).</summary>
    public static Transform Printouts(Transform parent, Vector3 pos, float yaw)
    {
        var root = new GameObject("PriyasFiles").transform;
        root.SetParent(parent, false); root.position = pos; root.rotation = Quaternion.Euler(0f, yaw, 0f);
        var white = ProcTex.Mat(ProcTex.Get("paper"), new Color(0.93f, 0.92f, 0.88f), new Vector2(1f, 1.4f), 0.12f, 0.25f);
        for (int i = 0; i < 3; i++)
        {
            var p = Sheet(root, "Page", new Vector3(0.004f * i, 0.0011f * i, -0.003f * i), new Vector2(0.21f, 0.29f), white);
            p.transform.localRotation = Quaternion.Euler(0f, i * 2.5f, 0f);
        }
        Printed(root, "ACQUISITION ORDER 1965  -  copy", new Vector3(-0.0f, 0.0036f, 0.118f), 0.0058f, Print, TextAnchor.MiddleCenter);
        Printed(root, "stamp: wrong district office", new Vector3(0.0f, 0.0036f, 0.06f), 0.0052f, new Color(0.6f, 0.1f, 0.08f), TextAnchor.MiddleCenter);
        Printed(root, "signature inconsistent  (P.K.)", new Vector3(0.0f, 0.0036f, 0.045f), 0.0052f, new Color(0.6f, 0.1f, 0.08f), TextAnchor.MiddleCenter);
        var grey = GameKit.Lit(new Color(0.45f, 0.44f, 0.42f), 0.1f);
        for (int l = 0; l < 9; l++)
        {
            var ln = GameKit.Box(root, "Line", new Vector3(0f, 0.0038f, 0.02f - l * 0.017f), new Vector3(0.16f, 0.0002f, 0.0032f), grey);
            ln.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        return root;
    }

    /// <summary>The original 1942 deed: a large aged sheet, its ribbon and red seal, inside a Crime Branch evidence sleeve.</summary>
    public static Transform OldDeed(Transform parent, Vector3 pos, float yaw)
    {
        var root = new GameObject("Deed1942").transform;
        root.SetParent(parent, false); root.position = pos; root.rotation = Quaternion.Euler(0f, yaw, 0f);
        var aged = ProcTex.Mat(ProcTex.Get("paper"), new Color(0.78f, 0.66f, 0.45f), new Vector2(1.4f, 1.8f), 0.1f, 0.3f);
        Sheet(root, "DeedPaper", Vector3.zero, new Vector2(0.26f, 0.36f), aged);
        Printed(root, "SANAD", new Vector3(0f, 0f, 0.15f), 0.012f, new Color(0.25f, 0.12f, 0.06f), TextAnchor.MiddleCenter);
        Printed(root, "Basti Nagar, to its residents, in perpetuity", new Vector3(0f, 0f, 0.128f), 0.0058f, new Color(0.25f, 0.12f, 0.06f), TextAnchor.MiddleCenter);
        Printed(root, "Chandnipur State  -  1942", new Vector3(0f, 0f, 0.112f), 0.0055f, new Color(0.25f, 0.12f, 0.06f), TextAnchor.MiddleCenter);
        var brown = GameKit.Lit(new Color(0.4f, 0.28f, 0.16f), 0.1f);
        for (int l = 0; l < 10; l++)
        {
            var ln = GameKit.Box(root, "DeedLine", new Vector3(0f, 0.0007f, 0.09f - l * 0.018f), new Vector3(0.2f, 0.0002f, 0.0028f), brown);
            ln.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        var seal = GameKit.Cyl(root, "DeedSeal", new Vector3(0.07f, 0.003f, -0.13f), 0.028f, 0.006f, GameKit.Lit(new Color(0.55f, 0.06f, 0.05f), 0.5f));
        seal.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
        GameKit.Box(root, "DeedRibbon", new Vector3(0.07f, 0.0016f, -0.16f), new Vector3(0.012f, 0.001f, 0.08f), GameKit.Lit(new Color(0.6f, 0.15f, 0.12f), 0.3f));
        // the clear sleeve and its evidence label
        var sleeve = GameKit.Box(root, "EvidenceSleeve", new Vector3(0f, 0.0045f, 0f), new Vector3(0.28f, 0.0006f, 0.38f), GameKit.Lit(new Color(0.85f, 0.9f, 0.92f), 0.9f));
        sleeve.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        GameKit.Box(root, "EvidenceLabel", new Vector3(-0.08f, 0.0052f, -0.16f), new Vector3(0.09f, 0.0004f, 0.035f), GameKit.Lit(new Color(0.95f, 0.85f, 0.2f), 0.3f));
        Printed(root, "CRIME BRANCH - EXHIBIT 1", new Vector3(-0.08f, 0.0042f, -0.16f), 0.0038f, Print, TextAnchor.MiddleCenter);
        return root;
    }

    /// <summary>A printed email: the Deputy Commissioner's reply about the Basti Nagar transfer, as Rohan printed it.</summary>
    public static Transform EmailPrintout(Transform parent, Vector3 pos, float yaw)
    {
        var root = new GameObject("EmailPrintout").transform;
        root.SetParent(parent, false); root.position = pos; root.rotation = Quaternion.Euler(0f, yaw, 0f);
        var white = ProcTex.Mat(ProcTex.Get("paper"), new Color(0.93f, 0.93f, 0.9f), new Vector2(1f, 1.4f), 0.12f, 0.25f);
        Sheet(root, "EmailPaper", Vector3.zero, new Vector2(0.21f, 0.29f), white);
        Printed(root, "From: Deputy Commissioner, Land Records", new Vector3(-0.095f, 0f, 0.125f), 0.0052f, Print);
        Printed(root, "To: Councilor M. Deshpande", new Vector3(-0.095f, 0f, 0.113f), 0.0052f, Print);
        Printed(root, "Subject: RE: Basti Nagar transfer", new Vector3(-0.095f, 0f, 0.101f), 0.0052f, Print);
        Printed(root, "\"Keep the 1965 order in the file -", new Vector3(-0.095f, 0f, 0.075f), 0.0055f, Print);
        Printed(root, "nobody will question a stamped", new Vector3(-0.095f, 0f, 0.064f), 0.0055f, Print);
        Printed(root, "government document.\"", new Vector3(-0.095f, 0f, 0.053f), 0.0055f, Print);
        var grey = GameKit.Lit(new Color(0.45f, 0.44f, 0.42f), 0.1f);
        for (int l = 0; l < 6; l++)
        {
            var ln = GameKit.Box(root, "Line", new Vector3(0f, 0.0007f, 0.03f - l * 0.017f), new Vector3(0.17f, 0.0002f, 0.003f), grey);
            ln.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        return root;
    }

    /// <summary>A page torn from Arjun's notebook with one line written on it.</summary>
    public static Transform NotebookPage(Transform parent, Vector3 pos, float yaw, string line1, string line2)
    {
        var root = new GameObject("NotebookPage").transform;
        root.SetParent(parent, false); root.position = pos; root.rotation = Quaternion.Euler(0f, yaw, 0f);
        var lined = ProcTex.Mat(ProcTex.Get("paper"), new Color(0.92f, 0.91f, 0.84f), new Vector2(0.6f, 0.8f), 0.12f, 0.25f);
        Sheet(root, "NotebookPaper", Vector3.zero, new Vector2(0.1f, 0.14f), lined);
        var blue = GameKit.Lit(new Color(0.55f, 0.62f, 0.78f), 0.1f);
        for (int l = 0; l < 7; l++)
        {
            var ln = GameKit.Box(root, "Ruling", new Vector3(0f, 0.0007f, 0.05f - l * 0.017f), new Vector3(0.094f, 0.0002f, 0.0005f), blue);
            ln.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        Printed(root, line1, new Vector3(-0.043f, 0f, 0.035f), 0.0068f, Ink);
        if (!string.IsNullOrEmpty(line2)) Printed(root, line2, new Vector3(-0.043f, 0f, 0.018f), 0.0058f, Ink);
        return root;
    }
}
