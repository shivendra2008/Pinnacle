using System.Collections.Generic;
using UnityEngine;

/// <summary>One cross-section of a lofted body: a rounded rectangle (super-ellipse) at position z.</summary>
public struct Sec
{
    public float z, y, hw, hh, n;
    /// <param name="z">position along the body</param><param name="y">centre height</param>
    /// <param name="hw">half width</param><param name="hh">half height</param>
    /// <param name="n">roundness: 2 = ellipse, 4 = soft box, 8 = nearly square</param>
    public Sec(float z, float y, float hw, float hh, float n = 4f) { this.z = z; this.y = y; this.hw = hw; this.hh = hh; this.n = n; }
}

/// <summary>
/// Smooth vehicle geometry made in code: bodies are "lofted" through a row of rounded cross-sections (like a hull),
/// wheels are turned on a lathe (rounded tyre + dished rim). This replaces the stacked-boxes vehicles.
/// </summary>
public static class VehicleKit
{
    static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

    public static Mesh Loft(Sec[] s, int ring = 28)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        int W = ring + 1;
        for (int i = 0; i < s.Length; i++)
            for (int k = 0; k <= ring; k++)
            {
                float a = k / (float)ring * Mathf.PI * 2f, c = Mathf.Cos(a), si = Mathf.Sin(a), e = 2f / Mathf.Max(2f, s[i].n);
                v.Add(new Vector3(Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e) * s[i].hw, Mathf.Sign(si) * Mathf.Pow(Mathf.Abs(si), e) * s[i].hh + s[i].y, s[i].z));
                uv.Add(new Vector2(k / (float)ring * 4f, s[i].z));
            }
        for (int i = 0; i < s.Length - 1; i++)
            for (int k = 0; k < ring; k++)
            {
                int a = i * W + k, b = a + 1, c = a + W, d = c + 1;
                t.Add(a); t.Add(c); t.Add(b); t.Add(b); t.Add(c); t.Add(d);
            }
        // make sure the skin faces outwards whichever way the sections were listed
        {
            Vector3 centre = new Vector3(0, s[0].y, s[0].z);
            Vector3 nrm = Vector3.Cross(v[t[1]] - v[t[0]], v[t[2]] - v[t[0]]);
            Vector3 mid = (v[t[0]] + v[t[1]] + v[t[2]]) / 3f - centre; mid.z = 0;
            if (Vector3.Dot(nrm, mid) < 0f) for (int i = 0; i < t.Count; i += 3) { int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; }
        }
        int skinVerts = v.Count;
        // flat end caps (their own vertices, so the rim stays crisp)
        for (int end = 0; end < 2; end++)
        {
            int i = end == 0 ? 0 : s.Length - 1;
            Vector3 outward = end == 0 ? Vector3.forward * Mathf.Sign(s[0].z - s[1].z) : Vector3.forward * Mathf.Sign(s[s.Length - 1].z - s[s.Length - 2].z);
            int centre = v.Count;
            v.Add(new Vector3(0, s[i].y, s[i].z)); uv.Add(new Vector2(0.5f, 0.5f));
            int first = v.Count;
            for (int k = 0; k <= ring; k++) { v.Add(v[i * W + k]); uv.Add(new Vector2(v[i * W + k].x, v[i * W + k].y)); }
            for (int k = 0; k < ring; k++)
            {
                int a = first + k, b = first + k + 1;
                Vector3 nrm = Vector3.Cross(v[a] - v[centre], v[b] - v[centre]);
                if (Vector3.Dot(nrm, outward) >= 0f) { t.Add(centre); t.Add(a); t.Add(b); }
                else { t.Add(centre); t.Add(b); t.Add(a); }
            }
        }
        var m = new Mesh { name = "Loft" };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
        m.RecalculateNormals();
        // weld the normals across the seam where the ring closes
        var nr = m.normals;
        for (int i = 0; i < s.Length; i++)
        {
            Vector3 avg = (nr[i * W] + nr[i * W + ring]).normalized;
            nr[i * W] = avg; nr[i * W + ring] = avg;
        }
        m.normals = nr;
        m.RecalculateBounds();
        m.RecalculateTangents();
        return m;
    }

    /// <summary>Surface of revolution around the X axis. Profile points are (x, radius).</summary>
    public static Mesh Lathe(Vector2[] profile, int seg = 28)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        int W = seg + 1;
        for (int i = 0; i < profile.Length; i++)
            for (int k = 0; k <= seg; k++)
            {
                float a = k / (float)seg * Mathf.PI * 2f;
                v.Add(new Vector3(profile[i].x, profile[i].y * Mathf.Cos(a), profile[i].y * Mathf.Sin(a)));
                uv.Add(new Vector2(k / (float)seg * 6f, i / (float)(profile.Length - 1)));
            }
        for (int i = 0; i < profile.Length - 1; i++)
            for (int k = 0; k < seg; k++)
            {
                int a = i * W + k, b = a + 1, c = a + W, d = c + 1;
                t.Add(a); t.Add(c); t.Add(b); t.Add(b); t.Add(c); t.Add(d);
            }
        // orient: find a triangle on the widest part and make it face away from the axis
        int best = 0; float bestR = -1f;
        for (int i = 0; i < t.Count; i += 3)
        {
            Vector3 mid = (v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3f;
            float r = new Vector2(mid.y, mid.z).magnitude;
            Vector3 nrm = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
            if (r > bestR && nrm.sqrMagnitude > 1e-10f && Mathf.Abs(nrm.normalized.x) < 0.5f) { bestR = r; best = i; }
        }
        {
            Vector3 mid = (v[t[best]] + v[t[best + 1]] + v[t[best + 2]]) / 3f; mid.x = 0;
            Vector3 nrm = Vector3.Cross(v[t[best + 1]] - v[t[best]], v[t[best + 2]] - v[t[best]]);
            if (Vector3.Dot(nrm, mid) < 0f) for (int i = 0; i < t.Count; i += 3) { int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; }
        }
        var m = new Mesh { name = "Lathe" };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
        m.RecalculateNormals();
        var nr = m.normals;
        for (int i = 0; i < profile.Length; i++)
        {
            Vector3 avg = (nr[i * W] + nr[i * W + seg]).normalized;
            nr[i * W] = avg; nr[i * W + seg] = avg;
        }
        m.normals = nr;
        m.RecalculateBounds();
        return m;
    }

    public static GameObject Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    /// <summary>A lofted body panel. Sections are in the parent's local space (z = length, y = height).</summary>
    public static GameObject Body(Transform parent, string name, Sec[] sections, Material mat, string cacheKey = null)
    {
        Mesh m = null;
        if (cacheKey != null) cache.TryGetValue(cacheKey, out m);
        if (m == null) { m = Loft(sections); if (cacheKey != null) cache[cacheKey] = m; }
        return Part(parent, name, m, mat, Vector3.zero);
    }

    static Material rubber, steel, hubDark;

    /// <summary>A wheel with a rounded tyre and a dished metal rim. The axle runs along the parent's local X.</summary>
    public static GameObject Wheel(Transform parent, Vector3 pos, float radius, float width, Material rim = null)
    {
        if (rubber == null)
        {
            rubber = GameKit.Lit(new Color(0.022f, 0.022f, 0.024f), 0.28f);
            steel = GameKit.Lit(new Color(0.5f, 0.51f, 0.53f), 0.78f, 0.9f);
            hubDark = GameKit.Lit(new Color(0.06f, 0.06f, 0.065f), 0.4f, 0.5f);
        }
        string key = "wheel_" + Mathf.RoundToInt(radius * 100) + "_" + Mathf.RoundToInt(width * 100);
        Mesh tyre, disc;
        if (!cache.TryGetValue(key + "t", out tyre) || tyre == null)
        {
            float R = radius, w = width / 2f;
            tyre = Lathe(new[]
            {
                new Vector2(-w * 0.92f, R * 0.70f), new Vector2(-w, R * 0.80f), new Vector2(-w, R * 0.92f), new Vector2(-w * 0.72f, R),
                new Vector2(w * 0.72f, R), new Vector2(w, R * 0.92f), new Vector2(w, R * 0.80f), new Vector2(w * 0.92f, R * 0.70f),
            });
            disc = Lathe(new[]
            {
                new Vector2(-w * 0.55f, 0f), new Vector2(-w * 0.55f, R * 0.16f), new Vector2(-w * 0.3f, R * 0.22f), new Vector2(-w * 0.35f, R * 0.60f),
                new Vector2(-w * 0.9f, R * 0.66f), new Vector2(-w * 0.9f, R * 0.71f), new Vector2(w * 0.9f, R * 0.71f), new Vector2(w * 0.9f, R * 0.66f),
                new Vector2(w * 0.35f, R * 0.60f), new Vector2(w * 0.3f, R * 0.22f), new Vector2(w * 0.55f, R * 0.16f), new Vector2(w * 0.55f, 0f),
            });
            cache[key + "t"] = tyre; cache[key + "d"] = disc;
        }
        else disc = cache[key + "d"];
        var root = new GameObject("Wheel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = pos;
        Part(root.transform, "Tyre", tyre, rubber, Vector3.zero);
        Part(root.transform, "Rim", disc, rim != null ? rim : steel, Vector3.zero);
        return root;
    }

    /// <summary>A sagging sheet (stall tarpaulin): w across, d deep, hanging lowest in the middle of the front edge. Two-sided.</summary>
    public static Mesh Tarp(float w, float d, float sag, int nx = 12, int nz = 7)
    {
        string key = "tarp_" + Mathf.RoundToInt(w * 100) + "_" + Mathf.RoundToInt(d * 100) + "_" + Mathf.RoundToInt(sag * 100);
        Mesh m;
        if (cache.TryGetValue(key, out m) && m != null) return m;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        for (int side = 0; side < 2; side++)
        {
            int o = v.Count;
            for (int z = 0; z <= nz; z++)
                for (int x = 0; x <= nx; x++)
                {
                    float u = x / (float)nx, vv = z / (float)nz;
                    float y = -sag * (1f - (2f * u - 1f) * (2f * u - 1f)) * (0.45f + 0.55f * vv)
                              - sag * 0.35f * (1f - (2f * vv - 1f) * (2f * vv - 1f))
                              + Mathf.Sin(u * 17f + vv * 5f) * 0.012f + Mathf.Sin(vv * 23f) * 0.006f;
                    v.Add(new Vector3((u - 0.5f) * w, y - side * 0.002f, (vv - 0.5f) * d));
                    uv.Add(new Vector2(u * w, vv * d));
                }
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    int a = o + z * (nx + 1) + x, b = a + 1, c = a + nx + 1, e = c + 1;
                    if (side == 0) { t.Add(a); t.Add(c); t.Add(b); t.Add(b); t.Add(c); t.Add(e); }
                    else { t.Add(a); t.Add(b); t.Add(c); t.Add(b); t.Add(e); t.Add(c); }
                }
        }
        m = new Mesh { name = "Tarp" };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
        m.RecalculateNormals(); m.RecalculateBounds(); m.RecalculateTangents();
        cache[key] = m;
        return m;
    }

    /// <summary>A stretched sphere (mudguards, fairings, lamp housings).</summary>
    public static GameObject Blob(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
    {
        var go = GameKit.Sphere(parent, name, pos, 1f, mat);
        go.transform.localScale = size;
        return go;
    }
}

/// <summary>Photo-scanned props (Poly Haven, CC0) from Resources/Props/{id}/{id}_1k.</summary>
public static class PropKit
{
    static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

    public static bool Has(string id) { return Load(id) != null; }

    static GameObject Load(string id)
    {
        GameObject p;
        if (prefabs.TryGetValue(id, out p)) return p;
        p = Resources.Load<GameObject>("Props/" + id + "/" + id + "_1k");
        prefabs[id] = p;
        return p;
    }

    /// <summary>Places a prop with its base at pos (parent space). Returns null if the model is not in the project.</summary>
    public static GameObject Place(Transform parent, string id, Vector3 pos, float yaw = 0f, float scale = 1f, Vector3? tilt = null)
    {
        var prefab = Load(id);
        if (prefab == null) return null;
        var go = Object.Instantiate(prefab, parent);
        go.name = id;
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(tilt ?? Vector3.zero);
        go.transform.localScale = Vector3.one * scale;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        return go;
    }
}
