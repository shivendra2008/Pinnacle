using System.Collections.Generic;
using UnityEngine;

/// <summary>Small helpers for building scenery out of primitives (used by the Act II market).</summary>
public static class GameKit
{
    static readonly Dictionary<string, Material> litCache = new Dictionary<string, Material>();

    /// <summary>Cached lit material (hundreds of props share a few dozen materials).</summary>
    public static Material Lit(Color c, float smooth = 0.25f, float metal = 0f)
    {
        string key = Mathf.RoundToInt(c.r * 100) + "," + Mathf.RoundToInt(c.g * 100) + "," + Mathf.RoundToInt(c.b * 100) + "," + Mathf.RoundToInt(smooth * 50) + "," + Mathf.RoundToInt(metal * 10);
        Material cached;
        if (litCache.TryGetValue(key, out cached) && cached != null) return cached;
        var m = MakeLit(c, smooth, metal);
        litCache[key] = m;
        return m;
    }

    static Material MakeLit(Color c, float smooth, float metal)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
        return m;
    }

    public static Material Unlit(Color c)
    {
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        var m = new Material(sh);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        return m;
    }

    /// <summary>A lit material that also glows (picked up by bloom).</summary>
    public static Material Glow(Color baseColor, Color emission, float intensity = 2f)
    {
        var m = MakeLit(baseColor, 0.3f, 0f);
        m.EnableKeyword("_EMISSION");
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission * intensity);
        return m;
    }

    static GameObject Make(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, bool collider)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        var rend = go.GetComponent<Renderer>();
        rend.sharedMaterial = mat;
        // tiny props don't need to cast shadows (keeps the shadow pass cheap)
        if (Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)) < 0.3f) rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (!collider) Object.Destroy(go.GetComponent<Collider>());
        return go;
    }

    public static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool collider = false)
    { return Make(PrimitiveType.Cube, parent, name, pos, size, mat, collider); }

    public static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Color c, bool collider = false, float smooth = 0.25f)
    { return Make(PrimitiveType.Cube, parent, name, pos, size, Lit(c, smooth), collider); }

    /// <summary>Cylinder given radius and full height (Unity's cylinder is 2 tall, so we halve it).</summary>
    public static GameObject Cyl(Transform parent, string name, Vector3 pos, float radius, float height, Material mat, bool collider = false)
    { return Make(PrimitiveType.Cylinder, parent, name, pos, new Vector3(radius * 2f, height / 2f, radius * 2f), mat, collider); }

    public static GameObject Sphere(Transform parent, string name, Vector3 pos, float diameter, Material mat)
    { return Make(PrimitiveType.Sphere, parent, name, pos, Vector3.one * diameter, mat, false); }

    /// <summary>Procedural cone (spice piles, fruit pyramids, cloth umbrellas). Origin at the base centre.</summary>
    public static GameObject Cone(Transform parent, string name, Vector3 pos, float radius, float height, Material mat, int sides = 14)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var mesh = new Mesh { name = "Cone" };
        var v = new Vector3[sides + 2];
        var t = new int[sides * 6];
        v[0] = new Vector3(0, height, 0);
        v[sides + 1] = Vector3.zero;
        for (int i = 0; i < sides; i++)
        {
            float a = i / (float)sides * Mathf.PI * 2f;
            v[i + 1] = new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius);
        }
        for (int i = 0; i < sides; i++)
        {
            int n = (i + 1) % sides;
            t[i * 6 + 0] = 0; t[i * 6 + 1] = n + 1; t[i * 6 + 2] = i + 1;            // side
            t[i * 6 + 3] = sides + 1; t[i * 6 + 4] = i + 1; t[i * 6 + 5] = n + 1;    // base
        }
        mesh.vertices = v; mesh.triangles = t;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        if (radius < 0.15f) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    static Font hindi;
    static bool hindiTried;
    /// <summary>A macOS font that can draw Devanagari (null if none is installed).</summary>
    public static Font HindiFont()
    {
        if (!hindiTried)
        {
            hindiTried = true;
            try
            {
                var f = Font.CreateDynamicFontFromOSFont(new[] { "Kohinoor Devanagari", "Devanagari Sangam MN", "Noto Sans Devanagari", "ITF Devanagari", "Mangal" }, 64);
                if (f != null && f.HasCharacter('\u091a')) hindi = f;
            }
            catch { }
        }
        return hindi;
    }

    static string Slug(string s)
    {
        var sb = new System.Text.StringBuilder();
        bool us = false;
        foreach (char ch in s.ToLowerInvariant())
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9')) { sb.Append(ch); us = false; }
            else if (!us) { sb.Append('_'); us = true; }
        }
        return sb.ToString().Trim('_');
    }

    /// <summary>
    /// Shop sign. Uses the pre-rendered bilingual Hindi + English image from Resources/Signs (correct Devanagari shaping,
    /// made by Tools/make_signs.swift); falls back to plain English text if the image is missing.
    /// maxHeight = letter height of one line; the image is shrunk to fit maxWidth.
    /// </summary>
    public static GameObject Sign(Transform parent, string english, string hindiText, Vector3 pos, Quaternion rot, float maxHeight, float maxWidth, Color c)
    {
        var tex = Resources.Load<Texture2D>("Signs/" + Slug(english));
        var sh = Shader.Find("Sprites/Default");
        if (tex != null && sh != null)
        {
            float aspect = tex.width / (float)tex.height;
            float H = maxHeight * 2.3f, W = H * aspect;
            if (W > maxWidth) { H *= maxWidth / W; W = maxWidth; }
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Sign_" + english;
            Object.Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localPosition = pos;
            q.transform.localRotation = rot;
            q.transform.localScale = new Vector3(W, H, 1f);
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = new Material(sh) { mainTexture = tex, color = c };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return q;
        }
        float h = Mathf.Min(maxHeight, maxWidth / (Mathf.Max(1, english.Length) * 0.55f));
        return Text(parent, english, pos, rot, h, c).gameObject;
    }

    public static Light PointLight(Transform parent, Vector3 pos, Color c, float intensity, float range)
    {
        var go = new GameObject("Light");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    // ---------------------------------------------------------------- PBR surfaces
    /// <summary>Applies a procedural PBR set; tiling follows the object's two largest dimensions (thin axis = face normal).</summary>
    public static void Surface(GameObject go, string setKey, float metersPerTile, float smooth = 1f, float bump = 1f, Color? tint = null)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        Vector3 s = go.transform.localScale;
        float ax = Mathf.Abs(s.x), ay = Mathf.Abs(s.y), az = Mathf.Abs(s.z);
        Vector2 t;
        if (ax <= ay && ax <= az) t = new Vector2(az, ay);
        else if (az <= ax && az <= ay) t = new Vector2(ax, ay);
        else t = new Vector2(ax, az);
        Color c = tint ?? r.sharedMaterial.color;
        r.sharedMaterial = ProcTex.Mat(ProcTex.Get(setKey), c, t / metersPerTile, smooth, bump);
    }

    /// <summary>For tall boxes seen from the +/-X side (building fronts): u = Z extent, v = Y extent.</summary>
    public static void SurfaceZY(GameObject go, string setKey, float metersPerTile, Color tint, float smooth = 1f, float bump = 1f)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        Vector3 s = go.transform.localScale;
        r.sharedMaterial = ProcTex.Mat(ProcTex.Get(setKey), tint, new Vector2(Mathf.Abs(s.z), Mathf.Abs(s.y)) / metersPerTile, smooth, bump);
    }

    /// <summary>One texture repeat across the whole surface (rugs, signs).</summary>
    public static void SurfaceFit(GameObject go, string setKey, Color tint, float smooth = 1f, float bump = 1f)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        r.sharedMaterial = ProcTex.Mat(ProcTex.Get(setKey), tint, Vector2.one, smooth, bump);
    }

    static Font font;

    /// <summary>World-space text (signs). size = approximate letter height in metres. Faces -Z by default; pass the rotation you need.</summary>
    public static TextMesh Text(Transform parent, string text, Vector3 pos, Quaternion rot, float size, Color c)
    {
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var go = new GameObject("Sign_" + text);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.font = font;
        tm.fontSize = 64;
        tm.characterSize = size / 6.4f; // size ~= letter height in metres (fontSize 64)
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = c;
        var mr = go.GetComponent<MeshRenderer>();
        if (font != null) mr.sharedMaterial = font.material;
        return tm;
    }
}
