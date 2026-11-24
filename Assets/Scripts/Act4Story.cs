using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Act IV - "The Web" (11:00 PM - 12:30 AM). Three variants, chosen at the end of Act III:
///   A  Café Irani: Raj Malhotra's confession and recordings, then a non-violent stealth escape
///      (a kitchen full of pots, a fork in the alleys, hiding behind a dumpster) to a busy dhaba.
///   B  The Kapoor house: a ransacked flat, Kamala's account, a search for clues, Rohan's laptop and his confession.
///   C  Inspector Reddy: a conversation puzzle at a chai stall by the police station - present the right
///      evidence in the right order to win her over.
/// </summary>
public partial class Act4Story : ActStoryBase
{
    protected override int ActNumber { get { return 4; } }
    protected override string ActTitle { get { return "ACT IV  -  THE WEB"; } }
    protected override string ActSubtitle
    {
        get { return variant == 0 ? "Café Irani   |   11:05 PM" : variant == 1 ? "The Kapoor house   |   11:10 PM" : "Chandnipur Police Station   |   11:15 PM"; }
    }
    protected override LookPreset Look { get { return variant == 2 ? LookPreset.Market : LookPreset.Apartment; } }
    // conversational intentions on the compass in every branch (Rohan's question, Reddy's last question); Reddy's
    // evidence goes on the counter through the folio, which takes as many pieces as Arjun has
    protected override bool UseCompass { get { return true; } }

    int variant;
    readonly List<Transform> fans = new List<Transform>();

    protected override void Start()
    {
        variant = Mathf.Clamp(SaveSystem.Data.branch, 0, 2);
        base.Start();
    }

    protected override void BuildWorld()
    {
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential;
        camComp.backgroundColor = new Color(0.03f, 0.03f, 0.07f);
        SetSun(new Color(0.55f, 0.62f, 0.95f), 0.5f, new Vector3(50f, 30f, 0f));
        if (variant == 0) BuildCafe();
        else if (variant == 1) BuildKapoorHouse();
        else BuildPoliceStreet();
    }

    protected override IEnumerator Run()
    {
        if (variant == 0) return CafeRun();
        if (variant == 1) return HouseRun();
        return ReddyRun();
    }

    protected override void Update()
    {
        base.Update();
        foreach (var f in fans) if (f != null) f.Rotate(0, 160f * Time.deltaTime, 0);
    }

    static Material Checker(Color a, Color b, Vector2 tiles)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        tex.SetPixel(0, 0, a); tex.SetPixel(1, 1, a); tex.SetPixel(1, 0, b); tex.SetPixel(0, 1, b); tex.Apply();
        var m = GameKit.Lit(Color.white, 0.4f);
        m = new Material(m);
        if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", tiles); }
        m.mainTexture = tex;
        return m;
    }

    void Wall(Vector3 a, Vector3 b, float h = 3.5f, Color? c = null)
    {
        Vector3 mid = (a + b) / 2f; Vector3 d = b - a;
        var w = B("AlleyWall", new Vector3(mid.x, h / 2f, mid.z), new Vector3(Mathf.Abs(d.x) + 0.3f, h, Mathf.Abs(d.z) + 0.3f), c ?? new Color(0.5f, 0.42f, 0.36f), true, 0.1f);
        GameKit.Surface(w, "facade", 2.5f, 1f, 1f, c ?? new Color(0.62f, 0.5f, 0.42f));
    }

    /// <summary>A white police jeep (Bolero class) with a blue band, roof beacon and real wheels. Faces +Z.</summary>
    void BuildJeep(Vector3 pos)
    {
        var j = new GameObject("PoliceJeep").transform; j.SetParent(transform, false); j.position = pos;
        var white = GameKit.Lit(new Color(0.9f, 0.9f, 0.88f), 0.8f, 0.2f);
        var blue = GameKit.Lit(new Color(0.12f, 0.22f, 0.55f), 0.7f, 0.2f);
        var black = GameKit.Lit(new Color(0.04f, 0.04f, 0.045f), 0.3f);
        var glass = GameKit.Lit(new Color(0.03f, 0.04f, 0.05f), 0.95f, 0.3f);
        VehicleKit.Body(j, "Body", new[] { new Sec(2.0f, 0.82f, 0.82f, 0.30f, 7f), new Sec(1.85f, 0.82f, 0.88f, 0.32f, 8f), new Sec(-1.85f, 0.82f, 0.88f, 0.32f, 8f), new Sec(-2.0f, 0.82f, 0.84f, 0.30f, 7f) }, white);
        VehicleKit.Body(j, "Bonnet", new[] { new Sec(1.95f, 1.16f, 0.80f, 0.06f, 5f), new Sec(1.4f, 1.2f, 0.84f, 0.07f, 5f), new Sec(0.85f, 1.22f, 0.84f, 0.07f, 5f) }, white);
        VehicleKit.Body(j, "Cabin", new[] { new Sec(0.75f, 1.5f, 0.80f, 0.34f, 6f), new Sec(0.4f, 1.56f, 0.84f, 0.40f, 6f), new Sec(-1.85f, 1.56f, 0.84f, 0.40f, 6f), new Sec(-1.98f, 1.52f, 0.80f, 0.36f, 6f) }, white);
        VehicleKit.Body(j, "Band", new[] { new Sec(1.9f, 0.86f, 0.89f, 0.09f, 8f), new Sec(-1.9f, 0.86f, 0.89f, 0.09f, 8f) }, blue);
        var ws = GameKit.Box(j, "Windscreen", new Vector3(0, 1.56f, 0.72f), new Vector3(1.5f, 0.52f, 0.05f), glass); ws.transform.localRotation = Quaternion.Euler(-18f, 0, 0);
        GameKit.Box(j, "RearGlass", new Vector3(0, 1.6f, -1.985f), new Vector3(1.2f, 0.4f, 0.03f), glass);
        foreach (float x in new[] { -0.845f, 0.845f })
        {
            GameKit.Box(j, "SideGlassF", new Vector3(x, 1.62f, 0.05f), new Vector3(0.02f, 0.4f, 0.85f), glass);
            GameKit.Box(j, "SideGlassR", new Vector3(x, 1.62f, -1.1f), new Vector3(0.02f, 0.4f, 1.1f), glass);
            GameKit.Box(j, "Sill", new Vector3(x * 1.05f, 0.5f, 0), new Vector3(0.1f, 0.14f, 2.4f), black);
            GameKit.Box(j, "Mirror", new Vector3(x * 1.2f, 1.42f, 0.78f), new Vector3(0.12f, 0.16f, 0.06f), black);
            foreach (float z in new[] { 1.3f, -1.25f }) VehicleKit.Wheel(j, new Vector3(x * 1.02f, 0.37f, z), 0.37f, 0.24f);
            GameKit.Box(j, "Headlamp", new Vector3(x * 0.72f, 0.98f, 2.01f), new Vector3(0.26f, 0.18f, 0.04f), GameKit.Glow(Color.white, new Color(1f, 0.95f, 0.8f), 2f));
            GameKit.Box(j, "TailLamp", new Vector3(x * 0.82f, 0.95f, -2.01f), new Vector3(0.14f, 0.3f, 0.04f), GameKit.Glow(new Color(0.5f, 0.02f, 0.02f), Color.red, 1.5f));
        }
        GameKit.Box(j, "Grille", new Vector3(0, 0.95f, 2.01f), new Vector3(0.8f, 0.3f, 0.04f), black);
        GameKit.Box(j, "BumperF", new Vector3(0, 0.55f, 2.06f), new Vector3(1.8f, 0.16f, 0.14f), black);
        GameKit.Box(j, "BumperR", new Vector3(0, 0.55f, -2.06f), new Vector3(1.8f, 0.16f, 0.14f), black);
        GameKit.Box(j, "BeaconBar", new Vector3(0, 2.0f, 0.1f), new Vector3(1.1f, 0.06f, 0.24f), black);
        GameKit.Box(j, "BeaconRed", new Vector3(-0.32f, 2.07f, 0.1f), new Vector3(0.4f, 0.1f, 0.2f), GameKit.Glow(new Color(1f, 0.1f, 0.08f), new Color(1f, 0.15f, 0.1f), 4f)).name = "JeepBeacon";
        GameKit.Box(j, "BeaconBlue", new Vector3(0.32f, 2.07f, 0.1f), new Vector3(0.4f, 0.1f, 0.2f), GameKit.Glow(new Color(0.1f, 0.2f, 1f), new Color(0.2f, 0.4f, 1f), 4f));
        GameKit.Text(j, "POLICE", new Vector3(0, 1.2f, 2.0f), Quaternion.Euler(-20f, 180f, 0), 0.16f, new Color(0.12f, 0.22f, 0.55f));
        GameKit.Text(j, "POLICE", new Vector3(0, 1.15f, -2.03f), Quaternion.identity, 0.16f, new Color(0.12f, 0.22f, 0.55f));
    }

    /// <summary>An Indian goods truck seen from behind: painted wooden load body, steel chassis, twin rear wheels, cab. Faces +Z.</summary>
    void BuildTruck(Vector3 pos)
    {
        var t = new GameObject("GoodsTruck").transform; t.SetParent(transform, false); t.position = pos;
        var orange = new Color(0.85f, 0.42f, 0.1f);
        var paint = GameKit.Lit(orange, 0.6f, 0.1f);
        var black = GameKit.Lit(new Color(0.05f, 0.05f, 0.055f), 0.3f);
        var steelC = new Color(0.2f, 0.2f, 0.22f);
        GameKit.Box(t, "Chassis", new Vector3(0, 0.95f, -0.2f), new Vector3(1.1f, 0.25f, 5.6f), black);
        // load body: planked sides painted in bands, as every such truck is
        foreach (float x in new[] { -1.16f, 1.16f })
        {
            Tex(GameKit.Box(t, "BodySide", new Vector3(x, 2.1f, -0.9f), new Vector3(0.08f, 1.7f, 4.1f), GameKit.Lit(orange)), "wood", 0.5f, orange);
            GameKit.Box(t, "SideBand", new Vector3(x * 1.01f, 1.42f, -0.9f), new Vector3(0.09f, 0.22f, 4.12f), GameKit.Lit(new Color(0.1f, 0.3f, 0.5f), 0.4f));
            GameKit.Box(t, "SideBandTop", new Vector3(x * 1.01f, 2.82f, -0.9f), new Vector3(0.09f, 0.18f, 4.12f), GameKit.Lit(new Color(0.9f, 0.78f, 0.15f), 0.4f));
            for (int k = 0; k < 4; k++) GameKit.Box(t, "SideRib", new Vector3(x * 1.04f, 2.1f, -2.7f + k * 1.2f), new Vector3(0.06f, 1.7f, 0.08f), black);
            VehicleKit.Wheel(t, new Vector3(x * 0.88f, 0.5f, 2.15f), 0.5f, 0.28f);
            VehicleKit.Wheel(t, new Vector3(x * 0.74f, 0.5f, -1.9f), 0.5f, 0.26f);
            VehicleKit.Wheel(t, new Vector3(x * 0.98f, 0.5f, -1.9f), 0.5f, 0.26f);
            GameKit.Box(t, "MudFlap", new Vector3(x * 0.86f, 0.55f, -2.5f), new Vector3(0.5f, 0.6f, 0.02f), black);
            GameKit.Box(t, "TailLamp", new Vector3(x * 0.85f, 1.05f, -2.98f), new Vector3(0.2f, 0.12f, 0.04f), GameKit.Glow(new Color(0.5f, 0.02f, 0.02f), Color.red, 1.2f));
        }
        Tex(GameKit.Box(t, "TailGate", new Vector3(0, 1.75f, -2.96f), new Vector3(2.3f, 1.0f, 0.08f), GameKit.Lit(orange)), "wood", 0.5f, orange);
        Tex(GameKit.Box(t, "BodyFront", new Vector3(0, 2.1f, 1.16f), new Vector3(2.3f, 1.7f, 0.08f), GameKit.Lit(orange)), "wood", 0.5f, orange);
        Tex(GameKit.Box(t, "BodyFloor", new Vector3(0, 1.22f, -0.9f), new Vector3(2.3f, 0.1f, 4.1f), GameKit.Lit(steelC)), "wood", 0.6f, new Color(0.4f, 0.3f, 0.2f));
        var tarp = ProcTex.Mat(ProcTex.Get("fabric"), new Color(0.25f, 0.3f, 0.22f), new Vector2(6f, 10f), 0.4f, 0.8f);
        if (tarp.HasProperty("_Cull")) tarp.SetFloat("_Cull", 0f);
        VehicleKit.Part(t, "LoadTarp", VehicleKit.Tarp(2.5f, 4.2f, -0.35f), tarp, new Vector3(0, 2.95f, -0.9f));          // a heaped load under canvas
        GameKit.Box(t, "RearBumper", new Vector3(0, 0.8f, -3.02f), new Vector3(2.3f, 0.14f, 0.1f), GameKit.Lit(new Color(0.9f, 0.78f, 0.15f), 0.4f));
        // cab
        VehicleKit.Body(t, "Cab", new[] { new Sec(3.0f, 1.75f, 1.05f, 0.75f, 5f), new Sec(2.85f, 1.8f, 1.14f, 0.85f, 6f), new Sec(1.35f, 1.8f, 1.14f, 0.85f, 6f), new Sec(1.25f, 1.78f, 1.1f, 0.8f, 5f) }, paint);
        GameKit.Box(t, "Windscreen", new Vector3(0, 2.15f, 3.01f), new Vector3(1.8f, 0.6f, 0.03f), GameKit.Lit(new Color(0.03f, 0.04f, 0.05f), 0.95f, 0.3f));
        GameKit.Box(t, "CabBumper", new Vector3(0, 0.85f, 3.08f), new Vector3(2.3f, 0.2f, 0.14f), black);
    }

    // ================================================================ A: CAFE IRANI
    CharacterNPC raj, watcher, owner;
    Transform rajSpot;
    static readonly Vector3 K = new Vector3(20f, 0f, 0f);      // kitchen
    static readonly Vector3 A = new Vector3(40f, 0f, 0f);      // alleys
    readonly List<Vector3> pots = new List<Vector3>();
    bool hidden, rajFollow;

    void BuildCafe()
    {
        PriyaRoomPlayer.Surface = 0;
        // ---- Café Irani at 11 PM: an open-air terrace after closing time, under a moonlit sky, lit by its own
        // string lights and the glow from inside (the story needs night: the hearing is "tomorrow morning")
        CinematicLook.NoSky(camComp, new Color(0.025f, 0.03f, 0.065f));
        CinematicLook.Ambient(new Color(0.2f, 0.22f, 0.33f), new Color(0.15f, 0.14f, 0.19f), new Color(0.06f, 0.05f, 0.06f));
        SetSun(new Color(0.52f, 0.6f, 0.95f), 0.32f, new Vector3(48f, 125f, 0f));      // moonlight from the street side
        RenderSettings.fogColor = new Color(0.05f, 0.055f, 0.09f); RenderSettings.fogDensity = 0.012f;
        var wood = new Color(0.3f, 0.18f, 0.1f);
        var cream = new Color(0.9f, 0.84f, 0.7f);

        // patio of old checkered tiles, cobbled street beyond the low wall
        var floor = B("Cafe_Floor", new Vector3(0, -0.05f, 0), new Vector3(10.6f, 0.1f, 8.6f), Color.white, true, 0.3f);
        GameKit.Surface(floor, "checker", 1.5f, 1f, 1f, Color.white);
        // the lane round the terrace's other sides (the kerbed street to the west is built in BuildStreet)
        var street = B("Street", new Vector3(0f, -0.11f, 0), new Vector3(10.8f, 0.1f, 30f), Color.white, false, 0.3f);
        GameKit.Surface(street, "cobble", 2.4f, 1f, 1f, new Color(0.62f, 0.6f, 0.58f));

        // the café building along the east side: plastered front, door to the kitchen, windows, signboard
        var front = B("CafeFront", new Vector3(5.2f, 2.5f, 0), new Vector3(0.4f, 5f, 11.5f), cream, true, 0.1f);
        GameKit.SurfaceZY(front, "plaster", 2.2f, cream);
        B("Cornice", new Vector3(5.05f, 5.05f, 0), new Vector3(0.7f, 0.18f, 11.7f), new Color(0.8f, 0.75f, 0.64f), false, 0.2f);
        GameKit.Surface(B("FrontPlinth", new Vector3(4.98f, 0.3f, 0), new Vector3(0.1f, 0.6f, 11.5f), new Color(0.55f, 0.2f, 0.16f), false, 0.2f), "plaster", 2f, 1f, 1f, new Color(0.55f, 0.2f, 0.16f));
        // the signboard hangs over the counter window, clear of the pergola (slats z -1.95..1.75): once its lettering was
        // depth-tested (it used to draw through everything) the 5.6 m board running under the pergola's roof lost most of
        // its letters behind the slats as seen from the entrance
        B("SignBoard", new Vector3(4.97f, 3.75f, -3.4f), new Vector3(0.05f, 0.8f, 2.9f), new Color(0.42f, 0.08f, 0.07f), false, 0.3f);
        GameKit.Solid(GameKit.Text(transform, "CAFÉ IRANI", new Vector3(4.94f, 3.87f, -3.4f), Quaternion.Euler(0, 90f, 0), 0.3f, new Color(0.95f, 0.88f, 0.7f)));
        GameKit.Solid(GameKit.Text(transform, "SINCE 1932", new Vector3(4.94f, 3.53f, -3.4f), Quaternion.Euler(0, 90f, 0), 0.13f, new Color(0.95f, 0.88f, 0.7f)));
        foreach (float wz in new[] { -3.0f, 3.2f })
        {
            GameKit.Surface(B("WindowFrame", new Vector3(4.98f, 1.9f, wz), new Vector3(0.08f, 1.7f, 1.5f), wood, false, 0.4f), "wood", 0.9f, 1f, 1f, wood);
            // lamps still on inside, behind half-drawn curtains: at 1.1 the glass clipped to a white slab
            Glow("WindowGlass", new Vector3(4.95f, 1.9f, wz), new Vector3(0.04f, 1.5f, 1.3f), new Color(1f, 0.7f, 0.42f), 0.42f);
            foreach (float cs in new[] { -1f, 1f })
                B("WindowCurtain", new Vector3(4.92f, 1.95f, wz + cs * 0.42f), new Vector3(0.02f, 1.35f, 0.42f), new Color(0.42f, 0.1f, 0.08f), false, 0.1f);
            // the lamp inside: by the counter it stays at the glass (at 0.9 m from the old case it burnt it to white)
            if (wz < 0f) PL(new Vector3(4.78f, 1.9f, wz), new Color(1f, 0.7f, 0.42f), 0.55f, 2.4f);
            else PL(new Vector3(4.2f, 1.9f, wz), new Color(1f, 0.7f, 0.42f), 1.4f, 5f);
            B("WindowBar", new Vector3(4.93f, 1.9f, wz), new Vector3(0.03f, 1.5f, 0.04f), wood);
            B("WindowSill", new Vector3(4.9f, 1.02f, wz), new Vector3(0.22f, 0.06f, 1.7f), new Color(0.8f, 0.75f, 0.64f));
        }
        GameKit.Surface(B("BackDoor", new Vector3(4.97f, 1.05f, 0.5f), new Vector3(0.05f, 2.1f, 1f), wood), "wood", 0.9f, 1f, 1f, wood);
        GameKit.Solid(GameKit.Text(transform, "STAFF ONLY", new Vector3(4.94f, 2.3f, 0.5f), Quaternion.Euler(0, 90f, 0), 0.1f, new Color(0.9f, 0.85f, 0.7f)));

        // low garden wall round the other three sides, with planters
        foreach (var w in new[] { new Vector4(-5.25f, 0, 0.3f, 8.9f), new Vector4(0, -4.25f, 10.8f, 0.3f), new Vector4(0, 4.25f, 10.8f, 0.3f) })
        {
            var lw = B("GardenWall", new Vector3(w.x, 0.4f, w.y), new Vector3(w.z, 0.8f, w.w), new Color(0.75f, 0.6f, 0.5f), true, 0.1f);
            GameKit.Surface(lw, "brick", 1.6f, 1f, 1f, new Color(0.85f, 0.7f, 0.6f));
            GameKit.Surface(B("Coping", new Vector3(w.x, 0.83f, w.y), new Vector3(w.z + 0.1f, 0.06f, w.w + 0.1f), new Color(0.7f, 0.68f, 0.64f), false, 0.2f), "concrete", 1.2f, 1f, 1f, new Color(0.75f, 0.73f, 0.7f));
            Fence(new Vector3(w.x, 2f, w.y), new Vector3(w.z, 4f, w.w));
        }
        foreach (float px in new[] { -3.2f, -0.6f, 2.0f }) PropKit.Place(transform, "planter_box_02", new Vector3(px, 0, 3.8f), 0f);
        PropKit.Place(transform, "planter_box_01", new Vector3(-4.8f, 0, 2.2f), 90f);
        PropKit.Place(transform, "potted_plant_02", new Vector3(-4.6f, 0, -0.4f), 30f, 1.3f);
        PropKit.Place(transform, "potted_plant_02", new Vector3(4.4f, 0, -0.6f), 200f, 1.2f);
        PropKit.Place(transform, "potted_plant_01", new Vector3(4.45f, 0, 3.75f), 80f);
        PropKit.Place(transform, "potted_plant_01", new Vector3(-0.8f, 0, -3.8f), 140f, 0.9f);
        PropKit.Place(transform, "painted_wooden_bench", new Vector3(-4.75f, 0, 1.0f), 90f);

        // a slatted wooden pergola: stripes of sun and shade across the tables, fans turning underneath
        var beamCol = new Color(0.36f, 0.24f, 0.14f);
        foreach (float bx in new[] { -4.0f, -1.0f, 2.0f, 4.6f }) foreach (float bz in new[] { -1.7f, 1.5f })      // clear of every table and chair
                GameKit.Surface(B("PergolaPost", new Vector3(bx, 1.68f, bz), new Vector3(0.12f, 3.36f, 0.12f), beamCol, true, 0.3f), "wood", 0.9f, 1f, 1f, beamCol);
        foreach (float bz in new[] { -1.7f, 1.5f })
            GameKit.Surface(B("PergolaBeam", new Vector3(0.3f, 3.4f, bz), new Vector3(8.9f, 0.12f, 0.12f), beamCol, false, 0.3f), "wood", 0.9f, 1f, 1f, beamCol);
        for (int i = 0; i < 20; i++)
            B("PergolaSlat", new Vector3(-4.0f + i * 0.45f, 3.49f, -0.1f), new Vector3(0.07f, 0.05f, 3.7f), beamCol * (0.9f + (i % 3) * 0.08f), false, 0.3f);
        // a string of bare bulbs along each beam, sagging between the posts, and the light they throw on the tables
        foreach (float bz in new[] { -1.7f, 1.5f })
        {
            for (int i = 0; i < 16; i++)
            {
                float x = -4.0f + i * 0.57f, sag = Mathf.Sin((i % 5) / 5f * Mathf.PI) * 0.12f;
                GameKit.Sphere(transform, "Bulb", new Vector3(x, 3.3f - sag, bz), 0.06f, GameKit.Glow(new Color(1f, 0.82f, 0.5f), new Color(1f, 0.74f, 0.4f), 6f));
            }
            for (int k = 0; k < 3; k++) PL(new Vector3(-2.8f + k * 3f, 3.0f, bz), new Color(1f, 0.72f, 0.42f), 1.6f, 6f);
        }
        // the street outside: kerbs, road, the opposite frontage, its lamps, a parked car and a scooter
        BuildStreet();

        // tables and chairs
        Vector3[] tables = { new Vector3(-2.5f, 0, -2f), new Vector3(0.5f, 0, -2f), new Vector3(-2.5f, 0, 1.5f), new Vector3(0.5f, 0, 1.5f), new Vector3(3.2f, 0, 2.6f) };
        int ti = 0;
        foreach (var t in tables)
        {
            if (PropKit.Place(transform, "round_wooden_table_01", t, ti * 37f, 0.74f) == null)
            {
                GameKit.Cyl(transform, "TableTop", t + new Vector3(0, 0.76f, 0), 0.45f, 0.04f, GameKit.Lit(wood, 0.4f));
                Cy("TableLeg", t + new Vector3(0, 0.38f, 0), 0.04f, 0.76f, new Color(0.1f, 0.1f, 0.1f), 0.5f, 0.6f);
            }
            var tc = new GameObject("TableCollider").AddComponent<BoxCollider>();
            tc.transform.SetParent(transform, false); tc.transform.position = t + new Vector3(0, 0.4f, 0); tc.size = new Vector3(0.8f, 0.8f, 0.8f);
            foreach (float a in new[] { 0f, 180f })
            {
                Vector3 cpos = t + Quaternion.Euler(0, a, 0) * new Vector3(0, 0, 0.82f);
                // the corner table's chairs sit square to it: Raj's (north side) and the one Arjun pulls out (south)
                float jitter = ti == 4 ? 0f : (ti * 53 % 30 - 15);
                var ch = PropKit.Place(transform, "painted_wooden_chair_01", cpos, a + 180f + jitter);
                if (ch == null) ch = B("ChairSeat", cpos + new Vector3(0, 0.45f, 0), new Vector3(0.42f, 0.04f, 0.42f), wood, false, 0.5f);
                if (ti == 4) { if (a == 0f) rajChair = ch.transform; else arjunChair = ch.transform; }
            }
            if (ti == 1 || ti == 4) PropKit.Place(transform, "tea_set_01", t + new Vector3(0.05f, 0.75f, 0f), ti * 70f, 0.7f);
            else B("ChaiGlass", t + new Vector3(0.1f, 0.80f, 0.05f), new Vector3(0.055f, 0.09f, 0.055f), new Color(0.6f, 0.38f, 0.2f), false, 0.85f);
            ti++;
        }
        // the counter: panelled teak, a marble top, the glass biscuit case, kettle, jug and cups; the owner behind it
        BuildCounter();
        owner = Npc(new Vector3(3.95f, 0f, -3.42f), 0f, Cast.Owner(), "Owner", false);
        // ceiling fans hang from the pergola
        foreach (var t in new[] { new Vector3(-1f, 0, -0.3f), new Vector3(2.5f, 0, -0.3f) })
        {
            B("FanRod", t + new Vector3(0, 3.33f, 0), new Vector3(0.03f, 0.2f, 0.03f), new Color(0.15f, 0.15f, 0.15f));
            var hub = new GameObject("Fan").transform; hub.SetParent(transform, false); hub.position = t + new Vector3(0, 3.22f, 0);
            for (int b = 0; b < 3; b++)
            {
                var blade = GameKit.Box(hub, "Blade", Quaternion.Euler(0, b * 120f, 0) * new Vector3(0.45f, 0, 0), new Vector3(0.8f, 0.02f, 0.14f), GameKit.Lit(wood, 0.3f));
                blade.transform.localRotation = Quaternion.Euler(0, b * 120f, 0);
            }
            fans.Add(hub);
        }
        // Raj at the corner table, seated, waiting; the watcher in the far corner, seated behind his newspaper
        rajSeat = new Vector3(3.2f, 0.465f, 3.44f);
        arjunSeatPos = new Vector3(3.2f, 0.1f, 1.74f);
        raj = Npc(new Vector3(3.2f, 0f, 3.62f), 180f, Cast.Raj(), "Raj Malhotra");
        raj.Sit(rajSeat, 180f, 0.4f, true);
        rajSpot = raj.transform;
        watcherSeat = new Vector3(-3.95f, 0.465f, 3.25f);
        var wch = PropKit.Place(transform, "painted_wooden_chair_01", new Vector3(-3.95f, 0f, 3.25f), 140f + 180f);
        if (wch != null) watcherChair = wch.transform;
        watcher = Npc(new Vector3(-4.1f, 0f, 3.45f), 140f, NpcStyle.Thug(), "Watcher", false);
        watcher.Sit(watcherSeat, 140f, 0.42f, true);
        var paper = B("Newspaper", Vector3.zero, new Vector3(0.55f, 0.4f, 0.012f), new Color(0.88f, 0.86f, 0.78f), false, 0.1f);
        paper.transform.rotation = Quaternion.Euler(-12f, 140f, 0);
        paper.transform.position = watcherSeat + Vector3.up * 0.55f + Quaternion.Euler(0f, 140f, 0f) * new Vector3(0f, 0f, 0.36f);
        newspaper = paper.transform;
        var fold = B("NewspaperFold", Vector3.zero, new Vector3(0.006f, 0.4f, 0.016f), new Color(0.7f, 0.68f, 0.6f), false, 0.1f);
        fold.transform.SetParent(newspaper, false);
        watcher.lookTarget = newspaper;

        // ---- the kitchen: white-tiled, bright, with real brass and enamel vessels all over the floor
        Room("Kitchen", K, new Vector3(8f, 3f, 6f), new Color(0.9f, 0.9f, 0.86f), Color.white, "terracotta", "plaster");
        foreach (var side in new[] { "N", "S", "E", "W" })
        {
            var wl = transform.Find("Kitchen_Wall" + side);
            if (wl != null) GameKit.Surface(wl.gameObject, "tiles_white", 1.3f, 1f, 1f, new Color(0.93f, 0.93f, 0.9f));
        }
        var steel = new Color(0.62f, 0.63f, 0.65f);
        B("Stove", K + new Vector3(0, 0.45f, 2.6f), new Vector3(5f, 0.9f, 0.7f), steel, true, 0.75f, 0.9f);
        foreach (float bx in new[] { -1.8f, -0.6f, 0.6f, 1.8f })
        {
            Cy("BurnerRing", K + new Vector3(bx, 0.915f, 2.6f), 0.16f, 0.03f, new Color(0.08f, 0.08f, 0.08f), 0.4f, 0.6f);
            if (bx < 0) Glow("Burner", K + new Vector3(bx, 0.935f, 2.6f), new Vector3(0.2f, 0.01f, 0.2f), new Color(0.3f, 0.5f, 1f), 3f);
        }
        PropKit.Place(transform, "brass_pot_01", K + new Vector3(-1.8f, 0.93f, 2.6f), 0f, 1.2f);
        PropKit.Place(transform, "pot_enamel_01", K + new Vector3(-0.6f, 0.93f, 2.6f), 60f, 1.3f);
        PropKit.Place(transform, "brass_pan_01", K + new Vector3(0.6f, 0.93f, 2.7f), 160f, 1.2f);
        PropKit.Place(transform, "vintage_electric_kettle", K + new Vector3(2.2f, 0.9f, 2.6f), 30f);
        B("Shelves", K + new Vector3(0, 1.8f, -2.85f), new Vector3(6f, 0.05f, 0.4f), steel, false, 0.7f, 0.9f);
        B("ShelvesLow", K + new Vector3(0, 1.2f, -2.85f), new Vector3(6f, 0.05f, 0.4f), steel, false, 0.7f, 0.9f);
        string[] shelfThings = { "metal_jug", "jug_01", "wooden_bowl_01", "brass_goblets", "pot_enamel_01", "brass_vase_01", "wooden_bowl_01", "metal_jug", "brass_pot_02", "jug_01" };
        for (int i = 0; i < shelfThings.Length; i++)
            PropKit.Place(transform, shelfThings[i], K + new Vector3(-2.6f + i * 0.58f, i % 2 == 0 ? 1.83f : 1.23f, -2.85f), i * 77f, shelfThings[i] == "brass_goblets" ? 0.6f : shelfThings[i] == "brass_vase_01" ? 0.5f : 1f);
        B("PrepTable", K + new Vector3(-3.3f, 0.86f, 2.0f), new Vector3(1.1f, 0.05f, 1.6f), steel, false, 0.7f, 0.9f);
        PropKit.Place(transform, "wooden_cutting_board", K + new Vector3(-3.3f, 0.89f, 2.1f), 80f);
        PropKit.Place(transform, "wooden_spoon", K + new Vector3(-3.2f, 0.93f, 1.6f), 20f);
        PropKit.Place(transform, "wooden_bowl_01", K + new Vector3(-3.4f, 0.89f, 1.5f), 0f);
        var rnd = new System.Random(4);
        string[] vessels = { "brass_pot_01", "ceramic_pot", "pot_enamel_01", "brass_pot_02", "metal_jug" };
        float[] vscale = { 1.45f, 0.75f, 1.9f, 1.3f, 1.7f };
        for (int i = 0; i < 16; i++)
        {
            Vector3 p = K + new Vector3(-2.6f + (i % 4) * 1.6f + (float)rnd.NextDouble() * 0.5f, 0, -1.7f + (i / 4) * 1.0f + (float)rnd.NextDouble() * 0.3f);
            float rr = 0.2f + (float)rnd.NextDouble() * 0.08f;
            if (i % 5 == 2) continue;           // leave a winding path
            pots.Add(p);
            int vi = i % vessels.Length;
            var pot = PropKit.Place(transform, vessels[vi], p, i * 61f, vscale[vi]);
            if (pot == null) pot = Cy("Pot", p + new Vector3(0, 0.16f, 0), rr, 0.32f, new Color(0.7f, 0.7f, 0.72f), 0.8f, 0.9f);
            else if (vi == 3) { var top = PropKit.Place(transform, "brass_pot_01", p + new Vector3(0, 0.14f, 0), i * 20f, 1.1f); if (top != null) top.transform.SetParent(pot.transform, true); }     // a pot stacked in the wide pan
            potObjs.Add(pot.transform); potQuiet.Add(0f);
        }
        // cover between the door they came in by and the back exit: a steel rack, sacks stacked on a pallet, a cold cabinet
        var rackM = GameKit.Lit(new Color(0.55f, 0.56f, 0.58f), 0.55f, 0.85f);
        foreach (float rz in new[] { -0.28f, 0.88f }) foreach (float rx in new[] { -2.0f, -1.6f })
            GameKit.Box(transform, "RackPost", K + new Vector3(rx, 0.95f, rz), new Vector3(0.035f, 1.9f, 0.035f), rackM);
        for (int sh = 0; sh < 4; sh++)
        {
            GameKit.Box(transform, "RackShelf", K + new Vector3(-1.8f, 0.25f + sh * 0.52f, 0.3f), new Vector3(0.44f, 0.025f, 1.2f), rackM);
            for (int k = 0; k < 3; k++)
                PropKit.Place(transform, k % 2 == 0 ? "pot_enamel_01" : "brass_pot_02", K + new Vector3(-1.8f, 0.27f + sh * 0.52f, -0.05f + k * 0.38f), sh * 40f + k * 70f, 0.9f);
        }
        Hide(B("RackBlock", K + new Vector3(-1.8f, 0.95f, 0.3f), new Vector3(0.46f, 1.9f, 1.22f), Color.black, true));
        Tex(B("Pallet", K + new Vector3(1.6f, 0.07f, -0.7f), new Vector3(0.8f, 0.14f, 1.1f), new Color(0.55f, 0.42f, 0.28f), true), "wood", 0.6f, new Color(0.55f, 0.42f, 0.28f));
        for (int k = 0; k < 6; k++)
        {
            var sack = B("Sack", K + new Vector3(1.42f + (k % 2) * 0.36f, 0.32f + (k / 2) * 0.36f, -1.0f + (k % 3) * 0.28f), new Vector3(0.38f, 0.34f, 0.5f), new Color(0.72f, 0.66f, 0.52f), true, 0.1f);
            GameKit.Surface(sack, "fabric", 0.4f, 1f, 1f, new Color(0.72f, 0.66f, 0.52f));
            sack.transform.rotation = Quaternion.Euler(0f, k * 13f - 20f, 0f);
        }
        var cab = B("ColdCabinet", K + new Vector3(3.25f, 1.0f, 1.6f), new Vector3(0.6f, 2.0f, 0.8f), steel, true, 0.6f, 0.8f);
        B("ColdCabinetHandle", K + new Vector3(2.93f, 1.1f, 1.45f), new Vector3(0.03f, 0.5f, 0.03f), new Color(0.3f, 0.3f, 0.3f), false, 0.7f, 0.9f);
        // a pan hanging by the door on a nail: the first thing that rings if anything brushes it
        PropKit.Place(transform, "brass_pan_01", K + new Vector3(-3.92f, 1.55f, 0.75f), 0f, 1.1f, new Vector3(0f, 0f, 90f));
        PL(K + new Vector3(-1.5f, 2.7f, 0), new Color(0.95f, 0.97f, 1f), 2.6f, 9f);
        PL(K + new Vector3(2f, 2.7f, 0.5f), new Color(0.95f, 0.97f, 1f), 2.4f, 9f);
        Glow("KitchenTube", K + new Vector3(0, 2.95f, 0), new Vector3(1.4f, 0.05f, 0.12f), new Color(0.92f, 0.97f, 1f), 2.6f);
        B("KitchenExit", K + new Vector3(3.97f, 1.05f, 0), new Vector3(0.05f, 2.1f, 1f), new Color(0.3f, 0.3f, 0.32f));

        // ---- the alleys: corridor, fork (left = dead end, right = the way out), dumpster, dhaba
        RenderSettings.fogDensity = 0.0035f;
        var ground = B("AlleyGround", A + new Vector3(0, -0.05f, 6f), new Vector3(30f, 0.1f, 40f), Color.white, true, 0.3f);
        GameKit.Surface(ground, "cobble", 2.4f, 1f, 1f, new Color(0.88f, 0.86f, 0.83f));
        Wall(A + new Vector3(-1.1f, 0, -9f), A + new Vector3(-1.1f, 0, 0f));
        Wall(A + new Vector3(1.1f, 0, -9f), A + new Vector3(1.1f, 0, 0f));
        Wall(A + new Vector3(-1.1f, 0, -9.2f), A + new Vector3(1.1f, 0, -9.2f));
        Wall(A + new Vector3(-10f, 0, 2.2f), A + new Vector3(8f, 0, 2.2f));            // north side of the cross-alley
        Wall(A + new Vector3(-10f, 0, 0f), A + new Vector3(-1.1f, 0, 0f));             // south side, west branch
        Wall(A + new Vector3(1.1f, 0, 0f), A + new Vector3(10.2f, 0, 0f));             // south side, east branch
        Wall(A + new Vector3(-10.2f, 0, 0f), A + new Vector3(-10.2f, 0, 2.2f));        // the dead end
        Wall(A + new Vector3(10.2f, 0, 0f), A + new Vector3(10.2f, 0, 14f));           // east side of the north run
        Wall(A + new Vector3(7.8f, 0, 2.2f), A + new Vector3(7.8f, 0, 14f));           // west side of the north run
        for (int i = 0; i < 4; i++) Hide(B("Trash", A + new Vector3(-9.4f, 0.3f, 0.5f + i * 0.4f), new Vector3(0.6f, 0.6f, 0.4f), new Color(0.15f, 0.2f, 0.15f), true));
        B("Dumpster", A + new Vector3(8.35f, 0.65f, 7f), new Vector3(0.9f, 1.3f, 1.8f), new Color(0.12f, 0.3f, 0.2f), true, 0.3f, 0.3f);
        PL(A + new Vector3(0, 3f, -4f), new Color(1f, 0.7f, 0.4f), 1.6f, 7f);
        PL(A + new Vector3(-5f, 3f, 1.1f), new Color(1f, 0.6f, 0.3f), 1.0f, 6f);
        PL(A + new Vector3(9f, 3f, 5f), new Color(0.8f, 0.85f, 1f), 1.2f, 7f).gameObject.AddComponent<FlickerLight>();
        // dhaba: an open, lit yard full of truck drivers
        var yard = A + new Vector3(9f, 0, 19f);
        for (int i = 0; i < 3; i++)
        {
            var c = yard + new Vector3(-3f + i * 3f, 0, 0);
            B("Charpai", c + new Vector3(0, 0.45f, 0), new Vector3(1.0f, 0.1f, 1.9f), new Color(0.85f, 0.75f, 0.5f), true, 0.1f);
            for (int l = 0; l < 4; l++) B("CharpaiLeg", c + new Vector3(l < 2 ? -0.45f : 0.45f, 0.22f, l % 2 == 0 ? -0.9f : 0.9f), new Vector3(0.08f, 0.45f, 0.08f), new Color(0.35f, 0.22f, 0.12f));
            if (i == 1) continue;                    // the middle charpai is free: Raj and Arjun take it
            // a driver on the edge of his charpai, his glass of chai on a stool beside him
            var drv = Npc(c + new Vector3(0.95f, 0, 0.35f), 90f, NpcStyle.Crowd(900 + i), "TruckDriver" + i, false);
            drv.Sit(c + new Vector3(0.44f, 0.52f, 0.35f), 90f, 0.42f, true);
            drv.lookTarget = null;
            dhabaDrivers.Add(drv);
            var cup = Cy("DriverChai", c + new Vector3(0.36f, 0.545f, -0.1f), 0.032f, 0.09f, new Color(0.62f, 0.62f, 0.64f), 0.7f, 0.9f);
            Destroy(cup.GetComponent<Collider>());
            dhabaCups.Add(cup.transform);
        }
        // the cook at the tandoor, and a stool for the radio playing low
        var cook = Npc(yard + new Vector3(-2.7f, 0, 3.1f), -130f, NpcStyle.Crowd(931), "DhabaCook", false);
        cook.lookTarget = null;
        dhabaDrivers.Add(cook);
        dhabaCups.Add(null);
        var radioAt = yard + new Vector3(1.2f, 0.75f, -1.05f);
        B("Radio", radioAt, new Vector3(0.26f, 0.15f, 0.09f), new Color(0.25f, 0.16f, 0.1f), false, 0.4f);
        dhabaRadio = radioAt;
        Prop("plastic_monobloc_chair_01", A + new Vector3(7.6f, 0f, 17.75f), 57f + 180f);           // the seat Arjun takes
        Hide(B("Truck", yard + new Vector3(4.5f, 1.5f, 3.5f), new Vector3(2.4f, 3f, 6f), new Color(0.9f, 0.45f, 0.1f), true, 0.4f));      // collider only
        BuildTruck(yard + new Vector3(4.5f, 0f, 3.5f));
        for (int i = 0; i < 3; i++)
        {
            var c2 = yard + new Vector3(-3f + i * 3f, 0, -1.2f);
            Prop("plastic_monobloc_chair_01", c2 + new Vector3(0.9f, 0, 0), 200f + i * 40f);
            Prop("metal_stool_01", c2 + new Vector3(-0.2f, 0, 0.3f), 0f, 0.55f);
            Prop("brass_pot_02", c2 + new Vector3(-0.2f, 0.49f, 0.3f), i * 70f, 0.9f);
        }
        GameKit.Text(transform, "HORN OK PLEASE", yard + new Vector3(4.5f, 1.75f, 0.49f), Quaternion.identity, 0.18f, new Color(0.98f, 0.9f, 0.3f));
        Turned("Tandoor", yard + new Vector3(-3.5f, 0f, 3.5f), new[] { new Vector2(0f, 0.5f), new Vector2(0.35f, 0.58f), new Vector2(0.75f, 0.5f), new Vector2(0.95f, 0.3f), new Vector2(1.0f, 0.24f) },
            ProcTex.Mat(ProcTex.Get("plaster"), new Color(0.62f, 0.4f, 0.28f), new Vector2(3f, 2f), 1f, 1.3f), 18);
        Glow("TandoorFire", yard + new Vector3(-3.5f, 0.97f, 3.5f), new Vector3(0.36f, 0.04f, 0.36f), new Color(1f, 0.35f, 0.08f), 5f);
        for (int i = 0; i < 3; i++) Prop("trashbag", A + new Vector3(-9.4f, 0, 0.5f + i * 0.5f), i * 80f, 0.9f);
        PL(yard + new Vector3(-3.5f, 1.5f, 3.2f), new Color(1f, 0.5f, 0.2f), 2.5f, 7f);
        for (int i = 0; i < 12; i++) Glow("StringBulb", yard + new Vector3(-6f + i * 1.1f, 3.2f - Mathf.Sin(i / 11f * Mathf.PI) * 0.4f, -2f), new Vector3(0.1f, 0.1f, 0.1f), Color.HSVToRGB(i * 0.13f % 1f, 0.6f, 1f), 5f);
        PL(yard + new Vector3(0, 3f, 0), new Color(1f, 0.8f, 0.55f), 3f, 12f);
        // keep the player in the yard, leaving the gap where the alley comes in
        Fence(A + new Vector3(9f, 2f, 25f), new Vector3(16f, 4f, 0.4f));
        Fence(A + new Vector3(17f, 2f, 19.5f), new Vector3(0.4f, 4f, 11f));
        Fence(A + new Vector3(1f, 2f, 19.5f), new Vector3(0.4f, 4f, 11f));
        Fence(A + new Vector3(4.4f, 2f, 14f), new Vector3(6.8f, 4f, 0.4f));
        Fence(A + new Vector3(13.6f, 2f, 14f), new Vector3(6.8f, 4f, 0.4f));
        GameKit.Text(transform, "SHER-E-PUNJAB DHABA  -  OPEN 24 HRS", yard + new Vector3(0, 3.8f, -2.2f), Quaternion.identity, 0.3f, new Color(1f, 0.85f, 0.3f));
        StartCoroutine(DhabaLife(dhabaDrivers.ToArray(), dhabaCups.ToArray()));
        StartCoroutine(DhabaSound());
    }
    readonly List<CharacterNPC> dhabaDrivers = new List<CharacterNPC>();
    readonly List<Transform> dhabaCups = new List<Transform>();
    Vector3 dhabaRadio;

    // ============================================================ B: KAPOOR HOUSE
    CharacterNPC kamala, rohan;
    Transform laptop, laptopLid, bootPrint, visitorPass;
    Light laptopGlow;
    bool laptopRead, laptopClosed, printSeen, passSeen, confronting;

    void BuildKapoorHouse()
    {
        RenderSettings.fogColor = new Color(0.1f, 0.09f, 0.08f); RenderSettings.fogDensity = 0.008f;
        CinematicLook.Ambient(new Color(0.6f, 0.55f, 0.5f), new Color(0.42f, 0.36f, 0.3f), new Color(0.15f, 0.12f, 0.1f));
        PriyaRoomPlayer.Surface = 0;
        Room("House", Vector3.zero, new Vector3(9f, 3f, 8f), new Color(0.82f, 0.78f, 0.62f), Color.white, "marble", "plaster");
        var wood = new Color(0.4f, 0.26f, 0.15f);
        // a real family living room, turned over: sofa with slashed cushions
        var sofa = B("Sofa", new Vector3(-2.5f, 0.35f, 2.8f), new Vector3(2.4f, 0.7f, 0.9f), new Color(0.45f, 0.18f, 0.2f), true, 0.15f);
        var sofaBack = B("SofaBack", new Vector3(-2.5f, 0.85f, 3.2f), new Vector3(2.4f, 0.6f, 0.2f), new Color(0.45f, 0.18f, 0.2f), false, 0.15f);
        bool realSofa = Prop("Sofa_01", new Vector3(-2.5f, 0, 2.95f), 180f, 1.4f) != null;
        if (realSofa) { Hide(sofa); Hide(sofaBack); }
        var rnd = new System.Random(8);
        for (int i = 0; i < 18; i++)
        {
            var st = Sp("Stuffing", new Vector3(-3.4f + (float)rnd.NextDouble() * 1.9f, (realSofa ? 0.6f : 0.72f) + (float)rnd.NextDouble() * 0.1f, 2.55f + (float)rnd.NextDouble() * 0.4f), 0.12f, new Color(0.95f, 0.95f, 0.92f), 0.05f);
            st.transform.localScale = new Vector3(0.13f, 0.07f + (float)rnd.NextDouble() * 0.05f, 0.11f);
        }
        Prop("ArmChair_01", new Vector3(-3.9f, 0, 0.4f), 90f);
        Prop("ceiling_fan", new Vector3(0.6f, 3.0f, 0.6f), 20f);
        Prop("wall_clock", new Vector3(0f, 2.2f, 3.97f), 180f);
        Prop("hanging_picture_frame_01", new Vector3(-2.5f, 1.95f, 3.97f), 180f, 1f, new Vector3(0, 0, 7f));      // knocked crooked
        Prop("hanging_picture_frame_01", new Vector3(-1.6f, 1.9f, 3.97f), 180f, 0.8f, new Vector3(0, 0, -11f));
        Prop("potted_plant_02", new Vector3(-0.4f, 0.28f, -3.3f), 40f, 1f, new Vector3(82f, 0, 0));                 // tipped over
        Prop("brass_vase_01", new Vector3(0.9f, 0.1f, 3.1f), 60f, 0.6f, new Vector3(88f, 0, 0));
        var cushion = B("Cushion", new Vector3(-1.2f, 0.1f, 1.8f), new Vector3(0.5f, 0.15f, 0.5f), new Color(0.45f, 0.18f, 0.2f), false, 0.1f);
        cushion.transform.rotation = Quaternion.Euler(10f, 35f, 0f);
        // overturned bookshelf and books
        var shelf = B("OverturnedShelf", new Vector3(2.8f, 0.25f, 2.6f), new Vector3(2f, 0.5f, 0.45f), wood, true, 0.2f);
        shelf.transform.rotation = Quaternion.Euler(0, 15f, 0);
        if (Prop("wooden_bookshelf_worn", new Vector3(1.8f, 0.3f, 2.87f), 105f, 1f, new Vector3(90f, 0, 0)) != null) Hide(shelf);    // pulled down on its face
        for (int i = 0; i < 14; i++)
        {
            var bk = B("Book", new Vector3(1.5f + (float)rnd.NextDouble() * 1.4f, 0.025f + (i % 3) * 0.045f, 2.0f + (float)rnd.NextDouble() * 0.9f), new Vector3(0.22f, 0.045f, 0.3f), i % 2 == 0 ? new Color(0.16f, 0.1f, 0.06f) : Color.HSVToRGB((float)rnd.NextDouble(), 0.45f, 0.5f), false, 0.1f);
            bk.transform.rotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 180f, 0);
        }
        // drawers pulled out where the search went (papers, the almirah and the boot prints: BuildHouseDetails)
        var cab = B("Cabinet", new Vector3(4.2f, 0.5f, -1f), new Vector3(0.5f, 1f, 1.4f), wood, true, 0.3f);
        if (Prop("vintage_cabinet_01", new Vector3(4.18f, 0, -1f), -90f, 0.7f) != null) Hide(cab);
        for (int i = 0; i < 3; i++)
        {
            var dr = B("PulledDrawer", new Vector3(3.5f - i * 0.3f, 0.12f + i * 0.02f, -1.6f + i * 0.5f), new Vector3(0.5f, 0.2f, 0.45f), wood * 1.1f, false, 0.3f);
            dr.transform.rotation = Quaternion.Euler(0, 20f * i, i == 1 ? 25f : 0f);
        }
        // broken photo frame
        var frame = B("BrokenFrame", new Vector3(0.3f, 0.02f, 3.4f), new Vector3(0.35f, 0.03f, 0.45f), new Color(0.55f, 0.4f, 0.15f), false, 0.6f);
        frame.transform.rotation = Quaternion.Euler(0, 30f, 0);
        if (Prop("hanging_picture_frame_01", new Vector3(0.3f, 0.03f, 3.4f), 30f, 0.7f, new Vector3(90f, 0, 0)) != null) Hide(frame);
        // kitchen table with Rohan's laptop
        var ktable = B("KitchenTable", new Vector3(2.4f, 0.75f, -2.4f), new Vector3(1.6f, 0.06f, 1f), wood, true, 0.4f);
        if (Prop("round_wooden_table_01", new Vector3(2.4f, 0, -2.4f), 0f, 0.765f) != null) Hide(ktable);
        else foreach (float lx in new[] { -0.7f, 0.7f }) foreach (float lz in new[] { -0.4f, 0.4f })
                    B("TableLeg", new Vector3(2.4f + lx, 0.37f, -2.4f + lz), new Vector3(0.05f, 0.75f, 0.05f), wood);
        Prop("painted_wooden_chair_01", new Vector3(2.4f, 0, -1.45f), 180f);
        Prop("painted_wooden_chair_01", new Vector3(3.5f, 0.24f, -2.9f), 250f, 1f, new Vector3(0, 0, 88f));          // kicked over
        Prop("tea_set_01", new Vector3(2.75f, 0.775f, -2.6f), 30f, 0.5f);
        B("LaptopBase", new Vector3(2.4f, 0.8f, -2.4f), new Vector3(0.36f, 0.02f, 0.25f), new Color(0.2f, 0.2f, 0.22f), false, 0.6f, 0.5f);
        B("LaptopKeys", new Vector3(2.4f, 0.8105f, -2.43f), new Vector3(0.3f, 0.002f, 0.12f), new Color(0.08f, 0.08f, 0.09f), false, 0.3f);
        laptopLid = new GameObject("LaptopLid").transform; laptopLid.SetParent(transform, false);
        laptopLid.position = new Vector3(2.4f, 0.811f, -2.285f);            // the hinge at the back edge
        laptopLid.rotation = Quaternion.Euler(-102f, 0f, 0f);               // open, leaning back a little
        GameKit.Box(laptopLid, "LidBack", new Vector3(0f, 0f, 0.125f), new Vector3(0.36f, 0.008f, 0.25f), GameKit.Lit(new Color(0.2f, 0.2f, 0.22f), 0.6f, 0.5f));
        var scr = GameKit.Box(laptopLid, "LaptopScreen", new Vector3(0f, -0.0045f, 0.125f), new Vector3(0.33f, 0.002f, 0.22f), GameKit.Glow(new Color(0.25f, 0.35f, 0.5f), new Color(0.5f, 0.7f, 1f), 1.8f));
        laptop = scr.transform;
        laptopGlow = PL(new Vector3(2.4f, 1.2f, -2.6f), new Color(0.55f, 0.75f, 1f), 0.8f, 2.5f);
        // optional clues: a boot print by the door, a dropped visitor pass
        bootPrint = B("BootPrint", new Vector3(-3.6f, 0.008f, -3f), new Vector3(0.14f, 0.004f, 0.32f), new Color(0.2f, 0.14f, 0.08f), false, 0.1f).transform;
        visitorPass = B("VisitorPass", new Vector3(1.5f, 0.01f, -2.9f), new Vector3(0.09f, 0.004f, 0.13f), new Color(0.2f, 0.45f, 0.85f), false, 0.3f).transform;
        // lights: one bulb swinging on its cord, a lamp knocked over
        PL(new Vector3(0, 2.6f, 0), new Color(1f, 0.85f, 0.6f), 2.6f, 8f);
        Glow("Bulb", new Vector3(0, 2.7f, 0), new Vector3(0.14f, 0.14f, 0.14f), new Color(1f, 0.85f, 0.6f), 5f);
        var lamp = Cy("FallenLamp", new Vector3(-0.6f, 0.15f, 2.3f), 0.12f, 0.5f, new Color(0.7f, 0.6f, 0.4f));
        lamp.transform.rotation = Quaternion.Euler(0, 0, 85f);
        if (Prop("vintage_oil_lamp", new Vector3(-0.3f, 0.11f, 2.3f), 0f, 0.8f, new Vector3(0, 0, 86f)) != null) Hide(lamp);
        kamala = Npc(new Vector3(-2.4f, 0f, 1.8f), 200f, Cast.Kamala(), "Kamala");
        rohan = Npc(new Vector3(1.5f, 0f, 0.5f), 90f, Cast.Rohan(), "Rohan");
        BuildHouseDetails();
    }

    // =========================================================== C: INSPECTOR REDDY
    CharacterNPC reddy;

    void BuildPoliceStreet()
    {
        RenderSettings.fogColor = new Color(0.05f, 0.05f, 0.09f); RenderSettings.fogDensity = 0.012f;
        CinematicLook.Ambient(new Color(0.32f, 0.34f, 0.5f), new Color(0.24f, 0.24f, 0.32f), new Color(0.08f, 0.08f, 0.1f));
        PriyaRoomPlayer.Surface = 1;
        var road = B("Road", new Vector3(0, -0.05f, 0), new Vector3(40f, 0.1f, 30f), new Color(0.2f, 0.2f, 0.22f), true, 0.6f);
        GameKit.Surface(road, "asphalt", 2.2f, 1f, 1f, new Color(0.3f, 0.3f, 0.32f));
        GameKit.Surface(B("Pavement", new Vector3(0, 0.08f, 6f), new Vector3(40f, 0.16f, 3f), new Color(0.5f, 0.48f, 0.45f), true), "stone", 1f);
        // police station facade
        var station = B("PoliceStation", new Vector3(0, 4f, 10f), new Vector3(18f, 8f, 3f), new Color(0.85f, 0.83f, 0.78f), true, 0.1f);
        GameKit.SurfaceZY(station, "facade", 3f, new Color(0.9f, 0.88f, 0.82f));
        B("StationBand", new Vector3(0, 3.2f, 8.45f), new Vector3(18f, 0.6f, 0.1f), new Color(0.1f, 0.2f, 0.5f));
        GameKit.Text(transform, "CHANDNIPUR POLICE STATION", new Vector3(0, 3.2f, 8.38f), Quaternion.identity, 0.32f, Color.white);
        B("StationDoor", new Vector3(0, 1.3f, 8.48f), new Vector3(2.2f, 2.6f, 0.05f), new Color(0.15f, 0.2f, 0.25f), false, 0.6f);
        for (int i = 0; i < 6; i++) Glow("StationWindow", new Vector3(-7.5f + i * 3f + (i >= 3 ? 1f : -1f), 5.5f, 8.47f), new Vector3(1.2f, 1.2f, 0.04f), i % 2 == 0 ? new Color(0.85f, 0.95f, 1f) : new Color(0.15f, 0.15f, 0.2f), 1.2f);
        Glow("BlueLamp", new Vector3(0, 7.4f, 8.4f), new Vector3(0.4f, 0.4f, 0.4f), new Color(0.2f, 0.4f, 1f), 5f);
        PL(new Vector3(0, 6.5f, 7.5f), new Color(0.35f, 0.5f, 1f), 3f, 12f);
        // police jeep
        Hide(B("Jeep", new Vector3(6f, 0.9f, 3.5f), new Vector3(1.8f, 1.2f, 4f), new Color(0.92f, 0.92f, 0.9f), true, 0.6f, 0.3f));      // collider only
        BuildJeep(new Vector3(6f, 0f, 3.5f));
        // steps, door frame and a notice board for the station; barricade drums on the road
        Tex(B("StationSteps", new Vector3(0, 0.2f, 8.1f), new Vector3(3.4f, 0.1f, 0.9f), Color.white, true), "concrete", 1.4f, new Color(0.8f, 0.78f, 0.74f));
        foreach (float dx in new[] { -1.25f, 1.25f }) Tex(B("StationPillar", new Vector3(dx, 1.5f, 8.35f), new Vector3(0.3f, 3f, 0.3f), Color.white, false), "plaster", 1.6f, new Color(0.88f, 0.86f, 0.8f));
        Tex(B("NoticeBoard", new Vector3(-3.2f, 1.6f, 8.46f), new Vector3(1.4f, 1f, 0.05f), new Color(0.2f, 0.3f, 0.2f), false), "wood", 0.9f, new Color(0.25f, 0.35f, 0.25f));
        // the stretch of the station's wall behind the chai stall (it is in frame through Reddy's interview): a barred
        // window with a light on behind it, a downpipe, a painted helpline sign, the plinth band
        B("StationWindowRecess", new Vector3(-7.4f, 1.75f, 8.47f), new Vector3(1.2f, 1.1f, 0.04f), new Color(0.05f, 0.05f, 0.06f), false, 0.6f);
        Glow("StationWindowLit", new Vector3(-7.4f, 1.75f, 8.465f), new Vector3(1.1f, 1.0f, 0.01f), new Color(0.75f, 0.85f, 0.7f), 0.5f);
        for (int i = 0; i < 6; i++) B("StationWindowBar", new Vector3(-7.9f + i * 0.2f, 1.75f, 8.44f), new Vector3(0.025f, 1.1f, 0.025f), new Color(0.12f, 0.12f, 0.13f), false, 0.4f, 0.6f);
        B("StationWindowSill", new Vector3(-7.4f, 1.17f, 8.4f), new Vector3(1.35f, 0.06f, 0.16f), new Color(0.7f, 0.68f, 0.62f), false, 0.3f);
        Cy("Downpipe", new Vector3(-5.15f, 2.6f, 8.4f), 0.05f, 5.2f, new Color(0.25f, 0.26f, 0.27f), 0.35f, 0.4f);
        B("StationPlinthBand", new Vector3(-6.5f, 0.3f, 8.47f), new Vector3(5f, 0.6f, 0.03f), new Color(0.42f, 0.38f, 0.34f), false, 0.2f);
        B("HelplineSign", new Vector3(-9.1f, 2.15f, 8.46f), new Vector3(0.9f, 0.45f, 0.03f), new Color(0.75f, 0.12f, 0.1f), false, 0.4f);
        GameKit.Solid(GameKit.Text(transform, "POLICE HELP  100", new Vector3(-9.1f, 2.18f, 8.443f), Quaternion.identity, 0.09f, Color.white));
        GameKit.Solid(GameKit.Text(transform, "WOMEN HELPLINE 1091", new Vector3(-9.1f, 2.03f, 8.443f), Quaternion.identity, 0.055f, Color.white));
        for (int i = 0; i < 3; i++) Prop("barrel_03", new Vector3(9.5f + i * 0.9f, 0, 0.6f + (i % 2) * 0.3f), i * 50f);
        Prop("trashbag", new Vector3(-9.5f, 0.16f, 6.6f), 30f);
        Prop("old_tyre", new Vector3(-8.6f, 0.24f, 6.9f), 10f, 1f, new Vector3(90f, 0, 0));
        // the chai stall, plastic chairs, Reddy
        var stall = new Vector3(-6f, 0, 5.5f);
        B("StallCounter", stall + new Vector3(0, 0.5f, 0.6f), new Vector3(2.2f, 1f, 0.8f), new Color(0.45f, 0.3f, 0.18f), true, 0.2f);
        var tarpMat = ProcTex.Mat(ProcTex.Get("fabric"), new Color(0.14f, 0.34f, 0.6f), new Vector2(2.9f, 1.9f) / 0.35f, 0.45f, 0.6f);
        if (tarpMat.HasProperty("_Cull")) tarpMat.SetFloat("_Cull", 0f);
        VehicleKit.Part(transform, "StallRoof", VehicleKit.Tarp(2.9f, 1.9f, 0.14f), tarpMat, stall + new Vector3(0, 2.55f, 0.3f)).transform.rotation = Quaternion.Euler(8f, 0, 0);
        foreach (float px in new[] { -1.35f, 1.35f }) foreach (float pz in new[] { -0.55f, 1.15f })
                Cy("StallPole", stall + new Vector3(px, 1.3f, pz), 0.028f, 2.6f, new Color(0.6f, 0.5f, 0.28f), 0.35f);
        // the vendor's things at the far (east) end and the back of the counter: the near end is where the evidence goes
        if (Prop("metal_jug", stall + new Vector3(0.85f, 1.0f, 0.85f), 40f, 1.1f) == null)
            Cy("Kettle", stall + new Vector3(0.85f, 1.2f, 0.85f), 0.16f, 0.36f, new Color(0.75f, 0.76f, 0.78f), 0.85f, 0.9f);
        Prop("brass_pot_01", stall + new Vector3(0.45f, 1.0f, 0.88f), 0f, 0.8f);
        Prop("pot_enamel_01", stall + new Vector3(0.9f, 1.0f, 0.45f), 70f, 1.0f);
        Prop("small_lpg_tank", stall + new Vector3(1.35f, 0, 0.9f), 0f, 1.2f);
        Glow("StallBulb", stall + new Vector3(0, 2.2f, 0.2f), new Vector3(0.15f, 0.15f, 0.15f), new Color(1f, 0.8f, 0.45f), 5f);
        PL(stall + new Vector3(0, 2f, -0.4f), new Color(1f, 0.75f, 0.45f), 2.4f, 6f);
        for (int i = 0; i < 3; i++)
        {
            var cp = stall + new Vector3(-0.9f + i * 0.9f, 0, -1.2f);
            if (Prop("plastic_monobloc_chair_01", cp + new Vector3(i == 1 ? 0.25f : 0f, 0, i == 1 ? -0.9f : 0f), 180f + (i - 1) * 25f) != null) continue;
            B("PlasticChair", cp + new Vector3(0, 0.45f, 0), new Vector3(0.45f, 0.05f, 0.45f), i == 1 ? new Color(0.85f, 0.15f, 0.12f) : new Color(0.15f, 0.4f, 0.8f), false, 0.4f);
            B("PlasticBack", cp + new Vector3(0, 0.72f, 0.2f), new Vector3(0.45f, 0.5f, 0.04f), i == 1 ? new Color(0.85f, 0.15f, 0.12f) : new Color(0.15f, 0.4f, 0.8f), false, 0.4f);
        }
        reddy = Npc(ReddyAt, 90f, Cast.Reddy(), "Inspector Reddy", false);
        reddy.lookTarget = cam;
        // street lights + a stray dog
        for (int i = 0; i < 4; i++)
        {
            float x = -15f + i * 10f;
            Cy("LampPost", new Vector3(x, 2.5f, 4.6f), 0.08f, 5f, new Color(0.3f, 0.3f, 0.3f), 0.4f, 0.6f);
            Glow("LampHead", new Vector3(x, 5f, 4.2f), new Vector3(0.5f, 0.12f, 0.25f), new Color(1f, 0.85f, 0.55f), 3f);
            PL(new Vector3(x, 4.7f, 3.8f), new Color(1f, 0.8f, 0.5f), 2.2f, 10f);
        }
        GameKit.Sphere(transform, "Moon", new Vector3(-30f, 40f, 90f), 7f, GameKit.Unlit(new Color(0.9f, 0.93f, 1f)));
        FenceArea(new Vector3(0, 0, -1f), 36f, 18f);
    }

    // where the conversation happens: the customer side of the stall's counter, Reddy leaning at its end
    static readonly Vector3 ReddyAt = new Vector3(-6.95f, 0f, 5.28f);
    static readonly Vector3 ArjunAtStall = new Vector3(-5.45f, 0.1f, 5.22f);
    const float StallTop = 1.0f;
    readonly List<EvidenceFolio.Item> pieces = new List<EvidenceFolio.Item>();
    Transform reddyHandL, reddyHandR;
    // read by the play-test route
    readonly List<string> presentedLog = new List<string>();
    int reddyRound;

    /// <summary>One piece Arjun can put on the counter: what it is, what it shows, and the thing itself.</summary>
    EvidenceFolio.Item Piece(string key, string name, string shows, Transform obj)
    {
        var it = new EvidenceFolio.Item { key = key, name = name, shows = shows, obj = obj };
        if (obj != null) obj.gameObject.SetActive(false);         // in Arjun's pocket until he lays it down
        pieces.Add(it);
        return it;
    }

    /// <summary>The evidence this player actually has, built as things on the counter.</summary>
    void GatherPieces()
    {
        var d = SaveSystem.Data;
        pieces.Clear();
        var root = new GameObject("CounterEvidence").transform; root.SetParent(transform, false);
        // laid facing Arjun: the tops of the papers towards Reddy (-x), readable from where he stands
        if (d.hasIdCard)
            Piece("id", "Her ID card", "Her Crime Branch card, dropped in the market while she watched Vikram's stall.", PaperKit.IdCard(root, Vector3.zero, -90f));
        Piece("signout", "The sign-out sheet", "The Record Room register: the 1942 deed signed out two weeks ago. \"S. Reddy\", in her hand.", PaperKit.SignOutSheet(root, Vector3.zero, -90f));
        Piece("usb", "Priya's files", "Copies from Priya's encrypted files: the forged 1965 order beside the 1942 deed, every discrepancy marked.", PaperKit.Printouts(root, Vector3.zero, -90f));
        if (d.clues.Contains("3_NewspaperClippings"))
            Piece("clip", "Priya's cuttings", "The cuttings from Priya's wall, still knotted with her red string: the Renewal Project and 500 families.", PaperKit.Cuttings(root, Vector3.zero, -90f));
        if (d.hasPlate)
            Piece("plate", "The SUV's plate", "MH-12-AB-4471, written down as the black SUV passed: the car that followed you.", PaperKit.NotebookPage(root, Vector3.zero, -90f, "MH-12-AB-4471", "black SUV - market lane"));
        else if (d.evidence.Contains("A black SUV followed Priya's auto."))      // only testimony this player actually heard
            Piece("bhaskar", "Bhaskar's account", "Bhaskar, who drove Priya two nights ago: a black SUV followed her auto.", PaperKit.NotebookPage(root, Vector3.zero, -90f, "Bhaskar (auto):", "black SUV followed P."));
    }

    /// <summary>Where piece i of n rests on the counter, between Arjun and Reddy.</summary>
    Vector3 PieceSpot(int i, int n)
    {
        float x0 = -6.5f, x1 = -5.72f;
        float x = n <= 1 ? (x0 + x1) * 0.5f : Mathf.Lerp(x0, x1, i / (float)(n - 1));
        return new Vector3(x, StallTop + 0.002f, 5.86f + (i % 2) * 0.04f);           // near the front edge, between them
    }

    /// <summary>Arjun lays the pieces down one at a time, from his hand to the counter.</summary>
    IEnumerator LayOut()
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            var it = pieces[i];
            if (it.obj == null) continue;
            Vector3 to = PieceSpot(i, pieces.Count);
            Vector3 from = cam.position + cam.right * 0.16f - Vector3.up * 0.42f + cam.forward * 0.22f;
            it.obj.gameObject.SetActive(true);
            Quaternion r1 = Quaternion.Euler(0f, -90f + (i % 2 == 0 ? -4f : 5f), 0f), r0 = Quaternion.Euler(-35f, -60f, 0f);
            for (float t = 0f; t < 0.42f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.42f);
                it.obj.position = Vector3.Lerp(from, to, k) + Vector3.up * 0.05f * Mathf.Sin(k * Mathf.PI);
                it.obj.rotation = Quaternion.Slerp(r0, r1, k);
                yield return null;
            }
            it.obj.position = to; it.obj.rotation = r1;
            EvidenceFolio.Repose(it);
            sfx.PlayOneShot(EvidenceFolio.Put(), 0.5f);
            yield return Wait(0.18f);
        }
    }

    /// <summary>Arjun turns a piece round and slides it across to her (back = takes it back to where it lay).</summary>
    IEnumerator SlidePiece(EvidenceFolio.Item it, bool across)
    {
        if (it.obj == null) yield break;
        if (!homes.ContainsKey(it)) homes[it] = new Pose(it.basePos, it.baseRot);
        var home = homes[it];
        Vector3 a = it.obj.position, b = across ? new Vector3(-6.62f, StallTop + 0.002f, 5.9f) : home.position;
        Quaternion ra = it.obj.rotation, rb = across ? Quaternion.Euler(0f, 90f, 0f) : home.rotation;     // turned round, so she can read it
        // the folio keeps every piece at its resting pose: move the rest with the hand
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.5f);
            it.basePos = Vector3.Lerp(a, b, k) + Vector3.up * 0.012f * Mathf.Sin(k * Mathf.PI);
            it.baseRot = Quaternion.Slerp(ra, rb, k);
            it.obj.position = it.basePos; it.obj.rotation = it.baseRot;
            yield return null;
        }
        it.basePos = b; it.baseRot = rb;
        it.obj.position = b; it.obj.rotation = rb;
    }
    readonly Dictionary<EvidenceFolio.Item, Pose> homes = new Dictionary<EvidenceFolio.Item, Pose>();

    /// <summary>She turns her own ID card face down under her hand (the one resting on the counter), without a word.</summary>
    IEnumerator TurnOverCard(EvidenceFolio.Item it)
    {
        if (it.obj == null || reddy == null) yield break;
        Vector3 rest = reddyHandL.position;
        Vector3 grip = it.obj.position + Vector3.up * 0.045f;
        for (float t = 0f; t < 0.5f; t += Time.deltaTime) { reddyHandL.position = Vector3.Lerp(rest, grip, Mathf.SmoothStep(0f, 1f, t / 0.5f)); yield return null; }
        Quaternion r0 = it.obj.rotation, r1 = Quaternion.AngleAxis(180f, it.obj.right) * r0;
        Vector3 p0 = it.obj.position;
        for (float t = 0f; t < 0.55f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.55f);
            it.baseRot = Quaternion.Slerp(r0, r1, k);
            it.basePos = p0 + Vector3.up * 0.03f * Mathf.Sin(k * Mathf.PI);
            it.obj.rotation = it.baseRot; it.obj.position = it.basePos;
            reddyHandL.position = it.obj.position + Vector3.up * 0.045f;
            yield return null;
        }
        it.obj.rotation = r1; it.obj.position = p0;
        sfx.PlayOneShot(EvidenceFolio.Put(), 0.35f);
        it.basePos = p0; it.baseRot = r1;
        yield return Wait(0.7f);                                  // her hand stays on it a moment
        Vector3 on = reddyHandL.position;
        for (float t = 0f; t < 0.5f; t += Time.deltaTime) { reddyHandL.position = Vector3.Lerp(on, rest, Mathf.SmoothStep(0f, 1f, t / 0.5f)); yield return null; }
        reddyHandL.position = rest;
    }

    /// <summary>A short answer to a piece that does not do what Arjun needs: what it fails to establish.</summary>
    static string WeakLine(string key, int round)
    {
        if (round == 1)
            return key == "usb" ? "a4c_weak1_usb" : key == "clip" ? "a4c_weak1_clip" : key == "plate" ? "a4c_weak1_plate" : "a4c_weak1_bhaskar";
        return key == "id" ? "a4c_weak2_id" : key == "signout" ? "a4c_weak2_signout" : key == "plate" ? "a4c_weak2_plate" : "a4c_weak2_bhaskar";
    }

    IEnumerator ReddyRun()
    {
        bool met = false;
        AddSpot(reddy.transform, Vector3.up * 1.5f, "Talk to Inspector Reddy", () => phase == Phase.Free && !met, () => { met = true; });
        // she waits at the end of the counter, one hand resting on it, watching the station
        reddyHandL = new GameObject("ReddyHandL").transform; reddyHandL.SetParent(transform, false);
        reddyHandR = new GameObject("ReddyHandR").transform; reddyHandR.SetParent(transform, false);
        reddyHandL.position = new Vector3(-6.86f, StallTop + 0.03f, 5.86f);
        reddy.Hold(0, reddyHandL, 0.01f);
        reddy.lean = 3f;
        var stationLook = new GameObject("ReddyLook").transform; stationLook.SetParent(transform, false);
        stationLook.position = new Vector3(0f, 2.2f, 8.4f);               // the station's door
        reddy.lookTarget = stationLook;
        GatherPieces();
        yield return OpenAct();
        Teleport(new Vector3(4f, 0.1f, -6f), -40f);
        yield return Narrate("a4c_vo");
        BeginFree("Meet Inspector Reddy at the chai stall", reddy.transform.position + Vector3.up * 1.9f, "Inspector Reddy");
        // she has seen him coming: once he is close she turns her head from the station to him
        while (!met)
        {
            if (Near(reddy.transform.position, 6f)) reddy.lookTarget = cam;
            yield return null;
        }
        BeginBusy();
        reddy.lookTarget = cam;
        yield return Glide(ArjunAtStall, -57f, 22f, 1.1f);          // her face and the counter's near end both in view
        yield return Talk("a4c_reddy_1", reddy);
        yield return Talk("a4c_arjun_id");
        // before she answers she looks back at the station, as if to check who is watching
        reddy.lookTarget = stationLook;
        yield return Wait(1.1f);
        reddy.lookTarget = cam;
        yield return Wait(0.3f);
        yield return Talk("a4c_reddy_2", reddy);
        EndTalk();

        // ---- round 1: "I don't know any Priya Kapoor"
        reddyRound = 1;
        yield return Talk("a4c_reddy_r1", reddy);
        EndTalk();
        yield return LayOut();
        yield return EvidenceRound(1, "INSPECTOR REDDY", "\"I don't know any Priya Kapoor.\"", "a4c_reddy_r1ok", new[] { "id", "signout" });
        // ---- round 2: "My investigation is classified"
        reddyRound = 2;
        yield return Talk("a4c_reddy_r2", reddy);
        EndTalk();
        yield return EvidenceRound(2, "INSPECTOR REDDY", "\"You're seeing a fraction of the picture. Stay out of it.\"", "a4c_reddy_r2ok", new[] { "usb", "clip" });
        reddyRound = 3;
        yield return Talk("a4c_reddy_r3", reddy);
        yield return Choose(new[] { "Show my hand", "Whose side?", "Priya first" },
                            new[] { DialogueDB.Text("a4c_q_a"), DialogueDB.Text("a4c_q_b"), DialogueDB.Text("a4c_q_c") }, null, null);
        int c = menuResult;
        yield return Talk(c == 0 ? "a4c_q_a" : c == 1 ? "a4c_q_b" : "a4c_q_c");
        yield return Talk(c == 0 ? "a4c_reddy_a" : c == 1 ? "a4c_reddy_b" : "a4c_reddy_c", reddy);
        yield return Talk("a4c_reddy_reveal", reddy);
        EndTalk();
        SaveSystem.Data.allyReddy = true; SaveSystem.Save();
        AddEvidence("Inspector Reddy is investigating Deshpande undercover; the original 1942 deed is safe in her evidence room.");
        AddEvidence("Reddy's men are guarding Priya at the NGO shelter by the river.");
        reddy.Release(0, 0.6f);
        yield return Narrate("a4c_vo_end");
        yield return CloseAct("", "");
    }

    /// <summary>
    /// One round of the interview: the folio is open over everything on the counter; a piece that answers her claim
    /// ends the round, a weak one gets a short reason and comes back. Her question is not read out again.
    /// </summary>
    IEnumerator EvidenceRound(int round, string who, string claim, string ok, string[] correct)
    {
        while (true)
        {
            yield return Present(pieces, who, claim);
            var it = pieces[menuResult];
            presentedLog.Add(round + ":" + it.key);
            reddy.lookTarget = it.obj != null ? it.obj : cam;      // she looks down at what he puts in front of her
            yield return SlidePiece(it, true);
            yield return Wait(0.5f);
            if (System.Array.IndexOf(correct, it.key) >= 0)
            {
                if (it.key == "signout")
                {
                    // she knows her own hand: a long look at the signature before she says so
                    yield return Wait(1.1f);
                    yield return Talk("a4c_reddy_sig", reddy);
                }
                else if (it.key == "id") yield return TurnOverCard(it);
                else yield return Wait(0.8f);
                reddy.lookTarget = cam;
                yield return Wait(0.35f);
                it.locked = true; it.lockedNote = "On the counter in front of her. She has seen it.";
                yield return Talk(ok, reddy);
                EndTalk();
                yield break;
            }
            yield return Wait(0.3f);
            reddy.lookTarget = cam;
            yield return Talk(WeakLine(it.key, round), reddy);
            EndTalk();
            yield return SlidePiece(it, false);               // he takes it back
        }
    }
}
