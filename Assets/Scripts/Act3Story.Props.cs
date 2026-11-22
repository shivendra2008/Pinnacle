using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Act III's near-camera furniture, rebuilt from its big shapes and contact points out: a front desk with a modesty
/// panel and a drawer pedestal (drawers set in their frame with a shadow gap and pulls), a real CRT on it (tapered
/// back, bezel, curved dark glass with green text, keyboard and mouse); the librarian's desk with its locked drawer
/// recessed on runners; bookcases with sides, a back, a plinth and a cornice, filled with books of believable sizes
/// (spines of 2-6 cm, 20-32 cm tall, a few leaning, gaps); the record room's steel shelving standing on feet with
/// cross-bracing; a wire-mesh cage door with a hasp; the calendar as a hanging paper calendar; and Priya's envelope
/// on a brass plate beside the Pandit. Materials: aged wood, dull metal, paper, worn stone.
/// </summary>
public partial class Act3Story
{
    Transform envelope;

    static Material WoodMat(Color tint, float tileM = 0.6f, float smooth = 0.85f) { return ProcTex.Mat(ProcTex.Get("wood"), tint, Vector2.one / tileM, smooth, 1f); }

    GameObject P(Transform parent, string n, Vector3 localPos, Vector3 size, Material m, bool shadows = true, bool col = false)
    {
        var go = GameKit.Box(parent, n, localPos, size, m, col);
        go.GetComponent<Renderer>().shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        return go;
    }

    Transform Root(string name, Vector3 worldPos, float yaw = 0f)
    {
        var t = new GameObject(name).transform;
        t.SetParent(transform, false);
        t.position = worldPos; t.rotation = Quaternion.Euler(0f, yaw, 0f);
        return t;
    }

    // ===================================================================== the front desk and its CRT
    /// <summary>The enquiry desk by the door (its front faces the hall, -z), with the computer whose password opens Priya's files.</summary>
    Transform BuildFrontDesk(Vector3 at)
    {
        var d = Root("FrontDeskSet", at);
        var teak = WoodMat(new Color(0.3f, 0.19f, 0.11f));
        var dark = WoodMat(new Color(0.2f, 0.12f, 0.07f));
        var brass = GameKit.Lit(new Color(0.7f, 0.55f, 0.25f), 0.55f, 0.85f);
        // the old collider volume is kept (2.6 x 0.9 x 0.9)
        var hit = GameKit.Box(d, "FrontDesk", new Vector3(0f, 0.45f, 0f), new Vector3(2.6f, 0.9f, 0.9f), GameKit.Lit(Color.black), true);
        hit.GetComponent<Renderer>().enabled = false;
        P(d, "DeskTop", new Vector3(0f, 0.885f, 0f), new Vector3(2.62f, 0.035f, 0.92f), teak);
        // the public side: a framed modesty panel down to a plinth
        P(d, "DeskModesty", new Vector3(0f, 0.47f, -0.43f), new Vector3(2.5f, 0.8f, 0.03f), dark);
        P(d, "DeskPlinth", new Vector3(0f, 0.04f, -0.41f), new Vector3(2.54f, 0.08f, 0.05f), GameKit.Lit(new Color(0.06f, 0.05f, 0.04f), 0.25f));
        for (int i = 0; i < 4; i++) P(d, "DeskStile", new Vector3(-1.2f + i * 0.8f, 0.47f, -0.45f), new Vector3(0.07f, 0.8f, 0.02f), teak);
        P(d, "DeskRail", new Vector3(0f, 0.84f, -0.45f), new Vector3(2.5f, 0.06f, 0.02f), teak);
        // ends, and the staff side's drawer pedestal (three drawers set into their frame, a shadow gap round each, brass pulls)
        foreach (float sx in new[] { -1.27f, 1.27f }) P(d, "DeskEnd", new Vector3(sx, 0.43f, 0f), new Vector3(0.04f, 0.86f, 0.88f), teak);
        P(d, "PedestalSide", new Vector3(0.62f, 0.43f, 0.02f), new Vector3(0.03f, 0.86f, 0.82f), teak);
        P(d, "PedestalRecess", new Vector3(0.95f, 0.45f, 0.42f), new Vector3(0.6f, 0.78f, 0.02f), GameKit.Lit(new Color(0.03f, 0.025f, 0.02f), 0.2f), false);
        for (int i = 0; i < 3; i++)
        {
            float y = 0.2f + i * 0.25f;
            P(d, "DrawerFront", new Vector3(0.95f, y, 0.435f), new Vector3(0.56f, 0.22f, 0.02f), teak);
            P(d, "DrawerPull", new Vector3(0.95f, y + 0.04f, 0.452f), new Vector3(0.12f, 0.018f, 0.014f), brass, false);
        }
        // the CRT: a beige case with a tapered back, a bezel round a curved dark screen, on a swivel foot
        var beige = GameKit.Lit(new Color(0.72f, 0.69f, 0.6f), 0.35f);
        var m = Root("CRT", d.TransformPoint(new Vector3(0f, 0.9f, -0.05f)), 180f);
        m.SetParent(d, true);
        P(m, "CRTFoot", new Vector3(0f, 0.012f, 0.0f), new Vector3(0.26f, 0.024f, 0.24f), beige);
        P(m, "CRTNeck", new Vector3(0f, 0.04f, 0.0f), new Vector3(0.1f, 0.04f, 0.1f), beige);
        VehicleKit.Body(m, "CRTCase", new[] { new Sec(0.2f, 0.25f, 0.215f, 0.19f, 6f), new Sec(0.12f, 0.25f, 0.215f, 0.19f, 6f), new Sec(-0.05f, 0.245f, 0.18f, 0.16f, 4f), new Sec(-0.2f, 0.235f, 0.12f, 0.11f, 3f) }, beige, "crt_case");
        P(m, "CRTBezel", new Vector3(0f, 0.25f, 0.205f), new Vector3(0.43f, 0.38f, 0.012f), beige);
        // the glass, set into the bezel (a CRT's screen is a dark, slightly glossy pane with green text glowing in it)
        var screen = P(m, "CRTScreen", new Vector3(0f, 0.255f, 0.209f), new Vector3(0.36f, 0.29f, 0.01f), ScreenMat(), false);
        screen.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);        // the text reads from the front
        var target = new GameObject("ComputerTarget").transform;
        target.SetParent(m, false); target.localPosition = new Vector3(0f, 0.25f, 0.2f);
        P(m, "CRTLed", new Vector3(0.16f, 0.085f, 0.212f), new Vector3(0.012f, 0.006f, 0.004f), GameKit.Glow(new Color(0.1f, 0.4f, 0.1f), new Color(0.3f, 1f, 0.3f), 2f), false);
        // keyboard with key rows, a mouse on a pad, and the tower under the desk
        var kb = Root("KeyboardSet", d.TransformPoint(new Vector3(0f, 0.905f, -0.32f)), 180f);
        kb.SetParent(d, true);
        P(kb, "Keyboard", new Vector3(0f, 0.012f, 0f), new Vector3(0.46f, 0.024f, 0.16f), beige);
        var keyMat = GameKit.Lit(new Color(0.6f, 0.58f, 0.52f), 0.3f);
        for (int r = 0; r < 4; r++) P(kb, "KeyRow", new Vector3(-0.02f, 0.028f, -0.05f + r * 0.032f), new Vector3(0.38f, 0.01f, 0.024f), keyMat, false);
        P(kb, "SpaceBar", new Vector3(-0.03f, 0.028f, -0.075f), new Vector3(0.16f, 0.01f, 0.02f), keyMat, false);
        P(d, "MousePad", new Vector3(-0.42f, 0.905f, -0.28f), new Vector3(0.22f, 0.004f, 0.18f), GameKit.Lit(new Color(0.12f, 0.16f, 0.3f), 0.2f), false);
        P(d, "Mouse", new Vector3(-0.42f, 0.92f, -0.28f), new Vector3(0.06f, 0.028f, 0.1f), beige, false);
        P(d, "Tower", new Vector3(-0.85f, 0.23f, 0.2f), new Vector3(0.19f, 0.42f, 0.44f), beige);
        P(d, "TowerFace", new Vector3(-0.85f, 0.32f, -0.025f), new Vector3(0.16f, 0.06f, 0.006f), GameKit.Lit(new Color(0.25f, 0.24f, 0.22f), 0.4f), false);
        // a register and a bell for the public
        P(d, "Register", new Vector3(0.75f, 0.925f, -0.15f), new Vector3(0.34f, 0.04f, 0.26f), GameKit.Lit(new Color(0.32f, 0.08f, 0.06f), 0.3f), false);
        GameKit.Cyl(d, "CounterBell", new Vector3(0.45f, 0.93f, -0.3f), 0.04f, 0.05f, brass);
        // the enquiry sign stands on the desk, facing the hall, depth-tested
        P(d, "EnquiryStand", new Vector3(0f, 1.0f, -0.42f), new Vector3(0.42f, 0.12f, 0.02f), dark, false);
        GameKit.Solid(GameKit.Text(d, "ENQUIRY", new Vector3(0f, 1.0f, -0.432f), Quaternion.identity, 0.06f, new Color(0.9f, 0.82f, 0.6f)));
        return target;
    }

    static Material screenMat;
    /// <summary>The CRT's glass: dark green-grey, faintly lit, with lines of text (made once).</summary>
    static Material ScreenMat()
    {
        if (screenMat != null) return screenMat;
        const int W = 128, H = 96;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "CRTText", wrapMode = TextureWrapMode.Clamp };
        var r = new System.Random(42);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W, v = y / (float)H;
                float vign = 1f - Mathf.Clamp01(Mathf.Pow(Mathf.Max(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f)) * 2f, 4f));
                int line = (H - 1 - y) / 6;
                bool text = line > 1 && line < 13 && (y % 6) < 4 && x > 8 && x < 8 + (int)(W * 0.75f * (0.45f + 0.55f * ((line * 7919) % 13) / 13f)) && r.NextDouble() < 0.72;
                float scan = (y % 2 == 0) ? 1f : 0.82f;
                Color c = new Color(0.03f, 0.08f, 0.05f) * vign + (text ? new Color(0.25f, 0.85f, 0.42f) * 0.9f : Color.clear);
                tex.SetPixel(x, y, c * scan);
            }
        tex.Apply();
        var m = GameKit.Glow(Color.black, Color.white, 1.0f);
        m = new Material(m) { name = "CRTScreen" };
        m.SetTexture("_EmissionMap", tex);
        m.SetColor("_EmissionColor", Color.white * 1.15f);
        m.SetColor("_BaseColor", new Color(0.02f, 0.03f, 0.03f));
        m.SetFloat("_Smoothness", 0.92f);
        screenMat = m;
        return m;
    }

    // ===================================================================== the librarian's desk
    /// <summary>The desk whose drawer holds the cage key: a top over a frame, the drawer set back in its opening on two runners,
    /// a brass escutcheon round the combination lock, and a pedestal of drawers.</summary>
    Transform BuildLibrarianDesk(Vector3 at)
    {
        var d = Root("LibrarianDeskSet", at);
        var teak = WoodMat(new Color(0.38f, 0.23f, 0.13f));
        var darker = WoodMat(new Color(0.24f, 0.14f, 0.08f));
        var brass = GameKit.Lit(new Color(0.8f, 0.62f, 0.26f), 0.6f, 0.9f);
        var hit = GameKit.Box(d, "LibrarianDesk", new Vector3(0f, 0.42f, 0f), new Vector3(2f, 0.84f, 0.9f), GameKit.Lit(Color.black), true);
        hit.GetComponent<Renderer>().enabled = false;
        P(d, "DeskTop", new Vector3(0f, 0.855f, 0f), new Vector3(2.1f, 0.03f, 1.0f), teak);
        P(d, "DeskApron", new Vector3(0f, 0.74f, -0.43f), new Vector3(1.96f, 0.2f, 0.03f), teak);
        foreach (float sx in new[] { -0.97f, 0.97f })
            foreach (float sz in new[] { -0.42f, 0.42f })
                P(d, "DeskLeg", new Vector3(sx, 0.38f, sz), new Vector3(0.06f, 0.76f, 0.06f), darker);
        // the drawer: an opening in the apron, the front set 6 mm back with a shadow gap, sitting on two runners
        P(d, "DrawerOpening", new Vector3(0f, 0.74f, -0.42f), new Vector3(0.84f, 0.22f, 0.025f), GameKit.Lit(new Color(0.03f, 0.02f, 0.015f), 0.2f), false);
        P(d, "Drawer", new Vector3(0f, 0.74f, -0.432f), new Vector3(0.8f, 0.19f, 0.02f), teak);
        foreach (float rx in new[] { -0.38f, 0.38f }) P(d, "DrawerRunner", new Vector3(rx, 0.63f, -0.1f), new Vector3(0.025f, 0.02f, 0.66f), darker, false);
        P(d, "DrawerEscutcheon", new Vector3(0f, 0.74f, -0.444f), new Vector3(0.1f, 0.09f, 0.004f), brass, false);
        P(d, "DrawerLock", new Vector3(0f, 0.74f, -0.452f), new Vector3(0.07f, 0.05f, 0.014f), GameKit.Lit(new Color(0.55f, 0.45f, 0.25f), 0.65f, 0.95f), false);
        for (int k = 0; k < 4; k++) P(d, "LockDial", new Vector3(-0.024f + k * 0.016f, 0.74f, -0.461f), new Vector3(0.011f, 0.03f, 0.006f), GameKit.Lit(new Color(0.2f, 0.18f, 0.15f), 0.5f, 0.6f), false);
        // a pedestal of drawers under the right end
        P(d, "Pedestal", new Vector3(0.62f, 0.35f, 0f), new Vector3(0.62f, 0.62f, 0.86f), darker);
        for (int i = 0; i < 2; i++)
        {
            P(d, "PedestalDrawer", new Vector3(0.62f, 0.2f + i * 0.29f, -0.436f), new Vector3(0.56f, 0.25f, 0.016f), teak);
            P(d, "PedestalPull", new Vector3(0.62f, 0.25f + i * 0.29f, -0.448f), new Vector3(0.1f, 0.016f, 0.012f), brass, false);
        }
        // on top: the blotter, the ledger, the brass nameplate (depth-tested lettering)
        P(d, "Blotter", new Vector3(-0.1f, 0.874f, -0.05f), new Vector3(0.7f, 0.008f, 0.45f), GameKit.Lit(new Color(0.12f, 0.25f, 0.15f), 0.1f), false);
        P(d, "Ledger", new Vector3(-0.65f, 0.89f, 0.05f), new Vector3(0.32f, 0.04f, 0.24f), GameKit.Lit(new Color(0.4f, 0.1f, 0.08f), 0.2f), false);
        P(d, "NamePlate", new Vector3(0f, 0.9f, -0.42f), new Vector3(0.5f, 0.08f, 0.025f), brass, false);
        GameKit.Solid(GameKit.Text(d, "LIBRARIAN", new Vector3(0f, 0.9f, -0.4335f), Quaternion.identity, 0.042f, new Color(0.15f, 0.1f, 0.05f)));
        return hit.transform;            // the drawer spot aims where it always did (the desk's own volume)
    }

    // ===================================================================== bookcases and books
    static Material[] bookMats;
    static Mesh cube;

    /// <summary>
    /// A double-sided library bookcase (the old one was a solid slab with 56 cm "books"): two sides, a spine in the middle,
    /// five shelves, a plinth set back as a kick, a cornice; each side filled with books of real sizes in a few cloth
    /// colours, combined into one mesh per colour (one draw each).
    /// </summary>
    void BuildBookcase(Vector3 centre, float width, float height, float depth, System.Random rnd)
    {
        var bc = Root("Bookcase", centre);
        var wood = WoodMat(new Color(0.3f, 0.19f, 0.11f), 0.7f, 0.8f);
        var hit = GameKit.Box(bc, "Shelf", new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), GameKit.Lit(Color.black), true);
        hit.GetComponent<Renderer>().enabled = false;
        float t = 0.03f;
        foreach (float sx in new[] { -1f, 1f }) P(bc, "CaseSide", new Vector3(sx * (width * 0.5f - t * 0.5f), height * 0.5f, 0f), new Vector3(t, height, depth), wood);
        P(bc, "CaseSpine", new Vector3(0f, height * 0.5f, 0f), new Vector3(width - 2f * t, height - 0.12f, 0.015f), wood);
        P(bc, "CasePlinth", new Vector3(0f, 0.05f, 0f), new Vector3(width - 2f * t, 0.1f, depth - 0.06f), GameKit.Lit(new Color(0.08f, 0.06f, 0.04f), 0.2f));
        P(bc, "CaseCornice", new Vector3(0f, height + 0.025f, 0f), new Vector3(width + 0.06f, 0.05f, depth + 0.06f), wood);
        int shelves = 5;
        float pitch = (height - 0.16f) / shelves;
        if (cube == null) { var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube); cube = tmp.GetComponent<MeshFilter>().sharedMesh; Destroy(tmp); }
        if (bookMats == null)
        {
            Color[] cloth = { new Color(0.42f, 0.1f, 0.08f), new Color(0.12f, 0.2f, 0.32f), new Color(0.16f, 0.26f, 0.14f), new Color(0.5f, 0.38f, 0.22f), new Color(0.24f, 0.2f, 0.17f), new Color(0.62f, 0.55f, 0.42f) };
            bookMats = new Material[cloth.Length];
            for (int i = 0; i < cloth.Length; i++) bookMats[i] = GameKit.Lit(cloth[i], 0.18f + (i % 2) * 0.12f);
        }
        var lists = new List<CombineInstance>[bookMats.Length];
        for (int i = 0; i < lists.Length; i++) lists[i] = new List<CombineInstance>();
        for (int s = 0; s <= shelves; s++)
        {
            float sy = 0.1f + s * pitch;
            P(bc, "ShelfBoard", new Vector3(0f, sy, 0f), new Vector3(width - 2f * t, 0.022f, depth - 0.01f), wood);
            if (s == shelves) break;
            foreach (float face in new[] { -1f, 1f })
            {
                float x = -width * 0.5f + t + 0.01f;
                float xEnd = width * 0.5f - t - 0.01f;
                while (x < xEnd - 0.03f)
                {
                    if (rnd.NextDouble() < 0.05) { x += 0.06f + (float)rnd.NextDouble() * 0.12f; continue; }      // a gap where books are out
                    float thick = 0.02f + (float)rnd.NextDouble() * 0.04f;
                    float h = Mathf.Min(pitch - 0.04f, 0.2f + (float)rnd.NextDouble() * 0.12f);
                    float dp = 0.15f + (float)rnd.NextDouble() * 0.07f;
                    if (x + thick > xEnd) break;
                    float lean = 0f;
                    if (rnd.NextDouble() < 0.04 && x + thick + 0.08f < xEnd) { lean = 14f + (float)rnd.NextDouble() * 10f; }
                    var pos = new Vector3(x + thick * 0.5f + (lean > 0f ? h * 0.18f : 0f), sy + 0.011f + h * 0.5f, face * (depth * 0.5f - dp * 0.5f - 0.012f));
                    var rot = Quaternion.Euler(0f, 0f, -lean);
                    lists[rnd.Next(bookMats.Length)].Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(pos, rot, new Vector3(thick, h, dp)) });
                    x += thick + (lean > 0f ? h * 0.36f : 0.0015f);
                }
            }
        }
        for (int i = 0; i < lists.Length; i++)
        {
            if (lists[i].Count == 0) continue;
            var mesh = new Mesh { name = "Books", indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(lists[i].ToArray(), true, true);
            var go = new GameObject("Books");
            go.transform.SetParent(bc, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = bookMats[i];
            mr.shadowCastingMode = ShadowCastingMode.Off;                  // the shelves' own shadow and SSAO ground them
        }
    }

    // ===================================================================== record room shelving
    /// <summary>Steel shelving bays: angle uprights on floor plates, X-bracing on the ends, shelves with a turned-down lip.</summary>
    void ShelvingSupports(Vector3 bayCentre, float depthZ)
    {
        var steel = GameKit.Lit(new Color(0.42f, 0.44f, 0.46f), 0.45f, 0.65f);
        foreach (float sx in new[] { -0.3f, 0.3f })
            foreach (float sz in new[] { -1f, 1f })
                GameKit.Box(transform, "ShelfFoot", bayCentre + new Vector3(sx, 0.006f, sz * depthZ), new Vector3(0.1f, 0.012f, 0.1f), steel).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        // the ends: two diagonal straps crossing between the uprights
        foreach (float sz in new[] { -1f, 1f })
        {
            for (int k = 0; k < 2; k++)
            {
                var brace = GameKit.Box(transform, "ShelfBrace", bayCentre + new Vector3(0f, 1.2f, sz * depthZ), new Vector3(0.018f, 2.35f, 0.004f), steel);
                brace.transform.localRotation = Quaternion.Euler(0f, 0f, k == 0 ? 14.3f : -14.3f);       // across the end, upright to upright
                brace.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }
    }

    /// <summary>The cage over Block C, Shelf 14: a welded wire mesh (10 cm grid) on the door's bars, a hasp and the padlock on it.</summary>
    void CageMesh(Transform doorPivot)
    {
        var wire = GameKit.Lit(new Color(0.16f, 0.16f, 0.17f), 0.45f, 0.7f);
        for (int i = 0; i <= 21; i++)
            GameKit.Box(doorPivot, "CageWireV", new Vector3(0.012f, 1.2f, 0.02f + i * 0.1f), new Vector3(0.004f, 2.3f, 0.004f), wire).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
        for (int i = 0; i <= 23; i++)
            GameKit.Box(doorPivot, "CageWireH", new Vector3(0.012f, 0.05f + i * 0.1f, 1.05f), new Vector3(0.004f, 0.004f, 2.1f), wire).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
        var steel = GameKit.Lit(new Color(0.4f, 0.4f, 0.4f), 0.5f, 0.8f);
        GameKit.Box(doorPivot, "Hasp", new Vector3(0.03f, 1.1f, 2.06f), new Vector3(0.012f, 0.05f, 0.14f), steel);
        foreach (float hy in new[] { 0.35f, 2.05f })
            GameKit.Cyl(doorPivot, "CageHinge", new Vector3(0f, hy, 0f), 0.018f, 0.12f, steel);
    }

    // ===================================================================== the calendar
    static Material calendarMat;
    /// <summary>A printed municipal calendar sheet (12 small months, three national days ringed in red ink), made once.</summary>
    static Material CalendarSheet()
    {
        if (calendarMat != null) return calendarMat;
        const int W = 256, H = 320;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "CalendarSheet", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
        var paper = new Color(0.86f, 0.82f, 0.68f);
        var r = new System.Random(15);
        var px = new Color[W * H];
        for (int i = 0; i < px.Length; i++) { float n = 0.96f + (float)r.NextDouble() * 0.05f; px[i] = paper * n; }
        // the twelve month blocks: grey day-grids
        for (int m = 0; m < 12; m++)
        {
            int bx = 14 + (m % 3) * 80, by = 22 + (3 - m / 3) * 64;
            for (int row = 0; row < 5; row++)
                for (int col = 0; col < 7; col++)
                {
                    int x0 = bx + col * 9, y0 = by + row * 9;
                    for (int y = y0; y < y0 + 4; y++) for (int x = x0; x < x0 + 5; x++) px[y * W + x] = new Color(0.36f, 0.34f, 0.3f);
                }
            // the month's name bar
            for (int y = by + 47; y < by + 52; y++) for (int x = bx; x < bx + 40; x++) px[y * W + x] = new Color(0.55f, 0.15f, 0.1f);
        }
        // three red rings: 26 January, 15 August, 2 October
        foreach (var c in new[] { new Vector2Int(14 + 0 * 80 + 4 * 9 + 2, 22 + 3 * 64 + 3 * 9 + 2), new Vector2Int(14 + 1 * 80 + 1 * 9 + 2, 22 + 1 * 64 + 2 * 9 + 2), new Vector2Int(14 + 0 * 80 + 1 * 9 + 2, 22 + 0 * 64 + 4 * 9 + 2) })
            for (int y = -9; y <= 9; y++)
                for (int x = -9; x <= 9; x++)
                {
                    float d = Mathf.Sqrt(x * x + y * y);
                    if (d > 6.4f && d < 8.4f && c.y + y >= 0 && c.y + y < H && c.x + x >= 0 && c.x + x < W) px[(c.y + y) * W + c.x + x] = new Color(0.72f, 0.06f, 0.05f);
                }
        tex.SetPixels(px); tex.Apply(true);
        var mat = GameKit.Lit(Color.white, 0.12f);
        mat = new Material(mat) { name = "CalendarSheet" };
        mat.SetTexture("_BaseMap", tex); mat.mainTexture = tex;
        calendarMat = mat;
        return mat;
    }

    /// <summary>The calendar on the record room's east wall, as paper on a nail: a board backing, a printed sheet with the
    /// three days ringed, the curled lower edge, and the printed and handwritten lines kept as lettering on top.</summary>
    void BuildCalendarSheet(Vector3 wallPoint)
    {
        var c = Root("CalendarSet", wallPoint, -90f);              // faces west, into the room
        var sheet = P(c, "CalendarPaper", new Vector3(0f, 0f, 0.003f), new Vector3(0.7f, 0.9f, 0.003f), CalendarSheet(), false);
        sheet.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);        // the printed face toward the room
        P(c, "CalendarCurl", new Vector3(0f, -0.455f, 0.012f), new Vector3(0.7f, 0.02f, 0.016f), GameKit.Lit(new Color(0.82f, 0.78f, 0.64f), 0.1f), false);
        P(c, "CalendarString", new Vector3(0f, 0.5f, 0.004f), new Vector3(0.006f, 0.12f, 0.004f), GameKit.Lit(new Color(0.3f, 0.25f, 0.2f), 0.2f), false);
    }

    // ===================================================================== Priya's envelope
    /// <summary>The sealed envelope the Pandit has kept, on a brass plate by the lamp; it goes when he hands it over.</summary>
    void BuildEnvelope(Vector3 at)
    {
        var brass = GameKit.Lit(new Color(0.78f, 0.58f, 0.22f), 0.75f, 1f);
        GameKit.Cyl(transform, "OfferingPlate", at + new Vector3(0f, 0.008f, 0f), 0.16f, 0.016f, brass);
        envelope = Root("PriyaEnvelope", at + new Vector3(0f, 0.02f, 0f), 18f);
        P(envelope, "Envelope", new Vector3(0f, 0.003f, 0f), new Vector3(0.22f, 0.006f, 0.11f), GameKit.Lit(new Color(0.8f, 0.72f, 0.55f), 0.15f), false);
        P(envelope, "EnvelopeFlap", new Vector3(0f, 0.0065f, 0.03f), new Vector3(0.22f, 0.001f, 0.05f), GameKit.Lit(new Color(0.74f, 0.66f, 0.5f), 0.15f), false);
        P(envelope, "EnvelopeSeal", new Vector3(0f, 0.008f, 0.012f), new Vector3(0.024f, 0.004f, 0.024f), GameKit.Lit(new Color(0.55f, 0.06f, 0.05f), 0.5f), false);
        P(envelope, "EnvelopeThread", new Vector3(0f, 0.007f, -0.01f), new Vector3(0.006f, 0.001f, 0.11f), GameKit.Lit(new Color(0.9f, 0.85f, 0.75f), 0.2f), false);
    }
}
