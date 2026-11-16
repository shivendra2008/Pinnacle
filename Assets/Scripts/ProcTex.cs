using System.Collections.Generic;
using UnityEngine;

/// <summary>A set of tileable PBR maps: albedo, tangent-space normal, and a mask (A = smoothness).</summary>
public class PbrSet
{
    public Texture2D albedo, normal, mask;
    public float scale = 1f;        // how many "procedural tiles" one repeat of this texture covers (photo scans cover more ground)
    public float gloss = 1f;        // multiplies the smoothness: scanned surfaces are dry and matte unless something makes them wet
}

/// <summary>
/// Generates tileable procedural PBR textures (wood, plaster, fabric, rug, asphalt, stone) at runtime,
/// so every surface in the game has normal-mapped micro detail and varying roughness without any asset files.
/// Albedo maps are "detail multipliers" (mean ~0.8) that are tinted by the material colour, except the rug.
/// </summary>
public static class ProcTex
{
    const int N = 256;
    static readonly Dictionary<string, PbrSet> cache = new Dictionary<string, PbrSet>();

    // ============================================================ noise
    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }

    static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }

    /// <summary>Tileable value noise. (x, y) in lattice units; the lattice repeats every px / py cells.</summary>
    static float VNoise(float x, float y, int px, int py, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        int xa = Mod(x0, px), xb = Mod(x0 + 1, px), ya = Mod(y0, py), yb = Mod(y0 + 1, py);
        float a = Hash(xa, ya, seed), b = Hash(xb, ya, seed), c = Hash(xa, yb, seed), d = Hash(xb, yb, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    /// <summary>Tileable fractal noise for u, v in [0,1).</summary>
    static float Fbm(float u, float v, int period, int octaves, int seed)
    {
        float sum = 0, amp = 0.5f, norm = 0;
        int p = period;
        for (int o = 0; o < octaves; o++)
        {
            sum += amp * VNoise(u * p, v * p, p, p, seed + o * 17);
            norm += amp; amp *= 0.5f; p *= 2;
        }
        return sum / norm;
    }

    // ===================================================== texture building
    static Texture2D MakeTex(Color32[] px, bool linear)
    {
        var t = new Texture2D(N, N, TextureFormat.RGBA32, true, linear);
        t.SetPixels32(px);
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Trilinear;
        t.anisoLevel = 8;
        t.Apply(true);
        return t;
    }

    static Color32 C(float r, float g, float b, float a = 1f)
    {
        return new Color32((byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(g) * 255f), (byte)(Mathf.Clamp01(b) * 255f), (byte)(Mathf.Clamp01(a) * 255f));
    }

    static PbrSet Build(System.Func<float, float, (Color col, float height, float smooth)> f, float bumpStrength)
    {
        var alb = new Color32[N * N];
        var mask = new Color32[N * N];
        var h = new float[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                var r = f(x / (float)N, y / (float)N);
                int i = y * N + x;
                alb[i] = C(r.col.r, r.col.g, r.col.b);
                mask[i] = C(0f, 1f, 0f, r.smooth);
                h[i] = r.height;
            }
        var nrm = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = h[y * N + Mod(x + 1, N)] - h[y * N + Mod(x - 1, N)];
                float dy = h[Mod(y + 1, N) * N + x] - h[Mod(y - 1, N) * N + x];
                var n = new Vector3(-dx * bumpStrength, -dy * bumpStrength, 1f).normalized;
                float r = n.x * 0.5f + 0.5f, g = n.y * 0.5f + 0.5f, b = n.z * 0.5f + 0.5f;
                nrm[y * N + x] = C(1f, g, b, r);   // Unity decodes x = r * a, so r must be 1 and the X component lives in alpha (DXT5nm layout)
            }
        return new PbrSet { albedo = MakeTex(alb, false), normal = MakeTex(nrm, true), mask = MakeTex(mask, true) };
    }

    public static PbrSet Get(string key)
    {
        PbrSet s;
        if (cache.TryGetValue(key, out s) && s.albedo != null) return s;
        s = Photo(key);
        if (s != null) { cache[key] = s; return s; }
        switch (key)
        {
            case "wood_floor": s = WoodFloor(); break;
            case "wood": s = Wood(); break;
            case "plaster": s = Plaster(0.35f); break;
            case "facade": s = Plaster(1f); break;
            case "fabric": s = Fabric(); break;
            case "rug": s = Rug(); break;
            case "asphalt": s = Asphalt(); break;
            case "stone": s = Stone(); break;
            default: s = Plaster(0.3f); break;
        }
        cache[key] = s;
        return s;
    }

    /// <summary>
    /// Photo-scanned materials (Poly Haven, CC0) in Resources/Textures: {key}_albedo, {key}_normal, {key}_mask.
    /// The albedo files were pre-processed into near-neutral detail maps, so the game's own surface colours still apply.
    /// Returns null when a set is not there, and the procedural texture is used instead.
    /// </summary>
    static PbrSet Photo(string key)
    {
        var alb = Resources.Load<Texture2D>("Textures/" + key + "_albedo");
        if (alb == null)
        {
            // new material names fall back to the nearest scanned set before falling back to procedural
            string alt = key == "brick" || key == "concrete" ? "facade" : key == "shutter" || key == "corrugated" || key == "metal" ? null : null;
            if (alt == null) return null;
            alb = Resources.Load<Texture2D>("Textures/" + alt + "_albedo"); key = alt;
            if (alb == null) return null;
        }
        var set = new PbrSet { albedo = alb, normal = Resources.Load<Texture2D>("Textures/" + key + "_normal"), mask = Resources.Load<Texture2D>("Textures/" + key + "_mask") };
        set.scale = key == "wood" ? 1.3f : key == "asphalt" || key == "stone" ? 2.2f : 1.8f;
        set.gloss = key == "wood_floor" ? 0.55f : key == "stone" || key == "wood" ? 0.6f : 0.8f;
        foreach (var t in new[] { set.albedo, set.normal, set.mask })
            if (t != null) { t.wrapMode = TextureWrapMode.Repeat; t.anisoLevel = 8; }
        return set;
    }

    // ======================================================== the textures
    static PbrSet WoodFloor()
    {
        const int planks = 6;
        return Build((u, v) =>
        {
            int pi = Mathf.Min(planks - 1, Mathf.FloorToInt(u * planks));
            float pu = u * planks - pi;
            float off = Hash(pi, 7, 3);
            float vv = Mathf.Repeat(v + off, 1f);
            int seg = vv < 0.5f ? 0 : 1;                      // two boards end-to-end per tile
            float segPos = (vv * 2f) % 1f;
            float tone = 0.74f + 0.22f * Hash(pi, seg, 9);
            float grain = VNoise(u * 56f, v * 3f, 56, 3, 5) * 0.6f + VNoise(u * 120f, v * 6f, 120, 6, 8) * 0.4f;
            float rings = 0.5f + 0.5f * Mathf.Sin((pu + Fbm(u, v, 3, 3, 4) * 0.6f) * 40f);
            float c = tone * (0.80f + 0.22f * grain + 0.06f * rings);
            float edge = Mathf.Min(pu, 1f - pu);
            float joint = Mathf.Min(segPos, 1f - segPos) * 0.5f;       // distance to the butt joint (in tile units)
            float gap = Mathf.Clamp01(1f - Mathf.Min(edge / 0.035f, joint / 0.012f));
            c *= 1f - 0.65f * gap;
            float wear = Fbm(u, v, 4, 3, 21);
            float smooth = Mathf.Lerp(0.25f, 0.62f, wear) * (0.7f + 0.5f * Hash(pi, seg, 4)) * (1f - 0.7f * gap);
            float height = grain * 0.12f + (1f - gap) * 0.6f;
            return (new Color(c, c * 0.97f, c * 0.94f), height, smooth);
        }, 3f);
    }

    static PbrSet Wood()
    {
        return Build((u, v) =>
        {
            float grain = VNoise(u * 30f, v * 2f, 30, 2, 12) * 0.65f + VNoise(u * 90f, v * 4f, 90, 4, 13) * 0.35f;
            float rings = 0.5f + 0.5f * Mathf.Sin((u + Fbm(u, v, 3, 3, 14) * 0.5f) * 24f);
            float c = 0.72f + 0.22f * grain + 0.1f * rings;
            return (new Color(c, c * 0.97f, c * 0.93f), grain * 0.15f, Mathf.Lerp(0.3f, 0.55f, Fbm(u, v, 4, 2, 15)));
        }, 2f);
    }

    static PbrSet Plaster(float grime)
    {
        return Build((u, v) =>
        {
            float low = Fbm(u, v, 3, 4, 31);
            float fine = VNoise(u * 128f, v * 128f, 128, 128, 32);
            float streak = VNoise(u * 9f, v * 1f, 9, 1, 33);                     // long vertical streaks
            float streakMask = Mathf.SmoothStep(0.55f, 0.95f, streak) * grime;
            float spots = Mathf.SmoothStep(0.72f, 0.9f, Fbm(u, v, 8, 3, 34)) * grime;
            float c = 0.86f + (low - 0.5f) * 0.18f + (fine - 0.5f) * 0.06f;
            c *= 1f - 0.35f * streakMask - 0.25f * spots;
            float peel = 0f, brickRelief = 0f;
            if (grime > 0.9f)                                     // old Indian facades: paint flaking off to bare brick
            {
                peel = Mathf.SmoothStep(0.62f, 0.7f, Fbm(u, v, 4, 4, 35));
                float by = v * 16f, bx = u * 8f + (Mathf.FloorToInt(by) & 1) * 0.5f;
                bool mortar = Mathf.Repeat(bx, 1f) < 0.08f || Mathf.Repeat(by, 1f) < 0.14f;
                float brick = mortar ? 0.55f : 0.78f;
                c = Mathf.Lerp(c, brick * (0.9f + 0.1f * fine), peel);
                brickRelief = peel * (mortar ? -0.5f : 0.15f);
            }
            float smooth = 0.08f + 0.1f * (1f - low) + 0.12f * streakMask;      // damp = shinier
            return (new Color(c, c * 0.985f, c * 0.96f), fine * 0.35f + low * 0.25f + brickRelief, smooth * (1f - 0.5f * peel));
        }, 2.2f);
    }

    static PbrSet Fabric()
    {
        return Build((u, v) =>
        {
            const int threads = 40;
            float warp = 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 2f * threads);
            float weft = 0.5f + 0.5f * Mathf.Sin(v * Mathf.PI * 2f * threads);
            bool over = ((Mathf.FloorToInt(u * threads) + Mathf.FloorToInt(v * threads)) & 1) == 0;
            float thread = over ? warp : weft;
            float fuzz = VNoise(u * 160f, v * 160f, 160, 160, 41);
            float c = 0.72f + 0.2f * thread + 0.08f * fuzz;
            return (new Color(c, c, c), thread * 0.8f + fuzz * 0.2f, 0.1f + 0.08f * fuzz);
        }, 2.4f);
    }

    static PbrSet Rug()
    {
        Color maroon = new Color(0.40f, 0.07f, 0.09f), gold = new Color(0.78f, 0.58f, 0.2f),
              navy = new Color(0.08f, 0.1f, 0.22f), cream = new Color(0.86f, 0.8f, 0.66f);
        return Build((u, v) =>
        {
            float x = u - 0.5f, y = v - 0.5f;
            float r = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
            float dia = Mathf.Abs(x) + Mathf.Abs(y);
            Color col;
            if (r > 0.475f) col = cream;                                             // fringe line
            else if (r > 0.44f) col = gold;
            else if (r > 0.37f)
            {
                float t = Mathf.Sin((x + y) * 70f) * Mathf.Sin((x - y) * 70f);        // diamond border pattern
                col = t > 0f ? maroon : navy;
            }
            else if (r > 0.345f) col = gold;
            else
            {
                col = Mathf.Abs(Mathf.Sin(dia * 38f)) > 0.8f ? gold : (dia < 0.25f ? navy : maroon);   // medallion
                if (dia < 0.06f) col = cream;
            }
            const int threads = 90;
            float warp = 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 2f * threads);
            float weft = 0.5f + 0.5f * Mathf.Sin(v * Mathf.PI * 2f * threads);
            float weave = 0.82f + 0.18f * (((Mathf.FloorToInt(u * threads) + Mathf.FloorToInt(v * threads)) & 1) == 0 ? warp : weft);
            float wearN = 0.9f + 0.1f * VNoise(u * 60f, v * 60f, 60, 60, 51);
            return (col * weave * wearN, weave * 0.8f, 0.08f);
        }, 2f);
    }

    static PbrSet Asphalt()
    {
        return Build((u, v) =>
        {
            float grit = VNoise(u * 200f, v * 200f, 200, 200, 61) * 0.6f + VNoise(u * 80f, v * 80f, 80, 80, 62) * 0.4f;
            float patches = Fbm(u, v, 3, 4, 63);
            float c = 0.62f + 0.3f * grit - 0.1f * patches;
            float wet = Mathf.SmoothStep(0.45f, 0.62f, patches);                    // wet patches mirror the neon
            float smooth = Mathf.Lerp(0.18f, 0.92f, wet) * (1f - 0.35f * grit);
            return (new Color(c, c, c * 1.03f), grit * 0.7f, smooth);
        }, 3.2f);
    }

    static PbrSet Stone()
    {
        const int cells = 4;
        return Build((u, v) =>
        {
            int ci = Mathf.FloorToInt(u * cells), cj = Mathf.FloorToInt(v * cells);
            float lu = u * cells - ci, lv = v * cells - cj;
            float edge = Mathf.Min(Mathf.Min(lu, 1f - lu), Mathf.Min(lv, 1f - lv));
            float grout = Mathf.Clamp01(1f - edge / 0.04f);
            float tone = 0.72f + 0.22f * Hash(ci, cj, 71);
            float grain = VNoise(u * 140f, v * 140f, 140, 140, 72);
            float c = tone * (0.85f + 0.2f * grain) * (1f - 0.6f * grout);
            float smooth = Mathf.Lerp(0.2f, 0.55f, Hash(ci, cj, 73)) * (1f - 0.7f * grout);
            return (new Color(c, c, c * 0.98f), (1f - grout) * 0.7f + grain * 0.1f, smooth);
        }, 3f);
    }

    // ============================================================ material
    static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();

    public static Material Mat(PbrSet s, Color tint, Vector2 tiling, float smoothness = 1f, float bump = 1f)
    {
        string ck = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(s.albedo) + "|" + ColorUtility.ToHtmlStringRGB(tint) + "|" + Mathf.RoundToInt(tiling.x * 20f) + "," + Mathf.RoundToInt(tiling.y * 20f) + "|" + Mathf.RoundToInt(smoothness * 20f) + "|" + Mathf.RoundToInt(bump * 10f);
        Material hit;
        if (matCache.TryGetValue(ck, out hit) && hit != null) return hit;
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh);
        matCache[ck] = m;
        m.color = tint;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
        if (s.scale > 0f && s.scale != 1f) tiling /= s.scale;
        if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", s.albedo); m.SetTextureScale("_BaseMap", tiling); }
        else { m.mainTexture = s.albedo; m.mainTextureScale = tiling; }
        if (m.HasProperty("_BumpMap") && s.normal != null)
        {
            m.SetTexture("_BumpMap", s.normal);
            m.SetFloat("_BumpScale", bump);
            m.EnableKeyword("_NORMALMAP");
        }
        if (m.HasProperty("_MetallicGlossMap") && s.mask != null)
        {
            m.SetTexture("_MetallicGlossMap", s.mask);
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", s.mask != null ? Mathf.Clamp01(smoothness * s.gloss) : Mathf.Min(smoothness, 0.3f));
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        return m;
    }
}
