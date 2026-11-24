using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shadows of Chandni - Act I: Priya Kapoor's apartment.
/// Builds the whole room from primitives at Awake (no external assets needed),
/// spawns a first-person player, and implements the TAB "detective vision"
/// that highlights the nine clues with a golden glow.
/// Drop this on an empty GameObject in any scene and press Play.
/// </summary>
public class PriyaRoomBuilder : MonoBehaviour
{
    // Room interior: X = -3..3 (6m), Z = -2.5..2.5 (5m), Y = 0..3
    const float HalfW = 3f, HalfD = 2.5f, Height = 3f, Wall = 0.15f;

    Transform root;
    public Transform Root => root;
    // The apartment builder lives on after its room. Only the active market or early Gallery may own the lighting.
    public GameObject RoomEffectsOwner { get; set; }
    public Transform DoorHinge;
    public GameObject ExtraRoot;   // Act II world (destroyed on restart)
    public GameObject HookNote;
    public ClueTag Focused;           // the clue the crosshair is on right now (gets a soft glow)
    public PriyaRoomPlayer PlayerCtrl;
    public bool VisionOn => visionOn;
    public bool VisionUsed;
    public readonly List<ClueTag> ClueTags = new List<ClueTag>();
    readonly List<ClueTag> rendererTags = new List<ClueTag>();
    readonly List<Renderer> clueRenderers = new List<Renderer>();
    readonly List<Color> clueBaseEmission = new List<Color>();
    Transform fan;
    Light sunLight;
    bool visionOn;
    float visionBlend;

    // ---------------------------------------------------------------- colours
    static readonly Color WallYellow = new Color(0.86f, 0.74f, 0.42f);
    static readonly Color Wood = new Color(0.36f, 0.22f, 0.12f);
    static readonly Color DarkWood = new Color(0.22f, 0.13f, 0.08f);
    static readonly Color Cream = new Color(0.9f, 0.86f, 0.76f);
    static readonly Color Maroon = new Color(0.45f, 0.10f, 0.12f);
    static readonly Color Teal = new Color(0.16f, 0.42f, 0.44f);

    void Awake()
    {
        Build();
    }

    // ================================================================== BUILD
    [ContextMenu("Rebuild Room")]
    public void Build()
    {
        GameState.ResetForNewScene();             // back to the menu: never inherit a paused clock or muted audio
        SaveSystem.Load();                        // and leave any chapter replay: the menu shows the real campaign
        if (root != null) Destroy(root.gameObject);
        if (ExtraRoot != null) { Destroy(ExtraRoot); ExtraRoot = null; }
        clueRenderers.Clear();
        clueBaseEmission.Clear();
        rendererTags.Clear();
        ClueTags.Clear();
        var oldStory = GetComponent<PriyaRoomStory>();
        if (oldStory) Destroy(oldStory);

        root = new GameObject("Priya_Apartment").transform;
        root.SetParent(transform, false);

        BuildShell();
        BuildWindow();
        BuildDoor();
        BuildDesk();
        BuildWallBoard();
        BuildBed();
        BuildCloset();
        BuildKitchenAlcove();
        BuildBookshelf();
        BuildDecor();
        DressFurniture();
        BuildFan();
        BuildLighting();
        Texturize();
        BuildAtmosphere();
        BuildPlayer();

        var story = gameObject.AddComponent<PriyaRoomStory>();
        story.builder = this;
    }

    // ------------------------------------------------------------- primitives
    GameObject Box(string name, Vector3 pos, Vector3 size, Color c, Transform parent = null,
                   float smooth = 0.25f, float metal = 0f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent != null ? parent : root, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = Mat(c, smooth, metal);
        return go;
    }

    GameObject Cyl(string name, Vector3 pos, float radius, float height, Color c, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent != null ? parent : root, false);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);
        go.GetComponent<Renderer>().sharedMaterial = Mat(c, 0.35f, 0f);
        return go;
    }

    GameObject Sphere(string name, Vector3 pos, float d, Color c, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent != null ? parent : root, false);
        go.transform.localPosition = pos;
        go.transform.localScale = Vector3.one * d;
        go.GetComponent<Renderer>().sharedMaterial = Mat(c, 0.3f, 0f);
        return go;
    }

    static Material Mat(Color c, float smooth, float metal)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var m = new Material(shader);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
        return m;
    }

    static void Strip(GameObject go)
    {
        // decorative pieces shouldn't block the player
        var col = go.GetComponent<Collider>();
        if (col) Destroy(col);
    }

    /// <summary>Mark an object as a clue: it will glow gold in detective vision.</summary>
    void MakeClue(GameObject go, string clueName)
    {
        go.name = "CLUE_" + clueName;
        var tag = go.AddComponent<ClueTag>();
        tag.id = clueName;
        ClueTags.Add(tag);
        AddClueHitbox(go);
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var m = r.material; // instance
            m.EnableKeyword("_EMISSION");
            clueRenderers.Add(r);
            rendererTags.Add(tag);
            clueBaseEmission.Add(Color.black);
        }
    }

    /// <summary>
    /// An invisible trigger box around the whole clue (slightly padded, never smaller than ~18 cm) so the crosshair
    /// can hit it exactly. Triggers never block the player or other physics.
    /// </summary>
    void AddClueHitbox(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        Bounds wb = rs[0].bounds;
        foreach (var r in rs) wb.Encapsulate(r.bounds);
        var t = go.transform;
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), max = -min;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = wb.center + Vector3.Scale(wb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 l = t.InverseTransformPoint(corner);
            min = Vector3.Min(min, l); max = Vector3.Max(max, l);
        }
        Vector3 ls = t.lossyScale;
        Vector3 inv = new Vector3(1f / Mathf.Max(0.0001f, Mathf.Abs(ls.x)), 1f / Mathf.Max(0.0001f, Mathf.Abs(ls.y)), 1f / Mathf.Max(0.0001f, Mathf.Abs(ls.z)));
        Vector3 size = (max - min) + Vector3.Scale(new Vector3(0.1f, 0.1f, 0.1f), inv);          // +5 cm each side
        size = Vector3.Max(size, Vector3.Scale(new Vector3(0.18f, 0.18f, 0.18f), inv));          // never tiny
        var col = go.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = (min + max) / 2f;
        col.size = size;
    }

    // ------------------------------------------------------------------ SHELL
    void BuildShell()
    {
        var floorMat = Box("Floor", new Vector3(0, -0.05f, 0), new Vector3(HalfW * 2 + 0.3f, 0.1f, HalfD * 2 + 0.3f), Wood, null, 0.5f);
        // floorboard lines
        for (float x = -HalfW + 0.4f; x < HalfW; x += 0.4f)
        {
            var line = Box("Board", new Vector3(x, 0.002f, 0), new Vector3(0.01f, 0.004f, HalfD * 2), DarkWood);
            Strip(line);
        }
        // rug
        var rug = Box("Rug", new Vector3(0.6f, 0.01f, 0.2f), new Vector3(2.4f, 0.02f, 1.6f), Maroon, null, 0.1f);
        Strip(rug);
        var rugTrim = Box("RugTrim", new Vector3(0.6f, 0.012f, 0.2f), new Vector3(2.1f, 0.02f, 1.3f), new Color(0.75f, 0.58f, 0.2f), null, 0.1f);
        Strip(rugTrim);
        var rugIn = Box("RugIn", new Vector3(0.6f, 0.014f, 0.2f), new Vector3(1.9f, 0.02f, 1.1f), Maroon, null, 0.1f);
        Strip(rugIn);

        Box("Ceiling", new Vector3(0, Height + 0.05f, 0), new Vector3(HalfW * 2 + 0.3f, 0.1f, HalfD * 2 + 0.3f), Cream, null, 0.05f);

        // walls (back wall z=+HalfD has the window, front wall z=-HalfD has the door)
        Box("Wall_Left", new Vector3(-HalfW - Wall / 2, Height / 2, 0), new Vector3(Wall, Height, HalfD * 2 + Wall * 2), WallYellow, null, 0.05f);
        Box("Wall_Right", new Vector3(HalfW + Wall / 2, Height / 2, 0), new Vector3(Wall, Height, HalfD * 2 + Wall * 2), WallYellow, null, 0.05f);

        // front wall (door gap at x -0.5..0.5 -> centred at x=-2.0 actually: door at x=-2.0, width 1)
        float doorX = -2.0f, doorW = 1.0f, doorH = 2.1f;
        float leftEdge = doorX - doorW / 2 + HalfW; // width of left piece
        Box("Wall_Front_A", new Vector3(-HalfW + leftEdge / 2, Height / 2, -HalfD - Wall / 2), new Vector3(leftEdge, Height, Wall), WallYellow, null, 0.05f);
        float rightStart = doorX + doorW / 2;
        float rightW = HalfW - rightStart;
        Box("Wall_Front_B", new Vector3(rightStart + rightW / 2, Height / 2, -HalfD - Wall / 2), new Vector3(rightW, Height, Wall), WallYellow, null, 0.05f);
        Box("Wall_Front_Top", new Vector3(doorX, doorH + (Height - doorH) / 2, -HalfD - Wall / 2), new Vector3(doorW, Height - doorH, Wall), WallYellow, null, 0.05f);

        // back wall with window opening (x 0.2..2.2, y 0.9..2.3)
        float wx0 = 0.2f, wx1 = 2.2f, wy0 = 0.9f, wy1 = 2.3f;
        float bz = HalfD + Wall / 2;
        float lw = wx0 + HalfW;
        Box("Wall_Back_A", new Vector3(-HalfW + lw / 2, Height / 2, bz), new Vector3(lw, Height, Wall), WallYellow, null, 0.05f);
        float rw = HalfW - wx1;
        Box("Wall_Back_B", new Vector3(wx1 + rw / 2, Height / 2, bz), new Vector3(rw, Height, Wall), WallYellow, null, 0.05f);
        Box("Wall_Back_Bottom", new Vector3((wx0 + wx1) / 2, wy0 / 2, bz), new Vector3(wx1 - wx0, wy0, Wall), WallYellow, null, 0.05f);
        Box("Wall_Back_Top", new Vector3((wx0 + wx1) / 2, wy1 + (Height - wy1) / 2, bz), new Vector3(wx1 - wx0, Height - wy1, Wall), WallYellow, null, 0.05f);

        // skirting board (fading paint / wear)
        Strip(Box("Skirt_L", new Vector3(-HalfW + 0.02f, 0.06f, 0), new Vector3(0.04f, 0.12f, HalfD * 2), DarkWood));
        Strip(Box("Skirt_R", new Vector3(HalfW - 0.02f, 0.06f, 0), new Vector3(0.04f, 0.12f, HalfD * 2), DarkWood));
        Strip(Box("Skirt_B", new Vector3(0, 0.06f, HalfD - 0.02f), new Vector3(HalfW * 2, 0.12f, 0.04f), DarkWood));
        Strip(Box("Skirt_F", new Vector3(0, 0.06f, -HalfD + 0.02f), new Vector3(HalfW * 2, 0.12f, 0.04f), DarkWood));

        // a darker "damp" stain in the corner for a crumbling-haveli feel
        var stain = Box("DampStain", new Vector3(-HalfW + 0.005f, 2.6f, 1.6f), new Vector3(0.01f, 0.8f, 1.1f), new Color(0.62f, 0.5f, 0.28f), null, 0f);
        Strip(stain);
    }

    // ----------------------------------------------------------------- WINDOW
    void BuildWindow()
    {
        float wx0 = 0.2f, wx1 = 2.2f, wy0 = 0.9f, wy1 = 2.3f;
        float cx = (wx0 + wx1) / 2, cy = (wy0 + wy1) / 2, z = HalfD;
        var frameC = new Color(0.30f, 0.17f, 0.09f);
        // the head fits between the jambs: it overlapped them with coplanar faces, each textured on its own, and the
        // top corners shimmered when the camera moved
        Strip(Box("WinFrame_T", new Vector3(cx, wy1, z - 0.02f), new Vector3(wx1 - wx0 - 0.08f, 0.08f, 0.14f), frameC));
        Strip(Box("WinFrame_B", new Vector3(cx, wy0, z - 0.06f), new Vector3(wx1 - wx0 + 0.2f, 0.06f, 0.26f), frameC)); // sill
        Strip(Box("WinFrame_L", new Vector3(wx0, cy, z - 0.02f), new Vector3(0.08f, wy1 - wy0, 0.14f), frameC));
        Strip(Box("WinFrame_R", new Vector3(wx1, cy, z - 0.02f), new Vector3(0.08f, wy1 - wy0, 0.14f), frameC));
        // iron bars
        for (float x = wx0 + 0.25f; x < wx1; x += 0.25f)
            Strip(Box("Bar", new Vector3(x, cy, z + 0.02f), new Vector3(0.02f, wy1 - wy0, 0.02f), new Color(0.1f, 0.1f, 0.1f)));

        // the view: the same Chandnipur, at three depths (BuildWindowView)
        BuildWindowView(cx, cy, z);

        // curtains
        var curtainC = new Color(0.72f, 0.35f, 0.18f);
        Strip(Box("Curtain_L", new Vector3(wx0 + 0.12f, cy + 0.1f, z - 0.16f), new Vector3(0.28f, 1.7f, 0.05f), curtainC, null, 0.05f));
        Strip(Box("Curtain_R", new Vector3(wx1 - 0.12f, cy + 0.1f, z - 0.16f), new Vector3(0.28f, 1.7f, 0.05f), curtainC, null, 0.05f));
        var rod = Cyl("CurtainRod", new Vector3(cx, wy1 + 0.12f, z - 0.16f), 0.015f, wx1 - wx0 + 0.3f, new Color(0.15f, 0.1f, 0.05f));
        rod.transform.localRotation = Quaternion.Euler(0, 0, 90);
        Strip(rod);

        // a wooden mullion divides the casement: the broken latch hangs from it (it used to float in the open window)
        Strip(Box("WinFrame_Mullion", new Vector3(cx + 0.645f, (wy0 + 0.03f + wy1 - 0.04f) * 0.5f, z - 0.08f), new Vector3(0.05f, wy1 - 0.04f - (wy0 + 0.03f), 0.06f), frameC));   // sill top to head
        Strip(Box("LatchKeeper", new Vector3(cx + 0.614f, cy - 0.215f, z - 0.112f), new Vector3(0.012f, 0.07f, 0.03f), new Color(0.32f, 0.3f, 0.28f), null, 0.4f, 0.8f));
        // CLUE 9 - the broken latch
        var latch = Box("Latch", new Vector3(cx + 0.55f, cy - 0.2f, z - 0.1f), new Vector3(0.14f, 0.03f, 0.05f), new Color(0.45f, 0.4f, 0.35f), null, 0.5f, 0.8f);
        latch.transform.localRotation = Quaternion.Euler(0, 0, -25);
        Strip(latch);
        MakeClue(latch, "9_BrokenWindowLatch");
        // rust flakes
        Strip(Box("Rust", new Vector3(cx + 0.6f, cy - 0.24f, z - 0.09f), new Vector3(0.05f, 0.02f, 0.02f), new Color(0.55f, 0.22f, 0.08f)));
    }

    // ------------------------------------------------------------- THE VIEWS
    static Material Backdrop(Texture2D tex, Color tint)
    {
        var sh = Shader.Find("Pinnacle/Backdrop");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        var m = new Material(sh) { name = "Backdrop" };
        m.mainTexture = tex;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        m.color = tint;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
        return m;
    }

    GameObject Flat(string name, Vector3 pos, Vector3 size, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(root, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        Destroy(go.GetComponent<Collider>());
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        return go;
    }

    static Material dusty;
    /// <summary>Old glass with a little dust on it: transparent, faintly warm, its highlights kept.</summary>
    static Material DustyGlass()
    {
        if (dusty != null) return dusty;
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "DustyGlass" };
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.SetFloat("_BlendModePreserveSpecular", 1f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        var c = new Color(0.74f, 0.66f, 0.56f, 0.2f);
        m.color = c; m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.82f);
        dusty = m;
        return m;
    }

    Renderer StairWindowPaneRenderer() { var t = root.Find("StairWindowPane"); return t != null ? t.GetComponent<Renderer>() : null; }

    /// <summary>
    /// The sky for a window: a vertical gradient with its brightest band at the viewer's horizon (eye height) and, when
    /// <paramref name="sunUV"/> is inside it, a soft glow where the sun is going down. Painted, so the room's fog
    /// (meant for 5 m) does not turn a sky 150 m away into brown.
    /// </summary>
    static Texture2D SkyTexture(float horizonV, Vector2 sunUV, Color top, Color mid, Color low, Color sun)
    {
        const int W = 256, H = 256;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "WindowSky" };
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
        {
            float v = (y + 0.5f) / H;
            float above = (v - horizonV) / (1f - horizonV);
            Color c = v < horizonV ? Color.Lerp(low * 0.85f, low, v / horizonV)
                    : above < 0.18f ? Color.Lerp(low, mid, above / 0.18f) : Color.Lerp(mid, top, Mathf.Pow((above - 0.18f) / 0.82f, 0.8f));
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W;
                Vector2 d = new Vector2((u - sunUV.x) * 2f, (v - sunUV.y) * 1f);
                float g = Mathf.Exp(-d.sqrMagnitude * 140f) * 1.0f + Mathf.Exp(-d.sqrMagnitude * 12f) * 0.35f;
                px[y * W + x] = c + sun * g;
            }
        }
        tex.SetPixels(px); tex.Apply();
        return tex;
    }

    static Texture2D flatWhite;
    static Texture2D White() { if (flatWhite == null) { flatWhite = new Texture2D(1, 1); flatWhite.SetPixel(0, 0, Color.white); flatWhite.Apply(); } return flatWhite; }

    /// <summary>
    /// What Priya's window looks onto, at the scale of a third floor: across an 8 m lane the neighbours' row (its roofs
    /// just under the eye, with water tanks, a stair head, a lit window and a bulb over a terrace door), old-quarter roofs
    /// 30-60 m out, then the far city and the Renewal Project's towers in the haze, and the sunset sky. The sky's glow sits
    /// where the room's sunlight comes from (12 degrees up, 10 degrees right of the window's axis), its horizon at eye
    /// height. Real depth, so the near roofline slides across the skyline when Arjun moves.
    /// </summary>
    void BuildWindowView(float cx, float cy, float z)
    {
        const float eye = 1.6f, ground = -9.3f;                   // three floors up
        // ---- sky, 150 m out, 600 x 300 m
        float skyZ = z + 150f, y0 = -60f, y1 = 240f, x0 = cx - 300f;
        Vector3 toSun = new Vector3(0.17f, 0.21f, 0.96f);
        float t = (skyZ - z) / toSun.z;
        Vector2 sunAt = new Vector2(cx + toSun.x * t, eye + toSun.y * t);
        var sky = SkyTexture((eye - y0) / (y1 - y0), new Vector2((sunAt.x - x0) / 600f, (sunAt.y - y0) / (y1 - y0)),
                             new Color(0.2f, 0.17f, 0.34f), new Color(0.88f, 0.44f, 0.26f), new Color(1f, 0.7f, 0.38f), new Color(1f, 0.82f, 0.5f));
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "View_Sky";
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(root, false);
        q.transform.localPosition = new Vector3(cx, (y0 + y1) * 0.5f, skyZ);
        q.transform.localScale = new Vector3(600f, y1 - y0, 1f);
        var qr = q.GetComponent<Renderer>(); qr.sharedMaterial = Backdrop(sky, Color.white); qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // ---- the far city and the towers: painted in haze (lighter and warmer with distance), in front of the sky
        var rnd = new System.Random(11);
        var farHaze = Backdrop(White(), new Color(0.6f, 0.36f, 0.27f));
        var midHaze = Backdrop(White(), new Color(0.42f, 0.24f, 0.2f));
        for (int i = 0; i < 46; i++)
        {
            float w = 6f + (float)rnd.NextDouble() * 16f, h = 8f + (float)rnd.NextDouble() * 22f;
            float x = cx - 160f + i * 7.2f + (float)rnd.NextDouble() * 3f;
            Flat("View_FarCity", new Vector3(x, ground + h * 0.5f, z + 125f + (float)rnd.NextDouble() * 10f), new Vector3(w, h, 1f), farHaze);
        }
        // a temple's shikhara on the far skyline, left of the sun
        Flat("View_Shikhara", new Vector3(cx - 34f, ground + 24f, z + 118f), new Vector3(6f, 24f, 1f), midHaze);
        Flat("View_ShikharaTop", new Vector3(cx - 34f, ground + 38f, z + 118f), new Vector3(2.4f, 4f, 1f), midHaze);
        // the Renewal Project: four glass towers going up out of the old city, a few floors lit
        var towerC = Backdrop(White(), new Color(0.46f, 0.38f, 0.4f));
        var towerLit = Backdrop(White(), new Color(1f, 0.82f, 0.55f));
        for (int i = 0; i < 4; i++)
        {
            // distant: 8-15 degrees above the horizon from the room (at 52-79 m tall they towered over the window)
            float tx = cx + 34f + i * 7f, th = 26f + i * 4f;
            Flat("View_Tower", new Vector3(tx, ground + th * 0.5f, z + 110f), new Vector3(4.2f, th, 1f), towerC);
            for (int f = 0; f < 6; f++)
                if (rnd.NextDouble() < 0.45) Flat("View_TowerLight", new Vector3(tx + ((float)rnd.NextDouble() - 0.5f) * 2.2f, ground + 6f + f * (th - 9f) / 6f, z + 109.4f), new Vector3(0.9f, 0.5f, 0.2f), towerLit);
            // a crane over the newest one
            if (i == 3) { Flat("View_Crane", new Vector3(tx, ground + th + 4f, z + 109.6f), new Vector3(0.4f, 8f, 0.2f), midHaze); Flat("View_CraneJib", new Vector3(tx - 4f, ground + th + 7.6f, z + 109.6f), new Vector3(11f, 0.35f, 0.2f), midHaze); }
        }

        // ---- old-quarter roofs 30-60 m out: lit like the scene (the sun is behind them, so they read dark against the sky)
        for (int i = 0; i < 26; i++)
        {
            float w = 5f + (float)rnd.NextDouble() * 7f, h = 10f + (float)rnd.NextDouble() * 9f;
            float x = cx - 60f + i * 4.9f;
            float zz = z + 30f + (float)rnd.NextDouble() * 28f;
            var b = Box("View_Roofs", new Vector3(x, ground + h * 0.5f, zz), new Vector3(w, h, 6f), new Color(0.3f, 0.17f, 0.13f), null, 0.1f);
            Strip(b); b.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (rnd.NextDouble() > 0.5) { var tk = Cyl("View_Tank", new Vector3(x + w * 0.2f, ground + h + 0.6f, zz), 0.6f, 1.2f, new Color(0.08f, 0.07f, 0.07f)); Strip(tk); tk.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            if (rnd.NextDouble() > 0.7) { var sh = Box("View_StairHead", new Vector3(x - w * 0.25f, ground + h + 1.2f, zz), new Vector3(2.2f, 2.4f, 2.4f), new Color(0.28f, 0.16f, 0.12f), null, 0.1f); Strip(sh); }
            if (rnd.NextDouble() > 0.78) { var lw = Box("View_RoofWindow", new Vector3(x, ground + h - 1.6f, zz - 3.01f), new Vector3(0.9f, 1.1f, 0.02f), Color.black, null); Strip(lw); lw.GetComponent<Renderer>().sharedMaterial = GameKit.Glow(new Color(0.3f, 0.2f, 0.1f), new Color(1f, 0.7f, 0.4f), 0.9f); }
        }

        // ---- the neighbours' row across the lane, 8 m out: three houses of different heights, fronts facing the window
        float nz = z + 8f;
        BuildNeighbour(new Vector3(cx - 6.5f, ground, nz), 7f, 4, new Color(0.72f, 0.6f, 0.44f), true, false);
        BuildNeighbour(new Vector3(cx + 1.5f, ground, nz + 0.6f), 8f, 3, new Color(0.62f, 0.64f, 0.6f), false, true);
        BuildNeighbour(new Vector3(cx + 9f, ground, nz - 0.4f), 6f, 3, new Color(0.76f, 0.56f, 0.5f), false, false);
    }

    /// <summary>One house of the row opposite: front facing the room (-z), storeys of 3.1 m, a parapet, and on the roof a
    /// water tank and a stair head with a bulb over its door (the practical light); one top-floor window lit, curtained.</summary>
    void BuildNeighbour(Vector3 baseCentre, float width, int floors, Color plaster, bool tall, bool litTop)
    {
        float h = floors * 3.1f;
        var front = Box("NeighbourFront", baseCentre + new Vector3(0f, h * 0.5f, 3f), new Vector3(width, h, 6f), plaster, null, 0.1f);
        Strip(front);
        GameKit.Surface(front, "facade", 2.4f, 1f, 1f, plaster);
        var par = Box("NeighbourParapet", baseCentre + new Vector3(0f, h + 0.45f, 0.1f), new Vector3(width, 0.9f, 0.2f), plaster * 0.95f, null, 0.1f);
        Strip(par); GameKit.Surface(par, "facade", 2.4f, 1f, 1f, plaster * 0.95f);
        Strip(Box("NeighbourCoping", baseCentre + new Vector3(0f, h + 0.93f, 0.1f), new Vector3(width + 0.06f, 0.06f, 0.3f), new Color(0.5f, 0.48f, 0.45f), null, 0.2f));
        // windows on the floors that can be seen from up here: recessed, sills and sunshades
        for (int f = floors - 2; f < floors; f++)
        {
            if (f < 0) continue;
            float wy = baseCentre.y + f * 3.1f + 1.5f;
            int n = Mathf.Max(2, Mathf.RoundToInt(width / 2.6f));
            for (int i = 0; i < n; i++)
            {
                float wx = baseCentre.x - width * 0.5f + (i + 0.5f) * width / n;
                bool lit = litTop && f == floors - 1 && i == n / 2;
                var rec = Box("NeighbourWindow", new Vector3(wx, wy, baseCentre.z - 0.01f), new Vector3(1.1f, 1.3f, 0.06f), new Color(0.04f, 0.035f, 0.035f), null, 0.6f);
                Strip(rec);
                if (lit)
                {
                    rec.GetComponent<Renderer>().sharedMaterial = GameKit.Glow(new Color(0.4f, 0.25f, 0.12f), new Color(1f, 0.72f, 0.42f), 1.1f);
                    Strip(Box("NeighbourCurtain", new Vector3(wx - 0.22f, wy, baseCentre.z - 0.05f), new Vector3(0.6f, 1.25f, 0.02f), new Color(0.55f, 0.16f, 0.1f), null, 0.1f));
                }
                Strip(Box("NeighbourSill", new Vector3(wx, wy - 0.7f, baseCentre.z - 0.07f), new Vector3(1.3f, 0.06f, 0.16f), new Color(0.55f, 0.53f, 0.5f), null, 0.2f));
                Strip(Box("NeighbourChajja", new Vector3(wx, wy + 0.85f, baseCentre.z - 0.22f), new Vector3(1.5f, 0.07f, 0.46f), new Color(0.52f, 0.5f, 0.47f), null, 0.2f));
            }
        }
        // the roof: a water tank, and on the taller house a stair head with a bulb over its door
        var tank = Cyl("NeighbourTank", baseCentre + new Vector3(width * 0.28f, h + 0.7f, 3.4f), 0.65f, 1.4f, new Color(0.06f, 0.06f, 0.07f));
        Strip(tank);
        if (tall)
        {
            var head = Box("NeighbourStairHead", baseCentre + new Vector3(-width * 0.2f, h + 1.25f, 3.2f), new Vector3(2.4f, 2.5f, 2.6f), plaster * 0.92f, null, 0.1f);
            Strip(head); GameKit.Surface(head, "facade", 2.4f, 1f, 1f, plaster * 0.92f);
            Strip(Box("NeighbourStairDoor", baseCentre + new Vector3(-width * 0.2f, h + 1.0f, 1.89f), new Vector3(0.9f, 2.0f, 0.04f), new Color(0.22f, 0.14f, 0.08f), null, 0.3f));
            Strip(GameKit.Sphere(root, "NeighbourBulb", baseCentre + new Vector3(-width * 0.2f, h + 2.2f, 1.82f), 0.08f, GameKit.Glow(new Color(1f, 0.85f, 0.6f), new Color(1f, 0.76f, 0.46f), 4f)));
            var l = new GameObject("NeighbourBulbLight").AddComponent<Light>();
            l.transform.SetParent(root, false);
            l.transform.localPosition = baseCentre + new Vector3(-width * 0.2f, h + 2.0f, 1.4f);
            l.type = LightType.Point; l.range = 4.5f; l.intensity = 1.4f; l.color = new Color(1f, 0.74f, 0.46f); l.shadows = LightShadows.None;
            // a washing line across the terrace
            Strip(Box("NeighbourLine", baseCentre + new Vector3(width * 0.05f, h + 1.7f, 2.2f), new Vector3(width * 0.6f, 0.01f, 0.01f), new Color(0.2f, 0.2f, 0.2f), null));
            for (int k = 0; k < 4; k++)
                Strip(Box("NeighbourWashing", baseCentre + new Vector3(width * (-0.18f + k * 0.12f), h + 1.35f, 2.2f), new Vector3(0.5f, 0.7f, 0.01f), k % 2 == 0 ? new Color(0.75f, 0.7f, 0.6f) : new Color(0.3f, 0.4f, 0.6f), null, 0.05f));
        }
    }

    /// <summary>
    /// Through the stair window: a lower neighbour 3.5 m away (its parapet under the window's sight line, one window lit,
    /// a drain pipe), and the dusk sky over it. Without it the window was a self-lit orange board on a solid wall.
    /// </summary>
    void BuildStairView(float outsideX)
    {
        float nx = outsideX - 3.4f;                                // the neighbour's wall, facing the window (+x)
        var wall = Box("StairViewWall", new Vector3(nx - 1f, -2.6f, -4.6f), new Vector3(2f, 8.8f, 16f), new Color(0.6f, 0.52f, 0.42f), null, 0.1f);
        Strip(wall); GameKit.Surface(wall, "facade", 2.2f, 1f, 1f, new Color(0.6f, 0.52f, 0.42f));
        Strip(Box("StairViewParapet", new Vector3(nx + 0.05f, 2.05f, -4.6f), new Vector3(0.2f, 0.5f, 16f), new Color(0.56f, 0.48f, 0.4f), null, 0.1f));
        var win = Box("StairViewWindow", new Vector3(nx + 0.02f, 0.6f, -3.9f), new Vector3(0.05f, 1.2f, 1.0f), Color.black, null);
        Strip(win); win.GetComponent<Renderer>().sharedMaterial = GameKit.Glow(new Color(0.35f, 0.22f, 0.1f), new Color(1f, 0.7f, 0.4f), 0.8f);
        Strip(Box("StairViewSill", new Vector3(nx + 0.08f, -0.03f, -3.9f), new Vector3(0.16f, 0.05f, 1.2f), new Color(0.5f, 0.48f, 0.45f), null, 0.2f));
        Strip(Cyl("StairViewDrainPipe", new Vector3(nx + 0.1f, -1f, -5.6f), 0.05f, 6f, new Color(0.2f, 0.2f, 0.22f)));
        var sky = SkyTexture(0.18f, new Vector2(-1f, -1f), new Color(0.16f, 0.15f, 0.3f), new Color(0.78f, 0.4f, 0.26f), new Color(0.95f, 0.62f, 0.34f), Color.clear);
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "StairView_Sky";
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(root, false);
        q.transform.localPosition = new Vector3(outsideX - 40f, 10f, -4.6f);
        q.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);           // faces +x, into the window
        q.transform.localScale = new Vector3(120f, 60f, 1f);
        var qr = q.GetComponent<Renderer>(); qr.sharedMaterial = Backdrop(sky, Color.white); qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    Material SkyGradientMat()
    {
        const int h = 64;
        var tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var top = new Color(0.28f, 0.22f, 0.45f);
        var mid = new Color(0.95f, 0.45f, 0.25f);
        var low = new Color(1f, 0.78f, 0.35f);
        for (int y = 0; y < h; y++)
        {
            float t = y / (h - 1f);
            Color c = t < 0.5f ? Color.Lerp(low, mid, t * 2f) : Color.Lerp(mid, top, (t - 0.5f) * 2f);
            tex.SetPixel(0, y, c);
        }
        tex.Apply();
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Texture");
        var m = new Material(shader);
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        m.mainTexture = tex;
        return m;
    }

    // ------------------------------------------------------------------- DOOR
    void BuildDoor()
    {
        float doorX = -2.0f;
        var frameC = new Color(0.28f, 0.16f, 0.09f);
        Strip(Box("DoorFrame_L", new Vector3(doorX - 0.52f, 1.05f, -HalfD), new Vector3(0.06f, 2.1f, 0.2f), frameC));
        Strip(Box("DoorFrame_R", new Vector3(doorX + 0.52f, 1.05f, -HalfD), new Vector3(0.06f, 2.1f, 0.2f), frameC));
        Strip(Box("DoorFrame_T", new Vector3(doorX, 2.13f, -HalfD), new Vector3(1.1f, 0.06f, 0.2f), frameC));
        // the door itself, slightly ajar (pivoting on the left hinge)
        var hinge = new GameObject("DoorHinge").transform;
        hinge.SetParent(root, false);
        hinge.localPosition = new Vector3(doorX - 0.48f, 0, -HalfD + 0.02f);
        hinge.localRotation = Quaternion.Euler(0, -100f, 0); // swung open into the room
        DoorHinge = hinge;
        var door = Box("Door", new Vector3(0.48f, 1.03f, 0), new Vector3(0.96f, 2.06f, 0.05f), new Color(0.40f, 0.24f, 0.13f), hinge, 0.3f);
        Strip(Box("DoorPanel1", new Vector3(0.48f, 1.5f, 0.03f), new Vector3(0.7f, 0.7f, 0.02f), new Color(0.33f, 0.19f, 0.1f), hinge));
        Strip(Box("DoorPanel2", new Vector3(0.48f, 0.6f, 0.03f), new Vector3(0.7f, 0.7f, 0.02f), new Color(0.33f, 0.19f, 0.1f), hinge));
        Strip(Sphere("Knob", new Vector3(0.85f, 1.0f, 0.06f), 0.07f, new Color(0.8f, 0.65f, 0.25f), hinge));
        BuildLanding(doorX);

        // CLUE (Act I hook): the folded note is placed on the hallway floor by the door
        var note = Box("HookNote", new Vector3(doorX + 0.12f, 0.012f, -HalfD - 0.22f), new Vector3(0.22f, 0.006f, 0.16f), Color.white, null, 0f);
        note.transform.localRotation = Quaternion.Euler(0, 20, 0);
        Strip(note);
        MakeClue(note, "Hook_FoldedNote");
        var beacon = new GameObject("NoteBeacon").AddComponent<Light>();
        beacon.transform.SetParent(note.transform, false);
        beacon.transform.localPosition = new Vector3(0, 6f, 0);
        beacon.type = LightType.Point; beacon.range = 1.6f; beacon.intensity = 1.6f; beacon.color = new Color(1f, 0.85f, 0.5f);
        beacon.shadows = LightShadows.None;
        note.SetActive(false);
        HookNote = note;
        // switch plate
        Strip(Box("Switch", new Vector3(doorX + 0.8f, 1.2f, -HalfD + 0.09f), new Vector3(0.08f, 0.12f, 0.02f), Cream));
    }


    // ================================================================ LANDING
    // The third-floor landing of the housing society: four flats (301-304), only Priya's door is open.
    const float LZ0 = -HalfD - Wall;      // landing face of Priya's front wall
    const float LZ1 = -6.6f;              // back wall
    const float LXL = -6f, LXR = 6f;      // landing ends
    const float LStair = -4.2f;           // stairwell edge (floor ends here)

    void BuildLanding(float doorX)
    {
        float zc = (LZ0 + LZ1) / 2f, depth = LZ0 - LZ1;
        var paint = new Color(0.88f, 0.76f, 0.46f);             // fading yellow, as in the story
        var dado = new Color(0.16f, 0.36f, 0.30f);

        // shell
        Box("HallFloor", new Vector3((LStair + LXR) / 2f, -0.05f, zc), new Vector3(LXR - LStair + 0.4f, 0.1f, depth + 0.3f), new Color(0.82f, 0.78f, 0.7f), null, 0.5f);
        Box("Hall_Ceiling", new Vector3(0, 3.05f, zc), new Vector3(12.6f, 0.1f, depth + 0.3f), Cream, null, 0.05f);
        Box("Hall_WallBack", new Vector3(0, 1.5f, LZ1 - 0.15f), new Vector3(12.6f, 3f, 0.3f), paint, null, 0.05f);
        // the end wall has the stair window cut through it (z -5.05..-4.15, y 1.35..2.45): four pieces round the opening
        {
            float wz0 = -5.05f, wz1 = -4.15f, wy0 = 1.35f, wy1 = 2.45f, z0 = zc - (depth + 0.3f) * 0.5f, z1 = zc + (depth + 0.3f) * 0.5f;
            Box("Hall_WallLeft", new Vector3(LXL - 0.15f, wy0 * 0.5f, zc), new Vector3(0.3f, wy0, depth + 0.3f), paint, null, 0.05f);
            Box("Hall_WallLeftTop", new Vector3(LXL - 0.15f, (wy1 + 3f) * 0.5f, zc), new Vector3(0.3f, 3f - wy1, depth + 0.3f), paint, null, 0.05f);
            Box("Hall_WallLeftA", new Vector3(LXL - 0.15f, (wy0 + wy1) * 0.5f, (z0 + wz0) * 0.5f), new Vector3(0.3f, wy1 - wy0, wz0 - z0), paint, null, 0.05f);
            Box("Hall_WallLeftB", new Vector3(LXL - 0.15f, (wy0 + wy1) * 0.5f, (wz1 + z1) * 0.5f), new Vector3(0.3f, wy1 - wy0, z1 - wz1), paint, null, 0.05f);
        }
        Box("Hall_WallRight", new Vector3(LXR + 0.15f, 1.5f, zc), new Vector3(0.3f, 3f, depth + 0.3f), paint, null, 0.05f);
        float fz = -HalfD - Wall / 2f;
        Box("Hall_FrontL", new Vector3((-6.3f + -3f) / 2f, 1.5f, fz), new Vector3(3.3f, 3f, Wall), paint, null, 0.05f);
        Box("Hall_FrontR", new Vector3((3f + 6.3f) / 2f, 1.5f, fz), new Vector3(3.3f, 3f, Wall), paint, null, 0.05f);

        // dark-green dado with a cream line, the way every Indian stairwell is painted
        Dado(new Vector3(0, 0.5f, LZ1 + 0.02f), new Vector3(12f, 1.0f, 0.04f), dado);
        Dado(new Vector3(-4.26f, 0.5f, LZ0 - 0.02f), new Vector3(3.5f, 1.0f, 0.04f), dado);
        Dado(new Vector3(2.26f, 0.5f, LZ0 - 0.02f), new Vector3(7.5f, 1.0f, 0.04f), dado);
        Dado(new Vector3(LXL + 0.02f, 0.5f, zc), new Vector3(0.04f, 1.0f, depth), dado);
        Dado(new Vector3(LXR - 0.02f, 0.5f, zc), new Vector3(0.04f, 1.0f, depth), dado);

        // lighting: tube lights on the ceiling (one is dying) + the orange evening glow from the stair window
        float[] tx = { -2.2f, 1.8f, 4.8f };
        for (int i = 0; i < tx.Length; i++)
        {
            Strip(GameKit.Box(root, "HallTubeFixture", new Vector3(tx[i], 2.97f, zc), new Vector3(1.3f, 0.05f, 0.14f), GameKit.Glow(Color.white, new Color(1f, 0.97f, 0.88f), 3f)));
            var l = new GameObject("HallTube").AddComponent<Light>();
            l.transform.SetParent(root, false);
            l.transform.localPosition = new Vector3(tx[i], 2.5f, zc);
            l.type = LightType.Point; l.range = 7.5f; l.intensity = 2.6f; l.color = new Color(1f, 0.95f, 0.85f);
            if (i == 2) l.intensity *= 0.88f;   // a weak tube without a flash in the stairwell
        }

        // ---- the four flats: 301 Kapoor (open), 302 Verma, 303 Joshi, 304 Iyer (closed, iron grille gates)
        DoorDecor(doorX, true, "KAPOOR", "301", new Color(0.35f, 0.28f, 0.2f));
        FlatDoor(2.2f, true, "VERMA", "302", new Color(0.18f, 0.35f, 0.5f), true, false);
        FlatDoor(-2.3f, false, "JOSHI", "303", new Color(0.45f, 0.2f, 0.16f), true, false);
        FlatDoor(2.5f, false, "IYER", "304", new Color(0.2f, 0.4f, 0.28f), true, true);

        BuildStairwell(zc, depth);
        BuildLift();

        // ---- society fixtures on the back wall
        float nz = LZ1 + 0.03f;
        Strip(Box("NoticeBoard", new Vector3(0.1f, 1.65f, nz), new Vector3(1.3f, 0.9f, 0.04f), new Color(0.6f, 0.42f, 0.25f), null, 0.05f));
        for (int i = 0; i < 6; i++)
        {
            var paper = Box("Notice", new Vector3(-0.45f + (i % 3) * 0.45f, 1.85f - (i / 3) * 0.42f, nz + 0.03f), new Vector3(0.3f, 0.36f, 0.01f), i % 2 == 0 ? Color.white : new Color(1f, 0.95f, 0.6f), null, 0.05f);
            paper.transform.localRotation = Quaternion.Euler(0, 0, (i * 37 % 11) - 5f);
            Strip(paper);
        }
        Strip(Box("Plaque", new Vector3(0.1f, 2.55f, nz), new Vector3(2.4f, 0.32f, 0.04f), new Color(0.75f, 0.6f, 0.25f), null, 0.6f, 0.7f));
        GameKit.Text(root, "ANAND CO-OP. HOUSING SOCIETY", new Vector3(0.1f, 2.55f, nz + 0.03f), Quaternion.Euler(0, 180f, 0), 0.07f, new Color(0.12f, 0.06f, 0.02f));
        GameKit.Text(root, "SOCIETY NOTICES", new Vector3(0.1f, 2.2f, nz + 0.03f), Quaternion.Euler(0, 180f, 0), 0.06f, new Color(0.95f, 0.9f, 0.8f));

        // fire extinguisher
        Strip(Cyl("Extinguisher", new Vector3(-4.0f, 0.45f, LZ1 + 0.16f), 0.09f, 0.5f, new Color(0.75f, 0.08f, 0.06f)));
        Strip(Box("ExtinguisherHead", new Vector3(-4.0f, 0.74f, LZ1 + 0.16f), new Vector3(0.1f, 0.06f, 0.1f), new Color(0.08f, 0.08f, 0.08f)));
        Strip(Box("FireSign", new Vector3(-4.0f, 1.2f, LZ1 + 0.03f), new Vector3(0.3f, 0.3f, 0.02f), new Color(0.8f, 0.1f, 0.08f)));
        GameKit.Text(root, "FIRE", new Vector3(-4.0f, 1.2f, LZ1 + 0.05f), Quaternion.Euler(0, 180f, 0), 0.08f, Color.white);

        // electricity meter box + conduit
        Strip(Box("MeterCabinet", new Vector3(4.95f, 1.35f, LZ1 + 0.1f), new Vector3(0.6f, 0.8f, 0.16f), new Color(0.45f, 0.5f, 0.48f), null, 0.4f, 0.5f));
        for (int i = 0; i < 4; i++)
        {
            var dial = Cyl("MeterDial", new Vector3(4.78f + (i % 2) * 0.34f, 1.5f - (i / 2) * 0.32f, LZ1 + 0.2f), 0.07f, 0.02f, new Color(0.08f, 0.08f, 0.08f));
            dial.transform.localRotation = Quaternion.Euler(90, 0, 0);
            Strip(dial);
        }
        Strip(Cyl("Conduit", new Vector3(4.95f, 2.3f, LZ1 + 0.06f), 0.03f, 1.4f, new Color(0.3f, 0.3f, 0.3f)));
        var cond = Cyl("CeilingConduit", new Vector3(0, 2.88f, LZ1 + 0.1f), 0.03f, 12f, new Color(0.3f, 0.3f, 0.3f));
        cond.transform.localRotation = Quaternion.Euler(0, 0, 90);
        Strip(cond);

        // letterboxes by the stairs
        for (int i = 0; i < 4; i++)
        {
            Strip(Box("Letterbox", new Vector3(-5.6f + i * 0.28f, 1.2f, LZ1 + 0.08f), new Vector3(0.24f, 0.3f, 0.12f), new Color(0.5f, 0.5f, 0.55f), null, 0.4f, 0.6f));
            GameKit.Text(root, (301 + i).ToString(), new Vector3(-5.6f + i * 0.28f, 1.2f, LZ1 + 0.15f), Quaternion.Euler(0, 180f, 0), 0.05f, Color.black);
        }

        // a tulsi plant in a pot by flat 304
        Strip(Cyl("TulsiPot", new Vector3(3.5f, 0.2f, LZ1 + 0.35f), 0.17f, 0.4f, new Color(0.65f, 0.3f, 0.15f)));
        for (int i = 0; i < 5; i++)
            Strip(Sphere("Tulsi", new Vector3(3.5f + Mathf.Sin(i * 1.3f) * 0.1f, 0.5f + i * 0.04f, LZ1 + 0.35f + Mathf.Cos(i * 1.3f) * 0.1f), 0.2f, new Color(0.15f, 0.5f, 0.2f)));
    }

    void Dado(Vector3 c, Vector3 size, Color col)
    {
        Strip(Box("HallDado", c, size, col, null, 0.2f));
        Vector3 sc = size; sc.y = 0.04f;
        sc.x = size.x > 0.1f ? size.x : size.x + 0.01f;
        sc.z = size.z > 0.1f ? size.z : size.z + 0.01f;
        Strip(Box("HallDadoLine", new Vector3(c.x, 1.02f, c.z), sc, new Color(0.95f, 0.9f, 0.75f), null, 0.2f));
    }

    /// <summary>A closed flat door with frame, iron grille gate, peephole, nameplate, mat and shoe rack.</summary>
    void FlatDoor(float x, bool front, string family, string num, Color leaf, bool grille, bool rangoli)
    {
        float zw = front ? LZ0 : LZ1;
        float sg = front ? -1f : 1f;                    // direction into the landing
        float zf = zw + sg * 0.04f;
        var frameC = new Color(0.25f, 0.15f, 0.08f);
        Strip(Box("DoorFrame_L", new Vector3(x - 0.52f, 1.05f, zf), new Vector3(0.08f, 2.1f, 0.1f), frameC));
        Strip(Box("DoorFrame_R", new Vector3(x + 0.52f, 1.05f, zf), new Vector3(0.08f, 2.1f, 0.1f), frameC));
        Strip(Box("DoorFrame_T", new Vector3(x, 2.13f, zf), new Vector3(1.12f, 0.08f, 0.1f), frameC));
        Strip(Box("FlatDoor", new Vector3(x, 1.03f, zf + sg * 0.01f), new Vector3(0.96f, 2.06f, 0.05f), leaf, null, 0.3f));
        Strip(Box("DoorPanel", new Vector3(x, 1.5f, zf + sg * 0.04f), new Vector3(0.7f, 0.7f, 0.02f), leaf * 0.8f, null, 0.3f));
        Strip(Box("DoorPanel", new Vector3(x, 0.6f, zf + sg * 0.04f), new Vector3(0.7f, 0.7f, 0.02f), leaf * 0.8f, null, 0.3f));
        Strip(Sphere("Handle", new Vector3(x + 0.38f, 1.0f, zf + sg * 0.07f), 0.07f, new Color(0.8f, 0.65f, 0.25f)));
        var peep = Cyl("Peephole", new Vector3(x, 1.6f, zf + sg * 0.05f), 0.015f, 0.02f, new Color(0.05f, 0.05f, 0.05f));
        peep.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Strip(peep);
        if (grille)                                         // the iron safety gate in front of the wooden door
        {
            var iron = new Color(0.07f, 0.07f, 0.08f);
            for (int i = 0; i < 7; i++) Strip(Box("Grille", new Vector3(x - 0.42f + i * 0.14f, 1.05f, zf + sg * 0.13f), new Vector3(0.02f, 2.0f, 0.02f), iron, null, 0.4f, 0.6f));
            foreach (float gy in new[] { 0.3f, 1.05f, 1.8f })
                Strip(Box("GrilleBar", new Vector3(x, gy, zf + sg * 0.13f), new Vector3(0.98f, 0.03f, 0.03f), iron, null, 0.4f, 0.6f));
        }
        DoorDecor(x, front, family, num, new Color(0.6f, 0.15f + (x * 7f % 0.3f), 0.2f));
        // little saffron Om sticker above the door
        var om = Cyl("OmSticker", new Vector3(x, 2.35f, zf + sg * 0.03f), 0.07f, 0.01f, new Color(0.95f, 0.5f, 0.1f));
        om.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Strip(om);
        if (rangoli)                                        // festive: a rangoli at the door and a marigold toran
        {
            Color[] rc = { Color.white, new Color(0.85f, 0.1f, 0.15f), new Color(1f, 0.8f, 0.1f), new Color(0.1f, 0.6f, 0.3f), new Color(1f, 0.5f, 0.1f), new Color(0.9f, 0.3f, 0.6f) };
            for (int i = 0; i < rc.Length; i++)
                Strip(Cyl("Rangoli", new Vector3(x, 0.012f + i * 0.002f, zf + sg * 1.1f), 0.5f - i * 0.075f, 0.01f, rc[i], null));
            for (int i = 0; i <= 14; i++)
            {
                float t = i / 14f;
                Strip(Sphere("Toran", new Vector3(x - 0.6f + 1.2f * t, 2.28f - 0.12f * Mathf.Sin(t * Mathf.PI), zf + sg * 0.06f), 0.1f, i % 3 == 0 ? new Color(0.85f, 0.2f, 0.1f) : (i % 2 == 0 ? new Color(1f, 0.55f, 0.05f) : new Color(1f, 0.78f, 0.1f))));
            }
        }
    }

    /// <summary>Nameplate, flat number, doormat and shoe rack: the details that make a door look lived in.</summary>
    void DoorDecor(float x, bool front, string family, string num, Color matColor)
    {
        float zw = front ? LZ0 : LZ1;
        float sg = front ? -1f : 1f;
        float zf = zw + sg * 0.04f;
        Quaternion face = front ? Quaternion.identity : Quaternion.Euler(0, 180f, 0);
        // The plate is screwed flat to the plaster (it floated 2.5 cm off the wall), clear of the frame and the door's swing,
        // and its lettering sits 1 mm proud of its face in the depth-tested text material: the default font material drew
        // the name through the wall into Priya's room whenever the on-screen UI was up. Both hang from one parent, so the
        // lettering cannot drift off its plate.
        var plate = new GameObject("NameplateMount").transform;
        plate.SetParent(root, false);
        plate.localPosition = new Vector3(x + 0.86f, 1.55f, zw + sg * 0.015f);
        plate.localRotation = face;
        var board = Box("Nameplate", Vector3.zero, new Vector3(0.42f, 0.24f, 0.03f), new Color(0.93f, 0.9f, 0.8f), plate, 0.4f);
        Strip(board);
        board.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Strip(Box("NameplateScrew", new Vector3(-0.18f, 0.09f, -0.016f), new Vector3(0.012f, 0.012f, 0.004f), new Color(0.55f, 0.5f, 0.4f), plate, 0.6f, 0.8f));
        Strip(Box("NameplateScrew", new Vector3(0.18f, -0.09f, -0.016f), new Vector3(0.012f, 0.012f, 0.004f), new Color(0.55f, 0.5f, 0.4f), plate, 0.6f, 0.8f));
        GameKit.Solid(GameKit.Text(plate, family, new Vector3(0f, -0.03f, -0.016f), Quaternion.identity, 0.06f, new Color(0.1f, 0.06f, 0.04f)));
        GameKit.Solid(GameKit.Text(plate, num, new Vector3(0f, 0.07f, -0.016f), Quaternion.identity, 0.075f, new Color(0.6f, 0.1f, 0.08f)));
        Strip(Box("Doormat", new Vector3(x, 0.012f, zf + sg * 0.5f), new Vector3(0.9f, 0.02f, 0.55f), matColor, null, 0.05f));
        // shoe rack beside the door
        float rx = x - 1.0f;
        Strip(Box("ShoeRack", new Vector3(rx, 0.3f, zf + sg * 0.17f), new Vector3(0.7f, 0.04f, 0.28f), new Color(0.3f, 0.2f, 0.12f)));
        Strip(Box("ShoeRack", new Vector3(rx, 0.02f, zf + sg * 0.17f), new Vector3(0.7f, 0.04f, 0.28f), new Color(0.3f, 0.2f, 0.12f)));
        for (int i = 0; i < 5; i++)
            Strip(Box("Shoe", new Vector3(rx - 0.28f + i * 0.14f, 0.35f + (i % 2) * 0f, zf + sg * 0.17f), new Vector3(0.1f, 0.06f, 0.22f), Color.HSVToRGB((x * 3f + i * 0.17f) % 1f, 0.5f, i % 2 == 0 ? 0.35f : 0.75f), null, 0.3f));
    }

    void BuildStairwell(float zc, float depth)
    {
        var rail = new Color(0.07f, 0.07f, 0.08f);
        // railing + an invisible blocker so nobody can fall down the stairs
        Box("StairRailTop", new Vector3(LStair, 1.0f, zc), new Vector3(0.06f, 0.06f, depth), rail, null, 0.4f, 0.6f);
        for (int i = 0; i < 11; i++)
            Strip(Box("Baluster", new Vector3(LStair, 0.5f, LZ0 - 0.15f - i * (depth - 0.3f) / 10f), new Vector3(0.025f, 1.0f, 0.025f), rail, null, 0.4f, 0.6f));
        var block = new GameObject("StairBlocker").AddComponent<BoxCollider>();
        block.transform.SetParent(root, false);
        block.transform.localPosition = new Vector3(LStair - 0.05f, 1.2f, zc);
        block.size = new Vector3(0.1f, 2.4f, depth + 0.4f);

        // the flight going down (visible below the railing)
        for (int i = 0; i < 9; i++)
        {
            float x = LStair - 0.25f - i * 0.0f;
            Strip(Box("Step", new Vector3(-5.1f, -0.18f * (i + 1) - 0.05f, LZ0 - 0.3f - i * 0.4f), new Vector3(1.7f, 0.1f, 0.4f), new Color(0.55f, 0.52f, 0.48f), null, 0.4f));
            Strip(Box("Riser", new Vector3(-5.1f, -0.18f * (i + 1) + 0.04f, LZ0 - 0.1f - i * 0.4f), new Vector3(1.7f, 0.18f, 0.03f), new Color(0.45f, 0.43f, 0.4f), null, 0.4f));
        }
        Strip(Box("LowerFloor", new Vector3(-5.1f, -1.8f, zc), new Vector3(1.8f, 0.1f, depth), new Color(0.3f, 0.28f, 0.26f), null, 0.3f));
        // the window in the end wall: a real opening with a frame round its reveal, dusty glass, bars, and the dusk outside
        float wx = LXL - 0.15f;                                   // the wall's centre plane (it runs from x -6.3 to -6.0)
        var frameC = new Color(0.2f, 0.12f, 0.07f);
        foreach (float sz in new[] { -1f, 1f })
            Strip(Box("StairWindowFrameJamb", new Vector3(wx, 1.9f, -4.6f + sz * 0.47f), new Vector3(0.3f, 1.14f, 0.05f), frameC, null, 0.2f));
        Strip(Box("StairWindowFrameHead", new Vector3(wx, 2.47f, -4.6f), new Vector3(0.3f, 0.05f, 0.99f), frameC, null, 0.2f));
        Strip(Box("StairWindowFrameSill", new Vector3(wx + 0.03f, 1.325f, -4.6f), new Vector3(0.38f, 0.05f, 1.05f), frameC, null, 0.2f));
        // the old pane was a self-lit orange board on a solid wall; this is glass, so the view behind it moves with the eye
        Strip(GameKit.Box(root, "StairWindowPane", new Vector3(wx - 0.05f, 1.9f, -4.6f), new Vector3(0.006f, 1.1f, 0.9f), DustyGlass()));
        StairWindowPaneRenderer().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        for (int i = 0; i < 4; i++) Strip(Box("StairWindowBar", new Vector3(wx + 0.06f, 1.9f, -4.95f + i * 0.23f), new Vector3(0.02f, 1.1f, 0.02f), rail, null, 0.4f, 0.6f));
        BuildStairView(wx - 0.15f);
        var wl = new GameObject("StairWindowLight").AddComponent<Light>();
        wl.transform.SetParent(root, false);
        wl.transform.localPosition = new Vector3(-5.2f, 1.9f, -4.6f);
        wl.type = LightType.Point; wl.range = 6f; wl.intensity = 2.4f; wl.color = new Color(1f, 0.6f, 0.3f);
        // a handrail along the end wall
        Strip(Box("Handrail", new Vector3(LXL + 0.1f, 0.95f, zc), new Vector3(0.05f, 0.05f, depth), rail, null, 0.4f, 0.6f));
    }

    void BuildLift()
    {
        float x = LXR - 0.03f;
        var steel = new Color(0.62f, 0.64f, 0.67f);
        Strip(Box("LiftFrame", new Vector3(x, 1.15f, -4.6f), new Vector3(0.08f, 2.3f, 1.5f), new Color(0.3f, 0.3f, 0.32f), null, 0.5f, 0.6f));
        Strip(Box("LiftDoor", new Vector3(x - 0.05f, 1.05f, -4.85f), new Vector3(0.04f, 2.1f, 0.62f), steel, null, 0.7f, 0.8f));
        Strip(Box("LiftDoor", new Vector3(x - 0.05f, 1.05f, -4.35f), new Vector3(0.04f, 2.1f, 0.62f), steel, null, 0.7f, 0.8f));
        Strip(Box("LiftIndicator", new Vector3(x - 0.04f, 2.45f, -4.6f), new Vector3(0.04f, 0.22f, 0.4f), new Color(0.05f, 0.05f, 0.06f)));
        GameKit.Text(root, "3", new Vector3(x - 0.07f, 2.45f, -4.6f), Quaternion.Euler(0, 90f, 0), 0.14f, new Color(0.3f, 1f, 0.4f));
        Strip(Box("LiftPanel", new Vector3(x - 0.04f, 1.15f, -3.7f), new Vector3(0.04f, 0.3f, 0.14f), new Color(0.4f, 0.4f, 0.42f), null, 0.5f, 0.6f));
        Strip(GameKit.Box(root, "LiftButtonUp", new Vector3(x - 0.07f, 1.22f, -3.7f), new Vector3(0.03f, 0.05f, 0.05f), GameKit.Glow(new Color(0.6f, 0.1f, 0.1f), new Color(1f, 0.2f, 0.1f), 2f)));
        Strip(GameKit.Box(root, "LiftButtonDown", new Vector3(x - 0.07f, 1.1f, -3.7f), new Vector3(0.03f, 0.05f, 0.05f), GameKit.Glow(new Color(0.4f, 0.4f, 0.4f), new Color(0.6f, 0.6f, 0.6f), 0.5f)));
        // the reason Arjun takes the stairs: a hand-written OUT OF ORDER sign
        Strip(Box("LiftSign", new Vector3(x - 0.08f, 1.6f, -4.6f), new Vector3(0.01f, 0.28f, 0.42f), new Color(1f, 0.95f, 0.55f), null, 0.05f));
        GameKit.Text(root, "OUT OF ORDER", new Vector3(x - 0.09f, 1.62f, -4.6f), Quaternion.Euler(0, 90f, 0), 0.05f, new Color(0.75f, 0.05f, 0.05f));
    }

    // ------------------------------------------------------------------- DESK
    void BuildDesk()
    {
        // desk sits against the right wall
        float dx = HalfW - 0.4f, dz = 0.4f;
        var desk = new GameObject("Desk").transform;
        desk.SetParent(root, false);
        desk.localPosition = new Vector3(dx, 0, dz);
        Box("DeskTop", new Vector3(0, 0.75f, 0), new Vector3(0.8f, 0.05f, 1.6f), Wood, desk, 0.5f);
        foreach (var sx in new[] { -0.35f, 0.35f })
            foreach (var sz in new[] { -0.75f, 0.75f })
                Box("Leg", new Vector3(sx, 0.375f, sz), new Vector3(0.05f, 0.75f, 0.05f), DarkWood, desk);
        Box("DeskDrawers", new Vector3(0.08f, 0.55f, -0.5f), new Vector3(0.6f, 0.3f, 0.5f), DarkWood, desk);
        Strip(Box("DrawerHandle", new Vector3(-0.23f, 0.55f, -0.5f), new Vector3(0.02f, 0.03f, 0.15f), new Color(0.8f, 0.65f, 0.25f), desk, 0.6f, 0.8f));

        // chair (pulled out - someone just left)
        var chair = new GameObject("Chair").transform;
        chair.SetParent(root, false);
        chair.localPosition = new Vector3(dx - 0.95f, 0, dz - 0.15f);
        chair.localRotation = Quaternion.Euler(0, 105, 0);
        Box("Seat", new Vector3(0, 0.45f, 0), new Vector3(0.45f, 0.05f, 0.45f), Teal, chair);
        Box("Back", new Vector3(0, 0.75f, -0.2f), new Vector3(0.45f, 0.5f, 0.04f), Teal, chair);
        foreach (var sx in new[] { -0.2f, 0.2f })
            foreach (var sz in new[] { -0.2f, 0.2f })
                Box("ChairLeg", new Vector3(sx, 0.22f, sz), new Vector3(0.04f, 0.44f, 0.04f), DarkWood, chair);

        float top = 0.775f;
        // CLUE 1 - laptop (open, black screen)
        var laptop = new GameObject("Laptop").transform;
        laptop.SetParent(desk, false);
        laptop.localPosition = new Vector3(0.05f, top, 0.15f);
        laptop.localRotation = Quaternion.Euler(0, -90, 0);
        Box("Base", new Vector3(0, 0.01f, 0), new Vector3(0.34f, 0.02f, 0.24f), new Color(0.6f, 0.6f, 0.62f), laptop, 0.6f, 0.7f);
        var screen = Box("Screen", new Vector3(0, 0.12f, 0.12f), new Vector3(0.34f, 0.22f, 0.012f), new Color(0.03f, 0.03f, 0.04f), laptop, 0.9f);
        screen.transform.localRotation = Quaternion.Euler(-12, 0, 0);
        Strip(laptop.gameObject); // parent has no collider, harmless
        MakeClue(laptop.gameObject, "1_Laptop");
        // CLUE 2 - sticky note under keyboard edge
        var sticky = Box("Sticky", new Vector3(-0.19f, top + 0.003f, -0.3f), new Vector3(0.07f, 0.004f, 0.07f), new Color(1f, 0.95f, 0.35f), desk, 0f);
        sticky.transform.localRotation = Quaternion.Euler(0, 12, 0);
        Strip(sticky);
        MakeClue(sticky, "2_StickyNote");

        // half-drunk chai cup (with saucer) - sits at edge of desk
        var cup = new GameObject("ChaiCup").transform;
        cup.SetParent(desk, false);
        cup.localPosition = new Vector3(-0.2f, top, 0.62f);
        Cyl("Saucer", new Vector3(0, 0.005f, 0), 0.07f, 0.01f, Color.white, cup);
        Cyl("Cup", new Vector3(0, 0.045f, 0), 0.04f, 0.08f, new Color(0.95f, 0.93f, 0.88f), cup);
        Cyl("Chai", new Vector3(0, 0.07f, 0), 0.036f, 0.005f, new Color(0.55f, 0.32f, 0.14f), cup);
        Strip(cup.gameObject);

        // desk lamp
        var lamp = new GameObject("DeskLamp").transform;
        lamp.SetParent(desk, false);
        lamp.localPosition = new Vector3(0.2f, top, 0.68f);
        Cyl("LampBase", new Vector3(0, 0.01f, 0), 0.08f, 0.02f, new Color(0.15f, 0.15f, 0.15f), lamp);
        Cyl("LampArm", new Vector3(0, 0.22f, 0), 0.012f, 0.42f, new Color(0.15f, 0.15f, 0.15f), lamp);
        var shade = Box("LampShade", new Vector3(-0.08f, 0.42f, 0), new Vector3(0.22f, 0.07f, 0.16f), new Color(0.2f, 0.4f, 0.3f), lamp, 0.5f);
        shade.transform.localRotation = Quaternion.Euler(0, 0, 15);
        var ll = new GameObject("LampLight").AddComponent<Light>();
        ll.transform.SetParent(lamp, false);
        ll.transform.localPosition = new Vector3(-0.08f, 0.38f, 0);
        ll.transform.localRotation = Quaternion.Euler(90, 0, 0);
        ll.type = LightType.Spot; ll.range = 2f; ll.spotAngle = 80f; ll.intensity = 2.2f;
        ll.color = new Color(1f, 0.82f, 0.5f);
        Strip(lamp.gameObject);

        // papers & pens
        for (int i = 0; i < 3; i++)
        {
            var p = Box("Paper" + i, new Vector3(-0.1f + i * 0.02f, top + 0.002f + i * 0.002f, -0.45f), new Vector3(0.21f, 0.002f, 0.3f), Color.white, desk, 0f);
            p.transform.localRotation = Quaternion.Euler(0, i * 9 - 8, 0);
            Strip(p);
        }
        var pen = Cyl("Pen", new Vector3(-0.15f, top + 0.008f, -0.65f), 0.006f, 0.15f, new Color(0.1f, 0.2f, 0.7f), desk);
        pen.transform.localRotation = Quaternion.Euler(0, 40, 90);
        Strip(pen);

        // CLUE 6 - torn photograph in the trash bin beside desk
        var bin = new GameObject("TrashBin").transform;
        bin.SetParent(root, false);
        bin.localPosition = new Vector3(HalfW - 0.3f, 0, dz - 1.1f);
        var binBody = Cyl("BinBody", new Vector3(0, 0.15f, 0), 0.14f, 0.3f, new Color(0.25f, 0.25f, 0.28f), bin);
        var photo = Box("Photo", new Vector3(0, 0.31f, 0), new Vector3(0.09f, 0.004f, 0.06f), new Color(0.85f, 0.75f, 0.6f), bin, 0f);
        photo.transform.localRotation = Quaternion.Euler(8, 20, 5);
        Strip(photo);
        MakeClue(photo, "6_TornPhotograph");
        Strip(binBody);
        // crumpled paper balls on the floor
        for (int i = 0; i < 3; i++)
            Strip(Sphere("Crumple", new Vector3(HalfW - 0.7f - i * 0.15f, 0.04f, dz - 1.3f + i * 0.1f), 0.08f, Color.white));
    }

    // ------------------------------------------------------- WALL CONSPIRACY BOARD
    void BuildWallBoard()
    {
        // right wall, above/beside the desk
        float x = HalfW - 0.03f;
        var board = new GameObject("WallBoard").transform;
        board.SetParent(root, false);
        board.localPosition = new Vector3(x, 1.75f, 0.4f);
        Box("Cork", new Vector3(0, 0, 0), new Vector3(0.04f, 1.1f, 1.6f), new Color(0.6f, 0.42f, 0.25f), board, 0.05f);

        var rnd = new System.Random(3);
        var clip = new List<Vector3>();
        for (int i = 0; i < 7; i++)
        {
            float z = -0.65f + (i % 4) * 0.42f + ((i > 3) ? 0.15f : 0f);
            float y = (i > 3) ? -0.25f : 0.25f;
            var c = Box("Clip" + i, new Vector3(-0.03f, y, z), new Vector3(0.008f, 0.3f, 0.22f), new Color(0.9f, 0.9f, 0.85f), board, 0f);
            c.transform.localRotation = Quaternion.Euler((float)(rnd.NextDouble() * 10 - 5), 0, 0);
            Strip(c);
            // fake text lines
            for (int l = 0; l < 4; l++)
                Strip(Box("Line", new Vector3(-0.036f, y + 0.09f - l * 0.05f, z), new Vector3(0.004f, 0.012f, 0.17f), new Color(0.2f, 0.2f, 0.2f), board, 0f));
            clip.Add(new Vector3(-0.04f, y, z));
        }
        // red string between clippings
        int[][] links = { new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2, 3 }, new[] { 0, 5 }, new[] { 1, 4 }, new[] { 3, 6 }, new[] { 5, 6 }, new[] { 4, 2 } };
        foreach (var l in links)
        {
            Vector3 a = clip[l[0]], b = clip[l[1]];
            var s = Box("String", (a + b) / 2, Vector3.one, new Color(0.8f, 0.05f, 0.05f), board, 0f);
            var d = b - a;
            s.transform.localScale = new Vector3(0.004f, 0.004f, d.magnitude);
            s.transform.localRotation = Quaternion.LookRotation(d);
            Strip(s);
        }
        // big headline cutting
        var head = Box("Headline", new Vector3(-0.045f, 0.0f, 0.0f), new Vector3(0.006f, 0.24f, 0.4f), new Color(0.93f, 0.9f, 0.8f), board, 0f);
        Strip(head);
        Strip(Box("HeadlineText", new Vector3(-0.05f, 0.07f, 0.0f), new Vector3(0.004f, 0.04f, 0.34f), Color.black, board, 0f));

        Strip(board.gameObject);
        MakeClue(board.gameObject, "3_NewspaperClippings");
    }

    // -------------------------------------------------------------------- BED
    void BuildBed()
    {
        // bed in the back-left corner
        float bx = -HalfW + 1.0f, bz = HalfD - 1.1f;
        var bed = new GameObject("Bed").transform;
        bed.SetParent(root, false);
        bed.localPosition = new Vector3(bx, 0, bz);
        Box("Frame", new Vector3(0, 0.22f, 0), new Vector3(1.9f, 0.3f, 2.1f), DarkWood, bed);
        Box("Mattress", new Vector3(0, 0.44f, 0), new Vector3(1.8f, 0.18f, 2.0f), new Color(0.92f, 0.9f, 0.85f), bed, 0.05f);
        Box("Blanket", new Vector3(0, 0.55f, -0.35f), new Vector3(1.82f, 0.08f, 1.2f), Teal, bed, 0.05f);
        Box("Pillow1", new Vector3(-0.42f, 0.6f, 0.75f), new Vector3(0.6f, 0.12f, 0.38f), Color.white, bed, 0.05f);
        Box("Pillow2", new Vector3(0.42f, 0.6f, 0.75f), new Vector3(0.6f, 0.12f, 0.38f), new Color(0.95f, 0.85f, 0.7f), bed, 0.05f);
        Box("Headboard", new Vector3(0, 0.75f, 1.07f), new Vector3(1.95f, 0.85f, 0.08f), DarkWood, bed);

        // bedside table with CLUE 8 diary
        var bt = new GameObject("BedsideTable").transform;
        bt.SetParent(root, false);
        bt.localPosition = new Vector3(bx + 1.35f, 0, bz + 0.75f);
        Box("Body", new Vector3(0, 0.27f, 0), new Vector3(0.45f, 0.54f, 0.4f), Wood, bt);
        Strip(Box("Handle", new Vector3(0, 0.35f, -0.21f), new Vector3(0.1f, 0.02f, 0.02f), new Color(0.8f, 0.65f, 0.25f), bt, 0.6f, 0.8f));
        var diary = Box("Diary", new Vector3(0, 0.575f, 0), new Vector3(0.16f, 0.035f, 0.22f), new Color(0.32f, 0.16f, 0.08f), bt, 0.4f);
        diary.transform.localRotation = Quaternion.Euler(0, 15, 0);
        Strip(diary);
        var pages = Box("DiaryPages", new Vector3(0.003f, 0.575f, 0), new Vector3(0.15f, 0.025f, 0.21f), Cream, bt, 0f);
        pages.transform.localRotation = Quaternion.Euler(0, 15, 0);
        Strip(pages);
        MakeClue(diary, "8_Diary");
        // small brass alarm clock
        var clock = Cyl("Clock", new Vector3(0.14f, 0.6f, 0.08f), 0.05f, 0.08f, new Color(0.75f, 0.6f, 0.2f), bt);
        clock.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Strip(clock);
    }

    // ----------------------------------------------------------------- CLOSET
    void BuildCloset()
    {
        // wardrobe on the left wall, toward the front
        float cx = -HalfW + 0.35f, cz = -0.4f;
        var closet = new GameObject("Closet").transform;
        closet.SetParent(root, false);
        closet.localPosition = new Vector3(cx, 0, cz);
        Box("WardrobeBack", new Vector3(-0.27f, 1.1f, 0), new Vector3(0.04f, 2.2f, 1.3f), DarkWood, closet);
        Box("WardrobeL", new Vector3(0, 1.1f, -0.63f), new Vector3(0.6f, 2.2f, 0.04f), Wood, closet);
        Box("WardrobeR", new Vector3(0, 1.1f, 0.63f), new Vector3(0.6f, 2.2f, 0.04f), Wood, closet);
        Box("WardrobeTop", new Vector3(0, 2.2f, 0), new Vector3(0.6f, 0.04f, 1.3f), Wood, closet);
        Box("WardrobeBottom", new Vector3(0, 0.05f, 0), new Vector3(0.6f, 0.06f, 1.3f), Wood, closet);
        Box("Shelf", new Vector3(0, 1.5f, 0), new Vector3(0.55f, 0.03f, 1.2f), Wood, closet);
        // hanging clothes rail + clothes
        var rail = Cyl("Rail", new Vector3(0, 1.9f, 0), 0.012f, 1.2f, new Color(0.5f, 0.5f, 0.5f), closet);
        rail.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Color[] kurta = { new Color(0.8f, 0.3f, 0.2f), new Color(0.2f, 0.4f, 0.6f), new Color(0.9f, 0.75f, 0.3f), new Color(0.3f, 0.5f, 0.35f), new Color(0.85f, 0.85f, 0.8f) };
        for (int i = 0; i < kurta.Length; i++)
            Strip(Box("Kurta" + i, new Vector3(0, 1.45f, -0.45f + i * 0.22f), new Vector3(0.05f, 0.85f, 0.18f), kurta[i], closet, 0.05f));
        // open doors
        var d1 = Box("DoorOpenL", new Vector3(0.28f, 1.1f, -0.9f), new Vector3(0.6f, 2.15f, 0.04f), Wood, closet);
        d1.transform.localRotation = Quaternion.Euler(0, -65, 0);
        d1.transform.localPosition = new Vector3(0.4f, 1.1f, -0.95f);
        Strip(d1);

        // CLUE 4 - half-packed suitcase on the floor in front of the closet
        var sc = new GameObject("Suitcase").transform;
        sc.SetParent(root, false);
        sc.localPosition = new Vector3(cx + 0.85f, 0, cz + 0.1f);
        sc.localRotation = Quaternion.Euler(0, 15, 0);
        Box("Case", new Vector3(0, 0.12f, 0), new Vector3(0.6f, 0.22f, 0.4f), new Color(0.3f, 0.12f, 0.12f), sc, 0.3f);
        var lid = Box("Lid", new Vector3(0, 0.36f, 0.24f), new Vector3(0.6f, 0.04f, 0.4f), new Color(0.35f, 0.14f, 0.14f), sc, 0.3f);
        lid.transform.localRotation = Quaternion.Euler(-105, 0, 0);
        lid.transform.localPosition = new Vector3(0, 0.34f, 0.25f);
        Box("Cloth1", new Vector3(-0.1f, 0.25f, 0), new Vector3(0.3f, 0.05f, 0.34f), new Color(0.9f, 0.7f, 0.25f), sc, 0.05f);
        Box("Cloth2", new Vector3(0.12f, 0.27f, 0), new Vector3(0.25f, 0.05f, 0.3f), new Color(0.2f, 0.45f, 0.6f), sc, 0.05f);
        Strip(Box("Ticket", new Vector3(0.22f, 0.245f, 0.16f), new Vector3(0.06f, 0.004f, 0.03f), new Color(1f, 0.7f, 0.75f), sc, 0f));
        Strip(sc.gameObject);
        MakeClue(sc.gameObject, "4_HalfPackedSuitcase");
    }

    // ---------------------------------------------------------------- KITCHEN
    void BuildKitchenAlcove()
    {
        // small counter + sink along the front wall, right of the door
        float kx = 1.6f, kz = -HalfD + 0.35f;
        var k = new GameObject("KitchenAlcove").transform;
        k.SetParent(root, false);
        k.localPosition = new Vector3(kx, 0, kz);
        Box("Counter", new Vector3(0, 0.45f, 0), new Vector3(2.0f, 0.9f, 0.6f), new Color(0.55f, 0.55f, 0.5f), k, 0.2f);
        Box("CounterTop", new Vector3(0, 0.92f, 0), new Vector3(2.05f, 0.04f, 0.65f), new Color(0.75f, 0.72f, 0.65f), k, 0.5f);
        // sink basin (dark inset)
        Strip(Box("Basin", new Vector3(-0.4f, 0.935f, 0.02f), new Vector3(0.5f, 0.02f, 0.4f), new Color(0.6f, 0.62f, 0.64f), k, 0.8f, 0.7f));
        Strip(Box("BasinHole", new Vector3(-0.4f, 0.946f, 0.02f), new Vector3(0.44f, 0.005f, 0.34f), new Color(0.15f, 0.16f, 0.17f), k, 0.3f));
        // tap
        Strip(Cyl("Tap", new Vector3(-0.4f, 1.03f, 0.24f), 0.015f, 0.2f, new Color(0.7f, 0.7f, 0.72f), k));
        Strip(Box("TapSpout", new Vector3(-0.4f, 1.12f, 0.17f), new Vector3(0.03f, 0.03f, 0.16f), new Color(0.7f, 0.7f, 0.72f), k, 0.8f, 0.8f));

        // CLUE 5 - two chai cups in the sink
        var cups = new GameObject("TwoChaiCups").transform;
        cups.SetParent(k, false);
        cups.localPosition = new Vector3(-0.4f, 0.96f, 0.02f);
        var cupA = Cyl("CupLipstick", new Vector3(-0.08f, 0.04f, 0), 0.04f, 0.08f, new Color(0.95f, 0.93f, 0.88f), cups);
        cupA.transform.localRotation = Quaternion.Euler(0, 0, 80);
        Strip(Box("Lipstick", new Vector3(-0.07f, 0.09f, 0), new Vector3(0.015f, 0.01f, 0.02f), new Color(0.75f, 0.1f, 0.2f), cups, 0f));
        Cyl("CupPlain", new Vector3(0.09f, 0.04f, 0.03f), 0.04f, 0.08f, new Color(0.9f, 0.9f, 0.9f), cups).transform.localRotation = Quaternion.Euler(75, 0, 10);
        Strip(cups.gameObject);
        MakeClue(cups.gameObject, "5_TwoChaiCups");

        // kettle on burner + steel tiffin
        Strip(Cyl("Kettle", new Vector3(0.45f, 1.05f, 0), 0.09f, 0.2f, new Color(0.7f, 0.7f, 0.72f), k));
        Strip(Cyl("Tiffin", new Vector3(0.8f, 1.02f, 0.05f), 0.08f, 0.14f, new Color(0.78f, 0.78f, 0.8f), k));
        // upper shelf with jars
        Box("UpperShelf", new Vector3(0, 1.75f, 0.14f), new Vector3(2.0f, 0.04f, 0.3f), Wood, k);
        for (int i = 0; i < 6; i++)
            Strip(Cyl("Jar" + i, new Vector3(-0.8f + i * 0.3f, 1.86f, 0.14f), 0.055f, 0.18f,
                Color.HSVToRGB(0.08f + i * 0.09f, 0.6f, 0.75f), k));
    }

    // --------------------------------------------------------------- BOOKSHELF
    void BuildBookshelf()
    {
        // on the back wall left of window, above the bed side (x ~ -0.6)
        float sx = -0.75f, sz = HalfD - 0.2f;
        var shelf = new GameObject("Bookshelf").transform;
        shelf.SetParent(root, false);
        shelf.localPosition = new Vector3(sx, 0, sz);
        Box("Back", new Vector3(0, 1.0f, 0.1f), new Vector3(1.0f, 2.0f, 0.03f), DarkWood, shelf);
        Box("SideL", new Vector3(-0.5f, 1.0f, 0), new Vector3(0.03f, 2.0f, 0.3f), Wood, shelf);
        Box("SideR", new Vector3(0.5f, 1.0f, 0), new Vector3(0.03f, 2.0f, 0.3f), Wood, shelf);
        for (int i = 0; i <= 4; i++)
            Box("Plank" + i, new Vector3(0, 0.05f + i * 0.5f, 0), new Vector3(1.0f, 0.03f, 0.3f), Wood, shelf);
        var rnd = new System.Random(11);
        for (int row = 0; row < 4; row++)
        {
            float xx = -0.45f;
            while (xx < 0.42f)
            {
                float w = 0.03f + (float)rnd.NextDouble() * 0.04f;
                float h = 0.28f + (float)rnd.NextDouble() * 0.15f;
                var col = Color.HSVToRGB((float)rnd.NextDouble(), 0.5f, 0.35f + (float)rnd.NextDouble() * 0.3f);
                if (row == 1) col = Color.Lerp(col, new Color(0.15f, 0.1f, 0.05f), 0.6f); // law books - dark
                Strip(Box("Book", new Vector3(xx + w / 2, 0.065f + row * 0.5f + h / 2, 0.0f), new Vector3(w, h, 0.2f), col, shelf, 0.15f));
                xx += w + 0.003f;
            }
        }
        // CLUE 7 - the burner phone tucked behind the law books (peeking out)
        var phone = Box("BurnerPhone", new Vector3(0.1f, 1.13f, -0.16f), new Vector3(0.05f, 0.11f, 0.014f), new Color(0.08f, 0.08f, 0.08f), shelf, 0.6f);
        phone.transform.localRotation = Quaternion.Euler(0, 0, 10);
        Strip(phone);
        MakeClue(phone, "7_BurnerPhone");
    }

    // ------------------------------------------------------------ REAL FURNITURE
    /// <summary>
    /// Replaces the look of the primitive furniture with photo-scanned CC0 models (Resources/Props) without moving
    /// anything the story depends on: the primitive pieces stay as invisible colliders (so walking, clue hit-boxes and
    /// clue positions are unchanged) and each model is fitted to exactly the same box, so surfaces that hold clues
    /// stay at the same height. A model that would have to be stretched visibly to fit is not used.
    /// </summary>
    void DressFurniture()
    {
        Dress(root.Find("Bed"), new[] { "Frame", "Headboard" }, "old_bed_frame");
        Dress(root.Find("Desk"), new[] { "DeskTop", "Leg", "DeskDrawers", "DrawerHandle" }, "WoodenTable_01");
        Dress(root.Find("Chair"), new[] { "Seat", "Back", "ChairLeg" }, "painted_wooden_chair_01");
        Dress(root.Find("BedsideTable"), new[] { "Body", "Handle" }, "vintage_cabinet_01");
    }

    /// <summary>Fit prop `id` to the combined box of the named parts of `group`, hide those parts. Returns false if skipped.</summary>
    bool Dress(Transform group, string[] parts, string id, float maxStretch = 1.35f)
    {
        if (group == null || !PropKit.Has(id)) return false;
        var hide = new List<Renderer>();
        foreach (Transform c in group)
            foreach (var n in parts)
                if (c.name == n && c.GetComponent<Renderer>() != null && c.GetComponent<ClueTag>() == null) hide.Add(c.GetComponent<Renderer>());
        if (hide.Count == 0) return false;
        Quaternion keep = group.localRotation;
        group.localRotation = Quaternion.identity;                      // measure in the group's own axes
        Bounds target = hide[0].bounds; foreach (var r in hide) target.Encapsulate(r.bounds);
        var prop = PropKit.Place(group, id, Vector3.zero);
        float bestStretch = float.MaxValue, bestYaw = 0f; Vector3 bestRatio = Vector3.one;
        foreach (float yaw in new[] { 0f, 90f })
        {
            prop.transform.localRotation = Quaternion.Euler(0, yaw, 0); prop.transform.localScale = Vector3.one;
            Bounds pb = PropBounds(prop);
            if (pb.size.x < 1e-3f || pb.size.y < 1e-3f || pb.size.z < 1e-3f) continue;
            Vector3 ratio = new Vector3(target.size.x / pb.size.x, target.size.y / pb.size.y, target.size.z / pb.size.z);
            float stretch = Mathf.Max(ratio.x, Mathf.Max(ratio.y, ratio.z)) / Mathf.Min(ratio.x, Mathf.Min(ratio.y, ratio.z));
            if (stretch < bestStretch) { bestStretch = stretch; bestYaw = yaw; bestRatio = ratio; }
        }
        if (bestStretch > maxStretch)
        {
            Debug.Log("Furniture: kept the simple " + group.name + " (" + id + " would be stretched x" + bestStretch.ToString("0.00") + ")");
            Destroy(prop); group.localRotation = keep; return false;
        }
        prop.transform.localRotation = Quaternion.Euler(0, bestYaw, 0);
        // the ratio is in group axes; at 90 degrees the model's own x and z are swapped
        prop.transform.localScale = bestYaw == 0f ? bestRatio : new Vector3(bestRatio.z, bestRatio.y, bestRatio.x);
        Bounds fb = PropBounds(prop);
        prop.transform.position += new Vector3(target.center.x - fb.center.x, target.min.y - fb.min.y, target.center.z - fb.center.z);
        foreach (var c in prop.GetComponentsInChildren<Collider>()) Destroy(c);       // the hidden primitives are the colliders
        foreach (var r in hide) r.enabled = false;
        group.localRotation = keep;
        return true;
    }

    static Bounds PropBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // ------------------------------------------------------------------ DECOR
    void BuildDecor()
    {
        // framed pictures on the left wall: Gandhi-era style portrait + Kali temple photo
        for (int i = 0; i < 2; i++)
        {
            Strip(Box("Frame" + i, new Vector3(-HalfW + 0.03f, 1.7f, -1.8f + i * -0.0f + (i == 0 ? 1.25f : 0.35f)), new Vector3(0.04f, 0.5f, 0.4f), DarkWood));
            Strip(Box("Pic" + i, new Vector3(-HalfW + 0.055f, 1.7f, -1.8f + (i == 0 ? 1.25f : 0.35f)), new Vector3(0.01f, 0.42f, 0.32f), i == 0 ? new Color(0.5f, 0.6f, 0.4f) : new Color(0.7f, 0.35f, 0.25f), null, 0f));
        }
        // calendar / diya
        Strip(Box("Calendar", new Vector3(1.4f, 1.9f, -HalfD + 0.05f), new Vector3(0.28f, 0.4f, 0.01f), new Color(0.9f, 0.85f, 0.7f), null, 0f));
        // scales-of-justice statuette (Priya is a lawyer) on the bookshelf-top
        var stat = new GameObject("JusticeScales").transform;
        stat.SetParent(root, false);
        stat.localPosition = new Vector3(-0.75f, 2.0f, HalfD - 0.2f);
        Strip(Cyl("Post", new Vector3(0, 0.15f, 0), 0.01f, 0.3f, new Color(0.75f, 0.6f, 0.2f), stat));
        Strip(Box("Beam", new Vector3(0, 0.3f, 0), new Vector3(0.25f, 0.01f, 0.01f), new Color(0.75f, 0.6f, 0.2f), stat));
        Strip(Cyl("PanL", new Vector3(-0.12f, 0.2f, 0), 0.05f, 0.01f, new Color(0.75f, 0.6f, 0.2f), stat));
        Strip(Cyl("PanR", new Vector3(0.12f, 0.2f, 0), 0.05f, 0.01f, new Color(0.75f, 0.6f, 0.2f), stat));
        // potted plant near window
        var pot = Cyl("Pot", new Vector3(2.6f, 0.15f, HalfD - 0.4f), 0.15f, 0.3f, new Color(0.65f, 0.3f, 0.15f));
        for (int i = 0; i < 6; i++)
        {
            var leaf = Sphere("Leaf", new Vector3(2.6f + Mathf.Sin(i) * 0.12f, 0.45f + i * 0.04f, HalfD - 0.4f + Mathf.Cos(i) * 0.12f), 0.22f, new Color(0.15f, 0.45f, 0.2f));
            leaf.transform.localScale = new Vector3(0.18f, 0.32f, 0.18f);
            Strip(leaf);
        }
        // clothesline of dupatta by window (city clue)
        // small side table / stool
        Strip(Cyl("Stool", new Vector3(-0.7f, 0.22f, -1.7f), 0.2f, 0.44f, Wood));
    }

    // -------------------------------------------------------------------- FAN
    void BuildFan()
    {
        fan = new GameObject("CeilingFan").transform;
        fan.SetParent(root, false);
        fan.localPosition = new Vector3(0f, Height - 0.35f, 0f);
        Cyl("Rod", new Vector3(0, 0.2f, 0), 0.015f, 0.3f, new Color(0.2f, 0.2f, 0.2f), fan);
        Cyl("Motor", new Vector3(0, 0, 0), 0.09f, 0.1f, new Color(0.25f, 0.2f, 0.15f), fan);
        for (int i = 0; i < 3; i++)
        {
            var arm = new GameObject("Blade" + i).transform;
            arm.SetParent(fan, false);
            arm.localRotation = Quaternion.Euler(0, i * 120f, 0);
            var b = Box("BladeMesh", new Vector3(0.5f, -0.03f, 0), new Vector3(0.75f, 0.015f, 0.16f), new Color(0.55f, 0.4f, 0.25f), arm, 0.2f);
            b.transform.localRotation = Quaternion.Euler(6, 0, 0);
            Strip(b);
        }
        foreach (var c in fan.GetComponentsInChildren<Collider>()) Destroy(c);
    }

    // --------------------------------------------------------------- LIGHTING
    void BuildLighting()
    {
        // main scene sun -> low, orange, coming through window (sunset)
        sunLight = FindAnyObjectByType<Light>();
        foreach (var l in FindObjectsByType<Light>())
            if (l.type == LightType.Directional) { sunLight = l; break; }
        if (sunLight != null && sunLight.type == LightType.Directional)
        {
            sunLight.color = new Color(1f, 0.6f, 0.3f);
            sunLight.intensity = 3.6f;
            sunLight.transform.rotation = Quaternion.Euler(12f, 180f + 10f, 0f); // low sun from the window side, shining into room
            sunLight.shadows = LightShadows.Soft;
        }
        else
        {
            var go = new GameObject("Sunset");
            go.transform.SetParent(root, false);
            sunLight = go.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = new Color(1f, 0.6f, 0.3f);
            sunLight.intensity = 2.4f;
            sunLight.transform.rotation = Quaternion.Euler(12f, 190f, 0f);
            sunLight.shadows = LightShadows.Soft;
        }

        // warm ceiling bulb
        var bulb = new GameObject("CeilingBulb").AddComponent<Light>();
        bulb.transform.SetParent(root, false);
        bulb.transform.localPosition = new Vector3(0, 2.6f, 0);
        bulb.type = LightType.Point; bulb.range = 9f; bulb.intensity = 3.4f;
        bulb.color = new Color(1f, 0.82f, 0.55f);
        bulb.shadows = LightShadows.Soft;

        // fill from window (golden hour spill)
        var win = new GameObject("WindowSpill").AddComponent<Light>();
        win.transform.SetParent(root, false);
        win.transform.localPosition = new Vector3(1.2f, 1.6f, HalfD - 0.3f);
        win.type = LightType.Point; win.range = 7f; win.intensity = 3.6f;
        win.color = new Color(1f, 0.55f, 0.25f);

        // ambient
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.32f, 0.22f, 0.2f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.25f, 0.15f, 0.12f);
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.012f;
    }


    // ============================================================ MATERIALS
    /// <summary>Swaps the flat colours for tileable PBR textures (normal-mapped, varying roughness).</summary>
    void Texturize()
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            var go = t.gameObject;
            string n = t.name;
            if (go.GetComponent<MeshRenderer>() == null || go.GetComponent<ClueTag>() != null) continue;
            if (go.GetComponentInParent<ClueTag>() != null) continue;

            if (n == "Floor") GameKit.Surface(go, "wood_floor", 1.3f, 1f, 1f, new Color(0.92f, 0.7f, 0.5f));
            else if (n == "HallFloor" || n == "LowerFloor") GameKit.Surface(go, "stone", 0.9f, 1f, 1f);          // chequered mosaic tiles
            else if (n == "Board" || n == "RugTrim" || n == "RugIn") go.SetActive(false);   // the textures draw these now
            else if (n == "Rug") GameKit.SurfaceFit(go, "rug", Color.white);
            else if (n.StartsWith("Wall_") || n.StartsWith("Hall") && !n.Contains("Light") || n == "Ceiling" || n == "DampStain")
                GameKit.Surface(go, "plaster", 1.6f, 1f, 1f);
            else if (n.StartsWith("Skirt") || n.Contains("Wardrobe") || n.StartsWith("Desk") || n.StartsWith("Plank") || n.Contains("Frame")
                     || n == "Headboard" || n == "Door" || n == "FlatDoor" || n == "Stool" || n == "Body" || n == "Seat" && false)
                GameKit.Surface(go, "wood", 0.9f, 1f, 1f);
            else if (n == "Mattress" || n == "Blanket" || n.StartsWith("Pillow") || n.StartsWith("Curtain_") || n.StartsWith("Cloth") || n.StartsWith("Kurta"))
                GameKit.Surface(go, "fabric", 0.35f, 1f, 1f);
            else if (n == "Counter" || n == "CounterTop")
                GameKit.Surface(go, "stone", 0.9f, 1f, 1f);
        }
    }

    // ============================================================ ATMOSPHERE
    /// <summary>Gradient ambient + reflection probe + soft shadows + shafts of golden-hour light.</summary>
    void BuildAtmosphere()
    {
        CinematicLook.Ambient(new Color(0.80f, 0.66f, 0.58f), new Color(0.62f, 0.49f, 0.42f), new Color(0.28f, 0.22f, 0.18f), 1f);
        RenderSettings.skybox = null;
        RenderSettings.fogDensity = 0.003f;
        RenderSettings.fogColor = new Color(0.22f, 0.15f, 0.12f);

        // every real light casts soft shadows with sensible bias
        foreach (var l in root.GetComponentsInChildren<Light>(true))
        {
            // only the ceiling bulb casts (soft) shadows: point-light shadows are expensive
            if (l.name == "CeilingBulb") CinematicLook.Shadows(l, 0.85f, 0.02f, 0.4f);
            else if (l.type != LightType.Directional) l.shadows = LightShadows.None;
        }
        if (sunLight != null) CinematicLook.Shadows(sunLight, 1f, 0.04f, 0.8f);

        CinematicLook.Probe(root, new Vector3(0, 1.4f, 0), new Vector3(7f, 3.2f, 6f), new Color(0.35f, 0.27f, 0.22f), 64);

        // two soft fill lights so corners never go black
        foreach (var fp in new[] { new Vector3(-2.1f, 2.4f, 1.6f), new Vector3(2.1f, 2.4f, -1.4f) })
        {
            var fl = new GameObject("Fill").AddComponent<Light>();
            fl.transform.SetParent(root, false);
            fl.transform.localPosition = fp;
            fl.type = LightType.Point; fl.range = 6.5f; fl.intensity = 1.6f; fl.color = new Color(1f, 0.88f, 0.7f);
            fl.shadows = LightShadows.None;
        }

        // light shafts: soft additive-looking sheets from the window along the sun direction
        var sh = Shader.Find("Sprites/Default");
        if (sh == null || sunLight == null) return;
        var tex = new Texture2D(32, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 32; x++)
            {
                float u = x / 31f, v = y / 63f;
                float a = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 1.6f) * Mathf.Pow(Mathf.Sin(Mathf.PI * v), 0.8f);
                tex.SetPixel(x, y, new Color(1, 1, 1, a * 0.16f));
            }
        tex.Apply();
        var mat = new Material(sh) { mainTexture = tex, color = new Color(1f, 0.78f, 0.42f, 1f) };
        Vector3 dir = sunLight.transform.forward;
        Vector3 n = Vector3.Cross(dir, Vector3.up).normalized;
        float[] xs = { 0.55f, 1.15f, 1.75f };
        for (int i = 0; i < xs.Length; i++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "LightShaft";
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(root, false);
            Vector3 start = new Vector3(xs[i], 1.6f, HalfD - 0.1f);
            q.transform.localPosition = start + dir * 2.4f;
            q.transform.localRotation = Quaternion.LookRotation(n, dir);
            q.transform.localScale = new Vector3(1.3f, 4.8f, 1f);
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    // ----------------------------------------------------------------- PLAYER
    void BuildPlayer()
    {
        // disable any pre-existing camera / audio listener so ours is the only one
        foreach (var c in FindObjectsByType<Camera>())
            c.gameObject.SetActive(false);

        var player = new GameObject("Arjun_Player");
        player.transform.SetParent(root, false);
        player.transform.localPosition = new Vector3(-2.6f, 0.1f, -HalfD - 2.2f); // just in front of the door, in the hallway
        player.transform.localRotation = Quaternion.Euler(0, 0, 0);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.75f; cc.radius = 0.25f; cc.center = new Vector3(0, 0.9f, 0);
        cc.stepOffset = 0.25f; cc.skinWidth = 0.03f;

        var camGo = new GameObject("PlayerCamera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(player.transform, false);
        camGo.transform.localPosition = new Vector3(0, 1.62f, 0);
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f; cam.fieldOfView = 56f;      // a natural lens: wide angles shrink the world and make it read as a miniature
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.05f, 0.05f);
        camGo.AddComponent<AudioListener>();

        // headlight-less; controller
        var ctrl = player.AddComponent<PriyaRoomPlayer>();
        ctrl.cam = camGo.transform;
        PlayerCtrl = ctrl;
        PriyaRoomPlayer.Surface = 0;
    }

    // ================================================================= UPDATE
    void Update()
    {
        if (RoomEffectsOwner != null && RoomEffectsOwner == ExtraRoot) return;
        if (fan != null) fan.Rotate(0, -140f * Time.deltaTime, 0);

        var kb = Keyboard.current;
        if (kb != null && kb.tabKey.wasPressedThisFrame) { visionOn = !visionOn; VisionUsed = true; }
        visionBlend = Mathf.MoveTowards(visionBlend, visionOn ? 1f : 0f, Time.deltaTime * 3f);

        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 3f);
        var gold = new Color(1f, 0.72f, 0.12f);
        for (int i = 0; i < clueRenderers.Count; i++)
        {
            var r = clueRenderers[i];
            if (r == null) continue;
            var m = r.material;
            var tint = rendererTags[i] != null && rendererTags[i].found ? new Color(0.3f, 0.8f, 0.5f) * 0.5f : gold;
            // the clue under the crosshair gets only a faint warm sheen: at full strength it washed dark objects
            // out to flat white (seen in the play-test captures); the ring and the hover light carry the hint
            float focus = (rendererTags[i] != null && rendererTags[i] == Focused && !rendererTags[i].found) ? 0.05f : 0f;      // barely there: even 0.22 tinted the maroon suitcase cream
            Color em = tint * (Mathf.Max(visionBlend, focus) * pulse * 2.5f);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", em);
        }
        RenderSettings.ambientLight = Color.Lerp(new Color(0.32f, 0.22f, 0.2f), new Color(0.16f, 0.12f, 0.14f), visionBlend);
    }

}

/// <summary>Simple first-person controller using the new Input System.</summary>
public class PriyaRoomPlayer : MonoBehaviour
{
    public Transform cam;
    public float speed = 1.75f, sprintMul = 1.9f, lookSens = 0.12f;     // human walking pace; covering ground too fast makes places feel small
    CharacterController cc;
    float pitch;
    float vy;
    public static bool Locked;
    /// <summary>While Locked: the head may still turn (seated at a table, hidden behind cover), within LookRange
    /// degrees of LookCentre, but the body does not move. Never on while a decision is open (the compass reads the mouse).</summary>
    public static bool LookOnly;
    public static float LookCentre, LookRange = 75f;
    public static int Surface;          // 0 = wooden floor (apartment), 1 = street (market)

    // walking feel
    Vector3 vel;
    float speedSm, bobT, appliedRoll, baseFov;
    Vector3 appliedBob;
    int lastStep;
    AudioSource stepSrc;
    Camera camComp;
    static AudioClip[] woodSteps, streetSteps;

    public void SetLook(float yaw, float p)
    {
        transform.rotation = Quaternion.Euler(0, yaw, 0);
        pitch = p;
        cam.localRotation = Quaternion.Euler(pitch, 0, appliedRoll);
    }
    public float Yaw => transform.eulerAngles.y;
    public float Pitch => pitch;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        camComp = cam.GetComponent<Camera>();
        baseFov = camComp != null ? camComp.fieldOfView : 68f;
        stepSrc = gameObject.AddComponent<AudioSource>();
        stepSrc.spatialBlend = 0f; stepSrc.volume = 0.5f; stepSrc.playOnAwake = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        var kb = Keyboard.current; var ms = Mouse.current;
        if (kb == null) return;

        if (!Locked && ms != null && ms.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }

        if (Locked)
        {
            vel = Vector3.zero; speedSm = 0f;
            if (cc.enabled) cc.Move(new Vector3(0, -1f, 0) * Time.deltaTime);
            if (LookOnly && !GameState.Paused)
            {
                Vector2 d = Vector2.zero;
                if (ms != null && Cursor.lockState == CursorLockMode.Locked) d += ms.delta.ReadValue() * lookSens;
                d += GameInput.Look * GameInput.LookSpeed * Time.deltaTime;
                float yaw = transform.eulerAngles.y + d.x;
                float off = Mathf.Clamp(Mathf.DeltaAngle(LookCentre, yaw), -LookRange, LookRange);
                transform.rotation = Quaternion.Euler(0f, LookCentre + off, 0f);
                pitch = Mathf.Clamp(pitch - d.y, -60f, 60f);
            }
            ApplyBob(Vector3.zero, 0f);
            return;
        }

        if (ms != null && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 d = ms.delta.ReadValue() * lookSens;
            transform.Rotate(0, d.x, 0);
            pitch = Mathf.Clamp(pitch - d.y, -80f, 80f);
        }

        // Arduino right stick: look (added to the mouse, does not need the cursor lock). Up = look up.
        Vector2 stickLook = GameInput.Look;
        if (stickLook.sqrMagnitude > 0f)
        {
            float rate = GameInput.LookSpeed * Time.deltaTime;
            transform.Rotate(0, stickLook.x * rate, 0);
            pitch = Mathf.Clamp(pitch - stickLook.y * rate, -80f, 80f);
        }

        Vector2 mv = Vector2.zero;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) mv.y += 1;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) mv.y -= 1;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) mv.x += 1;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) mv.x -= 1;
        mv += GameInput.Move;                       // Arduino left stick, analog, on top of the keys
        mv = Vector2.ClampMagnitude(mv, 1f);
        bool sprint = kb.leftShiftKey.isPressed && mv.y > 0.1f;
        float s = speed * (sprint ? sprintMul : 1f) * (mv.y < -0.1f ? 0.65f : 1f);   // backing up is slower

        // acceleration / deceleration instead of an instant start and stop
        Vector3 targetVel = (transform.right * mv.x + transform.forward * mv.y) * s;
        vel = Vector3.MoveTowards(vel, targetVel, (mv.sqrMagnitude > 0.01f ? 13f : 18f) * Time.deltaTime);

        // grounded, the capsule is pressed down firmly enough to follow a flight of stairs going down (31 degrees at a
        // sprint needs ~2 m/s): with the old -1 m/s it left the steps and fell from one to the next
        vy = cc.isGrounded ? -4f : vy - 9.81f * Time.deltaTime;
        if (GameInput.JumpPressed && cc.isGrounded) vy = GameInput.JumpSpeed;      // Arduino left button, once per press
        Vector3 move = vel; move.y = vy;
        cc.Move(move * Time.deltaTime);

        // ---------- head bob, sway, footsteps
        float groundSpeed = new Vector3(cc.velocity.x, 0, cc.velocity.z).magnitude;
        speedSm = Mathf.Lerp(speedSm, cc.isGrounded ? groundSpeed : 0f, Time.deltaTime * 10f);
        float k = Mathf.Clamp01(speedSm / 2.2f);
        bobT += speedSm * Time.deltaTime * 4.2f;                      // one full cycle = two steps (~1.5 m)
        float amp = sprint ? 1.45f : 1f;
        var bob = new Vector3(Mathf.Cos(bobT) * 0.022f * k * amp,
                              Mathf.Sin(bobT * 2f) * 0.032f * k * amp - 0.012f * k,
                              0f);
        ApplyBob(bob, Mathf.Cos(bobT) * 0.7f * k * amp);

        int stepIdx = Mathf.FloorToInt(bobT / Mathf.PI);
        if (stepIdx != lastStep)
        {
            lastStep = stepIdx;
            if (speedSm > 0.6f) PlayStep(sprint ? 0.75f : 0.5f);
        }

        // sprinting widens the view slightly
        if (camComp != null)
        {
            float tf = baseFov + (sprint && speedSm > speed * 1.2f ? 4f : 0f);
            camComp.fieldOfView = Mathf.Lerp(camComp.fieldOfView, tf, Time.deltaTime * 6f);
        }
    }

    /// <summary>Applies the bob as an offset on top of wherever the camera already is (other scripts can still crouch it).</summary>
    void ApplyBob(Vector3 bob, float roll)
    {
        Vector3 basePos = cam.localPosition - appliedBob;
        appliedBob = Vector3.Lerp(appliedBob, bob, 1f - Mathf.Exp(-30f * Time.deltaTime));
        cam.localPosition = basePos + appliedBob;
        appliedRoll = Mathf.Lerp(appliedRoll, roll, 1f - Mathf.Exp(-20f * Time.deltaTime));
        cam.localRotation = Quaternion.Euler(pitch, 0, appliedRoll);
    }

    void PlayStep(float vol)
    {
        var set = Surface == 1 ? streetSteps : woodSteps;
        if (set == null) { MakeSteps(); set = Surface == 1 ? streetSteps : woodSteps; }
        stepSrc.pitch = Random.Range(0.9f, 1.1f);
        stepSrc.PlayOneShot(set[Random.Range(0, set.Length)], vol);
    }

    // ---- synthesised footsteps (no audio files needed)
    static void MakeSteps()
    {
        woodSteps = new AudioClip[3];
        streetSteps = new AudioClip[3];
        for (int v = 0; v < 3; v++)
        {
            woodSteps[v] = Step(true, v);
            streetSteps[v] = Step(false, v);
        }
    }

    static AudioClip Step(bool wood, int variant)
    {
        const int sr = 44100;
        int n = sr * (wood ? 22 : 18) / 100;
        var d = new float[n];
        var r = new System.Random(40 + variant * 7 + (wood ? 0 : 100));
        float lp = 0, lp2 = 0;
        float f0 = (wood ? 78f : 95f) + variant * 6f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float w = (float)(r.NextDouble() * 2 - 1);
            lp += (w - lp) * (wood ? 0.1f : 0.35f);
            lp2 += (lp - lp2) * 0.3f;
            float body = Mathf.Sin(2f * Mathf.PI * f0 * t) * Mathf.Exp(-t * (wood ? 20f : 30f));
            float tex = (wood ? lp2 * 2.2f * Mathf.Exp(-t * 26f) : lp * 1.1f * Mathf.Exp(-t * 38f));
            float grit = wood ? 0f : (r.NextDouble() < 0.02 ? (float)(r.NextDouble() - 0.5) * Mathf.Exp(-t * 14f) : 0f);
            d[i] = body * (wood ? 0.9f : 0.45f) + tex + grit;
        }
        float m = 0.0001f;
        for (int i = 0; i < n; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
        for (int i = 0; i < n; i++) d[i] *= 0.8f / m;
        var c = AudioClip.Create("step", n, 1, sr, false);
        c.SetData(d, 0);
        return c;
    }
}

/// <summary>Attached to every clue object; the story script reads it.</summary>
public class ClueTag : MonoBehaviour
{
    public string id;
    public bool found;
}

/// <summary>A tube light on its last legs.</summary>
public class FlickerLight : MonoBehaviour
{
    Light l; float baseI, next;
    void Start() { l = GetComponent<Light>(); baseI = l.intensity; }
    void Update()
    {
        if (l == null) return;
        if (Time.time > next)
        {
            bool dip = Random.value < 0.25f;
            l.intensity = dip ? baseI * Random.Range(0.1f, 0.5f) : baseI;
            next = Time.time + (dip ? Random.Range(0.04f, 0.12f) : Random.Range(0.6f, 3f));
        }
    }
}
