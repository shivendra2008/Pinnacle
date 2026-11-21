using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The Curator's Gallery, a hall outside time. The Curator's interludes between the acts and the Act V casket choice
/// both happen here, so it is built in one place.
/// The light is motivated and kept in proportion so a face stays readable without heavy grading: candles and three
/// candle chandeliers are warm practicals (the key), the windows let in a dim, cold night-sky fill, and the dark keeps
/// its shape (low ambient, thin fog) instead of going black. A storm is outside: rain beyond the tall east window and
/// on the glass of every window, lightning coming in through the east window as a cold shadow-casting light, thunder
/// after it (GalleryStorm). Exposure follows the light on the subject's face (GalleryExposure), like an operator on
/// the iris. The mood volume is light: a gentle curve, a touch of bloom on the flames, no colour fringing, and it
/// follows the F2 graphics tier like the film look does.
/// Draped figures stand in the corners, and portraits with faint red eyes hang behind the bookcase.
/// </summary>
public class CuratorGallery
{
    public Transform root;
    public Vector3 origin;
    public readonly List<GameObject> flames = new List<GameObject>();
    public readonly List<CandleFlicker> lights = new List<CandleFlicker>();   // every warm light (candles + chandeliers)
    public readonly List<Light> moonLights = new List<Light>();
    public readonly List<Transform> drapes = new List<Transform>();
    public GalleryStorm storm;
    public GalleryExposure exposure;
    public VolumeProfile profile;                   // the Gallery's mood (registered with the graphics tiers)
    public Bounds tableTop, shelfBoards;            // (parent space) what hands and props keep above / clear of
    public StoryBookModel shelfBook;                // "Shadows of Chandni" on the shelf (null if not built)
    public Vector3 shelfBookPos;                    // its resting place (parent space)
    public const float ShelfFront = 8.7f;           // z of the bookcase's front (relative to origin)
    public const float WinZ0 = 0.5f, WinZ1 = 2.9f, WinY0 = 2.2f, WinY1 = 8.4f;
    static readonly Color BackSkyLow = new Color(0.2f, 0.22f, 0.29f), BackSkyHigh = new Color(0.08f, 0.095f, 0.14f);   // the high windows' night sky   // the tall east window (x = +12)
    public static readonly Vector3 ShelfBookSlot = new Vector3(1.0f, 1.76f, ShelfFront + StoryBookModel.W / 2f);

    static Mesh cubeMesh;

    // ------------------------------------------------------------------ mood
    // Ambient is the light bounced round the room: a little cold from the windows above, a little warm from the
    // candles at waist height, almost none from the floor. Low enough that the candles model the faces, high enough
    // that the shelves and the far walls still have shape in the dark.
    public static readonly Color AmbSky = new Color(0.3f, 0.31f, 0.37f), AmbEq = new Color(0.36f, 0.27f, 0.21f), AmbGround = new Color(0.1f, 0.08f, 0.068f);
    public static readonly Color FogColor = new Color(0.035f, 0.03f, 0.032f);
    public const float FogDensity = 0.012f;

    /// <summary>The gallery's ambient light and fog; level 0 = only the candles, 1 = the hall fully lit.</summary>
    public static void Mood(Camera cam, float level)
    {
        float k = Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(level));
        CinematicLook.Ambient(AmbSky * k, AmbEq * k, AmbGround * k, Mathf.Lerp(0.25f, 0.6f, level));
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = FogColor * Mathf.Lerp(0.5f, 1f, level);
        RenderSettings.fogDensity = FogDensity;
        if (cam != null) CinematicLook.NoSky(cam, new Color(0.012f, 0.011f, 0.013f));
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
        var wood = new Color(0.28f, 0.17f, 0.105f);         // aged oak: dark, but not so dark that the light dies on it
        var shelfWood = new Color(0.3f, 0.18f, 0.105f);

        // ---- the hall: 24 x 12 x 30, dark panelled wood and a polished floor that mirrors the candles
        Vector3 size = new Vector3(24f, 12f, 30f); float t = 0.25f;
        GameKit.Surface(B("Gallery_Floor", new Vector3(0, -0.05f, 0), new Vector3(size.x + 2 * t, 0.1f, size.z + 2 * t), new Color(0.26f, 0.16f, 0.1f), true, 0.75f), "wood_floor", 1.2f, 1.4f, 1f, new Color(0.26f, 0.16f, 0.1f));
        GameKit.Surface(B("Gallery_Ceiling", new Vector3(0, size.y + 0.05f, 0), new Vector3(size.x + 2 * t, 0.1f, size.z + 2 * t), wood * 0.6f, true), "wood", 2f);
        GameKit.Surface(B("Gallery_WallN", new Vector3(0, size.y / 2f, size.z / 2f + t / 2f), new Vector3(size.x + 2 * t, size.y, t), wood, true), "wood", 2f);
        GameKit.Surface(B("Gallery_WallS", new Vector3(0, size.y / 2f, -size.z / 2f - t / 2f), new Vector3(size.x + 2 * t, size.y, t), wood, true), "wood", 2f);
        // the east wall has an opening for the tall window (the lightning comes in through it)
        float ex = size.x / 2f + t / 2f;
        GameKit.Surface(B("Gallery_WallE", new Vector3(ex, size.y / 2f, (-size.z / 2f + WinZ0) / 2f), new Vector3(t, size.y, WinZ0 + size.z / 2f), wood, true), "wood", 2f);
        GameKit.Surface(B("Gallery_WallE", new Vector3(ex, size.y / 2f, (WinZ1 + size.z / 2f) / 2f), new Vector3(t, size.y, size.z / 2f - WinZ1), wood, true), "wood", 2f);
        GameKit.Surface(B("Gallery_WallE", new Vector3(ex, WinY0 / 2f, (WinZ0 + WinZ1) / 2f), new Vector3(t, WinY0, WinZ1 - WinZ0), wood, true), "wood", 2f);
        GameKit.Surface(B("Gallery_WallE", new Vector3(ex, (WinY1 + size.y) / 2f, (WinZ0 + WinZ1) / 2f), new Vector3(t, size.y - WinY1, WinZ1 - WinZ0), wood, true), "wood", 2f);
        GameKit.Surface(B("Gallery_WallW", new Vector3(-size.x / 2f - t / 2f, size.y / 2f, 0), new Vector3(t, size.y, size.z), wood, true), "wood", 2f);
        // a long blood-red runner down the aisle
        B("Velvet", new Vector3(0, 0.006f, -5.5f), new Vector3(2.4f, 0.012f, 17f), new Color(0.32f, 0.02f, 0.035f), false, 0.12f);
        B("Velvet", new Vector3(0, 0.007f, -5.5f), new Vector3(2.0f, 0.012f, 16.6f), new Color(0.42f, 0.03f, 0.05f), false, 0.12f);

        // ---- tower shelves of curios along both long walls (and glass specimen jars)
        foreach (float x in new[] { -11.4f, 11.4f })
        {
            // the east shelf stops either side of the tall window (z WinZ0..WinZ1)
            foreach (var span in x < 0 ? new[] { new Vector2(-14f, 14f) } : new[] { new Vector2(-14f, WinZ0 - 0.3f), new Vector2(WinZ1 + 0.3f, 14f) })
            {
                float zc = (span.x + span.y) / 2f, zl = span.y - span.x;
                B("TowerShelf", new Vector3(x, 5.5f, zc), new Vector3(0.8f, 11f, zl), shelfWood, true, 0.2f);
                for (int s2 = 0; s2 < 14; s2++)
                    B("ShelfBoard", new Vector3(x + (x < 0 ? 0.45f : -0.45f), 0.5f + s2 * 0.75f, zc), new Vector3(0.12f, 0.04f, zl), shelfWood * 1.2f, false, 0.2f);
            }
            for (int s = 0; s < 14; s++)
            {
                float y = 0.5f + s * 0.75f;
                for (int k = 0; k < 18; k++)
                {
                    if (rnd.NextDouble() < 0.35) continue;
                    var p = new Vector3(x + (x < 0 ? 0.6f : -0.6f), y + 0.15f, -13f + k * 1.5f + (float)rnd.NextDouble() * 0.5f);
                    if (x > 0 && p.z > WinZ0 - 0.6f && p.z < WinZ1 + 0.6f) continue;
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
        tableTop = new Bounds(origin + new Vector3(0, 0.9f, 2f), new Vector3(6.2f, 0.09f, 1.8f));
        PropKit.Place(root, "brass_candleholders", origin + new Vector3(-2.6f, 0.945f, 1.5f), 0f, 0.8f);
        PropKit.Place(root, "brass_candleholders", origin + new Vector3(2.6f, 0.945f, 1.5f), 40f, 0.8f);

        BuildBookcase(rnd, withShelfBook);
        BuildStorm();
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
        var frame = new Color(0.26f, 0.15f, 0.09f);
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

        // a candle sconce on each pillar of the bookcase: the shelves behind him keep their shape in the dark
        foreach (float sx in new[] { -1f, 1f })
        {
            var iron = GameKit.Lit(new Color(0.08f, 0.07f, 0.06f), 0.45f, 0.8f);
            var at = new Vector3(sx * 3.62f, 2.45f, ShelfFront - 0.12f);
            B("SconceArm", at + new Vector3(0, -0.05f, -0.08f), new Vector3(0.03f, 0.03f, 0.2f), new Color(0.08f, 0.07f, 0.06f), false, 0.45f, 0.8f);
            GameKit.Cyl(root, "SconceDish", origin + at + new Vector3(0, 0f, -0.18f), 0.05f, 0.012f, iron);
            GameKit.Cyl(root, "Candle", origin + at + new Vector3(0, 0.08f, -0.18f), 0.022f, 0.15f, GameKit.Lit(new Color(0.92f, 0.88f, 0.78f), 0.2f));
            AddFlame(at + new Vector3(0, 0.18f, -0.18f), 0.03f);
            AddLight(at + new Vector3(0, 0.3f, -0.32f), new Color(1f, 0.66f, 0.4f), 1.6f, 4.5f, false, 0.3f);
        }
        // heavy red curtains either side of the bookcase
        foreach (float sx in new[] { -1f, 1f })
            for (int f = 0; f < 6; f++)
                B("Velvet", new Vector3(sx * (3.95f + f * 0.2f), 3.2f, ShelfFront + 0.28f + (f % 2) * 0.08f), new Vector3(0.22f, 6.4f, 0.14f), new Color(0.3f, 0.02f, 0.04f) * (0.85f + (f % 2) * 0.25f), false, 0.1f);
        // a low red glow from behind the curtains
        foreach (float sx in new[] { -1f, 1f })
            AddLight(new Vector3(sx * 4.6f, 0.6f, ShelfFront - 0.4f), new Color(0.9f, 0.12f, 0.07f), 0.55f, 4.5f, false, 0f);
    }

    // ------------------------------------------- behind the bookcase: windows, the night sky, portraits
    void BuildBackdrop()
    {
        var nightC = new Color(0.55f, 0.66f, 0.95f);
        foreach (float x in new[] { -6f, 0f, 6f })
        {
            var w = new Vector3(x, 8.6f, 14.97f);
            // the glass: the stormy night through rain (these panes are on the wall: the sky is drawn in the glass).
            // Cloud lit from below by the town: the brightest thing above the bookcase, which gives the hall its
            // shape from the door and motivates the cold fill below (at the east window's level they read as black)
            var pane = Pane(origin + w + new Vector3(0, 0, -0.005f), Quaternion.Euler(0, 180f, 0), new Vector2(1.7f, 4.2f), 1f);
            pane.sharedMaterial.SetColor("_SkyBottom", BackSkyLow);
            pane.sharedMaterial.SetColor("_SkyTop", BackSkyHigh);
            var archMat = GameKit.Unlit(BackSkyHigh);
            storm.AddSky(archMat, BackSkyHigh * 0.9f);
            GameKit.Cone(root, "WindowArch", origin + w + new Vector3(0, 2.1f, -0.02f), 0.85f, 0.7f, archMat, 16);
            B("Mullion", w + new Vector3(0, 0, -0.04f), new Vector3(0.08f, 4.2f, 0.05f), new Color(0.05f, 0.04f, 0.04f));
            for (int i = 0; i < 4; i++) B("Mullion", w + new Vector3(0, -1.6f + i * 1.05f, -0.04f), new Vector3(1.7f, 0.07f, 0.05f), new Color(0.05f, 0.04f, 0.04f));
            // a dim, cold fill from the night sky across the hall onto the floor in front of the bookcase
            var spot = new GameObject("NightSky").AddComponent<Light>();
            spot.transform.SetParent(root, false);
            spot.transform.localPosition = origin + w + new Vector3(0, 0, -0.6f);
            spot.transform.localRotation = Quaternion.LookRotation(new Vector3(x * 0.15f, 0, 5f) - (w + new Vector3(0, 0, -0.6f)));
            spot.type = LightType.Spot; spot.color = nightC; spot.intensity = 30f; spot.range = 22f; spot.spotAngle = 30f; spot.innerSpotAngle = 12f;
            spot.shadows = x == 0f ? LightShadows.Soft : LightShadows.None;
            moonLights.Add(spot);
            storm.AddMoon(spot);
            // lightning through the same window: a brighter cold light from the same place, only during a flash
            var fl = new GameObject("LightningBack").AddComponent<Light>();
            fl.transform.SetParent(root, false);
            fl.transform.localPosition = spot.transform.localPosition + Vector3.up * 0.5f;
            fl.transform.localRotation = spot.transform.localRotation;
            fl.type = LightType.Spot; fl.color = new Color(0.78f, 0.85f, 1f); fl.intensity = 0f; fl.range = 26f; fl.spotAngle = 44f; fl.innerSpotAngle = 20f;
            fl.shadows = LightShadows.None; fl.enabled = false;
            storm.backFlash.Add(fl);
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

    // ------------------------------------------- the storm: the tall east window, rain, lightning
    void BuildStorm()
    {
        var go = new GameObject("Storm");
        go.transform.SetParent(root, false);
        storm = go.AddComponent<GalleryStorm>();
        float zc = (WinZ0 + WinZ1) / 2f, zw = WinZ1 - WinZ0, yc = (WinY0 + WinY1) / 2f, yh = WinY1 - WinY0;
        var frame = new Color(0.06f, 0.045f, 0.04f);
        // the reveal through the wall and the shelf, a sill, and the frame with its mullions (they cast the window's shadow)
        B("WindowReveal", new Vector3(11.6f, WinY0 - 0.05f, zc), new Vector3(1.2f, 0.1f, zw + 0.1f), frame * 1.4f, false, 0.35f);
        B("WindowReveal", new Vector3(11.6f, WinY1 + 0.05f, zc), new Vector3(1.2f, 0.1f, zw + 0.1f), frame * 1.4f, false, 0.35f);
        B("WindowReveal", new Vector3(11.6f, yc, WinZ0 - 0.05f), new Vector3(1.2f, yh + 0.2f, 0.1f), frame * 1.4f, false, 0.35f);
        B("WindowReveal", new Vector3(11.6f, yc, WinZ1 + 0.05f), new Vector3(1.2f, yh + 0.2f, 0.1f), frame * 1.4f, false, 0.35f);
        B("WindowSill", new Vector3(11.35f, WinY0 + 0.02f, zc), new Vector3(0.7f, 0.05f, zw + 0.3f), frame * 1.8f, false, 0.5f);
        float gx = 12.12f;
        B("Mullion", new Vector3(gx - 0.05f, yc, zc), new Vector3(0.06f, yh, 0.07f), frame);
        for (int i = 1; i < 6; i++) B("Mullion", new Vector3(gx - 0.05f, WinY0 + yh * i / 6f, zc), new Vector3(0.06f, 0.06f, zw), frame);
        foreach (float dz in new[] { -zw / 4f, zw / 4f }) B("Mullion", new Vector3(gx - 0.05f, yc, zc + dz), new Vector3(0.04f, yh, 0.04f), frame);
        // the glass (see-through between the drops), and outside: the sky and the falling rain
        Pane(origin + new Vector3(gx, yc, zc), Quaternion.Euler(0, -90f, 0), new Vector2(zw, yh), 0.34f);
        var sky = GameObject.CreatePrimitive(PrimitiveType.Quad); sky.name = "NightSky";
        Object.Destroy(sky.GetComponent<Collider>());
        sky.transform.SetParent(root, false);
        sky.transform.localPosition = origin + new Vector3(45f, 8f, zc);
        sky.transform.localRotation = Quaternion.Euler(0, 90f, 0);
        sky.transform.localScale = new Vector3(90f, 50f, 1f);
        var skyMat = GameKit.Unlit(new Color(0.045f, 0.05f, 0.068f));
        var skyR = sky.GetComponent<Renderer>(); skyR.sharedMaterial = skyMat; skyR.shadowCastingMode = ShadowCastingMode.Off;
        storm.AddSky(skyMat, new Color(0.045f, 0.05f, 0.068f));
        storm.rain.Add(RainOutside(origin + new Vector3(15.5f, 7f, zc), new Vector3(6f, 0.5f, 8f)));
        // the lightning: far outside, high up, through the window and onto the table and the man behind it
        var l = new GameObject("Lightning").AddComponent<Light>();
        l.transform.SetParent(root, false);
        l.transform.localPosition = origin + new Vector3(22f, 13f, zc - 0.4f);
        l.transform.localRotation = Quaternion.LookRotation(new Vector3(0.2f, 1.2f, 2.3f) - new Vector3(22f, 13f, zc - 0.4f));
        l.type = LightType.Spot; l.color = new Color(0.78f, 0.85f, 1f); l.range = 60f; l.spotAngle = 34f; l.innerSpotAngle = 20f;
        CinematicLook.Shadows(l, 0.92f, 0.02f, 0.4f);
        l.intensity = 0f; l.enabled = false;
        storm.mainFlash = l;
        storm.mainPeak = 7000f;      // a few times the candle key on a face at the table for a fraction of a second (the light is 24 m away)
        storm.backPeak = 140f;
        // the night sky through the east window, very dim and cold, all the time
        var n = new GameObject("NightSkyEast").AddComponent<Light>();
        n.transform.SetParent(root, false);
        n.transform.localPosition = l.transform.localPosition; n.transform.localRotation = l.transform.localRotation;
        n.type = LightType.Spot; n.color = new Color(0.55f, 0.65f, 0.95f); n.range = 60f; n.spotAngle = 34f; n.innerSpotAngle = 14f;
        n.intensity = 120f; n.shadows = LightShadows.Soft; n.shadowStrength = 0.85f;
        moonLights.Add(n);
        storm.AddMoon(n);
    }

    /// <summary>A window pane with rain on it (Pinnacle/RainGlass). alpha: how much of what is behind shows between the drops.</summary>
    Renderer Pane(Vector3 pos, Quaternion rot, Vector2 size, float alpha)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = "RainGlass";
        Object.Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(root, false);
        q.transform.localPosition = pos; q.transform.localRotation = rot; q.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sh = Shader.Find("Pinnacle/RainGlass");
        var m = sh != null ? new Material(sh) : GameKit.Unlit(new Color(0.05f, 0.06f, 0.08f));
        if (sh != null) { m.SetVector("_Size", new Vector4(size.x, size.y, 0, 0)); m.SetFloat("_GlassAlpha", alpha); }
        var r = q.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        storm.glass.Add(m);
        return r;
    }

    /// <summary>Rain falling outside a window: thin streaks, only seen through the glass.</summary>
    ParticleSystemRenderer RainOutside(Vector3 centre, Vector3 box)
    {
        var ps = new GameObject("RainOutside").AddComponent<ParticleSystem>();
        ps.transform.SetParent(root, false);
        ps.transform.localPosition = centre;
        var main = ps.main;
        main.loop = true; main.prewarm = true; main.startLifetime = 1.1f; main.startSpeed = 0f; main.gravityModifier = 0f;
        main.startSize3D = true; main.startSizeX = 0.006f; main.startSizeY = 0.3f; main.startSizeZ = 0.006f;
        main.maxParticles = 2500; main.simulationSpace = ParticleSystemSimulationSpace.World;
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.6f, -0.3f); vel.y = new ParticleSystem.MinMaxCurve(-9.5f, -8f); vel.z = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
        var em = ps.emission; em.rateOverTime = 1800f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = box;
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 2.5f; r.velocityScale = 0.012f;
        var shd = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        var m = new Material(shd != null ? shd : Shader.Find("Sprites/Default"));
        if (shd != null) { m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)RenderQueue.Transparent; }
        m.SetColor("_BaseColor", new Color(0.55f, 0.6f, 0.7f, 0.16f)); m.color = new Color(0.55f, 0.6f, 0.7f, 0.16f);
        r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        ps.Play();
        return r;
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
            AddLight(c + Vector3.down * 0.25f, new Color(1f, 0.64f, 0.38f), 7f, 13f, z == 2.2f, 0.25f);
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
            if (i % 5 == 0) AddLight(p + new Vector3(0, h + 0.3f, 0), new Color(1f, 0.68f, 0.4f), 2.4f, 7f, false, 0.35f);
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
        var mat = SoftParticle();
        if (mat != null) ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
        ps.Play();
    }

    static Texture2D softDot;
    /// <summary>
    /// A material for soft round particles (dust, candle smoke): alpha falls off from the centre like a Gaussian and
    /// is zero at the edge, so no particle reads as a square however close the lens is. The texture is made once.
    /// </summary>
    public static Material SoftParticle()
    {
        var sd = Shader.Find("Sprites/Default");
        if (sd == null) return null;
        if (softDot == null)
        {
            const int N = 64;
            softDot = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = "SoftDot", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f, r2 = dx * dx + dy * dy;
                    float a = Mathf.Exp(-r2 * 4f) * Mathf.Clamp01((1f - Mathf.Sqrt(r2)) * 5f);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            softDot.SetPixels32(px); softDot.Apply(false, true);
        }
        return new Material(sd) { name = "SoftParticle", mainTexture = softDot };
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

        // Light on purpose: the scene is lit to look right, the volume only finishes it. Every value here was a
        // stronger one (exposure +1.15, contrast 30, bloom 0.95 at threshold 0.85, red vignette 0.42, grain 0.4,
        // aberration 0.14), which blew out faces near the candle and crushed everything else to black.
        var ca = prof.Add<ColorAdjustments>(true);
        ca.postExposure.Override(0f);              // written by GalleryExposure (meters the light on the face)
        ca.contrast.Override(6f);
        ca.saturation.Override(-6f);
        ca.colorFilter.Override(new Color(1f, 0.985f, 0.97f));
        var wb = prof.Add<WhiteBalance>(true);     // the film look warms the flat; here the candles are warm enough
        wb.temperature.Override(-9f); wb.tint.Override(1f);
        var smh = prof.Add<ShadowsMidtonesHighlights>(true);      // a hint of cold in the shadows, warm in the flames
        smh.shadows.Override(new Vector4(0.97f, 0.99f, 1.04f, 0f));
        smh.highlights.Override(new Vector4(1.03f, 1.0f, 0.97f, 0f));
        var bloom = prof.Add<Bloom>(true);         // only the flames and the lightning in the glass glow
        bloom.threshold.Override(1.25f); bloom.intensity.Override(0.22f); bloom.scatter.Override(0.55f);
        bloom.tint.Override(new Color(1f, 0.9f, 0.8f));
        var vig = prof.Add<Vignette>(true);
        vig.intensity.Override(0.16f); vig.smoothness.Override(0.5f); vig.color.Override(Color.black);
        var grain = prof.Add<FilmGrain>(true);     // fine, and off on the Performance tier
        grain.type.Override(FilmGrainLookup.Thin1); grain.intensity.Override(0.08f); grain.response.Override(0.85f);
        var chroma = prof.Add<ChromaticAberration>(true);
        chroma.intensity.Override(0f);
        var lens = prof.Add<LensDistortion>(true);
        lens.intensity.Override(0f);
        var mb = prof.Add<MotionBlur>(true);       // camera blur smears the first frame of every hard cut
        mb.intensity.Override(0f);
        profile = prof;
        LookController.Register(prof);
        exposure = go.AddComponent<GalleryExposure>();
        exposure.adjust = ca;
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
    static readonly Color Leather = new Color(0.34f, 0.035f, 0.045f), Gold = new Color(1f, 0.8f, 0.4f), Paper = new Color(0.86f, 0.8f, 0.66f), Ink = new Color(0.16f, 0.05f, 0.04f);

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
        // the first page: printed just under the plane of a page about to be turned (BookLeaf), so a lifted page
        // covers it and carries its own copy of the words
        b.pageText = Txt(b.root, page, new Vector3((T - 0.02f) / 2f + 0.0004f, 0.01f, 0.006f), Quaternion.Euler(0, -90f, 0), 0.019f, Ink);
        return b;
    }

    static TextMesh Txt(Transform p, string s, Vector3 pos, Quaternion rot, float size, Color c)
    {
        var tm = GameKit.Solid(GameKit.Text(p, s, pos, rot, size, c));
        tm.lineSpacing = 0.9f;
        return tm;
    }

    /// <summary>
    /// A single page that can be turned over (it bends like paper as it goes); see BookLeaf. `stack`: pages already
    /// turned (each lands a hair above the last); `words`: what is printed on it, which goes over with it.
    /// </summary>
    public BookLeaf PageLeaf(int stack = 0, string words = null)
    {
        var leaf = BookLeaf.Make(root, new Vector3((T - 0.02f) / 2f + 0.0008f, 0, -W / 2f + 0.006f), H - 0.02f, W - 0.016f, Paper * 1.04f, stack);
        if (!string.IsNullOrEmpty(words))
        {
            // where pageText sits, in the leaf's own terms: across the page at 0.115 m from the hinge, 1 cm up
            leaf.face = Txt(leaf.hinge, words, Vector3.zero, Quaternion.identity, 0.019f, Ink).transform;
            leaf.faceAt = new Vector3(0.0007f, 0.01f, 0.115f);
            leaf.Shape(0f, 0f);
        }
        return leaf;
    }

    /// <summary>Where the hand holds the book (palm frame in the book's own space: +Z fingers, +Y out of the palm).</summary>
    public static Pose Grip(string name)
    {
        switch (name)
        {
            // fingertips hooked over the top of the spine, to tip the book out of the shelf
            case "hook": return new Pose(new Vector3(0f, H / 2f + 0.028f, -W / 2f + 0.032f), Quaternion.LookRotation(new Vector3(0f, -0.3f, 1f), new Vector3(0f, -1f, -0.3f)));
            // the hand round the top of the spine from above: palm on the spine, fingers down it and round onto the
            // back cover, thumb on the front cover. One grip from the shelf to the table: the book hangs from it when
            // carried, and tipped towards him it lies flat with the thumb on top and the fingers under the spine edge.
            case "spineTop": return new Pose(new Vector3(0f, H / 2f - 0.065f, -W / 2f - 0.004f), Quaternion.LookRotation(new Vector3(-0.12f, -1f, 0f), new Vector3(0f, 0f, 1f)));
            default: return new Pose(Vector3.zero, Quaternion.identity);
        }
    }

    /// <summary>Grip on the front cover's free edge (in the cover hinge's space), to lift it open.</summary>
    public static Pose CoverGrip()
    {
        return new Pose(new Vector3(0.016f, 0.05f, W - 0.018f), Quaternion.LookRotation(new Vector3(-0.35f, 0f, 1f), new Vector3(-1f, 0f, -0.35f)));
    }
}

/// <summary>
/// One page that turns: a strip of paper hinged at the spine, built from narrow segments so it bends. A page lifted by
/// its edge curls with the edge leading; a page let go settles flat. turn: 0 lying on the right, 180 on the left.
/// </summary>
public class BookLeaf
{
    public Transform hinge;
    /// <summary>Printed words that go over with the page (placed on its front face each Shape); faceAt: where, at rest.</summary>
    public Transform face; public Vector3 faceAt;
    Mesh mesh; float h, w; int stack; const int Seg = 14;
    Vector3[] verts;
    // the opened front cover lies higher than the page block (its endpaper is ~12 mm above this hinge), so a page
    // that lands on the left rises over it, curving up out of the gutter as real pages do
    const float LandLift = 0.0135f;

    public static BookLeaf Make(Transform book, Vector3 hingeLocal, float height, float width, Color paper, int stack = 0)
    {
        var l = new BookLeaf { h = height, w = width, stack = stack };
        l.hinge = new GameObject("Leaf").transform;
        l.hinge.SetParent(book, false);
        l.hinge.localPosition = hingeLocal;
        var go = new GameObject("Paper");
        go.transform.SetParent(l.hinge, false);
        l.mesh = new Mesh { name = "Leaf" };
        l.verts = new Vector3[(Seg + 1) * 2 * 2];
        var uv = new Vector2[l.verts.Length];
        var tris = new List<int>();
        for (int side = 0; side < 2; side++)
            for (int i = 0; i <= Seg; i++)
            {
                int b = side * (Seg + 1) * 2 + i * 2;
                uv[b] = new Vector2(i / (float)Seg, 0); uv[b + 1] = new Vector2(i / (float)Seg, 1);
                if (i < Seg)
                {
                    if (side == 0) tris.AddRange(new[] { b, b + 1, b + 2, b + 2, b + 1, b + 3 });
                    else tris.AddRange(new[] { b, b + 2, b + 1, b + 2, b + 3, b + 1 });
                }
            }
        l.Shape(0f, 0f);
        l.mesh.uv = uv; l.mesh.SetTriangles(tris, 0);
        l.mesh.RecalculateNormals();
        go.AddComponent<MeshFilter>().sharedMesh = l.mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = GameKit.Lit(paper, 0.12f);
        return l;
    }

    /// <summary>The page at `turn` degrees about the spine, curled by `bend` degrees along its width (edge leading).</summary>
    public void Shape(float turn, float bend)
    {
        // walk along the page from the spine: each segment turns a little more than the last
        Vector3 p = Vector3.zero; float segLen = w / Seg;
        float lift = (LandLift + stack * 0.0006f) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(70f, 180f, turn));
        float faceS = w > 0f ? Mathf.Clamp01(faceAt.z / w) : 0.5f; Vector3 faceP = Vector3.zero, prev = Vector3.zero; float faceA = 0f; bool faceSet = false;
        for (int i = 0; i <= Seg; i++)
        {
            float s = i / (float)Seg;
            float a = (turn + bend * s * s) * Mathf.Deg2Rad;
            if (i > 0) p += new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * segLen;
            Vector3 up = Vector3.right * lift * Mathf.SmoothStep(0f, 1f, s / 0.2f);
            if (i > 0 && !faceSet && s >= faceS)
            {
                float k = Mathf.InverseLerp((i - 1) / (float)Seg, s, faceS);
                faceP = Vector3.Lerp(prev, p + up, k); faceA = a; faceSet = true;
            }
            prev = p + up;
            for (int side = 0; side < 2; side++)
            {
                int b = side * (Seg + 1) * 2 + i * 2;
                Vector3 off = side == 0 ? Vector3.zero : new Vector3(-Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.0007f;     // two faces, a hair apart
                verts[b] = p + up + off + new Vector3(0, -h / 2f, 0);
                verts[b + 1] = p + up + off + new Vector3(0, h / 2f, 0);
            }
        }
        mesh.vertices = verts;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        if (face != null)
        {
            // the words lie on the front face where the page's own frame carries them (rigid: fine for a page in motion)
            if (!faceSet) { faceP = prev; faceA = turn * Mathf.Deg2Rad; }
            Vector3 n = new Vector3(Mathf.Cos(faceA), 0f, -Mathf.Sin(faceA));
            face.localPosition = faceP + n * faceAt.x + Vector3.up * faceAt.y;
            face.localRotation = Quaternion.Euler(0f, faceA * Mathf.Rad2Deg, 0f) * Quaternion.Euler(0f, -90f, 0f);
        }
    }

    /// <summary>A point on the page in world space: u across (0 spine .. 1 edge), v up (0 bottom .. 1 top), at the current shape.</summary>
    public Vector3 Point(float u, float v)
    {
        int i = Mathf.Clamp(Mathf.RoundToInt(u * Seg), 0, Seg);
        Vector3 local = Vector3.Lerp(verts[i * 2], verts[i * 2 + 1], v);
        return hinge.TransformPoint(local);
    }

    /// <summary>The page's orientation at a point (for the fingers that hold it).</summary>
    public Quaternion Frame(float u)
    {
        int i = Mathf.Clamp(Mathf.RoundToInt(u * Seg), 1, Seg);
        Vector3 along = hinge.TransformDirection(verts[i * 2] - verts[(i - 1) * 2]).normalized;
        return Quaternion.LookRotation(along, hinge.TransformDirection(Vector3.up));
    }
}

/// <summary>A warm light that breathes like a flame; level scales it (lighting up, snuffing out), gust makes it gutter.</summary>
public class CandleFlicker : MonoBehaviour
{
    public float full = 2f, level = 1f, dipChance = 0.25f, gust;
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
        float g = gust > 0f ? 1f - gust * (0.35f + 0.5f * Mathf.PerlinNoise(seed + 3f, Time.time * 22f)) : 1f;
        l.intensity = full * level * (0.8f + 0.35f * n) * dip * Mathf.Max(0f, g);
        l.enabled = level > 0.005f;
    }
}

/// <summary>Makes a candle flame shiver and stretch; with gust it bends away along gustDir and gutters.</summary>
public class FlameWobble : MonoBehaviour
{
    public float gust; public Vector3 gustDir = Vector3.forward;
    Vector3 s0; Quaternion r0; float seed;
    void Start() { s0 = transform.localScale; r0 = transform.localRotation; seed = Random.value * 50f; }
    void Update()
    {
        float n = Mathf.PerlinNoise(seed, Time.time * 7f);
        float g = Mathf.Clamp01(gust), shake = g * (Mathf.PerlinNoise(seed + 9f, Time.time * 30f) - 0.5f);
        transform.localScale = new Vector3(s0.x * (0.9f + 0.15f * n + g * 0.3f), s0.y * (0.8f + 0.45f * n) * (1f - g * 0.45f), s0.z * (0.9f + 0.15f * n));
        Vector3 d = transform.parent != null ? transform.parent.InverseTransformDirection(gustDir) : gustDir; d.y = 0;
        transform.localRotation = g > 0.001f && d.sqrMagnitude > 1e-4f
            ? Quaternion.AngleAxis((55f + 25f * shake) * g, Vector3.Cross(Vector3.up, d.normalized)) * r0 : r0;
    }
}
