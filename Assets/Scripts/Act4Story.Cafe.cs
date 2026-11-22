using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Café Irani's working counter, its owner at work and the night street outside (Act IV, variant A).
///
/// The counter was a solid pale box with a second pale box on it for the "glass case", the biscuits modelled inside
/// that box (so hidden), and a window lamp 0.9 m away burning both to white. Now: a panelled teak front with a recessed
/// toe kick, a 3 cm marble worktop, a glass biscuit case on an aluminium frame (clear front, side and top panes, a tray
/// of Osmania biscuits you can read through the glass), the kettle, a steel jug and cups within the owner's reach, a
/// warm pendant lamp over it that casts the contact shadows, and the owner on the staff side doing a quiet task.
/// Outside, across a kerbed road: a shuttered shop, lived-in flats over it, a recessed doorway, the corner of a side
/// street, a parked car and a scooter in the lamp's pool of light, and a passer-by now and then.
/// </summary>
public partial class Act4Story
{
    // ---- read by the play-test route: the worktop surface, the owner's cup and how well the hand stays on it
    float counterTopY;
    Transform taskCup;
    bool taskHolding;
    float taskGripError;                  // the largest wrist-to-grip distance while the cup is carried (metres)

    Transform ownerLook, ownerHandR, ownerHandL;
    Vector3 cupSpotA, cupSpotB;

    static Material glassMat, biscuitMatA, biscuitMatB, biscuitMatC;
    MarketWorld streetKit;                // the market's builders (scooter, lit sign paint), without building a market

    /// <summary>Clear glass: transparent with its specular highlights kept (URP "preserve specular lighting").</summary>
    static Material Glass()
    {
        if (glassMat != null) return glassMat;
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "CaseGlass" };
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.SetFloat("_BlendModePreserveSpecular", 1f);
        m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)RenderQueue.Transparent;
        var c = new Color(0.78f, 0.86f, 0.84f, 0.09f);
        m.color = c; m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.96f); m.SetFloat("_Metallic", 0f);
        // the lamps' highlights stay; the environment reflection turned the top pane into a milky sheet at grazing angles
        m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        glassMat = m;
        return m;
    }

    GameObject Part(Transform parent, string n, Vector3 pos, Vector3 size, Material m, bool shadows = true)
    {
        var go = GameKit.Box(parent, n, pos, size, m);
        var r = go.GetComponent<Renderer>();
        r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        return go;
    }

    GameObject Pane(Transform parent, string n, Vector3 pos, Vector3 size)
    {
        var go = GameKit.Box(parent, n, pos, size, Glass());
        var r = go.GetComponent<Renderer>();
        r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        return go;
    }

    static Material Wood(Color tint, float tileM = 0.6f, float smooth = 0.9f) { return ProcTex.Mat(ProcTex.Get("wood"), tint, Vector2.one / tileM, smooth, 1f); }

    // ===================================================================== the counter
    void BuildCounter()
    {
        var root = new GameObject("CafeCounter").transform;
        root.SetParent(transform, false);
        var teak = new Color(0.30f, 0.17f, 0.09f);
        var teakDeep = new Color(0.19f, 0.11f, 0.06f);
        var alu = GameKit.Lit(new Color(0.60f, 0.60f, 0.58f), 0.55f, 0.85f);
        var steel = GameKit.Lit(new Color(0.66f, 0.66f, 0.64f), 0.7f, 0.95f);
        const float x0 = 2.05f, x1 = 4.55f, zf = -2.45f, zb = -3.15f;      // customer face at zf (+z side), staff side at zb
        const float topY = 0.975f, topT = 0.03f;
        counterTopY = topY + topT;
        float cx = (x0 + x1) * 0.5f, len = x1 - x0;

        // the volume the player bumps into is the same as the old counter's
        Hide(B("CounterCollider", new Vector3(3.3f, 0.5f, -2.8f), new Vector3(2.6f, 1f, 0.8f), Color.black, true));

        // toe kick: a dark plinth set back 7 cm under the front, so the counter stands on the floor rather than in it
        Part(root, "ToeKick", new Vector3(cx, 0.05f, (zf - 0.07f + zb + 0.04f) * 0.5f), new Vector3(len - 0.06f, 0.1f, (zf - 0.07f) - (zb + 0.04f)), GameKit.Lit(new Color(0.07f, 0.05f, 0.04f), 0.2f));
        // the front skin (the recessed panel faces), the two ends and the staff-side frame
        var deep = Wood(teakDeep, 0.5f, 0.85f);
        var face = Wood(teak, 0.6f, 0.95f);
        Part(root, "CounterFront", new Vector3(cx, (0.1f + topY) * 0.5f, zf - 0.04f), new Vector3(len, topY - 0.1f, 0.04f), deep);
        foreach (float ex in new[] { x0 + 0.02f, x1 - 0.02f })
            Part(root, "CounterEnd", new Vector3(ex, topY * 0.5f, (zf + zb) * 0.5f), new Vector3(0.04f, topY, zf - zb), deep);
        // stiles and rails, 2.2 cm proud of the panels: four bays across the front
        float frontZ = zf - 0.02f + 0.011f;
        Part(root, "RailBottom", new Vector3(cx, 0.15f, frontZ), new Vector3(len, 0.1f, 0.022f), face);
        Part(root, "RailTop", new Vector3(cx, topY - 0.065f, frontZ), new Vector3(len, 0.13f, 0.022f), face);
        int bays = 4;
        float bayW = len / bays;
        for (int i = 0; i <= bays; i++)
            Part(root, "Stile", new Vector3(x0 + i * bayW, (0.1f + topY) * 0.5f, frontZ), new Vector3(0.075f, topY - 0.1f, 0.022f), face);
        for (int i = 0; i < bays; i++)
        {
            // a raised field inside each bay, with a bevel suggested by a slightly darker frame round it
            float bx = x0 + (i + 0.5f) * bayW;
            Part(root, "PanelField", new Vector3(bx, (0.2f + topY - 0.13f) * 0.5f, zf - 0.02f + 0.006f), new Vector3(bayW - 0.075f - 0.09f, topY - 0.13f - 0.2f - 0.09f, 0.012f), face, false);
        }
        // ends: one framed panel each
        foreach (float ex in new[] { x0, x1 })
        {
            float sx = ex < cx ? ex - 0.011f : ex + 0.011f;
            Part(root, "EndRail", new Vector3(sx, 0.15f, (zf + zb) * 0.5f), new Vector3(0.022f, 0.1f, zf - zb), face);
            Part(root, "EndRail", new Vector3(sx, topY - 0.065f, (zf + zb) * 0.5f), new Vector3(0.022f, 0.13f, zf - zb), face);
            foreach (float ez in new[] { zf - 0.04f, zb + 0.04f })
                Part(root, "EndStile", new Vector3(sx, (0.1f + topY) * 0.5f, ez), new Vector3(0.022f, topY - 0.1f, 0.075f), face);
        }
        // staff side: open shelves on teak frames, with the café's stock
        Part(root, "BackRail", new Vector3(cx, topY - 0.05f, zb + 0.02f), new Vector3(len, 0.1f, 0.04f), face);
        foreach (float sy in new[] { 0.12f, 0.5f })
            Part(root, "Shelf", new Vector3(cx, sy, (zf - 0.06f + zb) * 0.5f), new Vector3(len - 0.08f, 0.025f, (zf - 0.06f) - zb), deep);
        for (int i = 1; i < bays; i++)
            Part(root, "ShelfDivider", new Vector3(x0 + i * bayW, (0.12f + topY - 0.1f) * 0.5f, (zf - 0.06f + zb) * 0.5f), new Vector3(0.025f, topY - 0.1f - 0.12f, (zf - 0.06f) - zb), deep);
        var white = GameKit.Lit(new Color(0.86f, 0.85f, 0.82f), 0.75f);
        for (int s = 0; s < 3; s++)                                   // stacked cups and saucers, upside down, on the lower shelf
            for (int k = 0; k < 4; k++)
                GameKit.Cyl(root, "StackedCup", new Vector3(2.35f + s * 0.16f, 0.5125f + 0.027f + k * 0.052f, -2.95f), 0.034f, 0.05f, white).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        var jarGlass = GameKit.Lit(new Color(0.55f, 0.62f, 0.6f), 0.9f);
        for (int j = 0; j < 3; j++)
        {
            var jx = 3.1f + j * 0.2f;
            GameKit.Cyl(root, "RuskJar", new Vector3(jx, 0.5125f + 0.11f, -2.92f), 0.075f, 0.22f, jarGlass);
            GameKit.Cyl(root, "RuskJarLid", new Vector3(jx, 0.5125f + 0.225f, -2.92f), 0.06f, 0.02f, alu);
        }
        Part(root, "Cashbox", new Vector3(4.15f, 0.5125f + 0.07f, -2.9f), new Vector3(0.36f, 0.14f, 0.26f), GameKit.Lit(new Color(0.28f, 0.33f, 0.36f), 0.45f, 0.6f));
        Part(root, "Bucket", new Vector3(2.95f, 0.1325f + 0.13f, -2.85f), new Vector3(0.26f, 0.26f, 0.26f), steel).name = "SteelBin";

        // the worktop: a thin marble slab, honed, a little yellowed (the old slab was a near-white glossy box)
        var top = GameKit.Box(root, "CounterTop", new Vector3(cx, topY + topT * 0.5f, (zf + 0.06f + zb - 0.02f) * 0.5f), new Vector3(len + 0.1f, topT, (zf + 0.06f) - (zb - 0.02f)), GameKit.Lit(Color.white));
        GameKit.Surface(top, "marble", 0.7f, 0.55f, 0.6f, new Color(0.66f, 0.64f, 0.60f));
        top.AddComponent<BoxCollider>();

        BuildBiscuitCase(root, alu, steel);

        // within the owner's reach, east of the case: the kettle on its trivet, a steel milk jug, cups on a tray
        float ty = counterTopY;
        Part(root, "Trivet", new Vector3(4.33f, ty + 0.006f, -2.83f), new Vector3(0.22f, 0.012f, 0.22f), GameKit.Lit(new Color(0.05f, 0.05f, 0.05f), 0.3f, 0.5f));
        var kettle = PropKit.Place(root, "vintage_electric_kettle", new Vector3(4.33f, ty + 0.012f, -2.83f), 200f);
        if (kettle == null)
            Turned("Kettle", new Vector3(4.33f, ty + 0.012f, -2.83f), new[] { new Vector2(0f, 0.09f), new Vector2(0.12f, 0.1f), new Vector2(0.18f, 0.06f), new Vector2(0.2f, 0.02f) }, steel, 18);
        // a steel jug turned on a lathe (the scanned one is not in the project)
        Turned("MilkJug", new Vector3(4.07f, ty, -2.6f), new[] { new Vector2(0f, 0.052f), new Vector2(0.02f, 0.056f), new Vector2(0.11f, 0.058f), new Vector2(0.15f, 0.05f), new Vector2(0.175f, 0.047f), new Vector2(0.18f, 0.05f) }, steel, 22);
        GameKit.Cyl(root, "MilkJugInside", new Vector3(4.07f, ty + 0.15f, -2.6f), 0.046f, 0.004f, GameKit.Lit(new Color(0.9f, 0.88f, 0.82f), 0.6f)).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        var jugHandle = GameKit.Box(root, "MilkJugHandle", new Vector3(4.07f + 0.068f, ty + 0.1f, -2.6f), new Vector3(0.012f, 0.09f, 0.02f), steel);
        jugHandle.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        Part(root, "CupTray", new Vector3(3.82f, ty + 0.006f, -2.66f), new Vector3(0.3f, 0.012f, 0.22f), steel, false);
        for (int i = 0; i < 4; i++)
            Cup(root, new Vector3(3.74f + (i % 2) * 0.15f, ty + 0.012f, -2.71f + (i / 2) * 0.1f), white, false);
        // the cup the owner works with, and the two places it moves between
        cupSpotA = new Vector3(3.9f, ty, -3.0f);
        cupSpotB = new Vector3(4.08f, ty, -2.97f);
        taskCup = Cup(root, cupSpotA, white, true);
        // a soft contact shade under the things on the worktop (the pendant's shadows do the rest)
        foreach (var p in new[] { new Vector4(4.33f, -2.83f, 0.26f, 0.26f), new Vector4(4.07f, -2.6f, 0.15f, 0.15f), new Vector4(3.82f, -2.66f, 0.34f, 0.26f) })
            Shade(root, new Vector3(p.x, ty + 0.0012f, p.y), new Vector2(p.z, p.w));

        // a pendant over the counter, hung from a bracket on the café front: the task light, warm, with shadows
        var iron = GameKit.Lit(new Color(0.06f, 0.06f, 0.06f), 0.35f, 0.6f);
        Part(root, "BracketArm", new Vector3(4.55f, 2.62f, -2.85f), new Vector3(0.9f, 0.04f, 0.04f), iron);
        Part(root, "BracketPlate", new Vector3(4.985f, 2.62f, -2.85f), new Vector3(0.03f, 0.18f, 0.12f), iron);
        Part(root, "PendantFlex", new Vector3(4.12f, 2.38f, -2.85f), new Vector3(0.008f, 0.48f, 0.008f), iron, false);
        var shade = Turned("PendantShade", new Vector3(4.12f, 2.02f, -2.85f), new[] { new Vector2(0f, 0.2f), new Vector2(0.04f, 0.18f), new Vector2(0.11f, 0.08f), new Vector2(0.14f, 0.025f) }, GameKit.Lit(new Color(0.12f, 0.22f, 0.18f), 0.55f, 0.3f), 24);
        var shadeR = shade.GetComponent<Renderer>(); shadeR.shadowCastingMode = ShadowCastingMode.TwoSided;
        GameKit.Sphere(root, "PendantBulb", new Vector3(4.12f, 2.02f, -2.85f), 0.07f, GameKit.Glow(new Color(1f, 0.85f, 0.6f), new Color(1f, 0.74f, 0.42f), 5f)).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        var lamp = new GameObject("CounterTaskLight").AddComponent<Light>();
        lamp.transform.SetParent(transform, false);
        lamp.transform.localPosition = new Vector3(4.12f, 1.98f, -2.85f);
        lamp.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        lamp.type = LightType.Spot; lamp.spotAngle = 118f; lamp.innerSpotAngle = 70f;
        lamp.color = new Color(1f, 0.76f, 0.5f); lamp.intensity = 2.3f; lamp.range = 4.2f;
        CinematicLook.Shadows(lamp, 0.85f, 0.02f, 0.4f);
        // and a little of it lifts the shade's underside and the owner's face
        PL(new Vector3(4.0f, 1.75f, -2.95f), new Color(1f, 0.72f, 0.45f), 0.35f, 2.2f);
        // the string lights' spill on the customer side, low, so the panelled front and its toe kick read (it fell to black)
        PL(new Vector3(3.2f, 0.9f, -1.35f), new Color(1f, 0.74f, 0.46f), 0.7f, 2.6f);
    }

    /// <summary>A cup on a saucer: white porcelain, the saucer on the worktop.</summary>
    Transform Cup(Transform root, Vector3 basePos, Material white, bool task)
    {
        var cup = new GameObject(task ? "OwnerCup" : "Cup").transform;
        cup.SetParent(root, false);
        cup.position = basePos;
        var saucer = GameKit.Cyl(cup, "Saucer", Vector3.zero, 0.06f, 0.008f, white);
        saucer.transform.localPosition = new Vector3(0f, 0.004f, 0f);
        var body = VehicleKit.Part(cup, "CupBody", VehicleKit.Lathe(new[] { new Vector2(0f, 0.022f), new Vector2(0.01f, 0.03f), new Vector2(0.055f, 0.037f), new Vector2(0.058f, 0.0375f) }, 18), white, new Vector3(0f, 0.008f, 0f));
        body.transform.localRotation = Quaternion.Euler(0, 0, 90f);
        var tea = GameKit.Cyl(cup, "Tea", new Vector3(0f, 0.008f + 0.045f, 0f), 0.033f, 0.002f, GameKit.Lit(new Color(0.42f, 0.24f, 0.1f), 0.85f));
        tea.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        var handle = GameKit.Box(cup, "CupHandle", new Vector3(0.043f, 0.008f + 0.03f, 0f), new Vector3(0.012f, 0.03f, 0.008f), white);
        foreach (var r in cup.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
        tea.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        handle.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        return cup;
    }

    /// <summary>A soft dark disc on a surface: a contact shade where a lamp's shadow alone would be too small to read.</summary>
    void Shade(Transform root, Vector3 pos, Vector2 size)
    {
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) return;
        var m = new Material(sh) { name = "ContactShade" };
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent - 10;
        const int N = 32;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0.55f * Mathf.Clamp01(1f - r) * Mathf.Clamp01(1f - r)));
            }
        tex.Apply();
        m.SetTexture("_BaseMap", tex); m.mainTexture = tex;
        m.SetColor("_BaseColor", Color.white);
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "ContactShade";
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(root, false);
        q.transform.position = pos;
        q.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        q.transform.localScale = new Vector3(size.x, size.y, 1f);
        var qr = q.GetComponent<Renderer>(); qr.sharedMaterial = m; qr.shadowCastingMode = ShadowCastingMode.Off; qr.receiveShadows = false;
    }

    /// <summary>
    /// The biscuit case: a wooden plinth, an aluminium frame, clear glass front, sides and top, sliding glass doors on
    /// the staff side, a steel tray with Osmania biscuits laid in overlapping rows and a few stacked, and a price card.
    /// </summary>
    void BuildBiscuitCase(Transform root, Material alu, Material steel)
    {
        float y0 = counterTopY;
        const float cx = 2.9f, cz = -2.72f, w = 1.2f, d = 0.46f, plinth = 0.035f, h = 0.36f, t = 0.016f;
        var c = new GameObject("DisplayCase").transform;
        c.SetParent(root, false);
        c.position = new Vector3(cx, y0, cz);
        var vol = c.gameObject.AddComponent<BoxCollider>();             // the case is solid to rays (focus, interaction)
        vol.center = new Vector3(0f, (plinth + h) * 0.5f, 0f); vol.size = new Vector3(w, plinth + h, d);
        Part(c, "CasePlinth", new Vector3(0f, plinth * 0.5f, 0f), new Vector3(w, plinth, d), Wood(new Color(0.2f, 0.11f, 0.06f), 0.5f, 0.8f));
        float yb = plinth, yt = plinth + h;
        // frame: four posts, top and bottom rings
        foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                Part(c, "CasePost", new Vector3(sx * (w * 0.5f - t * 0.5f), (yb + yt) * 0.5f, sz * (d * 0.5f - t * 0.5f)), new Vector3(t, h, t), alu);
        foreach (float yy in new[] { yb + t * 0.5f, yt - t * 0.5f })
        {
            foreach (float sz in new[] { -1f, 1f }) Part(c, "CaseRail", new Vector3(0f, yy, sz * (d * 0.5f - t * 0.5f)), new Vector3(w, t, t), alu);
            foreach (float sx in new[] { -1f, 1f }) Part(c, "CaseRail", new Vector3(sx * (w * 0.5f - t * 0.5f), yy, 0f), new Vector3(t, t, d), alu);
        }
        // glass: front (customer side, +z), two ends, top; two sliding doors behind (staff side, -z)
        float g = 0.005f, inner = h - 2f * t;
        Pane(c, "CaseGlassFront", new Vector3(0f, (yb + yt) * 0.5f, d * 0.5f - t * 0.5f), new Vector3(w - 2f * t, inner, g));
        foreach (float sx in new[] { -1f, 1f }) Pane(c, "CaseGlassEnd", new Vector3(sx * (w * 0.5f - t * 0.5f), (yb + yt) * 0.5f, 0f), new Vector3(g, inner, d - 2f * t));
        Pane(c, "CaseGlassTop", new Vector3(0f, yt - t * 0.5f, 0f), new Vector3(w - 2f * t, g, d - 2f * t));
        Pane(c, "CaseDoorL", new Vector3(-w * 0.25f + 0.01f, (yb + yt) * 0.5f, -d * 0.5f + t * 0.5f + 0.004f), new Vector3(w * 0.5f - t, inner, g));
        Pane(c, "CaseDoorR", new Vector3(w * 0.25f - 0.01f, (yb + yt) * 0.5f, -d * 0.5f + t * 0.5f - 0.004f), new Vector3(w * 0.5f - t, inner, g));
        Part(c, "CaseDoorPull", new Vector3(-0.06f, yb + 0.12f, -d * 0.5f + 0.001f), new Vector3(0.012f, 0.06f, 0.012f), alu, false);
        // the tray, then the biscuits: round, golden, a little domed, browner at the rim
        Part(c, "BiscuitTray", new Vector3(0f, yb + 0.006f, 0.01f), new Vector3(w - 0.1f, 0.012f, d - 0.12f), steel, false);
        foreach (float sz in new[] { -1f, 1f }) Part(c, "TrayLip", new Vector3(0f, yb + 0.016f, 0.01f + sz * (d - 0.12f) * 0.5f), new Vector3(w - 0.1f, 0.02f, 0.006f), steel, false);
        BuildBiscuits(c, yb + 0.012f, w - 0.16f, d - 0.16f);
        // a handwritten price card leaning on a biscuit stack inside the front glass
        var card = Part(c, "PriceCard", new Vector3(0.38f, yb + 0.07f, d * 0.5f - 0.05f), new Vector3(0.11f, 0.065f, 0.002f), GameKit.Lit(new Color(0.88f, 0.85f, 0.76f), 0.2f), false);
        card.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        var ink = GameKit.Solid(GameKit.Text(c, "OSMANIA", new Vector3(0.38f, yb + 0.078f, d * 0.5f - 0.0475f), Quaternion.Euler(-12f, 180f, 0f), 0.016f, new Color(0.12f, 0.08f, 0.06f)));
        var ink2 = GameKit.Solid(GameKit.Text(c, "Rs 10", new Vector3(0.38f, yb + 0.058f, d * 0.5f - 0.052f), Quaternion.Euler(-12f, 180f, 0f), 0.014f, new Color(0.55f, 0.08f, 0.06f)));
        // (the cards face the customers: the text's front is +z)
        ink.transform.localRotation = Quaternion.Euler(12f, 180f, 0f); ink2.transform.localRotation = Quaternion.Euler(12f, 180f, 0f);
    }

    /// <summary>All the biscuits as three combined meshes (three bakes of slightly different colour): one draw each.</summary>
    void BuildBiscuits(Transform parent, float baseY, float areaW, float areaD)
    {
        if (biscuitMatA == null)
        {
            biscuitMatA = GameKit.Lit(new Color(0.84f, 0.60f, 0.30f), 0.42f);
            biscuitMatB = GameKit.Lit(new Color(0.78f, 0.53f, 0.25f), 0.38f);
            biscuitMatC = GameKit.Lit(new Color(0.88f, 0.66f, 0.36f), 0.45f);
        }
        // a biscuit 6 cm across and 1.2 cm thick, its top slightly domed and its rim rounded
        var mesh = VehicleKit.Lathe(new[] { new Vector2(0f, 0.0f), new Vector2(0f, 0.027f), new Vector2(0.0025f, 0.0298f), new Vector2(0.007f, 0.0302f), new Vector2(0.0105f, 0.028f), new Vector2(0.0122f, 0.02f), new Vector2(0.0128f, 0.0f) }, 20);
        var lists = new[] { new List<CombineInstance>(), new List<CombineInstance>(), new List<CombineInstance>() };
        var rnd = new System.Random(1932);
        Quaternion upright = Quaternion.Euler(0, 0, 90f);          // the lathe's axis is X: stand it up
        int cols = 14, rows = 4;
        for (int r = 0; r < rows; r++)
            for (int k = 0; k < cols; k++)
            {
                // rows of biscuits overlapping like tiles, each leaning on the one before
                float x = -areaW * 0.5f + 0.035f + k * (areaW - 0.07f) / (cols - 1) + (float)(rnd.NextDouble() - 0.5) * 0.004f;
                float z = -areaD * 0.5f + 0.04f + r * (areaD - 0.08f) / (rows - 1);
                var rot = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f) * Quaternion.Euler(r == 0 ? 0f : -18f, 0f, 0f) * upright;
                var pos = new Vector3(x, baseY + (r == 0 ? 0f : 0.008f), z);
                lists[rnd.Next(3)].Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(pos, rot, Vector3.one) });
            }
        // a few short stacks at the front corners
        foreach (float sx in new[] { -0.42f, 0.24f })
            for (int s = 0; s < 4; s++)
                lists[s % 3].Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(new Vector3(sx + (float)(rnd.NextDouble() - 0.5) * 0.006f, baseY + 0.0105f + s * 0.0126f, areaD * 0.5f - 0.05f), Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f) * upright, Vector3.one) });
        var mats = new[] { biscuitMatA, biscuitMatB, biscuitMatC };
        for (int i = 0; i < 3; i++)
        {
            var m = new Mesh { name = "OsmaniaBiscuits" + i };
            m.CombineMeshes(lists[i].ToArray(), true, true);
            m.RecalculateBounds();
            var go = new GameObject("OsmaniaBiscuits");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mats[i]; mr.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    // ===================================================================== the owner at work
    /// <summary>
    /// The owner keeps one hand occupied with a cloth on the worktop, now and then reaches for his cup, moves it next to
    /// the jug and sets it down, glances at the entrance, and goes back to work. The hand is put on the cup by two-bone IK
    /// and the cup is carried by the hand's goal, so the hand cannot slide off it; set down, the cup stands on the worktop.
    /// </summary>
    IEnumerator OwnerWork()
    {
        if (owner == null || taskCup == null) yield break;
        owner.freezeFacing = true;
        ownerLook = new GameObject("OwnerLook").transform; ownerLook.SetParent(transform, false);
        ownerLook.position = new Vector3(3.85f, counterTopY, -2.85f);
        owner.lookTarget = ownerLook;
        ownerHandR = new GameObject("OwnerHandGoalR").transform; ownerHandR.SetParent(transform, false);
        ownerHandL = new GameObject("OwnerHandGoalL").transform; ownerHandL.SetParent(transform, false);
        Vector3 fwd = owner.transform.forward, right = owner.transform.right;
        // the cloth hand: resting on the worktop at the staff edge, wiping in slow small circles
        Vector3 clothAt = new Vector3(3.68f, counterTopY + 0.03f, -3.04f);
        var cloth = Part(transform, "Cloth", clothAt - new Vector3(0f, 0.03f, 0f), new Vector3(0.16f, 0.008f, 0.11f), GameKit.Lit(new Color(0.75f, 0.73f, 0.66f), 0.15f), false);
        ownerHandL.position = clothAt;
        owner.Hold(0, ownerHandL, 0.8f);
        owner.lean = 6f;
        Vector3 wristOffset = -fwd * 0.075f + right * 0.015f + Vector3.up * 0.04f;      // the wrist behind and above the cup's grip
        var cupSpot = cupSpotA;
        float t0 = Time.time;
        while (this != null && owner != null && taskCup != null)
        {
            // --- wiping, eyes on the work
            float until = Time.time + Random.Range(3.5f, 6f);
            while (Time.time < until)
            {
                float a = (Time.time - t0) * 2f * Mathf.PI * 0.55f;
                ownerHandL.position = clothAt + new Vector3(Mathf.Cos(a) * 0.04f, 0f, Mathf.Sin(a) * 0.025f);
                cloth.transform.position = ownerHandL.position - new Vector3(0f, 0.03f, 0f) + fwd * 0.03f;
                yield return null;
            }
            // --- the cup: reach, grip, lift, carry, set down, let go
            Vector3 from = cupSpot, to = cupSpot == cupSpotA ? cupSpotB : cupSpotA;
            ownerLook.position = from + Vector3.up * 0.05f;
            Vector3 start = owner.HandPosition(1);
            ownerHandR.position = start;
            owner.Hold(1, ownerHandR, 0.35f);
            yield return MoveGoal(ownerHandR, start, from + wristOffset, 0.55f);
            taskHolding = true; taskGripError = 0f;
            taskCup.SetParent(ownerHandR, true);
            yield return MoveGoal(ownerHandR, from + wristOffset, from + wristOffset + Vector3.up * 0.06f, 0.25f);
            ownerLook.position = to + Vector3.up * 0.05f;
            yield return MoveGoal(ownerHandR, from + wristOffset + Vector3.up * 0.06f, to + wristOffset + Vector3.up * 0.06f, 0.6f, 0.04f);
            yield return MoveGoal(ownerHandR, to + wristOffset + Vector3.up * 0.06f, to + wristOffset, 0.25f);
            taskCup.SetParent(transform.Find("CafeCounter"), true);
            taskCup.position = to;                                  // standing on the worktop, upright
            taskCup.rotation = Quaternion.identity;
            taskHolding = false;
            cupSpot = to;
            owner.Release(1, 0.45f);
            yield return Wait(0.4f);
            // --- a glance toward the entrance, then back to the work
            ownerLook.position = new Vector3(-3.6f, 1.55f, -3.4f);
            yield return Wait(Random.Range(1.1f, 1.7f));
            ownerLook.position = new Vector3(3.85f, counterTopY, -2.95f);
            yield return Wait(Random.Range(0.6f, 1.2f));
        }
    }

    IEnumerator MoveGoal(Transform goal, Vector3 a, Vector3 b, float dur, float arc = 0f)
    {
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / dur);
            goal.position = Vector3.Lerp(a, b, k) + Vector3.up * arc * Mathf.Sin(k * Mathf.PI);
            if (taskHolding && owner != null) taskGripError = Mathf.Max(taskGripError, Vector3.Distance(owner.HandPosition(1), goal.position));
            yield return null;
        }
        goal.position = b;
    }

    // ===================================================================== the street outside
    /// <summary>
    /// West of the terrace: a pavement with a stone kerb, a 6.7 m road, the far pavement and the opposite frontage:
    /// a shuttered provision store under two floors of flats, a taller block with a recessed doorway and a lamp over it,
    /// and a corner house at the side street. A sodium lamp lights the road where a car and a scooter are parked; a cooler
    /// lamp across the road; a soft city glow behind the rooflines so they read against the night sky.
    /// </summary>
    void BuildStreet()
    {
        var root = new GameObject("CafeStreet").transform;
        root.SetParent(transform, false);
        streetKit = new GameObject("StreetKit").AddComponent<MarketWorld>();
        streetKit.transform.SetParent(root, false);
        var statics = new List<GameObject>();
        GameObject S(GameObject go) { statics.Add(go); return go; }
        Material Surf(string key, Color tint, Vector2 size, float tileM, float smooth = 1f) { return ProcTex.Mat(ProcTex.Get(key), tint, size / tileM, smooth, 1f); }

        // ---- ground: pavement, kerb, road, kerb, pavement (the old flat cobble plane is kept under it all as a base)
        S(Part(root, "PavementNear", new Vector3(-6.15f, -0.06f, 0f), new Vector3(1.5f, 0.12f, 34f), Surf("blocks", new Color(0.62f, 0.6f, 0.56f), new Vector2(1.5f, 34f), 0.9f)));
        S(Part(root, "KerbNear", new Vector3(-6.95f, -0.075f, 0f), new Vector3(0.14f, 0.15f, 34f), Surf("concrete", new Color(0.55f, 0.54f, 0.52f), new Vector2(0.14f, 34f), 0.8f)));
        S(Part(root, "Road", new Vector3(-10.27f, -0.2f, 0f), new Vector3(6.5f, 0.1f, 34f), Surf("asphalt", new Color(0.36f, 0.35f, 0.34f), new Vector2(6.5f, 34f), 3f, 0.8f)));
        S(Part(root, "KerbFar", new Vector3(-13.59f, -0.075f, 0f), new Vector3(0.14f, 0.15f, 34f), Surf("concrete", new Color(0.55f, 0.54f, 0.52f), new Vector2(0.14f, 34f), 0.8f)));
        S(Part(root, "PavementFar", new Vector3(-14.43f, -0.06f, 0f), new Vector3(1.55f, 0.12f, 34f), Surf("blocks", new Color(0.58f, 0.56f, 0.52f), new Vector2(1.55f, 34f), 0.9f)));
        // a worn line of paint down the middle and a drain grate at the kerb
        for (int i = 0; i < 9; i++) S(Part(root, "LaneMark", new Vector3(-10.27f, -0.149f, -15f + i * 3.6f), new Vector3(0.1f, 0.002f, 1.6f), GameKit.Lit(new Color(0.55f, 0.53f, 0.45f), 0.3f), false));
        S(Part(root, "Drain", new Vector3(-7.25f, -0.148f, 2.6f), new Vector3(0.45f, 0.004f, 0.8f), GameKit.Lit(new Color(0.08f, 0.08f, 0.08f), 0.4f, 0.6f), false));

        // ---- the opposite frontage (fronts at x = -15.2, facing the café)
        const float fx = -15.2f;
        var plasterA = new Color(0.72f, 0.62f, 0.48f);
        var plasterB = new Color(0.56f, 0.6f, 0.58f);
        var plasterC = new Color(0.74f, 0.66f, 0.6f);
        Frontage(root, statics, fx, -15.5f, -5.6f, 3, plasterA, "shop");
        Frontage(root, statics, fx, -5.6f, 4.4f, 4, plasterB, "door");
        Frontage(root, statics, fx, 4.4f, 12.6f, 2, plasterC, "corner");
        // the corner house turns into the side street: its side wall, with windows, faces the café's end of the street
        float sideZ = 12.6f;
        S(Part(root, "CornerSide", new Vector3(fx - 4f, 3.6f, sideZ + 0.15f), new Vector3(8f, 7.2f, 0.3f), Surf("facade", plasterC, new Vector2(8f, 7.2f), 2.4f)));
        for (int i = 0; i < 2; i++)
        {
            float wx = fx - 1.8f - i * 3.2f;
            S(Part(root, "SideWindowRecess", new Vector3(wx, 4.9f, sideZ + 0.27f), new Vector3(1.0f, 1.3f, 0.08f), GameKit.Lit(new Color(0.05f, 0.05f, 0.06f), 0.6f)));
            S(Part(root, "SideWindowSill", new Vector3(wx, 4.2f, sideZ + 0.36f), new Vector3(1.2f, 0.06f, 0.14f), Surf("concrete", new Color(0.6f, 0.58f, 0.55f), new Vector2(1.2f, 0.14f), 0.6f)));
        }
        // the side street's ground, so the corner opens onto something
        S(Part(root, "SideStreet", new Vector3(-20f, -0.2f, 16.5f), new Vector3(12f, 0.1f, 7.8f), Surf("asphalt", new Color(0.33f, 0.32f, 0.31f), new Vector2(12f, 7.8f), 3f, 0.8f)));
        S(Part(root, "SideStreetFar", new Vector3(-19.2f, 4.8f, 20.6f), new Vector3(9f, 9.6f, 0.4f), Surf("facade", new Color(0.5f, 0.46f, 0.42f), new Vector2(9f, 9.6f), 2.4f)));

        // ---- a soft city glow behind the rooflines (an unlit backdrop far out; the fog does the rest)
        var sky = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sky.name = "CityGlow";
        Destroy(sky.GetComponent<Collider>());
        sky.transform.SetParent(root, false);
        sky.transform.position = new Vector3(-70f, 14f, 0f);
        sky.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
        sky.transform.localScale = new Vector3(160f, 44f, 1f);
        sky.GetComponent<Renderer>().sharedMaterial = GlowGradient();
        sky.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

        // ---- lamps: sodium on this side (spot, with shadows, for the pool on the road and the cars in it), a cooler one opposite
        var post = GameKit.Lit(new Color(0.14f, 0.14f, 0.15f), 0.45f, 0.6f);
        S(Part(root, "LampPostA", new Vector3(-7.05f, 2.9f, -5.2f), new Vector3(0.12f, 5.8f, 0.12f), post));
        S(Part(root, "LampArmA", new Vector3(-7.55f, 5.75f, -5.2f), new Vector3(1.1f, 0.07f, 0.07f), post));
        S(Glow("LampHeadA", new Vector3(-8.05f, 5.66f, -5.2f), new Vector3(0.55f, 0.1f, 0.24f), new Color(1f, 0.6f, 0.25f), 3.2f));
        var sodium = new GameObject("SodiumLamp").AddComponent<Light>();
        sodium.transform.SetParent(transform, false);
        sodium.transform.localPosition = new Vector3(-8.05f, 5.55f, -5.2f);
        sodium.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        sodium.type = LightType.Spot; sodium.spotAngle = 128f; sodium.innerSpotAngle = 60f;
        sodium.color = new Color(1f, 0.6f, 0.28f); sodium.intensity = 30f; sodium.range = 16f;
        CinematicLook.Shadows(sodium, 0.8f, 0.03f, 0.5f);
        S(Part(root, "LampPostB", new Vector3(-13.75f, 2.7f, 8.2f), new Vector3(0.11f, 5.4f, 0.11f), post));
        S(Part(root, "LampArmB", new Vector3(-13.3f, 5.35f, 8.2f), new Vector3(0.9f, 0.06f, 0.06f), post));
        S(Glow("LampHeadB", new Vector3(-12.9f, 5.27f, 8.2f), new Vector3(0.5f, 0.09f, 0.2f), new Color(0.82f, 0.9f, 1f), 2.4f));
        PL(new Vector3(-12.9f, 5.0f, 8.2f), new Color(0.8f, 0.88f, 1f), 12f, 13f);
        // a third, older lamp further down the far side, so the shop front is not lost between the pools
        S(Part(root, "LampPostC", new Vector3(-13.75f, 2.7f, -11.5f), new Vector3(0.11f, 5.4f, 0.11f), post));
        S(Part(root, "LampArmC", new Vector3(-13.3f, 5.35f, -11.5f), new Vector3(0.9f, 0.06f, 0.06f), post));
        S(Glow("LampHeadC", new Vector3(-12.9f, 5.27f, -11.5f), new Vector3(0.5f, 0.09f, 0.2f), new Color(1f, 0.7f, 0.4f), 2.2f));
        PL(new Vector3(-12.9f, 5.0f, -11.5f), new Color(1f, 0.68f, 0.38f), 9f, 12f);

        // ---- parked: a car at the kerb under the sodium lamp, a scooter on the far pavement edge by the shop
        BuildParkedCar(root, new Vector3(-7.95f, -0.15f, -3.4f), 180f);
        var covered = PropKit.Place(root, "covered_car", new Vector3(-12.75f, -0.15f, 6.2f), 0f);
        if (covered != null) FitOnGround(covered.transform, 4.1f, -0.15f);
        var scooter = streetKit.SpawnScooter(new Vector3(-13.2f, -0.15f, -9.6f), 150f, new Color(0.62f, 0.07f, 0.06f));
        if (scooter != null)
        {
            scooter.rotation = Quaternion.Euler(0f, 150f, 6f);          // resting on its side stand
            var off = GameKit.Lit(new Color(0.6f, 0.6f, 0.58f), 0.8f);
            foreach (var r in scooter.GetComponentsInChildren<Renderer>())
                if (r.name == "Headlight" || r.name == "TailLight") r.sharedMaterial = off;      // parked: its lamps are off
        }
        // the street is static: batch it
        StaticBatchingUtility.Combine(statics.ToArray(), root.gameObject);
        StartCoroutine(Passerby());
    }

    static Material glowGradient;
    static Material GlowGradient()
    {
        if (glowGradient != null) return glowGradient;
        const int h = 64;
        var tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var low = new Color(0.17f, 0.11f, 0.08f);      // sodium haze over the old city
        var mid = new Color(0.07f, 0.06f, 0.09f);
        var top = new Color(0.025f, 0.03f, 0.065f);     // the camera's own background
        for (int y = 0; y < h; y++)
        {
            float t = y / (h - 1f);
            tex.SetPixel(0, y, t < 0.35f ? Color.Lerp(low, mid, t / 0.35f) : Color.Lerp(mid, top, (t - 0.35f) / 0.65f));
        }
        tex.Apply();
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "CityGlow" };
        m.SetTexture("_BaseMap", tex); m.mainTexture = tex; m.SetColor("_BaseColor", Color.white);
        glowGradient = m;
        return m;
    }

    /// <summary>
    /// One building of the opposite row: front at x = fx facing +x, from z0 to z1, with storeys of flats over a ground
    /// floor that is a shuttered shop, a recessed doorway, or (on the corner) a closed shop and a lit stair window.
    /// Windows are recessed into the wall with sills and sunshades; a few are lit (curtains, a tube light), most are dark.
    /// </summary>
    void Frontage(Transform root, List<GameObject> statics, float fx, float z0, float z1, int floors, Color plaster, string ground)
    {
        GameObject S(GameObject go) { statics.Add(go); return go; }
        float width = z1 - z0, zc = (z0 + z1) * 0.5f;
        const float groundH = 3.6f, floorH = 3.1f, depth = 9f;
        float height = groundH + (floors - 1) * floorH;
        var wallMat = ProcTex.Mat(ProcTex.Get("facade"), plaster, new Vector2(width, height) / 2.4f, 1f, 1f);
        // the mass, set back so the windows and the doorway can be recessed into a front skin
        S(Part(root, "Block", new Vector3(fx - depth * 0.5f, height * 0.5f, zc), new Vector3(depth, height, width - 0.02f), wallMat));
        // parapet with a coping, and something on the roof (a water tank, a stair head) to break the roofline
        S(Part(root, "Parapet", new Vector3(fx - 0.12f, height + 0.45f, zc), new Vector3(0.24f, 0.9f, width), wallMat));
        S(Part(root, "Coping", new Vector3(fx - 0.1f, height + 0.93f, zc), new Vector3(0.36f, 0.07f, width + 0.06f), ProcTex.Mat(ProcTex.Get("concrete"), new Color(0.55f, 0.53f, 0.5f), new Vector2(width, 0.36f) / 0.8f, 0.8f, 1f)));
        if (floors >= 3)
        {
            var tank = GameKit.Cyl(root, "WaterTank", new Vector3(fx - 3.2f, height + 0.75f, zc + width * 0.22f), 0.7f, 1.5f, GameKit.Lit(new Color(0.06f, 0.06f, 0.07f), 0.35f));
            S(tank);
            S(Part(root, "StairHead", new Vector3(fx - 5.5f, height + 1.3f, zc - width * 0.2f), new Vector3(2.6f, 2.6f, 2.8f), wallMat));
        }
        // a band between the shop and the flats
        S(Part(root, "Band", new Vector3(fx + 0.06f, groundH + 0.1f, zc), new Vector3(0.12f, 0.2f, width), ProcTex.Mat(ProcTex.Get("concrete"), plaster * 0.85f, new Vector2(width, 0.2f), 0.8f, 1f)));

        // ---- ground floor
        var dark = GameKit.Lit(new Color(0.03f, 0.03f, 0.035f), 0.4f);
        if (ground == "shop" || ground == "corner")
        {
            int bays = ground == "shop" ? 3 : 2;
            float bayW = (width - 0.6f) / bays;
            for (int i = 0; i < bays; i++)
            {
                float bz = z0 + 0.3f + (i + 0.5f) * bayW;
                // a rolling steel shutter, pulled down and padlocked for the night, in a plastered opening
                var sh = Part(root, "Shutter", new Vector3(fx + 0.02f, 1.4f, bz), new Vector3(0.05f, 2.8f, bayW - 0.25f), GameKit.Lit(Color.white));
                GameKit.Surface(sh, "shutter", 1.2f, 1f, 1f, new Color(0.46f, 0.47f, 0.48f));
                S(sh);
                S(Part(root, "ShutterBox", new Vector3(fx + 0.12f, 2.95f, bz), new Vector3(0.24f, 0.3f, bayW - 0.15f), GameKit.Lit(new Color(0.3f, 0.31f, 0.32f), 0.4f, 0.5f)));
                S(Part(root, "ShutterLock", new Vector3(fx + 0.07f, 0.12f, bz), new Vector3(0.04f, 0.07f, 0.06f), GameKit.Lit(new Color(0.55f, 0.45f, 0.2f), 0.6f, 0.9f)));
                S(Part(root, "Pier", new Vector3(fx + 0.04f, 1.65f, bz - bayW * 0.5f), new Vector3(0.12f, 3.3f, 0.28f), wallMat));
            }
            S(Part(root, "Pier", new Vector3(fx + 0.04f, 1.65f, z1 - 0.3f), new Vector3(0.12f, 3.3f, 0.28f), wallMat));
            // the shop's painted fascia board, fastened to the band (English over Hindi)
            // (signs with ready bilingual lettering in Resources/Signs)
            string en = ground == "shop" ? "GUPTA GENERAL STORE" : "KRISHNA MEDICAL STORE";
            string hi = ground == "shop" ? "गुप्ता जनरल स्टोर" : "कृष्णा मेडिकल स्टोर";
            var board = Part(root, "Fascia", new Vector3(fx + 0.09f, 3.32f, zc), new Vector3(0.04f, 0.5f, width - 1.2f), GameKit.Lit(ground == "shop" ? new Color(0.12f, 0.24f, 0.36f) : new Color(0.1f, 0.34f, 0.2f), 0.35f));
            S(board);
            // painted on the board, 1.5 mm proud of its face, lit like the board (the market's sign paint)
            var sign = streetKit.Paint(root, en, hi, new Vector3(fx + 0.1115f, 3.32f, zc), Quaternion.Euler(0f, -90f, 0f), 0.17f, width - 1.6f, new Color(0.95f, 0.92f, 0.8f));
            if (sign != null && sign.GetComponent<TextMesh>() != null) GameKit.Solid(sign.GetComponent<TextMesh>());
        }
        else
        {
            // a recessed doorway up two steps, a closed wooden door, a bare bulb over it, and a shuttered shop beside
            float dz = z0 + width * 0.32f;
            S(Part(root, "DoorRecessBack", new Vector3(fx - 0.6f, 1.25f, dz), new Vector3(0.1f, 2.5f, 1.4f), dark));
            S(Part(root, "Door", new Vector3(fx - 0.53f, 1.2f, dz), new Vector3(0.06f, 2.2f, 1.1f), Wood(new Color(0.28f, 0.17f, 0.1f), 0.6f, 0.8f)));
            foreach (float s in new[] { -1f, 1f }) S(Part(root, "DoorJamb", new Vector3(fx - 0.25f, 1.25f, dz + s * 0.75f), new Vector3(0.6f, 2.5f, 0.14f), wallMat));
            S(Part(root, "DoorHead", new Vector3(fx - 0.25f, 2.6f, dz), new Vector3(0.6f, 0.2f, 1.64f), wallMat));
            for (int st = 0; st < 2; st++)
                S(Part(root, "Step", new Vector3(fx + 0.25f - st * 0.3f, 0.07f + st * 0.14f, dz), new Vector3(0.6f, 0.14f, 1.6f), ProcTex.Mat(ProcTex.Get("stone"), new Color(0.5f, 0.48f, 0.45f), new Vector2(1.6f, 0.6f), 0.7f, 1f)));
            GameKit.Sphere(root, "DoorBulb", new Vector3(fx + 0.15f, 2.85f, dz), 0.09f, GameKit.Glow(new Color(1f, 0.85f, 0.6f), new Color(1f, 0.75f, 0.45f), 4f));
            PL(new Vector3(fx + 0.5f, 2.7f, dz), new Color(1f, 0.74f, 0.45f), 1.4f, 4.5f);
            float sz = z0 + width * 0.72f;
            var sh = Part(root, "Shutter", new Vector3(fx + 0.02f, 1.4f, sz), new Vector3(0.05f, 2.8f, width * 0.42f), GameKit.Lit(Color.white));
            GameKit.Surface(sh, "shutter", 1.2f, 1f, 1f, new Color(0.4f, 0.42f, 0.42f));
            S(sh);
            S(Part(root, "ShutterBox", new Vector3(fx + 0.12f, 2.95f, sz), new Vector3(0.24f, 0.3f, width * 0.44f), GameKit.Lit(new Color(0.3f, 0.31f, 0.32f), 0.4f, 0.5f)));
        }

        // ---- the flats: recessed windows with sills and sunshades, some lit
        var rnd = new System.Random(Mathf.RoundToInt(z0 * 10f) + 77);
        int perFloor = Mathf.Max(2, Mathf.RoundToInt(width / 3.2f));
        for (int f = 1; f < floors; f++)
        {
            float wy = groundH + (f - 1) * floorH + 1.55f;
            for (int i = 0; i < perFloor; i++)
            {
                float wz = z0 + (i + 0.5f) * width / perFloor;
                bool lit = rnd.NextDouble() < 0.4;
                bool tube = lit && rnd.NextDouble() < 0.35;
                // the opening: a dark recess 15 cm deep, a wooden frame, a sill and a concrete sunshade (chajja) over it
                S(Part(root, "WindowRecess", new Vector3(fx + 0.003f, wy, wz), new Vector3(0.006f, 1.35f, 1.1f), dark));
                if (lit)
                {
                    var glow = tube ? new Color(0.85f, 0.93f, 1f) : new Color(1f, 0.72f, 0.4f);
                    S(Glow("WindowLit", new Vector3(fx + 0.008f, wy, wz), new Vector3(0.004f, 1.25f, 1.0f), glow, tube ? 0.55f : 0.42f));     // lit rooms, not white panels
                    // a curtain drawn across most of it
                    S(Part(root, "Curtain", new Vector3(fx + 0.012f, wy, wz - 0.18f), new Vector3(0.004f, 1.2f, 0.62f), GameKit.Lit(rnd.NextDouble() < 0.5 ? new Color(0.55f, 0.18f, 0.12f) : new Color(0.75f, 0.62f, 0.3f), 0.1f)));
                }
                // the frame stands proud of the opening, so the glass reads as set back into the wall
                foreach (float s in new[] { -1f, 1f }) S(Part(root, "WindowJamb", new Vector3(fx + 0.06f, wy, wz + s * 0.58f), new Vector3(0.12f, 1.45f, 0.07f), Wood(new Color(0.25f, 0.2f, 0.15f), 0.4f, 0.6f)));
                S(Part(root, "WindowHead", new Vector3(fx + 0.06f, wy + 0.71f, wz), new Vector3(0.12f, 0.07f, 1.23f), Wood(new Color(0.25f, 0.2f, 0.15f), 0.4f, 0.6f)));
                S(Part(root, "WindowMullion", new Vector3(fx + 0.025f, wy, wz), new Vector3(0.04f, 1.35f, 0.05f), Wood(new Color(0.25f, 0.2f, 0.15f), 0.4f, 0.6f)));
                S(Part(root, "WindowSill", new Vector3(fx + 0.1f, wy - 0.72f, wz), new Vector3(0.2f, 0.06f, 1.3f), ProcTex.Mat(ProcTex.Get("concrete"), new Color(0.58f, 0.56f, 0.52f), new Vector2(1.3f, 0.18f), 0.6f, 1f)));
                S(Part(root, "Chajja", new Vector3(fx + 0.25f, wy + 0.86f, wz), new Vector3(0.5f, 0.07f, 1.5f), ProcTex.Mat(ProcTex.Get("concrete"), new Color(0.55f, 0.53f, 0.5f), new Vector2(1.5f, 0.5f), 0.6f, 1f)));
            }
            // one balcony per floor on the wider blocks, with an iron railing (and a lit doorway behind it, sometimes)
            if (width > 9f && f == 1)
            {
                float bz = z0 + width * 0.5f;
                S(Part(root, "BalconySlab", new Vector3(fx + 0.5f, groundH + 0.12f, bz), new Vector3(1.0f, 0.12f, 2.4f), ProcTex.Mat(ProcTex.Get("concrete"), new Color(0.56f, 0.54f, 0.5f), new Vector2(2.4f, 1f), 0.6f, 1f)));
                var iron = GameKit.Lit(new Color(0.06f, 0.06f, 0.07f), 0.4f, 0.6f);
                S(Part(root, "BalconyRail", new Vector3(fx + 0.98f, groundH + 1.08f, bz), new Vector3(0.04f, 0.04f, 2.4f), iron));
                for (int k = 0; k <= 12; k++) S(Part(root, "BalconyBar", new Vector3(fx + 0.98f, groundH + 0.62f, bz - 1.18f + k * 0.196f), new Vector3(0.018f, 0.92f, 0.018f), iron));
                foreach (float s in new[] { -1f, 1f }) S(Part(root, "BalconyRailSide", new Vector3(fx + 0.5f, groundH + 1.08f, bz + s * 1.18f), new Vector3(1.0f, 0.04f, 0.04f), iron));
            }
        }
    }

    /// <summary>
    /// A white Ambassador-style saloon parked at the kerb: rounded body, glasshouse, chrome bumpers, black tyres resting
    /// on the road (wheel centres at the tyre radius above the ground). Local +Z is forward.
    /// </summary>
    void BuildParkedCar(Transform root, Vector3 groundPos, float yaw)
    {
        var car = new GameObject("ParkedCar").transform;
        car.SetParent(root, false);
        car.position = groundPos; car.rotation = Quaternion.Euler(0f, yaw, 0f);
        var paint = GameKit.Lit(new Color(0.82f, 0.81f, 0.76f), 0.72f, 0.15f);
        var chrome = GameKit.Lit(new Color(0.72f, 0.72f, 0.74f), 0.9f, 1f);
        var black = GameKit.Lit(new Color(0.03f, 0.03f, 0.035f), 0.3f);
        var glass = GameKit.Lit(new Color(0.03f, 0.04f, 0.05f), 0.93f, 0.2f);
        const float r = 0.32f;
        VehicleKit.Body(car, "CarBody", new[] { new Sec(2.15f, 0.62f, 0.74f, 0.2f, 5f), new Sec(1.9f, 0.66f, 0.83f, 0.26f, 6f), new Sec(-1.9f, 0.66f, 0.83f, 0.27f, 6f), new Sec(-2.15f, 0.64f, 0.76f, 0.22f, 5f) }, paint, "amb_body");
        VehicleKit.Body(car, "CarBonnet", new[] { new Sec(2.1f, 0.84f, 0.72f, 0.05f, 4f), new Sec(1.5f, 0.9f, 0.8f, 0.07f, 4f), new Sec(0.85f, 0.92f, 0.8f, 0.07f, 4f) }, paint, "amb_bonnet");
        VehicleKit.Body(car, "CarCabin", new[] { new Sec(0.8f, 1.08f, 0.76f, 0.2f, 4f), new Sec(0.45f, 1.28f, 0.72f, 0.28f, 4f), new Sec(-0.85f, 1.28f, 0.72f, 0.28f, 4f), new Sec(-1.25f, 1.1f, 0.76f, 0.2f, 4f) }, paint, "amb_cabin");
        VehicleKit.Body(car, "CarBoot", new[] { new Sec(-1.2f, 0.92f, 0.8f, 0.07f, 4f), new Sec(-1.85f, 0.9f, 0.8f, 0.07f, 4f), new Sec(-2.12f, 0.84f, 0.74f, 0.05f, 4f) }, paint, "amb_boot");
        var ws = GameKit.Box(car, "Windscreen", new Vector3(0f, 1.2f, 0.66f), new Vector3(1.3f, 0.42f, 0.03f), glass); ws.transform.localRotation = Quaternion.Euler(-32f, 0f, 0f);
        var rw = GameKit.Box(car, "RearScreen", new Vector3(0f, 1.2f, -1.08f), new Vector3(1.25f, 0.38f, 0.03f), glass); rw.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
        foreach (float x in new[] { -0.735f, 0.735f })
        {
            GameKit.Box(car, "SideGlass", new Vector3(x, 1.22f, -0.2f), new Vector3(0.02f, 0.34f, 1.4f), glass);
            GameKit.Box(car, "Rubbing", new Vector3(x * 1.13f, 0.72f, 0f), new Vector3(0.02f, 0.04f, 3.6f), chrome);
            foreach (float z in new[] { 1.38f, -1.32f }) VehicleKit.Wheel(car, new Vector3(x * 1.0f, r, z), r, 0.19f);
            GameKit.Sphere(car, "Headlamp", new Vector3(x * 0.72f, 0.82f, 2.17f), 0.19f, GameKit.Lit(new Color(0.75f, 0.75f, 0.7f), 0.9f));
            GameKit.Box(car, "TailLamp", new Vector3(x * 0.78f, 0.82f, -2.17f), new Vector3(0.14f, 0.12f, 0.03f), GameKit.Lit(new Color(0.35f, 0.02f, 0.02f), 0.7f));
            GameKit.Box(car, "Mirror", new Vector3(x * 1.08f, 1.04f, 0.72f), new Vector3(0.08f, 0.08f, 0.04f), chrome);
        }
        GameKit.Box(car, "Grille", new Vector3(0f, 0.74f, 2.17f), new Vector3(0.82f, 0.24f, 0.03f), chrome);
        GameKit.Box(car, "BumperF", new Vector3(0f, 0.46f, 2.24f), new Vector3(1.66f, 0.1f, 0.08f), chrome);
        GameKit.Box(car, "BumperR", new Vector3(0f, 0.46f, -2.24f), new Vector3(1.66f, 0.1f, 0.08f), chrome);
        GameKit.Box(car, "Underside", new Vector3(0f, 0.3f, 0f), new Vector3(1.4f, 0.12f, 3.8f), black);
        foreach (var rr in car.GetComponentsInChildren<Renderer>()) rr.shadowCastingMode = ShadowCastingMode.On;
        var bc = car.gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, 0.75f, 0f); bc.size = new Vector3(1.7f, 1.5f, 4.5f);
    }

    /// <summary>Scales a scanned prop so its long side is <paramref name="length"/>, turns that side along Z and rests it on the ground.</summary>
    static void FitOnGround(Transform t, float length, float groundY)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        float longSide = Mathf.Max(b.size.x, b.size.z);
        if (longSide < 0.01f) return;
        if (b.size.x > b.size.z) t.rotation = Quaternion.Euler(0f, 90f, 0f) * t.rotation;
        t.localScale *= length / longSide;
        b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        t.position += new Vector3(0f, groundY - b.min.y, 0f);
    }

    /// <summary>Someone walks along the far pavement every so often (one character, far from the action, cheap).</summary>
    IEnumerator Passerby()
    {
        yield return Wait(Random.Range(6f, 12f));
        CharacterNPC n = null;
        bool north = true;
        while (this != null)
        {
            if (n == null)
            {
                n = CharacterNPC.Create(transform, new Vector3(-14.5f, -0.0f, north ? -16f : 15f), NpcStyle.Crowd(4101), "Passerby");
                n.transform.rotation = Quaternion.Euler(0f, north ? 0f : 180f, 0f);
            }
            yield return n.WalkTo(new Vector3(-14.55f, 0f, north ? 15.5f : -16f), 1.25f, 40f);
            if (n != null) { Destroy(n.gameObject); n = null; }
            north = !north;
            yield return Wait(Random.Range(18f, 30f));
        }
    }
}
