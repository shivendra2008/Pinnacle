using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The Curator's Gallery, a hall outside time. The Curator's interludes between the acts and the Act V casket choice
/// both happen here, so it is built in one place.
/// The lighting is meant to be bright and unsettling at once: three candle chandeliers and rows of candles light the
/// aisle, the table and the bookcase clearly, and the polished floor reflects them. Outside those pools of light the
/// hall drops into red-black fog. Cold moonlight comes down from high windows, draped figures stand in the corners,
/// and portraits with faint red eyes hang behind the bookcase.
/// </summary>
public class CuratorGallery
{
    public Transform root;
    public Vector3 origin;
    public readonly List<GameObject> flames = new List<GameObject>();
    public readonly List<CandleFlicker> lights = new List<CandleFlicker>();   // every warm light (candles + chandeliers)
    public readonly List<Light> moonLights = new List<Light>();
    public readonly List<Transform> drapes = new List<Transform>();
    public StoryBookModel shelfBook;                // "Shadows of Chandni" on the shelf (null if not built)
    public Vector3 shelfBookPos;                    // its resting place (parent space)
    public const float ShelfFront = 8.7f;           // z of the bookcase's front (relative to origin)
    public static readonly Vector3 ShelfBookSlot = new Vector3(1.0f, 1.76f, ShelfFront + StoryBookModel.W / 2f);

    static Mesh cubeMesh;

    // ------------------------------------------------------------------ mood
    public static readonly Color AmbSky = new Color(0.36f, 0.25f, 0.23f), AmbEq = new Color(0.27f, 0.17f, 0.15f), AmbGround = new Color(0.09f, 0.05f, 0.05f);
    public static readonly Color FogColor = new Color(0.075f, 0.035f, 0.035f);
    public const float FogDensity = 0.03f;

    /// <summary>The gallery's ambient light and fog; level 0 = only the candles, 1 = the hall fully lit.</summary>
    public static void Mood(Camera cam, float level)
    {
        float k = Mathf.Lerp(0.12f, 1f, Mathf.Clamp01(level));
        CinematicLook.Ambient(AmbSky * k, AmbEq * k, AmbGround * k, Mathf.Lerp(0.3f, 0.9f, level));
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = FogColor * Mathf.Lerp(0.3f, 1f, level);
        RenderSettings.fogDensity = FogDensity;
        if (cam != null) CinematicLook.NoSky(cam, new Color(0.01f, 0.004f, 0.004f));
    }

    /// <summary>Sets every warm light and flame to a level (0 = all out, 1 = full).</summary>
    public void SetLights(float level)
    {
        foreach (var l in lights) if (l != null) l.level = level;
        foreach (var f in flames) if (f != null) f.SetActive(level > 0.02f);
    }

    // ------------------------------------------------------------------ build
    public static CuratorGallery Build(Transform parent, Vector3 o, bool withShelfBook, bool clearTableCentre)
    {
        var g = new CuratorGallery { root = parent, origin = o };
        g.BuildAll(withShelfBook, clearTableCentre);
        return g;
    }

    GameObject B(string n, Vector3 p, Vector3 s, Color c, bool col = false, float smooth = 0.25f, float metal = 0f)
    { return GameKit.Box(root, n, origin + p, s, GameKit.Lit(c, smooth, metal), col); }

    void BuildAll(bool withShelfBook, bool clearTableCentre)
    {
        var rnd = new System.Random(21);
        var wood = new Color(0.2f, 0.12f, 0.075f);
        var shelfWood = new Color(0.22f, 0.13f, 0.075f);

        // ---- the hall: 24 x 12 x 30, dark panelled wood and a polished floor that mirrors the candles
        Vector3 size = new Vector3(24f, 12f, 30f); float t = 0.25f;
        GameKit.Surface(B("Gallery_Floor", new Vector3(0, -0.05f, 0), new Vector3(size.x + 2 * t, 0.1f, size.z + 2 * t), new Color(0.17f, 0.1f, 0.07f), true, 0.75f), "wood_floor", 1.2f, 1.4f, 1f, new Color(0.17f, 0.1f, 0.07f));
        GameKit.Surface(B("Gallery_Ceiling", new Vector3(0, size.y + 0.05f, 0), new Vector3(size.x + 2 * t, 0.1f, size.z + 2 * t), wood * 0.6f, true), "wood", 2f);
        GameKit.Surface(B("Gallery_WallN", new Vector3(0, size.y / 2f, size.z / 2f + t / 2f), new Vector3(size.x + 2 * t, size.y, t), wood, true), "wood", 2f);
        GameKit.Surface(B("Gallery_WallS", new Vector3(0, size.y / 2f, -size.z / 2f - t / 2f), new Vector3(size.x + 2 * t, size.y, t), wood, true), "wood", 2f);
        GameKit.Surface(B("Gallery_WallE", new Vector3(size.x / 2f + t / 2f, size.y / 2f, 0), new Vector3(t, size.y, size.z), wood, true), "wood", 2f);
        GameKit.Surface(B("Gallery_WallW", new Vector3(-size.x / 2f - t / 2f, size.y / 2f, 0), new Vector3(t, size.y, size.z), wood, true), "wood", 2f);
        // a long blood-red runner down the aisle
        B("Velvet", new Vector3(0, 0.006f, -5.5f), new Vector3(2.4f, 0.012f, 17f), new Color(0.32f, 0.02f, 0.035f), false, 0.12f);
        B("Velvet", new Vector3(0, 0.007f, -5.5f), new Vector3(2.0f, 0.012f, 16.6f), new Color(0.42f, 0.03f, 0.05f), false, 0.12f);

        // ---- tower shelves of curios along both long walls (and glass specimen jars)
        foreach (float x in new[] { -11.4f, 11.4f })
        {
            B("TowerShelf", new Vector3(x, 5.5f, 0), new Vector3(0.8f, 11f, 28f), shelfWood, true, 0.2f);
            for (int s = 0; s < 14; s++)
            {
                float y = 0.5f + s * 0.75f;
                B("ShelfBoard", new Vector3(x + (x < 0 ? 0.45f : -0.45f), y, 0), new Vector3(0.12f, 0.04f, 28f), shelfWood * 1.2f, false, 0.2f);
                for (int k = 0; k < 18; k++)
                {
                    if (rnd.NextDouble() < 0.35) continue;
                    var p = new Vector3(x + (x < 0 ? 0.6f : -0.6f), y + 0.15f, -13f + k * 1.5f + (float)rnd.NextDouble() * 0.5f);
                    Color c = rnd.NextDouble() < 0.15 ? new Color(0.85f, 0.65f, 0.25f) : Color.HSVToRGB((float)rnd.NextDouble(), 0.35f, 0.25f + (float)rnd.NextDouble() * 0.4f);
                    int kind = rnd.Next(4);
                    if (s < 5)
                    {
                        int pick = (s * 7 + k * 3 + kind) % CurioProps.Length;
                        if (PropKit.Place(root, CurioProps[pick], origin + p - Vector3.up * 0.13f, k * 47f, CurioScale[pick]) != null) continue;
                    }
                    if (kind == 0) GameKit.Sphere(root, "Curio", origin + p, 0.22f, GameKit.Lit(c, 0.6f));
                    else if (kind == 1) B("Curio", p, new Vector3(0.2f, 0.28f, 0.2f), c, false, 0.5f);
                    else if (kind == 2) GameKit.Cone(root, "Curio", origin + p - Vector3.up * 0.12f, 0.12f, 0.32f, GameKit.Lit(c, 0.5f));
                    else
                    {
                        // a specimen jar: murky liquid, something pale floating in it
                        GameKit.Cyl(root, "Jar", origin + p + Vector3.up * 0.02f, 0.09f, 0.32f, GameKit.Lit(new Color(0.42f, 0.46f, 0.3f), 0.92f));
                        GameKit.Sphere(root, "Specimen", origin + p + Vector3.up * 0.04f, 0.1f, GameKit.Lit(new Color(0.78f, 0.7f, 0.62f), 0.5f));
                        GameKit.Cyl(root, "JarLid", origin + p + Vector3.up * 0.2f, 0.095f, 0.03f, GameKit.Lit(new Color(0.3f, 0.25f, 0.18f), 0.6f, 0.8f));
                    }
                }
            }
        }

        // ---- the long table with red velvet
        B("Table", new Vector3(0, 0.45f, 2f), new Vector3(6f, 0.9f, 1.6f), shelfWood, true, 0.4f);
        B("Velvet", new Vector3(0, 0.92f, 2f), new Vector3(6.2f, 0.05f, 1.8f), new Color(0.4f, 0.03f, 0.05f), false, 0.15f);
        PropKit.Place(root, "brass_candleholders", origin + new Vector3(-2.6f, 0.945f, 1.5f), 0f, 0.8f);
        PropKit.Place(root, "brass_candleholders", origin + new Vector3(2.6f, 0.945f, 1.5f), 40f, 0.8f);

        BuildBookcase(rnd, withShelfBook);
        BuildBackdrop();
        BuildChandeliers();
        BuildCandles(rnd, clearTableCentre);
        BuildDrapes();
        BuildDust();
        BuildMoodVolume();
    }

    static readonly string[] CurioProps = { "brass_pot_01", "ceramic_pot", "jug_01", "metal_jug", "brass_pot_02", "pot_enamel_01" };
    static readonly float[] CurioScale = { 1f, 0.6f, 1.1f, 1f, 0.9f, 1.3f };

    // ---------------------------------------------------------- the bookcase
    void BuildBookcase(System.Random rnd, bool withShelfBook)
    {
        var frame = new Color(0.18f, 0.1f, 0.06f);
        float zc = ShelfFront + 0.24f;
        B("Shelf", new Vector3(0, 2.32f, ShelfFront + 0.47f), new Vector3(7.3f, 4.64f, 0.06f), frame * 0.8f, true, 0.3f);
        foreach (float x in new[] { -3.62f, 3.62f })
            B("Shelf", new Vector3(x, 2.32f, zc), new Vector3(0.1f, 4.64f, 0.5f), frame, true, 0.35f);
        B("Shelf", new Vector3(0, 4.7f, zc - 0.03f), new Vector3(7.6f, 0.14f, 0.6f), frame, false, 0.35f);       // cornice
        for (int k = 0; k <= 9; k++)
            B("ShelfBoard", new Vector3(0, 0.08f + k * 0.5f, zc), new Vector3(7.14f, 0.04f, 0.48f), frame * 1.15f, false, 0.3f);

        // ~700 books, merged into a handful of meshes (one per leather colour) so they cost almost nothing to draw
        Color[] leather =
        {
            new Color(0.32f, 0.05f, 0.05f), new Color(0.2f, 0.09f, 0.05f), new Color(0.1f, 0.14f, 0.09f), new Color(0.28f, 0.2f, 0.12f),
            new Color(0.09f, 0.08f, 0.12f), new Color(0.42f, 0.32f, 0.2f), new Color(0.14f, 0.05f, 0.08f), new Color(0.5f, 0.44f, 0.34f),
        };
        var groups = new List<CombineInstance>[leather.Length + 1];
        for (int i = 0; i < groups.Length; i++) groups[i] = new List<CombineInstance>();
        Vector3 slot = ShelfBookSlot;
        for (int k = 0; k < 9; k++)
        {
            float baseY = 0.1f + k * 0.5f;
            float x = -3.52f;
            while (x < 3.5f)
            {
                float th = 0.035f + (float)rnd.NextDouble() * 0.05f;
                if (rnd.NextDouble() < 0.04) { x += 0.12f + (float)rnd.NextDouble() * 0.25f; continue; }    // a gap
                // leave the slot for Shadows of Chandni empty (in Act V the book is already out on the table)
                if (k == 3 && x + th > slot.x - StoryBookModel.T / 2f - 0.004f && x < slot.x + StoryBookModel.T / 2f + 0.004f) { x = slot.x + StoryBookModel.T / 2f + 0.006f; continue; }
                if (x + th > 3.54f) break;
                float h = 0.24f + (float)rnd.NextDouble() * 0.17f, d = 0.18f + (float)rnd.NextDouble() * 0.07f;
                float lean = rnd.NextDouble() < 0.05 ? (float)(rnd.NextDouble() * 2 - 1) * 9f : 0f;
                var pos = origin + new Vector3(x + th / 2f, baseY + h / 2f, ShelfFront + d / 2f + (float)rnd.NextDouble() * 0.02f);
                var rot = Quaternion.Euler(0, 0, lean);
                int ci = rnd.Next(leather.Length);
                groups[ci].Add(new CombineInstance { mesh = Cube(), transform = Matrix4x4.TRS(pos, rot, new Vector3(th, h, d)) });
                if (rnd.NextDouble() < 0.35)
                    foreach (float by in new[] { -0.36f, 0.36f })        // gilt bands across the spine
                        groups[leather.Length].Add(new CombineInstance { mesh = Cube(), transform = Matrix4x4.TRS(pos + rot * new Vector3(0, h * by, -d / 2f - 0.002f), rot, new Vector3(th * 0.96f, 0.012f, 0.004f)) });
                x += th + 0.002f;
            }
        }
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i].Count == 0) continue;
            var mesh = new Mesh { name = "Books" + i, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(groups[i].ToArray(), true, true);
            var go = new GameObject("BookSpines" + i);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = i < leather.Length ? GameKit.Lit(leather[i], 0.38f) : GameKit.Lit(new Color(0.85f, 0.64f, 0.26f), 0.75f, 1f);
        }

        if (withShelfBook)
        {
            shelfBook = StoryBookModel.Make(root, "CHAPTER ONE\n\nThe Empty Room");
            shelfBookPos = origin + slot;
            shelfBook.root.localPosition = shelfBookPos;
            shelfBook.root.localRotation = Quaternion.identity;
        }

        // heavy red curtains either side of the bookcase
        foreach (float sx in new[] { -1f, 1f })
            for (int f = 0; f < 6; f++)
                B("Velvet", new Vector3(sx * (3.95f + f * 0.2f), 3.2f, ShelfFront + 0.28f + (f % 2) * 0.08f), new Vector3(0.22f, 6.4f, 0.14f), new Color(0.3f, 0.02f, 0.04f) * (0.85f + (f % 2) * 0.25f), false, 0.1f);
        // a low red glow from behind the curtains
        foreach (float sx in new[] { -1f, 1f })
            AddLight(new Vector3(sx * 4.6f, 0.6f, ShelfFront - 0.4f), new Color(0.9f, 0.08f, 0.05f), 1.1f, 4.5f, false, 0f);
    }

    // ------------------------------------------- behind the bookcase: windows, moonlight, portraits
    void BuildBackdrop()
    {
        var moonC = new Color(0.45f, 0.58f, 0.95f);
        foreach (float x in new[] { -6f, 0f, 6f })
        {
            var w = new Vector3(x, 8.6f, 14.97f);
            GameKit.Box(root, "MoonWindow", origin + w, new Vector3(1.7f, 4.2f, 0.04f), GameKit.Glow(moonC * 0.3f, moonC, 0.9f));
            B("Mullion", w + new Vector3(0, 0, -0.04f), new Vector3(0.08f, 4.2f, 0.05f), new Color(0.05f, 0.04f, 0.04f));
            for (int i = 0; i < 4; i++) B("Mullion", w + new Vector3(0, -1.6f + i * 1.05f, -0.04f), new Vector3(1.7f, 0.07f, 0.05f), new Color(0.05f, 0.04f, 0.04f));
            GameKit.Cone(root, "WindowArch", origin + w + new Vector3(0, 2.1f, -0.02f), 0.85f, 0.7f, GameKit.Glow(moonC * 0.25f, moonC, 0.6f), 16);
            // a cold shaft of light falling across the hall onto the floor in front of the bookcase
            var spot = new GameObject("Moonlight").AddComponent<Light>();
            spot.transform.SetParent(root, false);
            spot.transform.localPosition = origin + w + new Vector3(0, 0, -0.6f);
            spot.transform.localRotation = Quaternion.LookRotation(new Vector3(x * 0.15f, 0, 5f) - (w + new Vector3(0, 0, -0.6f)));
            spot.type = LightType.Spot; spot.color = moonC; spot.intensity = 9f; spot.range = 22f; spot.spotAngle = 26f; spot.innerSpotAngle = 10f;
            spot.shadows = x == 0f ? LightShadows.Soft : LightShadows.None;
            moonLights.Add(spot);
            Shaft(origin + w + new Vector3(0, 0, -0.3f), origin + new Vector3(x * 0.15f, 0.2f, 5.5f), 1.5f, moonC * 0.05f);
        }
        // portraits in the dark behind the bookcase: pale faces, eyes that catch the light
        foreach (float x in new[] { -7.6f, 7.6f })
        {
            var p = new Vector3(x, 3.4f, 14.85f);
            GameKit.Box(root, "PortraitFrame", origin + p, new Vector3(1.5f, 2f, 0.08f), GameKit.Lit(new Color(0.6f, 0.45f, 0.18f), 0.7f, 1f));
            B("Canvas", p + new Vector3(0, 0, -0.05f), new Vector3(1.3f, 1.8f, 0.02f), new Color(0.06f, 0.04f, 0.035f), false, 0.4f);
            var face = GameKit.Sphere(root, "PortraitFace", origin + p + new Vector3(0, 0.25f, -0.07f), 0.5f, GameKit.Lit(new Color(0.55f, 0.5f, 0.46f), 0.3f));
            face.transform.localScale = new Vector3(0.42f, 0.56f, 0.06f);
            B("Canvas", p + new Vector3(0, -0.45f, -0.07f), new Vector3(0.8f, 0.7f, 0.03f), new Color(0.08f, 0.06f, 0.06f), false, 0.3f);
            foreach (float ex in new[] { -0.08f, 0.08f })
                GameKit.Sphere(root, "PortraitEye", origin + p + new Vector3(ex, 0.3f, -0.105f), 0.035f, GameKit.Glow(new Color(0.3f, 0f, 0f), new Color(1f, 0.05f, 0.02f), 3f));
        }
    }

    /// <summary>A soft beam of light: a stretched additive box from a to b.</summary>
    void Shaft(Vector3 a, Vector3 b, float width, Color c)
    {
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) return;
        var m = new Material(sh);
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 2f);
        m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)BlendMode.One);
        m.SetFloat("_ZWrite", 0f); m.SetFloat("_Cull", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetColor("_BaseColor", c);
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "LightShaft";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(root, false);
        go.transform.localPosition = (a + b) / 2f;
        go.transform.localRotation = Quaternion.LookRotation(b - a);
        go.transform.localScale = new Vector3(width, width * 0.3f, (b - a).magnitude);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
    }

    // ------------------------------------------------------- chandeliers
    void BuildChandeliers()
    {
        var iron = GameKit.Lit(new Color(0.08f, 0.07f, 0.06f), 0.45f, 0.8f);
        var wax = GameKit.Lit(new Color(0.9f, 0.86f, 0.76f), 0.25f);
        foreach (float z in new[] { -7.5f, 2.2f, 6.6f })
        {
            var c = new Vector3(0, z > 5f ? 4.3f : 4.7f, z);
            GameKit.Box(root, "Chain", origin + c + new Vector3(0, (12f - c.y) / 2f, 0), new Vector3(0.03f, 12f - c.y, 0.03f), iron);
            const int n = 12; const float r = 0.85f;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                var p = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                var seg = GameKit.Box(root, "Ring", origin + p, new Vector3(0.05f, 0.05f, 2f * Mathf.PI * r / n + 0.02f), iron);
                seg.transform.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0);
                if (i % 3 == 0)
                {
                    var arm = GameKit.Box(root, "Spoke", origin + c + (p - c) * 0.5f + Vector3.up * 0.25f, new Vector3(0.025f, 0.025f, r * 1.08f), iron);
                    arm.transform.localRotation = Quaternion.LookRotation((p - c) + Vector3.down * 0.5f);
                }
                GameKit.Cyl(root, "Candle", origin + p + Vector3.up * 0.1f, 0.025f, 0.16f, wax);
                AddFlame(p + Vector3.up * 0.2f, 0.035f);
            }
            AddLight(c + Vector3.down * 0.25f, new Color(1f, 0.6f, 0.3f), 5.5f, 12f, z == 2.2f, 0.25f);
        }
    }

    // ----------------------------------------------- candles (table, aisle, shelves)
    void BuildCandles(System.Random rnd, bool clearTableCentre)
    {
        var wax = GameKit.Lit(new Color(0.92f, 0.88f, 0.78f), 0.2f);
        for (int i = 0; i < 46; i++)
        {
            Vector3 p;
            if (i < 10)
            {
                if (clearTableCentre && (i == 4 || i == 5)) continue;
                p = new Vector3(-2.8f + i * 0.62f, 0.95f, 2.62f);
            }
            else if (i < 30) p = new Vector3((i % 2 == 0 ? -1 : 1) * (4f + (float)rnd.NextDouble() * 5f), 0f, -12f + (i - 10) * 1.3f);
            else p = new Vector3(i % 2 == 0 ? -10.8f : 10.8f, 0.6f + ((i - 30) / 2) * 1.5f, -10f + (i % 5) * 5f);
            float h = i < 10 ? 0.25f : (i < 30 ? 1.1f : 0.25f);
            if (i >= 10 && i < 30)        // tall floor candles stand on an iron stand with a drip tray
            {
                GameKit.Cyl(root, "CandleStand", origin + p + new Vector3(0, 0.02f, 0), 0.16f, 0.04f, GameKit.Lit(new Color(0.08f, 0.07f, 0.06f), 0.4f, 0.8f));
            }
            GameKit.Cyl(root, "Candle", origin + p + new Vector3(0, h / 2f, 0), i < 10 ? 0.03f : 0.05f, h, wax);
            AddFlame(p + new Vector3(0, h + 0.04f, 0), 0.05f);
            if (i % 5 == 0) AddLight(p + new Vector3(0, h + 0.3f, 0), new Color(1f, 0.68f, 0.35f), 2.4f, 7f, false, 0.35f);
        }
    }

    void AddFlame(Vector3 p, float d)
    {
        var f = GameKit.Sphere(root, "Flame", origin + p, d, GameKit.Glow(new Color(1f, 0.8f, 0.4f), new Color(1f, 0.7f, 0.3f), 8f));
        f.transform.localScale = new Vector3(d, d * 1.7f, d);
        f.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        f.AddComponent<FlameWobble>();
        flames.Add(f);
    }

    void AddLight(Vector3 p, Color c, float intensity, float range, bool shadows, float dip)
    {
        var l = GameKit.PointLight(root, origin + p, c, intensity, range);
        l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        var f = l.gameObject.AddComponent<CandleFlicker>();
        f.full = intensity; f.dipChance = dip;
        lights.Add(f);
    }

    // ------------------------------------------------ draped figures in the corners
    void BuildDrapes()
    {
        var sheet = GameKit.Lit(new Color(0.6f, 0.58f, 0.54f), 0.08f);
        Vector3[] at = { new Vector3(-8.6f, 0, 10.5f), new Vector3(8.4f, 0, 11f), new Vector3(-8.8f, 0, -9.5f), new Vector3(8.7f, 0, -10.5f) };
        for (int i = 0; i < at.Length; i++)
        {
            var d = new GameObject("DrapedFigure").transform;
            d.SetParent(root, false);
            d.localPosition = origin + at[i];
            GameKit.Cone(d, "Sheet", new Vector3(0, 0, 0), 0.5f, 1.45f, sheet, 18);
            GameKit.Cyl(d, "Sheet", new Vector3(0, 1.25f, 0), 0.2f, 0.5f, sheet);
            var head = GameKit.Sphere(d, "Sheet", new Vector3(0, 1.62f, 0.02f), 0.34f, sheet);
            head.transform.localScale = new Vector3(0.3f, 0.36f, 0.32f);
            GameKit.Box(d, "Sheet", new Vector3(0, 1.38f, 0), new Vector3(0.62f, 0.12f, 0.3f), sheet);     // shoulders under the cloth
            d.localRotation = Quaternion.Euler(0, 180f + at[i].x * 4f, 0);
            drapes.Add(d);
        }
    }

    // ------------------------------------------------ dust in the candlelight
    void BuildDust()
    {
        var ps = new GameObject("Dust").AddComponent<ParticleSystem>();
        ps.transform.SetParent(root, false);
        ps.transform.localPosition = origin + new Vector3(0, 2.5f, 0f);
        var main = ps.main;
        main.loop = true; main.prewarm = true; main.startLifetime = 14f; main.startSpeed = 0.02f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.018f);
        main.startColor = new Color(1f, 0.88f, 0.72f, 0.4f); main.maxParticles = 500; main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 32f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(9f, 4.5f, 22f);
        var nz = ps.noise; nz.enabled = true; nz.strength = 0.05f; nz.frequency = 0.25f;
        var sd = Shader.Find("Sprites/Default");
        if (sd != null) ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = new Material(sd);
        ps.Play();
    }

    // ------------------------------------------------ the film look inside the hall
    void BuildMoodVolume()
    {
        var go = new GameObject("GalleryMood");
        go.transform.SetParent(root, false);
        go.transform.localPosition = origin + new Vector3(0, 6f, 0);
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true; box.size = new Vector3(24f, 12f, 30f);
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = false; vol.priority = 60; vol.blendDistance = 1f;
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        vol.sharedProfile = prof;

        var ca = prof.Add<ColorAdjustments>(true);
        ca.postExposure.Override(1.15f);           // bright: the candlelit hall reads clearly...
        ca.contrast.Override(30f);                 // ...but the blacks are deep
        ca.saturation.Override(-22f);
        ca.colorFilter.Override(new Color(1f, 0.93f, 0.88f));
        var smh = prof.Add<ShadowsMidtonesHighlights>(true);      // cold, sick shadows; warm candle highlights
        smh.shadows.Override(new Vector4(0.86f, 0.96f, 1.12f, -0.08f));
        smh.highlights.Override(new Vector4(1.12f, 0.98f, 0.86f, 0.04f));
        var bloom = prof.Add<Bloom>(true);
        bloom.threshold.Override(0.85f); bloom.intensity.Override(0.95f); bloom.scatter.Override(0.7f);
        bloom.tint.Override(new Color(1f, 0.72f, 0.5f));
        var vig = prof.Add<Vignette>(true);
        vig.intensity.Override(0.42f); vig.smoothness.Override(0.42f); vig.color.Override(new Color(0.06f, 0f, 0f));
        var grain = prof.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Medium3); grain.intensity.Override(0.4f); grain.response.Override(0.7f);
        var chroma = prof.Add<ChromaticAberration>(true);
        chroma.intensity.Override(0.14f);
    }

    static Mesh Cube()
    {
        if (cubeMesh != null) return cubeMesh;
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubeMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(tmp);
        return cubeMesh;
    }
}

/// <summary>
/// "Shadows of Chandni": the Curator's red leather book. Built standing upright with the spine facing -Z, the
/// front cover on +X (it opens on a hinge at the spine) and a short text on the first page.
/// </summary>
public class StoryBookModel
{
    public const float T = 0.07f, H = 0.32f, W = 0.23f;     // thickness (x), height (y), width (z)
    public Transform root, cover;
    public TextMesh pageText;
    static readonly Color Leather = new Color(0.34f, 0.035f, 0.045f), Gold = new Color(1f, 0.8f, 0.4f), Paper = new Color(0.86f, 0.8f, 0.66f);

    public static StoryBookModel Make(Transform parent, string page)
    {
        var b = new StoryBookModel();
        b.root = new GameObject("ShadowsOfChandni").transform;
        b.root.SetParent(parent, false);
        var leather = GameKit.Lit(Leather, 0.42f);
        var gilt = GameKit.Lit(Gold * 0.85f, 0.8f, 1f);
        GameKit.Box(b.root, "BackCover", new Vector3(-T / 2f + 0.005f, 0, 0.004f), new Vector3(0.01f, H, W), leather);
        GameKit.Box(b.root, "Pages", new Vector3(0, 0, 0.004f), new Vector3(T - 0.02f, H - 0.016f, W - 0.014f), GameKit.Lit(Paper, 0.15f));
        GameKit.Box(b.root, "Spine", new Vector3(0, 0, -W / 2f), new Vector3(T, H, 0.012f), leather);
        foreach (float by in new[] { -0.142f, 0.142f, -0.122f, 0.122f })
            GameKit.Box(b.root, "SpineBand", new Vector3(0, by, -W / 2f - 0.004f), new Vector3(T + 0.002f, 0.007f, 0.008f), gilt);
        Txt(b.root, "SHADOWS OF CHANDNI", new Vector3(0, 0f, -W / 2f - 0.0095f), Quaternion.Euler(0, 0, -90f), 0.018f, Gold);

        // front cover on a hinge at the spine
        b.cover = new GameObject("CoverHinge").transform;
        b.cover.SetParent(b.root, false);
        b.cover.localPosition = new Vector3(T / 2f - 0.005f, 0, -W / 2f);
        GameKit.Box(b.cover, "FrontCover", new Vector3(0, 0, W / 2f + 0.004f), new Vector3(0.01f, H, W), leather);
        GameKit.Box(b.cover, "CoverFrame", new Vector3(0.0056f, 0, W / 2f + 0.004f), new Vector3(0.001f, H - 0.04f, W - 0.04f), gilt);
        GameKit.Box(b.cover, "CoverPanel", new Vector3(0.0062f, 0, W / 2f + 0.004f), new Vector3(0.001f, H - 0.05f, W - 0.05f), leather);
        Txt(b.cover, "SHADOWS\nOF\nCHANDNI", new Vector3(0.0085f, 0.035f, W / 2f + 0.004f), Quaternion.Euler(0, -90f, 0), 0.026f, Gold);
        Txt(b.cover, "- the collection -", new Vector3(0.0085f, -0.09f, W / 2f + 0.004f), Quaternion.Euler(0, -90f, 0), 0.011f, Gold * 0.9f);
        // the inside of the cover (the left-hand page once it is open)
        GameKit.Box(b.cover, "Endpaper", new Vector3(-0.0056f, 0, W / 2f + 0.004f), new Vector3(0.001f, H - 0.01f, W - 0.01f), GameKit.Lit(new Color(0.3f, 0.06f, 0.06f), 0.2f));
        Txt(b.cover, "Chandnipur\n\nas kept by\nTHE CURATOR", new Vector3(-0.0075f, 0.02f, W / 2f + 0.004f), Quaternion.Euler(0, 90f, 0), 0.014f, Gold * 0.9f);
        // the first page
        b.pageText = Txt(b.root, page, new Vector3((T - 0.02f) / 2f + 0.0015f, 0.01f, 0.006f), Quaternion.Euler(0, -90f, 0), 0.019f, new Color(0.16f, 0.05f, 0.04f));
        return b;
    }

    static TextMesh Txt(Transform p, string s, Vector3 pos, Quaternion rot, float size, Color c)
    {
        var tm = GameKit.Solid(GameKit.Text(p, s, pos, rot, size, c));
        tm.lineSpacing = 0.9f;
        return tm;
    }

    /// <summary>A single page turning over (a thin leaf swinging round the spine); returns the leaf.</summary>
    public Transform PageLeaf()
    {
        var hinge = new GameObject("Leaf").transform;
        hinge.SetParent(root, false);
        hinge.localPosition = new Vector3((T - 0.02f) / 2f - 0.001f, 0, -W / 2f + 0.006f);
        GameKit.Box(hinge, "Paper", new Vector3(0.0006f, 0, (W - 0.016f) / 2f + 0.002f), new Vector3(0.0012f, H - 0.02f, W - 0.016f), GameKit.Lit(Paper * 1.05f, 0.15f));
        return hinge;
    }
}

/// <summary>A warm light that breathes like a flame; level scales it (lighting up, snuffing out).</summary>
public class CandleFlicker : MonoBehaviour
{
    public float full = 2f, level = 1f, dipChance = 0.25f;
    Light l; float seed, dipUntil, nextDip, dipAmt = 1f;

    void Awake() { l = GetComponent<Light>(); seed = Random.value * 100f; nextDip = Time.time + Random.Range(2f, 6f); }

    void Update()
    {
        if (l == null) return;
        float n = Mathf.PerlinNoise(seed, Time.time * 2.6f) * 0.7f + Mathf.PerlinNoise(seed + 7f, Time.time * 9f) * 0.3f;
        if (Time.time > nextDip)
        {
            nextDip = Time.time + Random.Range(2.5f, 8f);
            if (Random.value < dipChance) { dipUntil = Time.time + Random.Range(0.06f, 0.2f); dipAmt = Random.Range(0.25f, 0.6f); }
        }
        float dip = Time.time < dipUntil ? dipAmt : 1f;
        l.intensity = full * level * (0.8f + 0.35f * n) * dip;
        l.enabled = level > 0.005f;
    }
}

/// <summary>Makes a candle flame shiver and stretch.</summary>
public class FlameWobble : MonoBehaviour
{
    Vector3 s0; float seed;
    void Start() { s0 = transform.localScale; seed = Random.value * 50f; }
    void Update()
    {
        float n = Mathf.PerlinNoise(seed, Time.time * 7f);
        transform.localScale = new Vector3(s0.x * (0.9f + 0.15f * n), s0.y * (0.8f + 0.45f * n), s0.z * (0.9f + 0.15f * n));
    }
}
