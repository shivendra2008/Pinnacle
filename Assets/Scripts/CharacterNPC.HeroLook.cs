using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The hero's own materials (the Curator only; other characters keep the shared ones in CharacterNPC.RbMaterial).
///  - Head: the model has one material for skin, lips and eyes, so everything had the same sheen (matte eyes with no
///    catch-light, plastic-looking skin). A smoothness map is made from the mesh itself: the eyeballs (vertices the
///    eye bones own) are wet, the lips a little glossy, the nose and the scalp have a faint sheen, the rest of the
///    skin is soft with a fine variation, never one uniform gloss.
///  - Body: the costume tint multiplies the whole body texture, and the hands are on that texture too, so his hands
///    took the suit's colour. The tint is baked into a copy of the texture everywhere except the hands (vertices the
///    hand and finger bones own), which keep the skin.
/// Regions come from the skin weights, drawn into texture space through the mesh's UVs.
/// </summary>
public partial class CharacterNPC
{
    static readonly Dictionary<string, Material> heroMats = new Dictionary<string, Material>();

    void HeroMaterials()
    {
        if (rbRoot == null) return;
        foreach (var smr in rbRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mesh = smr.sharedMesh;
            if (mesh == null || !mesh.isReadable) continue;
            var mats = smr.sharedMaterials;
            var bones = smr.bones;
            var bw = mesh.boneWeights; var uv = mesh.uv;
            if (uv == null || uv.Length != mesh.vertexCount) continue;
            for (int sub = 0; sub < mesh.subMeshCount && sub < mats.Length; sub++)
            {
                var m = mats[sub]; if (m == null) continue;
                string key = style.rb + "/" + m.name;
                Material made;
                if (heroMats.TryGetValue(key, out made) && made != null) { mats[sub] = made; continue; }
                var tris = mesh.GetTriangles(sub);
                if (m.name.Contains("_head")) made = HeadMaterial(m, tris, uv, bw, bones);
                else if (m.name.Contains("_body")) made = BodyMaterial(m, tris, uv, bw, bones);
                if (made != null) { heroMats[key] = made; mats[sub] = made; }
            }
            smr.sharedMaterials = mats;
        }
    }

    static float Owned(BoneWeight b, HashSet<int> set)
    {
        float w = 0f;
        if (set.Contains(b.boneIndex0)) w += b.weight0; if (set.Contains(b.boneIndex1)) w += b.weight1;
        if (set.Contains(b.boneIndex2)) w += b.weight2; if (set.Contains(b.boneIndex3)) w += b.weight3;
        return w;
    }

    static HashSet<int> BoneSet(Transform[] bones, System.Func<string, bool> pick)
    {
        var s = new HashSet<int>();
        for (int i = 0; i < bones.Length; i++) if (bones[i] != null && pick(bones[i].name)) s.Add(i);
        return s;
    }

    /// <summary>Draws every triangle whose three corners satisfy `own` into a mask (UV space) with value v (max-blend).</summary>
    static void Paint(float[] mask, int size, int[] tris, Vector2[] uv, System.Func<int, float> own, float thr, float v)
    {
        for (int t = 0; t + 2 < tris.Length; t += 3)
        {
            int a = tris[t], b = tris[t + 1], c = tris[t + 2];
            float w = Mathf.Min(own(a), Mathf.Min(own(b), own(c)));
            if (w < thr) continue;
            Vector2 pa = uv[a] * size, pb = uv[b] * size, pc = uv[c] * size;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.x, Mathf.Min(pb.x, pc.x))) - 1), x1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(pa.x, Mathf.Max(pb.x, pc.x))) + 1);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.y, Mathf.Min(pb.y, pc.y))) - 1), y1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(pa.y, Mathf.Max(pb.y, pc.y))) + 1);
            float den = (pb.y - pc.y) * (pa.x - pc.x) + (pc.x - pb.x) * (pa.y - pc.y);
            if (Mathf.Abs(den) < 1e-6f) continue;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float l1 = ((pb.y - pc.y) * (px - pc.x) + (pc.x - pb.x) * (py - pc.y)) / den;
                    float l2 = ((pc.y - pa.y) * (px - pc.x) + (pa.x - pc.x) * (py - pc.y)) / den;
                    float l3 = 1f - l1 - l2;
                    if (l1 < -0.02f || l2 < -0.02f || l3 < -0.02f) continue;          // a little dilation closes the seams
                    int i = y * size + x;
                    if (v > mask[i]) mask[i] = v;
                }
        }
    }

    static Texture2D Readable(Texture src, int size)
    {
        var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(src, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var t = new Texture2D(size, size, TextureFormat.RGBA32, true, false);
        t.ReadPixels(new Rect(0, 0, size, size), 0, 0); t.Apply(true);
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        return t;
    }

    Material HeadMaterial(Material src, int[] tris, Vector2[] uv, BoneWeight[] bw, Transform[] bones)
    {
        const int S = 512;
        var eyes = BoneSet(bones, n => n.EndsWith("LEye") || n.EndsWith("REye"));
        var lips = BoneSet(bones, n => n.Contains("Lip") || n.Contains("lip") || n.Contains("MouthBottom") || n.Contains("MouthCorner"));
        var nose = BoneSet(bones, n => n.EndsWith("MNose") || n.Contains("MiddleEyebrow"));
        var lids = BoneSet(bones, n => n.Contains("EyeBlink"));
        var head = BoneSet(bones, n => n.EndsWith(" Head"));
        var mask = new float[S * S];
        for (int i = 0; i < mask.Length; i++) mask[i] = 0.36f;                       // soft skin
        Paint(mask, S, tris, uv, v => Owned(bw[v], head), 0.95f, 0.41f);              // the scalp and the brow: a faint sheen
        Paint(mask, S, tris, uv, v => Owned(bw[v], nose), 0.25f, 0.46f);
        Paint(mask, S, tris, uv, v => Owned(bw[v], lids), 0.2f, 0.47f);
        Paint(mask, S, tris, uv, v => Owned(bw[v], lips), 0.3f, 0.54f);
        Paint(mask, S, tris, uv, v => Owned(bw[v], eyes), 0.6f, 0.94f);               // wet eyes: they catch the candle
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, true, true) { name = "CuratorHeadSmoothness", wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float m = mask[y * S + x];
                if (m < 0.9f) m += (Mathf.PerlinNoise(x * 0.11f, y * 0.11f) - 0.5f) * 0.06f + (Mathf.PerlinNoise(x * 0.5f + 7f, y * 0.5f) - 0.5f) * 0.03f;   // skin is never one gloss
                px[y * S + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(Mathf.Clamp01(m) * 255f));
            }
        tex.SetPixels32(px); tex.Apply(true);
        var m2 = new Material(src) { name = src.name + "_hero" };
        m2.SetTexture("_MetallicGlossMap", tex);
        m2.EnableKeyword("_METALLICSPECGLOSSMAP");
        m2.SetFloat("_Smoothness", 1f);
        m2.SetFloat("_SmoothnessTextureChannel", 0f);
        m2.SetFloat("_Metallic", 0f);
        if (m2.HasProperty("_SpecularHighlights")) m2.SetFloat("_SpecularHighlights", 1f);
        if (m2.HasProperty("_EnvironmentReflections")) m2.SetFloat("_EnvironmentReflections", 1f);
        return m2;
    }

    Material BodyMaterial(Material src, int[] tris, Vector2[] uv, BoneWeight[] bw, Transform[] bones)
    {
        var baseMap = src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : src.mainTexture;
        if (baseMap == null) return null;
        Color tint = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : src.color;
        if (Mathf.Abs(tint.r - 1f) + Mathf.Abs(tint.g - 1f) + Mathf.Abs(tint.b - 1f) < 0.02f) return null;     // nothing to keep off the hands
        int S = Mathf.Min(1024, baseMap.width);
        var hands = BoneSet(bones, n => n.Contains(" Hand") || n.Contains(" Finger"));
        var mask = new float[S * S];
        Paint(mask, S, tris, uv, v => Owned(bw[v], hands), 0.5f, 1f);
        // soften the edge of the region so the wrist does not show a line
        var soft = new float[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float sum = 0f; int c = 0;
                for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++)
                {
                    int xx = x + dx, yy = y + dy; if (xx < 0 || yy < 0 || xx >= S || yy >= S) continue;
                    sum += mask[yy * S + xx]; c++;
                }
                soft[y * S + x] = sum / c;
            }
        var img = Readable(baseMap, S);
        var px = img.GetPixels32();
        for (int i = 0; i < px.Length; i++)
        {
            float k = soft[i];
            Color c = px[i];
            Color clothed = new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a);
            px[i] = Color.Lerp(clothed, c, k);
        }
        img.SetPixels32(px); img.Apply(true);
        img.name = "CuratorBody";
        var m2 = new Material(src) { name = src.name + "_hero" };
        if (m2.HasProperty("_BaseMap")) m2.SetTexture("_BaseMap", img);
        m2.mainTexture = img;
        if (m2.HasProperty("_BaseColor")) m2.SetColor("_BaseColor", Color.white);
        m2.color = Color.white;
        return m2;
    }
}
