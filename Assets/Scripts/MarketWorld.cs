using System.Collections.Generic;
using UnityEngine;

public class NpcSpot { public Vector3 pos; public float yaw; }

/// <summary>
/// Act II scenery: a crowded Indian bazaar lane (think Chandni Chowk / Paharganj), built at real-world scale.
///   - lane: 8 m between the shop fronts, 3-4 storey faded buildings, shop plinths, wet dusty tarmac
///   - ground floor shops with rolling shutters and hand-painted Hindi/English signs
///   - balconies, window shutters, desert coolers, water tanks, laundry, tangled wires, an electricity pole
///   - stalls: vegetables, spices (cone piles), sweets, marigold garlands, sarees, bangles, coconuts, fruit cart,
///     balloons, pani puri, shoes, mobile repair, bhutta, newspapers, samosa/jalebi, utensils, toys
///   - a roadside shrine, a gate arch, bunting and string lights, a cow, scooters, cycles, autos
/// The lane runs along +Z (z = -6 .. 84). Shop fronts are at x = +/-4.
/// </summary>
public class MarketWorld : MonoBehaviour
{
    // ---- key positions (world space; this object sits at the origin) ----
    public Vector3 PlayerStart = new Vector3(-0.8f, 0.1f, -3.0f);
    public Vector3 BhaskarPos = new Vector3(0.7f, 0f, 0.5f);
    public Vector3 AutoPos = new Vector3(1.6f, 0f, 1.2f);
    public Vector3 VikramPos = new Vector3(-3.75f, 0.18f, 34f);   // stands on the shop plinth
    public Vector3 VikramStallFront = new Vector3(-2.0f, 0f, 34f);
    public Vector3 ReddyPos = new Vector3(3.75f, 0.18f, 38.0f);
    public Vector3 IdDropPos = new Vector3(2.2f, 0.03f, 40.3f);
    public Vector3 AlleyEnd = new Vector3(11.5f, 0f, 45f);
    public Vector3 SuvStart = new Vector3(0.6f, 0f, 94f);

    public Transform Auto;
    public Transform Suv;
    public readonly List<Light> StreetLights = new List<Light>();
    public readonly List<float> StreetLightBase = new List<float>();
    public readonly List<Material> NeonMats = new List<Material>();
    public Light[] SuvHeadlights;
    public Transform PlateText;
    public readonly List<NpcSpot> Vendors = new List<NpcSpot>();
    public readonly List<NpcSpot> Customers = new List<NpcSpot>();
    public readonly List<Transform> Swayers = new List<Transform>();     // flags, laundry: gently moving in the breeze
    public readonly List<Transform> Spinners = new List<Transform>();    // pinwheels
    public readonly List<Light> FlameLights = new List<Light>();         // fires and diyas: flicker
    readonly HashSet<GameObject> animated = new HashSet<GameObject>();   // excluded from static batching

    const float FX = 4.0f;               // shop-front plane: x = +/-4
    System.Random rnd = new System.Random(2024);

    // ---- palette: faded Indian building paints, bright signboards, produce ----
    static readonly Color[] Paints =
    {
        new Color(0.36f, 0.62f, 0.60f), new Color(0.86f, 0.52f, 0.58f), new Color(0.86f, 0.68f, 0.30f),
        new Color(0.42f, 0.55f, 0.76f), new Color(0.55f, 0.68f, 0.42f), new Color(0.90f, 0.82f, 0.62f),
        new Color(0.72f, 0.42f, 0.32f), new Color(0.62f, 0.50f, 0.72f),
    };
    static readonly Color[] SignBg =
    {
        new Color(0.10f, 0.28f, 0.62f), new Color(0.80f, 0.12f, 0.12f), new Color(0.95f, 0.78f, 0.12f),
        new Color(0.08f, 0.45f, 0.25f), new Color(0.90f, 0.40f, 0.08f), new Color(0.45f, 0.12f, 0.50f),
    };
    static readonly Color[] Flags =
    {
        new Color(1f, 0.55f, 0.1f), new Color(0.98f, 0.98f, 0.95f), new Color(0.15f, 0.65f, 0.3f),
        new Color(0.95f, 0.3f, 0.5f), new Color(1f, 0.85f, 0.15f), new Color(0.2f, 0.5f, 0.9f),
    };
    static readonly string[][] Shops =
    {
        new[] { "SHARMA SWEETS", "शर्मा स्वीट्स" }, new[] { "GUPTA GENERAL STORE", "गुप्ता जनरल स्टोर" },
        new[] { "RAJU TAILORS", "राजू टेलर्स" }, new[] { "MAA DURGA JEWELLERS", "माँ दुर्गा ज्वेलर्स" },
        new[] { "NEW BOMBAY CLOTH HOUSE", "न्यू बॉम्बे क्लॉथ हाउस" }, new[] { "SHREE GANESH PAN BHANDAR", "श्री गणेश पान भंडार" },
        new[] { "KRISHNA MEDICAL STORE", "कृष्णा मेडिकल स्टोर" }, new[] { "HOTEL ANNAPURNA", "होटल अन्नपूर्णा" },
        new[] { "MOBILE & RECHARGE", "मोबाइल रिचार्ज" }, new[] { "BOOK DEPOT", "बुक डिपो" },
        new[] { "SHOE PALACE", "शू पैलेस" }, new[] { "KIRANA & DRY FRUITS", "किराना ड्राई फ्रूट" },
        new[] { "RAMLAL HARDWARE", "रामलाल हार्डवेयर" }, new[] { "STD PCO", "एसटीडी पीसीओ" },
    };

    // ===================================================================== helpers
    static Color Rgb(float r, float g, float b) { return new Color(r, g, b); }
    float R() { return (float)rnd.NextDouble(); }

    GameObject B(Transform p, string n, Vector3 pos, Vector3 size, Color c, bool col = false, float sm = 0.25f, float met = 0f)
    { return GameKit.Box(p, n, pos, size, GameKit.Lit(c, sm, met), col); }
    GameObject Cy(Transform p, string n, Vector3 pos, float r, float h, Color c, float sm = 0.3f, float met = 0f)
    { return GameKit.Cyl(p, n, pos, r, h, GameKit.Lit(c, sm, met)); }
    GameObject Sp(Transform p, string n, Vector3 pos, float d, Color c, float sm = 0.35f)
    { return GameKit.Sphere(p, n, pos, d, GameKit.Lit(c, sm)); }
    GameObject Gl(Transform p, string n, Vector3 pos, Vector3 size, Color c, float intensity = 2f)
    { return GameKit.Box(p, n, pos, size, GameKit.Glow(c * 0.5f, c, intensity)); }

    // everything is a bit brighter than before: it is a lively evening bazaar, not a dark alley
    const float LightBoost = 1.45f;

    Light AddLight(Vector3 pos, Color c, float intensity, float range, bool flame = false)
    {
        var l = GameKit.PointLight(transform, pos, c, intensity * LightBoost, range * 1.2f);
        StreetLights.Add(l);
        StreetLightBase.Add(intensity * LightBoost);
        if (flame) FlameLights.Add(l);
        return l;
    }
    void AddLightMaybe(Vector3 p, Color c, float i, float r, bool yes) { if (yes) AddLight(p, c, i, r); }
    static readonly Color Tungsten = new Color(1f, 0.72f, 0.38f);
    static readonly Color Tube = new Color(0.82f, 0.95f, 1f);

    /// <summary>A thin straight segment between two world points (wires, cords).</summary>
    GameObject Seg(string n, Vector3 a, Vector3 b, float thick, Color c)
    {
        var go = GameKit.Box(transform, n, (a + b) / 2f, new Vector3(thick, thick, Vector3.Distance(a, b)), GameKit.Lit(c, 0.2f));
        go.transform.rotation = Quaternion.LookRotation((b - a).normalized);
        return go;
    }

    /// <summary>Sagging wire made of two segments.</summary>
    void Wire(Vector3 a, Vector3 b, float sag, float thick = 0.02f)
    {
        Vector3 m = (a + b) / 2f + Vector3.down * sag;
        var c = new Color(0.04f, 0.04f, 0.04f);
        Seg("Wire", a, m, thick, c); Seg("Wire", m, b, thick, c);
    }

    static Vector3 Sag(Vector3 a, Vector3 b, float t, float sag)
    {
        return Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
    }

    void Bunting(Vector3 a, Vector3 b, int n, float sag)
    {
        Seg("BuntingCord", a, Sag(a, b, 0.5f, sag), 0.01f, Color.black);
        Seg("BuntingCord", Sag(a, b, 0.5f, sag), b, 0.01f, Color.black);
        for (int i = 1; i < n; i++)
        {
            float t = i / (float)n;
            Vector3 p = Sag(a, b, t, sag) + Vector3.down * 0.15f;
            var f = GameKit.Box(transform, "Flag", p, new Vector3(0.26f, 0.26f, 0.01f), GameKit.Lit(Flags[i % Flags.Length], 0.1f));
            f.transform.rotation = Quaternion.Euler(0, 0, 45f);
            animated.Add(f); Swayers.Add(f.transform);
        }
    }

    void Garland(Vector3 a, Vector3 b, int n, float sag, bool rose = false)
    {
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            Color c = rose ? (i % 3 == 0 ? Rgb(0.9f, 0.2f, 0.4f) : Rgb(0.95f, 0.5f, 0.6f)) : (i % 4 == 3 ? Rgb(0.8f, 0.15f, 0.1f) : (i % 2 == 0 ? Rgb(1f, 0.55f, 0.05f) : Rgb(1f, 0.78f, 0.1f)));
            Sp(transform, "Marigold", Sag(a, b, t, sag), 0.11f, c, 0.2f);
        }
    }

    void StringLights(Vector3 a, Vector3 b, int n, float sag, Color tint, bool coloured)
    {
        Seg("Cord", a, Sag(a, b, 0.5f, sag), 0.01f, Color.black);
        Seg("Cord", Sag(a, b, 0.5f, sag), b, 0.01f, Color.black);
        for (int i = 1; i < n; i++)
        {
            float t = i / (float)n;
            Color c = coloured ? Flags[i % Flags.Length] : tint;
            GameKit.Sphere(transform, "Bulb", Sag(a, b, t, sag) + Vector3.down * 0.06f, 0.1f, GameKit.Glow(c * 0.4f, c, 6f));
        }
    }

    // ======================================================================= BUILD
    public void Generate()
    {
        BuildGround();
        BuildFacades();
        BuildOverhead();
        BuildGate();
        BuildStalls();
        BuildShrine();
        BuildPole();
        BuildVehicles();
        BuildAnimals();
        BuildClutter();
        CombineStatic();
        BuildAuto();
        BuildSuv();
        BuildSkyline();
        var life = gameObject.AddComponent<AmbientLife>();
        life.world = this;
    }

    // ------------------------------------------------------- things that move
    static Material steamMat;
    static Material SteamMaterial()
    {
        if (steamMat != null) return steamMat;
        var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float d = Mathf.Sqrt((x - 15.5f) * (x - 15.5f) + (y - 15.5f) * (y - 15.5f)) / 15.5f;
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d)));
            }
        tex.Apply();
        var sh = Shader.Find("Sprites/Default");
        steamMat = new Material(sh != null ? sh : Shader.Find("Unlit/Transparent")) { mainTexture = tex };
        return steamMat;
    }

    /// <summary>Rising steam / smoke from kettles, woks and grills.</summary>
    void Steam(Vector3 worldPos, float size)
    {
        var go = new GameObject("Steam");
        go.transform.SetParent(transform, false);
        go.transform.position = worldPos;
        go.transform.rotation = Quaternion.Euler(-90f, 0, 0);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true; main.prewarm = true;
        main.startLifetime = 1.9f;
        main.startSpeed = 0.35f * size;
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f * size, 0.2f * size);
        main.startColor = new Color(1f, 1f, 1f, 0.5f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        var em = ps.emission; em.rateOverTime = 9f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 9f; sh.radius = 0.05f;
        var sol = ps.sizeOverLifetime; sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2.4f));
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.45f, 0.2f), new GradientAlphaKey(0, 1) });
        col.color = g;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = SteamMaterial();
    }

    /// <summary>A moving auto-rickshaw for the traffic (no collider, so it never traps the player).</summary>
    /// <summary>Makes a moving vehicle a real physical obstacle (kinematic body + collider), so people cannot walk through it.</summary>
    static void Kinematic(Transform t)
    {
        var rb = t.gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    public Transform SpawnAuto(Vector3 pos, float yaw)
    {
        var t = MakeAuto(pos, yaw, false);
        Kinematic(t);
        return t;
    }

    public Transform SpawnScooter(Vector3 pos, float yaw, Color c)
    {
        var t = Scooter(pos, yaw, c);
        Kinematic(t);
        return t;
    }

    /// <summary>A hand-pulled thela piled with vegetables (local +Z is where the puller stands).</summary>
    public Transform SpawnHandCart(Vector3 pos)
    {
        var c = new GameObject("HandCart").transform;
        c.SetParent(transform, false);
        c.position = pos;
        B(c, "Bed", new Vector3(0, 0.75f, 0), new Vector3(1.0f, 0.1f, 1.9f), Rgb(0.45f, 0.3f, 0.15f), false, 0.2f);
        foreach (float x in new[] { -0.58f, 0.58f })
            Cy(c, "Wheel", new Vector3(x, 0.38f, 0.1f), 0.38f, 0.07f, Rgb(0.06f, 0.06f, 0.06f), 0.3f).transform.localRotation = Quaternion.Euler(0, 0, 90);
        B(c, "Handle", new Vector3(0, 0.62f, 1.55f), new Vector3(0.9f, 0.05f, 0.05f), Rgb(0.4f, 0.28f, 0.15f));
        Pile(c, new Vector3(0, 0.85f, -0.5f), Rgb(0.85f, 0.15f, 0.1f), 16, 0.28f, 0.11f);
        Pile(c, new Vector3(0, 0.85f, 0.15f), Rgb(0.95f, 0.6f, 0.1f), 16, 0.28f, 0.11f);
        Pile(c, new Vector3(0, 0.85f, 0.7f), Rgb(0.25f, 0.65f, 0.2f), 14, 0.26f, 0.12f);
        var cb = c.gameObject.AddComponent<BoxCollider>();
        cb.center = new Vector3(0, 0.9f, 0.1f); cb.size = new Vector3(1.2f, 1.2f, 2.0f);
        Kinematic(c);
        return c;
    }

    /// <summary>Merges all the static scenery into a few big meshes (thousands of props would otherwise be thousands of draw calls).</summary>
    void CombineStatic()
    {
        try
        {
            var list = new List<GameObject>();
            foreach (var mr in GetComponentsInChildren<MeshRenderer>())
            {
                if (mr.GetComponent<TextMesh>() != null) continue;      // text meshes are rebuilt dynamically
                if (animated.Contains(mr.gameObject) || mr.GetComponent<ParticleSystem>() != null) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                list.Add(mr.gameObject);
            }
            if (list.Count > 0) StaticBatchingUtility.Combine(list.ToArray(), gameObject);
        }
        catch (System.Exception e) { Debug.LogWarning("Static batching skipped: " + e.Message); }
    }

    // ------------------------------------------------------------------ ground
    void BuildGround()
    {
        var road = B(transform, "Road", new Vector3(0, -0.1f, 39f), new Vector3(8.2f, 0.2f, 90f), Rgb(0.3f, 0.27f, 0.25f), true, 0.5f);
        GameKit.Surface(road, "asphalt", 2.2f, 1f, 1f, Rgb(0.4f, 0.36f, 0.33f));
        for (int side = -1; side <= 1; side += 2)
        {
            var pl = B(transform, "Plinth", new Vector3(side * 3.65f, 0.09f, 39f), new Vector3(0.7f, 0.18f, 90f), Rgb(0.5f, 0.48f, 0.45f), true, 0.3f);
            GameKit.Surface(pl, "stone", 1.0f, 1f, 1f, Rgb(0.55f, 0.52f, 0.48f));
        }
        var wet = GameKit.Lit(new Color(0.05f, 0.06f, 0.09f), 1f);
        for (int i = 0; i < 12; i++)
            GameKit.Box(transform, "Puddle", new Vector3((R() - 0.5f) * 4.5f, 0.006f, 3 + i * 6.5f + R() * 3f), new Vector3(0.8f + R() * 1.4f, 0.01f, 0.6f + R()), wet);
        GameKit.Box(transform, "Drain", new Vector3(0, 0.002f, 39f), new Vector3(0.18f, 0.01f, 90f), GameKit.Lit(new Color(0.03f, 0.03f, 0.03f), 0.4f));

        B(transform, "Wall_Start", new Vector3(0, 3f, -6.6f), new Vector3(9f, 6f, 1f), Rgb(0.2f, 0.18f, 0.18f), true);
        // the far end of the lane is a gateway onto a dark main road: the SUV drives in through it
        B(transform, "Wall_EndL", new Vector3(-3.4f, 3f, 80.6f), new Vector3(2.2f, 6f, 1f), Rgb(0.16f, 0.15f, 0.18f), true);
        B(transform, "Wall_EndR", new Vector3(3.4f, 3f, 80.6f), new Vector3(2.2f, 6f, 1f), Rgb(0.16f, 0.15f, 0.18f), true);
        B(transform, "Wall_EndTop", new Vector3(0, 4.7f, 80.6f), new Vector3(4.6f, 2.6f, 1f), Rgb(0.16f, 0.15f, 0.18f), false);
        var blocker = new GameObject("GatewayBlocker").AddComponent<BoxCollider>();   // keeps the player in the lane (vehicles are moved by script)
        blocker.transform.SetParent(transform, false);
        blocker.transform.position = new Vector3(0, 1.5f, 81.2f);
        blocker.size = new Vector3(5f, 3f, 0.4f);
        B(transform, "RoadFar", new Vector3(0, -0.1f, 98f), new Vector3(8.2f, 0.2f, 40f), Rgb(0.12f, 0.12f, 0.14f), false, 0.7f);
        AddLight(new Vector3(0, 4.2f, 84f), new Color(0.75f, 0.85f, 1f), 2.6f, 14f);                  // a street lamp outside the gate
        // billboard at the far end of the lane
        B(transform, "Hoarding", new Vector3(0, 5.2f, 79.7f), new Vector3(7.6f, 3.4f, 0.25f), Rgb(0.06f, 0.09f, 0.15f), false, 0.4f);
        GameKit.Text(transform, "CHANDNIPUR RENEWAL PROJECT", new Vector3(0, 5.9f, 79.5f), Quaternion.identity, 0.42f, new Color(0.75f, 0.9f, 1f));
        GameKit.Text(transform, "A NEW CITY IS COMING", new Vector3(0, 4.7f, 79.5f), Quaternion.identity, 0.32f, new Color(1f, 0.8f, 0.4f));
        AddLight(new Vector3(0, 3.2f, 77f), new Color(0.6f, 0.8f, 1f), 2.2f, 9f);
    }

    // --------------------------------------------------------------- facades
    void BuildFacades()
    {
        for (int side = -1; side <= 1; side += 2)
        {
            for (int k = 0; k <= 14; k++)
            {
                float zc = -6f + k * 6f + 3f;
                if (side == 1 && k == 8) { BuildAlley(zc); continue; }

                int floors = 3 + (R() < 0.5f ? 1 : 0);
                float h = 3.5f + (floors - 1) * 3.2f + 0.7f;
                Color paint = Paints[rnd.Next(Paints.Length)];
                var body = B(transform, "Building", new Vector3(side * (FX + 4f), h / 2f, zc), new Vector3(8f, h, 6f), paint * 0.7f, true, 0.1f);
                GameKit.SurfaceZY(body, "facade", 3.2f, new Color(Mathf.Min(1f, paint.r * 1.15f), Mathf.Min(1f, paint.g * 1.15f), Mathf.Min(1f, paint.b * 1.15f)), 1f, 1f);
                BuildFacade(side, zc, h, floors, paint, k);
            }
        }
    }

    void BuildAlley(float zc)
    {
        B(transform, "AlleyFloor", new Vector3(8.5f, -0.1f, zc), new Vector3(9f, 0.2f, 6f), Rgb(0.1f, 0.09f, 0.09f), true, 0.7f);
        B(transform, "AlleyEnd", new Vector3(13.2f, 5f, zc), new Vector3(1f, 10f, 6f), Rgb(0.2f, 0.18f, 0.17f), true);
        AddLight(new Vector3(8f, 3f, zc), Tungsten, 2.4f, 7f);
        Cy(transform, "Bin", new Vector3(7f, 0.45f, zc + 2.2f), 0.3f, 0.9f, Rgb(0.12f, 0.35f, 0.18f));
        for (int i = 0; i < 3; i++) B(transform, "Crate", new Vector3(9.5f, 0.2f + i * 0.4f, zc - 2f), new Vector3(0.6f, 0.38f, 0.45f), Rgb(0.2f, 0.4f, 0.8f), true);
    }

    void BuildFacade(int side, float zc, float h, int floors, Color paint, int k)
    {
        Quaternion face = Quaternion.Euler(0, side < 0 ? -90f : 90f, 0);   // text / panels face the lane
        System.Func<float, float, float, Vector3> P = (inward, y, dz) => new Vector3(side * (FX - inward), y, zc + dz);
        var dark = Rgb(0.07f, 0.06f, 0.06f);

        // ---------- ground-floor shop
        bool open = R() < 0.7f;
        if (open)
        {
            bool tube = R() < 0.5f;
            Gl(transform, "ShopInterior", P(-0.02f, 1.35f, 0), new Vector3(0.05f, 2.6f, 4.6f), tube ? Tube : Tungsten, 1.6f);
            for (int i = 0; i < 3; i++)   // shelves with goods
            {
                B(transform, "Shelf", P(0.12f, 0.7f + i * 0.65f, 0), new Vector3(0.28f, 0.04f, 4.4f), Rgb(0.3f, 0.2f, 0.12f));
                for (int j = 0; j < 9; j++)
                    B(transform, "Goods", P(0.12f, 0.82f + i * 0.65f, -2f + j * 0.5f), new Vector3(0.18f, 0.18f, 0.3f), Color.HSVToRGB(R(), 0.55f, 0.85f), false, 0.4f);
            }
            AddLight(P(0.6f, 2.5f, 0), tube ? Tube : Tungsten, 1.8f, 5.5f);
        }
        else
        {
            B(transform, "Shutter", P(0.04f, 1.4f, 0), new Vector3(0.1f, 2.8f, 4.6f), Rgb(0.34f, 0.37f, 0.4f), false, 0.4f, 0.5f);
            for (int i = 0; i < 9; i++) B(transform, "Rib", P(0.1f, 0.25f + i * 0.3f, 0), new Vector3(0.04f, 0.03f, 4.6f), Rgb(0.5f, 0.52f, 0.55f), false, 0.4f, 0.5f);
        }
        foreach (float dz in new[] { -2.4f, 2.4f }) B(transform, "ShopPost", P(0.12f, 1.45f, dz), new Vector3(0.3f, 2.9f, 0.25f), Rgb(0.7f, 0.62f, 0.5f), false, 0.2f);
        B(transform, "RollerBox", P(0.15f, 2.95f, 0), new Vector3(0.3f, 0.3f, 4.8f), Rgb(0.4f, 0.4f, 0.42f), false, 0.4f, 0.5f);

        // signboard
        var sc = SignBg[rnd.Next(SignBg.Length)];
        var names = Shops[(k * 2 + (side > 0 ? 5 : 0)) % Shops.Length];
        GameKit.Box(transform, "SignBoard", P(0.18f, 3.55f, 0), new Vector3(0.14f, 0.85f, 4.5f), GameKit.Glow(sc, sc, 0.7f));   // lit signboard
        B(transform, "SignFrame", P(0.14f, 3.55f, 0), new Vector3(0.1f, 0.95f, 4.6f), Rgb(0.9f, 0.85f, 0.7f), false, 0.3f);
        Color tc = (sc.r + sc.g + sc.b) > 1.6f ? Rgb(0.08f, 0.08f, 0.1f) : Rgb(1f, 0.97f, 0.85f);
        GameKit.Sign(transform, names[0], names[1], P(0.27f, 3.55f, 0), face, 0.34f, 4.2f, tc);

        // ---------- upper floors
        for (int f = 1; f < floors; f++)
        {
            float yb = 3.8f + (f - 1) * 3.2f;
            B(transform, "FloorBand", P(0.2f, yb - 0.1f, 0), new Vector3(0.45f, 0.12f, 5.9f), paint * 0.6f, false, 0.1f);
            Color leaf = R() < 0.5f ? Rgb(0.12f, 0.45f, 0.35f) : Rgb(0.2f, 0.35f, 0.65f);
            for (int w = 0; w < 2; w++)
            {
                float dz = w == 0 ? -1.65f : 1.65f;
                bool lit = R() < 0.55f;
                B(transform, "WinFrame", P(0.05f, yb + 1.3f, dz), new Vector3(0.14f, 1.7f, 1.15f), Rgb(0.25f, 0.17f, 0.1f));
                if (lit) Gl(transform, "WinGlow", P(0.0f, yb + 1.3f, dz), new Vector3(0.06f, 1.45f, 0.95f), R() < 0.6f ? Tungsten : Tube, 1.4f);
                else B(transform, "WinDark", P(0.0f, yb + 1.3f, dz), new Vector3(0.06f, 1.45f, 0.95f), dark, false, 0.8f);
                for (int s = -1; s <= 1; s += 2)   // louvred shutters
                    B(transform, "Shutter", P(0.14f, yb + 1.3f, dz + s * 0.31f), new Vector3(0.05f, 1.45f, 0.48f), leaf, false, 0.2f);
                for (int g = 0; g < 4; g++)         // iron grill
                    B(transform, "Grill", P(0.2f, yb + 1.3f, dz - 0.42f + g * 0.28f), new Vector3(0.025f, 1.5f, 0.025f), dark, false, 0.4f, 0.6f);
                B(transform, "Sunshade", P(0.32f, yb + 2.25f, dz), new Vector3(0.6f, 0.05f, 1.3f), paint * 0.5f);
            }
            float roll = R();
            if (roll < 0.45f) Balcony(side, zc, yb);
            else if (roll < 0.7f)        // desert cooler
            {
                B(transform, "Cooler", P(0.5f, yb + 0.5f, 0.2f), new Vector3(0.6f, 0.9f, 0.7f), Rgb(0.2f, 0.45f, 0.4f), false, 0.4f, 0.5f);
                Cy(transform, "CoolerFan", P(0.82f, yb + 0.55f, 0.2f), 0.22f, 0.04f, Rgb(0.1f, 0.1f, 0.1f)).transform.localRotation = Quaternion.Euler(0, 0, 90);
            }
            if (R() < 0.35f)             // washing line
            {
                float y = yb + 2.0f;
                Seg("Line", P(0.9f, y, -2.8f), P(0.9f, y, 2.8f), 0.012f, Rgb(0.7f, 0.7f, 0.6f));
                for (int c = 0; c < 4; c++)
                {
                    var cl = B(transform, "Cloth", P(0.9f, y - 0.3f, -2f + c * 1.3f), new Vector3(0.03f, 0.6f, 0.5f), Color.HSVToRGB(R(), 0.6f, 0.85f), false, 0.1f);
                    animated.Add(cl); Swayers.Add(cl.transform);
                }
            }
        }
        // drain pipes + meter box
        foreach (float dz in new[] { -2.95f, 2.95f })
            Cy(transform, "Pipe", P(0.1f, h / 2f, dz), 0.05f, h, Rgb(0.08f, 0.08f, 0.08f), 0.3f, 0.6f);
        B(transform, "MeterBox", P(0.15f, 2.1f, 2.7f), new Vector3(0.18f, 0.4f, 0.3f), Rgb(0.35f, 0.4f, 0.35f));

        // roof: parapet, water tank, antenna
        B(transform, "Parapet", new Vector3(side * (FX + 0.12f), h + 0.35f, zc), new Vector3(0.28f, 0.7f, 6f), paint * 0.7f, false, 0.1f);
        float tz = (R() - 0.5f) * 3.5f;
        Cy(transform, "WaterTank", new Vector3(side * (FX + 2.0f), h + 0.65f, zc + tz), 0.55f, 1.1f, Rgb(0.05f, 0.05f, 0.06f), 0.4f);
        Cy(transform, "TankLid", new Vector3(side * (FX + 2.0f), h + 1.2f, zc + tz), 0.3f, 0.1f, Rgb(0.07f, 0.07f, 0.08f), 0.4f);
        if (R() < 0.5f) Seg("Antenna", new Vector3(side * (FX + 3.5f), h, zc - 2f), new Vector3(side * (FX + 3.5f), h + 2.4f, zc - 2f), 0.04f, Rgb(0.2f, 0.2f, 0.2f));
    }

    void Balcony(int side, float zc, float yb)
    {
        var conc = Rgb(0.55f, 0.52f, 0.48f);
        var rail = R() < 0.5f ? Rgb(0.05f, 0.25f, 0.2f) : Rgb(0.08f, 0.08f, 0.1f);
        B(transform, "BalconySlab", new Vector3(side * (FX - 0.65f), yb - 0.05f, zc), new Vector3(1.3f, 0.12f, 3.0f), conc);
        B(transform, "BalconyRail", new Vector3(side * (FX - 1.28f), yb + 0.95f, zc), new Vector3(0.05f, 0.06f, 3.0f), rail, false, 0.3f, 0.6f);
        B(transform, "BalconyRail", new Vector3(side * (FX - 1.28f), yb + 0.1f, zc), new Vector3(0.05f, 0.06f, 3.0f), rail, false, 0.3f, 0.6f);
        for (int i = 0; i < 10; i++)
            B(transform, "Baluster", new Vector3(side * (FX - 1.28f), yb + 0.5f, zc - 1.35f + i * 0.3f), new Vector3(0.03f, 0.9f, 0.03f), rail, false, 0.3f, 0.6f);
        for (int i = 0; i < 2; i++)   // flower pots
        {
            Cy(transform, "Pot", new Vector3(side * (FX - 1.1f), yb + 0.25f, zc - 0.8f + i * 1.6f), 0.14f, 0.3f, Rgb(0.65f, 0.3f, 0.15f));
            Sp(transform, "Plant", new Vector3(side * (FX - 1.1f), yb + 0.6f, zc - 0.8f + i * 1.6f), 0.4f, Rgb(0.15f, 0.5f, 0.2f));
        }
    }

    // ------------------------------------------------------------- overhead
    void BuildOverhead()
    {
        float[] bz = { 6f, 15f, 24f, 33f, 42f, 51f, 60f, 69f, 77f };
        for (int i = 0; i < bz.Length; i++)
        {
            float z = bz[i];
            Bunting(new Vector3(-3.9f, 4.4f, z), new Vector3(3.9f, 4.4f, z), 22, 0.5f);
            StringLights(new Vector3(-3.9f, 4.1f, z + 1.6f), new Vector3(3.9f, 4.1f, z + 1.6f), 18, 0.55f, new Color(1f, 0.85f, 0.55f), i % 3 == 1);
            if (i % 3 == 0) AddLight(new Vector3(0, 3.6f, z + 1.6f), Tungsten, 2.6f, 8f);
            if (i % 2 == 0)
            {   // lane-crossing tarpaulin, half-width, tilted
                float x0 = i % 4 == 0 ? -3.9f : 0.2f;
                Color tc = SignBg[(i + 2) % SignBg.Length];
                var tarp = B(transform, "Tarp", new Vector3(x0 + 1.85f, 3.9f, z + 0.5f), new Vector3(3.7f, 0.03f, 2.4f), tc, false, 0.1f);
                tarp.transform.rotation = Quaternion.Euler(0, 0, (x0 < 0 ? 5f : -5f));
            }
        }
        Garland(new Vector3(-3.9f, 3.3f, 20f), new Vector3(3.9f, 3.3f, 20f), 30, 0.45f);
        Garland(new Vector3(-3.9f, 3.3f, 55f), new Vector3(3.9f, 3.3f, 55f), 30, 0.45f, true);
        // cable bundles along the lane
        for (int i = 0; i < 5; i++)
        {
            float x = -3f + i * 1.5f;
            Wire(new Vector3(x, 6.6f + R() * 0.8f, -4f), new Vector3(x + (R() - 0.5f), 6.4f + R() * 0.6f, 40f), 0.8f);
            Wire(new Vector3(x, 6.4f + R() * 0.8f, 40f), new Vector3(x + (R() - 0.5f), 6.2f + R() * 0.6f, 80f), 0.8f);
        }
        for (int i = 0; i < 10; i++)
            Wire(new Vector3(-FX, 5.5f + R() * 3f, 3 + i * 7.5f), new Vector3(FX, 5.5f + R() * 3f, 3 + i * 7.5f + R() * 4f), 0.5f);
    }

    // ----------------------------------------------------------- gate arch
    void BuildGate()
    {
        float z = 9f;
        var pillar = Rgb(0.85f, 0.7f, 0.45f);
        B(transform, "GatePillarL", new Vector3(-3.45f, 2.7f, z), new Vector3(0.7f, 5.4f, 0.7f), pillar, true, 0.2f);
        B(transform, "GatePillarR", new Vector3(3.45f, 2.7f, z), new Vector3(0.7f, 5.4f, 0.7f), pillar, true, 0.2f);
        B(transform, "GateBeam", new Vector3(0, 5.35f, z), new Vector3(8f, 1.1f, 0.6f), Rgb(0.7f, 0.2f, 0.15f), false, 0.2f);
        B(transform, "GateTop", new Vector3(0, 6.05f, z), new Vector3(6f, 0.4f, 0.5f), Rgb(0.95f, 0.75f, 0.2f), false, 0.3f);
        GameKit.Sign(transform, "CHANDNI CHOWK BAZAAR", "चांदनी चौक बाज़ार", new Vector3(0, 5.35f, z - 0.33f), Quaternion.identity, 0.42f, 7f, Rgb(1f, 0.92f, 0.65f));
        GameKit.Sign(transform, "WELCOME", "स्वागतम्", new Vector3(0, 6.05f, z - 0.28f), Quaternion.identity, 0.17f, 3f, Rgb(0.3f, 0.1f, 0.05f));
        Garland(new Vector3(-3.1f, 4.8f, z - 0.4f), new Vector3(3.1f, 4.8f, z - 0.4f), 26, 0.7f);
        for (int i = 0; i < 9; i++)   // LED border
            Gl(transform, "GateLED", new Vector3(-3.6f + i * 0.9f, 4.78f, z - 0.34f), new Vector3(0.12f, 0.12f, 0.05f), Flags[i % Flags.Length], 4f);
        AddLight(new Vector3(0, 4.4f, z - 1.2f), Tungsten, 3f, 9f);
    }

    // ------------------------------------------------------------------ stalls
    void BuildStalls()
    {
        // left side (x = -2.95, facing +x)
        Stall(-1, 11f, "veg"); Stall(-1, 16.5f, "spice"); Stall(-1, 23f, "sweets"); Stall(-1, 28.5f, "garland");
        VikramStall();
        Stall(-1, 40f, "saree"); Stall(-1, 47f, "bangles"); Stall(-1, 53f, "coconut"); Stall(-1, 59f, "fruitcart");
        Stall(-1, 65f, "balloons"); Stall(-1, 72f, "utensils");
        // right side (x = +2.95, facing -x)
        Stall(1, 7f, "panipuri"); Stall(1, 20f, "shoes"); Stall(1, 26f, "mobile"); Stall(1, 32f, "corn");
        Stall(1, 38f, "news"); Stall(1, 52f, "samosa"); Stall(1, 58f, "veg"); Stall(1, 64f, "pushcart");
        Stall(1, 70f, "toys"); Stall(1, 76f, "spice");
    }

    Transform StallBase(int side, float z, string kind, Color tarp, bool vendor, bool customer)
    {
        var st = new GameObject("Stall_" + kind).transform;
        st.SetParent(transform, false);
        float yaw = side < 0 ? 90f : -90f;
        st.position = new Vector3(side * 2.95f, 0, z);
        st.rotation = Quaternion.Euler(0, yaw, 0);                 // local +Z points into the lane

        var wood = Rgb(0.5f, 0.34f, 0.2f);
        var ctr = B(st, "Counter", new Vector3(0, 0.42f, 0), new Vector3(2.2f, 0.85f, 0.8f), wood, true, 0.2f);
        GameKit.Surface(ctr, "wood", 0.9f, 1f, 1f, wood);
        B(st, "Top", new Vector3(0, 0.87f, 0), new Vector3(2.3f, 0.04f, 0.9f), Rgb(0.45f, 0.4f, 0.35f), false, 0.3f);
        foreach (float x in new[] { -1.1f, 1.1f })
        {
            B(st, "Pole", new Vector3(x, 1.25f, 0.45f), new Vector3(0.05f, 2.5f, 0.05f), Rgb(0.35f, 0.25f, 0.15f));
            B(st, "PoleB", new Vector3(x, 1.25f, -0.45f), new Vector3(0.05f, 2.5f, 0.05f), Rgb(0.35f, 0.25f, 0.15f));
        }
        var aw = B(st, "Awning", new Vector3(0, 2.52f, 0.0f), new Vector3(2.6f, 0.04f, 1.7f), tarp, false, 0.1f);
        aw.transform.localRotation = Quaternion.Euler(-9f, 0, 0);
        for (int i = 0; i < 5; i++)
        {
            var s = B(st, "Stripe", new Vector3(-1.1f + i * 0.55f, 2.54f, 0.0f), new Vector3(0.16f, 0.045f, 1.7f), Rgb(0.97f, 0.95f, 0.9f), false, 0.1f);
            s.transform.localRotation = Quaternion.Euler(-9f, 0, 0);
        }
        // hanging bulb (tungsten) + light
        GameKit.Sphere(st, "Bulb", new Vector3(0, 2.2f, 0.35f), 0.14f, GameKit.Glow(Color.white, new Color(1f, 0.78f, 0.4f), 5f));
        AddLight(st.TransformPoint(new Vector3(0, 2.0f, 0.5f)), Tungsten, 2.2f, 5f);

        if (vendor) { Vector3 vp = st.TransformPoint(new Vector3(0.2f, 0, -0.85f)); Vendors.Add(new NpcSpot { pos = new Vector3(vp.x, 0.18f, vp.z), yaw = yaw }); }
        if (customer) Customers.Add(new NpcSpot { pos = st.TransformPoint(new Vector3(-0.4f, 0, 1.15f)), yaw = yaw + 180f });
        return st;
    }

    void Pile(Transform p, Vector3 center, Color c, int count, float spread, float d)
    {
        for (int i = 0; i < count; i++)
        {
            float a = R() * Mathf.PI * 2f, r = R() * spread;
            float h = (1f - r / Mathf.Max(0.01f, spread)) * spread * 0.9f;
            Sp(p, "Item", center + new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r), d, c * (0.85f + R() * 0.3f), 0.5f);
        }
    }

    void Stall(int side, float z, string kind)
    {
        Color[] tarps = { Rgb(0.1f, 0.3f, 0.7f), Rgb(0.9f, 0.4f, 0.08f), Rgb(0.1f, 0.5f, 0.3f), Rgb(0.8f, 0.12f, 0.15f), Rgb(0.95f, 0.78f, 0.15f) };
        bool vend = kind != "pushcart" && kind != "fruitcart" && kind != "balloons" && kind != "news";
        var st = StallBase(side, z, kind, tarps[rnd.Next(tarps.Length)], vend, R() < 0.55f);
        float top = 0.9f;
        switch (kind)
        {
            case "veg":
                for (int i = 0; i < 4; i++) B(st, "Crate", new Vector3(-0.8f + i * 0.55f, top + 0.1f, 0.05f), new Vector3(0.5f, 0.18f, 0.55f), Rgb(0.55f, 0.4f, 0.25f), false, 0.2f);
                Pile(st, new Vector3(-0.8f, top + 0.2f, 0.05f), Rgb(0.85f, 0.12f, 0.1f), 14, 0.2f, 0.1f);   // tomatoes
                Pile(st, new Vector3(-0.25f, top + 0.2f, 0.05f), Rgb(0.6f, 0.25f, 0.35f), 14, 0.2f, 0.1f);  // onions
                Pile(st, new Vector3(0.3f, top + 0.2f, 0.05f), Rgb(0.6f, 0.45f, 0.3f), 14, 0.2f, 0.1f);    // potatoes
                Pile(st, new Vector3(0.85f, top + 0.2f, 0.05f), Rgb(0.2f, 0.6f, 0.2f), 12, 0.2f, 0.11f);   // greens
                for (int i = 0; i < 4; i++) Cy(st, "Basket", new Vector3(-0.8f + i * 0.55f, 0.15f, 0.7f), 0.2f, 0.3f, Rgb(0.7f, 0.55f, 0.3f), 0.1f);
                GameKit.Sign(st, "SABZI", "सब्ज़ी", new Vector3(0, 2.2f, 0.88f), Quaternion.Euler(0, 180f, 0), 0.22f, 1.2f, Rgb(1f, 0.95f, 0.8f));
                break;
            case "spice":
                Color[] sp = { Rgb(0.8f, 0.1f, 0.05f), Rgb(0.95f, 0.7f, 0.05f), Rgb(0.45f, 0.4f, 0.15f), Rgb(0.5f, 0.28f, 0.1f), Rgb(0.15f, 0.12f, 0.1f), Rgb(0.4f, 0.5f, 0.15f) };
                for (int i = 0; i < 6; i++)
                {
                    float x = -0.9f + i * 0.36f;
                    Cy(st, "Tray", new Vector3(x, top + 0.03f, 0.05f), 0.16f, 0.05f, Rgb(0.6f, 0.6f, 0.62f), 0.7f, 0.8f);
                    GameKit.Cone(st, "SpicePile", new Vector3(x, top + 0.05f, 0.05f), 0.15f, 0.2f, GameKit.Lit(sp[i], 0.3f));
                }
                for (int i = 0; i < 3; i++)
                {
                    Cy(st, "Sack", new Vector3(-0.8f + i * 0.8f, 0.3f, 0.75f), 0.28f, 0.6f, Rgb(0.8f, 0.72f, 0.55f), 0.1f);
                    GameKit.Cone(st, "SackPile", new Vector3(-0.8f + i * 0.8f, 0.58f, 0.75f), 0.25f, 0.22f, GameKit.Lit(sp[(i * 2) % 6], 0.3f));
                }
                B(st, "ScaleBar", new Vector3(0.9f, 1.55f, 0.1f), new Vector3(0.6f, 0.025f, 0.025f), Rgb(0.6f, 0.5f, 0.2f), false, 0.7f, 0.8f);
                GameKit.Sign(st, "MASALA", "मसाले", new Vector3(0, 2.2f, 0.88f), Quaternion.Euler(0, 180f, 0), 0.22f, 1.2f, Rgb(1f, 0.9f, 0.6f));
                break;
            case "sweets":
                Gl(st, "GlassCase", new Vector3(0, top + 0.3f, 0.1f), new Vector3(2.0f, 0.5f, 0.55f), Rgb(0.8f, 0.9f, 0.95f), 0.5f);
                Color[] mc = { Rgb(0.95f, 0.55f, 0.1f), Rgb(0.98f, 0.97f, 0.9f), Rgb(0.95f, 0.8f, 0.2f), Rgb(0.9f, 0.45f, 0.55f), Rgb(0.45f, 0.28f, 0.12f) };
                for (int r2 = 0; r2 < 2; r2++)
                    for (int i = 0; i < 7; i++)
                        Sp(st, "Mithai", new Vector3(-0.85f + i * 0.28f, top + 0.15f + r2 * 0.25f, 0.1f), 0.12f, mc[(i + r2) % mc.Length], 0.5f);
                Cy(st, "Kadhai", new Vector3(0.7f, top + 0.12f, -0.2f), 0.4f, 0.2f, Rgb(0.04f, 0.04f, 0.04f), 0.5f, 0.7f);
                GameKit.Cyl(st, "Jalebi", new Vector3(0.7f, top + 0.2f, -0.2f), 0.34f, 0.03f, GameKit.Glow(Rgb(0.8f, 0.4f, 0.05f), Rgb(1f, 0.5f, 0.05f), 1.5f));
                Steam(st.TransformPoint(new Vector3(0.7f, top + 0.3f, -0.2f)), 1.1f);
                GameKit.Sign(st, "MITHAI", "मिठाई", new Vector3(0, 2.2f, 0.88f), Quaternion.Euler(0, 180f, 0), 0.25f, 1.4f, Rgb(1f, 0.9f, 0.5f));
                break;
            case "garland":
                for (int i = 0; i < 3; i++)
                {
                    Cy(st, "Tub", new Vector3(-0.7f + i * 0.7f, top + 0.12f, 0.1f), 0.28f, 0.25f, Rgb(0.2f, 0.45f, 0.75f), 0.4f);
                    Pile(st, new Vector3(-0.7f + i * 0.7f, top + 0.25f, 0.1f), i == 2 ? Rgb(0.9f, 0.25f, 0.45f) : (i == 1 ? Rgb(1f, 0.78f, 0.1f) : Rgb(1f, 0.5f, 0.05f)), 22, 0.24f, 0.12f);
                }
                for (int i = 0; i < 6; i++)
                    Garland(st.TransformPoint(new Vector3(-1.0f + i * 0.4f, 2.4f, 0.5f)), st.TransformPoint(new Vector3(-1.0f + i * 0.4f, 1.0f, 0.55f)), 12, 0.0f, i == 4);
                break;
            case "saree":
                for (int i = 0; i < 9; i++)
                    B(st, "Saree", new Vector3(-1.0f + i * 0.25f, 1.55f, 0.25f), new Vector3(0.22f, 1.6f, 0.03f), Color.HSVToRGB(R(), 0.7f, 0.9f), false, 0.1f);
                B(st, "Rod", new Vector3(0, 2.35f, 0.25f), new Vector3(2.4f, 0.04f, 0.04f), Rgb(0.4f, 0.3f, 0.2f));
                for (int i = 0; i < 4; i++) B(st, "Fold", new Vector3(-0.8f + i * 0.5f, top + 0.12f, 0.05f), new Vector3(0.4f, 0.2f, 0.5f), Color.HSVToRGB(R(), 0.6f, 0.85f), false, 0.1f);
                break;
            case "bangles":
                for (int s2 = 0; s2 < 5; s2++)
                {
                    float x = -0.9f + s2 * 0.45f;
                    Cy(st, "BangleRod", new Vector3(x, top + 0.3f, 0.15f), 0.015f, 0.6f, Rgb(0.4f, 0.3f, 0.2f));
                    for (int r2 = 0; r2 < 9; r2++)
                        Cy(st, "Bangle", new Vector3(x, top + 0.06f + r2 * 0.055f, 0.15f), 0.07f, 0.012f, Color.HSVToRGB(R(), 0.85f, 0.9f), 0.6f);
                }
                B(st, "Mirror", new Vector3(0, 1.8f, -0.3f), new Vector3(0.7f, 0.5f, 0.02f), Rgb(0.7f, 0.85f, 0.9f), false, 0.95f, 0.8f);
                break;
            case "coconut":
                int[] layer = { 10, 6, 3, 1 };
                for (int l = 0; l < 4; l++)
                    for (int i = 0; i < layer[l]; i++)
                    {
                        float a = i / (float)layer[l] * Mathf.PI * 2f, rr = (3 - l) * 0.11f;
                        Sp(st, "Coconut", new Vector3(-0.4f + Mathf.Cos(a) * rr, top + 0.1f + l * 0.15f, 0.1f + Mathf.Sin(a) * rr), 0.2f, Rgb(0.4f, 0.6f, 0.2f), 0.4f);
                    }
                Cy(st, "Log", new Vector3(0.7f, top + 0.15f, 0.1f), 0.2f, 0.3f, Rgb(0.45f, 0.3f, 0.18f), 0.1f);
                GameKit.Sign(st, "NARIYAL PAANI", "नारियल पानी", new Vector3(0, 2.2f, 0.88f), Quaternion.Euler(0, 180f, 0), 0.22f, 1.6f, Rgb(0.9f, 1f, 0.7f));
                break;
            case "fruitcart":
                B(st, "CartBed", new Vector3(0, 0.75f, 0.2f), new Vector3(1.9f, 0.1f, 0.9f), Rgb(0.45f, 0.3f, 0.15f), true, 0.2f);
                foreach (float x in new[] { -0.95f, 0.95f })
                    Cy(st, "CartWheel", new Vector3(x, 0.38f, 0.2f), 0.38f, 0.07f, Rgb(0.08f, 0.08f, 0.08f), 0.3f).transform.localRotation = Quaternion.Euler(0, 0, 90);
                B(st, "Handle", new Vector3(0, 0.6f, -0.9f), new Vector3(1.4f, 0.05f, 0.05f), Rgb(0.4f, 0.28f, 0.15f));
                Pile(st, new Vector3(-0.6f, 0.85f, 0.2f), Rgb(0.85f, 0.1f, 0.1f), 14, 0.24f, 0.1f);   // apples
                Pile(st, new Vector3(0.0f, 0.85f, 0.2f), Rgb(1f, 0.55f, 0.05f), 14, 0.24f, 0.1f);      // oranges
                for (int i = 0; i < 7; i++)                                                              // bananas
                    Cy(st, "Banana", new Vector3(0.6f + (i % 3) * 0.08f, 0.9f + (i / 3) * 0.06f, 0.2f), 0.035f, 0.22f, Rgb(0.98f, 0.85f, 0.2f), 0.4f).transform.localRotation = Quaternion.Euler(80f, 0, 0);
                Sp(st, "Watermelon", new Vector3(-0.05f, 1.15f, 0.55f), 0.38f, Rgb(0.1f, 0.4f, 0.15f));
                break;
            case "balloons":
                B(st, "Stand", new Vector3(0, 0.6f, 0.2f), new Vector3(0.9f, 0.05f, 0.5f), Rgb(0.4f, 0.3f, 0.2f));
                for (int i = 0; i < 24; i++)
                {
                    float a = R() * Mathf.PI * 2f, r2 = R() * 0.5f;
                    var bp = new Vector3(Mathf.Cos(a) * r2, 2.0f + R() * 0.8f, 0.2f + Mathf.Sin(a) * r2 * 0.6f);
                    GameKit.Sphere(st, "Balloon", bp, 0.3f, GameKit.Lit(Color.HSVToRGB(R(), 0.85f, 0.95f), 0.9f));
                    Seg("String", st.TransformPoint(bp + Vector3.down * 0.15f), st.TransformPoint(new Vector3(0, 0.65f, 0.2f)), 0.005f, Rgb(0.9f, 0.9f, 0.9f));
                }
                break;
            case "panipuri":
                B(st, "CartBody", new Vector3(0, 0.45f, 0), new Vector3(1.3f, 0.9f, 0.8f), Rgb(0.1f, 0.35f, 0.72f), true, 0.3f);
                Gl(st, "PuriBox", new Vector3(-0.2f, 1.15f, 0.05f), new Vector3(0.7f, 0.45f, 0.5f), Rgb(0.85f, 0.9f, 0.95f), 0.4f);
                Pile(st, new Vector3(-0.2f, 1.05f, 0.05f), Rgb(0.8f, 0.55f, 0.25f), 20, 0.22f, 0.09f);
                Cy(st, "WaterPot", new Vector3(0.45f, 1.1f, 0.0f), 0.17f, 0.45f, Rgb(0.6f, 0.4f, 0.2f), 0.5f);
                GameKit.Sign(st, "PANI PURI", "पानी पूरी", new Vector3(0, 2.15f, 0.88f), Quaternion.Euler(0, 180f, 0), 0.24f, 1.6f, Rgb(1f, 0.95f, 0.6f));
                break;
            case "shoes":
                B(st, "Mat", new Vector3(0, 0.015f, 0.9f), new Vector3(2.4f, 0.03f, 1.6f), Rgb(0.5f, 0.15f, 0.12f), false, 0.1f);
                for (int r2 = 0; r2 < 3; r2++)
                    for (int i = 0; i < 8; i++)
                        B(st, "Shoe", new Vector3(-1.0f + i * 0.28f, 0.06f, 0.35f + r2 * 0.4f), new Vector3(0.12f, 0.08f, 0.27f), Color.HSVToRGB(R(), 0.5f, R() < 0.5f ? 0.3f : 0.8f), false, 0.3f);
                for (int i = 0; i < 6; i++) B(st, "HangShoe", new Vector3(-0.9f + i * 0.35f, 1.9f, 0.4f), new Vector3(0.11f, 0.07f, 0.26f), Color.HSVToRGB(R(), 0.6f, 0.8f), false, 0.3f);
                break;
            case "mobile":
                Gl(st, "PhoneGlass", new Vector3(0, top + 0.1f, 0.1f), new Vector3(1.8f, 0.2f, 0.55f), Rgb(0.3f, 0.6f, 1f), 1.2f);
                for (int i = 0; i < 10; i++)
                    B(st, "Cover", new Vector3(-0.9f + i * 0.2f, 1.7f, 0.4f), new Vector3(0.1f, 0.18f, 0.01f), Color.HSVToRGB(R(), 0.8f, 0.9f), false, 0.4f);
                GameKit.Sign(st, "RECHARGE", "रिचार्ज", new Vector3(0, 2.2f, 0.88f), Quaternion.Euler(0, 180f, 0), 0.22f, 1.4f, Rgb(0.7f, 1f, 1f));
                break;
            case "corn":
                B(st, "Grill", new Vector3(-0.3f, top + 0.15f, 0.1f), new Vector3(0.9f, 0.3f, 0.45f), Rgb(0.1f, 0.1f, 0.1f), false, 0.4f, 0.5f);
                Gl(st, "Coals", new Vector3(-0.3f, top + 0.32f, 0.1f), new Vector3(0.8f, 0.03f, 0.38f), Rgb(1f, 0.3f, 0.05f), 3f);
                AddLight(st.TransformPoint(new Vector3(-0.3f, 1.3f, 0.3f)), Rgb(1f, 0.4f, 0.1f), 1.8f, 4f, true);
                Steam(st.TransformPoint(new Vector3(-0.3f, top + 0.45f, 0.1f)), 0.9f);
                for (int i = 0; i < 6; i++)
                    Cy(st, "Corn", new Vector3(-0.55f + i * 0.1f, top + 0.4f, 0.1f), 0.04f, 0.22f, Rgb(0.95f, 0.8f, 0.2f)).transform.localRotation = Quaternion.Euler(0, 0, 90);
                for (int i = 0; i < 10; i++)
                    Cy(st, "Cob", new Vector3(0.5f + (i % 4) * 0.12f, top + 0.15f + (i / 4) * 0.1f, 0.1f), 0.045f, 0.24f, Rgb(0.95f, 0.85f, 0.3f)).transform.localRotation = Quaternion.Euler(0, 0, 90);
                Cy(st, "Lime", new Vector3(0.8f, top + 0.05f, 0.4f), 0.05f, 0.04f, Rgb(0.4f, 0.7f, 0.2f));
                break;
            case "news":
                for (int i = 0; i < 12; i++)
                    B(st, "Magazine", new Vector3(-0.95f + i * 0.17f, 1.4f, 0.4f), new Vector3(0.15f, 0.4f, 0.02f), Color.HSVToRGB(R(), 0.5f, 0.9f), false, 0.3f);
                B(st, "PaperStack", new Vector3(0.6f, top + 0.12f, 0.1f), new Vector3(0.5f, 0.24f, 0.4f), Rgb(0.85f, 0.83f, 0.75f), false, 0.1f);
                GameKit.Sign(st, "NEWS", "समाचार", new Vector3(0, 2.2f, 0.88f), Quaternion.Euler(0, 180f, 0), 0.24f, 1.4f, Rgb(1f, 0.95f, 0.8f));
                break;
            case "samosa":
                Gl(st, "GlassCase", new Vector3(-0.5f, top + 0.25f, 0.1f), new Vector3(0.9f, 0.4f, 0.5f), Rgb(0.8f, 0.9f, 0.95f), 0.5f);
                for (int i = 0; i < 6; i++) GameKit.Cone(st, "Samosa", new Vector3(-0.8f + i * 0.12f, top + 0.08f, 0.1f), 0.06f, 0.1f, GameKit.Lit(Rgb(0.85f, 0.6f, 0.2f), 0.4f));
                Cy(st, "Kadhai", new Vector3(0.5f, top + 0.12f, 0.0f), 0.42f, 0.2f, Rgb(0.04f, 0.04f, 0.04f), 0.5f, 0.7f);
                GameKit.Cyl(st, "Oil", new Vector3(0.5f, top + 0.2f, 0.0f), 0.36f, 0.03f, GameKit.Glow(Rgb(0.8f, 0.45f, 0.05f), Rgb(1f, 0.55f, 0.05f), 1.5f));
                Cy(st, "Gas", new Vector3(0.9f, 0.3f, -0.1f), 0.17f, 0.6f, Rgb(0.75f, 0.1f, 0.08f), 0.4f);
                AddLight(st.TransformPoint(new Vector3(0.5f, 1.3f, 0.2f)), Rgb(1f, 0.5f, 0.1f), 1.6f, 4f, true);
                Steam(st.TransformPoint(new Vector3(0.5f, top + 0.3f, 0f)), 1.3f);
                GameKit.Sign(st, "SAMOSA JALEBI", "समोसा जलेबी", new Vector3(0, 2.2f, 0.88f), Quaternion.Euler(0, 180f, 0), 0.22f, 1.8f, Rgb(1f, 0.9f, 0.5f));
                break;
            case "pushcart":
                B(st, "CartBed", new Vector3(0, 0.75f, 0.1f), new Vector3(1.7f, 0.1f, 0.85f), Rgb(0.4f, 0.28f, 0.15f), true, 0.2f);
                foreach (float x in new[] { -0.85f, 0.85f })
                    Cy(st, "Wheel", new Vector3(x, 0.38f, 0.1f), 0.38f, 0.07f, Rgb(0.06f, 0.06f, 0.06f), 0.3f).transform.localRotation = Quaternion.Euler(0, 0, 90);
                Pile(st, new Vector3(-0.5f, 0.85f, 0.1f), Rgb(0.2f, 0.65f, 0.2f), 14, 0.22f, 0.12f);   // gourds
                Pile(st, new Vector3(0.1f, 0.85f, 0.1f), Rgb(0.85f, 0.4f, 0.1f), 14, 0.22f, 0.11f);   // carrots / pumpkins
                Pile(st, new Vector3(0.65f, 0.85f, 0.1f), Rgb(0.9f, 0.9f, 0.7f), 12, 0.2f, 0.12f);    // cauliflower
                break;
            case "toys":
                for (int i = 0; i < 14; i++)
                {
                    var pw = B(st, "Pinwheel", new Vector3(-1.0f + i * 0.15f, 1.5f + R() * 0.5f, 0.45f), new Vector3(0.14f, 0.14f, 0.01f), Color.HSVToRGB(R(), 0.8f, 0.95f), false, 0.3f);
                    pw.transform.localRotation = Quaternion.Euler(0, 0, 45f);
                    animated.Add(pw); Spinners.Add(pw.transform);
                }
                for (int i = 0; i < 8; i++) Sp(st, "Ball", new Vector3(-0.8f + i * 0.22f, top + 0.1f, 0.1f), 0.16f, Color.HSVToRGB(R(), 0.8f, 0.95f), 0.7f);
                B(st, "Drum", new Vector3(0.8f, top + 0.12f, 0.2f), new Vector3(0.22f, 0.22f, 0.22f), Rgb(0.8f, 0.1f, 0.15f));
                break;
            case "utensils":
                for (int i = 0; i < 4; i++)
                    for (int l = 0; l < 4; l++)
                        Cy(st, "Pot", new Vector3(-0.9f + i * 0.45f, top + 0.06f + l * 0.1f, 0.1f), 0.2f - l * 0.03f, 0.1f, i % 2 == 0 ? Rgb(0.78f, 0.78f, 0.8f) : Rgb(0.85f, 0.65f, 0.2f), 0.85f, 0.9f);
                for (int i = 0; i < 5; i++) Cy(st, "Bucket", new Vector3(-0.9f + i * 0.4f, 0.17f, 0.8f), 0.17f, 0.34f, Color.HSVToRGB(R(), 0.8f, 0.9f), 0.5f);
                break;
        }
    }

    // ----------------------------------------------------- Vikram's chai stall
    void VikramStall()
    {
        var st = StallBase(-1, 34f, "chai", Rgb(0.1f, 0.35f, 0.65f), false, false);
        float top = 0.9f;
        // big dented kettle on a kerosene stove
        B(st, "Stove", new Vector3(-0.55f, top + 0.06f, 0.0f), new Vector3(0.6f, 0.1f, 0.6f), Rgb(0.1f, 0.1f, 0.1f), false, 0.4f, 0.6f);
        Cy(st, "Kettle", new Vector3(-0.55f, top + 0.35f, 0.0f), 0.24f, 0.46f, Rgb(0.75f, 0.76f, 0.78f), 0.85f, 0.9f);
        Cy(st, "Spout", new Vector3(-0.25f, top + 0.45f, 0.0f), 0.03f, 0.3f, Rgb(0.75f, 0.76f, 0.78f), 0.85f, 0.9f).transform.localRotation = Quaternion.Euler(0, 0, -55f);
        Gl(st, "Flame", new Vector3(-0.55f, top + 0.12f, 0.0f), new Vector3(0.4f, 0.03f, 0.4f), Rgb(1f, 0.4f, 0.1f), 3f);
        AddLight(st.TransformPoint(new Vector3(-0.55f, 1.2f, 0.3f)), Rgb(1f, 0.55f, 0.2f), 1.8f, 4f, true);
        Steam(st.TransformPoint(new Vector3(-0.28f, top + 0.75f, 0f)), 1f);
        Steam(st.TransformPoint(new Vector3(-0.55f, top + 0.75f, 0f)), 1.2f);
        // steel glasses in a tray, biscuit jars, a bunch of bananas
        for (int i = 0; i < 8; i++) Cy(st, "Glass", new Vector3(0.1f + (i % 4) * 0.1f, top + 0.07f, -0.1f + (i / 4) * 0.12f), 0.035f, 0.1f, Rgb(0.88f, 0.92f, 0.92f), 0.9f);
        for (int i = 0; i < 3; i++) Cy(st, "Jar", new Vector3(0.65f + i * 0.2f, top + 0.15f, 0.15f), 0.08f, 0.28f, Rgb(0.85f, 0.55f, 0.2f), 0.6f);
        for (int i = 0; i < 6; i++) Cy(st, "Banana", new Vector3(0.0f + i * 0.04f, 1.9f - i * 0.02f, 0.4f), 0.03f, 0.22f, Rgb(0.98f, 0.85f, 0.2f), 0.4f);
        GameKit.Sign(st, "VIKRAM CHAI", "विक्रम चाय", new Vector3(0, 2.25f, 0.9f), Quaternion.Euler(0, 180f, 0), 0.3f, 2.0f, Rgb(1f, 0.9f, 0.5f));
        // customers' low stools and plastic chairs in front
        for (int i = 0; i < 3; i++)
        {
            Cy(st, "Stool", new Vector3(-0.8f + i * 0.8f, 0.2f, 1.45f), 0.2f, 0.4f, Rgb(0.65f, 0.25f, 0.15f), 0.3f);
            Color cc = i == 1 ? Rgb(0.8f, 0.1f, 0.1f) : Rgb(0.1f, 0.4f, 0.8f);
            B(st, "Chair", new Vector3(-0.8f + i * 0.8f, 0.45f, 2.2f), new Vector3(0.45f, 0.05f, 0.45f), cc, false, 0.4f);
            B(st, "ChairBack", new Vector3(-0.8f + i * 0.8f, 0.72f, 2.42f), new Vector3(0.45f, 0.5f, 0.04f), cc, false, 0.4f);
            foreach (float cx in new[] { -0.19f, 0.19f })
                foreach (float cz in new[] { -0.19f, 0.19f })
                    B(st, "ChairLeg", new Vector3(-0.8f + i * 0.8f + cx, 0.22f, 2.2f + cz), new Vector3(0.03f, 0.44f, 0.03f), Rgb(0.7f, 0.7f, 0.7f), false, 0.5f, 0.6f);
        }
    }

    // ----------------------------------------------------------------- shrine
    void BuildShrine()
    {
        // a small roadside mandir on the right plinth
        var sh = new GameObject("Shrine").transform;
        sh.SetParent(transform, false);
        sh.position = new Vector3(3.3f, 0, 14f);
        var marble = Rgb(0.92f, 0.9f, 0.85f);
        B(sh, "Base", new Vector3(0.1f, 0.3f, 0), new Vector3(1.0f, 0.6f, 1.3f), marble, true, 0.5f);
        B(sh, "Niche", new Vector3(0.2f, 1.2f, 0), new Vector3(0.7f, 1.2f, 1.1f), Rgb(0.8f, 0.35f, 0.1f), false, 0.3f);
        B(sh, "NicheInner", new Vector3(-0.1f, 1.2f, 0), new Vector3(0.3f, 0.9f, 0.8f), Rgb(0.95f, 0.65f, 0.15f), false, 0.4f);
        GameKit.Cone(sh, "Shikhara", new Vector3(0.2f, 1.8f, 0), 0.65f, 0.9f, GameKit.Lit(Rgb(0.95f, 0.55f, 0.1f), 0.3f), 4).transform.localRotation = Quaternion.Euler(0, 45f, 0);
        Sp(sh, "Kalash", new Vector3(0.2f, 2.75f, 0), 0.14f, Rgb(0.95f, 0.8f, 0.2f), 0.7f);
        GameKit.Cone(sh, "Idol", new Vector3(-0.1f, 0.85f, 0), 0.2f, 0.5f, GameKit.Lit(Rgb(0.9f, 0.4f, 0.05f), 0.3f));
        Sp(sh, "IdolHead", new Vector3(-0.1f, 1.45f, 0), 0.2f, Rgb(0.9f, 0.45f, 0.1f), 0.3f);
        Garland(sh.TransformPoint(new Vector3(-0.35f, 1.4f, -0.4f)), sh.TransformPoint(new Vector3(-0.35f, 1.4f, 0.4f)), 12, 0.25f);
        for (int i = 0; i < 4; i++)   // diyas
        {
            Cy(sh, "Diya", new Vector3(-0.45f, 0.63f, -0.45f + i * 0.3f), 0.05f, 0.04f, Rgb(0.7f, 0.35f, 0.15f), 0.3f);
            GameKit.Sphere(sh, "Flame", new Vector3(-0.45f, 0.7f, -0.45f + i * 0.3f), 0.06f, GameKit.Glow(Color.yellow, new Color(1f, 0.6f, 0.1f), 6f));
        }
        AddLight(sh.TransformPoint(new Vector3(-0.5f, 1.0f, 0)), new Color(1f, 0.55f, 0.15f), 2.4f, 5f, true);
        GameKit.Sign(sh, "OM", "ॐ", new Vector3(-0.32f, 2.35f, 0), Quaternion.Euler(0, -90f, 0), 0.22f, 0.8f, Rgb(1f, 0.6f, 0.1f));
        Bunting(sh.TransformPoint(new Vector3(-0.5f, 2.9f, -0.9f)), sh.TransformPoint(new Vector3(-0.5f, 2.9f, 0.9f)), 8, 0.2f);
    }

    // ------------------------------------------------------ electricity pole
    void BuildPole()
    {
        Vector3 b = new Vector3(3.45f, 0, 23.5f);
        Cy(transform, "Pole", b + Vector3.up * 4.5f, 0.12f, 9f, Rgb(0.55f, 0.55f, 0.52f), 0.2f);
        B(transform, "Crossarm", b + new Vector3(0, 8.4f, 0), new Vector3(0.12f, 0.12f, 2.2f), Rgb(0.2f, 0.2f, 0.2f));
        B(transform, "Crossarm", b + new Vector3(0, 7.7f, 0), new Vector3(0.12f, 0.12f, 1.8f), Rgb(0.2f, 0.2f, 0.2f));
        B(transform, "Transformer", b + new Vector3(-0.35f, 6.3f, 0), new Vector3(0.6f, 0.9f, 0.5f), Rgb(0.45f, 0.5f, 0.45f), false, 0.4f, 0.5f);
        for (int i = 0; i < 9; i++)   // the famous tangle
        {
            Vector3 a = b + new Vector3(0, 6.6f + R() * 1.8f, (R() - 0.5f) * 1.6f);
            Vector3 t = new Vector3(-FX + R() * 0.3f, 4.5f + R() * 3f, 18f + R() * 14f);
            if (i % 2 == 0) t.x = FX - R() * 0.3f;
            Wire(a, t, 0.9f, 0.02f);
        }
        AddLight(b + new Vector3(-0.6f, 5.5f, 0), Tube, 2.2f, 8f);
        GameKit.Sphere(transform, "StreetLamp", b + new Vector3(-0.7f, 5.5f, 0), 0.25f, GameKit.Glow(Color.white, Tube, 5f));
    }

    // ---------------------------------------------------------------- vehicles
    Transform Scooter(Vector3 pos, float yaw, Color body)
    {
        var s = new GameObject("Scooter").transform;
        s.SetParent(transform, false);
        s.position = pos; s.rotation = Quaternion.Euler(0, yaw, 0);
        B(s, "Floor", new Vector3(0, 0.3f, 0), new Vector3(0.38f, 0.1f, 0.9f), body, false, 0.5f);
        B(s, "Rear", new Vector3(0, 0.5f, -0.4f), new Vector3(0.4f, 0.4f, 0.5f), body, false, 0.5f);
        B(s, "Front", new Vector3(0, 0.65f, 0.38f), new Vector3(0.36f, 0.7f, 0.12f), body, false, 0.5f);
        B(s, "Seat", new Vector3(0, 0.74f, -0.3f), new Vector3(0.34f, 0.1f, 0.65f), Rgb(0.05f, 0.05f, 0.05f), false, 0.3f);
        B(s, "Handle", new Vector3(0, 1.05f, 0.4f), new Vector3(0.7f, 0.04f, 0.05f), Rgb(0.1f, 0.1f, 0.1f), false, 0.5f, 0.7f);
        GameKit.Sphere(s, "Headlight", new Vector3(0, 0.95f, 0.5f), 0.14f, GameKit.Glow(Color.white, new Color(1f, 0.9f, 0.6f), 3f));
        foreach (float z in new[] { 0.42f, -0.5f })
            Cy(s, "Wheel", new Vector3(0, 0.2f, z), 0.2f, 0.1f, Rgb(0.04f, 0.04f, 0.04f), 0.3f).transform.localRotation = Quaternion.Euler(0, 0, 90);
        var col = s.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.5f, 0); col.size = new Vector3(0.7f, 1f, 1.3f);
        return s;
    }

    void Bicycle(Vector3 pos, float yaw)
    {
        var s = new GameObject("Bicycle").transform;
        s.SetParent(transform, false);
        s.position = pos; s.rotation = Quaternion.Euler(0, yaw, 0);
        var dk = Rgb(0.06f, 0.06f, 0.06f);
        foreach (float z in new[] { 0.5f, -0.5f })
            Cy(s, "Wheel", new Vector3(0, 0.33f, z), 0.33f, 0.03f, dk, 0.3f).transform.localRotation = Quaternion.Euler(0, 0, 90);
        TubeSeg(s, new Vector3(0, 0.33f, -0.5f), new Vector3(0, 0.75f, -0.15f), 0.03f, Rgb(0.7f, 0.1f, 0.1f));
        TubeSeg(s, new Vector3(0, 0.75f, -0.15f), new Vector3(0, 0.33f, 0.5f), 0.03f, Rgb(0.7f, 0.1f, 0.1f));
        TubeSeg(s, new Vector3(0, 0.75f, -0.15f), new Vector3(0, 0.9f, 0.35f), 0.03f, Rgb(0.7f, 0.1f, 0.1f));
        B(s, "Handle", new Vector3(0, 0.95f, 0.38f), new Vector3(0.5f, 0.03f, 0.03f), dk, false, 0.4f, 0.7f);
        B(s, "Seat", new Vector3(0, 0.82f, -0.2f), new Vector3(0.14f, 0.05f, 0.25f), dk);
        var col = s.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.5f, 0); col.size = new Vector3(0.5f, 1f, 1.4f);
    }

    void TubeSeg(Transform p, Vector3 a, Vector3 b, float thick, Color c)
    {
        var go = GameKit.Box(p, "Tube", (a + b) / 2f, new Vector3(thick, thick, Vector3.Distance(a, b)), GameKit.Lit(c, 0.4f, 0.6f));
        go.transform.localRotation = Quaternion.LookRotation((b - a).normalized);
    }

    void BuildVehicles()
    {
        Scooter(new Vector3(-3.4f, 0, 21.5f), 80f, Rgb(0.8f, 0.1f, 0.12f));
        Scooter(new Vector3(-3.4f, 0, 44.5f), 100f, Rgb(0.15f, 0.3f, 0.65f));
        Scooter(new Vector3(3.4f, 0, 29.0f), -95f, Rgb(0.9f, 0.9f, 0.88f));
        Scooter(new Vector3(3.4f, 0, 61.2f), -80f, Rgb(0.1f, 0.1f, 0.12f));
        Scooter(new Vector3(-3.45f, 0, 68.5f), 95f, Rgb(0.85f, 0.65f, 0.1f));
        Bicycle(new Vector3(-3.6f, 0, 14.0f), 90f);
        Bicycle(new Vector3(3.6f, 0, 44.0f), -90f);
        Bicycle(new Vector3(3.6f, 0, 56.0f), -88f);
        MakeAuto(new Vector3(-2.6f, 0, 79f), 0f, true);     // a second, parked auto-rickshaw at the far end
    }

    void BuildAuto() { Auto = MakeAuto(AutoPos, 0f, false); Kinematic(Auto); }

    Transform MakeAuto(Vector3 pos, float yaw, bool lightsOff)
    {
        var a = new GameObject(lightsOff ? "ParkedAuto" : "AutoRickshaw").transform;
        a.SetParent(transform, false);
        a.position = pos; a.rotation = Quaternion.Euler(0, yaw, 0);

        var yellow = Rgb(0.98f, 0.76f, 0.1f);
        var green = Rgb(0.08f, 0.4f, 0.2f);
        var black = Rgb(0.04f, 0.04f, 0.04f);
        B(a, "Chassis", new Vector3(0, 0.38f, 0), new Vector3(1.2f, 0.12f, 2.5f), black);
        B(a, "BodyLow", new Vector3(0, 0.78f, -0.4f), new Vector3(1.3f, 0.7f, 1.5f), yellow, false, 0.5f);       // passenger body (yellow)
        B(a, "BodyBelt", new Vector3(0, 0.45f, -0.4f), new Vector3(1.32f, 0.18f, 1.52f), green, false, 0.5f);     // green stripe (CNG auto)
        B(a, "Hood", new Vector3(0, 0.75f, 0.95f), new Vector3(0.62f, 0.6f, 0.75f), yellow, false, 0.5f);
        B(a, "HoodTop", new Vector3(0, 1.08f, 0.9f), new Vector3(0.6f, 0.06f, 0.65f), black);
        var ws = B(a, "Windscreen", new Vector3(0, 1.4f, 0.55f), new Vector3(0.85f, 0.55f, 0.04f), Rgb(0.5f, 0.7f, 0.8f), false, 0.9f);
        ws.transform.localRotation = Quaternion.Euler(-16f, 0, 0);
        B(a, "Roof", new Vector3(0, 1.78f, -0.3f), new Vector3(1.4f, 0.07f, 2.1f), black, false, 0.3f);       // black canvas roof
        foreach (float px in new[] { -0.62f, 0.62f })
        {
            B(a, "Pillar", new Vector3(px, 1.3f, 0.5f), new Vector3(0.05f, 1.0f, 0.05f), black);
            B(a, "Pillar", new Vector3(px, 1.3f, -1.0f), new Vector3(0.05f, 1.0f, 0.05f), black);
        }
        B(a, "Seat", new Vector3(0, 0.98f, -0.55f), new Vector3(1.1f, 0.12f, 0.55f), Rgb(0.3f, 0.06f, 0.06f), false, 0.3f);
        B(a, "SeatBack", new Vector3(0, 1.28f, -0.85f), new Vector3(1.1f, 0.5f, 0.1f), Rgb(0.3f, 0.06f, 0.06f), false, 0.3f);
        var tyre = Rgb(0.03f, 0.03f, 0.03f);
        Cy(a, "WheelF", new Vector3(0, 0.28f, 1.0f), 0.28f, 0.14f, tyre).transform.localRotation = Quaternion.Euler(0, 0, 90);
        foreach (float x in new[] { -0.6f, 0.6f })
            Cy(a, "WheelR", new Vector3(x, 0.28f, -0.7f), 0.28f, 0.14f, tyre).transform.localRotation = Quaternion.Euler(0, 0, 90);
        B(a, "Handlebar", new Vector3(0, 1.0f, 0.7f), new Vector3(0.7f, 0.04f, 0.04f), black, false, 0.4f, 0.7f);
        if (!lightsOff)
        {
            GameKit.Sphere(a, "Headlight", new Vector3(0, 0.9f, 1.35f), 0.2f, GameKit.Glow(Color.white, new Color(1f, 0.9f, 0.6f), 5f));
            GameKit.Box(a, "Tail", new Vector3(-0.5f, 0.8f, -1.17f), new Vector3(0.15f, 0.1f, 0.03f), GameKit.Glow(Color.red, Color.red, 3f));
            GameKit.Box(a, "Tail", new Vector3(0.5f, 0.8f, -1.17f), new Vector3(0.15f, 0.1f, 0.03f), GameKit.Glow(Color.red, Color.red, 3f));
            var spot = new GameObject("HeadSpot").AddComponent<Light>();
            spot.transform.SetParent(a, false);
            spot.transform.localPosition = new Vector3(0, 0.9f, 1.4f);
            spot.type = LightType.Spot; spot.spotAngle = 65f; spot.range = 11f; spot.intensity = 5f; spot.color = new Color(1f, 0.92f, 0.7f);
            spot.shadows = LightShadows.None;
        }
        GameKit.Text(a, "CHANDNI AUTO", new Vector3(0, 0.95f, -1.17f), Quaternion.Euler(0, 180f, 0), 0.1f, Color.black);
        GameKit.Text(a, "HORN OK PLEASE", new Vector3(0, 0.62f, -1.17f), Quaternion.Euler(0, 180f, 0), 0.07f, Rgb(0.3f, 0.05f, 0.05f));
        var col = a.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.9f, 0.1f); col.size = new Vector3(1.5f, 1.8f, 2.7f);
        return a;
    }

    // ----------------------------------------------------------------- animals
    void BuildAnimals()
    {
        // a cow resting in the lane, as always
        var cow = new GameObject("Cow").transform;
        cow.SetParent(transform, false);
        cow.position = new Vector3(-1.55f, 0, 45.5f); cow.rotation = Quaternion.Euler(0, 20f, 0);
        var hide = Rgb(0.93f, 0.9f, 0.82f);
        B(cow, "Body", new Vector3(0, 0.95f, 0), new Vector3(0.65f, 0.7f, 1.6f), hide, false, 0.3f);
        Sp(cow, "Hump", new Vector3(0, 1.4f, 0.45f), 0.38f, hide, 0.3f);
        B(cow, "Head", new Vector3(0, 1.1f, 1.0f), new Vector3(0.3f, 0.4f, 0.5f), Rgb(0.88f, 0.85f, 0.78f), false, 0.3f);
        B(cow, "Snout", new Vector3(0, 0.95f, 1.28f), new Vector3(0.26f, 0.22f, 0.2f), Rgb(0.55f, 0.4f, 0.35f), false, 0.3f);
        B(cow, "Dewlap", new Vector3(0, 0.7f, 0.75f), new Vector3(0.2f, 0.4f, 0.4f), hide, false, 0.3f);
        foreach (float x in new[] { -0.17f, 0.17f })
        {
            B(cow, "Horn", new Vector3(x, 1.38f, 0.95f), new Vector3(0.05f, 0.25f, 0.05f), Rgb(0.8f, 0.78f, 0.7f)).transform.localRotation = Quaternion.Euler(0, 0, x < 0 ? 25f : -25f);
            B(cow, "Ear", new Vector3(x * 1.6f, 1.25f, 0.9f), new Vector3(0.18f, 0.06f, 0.12f), hide);
        }
        foreach (float x in new[] { -0.22f, 0.22f })
            foreach (float z in new[] { -0.55f, 0.6f })
                B(cow, "Leg", new Vector3(x, 0.3f, z), new Vector3(0.14f, 0.6f, 0.14f), hide, false, 0.3f);
        B(cow, "Tail", new Vector3(0, 0.85f, -0.85f), new Vector3(0.06f, 0.7f, 0.06f), hide);
        B(cow, "Marigold", new Vector3(0, 1.45f, 0.1f), new Vector3(0.5f, 0.06f, 0.1f), Rgb(1f, 0.6f, 0.1f), false, 0.2f);   // a garland: a sacred cow
        var col = cow.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.9f, 0.3f); col.size = new Vector3(0.8f, 1.6f, 2.2f);

        // a stray dog by the samosa stall
        var dog = new GameObject("StrayDog").transform;
        dog.SetParent(transform, false);
        dog.position = new Vector3(2.1f, 0, 50.2f); dog.rotation = Quaternion.Euler(0, -40f, 0);
        var fur = Rgb(0.6f, 0.42f, 0.25f);
        B(dog, "Body", new Vector3(0, 0.35f, 0), new Vector3(0.25f, 0.28f, 0.6f), fur, false, 0.2f);
        B(dog, "Head", new Vector3(0, 0.45f, 0.42f), new Vector3(0.2f, 0.2f, 0.24f), fur, false, 0.2f);
        foreach (float x in new[] { -0.08f, 0.08f })
            foreach (float z in new[] { -0.2f, 0.2f })
                B(dog, "Leg", new Vector3(x, 0.15f, z), new Vector3(0.06f, 0.3f, 0.06f), fur);
        B(dog, "Tail", new Vector3(0, 0.45f, -0.36f), new Vector3(0.05f, 0.05f, 0.25f), fur);
    }

    // ----------------------------------------------------------------- clutter
    void BuildClutter()
    {
        float[] zs = { 4f, 13f, 18.5f, 25f, 30.5f, 36.5f, 42.5f, 49.5f, 55.5f, 62f, 67.5f, 74f };
        for (int i = 0; i < zs.Length; i++)
        {
            int side = i % 2 == 0 ? 1 : -1;
            float x = side * 3.55f, z = zs[i];
            switch (i % 4)
            {
                case 0:   // stacked plastic crates
                    for (int c = 0; c < 3; c++) B(transform, "Crate", new Vector3(x, 0.4f + c * 0.3f, z), new Vector3(0.55f, 0.28f, 0.4f), c % 2 == 0 ? Rgb(0.8f, 0.15f, 0.12f) : Rgb(0.12f, 0.35f, 0.75f), true, 0.4f);
                    break;
                case 1:   // grain sacks
                    for (int c = 0; c < 3; c++) B(transform, "Sack", new Vector3(x + side * 0.1f, 0.4f + (c / 2) * 0.3f, z - 0.3f + (c % 2) * 0.5f), new Vector3(0.5f, 0.28f, 0.7f), Rgb(0.8f, 0.72f, 0.55f), true, 0.1f);
                    break;
                case 2:   // LPG cylinders + a blue water drum
                    Cy(transform, "LPG", new Vector3(x, 0.55f, z), 0.17f, 0.5f, Rgb(0.75f, 0.1f, 0.08f), 0.4f);
                    Cy(transform, "LPG", new Vector3(x, 0.55f, z + 0.4f), 0.17f, 0.5f, Rgb(0.75f, 0.1f, 0.08f), 0.4f);
                    Cy(transform, "Drum", new Vector3(x, 0.55f, z - 0.5f), 0.28f, 0.7f, Rgb(0.1f, 0.3f, 0.7f), 0.4f);
                    break;
                default:  // green dustbin + cardboard boxes
                    Cy(transform, "Dustbin", new Vector3(x, 0.45f, z), 0.28f, 0.8f, Rgb(0.1f, 0.4f, 0.2f), 0.3f);
                    for (int c = 0; c < 3; c++) B(transform, "Carton", new Vector3(x, 0.38f + c * 0.3f, z + 0.7f), new Vector3(0.5f, 0.28f, 0.5f), Rgb(0.65f, 0.5f, 0.32f), true, 0.1f);
                    break;
            }
        }
        for (int i = 0; i < 14; i++)   // litter on the road
            GameKit.Box(transform, "Litter", new Vector3((R() - 0.5f) * 4.5f, 0.012f, 2 + R() * 76f), new Vector3(0.2f, 0.01f, 0.15f), GameKit.Lit(Color.HSVToRGB(R(), 0.3f, 0.8f), 0.2f));
    }

    // ------------------------------------------------------------------- SUV
    /// <summary>
    /// A black full-size SUV (Fortuner / Scorpio class: 4.8 x 1.9 x 1.8 m): tall boxy body, raked windscreen, glasshouse with
    /// pillars, roof rails, black cladding and wheel arches, alloy wheels, chrome grille, LED headlights with daytime
    /// running strips, fog lamps, tail lights, mirrors, door lines, a rear spare wheel and a readable front licence plate.
    /// </summary>
    void BuildSuv()
    {
        Suv = new GameObject("BlackSUV").transform;
        Suv.SetParent(transform, false);
        Suv.localPosition = SuvStart;
        Suv.localRotation = Quaternion.Euler(0, 180f, 0);    // local +Z = front; it drives towards -Z
        var s = Suv;

        var paint = GameKit.Lit(new Color(0.03f, 0.03f, 0.035f), 0.92f, 0.65f);       // gloss black metallic
        var cladding = GameKit.Lit(new Color(0.045f, 0.045f, 0.05f), 0.2f);             // matte black plastic
        var glass = GameKit.Lit(new Color(0.015f, 0.02f, 0.03f), 1f, 0.3f);             // tinted glass
        var chrome = GameKit.Lit(new Color(0.75f, 0.77f, 0.8f), 0.95f, 1f);
        var tyre = GameKit.Lit(new Color(0.02f, 0.02f, 0.02f), 0.15f);
        var alloy = GameKit.Lit(new Color(0.55f, 0.57f, 0.6f), 0.9f, 0.9f);

        // ---- body
        GameKit.Box(s, "LowerBody", new Vector3(0, 0.78f, 0), new Vector3(1.88f, 0.66f, 4.7f), paint);
        var hood = GameKit.Box(s, "Hood", new Vector3(0, 1.14f, 1.52f), new Vector3(1.84f, 0.14f, 1.5f), paint);
        hood.transform.localRotation = Quaternion.Euler(-4f, 0, 0);
        GameKit.Box(s, "Tailgate", new Vector3(0, 1.1f, -2.28f), new Vector3(1.84f, 0.5f, 0.2f), paint);
        GameKit.Box(s, "SideSillL", new Vector3(-0.96f, 0.5f, 0), new Vector3(0.08f, 0.2f, 3.5f), cladding);
        GameKit.Box(s, "SideSillR", new Vector3(0.96f, 0.5f, 0), new Vector3(0.08f, 0.2f, 3.5f), cladding);
        // glasshouse: windscreen (raked), rear glass, side glass between pillars, roof
        var ws = GameKit.Box(s, "Windscreen", new Vector3(0, 1.45f, 1.0f), new Vector3(1.6f, 0.66f, 0.06f), glass);
        ws.transform.localRotation = Quaternion.Euler(-32f, 0, 0);
        var rg = GameKit.Box(s, "RearGlass", new Vector3(0, 1.45f, -1.78f), new Vector3(1.5f, 0.6f, 0.06f), glass);
        rg.transform.localRotation = Quaternion.Euler(12f, 0, 0);
        foreach (float x in new[] { -0.83f, 0.83f })
        {
            GameKit.Box(s, "SideGlass", new Vector3(x, 1.45f, -0.35f), new Vector3(0.05f, 0.56f, 2.55f), glass);
            foreach (float z in new[] { 0.78f, -0.12f, -0.95f, -1.7f })     // A, B, C, D pillars
                GameKit.Box(s, "Pillar", new Vector3(x, 1.45f, z), new Vector3(0.07f, 0.62f, 0.09f), paint);
            GameKit.Box(s, "DoorLine", new Vector3(x * 1.01f, 0.95f, 0.32f), new Vector3(0.02f, 0.62f, 0.02f), cladding);
            GameKit.Box(s, "DoorLine", new Vector3(x * 1.01f, 0.95f, -0.62f), new Vector3(0.02f, 0.62f, 0.02f), cladding);
            GameKit.Box(s, "DoorHandle", new Vector3(x * 1.02f, 1.1f, 0.12f), new Vector3(0.03f, 0.04f, 0.16f), chrome);
            GameKit.Box(s, "DoorHandle", new Vector3(x * 1.02f, 1.1f, -0.85f), new Vector3(0.03f, 0.04f, 0.16f), chrome);
            GameKit.Box(s, "Mirror", new Vector3(x * 1.12f, 1.3f, 0.82f), new Vector3(0.14f, 0.12f, 0.2f), paint);
            GameKit.Box(s, "RoofRail", new Vector3(x * 0.78f, 1.82f, -0.35f), new Vector3(0.04f, 0.05f, 2.3f), chrome);
        }
        GameKit.Box(s, "Roof", new Vector3(0, 1.76f, -0.35f), new Vector3(1.66f, 0.08f, 2.5f), paint);
        GameKit.Box(s, "RoofSpoiler", new Vector3(0, 1.74f, -1.72f), new Vector3(1.5f, 0.05f, 0.25f), cladding);
        GameKit.Box(s, "SharkFin", new Vector3(0, 1.84f, -1.1f), new Vector3(0.06f, 0.08f, 0.22f), cladding);

        // ---- wheels and arches
        foreach (float x in new[] { -0.93f, 0.93f })
            foreach (float z in new[] { -1.45f, 1.4f })
            {
                GameKit.Box(s, "ArchFlare", new Vector3(x * 1.02f, 0.84f, z), new Vector3(0.1f, 0.12f, 1.0f), cladding);
                GameKit.Box(s, "ArchDark", new Vector3(x * 0.97f, 0.62f, z), new Vector3(0.14f, 0.5f, 0.92f), cladding);
                var w = GameKit.Cyl(s, "Tyre", new Vector3(x, 0.4f, z), 0.4f, 0.26f, tyre);
                w.transform.localRotation = Quaternion.Euler(0, 0, 90);
                var rim = GameKit.Cyl(s, "Alloy", new Vector3(x * 1.04f, 0.4f, z), 0.27f, 0.25f, alloy);
                rim.transform.localRotation = Quaternion.Euler(0, 0, 90);
                var cap = GameKit.Cyl(s, "HubCap", new Vector3(x * 1.1f, 0.4f, z), 0.07f, 0.1f, chrome);
                cap.transform.localRotation = Quaternion.Euler(0, 0, 90);
            }

        // ---- front: grille, bumper, headlights, DRLs, fog lamps
        GameKit.Box(s, "GrilleFrame", new Vector3(0, 0.88f, 2.37f), new Vector3(1.0f, 0.36f, 0.06f), cladding);
        for (int i = 0; i < 4; i++) GameKit.Box(s, "GrilleSlat", new Vector3(0, 0.76f + i * 0.09f, 2.41f), new Vector3(0.92f, 0.03f, 0.03f), chrome);
        var badge = GameKit.Cyl(s, "Badge", new Vector3(0, 0.88f, 2.43f), 0.07f, 0.02f, chrome);
        badge.transform.localRotation = Quaternion.Euler(90, 0, 0);
        GameKit.Box(s, "FrontBumper", new Vector3(0, 0.5f, 2.34f), new Vector3(1.9f, 0.34f, 0.16f), paint);
        GameKit.Box(s, "FrontLip", new Vector3(0, 0.34f, 2.38f), new Vector3(1.5f, 0.1f, 0.18f), cladding);
        GameKit.Box(s, "SkidPlate", new Vector3(0, 0.3f, 2.3f), new Vector3(1.0f, 0.06f, 0.4f), chrome);
        var head = GameKit.Glow(new Color(0.9f, 0.95f, 1f), new Color(0.85f, 0.92f, 1f), 7f);
        foreach (float x in new[] { -0.7f, 0.7f })
        {
            GameKit.Box(s, "HeadlampHousing", new Vector3(x, 1.04f, 2.34f), new Vector3(0.6f, 0.24f, 0.1f), cladding);
            GameKit.Box(s, "Headlamp", new Vector3(x, 1.04f, 2.4f), new Vector3(0.5f, 0.16f, 0.04f), head);
            GameKit.Box(s, "DRL", new Vector3(x, 0.9f, 2.42f), new Vector3(0.46f, 0.025f, 0.03f), head);
            var fog = GameKit.Cyl(s, "FogLamp", new Vector3(x * 0.88f, 0.52f, 2.43f), 0.07f, 0.04f, GameKit.Glow(Color.white, new Color(1f, 0.95f, 0.8f), 5f));
            fog.transform.localRotation = Quaternion.Euler(90, 0, 0);
        }
        // front licence plate (readable!)
        GameKit.Box(s, "Plate", new Vector3(0, 0.55f, 2.44f), new Vector3(0.62f, 0.16f, 0.03f), GameKit.Lit(new Color(0.95f, 0.9f, 0.6f), 0.2f));
        var plate = GameKit.Text(s, "MH-12-AB-4471", new Vector3(0, 0.55f, 2.465f), Quaternion.Euler(0, 180f, 0), 0.09f, Color.black);
        PlateText = plate.transform;

        // ---- rear: tail lights, bumper, spare wheel, plate
        var tail = GameKit.Glow(Color.red, new Color(1f, 0.1f, 0.05f), 4f);
        foreach (float x in new[] { -0.76f, 0.76f })
        {
            GameKit.Box(s, "TailLight", new Vector3(x, 1.08f, -2.4f), new Vector3(0.34f, 0.28f, 0.05f), tail);
            GameKit.Box(s, "BrakeLine", new Vector3(x, 0.78f, -2.4f), new Vector3(0.3f, 0.03f, 0.04f), tail);
        }
        GameKit.Box(s, "RearBumper", new Vector3(0, 0.5f, -2.36f), new Vector3(1.9f, 0.34f, 0.16f), paint);
        var spare = GameKit.Cyl(s, "SpareWheel", new Vector3(0, 1.0f, -2.5f), 0.38f, 0.22f, tyre);
        spare.transform.localRotation = Quaternion.Euler(90, 0, 0);
        var spareHub = GameKit.Cyl(s, "SpareHub", new Vector3(0, 1.0f, -2.62f), 0.2f, 0.04f, alloy);
        spareHub.transform.localRotation = Quaternion.Euler(90, 0, 0);

        // ---- physics, headlights and a faint fill so the black body is visible in the dark street
        var sc = s.gameObject.AddComponent<BoxCollider>();
        sc.center = new Vector3(0, 1.0f, -0.1f); sc.size = new Vector3(1.95f, 1.75f, 4.9f);
        Kinematic(s);

        SuvHeadlights = new Light[2];
        for (int i = 0; i < 2; i++)
        {
            var l = new GameObject("SuvHead" + i).AddComponent<Light>();
            l.transform.SetParent(s, false);
            l.transform.localPosition = new Vector3(i == 0 ? -0.7f : 0.7f, 1.0f, 2.5f);
            l.type = LightType.Spot; l.spotAngle = 58f; l.range = 28f; l.intensity = 14f; l.color = new Color(0.92f, 0.96f, 1f);
            CinematicLook.Shadows(l, 0.9f, 0.03f, 0.5f);
            SuvHeadlights[i] = l;
        }
        var fill = new GameObject("SuvFill").AddComponent<Light>();
        fill.transform.SetParent(s, false);
        fill.transform.localPosition = new Vector3(0, 2.6f, 0.3f);
        fill.type = LightType.Point; fill.range = 7f; fill.intensity = 2.4f; fill.color = new Color(0.65f, 0.72f, 1f);
        fill.shadows = LightShadows.None;
    }

    // --------------------------------------------------------------- skyline
    void BuildSkyline()
    {
        var m = GameKit.Lit(new Color(0.05f, 0.06f, 0.1f), 0.6f);
        var win = GameKit.Glow(new Color(0.6f, 0.5f, 0.3f), new Color(1f, 0.8f, 0.45f), 1.5f);
        for (int i = 0; i < 16; i++)
        {
            float x = -50 + i * 6.5f;
            float h = 14f + R() * 20f;
            GameKit.Box(transform, "FarBuilding", new Vector3(x, h / 2f, 100f + R() * 14f), new Vector3(5.5f, h, 5f), m);
            for (float y = 3f; y < h; y += 3f)
                if (R() < 0.3) GameKit.Box(transform, "FarWindow", new Vector3(x + (R() - 0.5f) * 3f, y, 97.3f), new Vector3(0.7f, 0.9f, 0.05f), win);
        }
        GameKit.Sphere(transform, "Moon", new Vector3(-18f, 42f, 130f), 7f, GameKit.Unlit(new Color(0.9f, 0.93f, 1f)));
    }
}

/// <summary>Breeze, spinning toys and flickering fires: the small constant motion that makes the lane feel alive.</summary>
public class AmbientLife : MonoBehaviour
{
    public MarketWorld world;
    Quaternion[] baseRot;
    float[] phase;
    float[] flameBase;
    float flickerTimer;

    void Start()
    {
        baseRot = new Quaternion[world.Swayers.Count];
        phase = new float[world.Swayers.Count];
        for (int i = 0; i < baseRot.Length; i++)
        {
            baseRot[i] = world.Swayers[i].localRotation;
            phase[i] = Random.value * 6.28f;
        }
        flameBase = new float[world.FlameLights.Count];
        for (int i = 0; i < flameBase.Length; i++) flameBase[i] = world.FlameLights[i].intensity;
    }

    void Update()
    {
        float t = Time.time;
        for (int i = 0; i < baseRot.Length; i++)
        {
            var tr = world.Swayers[i];
            if (tr == null) continue;
            tr.localRotation = baseRot[i] * Quaternion.Euler(Mathf.Sin(t * 1.9f + phase[i]) * 6f, Mathf.Sin(t * 1.3f + phase[i]) * 10f, Mathf.Sin(t * 2.3f + phase[i]) * 12f);
        }
        foreach (var sp in world.Spinners)
            if (sp != null) sp.Rotate(0, 0, 260f * Time.deltaTime, Space.Self);

        flickerTimer -= Time.deltaTime;
        if (flickerTimer <= 0f)
        {
            flickerTimer = 0.07f;
            for (int i = 0; i < flameBase.Length; i++)
                if (world.FlameLights[i] != null) world.FlameLights[i].intensity = flameBase[i] * Random.Range(0.75f, 1.2f);
        }
    }
}
