using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Act III - "The File" (9:00-10:30 PM).
/// 1. Old Chandnipur Library: the night guard lets Arjun in; the front-desk computer holds Priya's encrypted research
///    (password puzzle: NANI1987, from the diary line + the photo Kamala sent).
/// 2. Record Room (basement): Block C, Shelf 14 is caged. The key is in the librarian's desk upstairs, whose lock
///    opens with 1947 reversed (7491) - the basement calendar circles the national days. The 1942 folder is empty:
///    the sign-out sheet says "S. Reddy".
/// 3. Kali Temple: Pandit Harishankar's question (three answers, each reveals more), Priya's envelope and letter,
///    then the phone call + Kamala's text and the branch choice that decides Act IV.
/// </summary>
public class Act3Story : ActStoryBase
{
    protected override int ActNumber { get { return 3; } }
    protected override string ActTitle { get { return "ACT III  -  THE FILE"; } }
    protected override string ActSubtitle { get { return "Old Chandnipur Library   |   9:00 PM"; } }
    protected override LookPreset Look { get { return LookPreset.Apartment; } }

    static readonly Vector3 LibStart = new Vector3(-5f, 0.1f, -9.4f);
    static readonly Vector3 Base = new Vector3(60f, 0f, 0f);          // basement origin
    static readonly Vector3 Temple = new Vector3(-90f, 0f, 0f);       // temple origin

    CharacterNPC sharma, pandit;
    Transform computer, libDesk, stairsDown, stairsUp, cage, cageDoor, calendar, gate;
    bool usbDone, cageSeen, calendarSeen, keyFound, cageOpen, panditDone, deskTried;
    AudioClip libBed, baseBed, templeBed;
    float nextBell, nextRat;
    int area;   // 0 library, 1 basement, 2 temple

    // ===================================================================== WORLD
    protected override void BuildWorld()
    {
        BuildLibrary();
        BuildBasement();
        BuildTemple();
        libBed = MakeNoiseBed(0.025f, 0.035f, 0.2f);
        baseBed = MakeNoiseBed(0.05f, 0.03f, 0.45f);
        templeBed = MakeChant();
        ambience.clip = libBed; ambience.Play();
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential;
        SetSun(new Color(0.6f, 0.68f, 1f), 0.6f, new Vector3(28f, 200f, 0f));       // moonlight (comes from the moon's side of the sky)
        camComp.backgroundColor = new Color(0.03f, 0.03f, 0.07f);
        AreaLook(0);
        PriyaRoomPlayer.Surface = 0;
    }

    /// <summary>Ambient light, fog and moon strength for each location. The old single setting left all three far too dark.</summary>
    void AreaLook(int a)
    {
        if (a == 0)        // library: warm lamp-lit hall with cool moonlight from the windows
        {
            CinematicLook.Ambient(new Color(0.62f, 0.6f, 0.66f), new Color(0.5f, 0.44f, 0.38f), new Color(0.24f, 0.2f, 0.16f));
            RenderSettings.fogColor = new Color(0.13f, 0.12f, 0.14f); RenderSettings.fogDensity = 0.006f;
        }
        else if (a == 1)   // basement: grey, dusty, but readable
        {
            CinematicLook.Ambient(new Color(0.5f, 0.52f, 0.55f), new Color(0.4f, 0.39f, 0.37f), new Color(0.2f, 0.19f, 0.17f));
            RenderSettings.fogColor = new Color(0.14f, 0.14f, 0.13f); RenderSettings.fogDensity = 0.012f;
        }
        else               // temple: moonlit courtyard, fire-lit shrine
        {
            CinematicLook.Ambient(new Color(0.4f, 0.43f, 0.62f), new Color(0.36f, 0.29f, 0.28f), new Color(0.17f, 0.13f, 0.1f));
            RenderSettings.fogColor = new Color(0.12f, 0.08f, 0.09f); RenderSettings.fogDensity = 0.008f;
        }
    }

    protected override void Start()
    {
        base.Start();
        // Act III is all night interiors: lift exposure a little and ease the vignette so the corners do not fall to black.
        foreach (var v in GetComponentsInChildren<Volume>())
        {
            if (v.sharedProfile == null) continue;
            if (v.sharedProfile.TryGet(out ColorAdjustments ca)) ca.postExposure.value += 0.45f;
            if (v.sharedProfile.TryGet(out Vignette vg)) vg.intensity.value = 0.12f;
        }
    }

    Light Spot(Vector3 pos, Vector3 euler, Color c, float intensity, float range, float angle)
    {
        var go = new GameObject("SpotLight"); go.transform.SetParent(transform, false);
        go.transform.position = pos; go.transform.rotation = Quaternion.Euler(euler);
        var l = go.AddComponent<Light>();
        l.type = LightType.Spot; l.color = c; l.intensity = intensity; l.range = range;
        l.spotAngle = angle; l.innerSpotAngle = angle * 0.45f; l.shadows = LightShadows.None;
        return l;
    }

    void BuildLibrary()
    {
        var cream = new Color(0.85f, 0.78f, 0.62f);
        Room("Library", Vector3.zero, new Vector3(14f, 6.5f, 22f), cream, new Color(0.55f, 0.4f, 0.28f), "wood_floor", "plaster");
        var wood = new Color(0.32f, 0.2f, 0.12f);
        // colonnade and arched windows with moonlight
        for (int i = 0; i < 5; i++)
        {
            float z = -8f + i * 4f;
            foreach (float x in new[] { -6.4f, 6.4f })
            {
                B("Column", new Vector3(x, 3.25f, z), new Vector3(0.55f, 6.5f, 0.55f), new Color(0.9f, 0.85f, 0.72f), true, 0.3f);
                if (i < 4)
                {
                    float wx = x < 0 ? -6.95f : 6.95f;
                    Glow("ArchWindow", new Vector3(wx, 3.8f, z + 2f), new Vector3(0.05f, 2.6f, 1.4f), new Color(0.4f, 0.52f, 0.85f), 1.8f);
                    B("ArchTop", new Vector3(wx, 5.25f, z + 2f), new Vector3(0.08f, 0.3f, 1.6f), wood);
                    B("WinSill", new Vector3(wx + (x < 0 ? 0.12f : -0.12f), 2.45f, z + 2f), new Vector3(0.3f, 0.06f, 1.6f), new Color(0.9f, 0.85f, 0.72f), false, 0.3f);
                    for (int b = 0; b < 3; b++) B("WinBar", new Vector3(wx, 3.8f, z + 1.55f + b * 0.45f), new Vector3(0.06f, 2.6f, 0.04f), wood);
                    B("WinTransom", new Vector3(wx, 4.3f, z + 2f), new Vector3(0.06f, 0.04f, 1.4f), wood);
                    // moonlight falling through the window onto the floor
                    Spot(new Vector3(x < 0 ? -6.6f : 6.6f, 4.8f, z + 2f), new Vector3(42f, x < 0 ? 90f : -90f, 0f), new Color(0.55f, 0.66f, 1f), 4f, 10f, 55f);
                }
                // brass wall sconce on the inner face of each column
                float sx = x < 0 ? x + 0.32f : x - 0.32f;
                B("SconceArm", new Vector3(sx, 2.7f, z), new Vector3(0.1f, 0.05f, 0.05f), new Color(0.8f, 0.6f, 0.25f), false, 0.8f, 0.9f);
                Glow("SconceShade", new Vector3(sx + (x < 0 ? 0.06f : -0.06f), 2.82f, z), new Vector3(0.16f, 0.2f, 0.16f), new Color(1f, 0.8f, 0.5f), 2.5f);
                PL(new Vector3(sx + (x < 0 ? 0.3f : -0.3f), 2.8f, z), new Color(1f, 0.78f, 0.52f), 1.6f, 5.5f);
            }
        }
        // dark wood wainscot + chair rail on every wall, cornice at the ceiling
        foreach (var w in new[] { new Vector4(0, 10.97f, 14f, 0.06f), new Vector4(0, -10.97f, 14f, 0.06f), new Vector4(6.97f, 0, 0.06f, 22f), new Vector4(-6.97f, 0, 0.06f, 22f) })
        {
            var panel = B("Wainscot", new Vector3(w.x, 0.55f, w.y), new Vector3(w.z, 1.1f, w.w), new Color(0.3f, 0.19f, 0.11f), false, 0.4f);
            GameKit.Surface(panel, "wood", 1.2f, 1f, 1f, new Color(0.3f, 0.19f, 0.11f));
            B("ChairRail", new Vector3(w.x, 1.12f, w.y), new Vector3(w.z + 0.04f, 0.06f, w.w + 0.04f), new Color(0.22f, 0.13f, 0.08f), false, 0.5f);
            B("Cornice", new Vector3(w.x, 6.4f, w.y), new Vector3(w.z + 0.1f, 0.2f, w.w + 0.1f), new Color(0.92f, 0.88f, 0.76f), false, 0.3f);
        }
        // long red runner down the central aisle
        var runner = B("Runner", new Vector3(-0.15f, 0.006f, 0), new Vector3(1.0f, 0.012f, 19f), new Color(0.5f, 0.12f, 0.1f), false, 0.1f);
        GameKit.Surface(runner, "rug", 1.0f, 1f, 1f, new Color(0.55f, 0.14f, 0.11f));
        // bookshelves with books
        var rnd = new System.Random(3);
        foreach (float z in new[] { -2f, 1.5f, 5f })
            foreach (float x0 in new[] { -4.4f, 1.6f })
                for (int u = 0; u < 2; u++)
                {
                    float x = x0 + u * 2.5f;
                    B("Shelf", new Vector3(x, 1.4f, z), new Vector3(2.4f, 2.8f, 0.5f), wood, true, 0.2f);
                    for (int sh = 0; sh < 5; sh++)
                        foreach (float face in new[] { -0.26f, 0.26f })
                            for (int k = 0; k < 4; k++)
                            {
                                Color bc = Color.HSVToRGB((float)rnd.NextDouble(), 0.45f, 0.25f + (float)rnd.NextDouble() * 0.35f);
                                B("Books", new Vector3(x - 0.9f + k * 0.6f, 0.4f + sh * 0.52f, z + face), new Vector3(0.56f, 0.34f + (float)rnd.NextDouble() * 0.08f, 0.04f), bc, false, 0.15f);
                            }
                }
        // reading tables with green banker's lamps
        foreach (float x in new[] { -3f, 3f })
            for (int i = 0; i < 2; i++)
            {
                float z = -6.5f + i * 3.5f;
                B("ReadingTable", new Vector3(x, 0.75f, z), new Vector3(2.4f, 0.06f, 1.1f), wood, true, 0.5f);
                foreach (float lx in new[] { -1.1f, 1.1f }) foreach (float lz in new[] { -0.5f, 0.5f })
                        B("TableLeg", new Vector3(x + lx, 0.37f, z + lz), new Vector3(0.06f, 0.75f, 0.06f), wood);
                // banker's lamp: brass base + stem + green glass shade
                B("LampBase", new Vector3(x, 0.79f, z), new Vector3(0.18f, 0.03f, 0.12f), new Color(0.8f, 0.6f, 0.25f), false, 0.8f, 0.9f);
                B("LampStem", new Vector3(x, 0.88f, z), new Vector3(0.025f, 0.16f, 0.025f), new Color(0.8f, 0.6f, 0.25f), false, 0.8f, 0.9f);
                Glow("BankerLamp", new Vector3(x, 0.98f, z), new Vector3(0.4f, 0.1f, 0.16f), new Color(0.2f, 0.75f, 0.35f), 1.8f);
                PL(new Vector3(x, 1.25f, z), new Color(1f, 0.85f, 0.6f), 2.4f, 5f);
                // a few books and chairs
                B("OpenBook", new Vector3(x - 0.6f, 0.79f, z + 0.1f), new Vector3(0.36f, 0.02f, 0.26f), new Color(0.92f, 0.88f, 0.78f), false, 0.1f);
                B("BookStack", new Vector3(x + 0.7f, 0.83f, z - 0.2f), new Vector3(0.24f, 0.1f, 0.18f), new Color(0.45f, 0.18f, 0.12f), false, 0.2f);
                foreach (float cz in new[] { -0.85f, 0.85f })
                    for (int ci = 0; ci < 2; ci++)
                    {
                        if (i == 1 && cz > 0) continue;   // the bookshelf row is right behind this side
                        float cx = x - 0.6f + ci * 1.2f;
                        B("ChairSeat", new Vector3(cx, 0.46f, z + cz), new Vector3(0.45f, 0.05f, 0.45f), wood, true, 0.35f);
                        B("ChairBack", new Vector3(cx, 0.75f, z + cz + Mathf.Sign(cz) * 0.21f), new Vector3(0.45f, 0.55f, 0.04f), wood, false, 0.35f);
                        foreach (float lx in new[] { -0.19f, 0.19f }) foreach (float lz in new[] { -0.19f, 0.19f })
                                B("ChairLeg", new Vector3(cx + lx, 0.22f, z + cz + lz), new Vector3(0.04f, 0.44f, 0.04f), wood);
                    }
            }
        // tube lights, one dying (brighter than before - the hall was unreadable)
        for (int i = 0; i < 4; i++)
        {
            B("TubeHousing", new Vector3(0, 6.36f, -8f + i * 5.3f), new Vector3(1.5f, 0.05f, 0.24f), new Color(0.85f, 0.85f, 0.82f), false, 0.4f);
            Glow("Tube", new Vector3(0, 6.3f, -8f + i * 5.3f), new Vector3(1.4f, 0.06f, 0.14f), new Color(0.9f, 0.96f, 1f), 2.6f);
            var l = PL(new Vector3(0, 5.6f, -8f + i * 5.3f), new Color(0.9f, 0.94f, 1f), 3.8f, 14f);
            if (i == 2) l.gameObject.AddComponent<FlickerLight>();
        }
        // warm fill so the shelves and far corners are not black holes
        foreach (float fx in new[] { -4.5f, 4.5f })
            foreach (float fz in new[] { -6f, 0f, 6f })
                PL(new Vector3(fx, 4.2f, fz), new Color(1f, 0.86f, 0.66f), 1.3f, 7f);
        // the Maharaja's portrait
        B("PortraitFrame", new Vector3(0, 4f, 10.9f), new Vector3(1.6f, 2f, 0.08f), new Color(0.55f, 0.4f, 0.15f), false, 0.6f, 0.6f);
        B("Portrait", new Vector3(0, 4f, 10.85f), new Vector3(1.35f, 1.75f, 0.04f), new Color(0.3f, 0.22f, 0.18f), false, 0.2f);
        Spot(new Vector3(0, 6.1f, 9.6f), new Vector3(55f, 0f, 0f), new Color(1f, 0.85f, 0.6f), 3f, 4f, 40f);   // picture light
        GameKit.Text(transform, "MAHARAJA OF CHANDNIPUR", new Vector3(0, 2.8f, 10.84f), Quaternion.identity, 0.08f, new Color(0.9f, 0.8f, 0.5f));

        // front desk + computer (CRT)
        B("FrontDesk", new Vector3(3.8f, 0.45f, -9.3f), new Vector3(2.6f, 0.9f, 0.9f), wood, true, 0.3f);
        var mon = B("Monitor", new Vector3(3.8f, 1.15f, -9.35f), new Vector3(0.5f, 0.45f, 0.45f), new Color(0.75f, 0.72f, 0.65f), false, 0.3f);
        Glow("Screen", new Vector3(3.8f, 1.17f, -9.58f), new Vector3(0.38f, 0.3f, 0.01f), new Color(0.3f, 0.7f, 0.45f), 1.4f);
        B("Keyboard", new Vector3(3.8f, 0.92f, -9.65f), new Vector3(0.45f, 0.03f, 0.16f), new Color(0.75f, 0.72f, 0.65f));
        PL(new Vector3(3.8f, 1.5f, -9.9f), new Color(0.5f, 0.9f, 0.6f), 0.8f, 3f);
        computer = mon.transform;
        GameKit.Text(transform, "ENQUIRY", new Vector3(3.8f, 1.05f, -9.76f), Quaternion.identity, 0.09f, new Color(0.9f, 0.85f, 0.7f));

        // librarian's desk with locked drawer
        var desk = B("LibrarianDesk", new Vector3(-4.2f, 0.42f, 8.5f), new Vector3(2f, 0.84f, 0.9f), new Color(0.4f, 0.25f, 0.14f), true, 0.35f);
        B("Drawer", new Vector3(-4.2f, 0.62f, 8.04f), new Vector3(0.8f, 0.22f, 0.04f), new Color(0.35f, 0.21f, 0.12f));
        B("DrawerLock", new Vector3(-4.2f, 0.62f, 8.01f), new Vector3(0.12f, 0.08f, 0.03f), new Color(0.85f, 0.65f, 0.25f), false, 0.8f, 0.9f);
        B("BrassLamp", new Vector3(-3.6f, 1.0f, 8.6f), new Vector3(0.12f, 0.3f, 0.12f), new Color(0.85f, 0.65f, 0.25f), false, 0.8f, 0.9f);
        Glow("BrassLampShade", new Vector3(-3.6f, 1.2f, 8.6f), new Vector3(0.3f, 0.16f, 0.3f), new Color(1f, 0.82f, 0.5f), 2.2f);
        PL(new Vector3(-3.7f, 1.45f, 8.3f), new Color(1f, 0.82f, 0.55f), 2.6f, 5f);
        B("DeskTop", new Vector3(-4.2f, 0.855f, 8.5f), new Vector3(2.1f, 0.03f, 1.0f), new Color(0.28f, 0.16f, 0.09f), false, 0.55f);
        B("Blotter", new Vector3(-4.3f, 0.875f, 8.45f), new Vector3(0.7f, 0.01f, 0.45f), new Color(0.12f, 0.25f, 0.15f), false, 0.1f);
        B("Ledger", new Vector3(-4.85f, 0.9f, 8.55f), new Vector3(0.32f, 0.05f, 0.24f), new Color(0.4f, 0.1f, 0.08f), false, 0.2f);
        B("NamePlate", new Vector3(-4.2f, 0.92f, 8.08f), new Vector3(0.5f, 0.1f, 0.03f), new Color(0.85f, 0.65f, 0.25f), false, 0.8f, 0.9f);
        GameKit.Text(transform, "LIBRARIAN", new Vector3(-4.2f, 0.92f, 8.06f), Quaternion.identity, 0.045f, new Color(0.15f, 0.1f, 0.05f));
        B("LibChairSeat", new Vector3(-4.2f, 0.48f, 9.4f), new Vector3(0.55f, 0.06f, 0.5f), new Color(0.35f, 0.21f, 0.12f), true, 0.35f);
        B("LibChairBack", new Vector3(-4.2f, 0.85f, 9.65f), new Vector3(0.55f, 0.7f, 0.05f), new Color(0.35f, 0.21f, 0.12f), false, 0.35f);
        // the clue: a yellowed note taped beside the drawer lock
        Glow("DeskNote", new Vector3(-3.55f, 0.6f, 8.035f), new Vector3(0.26f, 0.2f, 0.005f), new Color(0.95f, 0.88f, 0.55f), 0.35f);
        GameKit.Text(transform, "drawer code:", new Vector3(-3.55f, 0.66f, 8.028f), Quaternion.identity, 0.028f, new Color(0.15f, 0.12f, 0.35f));
        GameKit.Text(transform, "the year we became", new Vector3(-3.55f, 0.61f, 8.028f), Quaternion.identity, 0.024f, new Color(0.15f, 0.12f, 0.35f));
        GameKit.Text(transform, "free - BACKWARDS", new Vector3(-3.55f, 0.565f, 8.028f), Quaternion.identity, 0.024f, new Color(0.15f, 0.12f, 0.35f));
        GameKit.Text(transform, "(see calendar, Record Rm)", new Vector3(-3.55f, 0.525f, 8.028f), Quaternion.identity, 0.018f, new Color(0.6f, 0.1f, 0.1f));
        PL(new Vector3(-3.8f, 0.7f, 7.4f), new Color(1f, 0.9f, 0.7f), 0.8f, 1.8f);      // little kicker so the note can be read
        libDesk = desk.transform;

        // stairs to the record room
        B("BasementDoorFrame", new Vector3(5.6f, 1.15f, 10.85f), new Vector3(1.3f, 2.3f, 0.12f), wood);
        Glow("BasementDoorDark", new Vector3(5.6f, 1.1f, 10.8f), new Vector3(1.05f, 2.1f, 0.04f), new Color(0.05f, 0.05f, 0.06f), 0.2f);
        GameKit.Text(transform, "RECORD ROOM  (BASEMENT)", new Vector3(5.6f, 2.55f, 10.78f), Quaternion.identity, 0.1f, new Color(1f, 0.9f, 0.6f));
        PL(new Vector3(5.6f, 2.9f, 10.2f), new Color(1f, 0.8f, 0.5f), 1.4f, 4f);      // lamp over the basement door
        stairsDown = new GameObject("StairsDown").transform; stairsDown.SetParent(transform, false); stairsDown.position = new Vector3(5.6f, 1.1f, 10.7f);

        // side door + guard
        B("SideDoor", new Vector3(-6.95f, 1.1f, -9.6f), new Vector3(0.08f, 2.2f, 1.1f), new Color(0.35f, 0.22f, 0.13f));
        sharma = Npc(new Vector3(-4.3f, 0f, -8.0f), 220f, Cast.Sharma(), "Sharma-ji");
        Cy("Torch", new Vector3(-4.0f, 0.95f, -8.2f), 0.03f, 0.25f, new Color(0.2f, 0.2f, 0.2f));
    }

    void BuildBasement()
    {
        var grey = new Color(0.45f, 0.45f, 0.43f);
        Room("Basement", Base, new Vector3(16f, 3f, 14f), grey, new Color(0.35f, 0.34f, 0.32f), "stone", "facade");
        var metal = new Color(0.42f, 0.44f, 0.46f);
        string[] blocks = { "A", "B", "C", "D" };
        var rnd = new System.Random(5);
        for (int bi = 0; bi < 4; bi++)
        {
            float x = Base.x - 6f + bi * 3.4f;
            for (int u = 0; u < 4; u++)
            {
                float z = -3.5f + u * 2.2f;
                foreach (float sx in new[] { -0.3f, 0.3f })
                    foreach (float sz in new[] { -1f, 1f })
                        B("Upright", new Vector3(x + sx, 1.2f, z + sz), new Vector3(0.05f, 2.4f, 0.05f), metal, false, 0.4f, 0.6f);
                for (int s = 0; s < 5; s++)
                {
                    B("ShelfBoard", new Vector3(x, 0.2f + s * 0.52f, z), new Vector3(0.65f, 0.03f, 2.05f), metal, false, 0.4f, 0.6f);
                    for (int k = 0; k < 4; k++)
                        if (rnd.NextDouble() < 0.8)
                            B("FileBox", new Vector3(x, 0.36f + s * 0.52f, z - 0.75f + k * 0.5f), new Vector3(0.5f, 0.28f, 0.42f), new Color(0.6f, 0.48f, 0.32f) * (0.8f + 0.3f * (float)rnd.NextDouble()), false, 0.1f);
                }
                B("ShelfBlock", new Vector3(x, 1.2f, z), new Vector3(0.66f, 2.4f, 2.05f), metal * 0.1f, true).GetComponent<Renderer>().enabled = false;   // solid but invisible
                GameKit.Text(transform, (u + 11).ToString(), new Vector3(x + 0.34f, 2.3f, z), Quaternion.Euler(0, -90f, 0), 0.09f, new Color(0.95f, 0.9f, 0.7f));
            }
            GameKit.Text(transform, "BLOCK " + blocks[bi], new Vector3(x, 2.75f, -5.2f), Quaternion.identity, 0.16f, new Color(1f, 0.9f, 0.6f));
        }
        // Block C, Shelf 14: a wire cage on its open side
        float cx = Base.x - 6f + 2 * 3.4f + 0.36f, cz = -3.5f + 3 * 2.2f;
        cage = new GameObject("Cage").transform; cage.SetParent(transform, false); cage.position = new Vector3(cx + 0.05f, 1.2f, cz);
        var iron = new Color(0.12f, 0.12f, 0.13f);
        for (int i = 0; i < 2; i++) B("CageFrame", new Vector3(cx + 0.05f, 1.2f, cz - 1.05f + i * 2.1f), new Vector3(0.05f, 2.4f, 0.05f), iron, false, 0.4f, 0.6f);
        var doorPivot = new GameObject("CageDoor").transform; doorPivot.SetParent(transform, false);
        doorPivot.position = new Vector3(cx + 0.06f, 0, cz - 1.05f);
        cageDoor = doorPivot;
        for (int i = 0; i < 9; i++)
            GameKit.Box(doorPivot, "CageBar", new Vector3(0, 1.2f, 0.12f + i * 0.23f), new Vector3(0.02f, 2.3f, 0.02f), GameKit.Lit(iron, 0.4f, 0.6f));
        for (int i = 0; i < 4; i++)
            GameKit.Box(doorPivot, "CageRail", new Vector3(0, 0.2f + i * 0.7f, 1.05f), new Vector3(0.02f, 0.02f, 2.1f), GameKit.Lit(iron, 0.4f, 0.6f));
        GameKit.Box(doorPivot, "Padlock", new Vector3(0.04f, 1.1f, 2.0f), new Vector3(0.06f, 0.12f, 0.1f), GameKit.Lit(new Color(0.85f, 0.65f, 0.25f), 0.8f, 0.9f));
        GameKit.Text(transform, "1940 - 1945", new Vector3(cx + 0.1f, 2.05f, cz), Quaternion.Euler(0, -90f, 0), 0.08f, new Color(1f, 0.85f, 0.5f));
        // the wall calendar - lit by its own lamp so it reads as something worth looking at
        var calRot = Quaternion.Euler(0, 90f, 0);   // faces into the room (west)
        var ink = new Color(0.12f, 0.1f, 0.08f); var redInk = new Color(0.8f, 0.08f, 0.08f);
        B("CalendarBoard", new Vector3(Base.x + 7.95f, 1.6f, -3f), new Vector3(0.04f, 0.95f, 0.72f), new Color(0.95f, 0.92f, 0.8f), false, 0.1f);
        B("CalendarHeader", new Vector3(Base.x + 7.925f, 2.0f, -3f), new Vector3(0.01f, 0.14f, 0.72f), new Color(0.6f, 0.15f, 0.1f));
        B("CalendarNail", new Vector3(Base.x + 7.9f, 2.12f, -3f), new Vector3(0.03f, 0.03f, 0.03f), new Color(0.3f, 0.3f, 0.3f), false, 0.6f, 0.8f);
        GameKit.Text(transform, "NATIONAL DAYS", new Vector3(Base.x + 7.915f, 2.0f, -3f), calRot, 0.06f, new Color(1f, 0.95f, 0.85f));
        GameKit.Text(transform, "26 JAN    15 AUG    2 OCT", new Vector3(Base.x + 7.92f, 1.78f, -3f), calRot, 0.045f, ink);
        for (int i = 0; i < 3; i++) B("RedCircle", new Vector3(Base.x + 7.925f, 1.78f, -3.22f + i * 0.22f), new Vector3(0.005f, 0.1f, 0.17f), redInk * 0.9f);
        GameKit.Text(transform, "Independence - 15 . 8 . 1947", new Vector3(Base.x + 7.92f, 1.55f, -3f), calRot, 0.04f, ink);
        GameKit.Text(transform, "\"the year we became free -", new Vector3(Base.x + 7.92f, 1.36f, -3f), calRot, 0.035f, redInk);
        GameKit.Text(transform, "read it backwards\"", new Vector3(Base.x + 7.92f, 1.29f, -3f), calRot, 0.035f, redInk);
        Spot(new Vector3(Base.x + 6.9f, 2.85f, -3f), new Vector3(50f, 90f, 0f), new Color(1f, 0.86f, 0.62f), 5f, 4f, 45f);
        calendar = new GameObject("Calendar").transform; calendar.SetParent(transform, false); calendar.position = new Vector3(Base.x + 7.9f, 1.6f, -3f);
        // ceiling pipes, a clerk's table by the stairs
        for (int i = 0; i < 2; i++)
            B("Pipe", new Vector3(Base.x, 2.8f, -6.4f + i * 0.18f), new Vector3(15.8f, 0.09f, 0.09f), new Color(0.3f, 0.32f, 0.3f), false, 0.4f, 0.7f);
        B("ClerkTable", new Vector3(Base.x - 3f, 0.75f, -6.2f), new Vector3(1.6f, 0.05f, 0.7f), new Color(0.35f, 0.22f, 0.13f), true, 0.35f);
        foreach (float lx in new[] { -0.75f, 0.75f }) foreach (float lz in new[] { -0.3f, 0.3f })
                B("ClerkLeg", new Vector3(Base.x - 3f + lx, 0.37f, -6.2f + lz), new Vector3(0.05f, 0.75f, 0.05f), new Color(0.35f, 0.22f, 0.13f));
        B("FileStack", new Vector3(Base.x - 3.3f, 0.86f, -6.2f), new Vector3(0.35f, 0.18f, 0.28f), new Color(0.65f, 0.52f, 0.35f), false, 0.1f);
        Glow("ClerkLampShade", new Vector3(Base.x - 2.5f, 1.05f, -6.1f), new Vector3(0.22f, 0.14f, 0.22f), new Color(1f, 0.8f, 0.5f), 2f);
        PL(new Vector3(Base.x - 2.5f, 1.3f, -5.9f), new Color(1f, 0.8f, 0.55f), 1.8f, 5f);
        // stairs up
        B("StairsUpFrame", new Vector3(Base.x, 1.1f, -6.95f), new Vector3(1.3f, 2.2f, 0.1f), new Color(0.3f, 0.2f, 0.12f));
        stairsUp = new GameObject("StairsUp").transform; stairsUp.SetParent(transform, false); stairsUp.position = new Vector3(Base.x, 1.1f, -6.8f);
        // tubes: two working, one buzzing on and off, one dead
        for (int i = 0; i < 4; i++)
        {
            float tx = Base.x - 6f + i * 4f;
            B("TubeHousing", new Vector3(tx, 2.96f, 0), new Vector3(0.22f, 0.04f, 1.4f), new Color(0.8f, 0.8f, 0.78f), false, 0.4f);
            Glow("Tube", new Vector3(tx, 2.92f, 0), new Vector3(0.14f, 0.05f, 1.3f), new Color(0.85f, 0.95f, 1f), i == 3 ? 0.2f : 2.4f);
            if (i == 3) continue;
            var l = PL(new Vector3(tx, 2.6f, 0), new Color(0.88f, 0.94f, 1f), 3.2f, 11f);
            if (i == 1) l.gameObject.AddComponent<FlickerLight>();
        }
        // a bare bulb hanging over Block C, so the cage is easy to spot
        B("BulbCord", new Vector3(cx + 1.2f, 2.75f, cz), new Vector3(0.01f, 0.5f, 0.01f), new Color(0.1f, 0.1f, 0.1f));
        Glow("Bulb", new Vector3(cx + 1.2f, 2.45f, cz), new Vector3(0.09f, 0.12f, 0.09f), new Color(1f, 0.85f, 0.55f), 4f);
        PL(new Vector3(cx + 1.2f, 2.35f, cz), new Color(1f, 0.82f, 0.55f), 2.4f, 5.5f);
        PL(new Vector3(Base.x, 2.2f, -5.8f), new Color(1f, 0.85f, 0.6f), 1.4f, 5f);      // landing at the foot of the stairs
    }

    void BuildTemple()
    {
        var stone = new Color(0.55f, 0.5f, 0.45f);
        var f = B("TempleCourtyard", Temple + new Vector3(0, -0.05f, 0), new Vector3(30f, 0.1f, 30f), stone, true, 0.4f);
        GameKit.Surface(f, "stone", 1.4f, 1f, 1f, stone);
        foreach (var w in new[] { new Vector4(0, 15, 30, 0.6f), new Vector4(0, -15, 30, 0.6f), new Vector4(15, 0, 0.6f, 30), new Vector4(-15, 0, 0.6f, 30) })
            GameKit.Surface(B("CompoundWall", Temple + new Vector3(w.x, 1.25f, w.y), new Vector3(w.z, 2.5f, w.w), new Color(0.75f, 0.45f, 0.3f), true), "facade", 2.5f);
        // the sanctum: plinth, steps, shrine, shikhara
        var red = new Color(0.65f, 0.12f, 0.1f);
        B("Plinth", Temple + new Vector3(0, 0.6f, 9f), new Vector3(9f, 1.2f, 8f), stone * 0.9f, true, 0.3f);
        for (int i = 0; i < 5; i++) B("Step", Temple + new Vector3(0, 0.12f + i * 0.24f, 4.6f + i * 0.3f - 1.2f), new Vector3(4f, 0.24f, 0.4f), stone, true, 0.4f);
        B("Sanctum", Temple + new Vector3(0, 3.0f, 10f), new Vector3(5f, 3.6f, 5f), new Color(0.85f, 0.55f, 0.3f), true, 0.2f);
        GameKit.Cone(transform, "Shikhara", Temple + new Vector3(0, 4.8f, 10f), 3.4f, 7f, GameKit.Lit(new Color(0.9f, 0.5f, 0.2f), 0.2f), 8);
        Sp("Kalash", Temple + new Vector3(0, 12f, 10f), 0.6f, new Color(0.95f, 0.75f, 0.2f), 0.8f);
        Glow("SanctumDoor", Temple + new Vector3(0, 2.2f, 7.48f), new Vector3(1.6f, 2.0f, 0.05f), new Color(1f, 0.35f, 0.1f), 2.5f);
        // Kali Ma inside the doorway: dark figure, red garland
        GameKit.Cone(transform, "Kali", Temple + new Vector3(0, 1.2f, 8.2f), 0.35f, 1.4f, GameKit.Lit(new Color(0.08f, 0.1f, 0.22f), 0.6f));
        Sp("KaliHead", Temple + new Vector3(0, 2.75f, 8.2f), 0.35f, new Color(0.08f, 0.1f, 0.22f), 0.6f);
        for (int i = 0; i <= 10; i++) Sp("Garland", Temple + new Vector3(-0.3f + i * 0.06f, 2.3f - Mathf.Sin(i / 10f * Mathf.PI) * 0.25f, 7.95f), 0.08f, i % 2 == 0 ? red : new Color(1f, 0.6f, 0.1f));
        PL(Temple + new Vector3(0, 2.5f, 7f), new Color(1f, 0.5f, 0.18f), 5f, 12f);
        PL(Temple + new Vector3(0, 1.6f, 5.5f), new Color(1f, 0.6f, 0.3f), 2.5f, 8f);                   // steady glow on the steps
        // warm uplight on the shikhara so the temple reads against the night sky
        Spot(Temple + new Vector3(0, 0.3f, 2.5f), new Vector3(-48f, 0f, 0f), new Color(1f, 0.62f, 0.32f), 6f, 16f, 50f);
        // carved pillars + marigold strings across the sanctum front
        foreach (float px in new[] { -2.1f, 2.1f })
        {
            B("SanctumPillar", Temple + new Vector3(px, 2.8f, 7.35f), new Vector3(0.4f, 3.2f, 0.4f), new Color(0.8f, 0.5f, 0.28f), true, 0.25f);
            B("PillarCap", Temple + new Vector3(px, 4.45f, 7.35f), new Vector3(0.55f, 0.15f, 0.55f), new Color(0.7f, 0.4f, 0.2f), false, 0.25f);
        }
        for (int i = 0; i <= 16; i++)
            Sp("Marigold", Temple + new Vector3(-2.1f + i * 0.2625f, 4.1f - Mathf.Sin(i / 16f * Mathf.PI) * 0.45f, 7.2f), 0.11f, i % 3 == 0 ? new Color(0.9f, 0.15f, 0.08f) : new Color(1f, 0.6f, 0.08f));
        // bells at the entrance
        for (int i = 0; i < 3; i++)
        {
            B("BellChain", Temple + new Vector3(-1.2f + i * 1.2f, 3.6f, 6.2f), new Vector3(0.02f, 0.8f, 0.02f), new Color(0.6f, 0.5f, 0.2f), false, 0.7f, 0.8f);
            GameKit.Cone(transform, "Bell", Temple + new Vector3(-1.2f + i * 1.2f, 2.9f, 6.2f), 0.18f, 0.32f, GameKit.Lit(new Color(0.85f, 0.65f, 0.2f), 0.8f, 0.9f));
        }
        // oil lamps in wall niches
        for (int i = 0; i < 10; i++)
        {
            float x = -13f + i * 2.9f;
            foreach (float z in new[] { -14.6f, 14.6f })
            {
                if (z > 0 && Mathf.Abs(x) < 5f) continue;
                Glow("Diya", Temple + new Vector3(x, 1.2f, z), new Vector3(0.1f, 0.08f, 0.1f), new Color(1f, 0.6f, 0.15f), 6f);
                if (i % 2 == 0) PL(Temple + new Vector3(x, 1.4f, z * 0.96f), new Color(1f, 0.62f, 0.25f), 2.2f, 6.5f);
            }
        }
        // banyan tree with red threads
        Cy("BanyanTrunk", Temple + new Vector3(-9f, 2.5f, -6f), 0.8f, 5f, new Color(0.35f, 0.27f, 0.2f), 0.1f);
        for (int i = 0; i < 6; i++) Sp("BanyanCanopy", Temple + new Vector3(-9f + Mathf.Sin(i) * 2.5f, 6f + (i % 2), -6f + Mathf.Cos(i) * 2.5f), 4f, new Color(0.12f, 0.3f, 0.12f), 0.1f);
        for (int i = 0; i < 8; i++) B("SacredThread", Temple + new Vector3(-9f + Mathf.Sin(i * 0.8f) * 0.82f, 1.4f, -6f + Mathf.Cos(i * 0.8f) * 0.82f), new Vector3(0.05f, 0.05f, 0.05f), new Color(0.9f, 0.1f, 0.1f));
        // entrance gate (south)
        B("GateL", Temple + new Vector3(-2.2f, 2f, -14.7f), new Vector3(0.8f, 4f, 0.8f), new Color(0.8f, 0.5f, 0.3f), true);
        B("GateR", Temple + new Vector3(2.2f, 2f, -14.7f), new Vector3(0.8f, 4f, 0.8f), new Color(0.8f, 0.5f, 0.3f), true);
        B("GateTop", Temple + new Vector3(0, 4.2f, -14.7f), new Vector3(5.2f, 0.6f, 0.9f), new Color(0.75f, 0.2f, 0.12f));
        gate = new GameObject("Gate").transform; gate.SetParent(transform, false); gate.position = Temple + new Vector3(0, 1.5f, -13.5f);
        // the priest's alcove near the entrance, devotees
        B("Alcove", Temple + new Vector3(5.5f, 1.2f, -13.6f), new Vector3(2.4f, 2.4f, 1.2f), new Color(0.7f, 0.45f, 0.3f), true);
        B("AlcoveMat", Temple + new Vector3(5.5f, 0.02f, -12.6f), new Vector3(1.6f, 0.03f, 1.0f), new Color(0.6f, 0.15f, 0.12f));
        Glow("AlcoveLamp", Temple + new Vector3(4.6f, 0.15f, -12.4f), new Vector3(0.1f, 0.08f, 0.1f), new Color(1f, 0.6f, 0.15f), 6f);
        PL(Temple + new Vector3(5f, 1.8f, -11.8f), new Color(1f, 0.68f, 0.35f), 2.8f, 6f);
        // lanterns on the gate posts and lamp posts around the courtyard
        foreach (float gx in new[] { -2.2f, 2.2f })
        {
            Glow("GateLantern", Temple + new Vector3(gx, 3.3f, -14.2f), new Vector3(0.25f, 0.35f, 0.25f), new Color(1f, 0.7f, 0.35f), 3.5f);
            PL(Temple + new Vector3(gx, 3.1f, -13.6f), new Color(1f, 0.7f, 0.38f), 2.4f, 7f);
        }
        foreach (var lp in new[] { new Vector2(-8f, -8f), new Vector2(8f, -8f), new Vector2(-8f, 4f), new Vector2(8f, 4f) })
        {
            Cy("LampPost", Temple + new Vector3(lp.x, 1.3f, lp.y), 0.06f, 2.6f, new Color(0.2f, 0.18f, 0.15f), 0.4f, 0.6f);
            Glow("LampHead", Temple + new Vector3(lp.x, 2.7f, lp.y), new Vector3(0.28f, 0.3f, 0.28f), new Color(1f, 0.75f, 0.42f), 3f);
            PL(Temple + new Vector3(lp.x, 2.6f, lp.y), new Color(1f, 0.74f, 0.42f), 2.2f, 8f);
        }
        pandit = Npc(Temple + new Vector3(5.5f, 0f, -12.4f), 0f, Cast.Pandit(), "Pandit Harishankar");
        for (int i = 0; i < 4; i++) Npc(Temple + new Vector3(-2.5f + i * 1.6f, 0f, 3.2f), (i - 1.5f) * 6f, Cast.Devotee(800 + i), "Devotee" + i, false);   // facing the shrine
        GameKit.Sphere(transform, "Moon", Temple + new Vector3(30f, 45f, 90f), 7f, GameKit.Unlit(new Color(0.9f, 0.93f, 1f)));
    }

    AudioClip MakeChant()
    {
        const int SR = 44100; int n = SR * 8;
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float env = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * t / 8f * 2f);            // two slow swells per loop
            float s = 0;
            for (int h = 1; h <= 5; h++) s += Mathf.Sin(2f * Mathf.PI * 110f * h * t) / (h * h);
            d[i] = s * env * 0.25f;
        }
        Norm(d, 0.35f);
        var c = AudioClip.Create("chant", n, 1, SR, false); c.SetData(d, 0); return c;
    }

    // ===================================================================== FLOW
    protected override IEnumerator Run()
    {
        var d = SaveSystem.Data;
        int stage = resume ? d.act3Stage : 0;
        usbDone = stage >= 1;
        RegisterSpots();
        yield return OpenAct();

        if (stage < 2)
        {
            Teleport(LibStart, 40f);
            if (stage == 0)
            {
                yield return Narrate("a3_vo_intro");
                yield return Turn(sharma.transform.position + Vector3.up * 1.5f, 0.8f);
                yield return Talk("a3_sharma_1", sharma);
                yield return Talk("a3_arjun_letter");
                yield return Talk("a3_sharma_2", sharma);
                EndTalk();
                BeginFree("Use the computer at the front desk", computer.position + Vector3.up * 0.4f, "Front desk computer");
                yield return new WaitUntil(() => usbDone);
            }
            else BeginFree("Go down to the Record Room", stairsDown.position + Vector3.up, "Record Room stairs");

            yield return new WaitUntil(() => cageOpen);
        }

        // ---- the temple
        yield return FadeOut(1f);
        area = 2; SwitchBed(templeBed); PriyaRoomPlayer.Surface = 1;
        AreaLook(2);
        Teleport(Temple + new Vector3(0f, 0.1f, -13f), 0f);
        ShowTitle("THE KALI TEMPLE", "Old Quarter   |   10:10 PM", 3.5f);
        yield return FadeIn(0.8f);
        BeginBusy();
        if (d.act3Stage < 3)
        {
            yield return Narrate("a3_vo_temple");
            BeginFree("Find the temple priest", pandit.transform.position + Vector3.up * 1.9f, "Pandit Harishankar");
            yield return new WaitUntil(() => panditDone);
        }
        else panditDone = true;

        BeginFree("Leave the temple", gate.position + Vector3.up, "Temple gate");
        yield return new WaitUntil(() => Near(gate.position, 3f));
        yield return PhoneHook();
        yield return CloseAct("ACT IV  -  THE WEB", "");
    }

    void SwitchBed(AudioClip c)
    {
        ambience.clip = c; ambience.Play();
    }

    protected override void Update()
    {
        base.Update();
        if (area == 2 && Time.time > nextBell)
        {
            sfx.PlayOneShot(Tone(392f, 2.5f, 1.2f, 0.25f), 0.6f);
            nextBell = Time.time + Random.Range(9f, 16f);
        }
        if (area == 1 && Time.time > nextRat)
        {
            sfx.PlayOneShot(Tone(Random.Range(2600f, 3400f), 0.12f, 30f, 0.15f), 0.4f);       // a rat squeaks somewhere
            nextRat = Time.time + Random.Range(6f, 14f);
        }
    }

    void RegisterSpots()
    {
        AddSpot(computer, Vector3.zero, "Use the computer", () => !usbDone && area == 0, () => StartCoroutine(ComputerPuzzle()));
        AddSpot(stairsDown, Vector3.zero, "Go down to the Record Room", () => usbDone && area == 0, () => StartCoroutine(GoDown()));
        AddSpot(stairsUp, Vector3.zero, "Go back upstairs", () => area == 1 && !cageOpen, () => StartCoroutine(GoUp()));
        cageSpot = AddSpot(cage, Vector3.zero, "Inspect the cage", () => area == 1 && !cageOpen, () => StartCoroutine(CageAction()), 3.2f, 0.8f);
        AddSpot(calendar, Vector3.zero, "Look at the calendar", () => area == 1, () => StartCoroutine(CalendarAction()));
        AddSpot(libDesk, new Vector3(0, 0.3f, -0.45f), "Try the desk drawer", () => area == 0 && cageSeen && !keyFound, () => StartCoroutine(DeskPuzzle()));
        AddSpot(pandit.transform, Vector3.up * 1.5f, "Talk to Pandit Harishankar", () => area == 2 && !panditDone && phase == Phase.Free, () => StartCoroutine(PanditTalk()));
    }

    Spot cageSpot;

    // ---- library: the encrypted drive
    IEnumerator ComputerPuzzle()
    {
        BeginBusy();
        yield return Turn(computer.position, 0.5f);
        yield return Talk(SaveSystem.Data.hasUsb ? "a3_arjun_usb" : "a3_arjun_backup");
        yield return Talk("a3_arjun_pass");
        EndTalk();
        yield return EnterCode("ENCRYPTED FILES  -  PRIYA KAPOOR",
            "Password required.\nPriya's diary: \"Where it all began for us - the year Nani told us stories.\"",
            "NANI1987", 8, false, "the photo Kamala-ji sent",
            "A faded photograph: Priya and Rohan as children on the steps of their grandmother's house.\nOn the back, in blue ink: \"Nani's house - 1987\".");
        if (!codeResult)
        {
            Toast("The files stay locked. Come back when you know the password.", 4f);
            BeginFree("Use the computer at the front desk", computer.position + Vector3.up * 0.4f, "Front desk computer");
            yield break;
        }
        yield return ShowCard("PRIYA'S FILES - UNLOCKED",
            "1.  Land Acquisition Order, 1965 - claims Basti Nagar was acquired by the government. The signature is inconsistent and the stamp is from the wrong district office.\n\n" +
            "2.  Original Land Deed, 1942 (scan) - granted by the Maharaja of Chandnipur to the residents of Basti Nagar \"in perpetuity\".\n\n" +
            "3.  Priya's comparison notes - every discrepancy between the two, highlighted.\n\n" +
            "4.  Surveillance photo - Councilor Deshpande meeting builder Raj Malhotra and a third person whose face is in shadow.\n\n" +
            "5.  Audio file (corrupted) - fragments of a phone call.");
        yield return Narrate("a3_audio");
        AddEvidence("USB files: the 1965 acquisition order is forged (wrong stamp, inconsistent signature).");
        AddEvidence("USB files: scan of the original 1942 deed - Basti Nagar belongs to its residents in perpetuity.");
        AddEvidence("USB files: photo of Deshpande meeting Raj Malhotra and a man in shadow.");
        AddEvidence("Corrupted recording: \"...make sure the originals are destroyed... Reddy is getting too close...\"");
        yield return Talk("a3_arjun_unlock");
        EndTalk();
        usbDone = true;
        SaveSystem.Data.act3Stage = 1; SaveSystem.Save();
        BeginFree("Go down to the Record Room", stairsDown.position + Vector3.up, "Record Room stairs");
    }

    IEnumerator GoDown()
    {
        BeginBusy();
        sfx.PlayOneShot(stepClip, 0.6f);
        yield return FadeOut(2f);
        area = 1; SwitchBed(baseBed); AreaLook(1);
        Teleport(Base + new Vector3(0, 0.1f, -5.8f), 0f);
        yield return FadeIn(1.5f);
        if (!cageSeen)
        {
            yield return Talk("a3_arjun_basement");
            EndTalk();
        }
        if (cageSeen && !keyFound && deskTried && !calendarSeen)
            BeginFree("Find the drawer code - check the calendar on the east wall", calendar.position, "Wall calendar");
        else
            BeginFree(keyFound ? "Open the cage at Block C, Shelf 14" : (cageSeen ? "Find the key - try the librarian's desk upstairs" : "Find Block C, Shelf 14"),
                      cage.position + Vector3.up * 0.8f, "Block C, Shelf 14");
    }

    IEnumerator GoUp()
    {
        BeginBusy();
        sfx.PlayOneShot(stepClip, 0.6f);
        yield return FadeOut(2f);
        area = 0; SwitchBed(libBed); AreaLook(0);
        Teleport(stairsDown.position + new Vector3(0, -1f, -1.2f), 180f);
        yield return FadeIn(1.5f);
        BeginFree(cageSeen && !keyFound ? "Open the librarian's desk" : "Go back down to the Record Room",
                  cageSeen && !keyFound ? libDesk.position + Vector3.up : stairsDown.position + Vector3.up, cageSeen && !keyFound ? "Librarian's desk" : "Record Room stairs");
    }

    IEnumerator CalendarAction()
    {
        BeginBusy();
        yield return ShowCard("MUNICIPAL CALENDAR",
            "A yellowed calendar of municipal holidays. Three dates are circled in red ink:\n\n" +
            "    15 August  -  Independence Day  (India became free on 15 August 1947)\n    26 January  -  Republic Day\n    2 October  -  Gandhi Jayanti\n\n" +
            "In the margin, in the librarian's handwriting:  \"the year we became free - read it backwards\"\n\n" +
            "(This matches the note taped to the librarian's desk drawer upstairs.)");
        yield return Talk("a3_arjun_calendar");
        EndTalk();
        calendarSeen = true;
        if (cageSeen && !keyFound)
            BeginFree("Go upstairs and open the librarian's desk drawer", stairsUp.position + Vector3.up * 0.5f, "Stairs up");
        else BeginFree(objective, objectiveTarget, objectiveName);
    }

    IEnumerator CageAction()
    {
        BeginBusy();
        if (!keyFound)
        {
            yield return Talk("a3_arjun_cage");
            EndTalk();
            cageSeen = true;
            BeginFree("Find the key - try the librarian's desk upstairs", stairsUp.position + Vector3.up * 0.5f, "Stairs up");
            yield break;
        }
        // open the cage
        sfx.PlayOneShot(Tone(600f, 0.25f, 18f, 0.5f, true), 0.6f);
        for (float t = 0; t < 1f; t += Time.deltaTime)
        {
            cageDoor.localRotation = Quaternion.Euler(0, -100f * Mathf.SmoothStep(0, 1, t), 0);
            yield return null;
        }
        yield return Talk("a3_arjun_empty");
        EndTalk();
        yield return ShowCard("RECORD ROOM - SIGN-OUT SHEET  (Block C / 14 / 1942)",
            "Date          Name             Purpose                Signature\n" +
            "14 Mar 1998    R. Joshi          Audit                    R.J.\n" +
            "02 Jul 2011    M. Iyer            Court copy              M. Iyer\n" +
            "two weeks ago  S. REDDY          (left blank)            S. Reddy\n\n" +
            "The folder for 1942 is empty. The original deed is gone.");
        yield return Talk("a3_arjun_reddy");
        EndTalk();
        AddEvidence("Library sign-out sheet: the original 1942 deed was taken two weeks ago by \"S. Reddy\".");
        cageOpen = true;
        SaveSystem.Data.act3Stage = 2; SaveSystem.Save();
    }

    IEnumerator DeskPuzzle()
    {
        BeginBusy();
        yield return Talk("a3_arjun_desk");
        EndTalk();
        deskTried = true;
        yield return EnterCode("LIBRARIAN'S DESK  -  COMBINATION LOCK",
            "Four digits. A yellowed note is taped beside the lock:\n\"drawer code: the year we became free - BACKWARDS  (see calendar, Record Rm)\"",
            "7491", 4, true,
            calendarSeen ? "the note + the basement calendar" : "the note on the drawer",
            calendarSeen
                ? "Note: \"the year we became free - BACKWARDS\".\nCalendar: 15 August 1947 - Independence Day.\nSo: take 1947 and read the digits from right to left."
                : "Note: \"drawer code: the year we became free - BACKWARDS (see calendar, Record Rm)\".\nThe librarian's calendar is on the east wall of the Record Room downstairs.");
        if (!codeResult)
        {
            if (calendarSeen)
            {
                Toast("Still locked. India became free in 1947 - now read it backwards.", 5f);
                BeginFree("Open the librarian's desk", libDesk.position + Vector3.up, "Librarian's desk");
            }
            else
            {
                Toast("Locked. The note says to check the calendar in the Record Room.", 5f);
                BeginFree("Check the calendar in the Record Room downstairs", stairsDown.position + Vector3.up, "Record Room stairs");
            }
            yield break;
        }
        sfx.PlayOneShot(Tone(900f, 0.2f, 20f, 0.4f), 0.5f);
        yield return Talk("a3_arjun_key");
        EndTalk();
        keyFound = true;
        cageSpot.label = "Open the cage with the key";
        Toast("You found: CAGE KEY", 4f, true);
        BeginFree("Open the cage at Block C, Shelf 14", stairsDown.position + Vector3.up, "Record Room stairs");
    }

    // ---- temple
    IEnumerator PanditTalk()
    {
        BeginBusy();
        yield return Turn(pandit.transform.position + Vector3.up * 1.5f, 0.7f);
        yield return Talk("a3_pandit_1", pandit);
        yield return Talk("a3_arjun_q");
        yield return Talk("a3_pandit_2", pandit);
        yield return Choose(new[] { DialogueDB.Text("a3_ans_a"), DialogueDB.Text("a3_ans_b"), DialogueDB.Text("a3_ans_c") });
        int c = menuResult;
        yield return Talk(c == 0 ? "a3_ans_a" : c == 1 ? "a3_ans_b" : "a3_ans_c");
        yield return Talk(c == 0 ? "a3_pandit_a" : c == 1 ? "a3_pandit_b" : "a3_pandit_c", pandit);
        EndTalk();
        SaveSystem.Data.panditAnswer = c;
        Toast("You received: PRIYA'S SEALED ENVELOPE", 4f, true);
        AddEvidence("Certified copy of the original 1942 land deed - admissible in court.");
        AddEvidence("Priya's letter: councilor, builder and police are connected; the hearing is at 10 AM, Municipal Court.");
        if (c == 1) AddEvidence("Panditji: the hearing is tomorrow at 10 AM - Priya wants the documents there.");
        if (c == 2) AddEvidence("Panditji: Priya is safe at the NGO shelter in the old school by the river.");
        StartCoroutine(Narrate("a3_letter"));
        yield return ShowCard("A LETTER, IN PRIYA'S HAND",
            "Dear whoever-you-are,\n\nIf you're reading this, I hope you're someone I can trust. I have the original documents with me. " +
            "The councilor, the builder, and someone in the police are all connected. I'm safe for now, but I can't stay hidden forever.\n\n" +
            "The hearing is tomorrow morning at 10 AM at the Municipal Court. These documents must reach the court. Five hundred families are counting on this.\n\n" +
            "I'm trusting you with their futures. Please don't let me down.\n\n- Priya");
        voice.Stop();
        SaveSystem.Data.act3Stage = 3; SaveSystem.Save();
        panditDone = true;
    }

    IEnumerator PhoneHook()
    {
        BeginBusy();
        objective = ""; objectiveTarget = null;
        sfx.PlayOneShot(ringClip, 0.7f);
        yield return new WaitForSeconds(2.3f);
        yield return Narrate("a3_caller");
        sfx.PlayOneShot(dingClip, 0.6f);
        yield return ShowCard("NEW MESSAGE  -  Kamala-ji",
            "\"Arjun, someone broke into our house. They tore everything apart. They were looking for something. Rohan is scared. I'm scared. Please help us.\"");
        yield return Narrate("a3_vo_crossroads");
        dlgSpeaker = "WHERE NOW?";
        dlgText = "Two urgent calls, and time for one. Whatever you choose, the others will have to wait.";
        yield return Choose(new[]
        {
            "Go to Café Irani  -  meet the mysterious caller",
            "Go to the Kapoor house  -  protect the family",
            "Call Inspector Reddy  -  confront her directly",
        });
        EndTalk();
        SaveSystem.Data.branch = menuResult; SaveSystem.Save();
        yield return Narrate("a3_vo_end");
    }
}
