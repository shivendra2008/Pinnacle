using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The Kapoor house (Act IV, variant B): the family's own front door off the landing Arjun knows from Act I (flat
/// 301, the cream KAPOOR nameplate, the doormat, the shoe rack), and what the search did to it, in the order it was
/// done: the steel almirah forced, the drawers pulled out, the law books swept off the shelf, the sofa cut open, and
/// muddy prints walking back out of the door. Reading Rohan's laptop does not start the confession: Arjun closes it,
/// Rohan stops at the sound, Kamala looks at her son, and the player turns to him when ready.
/// </summary>
public partial class Act4Story
{
    // read by the play-test route
    bool rohanStoppedAtClick, kamalaLookedAtRohan;
    Transform almirahDoor, kamalaCloth;

    void BuildHouseDetails()
    {
        var root = new GameObject("HouseDetails").transform; root.SetParent(transform, false);
        var wallC = new Color(0.82f, 0.78f, 0.62f);
        var wood = new Color(0.4f, 0.26f, 0.15f);
        // ---- the west wall gets the front doorway (it was a door painted on a solid wall)
        var ww = transform.Find("House_WallW");
        if (ww != null) { ww.gameObject.SetActive(false); }
        GameObject W(string n, Vector3 c, Vector3 size) { var g = B(n, c, size, wallC, true); GameKit.Surface(g, "plaster", 2f, 1f, 1f, wallC); return g; }
        W("House_WallW_S", new Vector3(-4.625f, 1.5f, -3.875f), new Vector3(0.25f, 3f, 0.75f));
        W("House_WallW_N", new Vector3(-4.625f, 1.5f, 0.875f), new Vector3(0.25f, 3f, 6.75f));
        W("House_WallW_Head", new Vector3(-4.625f, 2.55f, -3.0f), new Vector3(0.25f, 0.9f, 1.0f));
        var frameM = ProcTex.Mat(ProcTex.Get("wood"), wood * 0.9f, new Vector2(0.3f, 2f), 0.8f, 1f);
        foreach (float jz in new[] { -3.53f, -2.47f }) GameKit.Box(root, "DoorJamb", new Vector3(-4.5f, 1.05f, jz), new Vector3(0.28f, 2.12f, 0.06f), frameM);
        GameKit.Box(root, "DoorHead", new Vector3(-4.5f, 2.13f, -3.0f), new Vector3(0.28f, 0.06f, 1.12f), frameM);
        // the leaf stands open against the inside, as they left it
        var hinge = new GameObject("FrontDoorHinge").transform; hinge.SetParent(root, false);
        hinge.position = new Vector3(-4.48f, 0f, -3.5f); hinge.rotation = Quaternion.Euler(0f, 97f, 0f);      // swung in against the room side
        var leaf = GameKit.Box(hinge, "FrontDoorLeaf", new Vector3(0f, 1.03f, 0.48f), new Vector3(0.05f, 2.06f, 0.96f), ProcTex.Mat(ProcTex.Get("wood"), new Color(0.35f, 0.28f, 0.2f), new Vector2(1f, 2f), 0.8f, 1f));
        leaf.AddComponent<BoxCollider>();
        foreach (float py in new[] { 1.5f, 0.6f }) GameKit.Box(hinge, "DoorPanel", new Vector3(0.03f, py, 0.48f), new Vector3(0.02f, 0.7f, 0.7f), GameKit.Lit(new Color(0.28f, 0.22f, 0.16f), 0.3f));
        GameKit.Box(hinge, "DoorHandle", new Vector3(0.05f, 1.0f, 0.86f), new Vector3(0.03f, 0.03f, 0.12f), GameKit.Lit(new Color(0.7f, 0.55f, 0.25f), 0.8f, 0.9f));
        // the latch keeper was levered off the frame: splinters at the strike side
        var splinter = GameKit.Lit(new Color(0.62f, 0.48f, 0.3f), 0.2f);
        for (int i = 0; i < 4; i++) GameKit.Box(root, "Splinter", new Vector3(-4.42f, 0.98f + i * 0.035f, -2.49f + (i % 2) * 0.01f), new Vector3(0.02f, 0.012f, 0.07f - i * 0.01f), splinter).transform.rotation = Quaternion.Euler(0f, 20f * i, 15f);

        // ---- the landing outside flat 301, as in Act I: tiled floor, the nameplate, the doormat, the shoe rack
        var hall = B("HallFloor", new Vector3(-5.95f, -0.05f, -3.0f), new Vector3(2.4f, 0.1f, 3.2f), new Color(0.82f, 0.78f, 0.7f), true, 0.5f);
        GameKit.Surface(hall, "stone", 1f, 1f, 1f, new Color(0.82f, 0.78f, 0.7f));
        var hallWall = new Color(0.86f, 0.84f, 0.76f);
        foreach (var w in new[] { new Vector4(-7.27f, -3f, 0.15f, 3.2f), new Vector4(-5.95f, -4.67f, 2.4f, 0.15f), new Vector4(-5.95f, -1.33f, 2.4f, 0.15f) })
        { var g = B("HallWall", new Vector3(w.x, 1.5f, w.y), new Vector3(w.z, 3f, w.w), hallWall, true); GameKit.Surface(g, "plaster", 2f, 1f, 1f, hallWall); }
        GameKit.Surface(B("HallCeiling", new Vector3(-5.95f, 3.05f, -3f), new Vector3(2.6f, 0.1f, 3.4f), hallWall * 0.95f, true), "plaster", 2f);
        B("HallDado", new Vector3(-7.18f, 0.5f, -3f), new Vector3(0.02f, 1f, 3.1f), new Color(0.55f, 0.6f, 0.52f), false, 0.2f);
        Glow("HallTube", new Vector3(-6.6f, 2.96f, -3f), new Vector3(0.14f, 0.05f, 1.2f), new Color(0.92f, 0.97f, 1f), 2.6f);
        PL(new Vector3(-6.3f, 2.6f, -3f), new Color(0.9f, 0.95f, 1f), 1.6f, 6f);
        // the cream nameplate beside the door (the same plate as in Act I: 301 in red over KAPOOR, two brass screws)
        var plate = new GameObject("NameplateMount").transform; plate.SetParent(root, false);
        plate.position = new Vector3(-4.768f, 1.55f, -3.86f); plate.rotation = Quaternion.Euler(0f, 90f, 0f);      // its face (local -z) to the landing
        var board = GameKit.Box(plate, "Nameplate", Vector3.zero, new Vector3(0.42f, 0.24f, 0.03f), GameKit.Lit(new Color(0.93f, 0.9f, 0.8f), 0.4f));
        board.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        foreach (var sc in new[] { new Vector3(-0.18f, 0.09f, -0.016f), new Vector3(0.18f, -0.09f, -0.016f) }) GameKit.Box(plate, "NameplateScrew", sc, new Vector3(0.012f, 0.012f, 0.004f), GameKit.Lit(new Color(0.55f, 0.5f, 0.4f), 0.6f, 0.8f));
        GameKit.Solid(GameKit.Text(plate, "KAPOOR", new Vector3(0f, -0.03f, -0.016f), Quaternion.identity, 0.06f, new Color(0.1f, 0.06f, 0.04f)));
        GameKit.Solid(GameKit.Text(plate, "301", new Vector3(0f, 0.07f, -0.016f), Quaternion.identity, 0.075f, new Color(0.6f, 0.1f, 0.08f)));
        // what changed: the doormat kicked crooked with mud on it, the shoe rack knocked over, the family's shoes scattered
        var mat = B("Doormat", new Vector3(-5.25f, 0.012f, -3.05f), new Vector3(0.55f, 0.02f, 0.9f), new Color(0.35f, 0.28f, 0.2f), false, 0.05f);
        GameKit.Surface(mat, "fabric", 0.3f, 1f, 1f, new Color(0.35f, 0.28f, 0.2f));
        mat.transform.rotation = Quaternion.Euler(0f, 17f, 0f);
        var rack = B("ShoeRack", new Vector3(-4.95f, 0.16f, -1.95f), new Vector3(0.28f, 0.04f, 0.7f), new Color(0.3f, 0.2f, 0.12f));
        rack.transform.rotation = Quaternion.Euler(0f, 0f, 74f);
        B("ShoeRack", new Vector3(-5.1f, 0.02f, -1.95f), new Vector3(0.28f, 0.04f, 0.7f), new Color(0.3f, 0.2f, 0.12f)).transform.rotation = Quaternion.Euler(0f, 12f, 0f);
        var rs = new System.Random(301);
        for (int i = 0; i < 5; i++)
        {
            var sh = B("Shoe", new Vector3(-5.2f - (float)rs.NextDouble() * 0.7f, 0.03f, -2.4f + (float)rs.NextDouble() * 0.9f), new Vector3(0.1f, 0.06f, 0.22f), Color.HSVToRGB((-4.5f * 3f + i * 0.17f + 10f) % 1f, 0.5f, i % 2 == 0 ? 0.35f : 0.75f), false, 0.3f);
            sh.transform.rotation = Quaternion.Euler(0f, (float)rs.NextDouble() * 360f, i == 2 ? 90f : 0f);
        }
        Fence(new Vector3(-7.1f, 1.5f, -3f), new Vector3(0.2f, 3f, 3.2f));

        // ---- the steel almirah (every Indian home's locked cupboard), levered open: one door wrenched back, the lock bent
        var steel = GameKit.Lit(new Color(0.36f, 0.42f, 0.4f), 0.48f, 0.3f);          // painted steel (the old grey-green), not bare metal
        var alm = new GameObject("Almirah").transform; alm.SetParent(root, false);
        alm.position = new Vector3(-1.2f, 0f, -3.72f); alm.rotation = Quaternion.identity;        // its front faces into the room (+z)
        var body = GameKit.Box(alm, "AlmirahBody", new Vector3(0f, 0.92f, 0f), new Vector3(0.92f, 1.84f, 0.5f), steel);
        body.AddComponent<BoxCollider>();
        GameKit.Box(alm, "AlmirahInside", new Vector3(0f, 0.92f, 0.235f), new Vector3(0.86f, 1.78f, 0.02f), GameKit.Lit(new Color(0.18f, 0.19f, 0.2f), 0.4f));
        for (int k = 0; k < 3; k++) GameKit.Box(alm, "AlmirahShelf", new Vector3(0f, 0.45f + k * 0.45f, 0.22f), new Vector3(0.86f, 0.015f, 0.04f), steel);
        // the right-hand door still shut, its handle and lock plate
        GameKit.Box(alm, "AlmirahDoorR", new Vector3(0.23f, 0.92f, 0.258f), new Vector3(0.45f, 1.8f, 0.015f), steel);
        GameKit.Box(alm, "AlmirahHandle", new Vector3(0.03f, 1.0f, 0.275f), new Vector3(0.02f, 0.14f, 0.02f), GameKit.Lit(new Color(0.75f, 0.75f, 0.72f), 0.85f, 0.95f));
        // the left door, levered back on its hinge, the lock tongue bent out, pry marks on the edge
        var ad = new GameObject("AlmirahDoorHinge").transform; ad.SetParent(alm, false);
        ad.localPosition = new Vector3(-0.455f, 0f, 0.265f); ad.localRotation = Quaternion.Euler(0f, -118f, 0f);
        almirahDoor = ad;
        GameKit.Box(ad, "AlmirahDoorL", new Vector3(0.225f, 0.92f, 0f), new Vector3(0.45f, 1.8f, 0.015f), steel);
        GameKit.Box(ad, "BentLock", new Vector3(0.43f, 1.0f, 0.015f), new Vector3(0.04f, 0.06f, 0.02f), GameKit.Lit(new Color(0.65f, 0.58f, 0.3f), 0.7f, 0.9f)).transform.localRotation = Quaternion.Euler(0f, 0f, 24f);
        var pry = GameKit.Lit(new Color(0.22f, 0.22f, 0.23f), 0.3f, 0.6f);
        for (int i = 0; i < 3; i++) GameKit.Box(alm, "PryMark", new Vector3(0.005f, 0.96f + i * 0.05f, 0.267f), new Vector3(0.012f, 0.03f, 0.003f), pry);
        // what was inside, dropped where they rummaged: folded saris, a document box tipped out, papers
        var sari = new[] { new Color(0.7f, 0.12f, 0.15f), new Color(0.15f, 0.35f, 0.55f), new Color(0.85f, 0.65f, 0.2f) };
        for (int i = 0; i < 3; i++)
        {
            var f = B("FoldedSari", new Vector3(-1.45f + i * 0.28f, 0.03f + (i == 1 ? 0.05f : 0f), -3.15f + (i % 2) * 0.12f), new Vector3(0.32f, 0.05f, 0.24f), sari[i], false, 0.1f);
            GameKit.Surface(f, "fabric", 0.25f, 1f, 1f, sari[i]);
            f.transform.rotation = Quaternion.Euler(0f, i * 23f - 15f, 0f);
        }
        var docBox = B("DocumentBox", new Vector3(-0.6f, 0.09f, -3.05f), new Vector3(0.34f, 0.16f, 0.26f), new Color(0.3f, 0.25f, 0.2f), false, 0.3f);
        docBox.transform.rotation = Quaternion.Euler(0f, 35f, 78f);
        // a table lamp knocked onto the floor by the almirah, still lit: it shows the forced door, the empty shelves, the
        // dropped saris and the first muddy prints
        var lampBase = Turned("KnockedLamp", new Vector3(-2.15f, 0.06f, -2.95f), new[] { new Vector2(0f, 0.08f), new Vector2(0.03f, 0.08f), new Vector2(0.05f, 0.02f), new Vector2(0.3f, 0.015f) }, GameKit.Lit(new Color(0.55f, 0.42f, 0.22f), 0.6f, 0.8f), 14);
        lampBase.transform.rotation = Quaternion.Euler(0f, 0f, 84f);
        Glow("KnockedLampBulb", new Vector3(-1.86f, 0.07f, -2.95f), new Vector3(0.06f, 0.06f, 0.06f), new Color(1f, 0.82f, 0.55f), 4f);
        var kl = new GameObject("KnockedLampLight").AddComponent<Light>(); kl.transform.SetParent(transform, false);
        kl.transform.position = new Vector3(-1.82f, 0.12f, -2.95f);
        kl.transform.rotation = Quaternion.LookRotation(new Vector3(0.6f, 0.35f, -0.7f));
        kl.type = LightType.Spot; kl.spotAngle = 95f; kl.innerSpotAngle = 40f; kl.range = 3.4f; kl.intensity = 2.2f; kl.color = new Color(1f, 0.78f, 0.5f);
        CinematicLook.Shadows(kl, 0.8f, 0.02f, 0.4f);

        // ---- papers: in heaps where each place was searched, thinning along the way, none across the open floor
        var rnd = new System.Random(8);
        void Heap(Vector3 c, int n, float spread)
        {
            for (int i = 0; i < n; i++)
            {
                float r = spread * Mathf.Sqrt((float)rnd.NextDouble()), a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                var pp = B("Paper", c + new Vector3(Mathf.Cos(a) * r, 0.004f + i * 0.0007f, Mathf.Sin(a) * r), new Vector3(0.21f, 0.003f, 0.3f), new Color(0.94f, 0.93f, 0.88f), false, 0.05f);
                pp.transform.rotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
            }
        }
        Heap(new Vector3(-0.9f, 0f, -2.95f), 12, 0.45f);        // the almirah's locker: the family's papers
        Heap(new Vector3(3.55f, 0f, -1.2f), 10, 0.5f);          // in front of the cabinet, under its drawers
        Heap(new Vector3(2.1f, 0f, 2.2f), 5, 0.4f);             // under the shelf
        Heap(new Vector3(-2.6f, 0f, 2.1f), 3, 0.3f);            // by the sofa

        // ---- muddy boot prints: heavy, from the almirah back to the door and out over the mat, fading as the mud wore off
        var mud = new Color(0.2f, 0.14f, 0.08f);
        Vector3 from = new Vector3(-1.5f, 0f, -3.05f), to = new Vector3(-5.6f, 0f, -3.0f);
        int steps = 8;
        for (int i = 0; i < steps; i++)
        {
            float u = i / (float)(steps - 1);
            Vector3 p = Vector3.Lerp(from, to, u) + new Vector3(0f, 0f, (i % 2 == 0 ? 0.11f : -0.11f));
            float fade = Mathf.Lerp(1f, 0.35f, u);
            var bp = B("BootPrintTrail", new Vector3(p.x, 0.007f + (p.x < -4.75f ? 0.015f : 0f), p.z), new Vector3(0.32f, 0.004f, 0.13f), Color.Lerp(new Color(0.55f, 0.5f, 0.42f), mud, fade), false, 0.1f);
            bp.transform.rotation = Quaternion.Euler(0f, (i % 2 == 0 ? -4f : 6f), 0f);          // toes pointing at the door (-x)
            var heel = B("BootHeel", bp.transform.position + new Vector3(0.12f, 0.0005f, 0f), new Vector3(0.08f, 0.004f, 0.11f), Color.Lerp(new Color(0.55f, 0.5f, 0.42f), mud * 0.8f, fade), false, 0.1f);
            heel.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        // the examinable print is the clearest one, by the door (the old single print is turned to match the trail)
        if (bootPrint != null) { bootPrint.position = new Vector3(-3.95f, 0.008f, -2.92f); bootPrint.rotation = Quaternion.Euler(0f, 90f, 0f); }

        // ---- Kamala holds a folded sari she has picked up; she has been putting things back
        var cloth = B("KamalaSari", Vector3.zero, new Vector3(0.3f, 0.05f, 0.22f), new Color(0.62f, 0.1f, 0.18f), false, 0.1f);
        GameKit.Surface(cloth, "fabric", 0.25f, 1f, 1f, new Color(0.62f, 0.1f, 0.18f));
        kamalaCloth = cloth.transform;
        kamalaCloth.SetParent(kamala.transform, false);
        kamalaCloth.localPosition = new Vector3(0f, 1.0f, 0.3f);
        var hl = new GameObject("KamalaHandL").transform; hl.SetParent(kamalaCloth, false); hl.localPosition = new Vector3(-0.15f, 0.0f, -0.02f);
        var hr = new GameObject("KamalaHandR").transform; hr.SetParent(kamalaCloth, false); hr.localPosition = new Vector3(0.15f, 0.0f, -0.02f);
        kamala.Hold(0, hl, 0.01f); kamala.Hold(1, hr, 0.01f);
        kamala.transform.position = new Vector3(-1.9f, 0f, -2.55f);
    }

    bool rohanPacing = true;
    IEnumerator RohanPaces()
    {
        while (rohanPacing && rohan != null)
        {
            yield return rohan.WalkTo(new Vector3(3.2f, 0, 0.6f), 0.9f);
            if (!rohanPacing) break;
            yield return rohan.WalkTo(new Vector3(0.6f, 0, 0.2f), 0.9f);
        }
    }

    IEnumerator HouseRun()
    {
        AddSpot(laptop, Vector3.zero, "Look at Rohan's laptop", () => phase == Phase.Free && !laptopRead, () => StartCoroutine(LaptopClue()), 3f, 0.3f);
        AddSpot(laptopLid, new Vector3(0f, 0.12f, 0f), "Close the laptop", () => phase == Phase.Free && laptopRead && !laptopClosed, () => StartCoroutine(CloseLaptop()), 3f, 0.3f);
        AddSpot(rohan.transform, Vector3.up * 1.5f, "Ask Rohan about the emails", () => phase == Phase.Free && laptopClosed && !confronting, () => confronting = true, 3.5f, 0.45f);
        AddSpot(bootPrint, Vector3.zero, "Examine the boot print", () => phase == Phase.Free && !printSeen && !confronting, () => StartCoroutine(SmallClue(true)), 3f, 0.35f);
        AddSpot(visitorPass, Vector3.zero, "Pick up the visitor pass", () => phase == Phase.Free && !passSeen && !confronting, () => StartCoroutine(SmallClue(false)), 3f, 0.3f);
        StartCoroutine(RohanPaces());
        yield return OpenAct();
        // on the landing outside flat 301, the door standing open
        Teleport(new Vector3(-6.2f, 0.1f, -3.0f), 90f);
        yield return Narrate("a4b_vo");
        BeginFree("Go in", new Vector3(-3.6f, 1.5f, -3f), "Flat 301");
        yield return new WaitUntil(() => player.transform.position.x > -4.1f);
        BeginBusy();
        kamala.lookTarget = cam;
        yield return Turn(kamala.EyePoint(), 0.8f);
        yield return Talk("a4b_kamala_1", kamala);
        yield return Talk("a4b_arjun_find");
        // she looks round her own front room before she answers
        var look = new GameObject("KamalaLooksRound").transform; look.SetParent(transform, false);
        look.position = new Vector3(-1.2f, 1.1f, -3.5f);
        kamala.lookTarget = look;
        yield return Wait(1.0f);
        look.position = new Vector3(2.6f, 0.4f, 2.4f);
        yield return Wait(0.9f);
        kamala.lookTarget = cam;
        yield return Wait(0.3f);
        Destroy(look.gameObject);
        yield return Talk("a4b_kamala_2", kamala);
        EndTalk();
        BeginFree("Search the flat for anything they missed", null);
        yield return new WaitUntil(() => laptopRead);
        BeginFree("Close the laptop", laptopLid.position + Vector3.up * 0.1f, "Rohan's laptop");
        // closing it plays the reaction and hands the room back (CloseLaptop); then silence until the player turns to Rohan
        yield return new WaitUntil(() => confronting);

        // the confession
        BeginBusy();
        rohanPacing = false;
        rohan.StopWalking();
        rohan.lookTarget = cam;
        yield return Turn(rohan.EyePoint(), 0.6f);
        yield return Talk("a4b_arjun_confront");
        yield return Talk("a4b_rohan_1", rohan);
        yield return Talk("a4b_arjun_told");
        yield return Talk("a4b_rohan_2", rohan);
        yield return Choose(new[] { "Take me to her", "The police", "Protect the files" },
                            new[] { DialogueDB.Text("a4b_q_a"), DialogueDB.Text("a4b_q_b"), DialogueDB.Text("a4b_q_c") }, null, null);
        int c = menuResult;
        yield return Talk(c == 0 ? "a4b_q_a" : c == 1 ? "a4b_q_b" : "a4b_q_c");
        yield return Talk(c == 0 ? "a4b_rohan_a" : c == 1 ? "a4b_rohan_b" : "a4b_rohan_c", rohan);
        if (c == 0) AddEvidence("Rohan knows Priya is at the NGO shelter by the river, and will take you there.");
        if (c == 2) AddEvidence("Rohan's cloud drive: copies of the emails, the forged orders and Priya's analysis.");
        kamala.lookTarget = cam;
        yield return Turn(kamala.EyePoint(), 0.6f);
        yield return Talk("a4b_kamala_3", kamala);
        EndTalk();
        SaveSystem.Data.allyRohan = true; SaveSystem.Save();
        yield return Narrate("a4b_vo_end");
        yield return CloseAct("", "");
    }

    IEnumerator LaptopClue()
    {
        BeginBusy();
        yield return Turn(laptop.position, 0.45f);
        yield return Talk("a4b_arjun_laptop");
        EndTalk();
        yield return ShowCard("MUNICIPAL OFFICE MAIL  -  rohan.kapoor (intern)",
            "From:  Deputy Commissioner, Land Records\nTo:  Councilor M. Deshpande\nSubject:  RE: Basti Nagar transfer\n\n" +
            "\"The original deed is no longer in the library. S. Reddy signed it out two weeks ago. We need it back from her before the hearing. " +
            "Keep the 1965 order in the file - nobody will question a stamped government document.\"");
        SaveSystem.Data.hasEmails = true; SaveSystem.Save();
        AddEvidence("MUNICIPAL EMAILS: senior officials knew the 1965 order was forged and planned to seize the original deed.");
        laptopRead = true;
    }

    /// <summary>The lid comes down with a click. Rohan stops where he is; his mother looks at him, not at Arjun.</summary>
    IEnumerator CloseLaptop()
    {
        BeginBusy();
        Quaternion r0 = laptopLid.rotation, r1 = Quaternion.Euler(0f, 0f, 0f);
        float i0 = laptopGlow != null ? laptopGlow.intensity : 0f;
        for (float t = 0f; t < 0.38f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.38f);
            laptopLid.rotation = Quaternion.Slerp(r0, r1, k * k);
            if (laptopGlow != null) laptopGlow.intensity = Mathf.Lerp(i0, 0f, k);
            yield return null;
        }
        laptopLid.rotation = r1;
        if (laptopGlow != null) laptopGlow.enabled = false;
        At(laptopLid.position, Tone(2400f, 0.05f, 90f, 0.6f), 0.8f, 0.6f, 9f);
        At(laptopLid.position, EvidenceFolio.Put(), 0.5f, 0.6f, 9f);
        laptopClosed = true;
        // Rohan, mid-stride, stops at the sound and looks over; then at the floor
        rohanPacing = false;
        if (rohan != null)
        {
            rohan.StopWalking();
            rohan.lookTarget = cam;
            rohanStoppedAtClick = true;
        }
        yield return Wait(0.7f);
        // his mother turns to him before anyone asks anything
        if (kamala != null && rohan != null) { kamala.lookTarget = HeadOf(rohan); kamalaLookedAtRohan = true; }
        yield return Wait(0.9f);
        if (rohan != null)
        {
            var down = new GameObject("RohanLooksDown").transform; down.SetParent(transform, false);
            down.position = rohan.transform.position + rohan.transform.forward * 0.8f;
            rohan.lookTarget = down;
        }
        // the player has the room: free to look, walk over, or pick up what is still on the floor
        BeginFree("Ask Rohan about the emails", rohan != null ? rohan.EyePoint() + Vector3.up * 0.25f : (Vector3?)null, "Rohan");
    }

    IEnumerator SmallClue(bool print)
    {
        BeginBusy();
        yield return Turn(print ? bootPrint.position : visitorPass.position, 0.4f);
        yield return Talk(print ? "a4b_arjun_print" : "a4b_arjun_pass");
        EndTalk();
        if (print) { printSeen = true; AddEvidence("Kapoor house: heavy muddy boot prints - the 'clerks' were hired muscle."); }
        else
        {
            passSeen = true; AddEvidence("Kapoor house: a Municipal Office visitor pass dropped by one of the intruders.");
            if (visitorPass != null) visitorPass.gameObject.SetActive(false);
        }
        // back to whatever Arjun was doing
        if (laptopClosed) BeginFree("Ask Rohan about the emails", rohan != null ? rohan.EyePoint() + Vector3.up * 0.25f : (Vector3?)null, "Rohan");
        else if (laptopRead) BeginFree("Close the laptop", laptopLid.position + Vector3.up * 0.1f, "Rohan's laptop");
        else BeginFree("Search the flat for anything they missed", null);
    }
}
