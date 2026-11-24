using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Act V's shelter at four in the morning: a former government school where Basti Nagar's families are sleeping.
/// A few sleeping places that say who lies there (a rolled blanket under a head, chappals beside a mat, a family's
/// papers pushed into a cloth bag), people awake doing one quiet thing each, out of step with one another, a lamp at
/// the top of the stairs and Priya's voice coming down them, and Priya upstairs with the 1965 file.
/// </summary>
public partial class Act5Story
{
    // read by the play-test route
    Transform priyaFile, priyaFileCover, stairVoice;
    bool priyaClosedFile, priyaHoldsFile;
    readonly List<Transform> sleepers = new List<Transform>();
    readonly List<CharacterNPC> awake = new List<CharacterNPC>();

    static Material Durrie(Color c) { var m = ProcTex.Mat(ProcTex.Get("fabric"), c, new Vector2(1.2f, 2.2f), 0.15f, 0.6f); return m; }

    /// <summary>A sleeper under a cotton blanket: a body-shaped lump, the head on a rolled blanket, hair showing.</summary>
    Transform Sleeper(Vector3 at, float yaw, float length, Color blanket, bool child, int seed)
    {
        var root = new GameObject(child ? "SleepingChild" : "Sleeper").transform; root.SetParent(transform, false);
        root.position = at; root.rotation = Quaternion.Euler(0f, yaw, 0f);
        var r = new System.Random(seed);
        float L = length, w = child ? 0.32f : 0.46f;
        // the body under the blanket: hips and shoulders higher than the legs, knees a little drawn up on one side
        var torso = GameKit.Box(root, "Blanket", new Vector3(0f, 0.11f, 0.05f), new Vector3(w, 0.2f, L * 0.62f), Durrie(blanket));
        torso.transform.localRotation = Quaternion.Euler(2f, 0f, (float)(r.NextDouble() - 0.5) * 6f);
        var legs = GameKit.Box(root, "BlanketLegs", new Vector3(0.03f, 0.08f, -L * 0.38f), new Vector3(w * 0.8f, 0.15f, L * 0.36f), Durrie(blanket * 0.95f));
        legs.transform.localRotation = Quaternion.Euler(-4f, (float)(r.NextDouble() - 0.5) * 14f, 0f);
        // the edge of the blanket folded back under the chin
        GameKit.Box(root, "BlanketFold", new Vector3(0f, 0.2f, L * 0.36f), new Vector3(w * 0.96f, 0.05f, 0.12f), Durrie(Color.Lerp(blanket, Color.white, 0.25f)));
        // a rolled blanket for a pillow, and the head on it, turned away (hair, an ear of skin)
        var roll = GameKit.Cyl(root, "RolledBlanket", new Vector3(0f, 0.06f, L * 0.5f), 0.075f, w * 0.9f, Durrie(new Color(0.55f, 0.5f, 0.42f)));
        roll.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        var head = GameKit.Sphere(root, "SleeperHead", new Vector3(0.02f, 0.17f, L * 0.47f), child ? 0.16f : 0.2f, GameKit.Lit(new Color(0.04f, 0.035f, 0.03f), 0.35f));
        head.transform.localScale = new Vector3(child ? 0.15f : 0.19f, child ? 0.15f : 0.18f, child ? 0.17f : 0.21f);
        GameKit.Sphere(root, "SleeperNeck", new Vector3(0.06f, 0.14f, L * 0.42f), 0.07f, GameKit.Lit(new Color(0.45f, 0.3f, 0.21f), 0.3f));
        foreach (var rr in root.GetComponentsInChildren<Renderer>()) rr.shadowCastingMode = ShadowCastingMode.On;
        sleepers.Add(torso.transform);
        return root;
    }

    void Chappals(Vector3 at, float yaw, Color c)
    {
        var m = GameKit.Lit(c, 0.25f);
        for (int i = 0; i < 2; i++)
        {
            var sole = GameKit.Box(transform, "Chappal", at + Quaternion.Euler(0f, yaw, 0f) * new Vector3(i * 0.12f - 0.06f, 0.008f, i * 0.04f), new Vector3(0.09f, 0.016f, 0.25f), m);
            sole.transform.rotation = Quaternion.Euler(0f, yaw + (i == 0 ? -6f : 9f), 0f);
            GameKit.Box(sole.transform, "Strap", new Vector3(0f, 1.2f, 0.18f), new Vector3(0.9f, 0.8f, 0.12f), GameKit.Lit(c * 0.6f, 0.3f));
        }
    }

    /// <summary>A family's papers pushed into a cloth bag: a ration card, a folded patta, a plastic sleeve, by the head of a mat.</summary>
    void PapersBag(Vector3 at, float yaw, Color cloth)
    {
        var bag = GameKit.Box(transform, "ClothBag", at + new Vector3(0f, 0.13f, 0f), new Vector3(0.34f, 0.26f, 0.12f), Durrie(cloth));
        bag.transform.rotation = Quaternion.Euler(-8f, yaw, 0f);
        var t = bag.transform;
        var paper = ProcTex.Mat(ProcTex.Get("paper"), new Color(0.88f, 0.85f, 0.74f), new Vector2(0.4f, 0.4f), 0.12f, 0.25f);
        var p1 = GameKit.Box(t, "FoldedPapers", new Vector3(-0.12f, 0.62f, 0f), new Vector3(0.45f, 0.5f, 0.3f), paper); p1.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);
        var card = GameKit.Box(t, "RationCard", new Vector3(0.18f, 0.55f, 0.05f), new Vector3(0.36f, 0.45f, 0.1f), GameKit.Lit(new Color(0.75f, 0.35f, 0.3f), 0.3f)); card.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
        GameKit.Box(t, "Sleeve", new Vector3(0.02f, 0.5f, -0.2f), new Vector3(0.6f, 0.7f, 0.05f), GameKit.Lit(new Color(0.8f, 0.85f, 0.9f), 0.85f));
    }

    void BuildSleepers()
    {
        // three families along the walls, the floor between them kept clear as a path to the stairs
        var faded = new[] { new Color(0.55f, 0.28f, 0.22f), new Color(0.3f, 0.36f, 0.42f), new Color(0.6f, 0.52f, 0.3f), new Color(0.38f, 0.42f, 0.3f) };
        // by the blackboard: a mother and two children on two durries, the children under one blanket
        Tex(B("SleepingMat", new Vector3(-3.6f, 0.012f, 3.5f), new Vector3(2.0f, 0.02f, 1.1f), faded[0], false, 0.05f), "fabric", 0.4f, faded[0]);
        Sleeper(new Vector3(-4.2f, 0.02f, 3.55f), -90f, 1.6f, faded[1], false, 1);
        Sleeper(new Vector3(-3.05f, 0.02f, 3.5f), -95f, 1.05f, faded[2], true, 2);
        Sleeper(new Vector3(-2.75f, 0.02f, 3.25f), -80f, 0.95f, faded[2] * 0.95f, true, 3);
        Chappals(new Vector3(-4.95f, 0f, 2.85f), 0f, new Color(0.15f, 0.12f, 0.1f));
        Chappals(new Vector3(-2.2f, 0f, 2.9f), 30f, new Color(0.55f, 0.15f, 0.2f));
        PapersBag(new Vector3(-5.3f, 0.02f, 3.6f), 80f, new Color(0.25f, 0.4f, 0.35f));
        // along the west wall: an old man asleep on a mat with his stick beside him
        Tex(B("SleepingMat", new Vector3(-6.3f, 0.012f, -1.2f), new Vector3(0.95f, 0.02f, 1.9f), faded[3], false, 0.05f), "fabric", 0.4f, faded[3]);
        Sleeper(new Vector3(-6.3f, 0.02f, -1.3f), 180f, 1.65f, new Color(0.62f, 0.6f, 0.55f), false, 4);
        GameKit.Box(transform, "WalkingStick", new Vector3(-5.75f, 0.02f, -1.1f), new Vector3(0.025f, 0.025f, 0.9f), GameKit.Lit(new Color(0.32f, 0.2f, 0.1f), 0.4f)).transform.rotation = Quaternion.Euler(0f, 8f, 0f);
        Chappals(new Vector3(-5.6f, 0f, -2.4f), -20f, new Color(0.3f, 0.25f, 0.2f));
        // by the south wall, under the window: a couple, a steel trunk at their heads with their things on it
        Tex(B("SleepingMat", new Vector3(1.2f, 0.012f, -4.2f), new Vector3(2.1f, 0.02f, 1.0f), faded[1], false, 0.05f), "fabric", 0.4f, faded[1]);
        Sleeper(new Vector3(0.6f, 0.02f, -4.2f), 90f, 1.65f, faded[0] * 1.1f, false, 5);
        Prop("metal_jug", new Vector3(-0.4f, 0f, -4.5f), 40f, 1.0f);
        var trunk = B("SteelTrunk", new Vector3(2.75f, 0.2f, -4.3f), new Vector3(0.75f, 0.4f, 0.45f), new Color(0.35f, 0.4f, 0.45f), true, 0.5f, 0.7f);
        B("TrunkLatch", new Vector3(2.75f, 0.32f, -4.07f), new Vector3(0.06f, 0.08f, 0.02f), new Color(0.7f, 0.6f, 0.3f), false, 0.7f, 0.9f);
        PapersBag(new Vector3(2.55f, 0.4f, -4.3f), 0f, new Color(0.55f, 0.45f, 0.25f));
        Chappals(new Vector3(1.4f, 0f, -3.55f), 180f, new Color(0.2f, 0.2f, 0.3f));
        // ---- the people still awake at four: each with one thing to do, out of step with the others
        var watcherW = Npc(new Vector3(-0.9f, 0f, 3.9f), 200f, NpcStyle.CrowdWoman(1211), "AwakeMother", false);
        watcherW.Sit(new Vector3(-0.9f, 0.46f, 4.05f), 200f, 0.4f, true);            // on the stacked desk by the children, watching them
        watcherW.lookTarget = sleepers.Count > 1 ? sleepers[1] : null;
        awake.Add(watcherW);
        var oldW = Npc(new Vector3(-5.2f, 0f, 0.4f), 100f, NpcStyle.CrowdWoman(1217), "GrandmotherAwake", false);
        oldW.Sit(new Vector3(-5.35f, 0.45f, 0.45f), 100f, 0.38f, true);              // on a school chair, telling her beads
        oldW.lookTarget = null;
        awake.Add(oldW);
        var man = Npc(new Vector3(3.6f, 0f, 0.8f), 230f, NpcStyle.Crowd(1219), "AwakeMan", false);
        man.Sit(new Vector3(3.6f, 0.45f, 0.9f), 230f, 0.42f, true);                  // on the plastic chair, fanning himself
        man.lookTarget = null;
        awake.Add(man);
        StartCoroutine(ShelterLife());
        // light to read them by at four in the morning: a hurricane lantern turned low beside the mother, and moonlight
        // through the south windows laid across the floor
        Glow("HurricaneLantern", new Vector3(-1.55f, 0.18f, 3.55f), new Vector3(0.12f, 0.2f, 0.12f), new Color(1f, 0.66f, 0.32f), 3f);
        var lan = PL(new Vector3(-1.55f, 0.55f, 3.4f), new Color(1f, 0.64f, 0.34f), 1.8f, 5.5f);
        CinematicLook.Shadows(lan, 0.7f, 0.03f, 0.4f);
        var moon = new GameObject("ShelterMoonlight").AddComponent<Light>(); moon.transform.SetParent(transform, false);
        moon.transform.position = new Vector3(0.5f, 3.1f, -5.2f); moon.transform.rotation = Quaternion.Euler(38f, 8f, 0f);
        moon.type = LightType.Spot; moon.spotAngle = 85f; moon.innerSpotAngle = 35f; moon.range = 14f; moon.intensity = 3.2f; moon.color = new Color(0.55f, 0.65f, 1f);
        CinematicLook.Shadows(moon, 0.75f, 0.03f, 0.5f);
        for (int i = 0; i < 3; i++) Glow("SouthWindow", new Vector3(-3.5f + i * 3.5f, 2.2f, -4.97f), new Vector3(1.2f, 1.1f, 0.03f), new Color(0.35f, 0.45f, 0.75f), 0.9f);
    }

    /// <summary>The lamp at the head of the stairs throws a pool of warm light down them, and Priya's voice comes down too.</summary>
    void BuildStairLight()
    {
        var l = new GameObject("StairPool").AddComponent<Light>();
        l.transform.SetParent(transform, false);
        l.transform.position = new Vector3(6.2f, 3.0f, 4.4f); l.transform.rotation = Quaternion.Euler(62f, 180f, 0f);
        l.type = LightType.Spot; l.spotAngle = 80f; l.innerSpotAngle = 30f; l.range = 6f; l.intensity = 4f; l.color = new Color(1f, 0.68f, 0.38f);
        CinematicLook.Shadows(l, 0.8f, 0.03f, 0.5f);
        Glow("StairLampGlow", new Vector3(6.2f, 2.35f, 4.9f), new Vector3(1.0f, 0.12f, 0.02f), new Color(1f, 0.7f, 0.4f), 1.6f);
        var v = new GameObject("PriyasVoiceDownTheStairs"); v.transform.SetParent(transform, false); v.transform.position = new Vector3(6.2f, 2.2f, 4.85f);
        stairVoice = v.transform;
    }

    /// <summary>A trestle table upstairs with the 1965 file open on it, a pen, a glass of cold tea and her phone.</summary>
    void BuildPriyasDesk()
    {
        var deskAt = U + new Vector3(-0.15f, 0f, 1.62f);
        var top = B("PriyaDesk", deskAt + new Vector3(0f, 0.72f, 0f), new Vector3(1.0f, 0.04f, 0.55f), new Color(0.42f, 0.3f, 0.18f), true, 0.35f);
        Tex(top, "wood", 0.8f, new Color(0.42f, 0.3f, 0.18f));
        foreach (float lx in new[] { -0.45f, 0.45f }) foreach (float lz in new[] { -0.23f, 0.23f })
            B("DeskLeg", deskAt + new Vector3(lx, 0.35f, lz), new Vector3(0.04f, 0.7f, 0.04f), new Color(0.3f, 0.3f, 0.32f), false, 0.5f, 0.6f);
        // the file: a buff government file cover, tied with tape, open on the table with its pages
        var file = new GameObject("Act5_1965File").transform; file.SetParent(transform, false);
        file.position = deskAt + new Vector3(0.02f, 0.742f, -0.06f); file.rotation = Quaternion.Euler(0f, 0f, 0f);
        priyaFile = file;
        var buff = GameKit.Lit(new Color(0.72f, 0.6f, 0.42f), 0.15f);
        GameKit.Box(file, "FileBack", new Vector3(0f, 0.002f, 0f), new Vector3(0.24f, 0.004f, 0.34f), buff);
        var pages = ProcTex.Mat(ProcTex.Get("paper"), new Color(0.86f, 0.82f, 0.68f), new Vector2(0.5f, 0.6f), 0.12f, 0.25f);
        GameKit.Box(file, "FilePages", new Vector3(0f, 0.008f, 0f), new Vector3(0.22f, 0.008f, 0.32f), pages);
        GameKit.Solid(GameKit.Text(file, "GOVT. OF CHANDNIPUR - LAND ACQUISITION", new Vector3(0f, 0.0125f, 0.13f), Quaternion.Euler(90f, 0f, 0f), 0.0065f, new Color(0.15f, 0.12f, 0.1f)));
        GameKit.Solid(GameKit.Text(file, "Order No. 114 / 1965  -  Basti Nagar", new Vector3(0f, 0.0125f, 0.112f), Quaternion.Euler(90f, 0f, 0f), 0.006f, new Color(0.15f, 0.12f, 0.1f)));
        var stamp = GameKit.Cyl(file, "FileStamp", new Vector3(0.06f, 0.0125f, -0.09f), 0.025f, 0.0005f, GameKit.Lit(new Color(0.45f, 0.15f, 0.4f), 0.2f));
        stamp.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        var cover = new GameObject("FileCoverHinge").transform; cover.SetParent(file, false);
        cover.localPosition = new Vector3(-0.12f, 0.006f, 0f); cover.localRotation = Quaternion.Euler(0f, 0f, 178f);        // laid open to the left
        GameKit.Box(cover, "FileCover", new Vector3(0.12f, 0.002f, 0f), new Vector3(0.24f, 0.004f, 0.34f), buff);
        GameKit.Box(cover, "FileTape", new Vector3(0.235f, 0.004f, 0f), new Vector3(0.01f, 0.002f, 0.02f), GameKit.Lit(new Color(0.75f, 0.2f, 0.15f), 0.2f));
        priyaFileCover = cover;
        var pen = GameKit.Cyl(transform, "Pen", deskAt + new Vector3(0.22f, 0.748f, 0.02f), 0.005f, 0.14f, GameKit.Lit(new Color(0.08f, 0.12f, 0.35f), 0.6f));
        pen.transform.rotation = Quaternion.Euler(90f, 30f, 0f);
        Cy("ColdTea", deskAt + new Vector3(-0.36f, 0.79f, 0.1f), 0.032f, 0.09f, new Color(0.62f, 0.62f, 0.64f), 0.7f, 0.9f);
        var ph = GameKit.Box(transform, "PriyaPhone", deskAt + new Vector3(0.36f, 0.746f, 0.12f), new Vector3(0.072f, 0.008f, 0.15f), GameKit.Lit(new Color(0.05f, 0.05f, 0.06f), 0.75f, 0.3f));
        ph.transform.rotation = Quaternion.Euler(0f, -18f, 0f);
        // a little fill on her face and hands from the lamp's side: she is lit by the oil lamp
        PL(U + new Vector3(0.6f, 1.5f, 0.3f), new Color(1f, 0.72f, 0.48f), 0.7f, 3.5f);
        // she is reading, her back half to the door, her eyes on the page
        priya.freezeFacing = true;
        priya.transform.rotation = Quaternion.Euler(0f, 10f, 0f);
        priya.lookTarget = file;
        priya.lean = 10f;
        var hl = new GameObject("PriyaHandL").transform; hl.SetParent(transform, false); hl.position = file.position + new Vector3(-0.1f, 0.03f, -0.1f);
        var hr = new GameObject("PriyaHandR").transform; hr.SetParent(transform, false); hr.position = file.position + new Vector3(0.14f, 0.02f, -0.06f);
        priyaHands = new[] { hl, hr };
        priya.Hold(0, hl, 0.01f); priya.Hold(1, hr, 0.01f);
    }
    Transform[] priyaHands;

    /// <summary>She hears him, turns her head, shuts the file and gathers it to her chest before she says a word.</summary>
    IEnumerator PriyaGuardsTheFile()
    {
        priya.lookTarget = cam;                         // her head first
        yield return Wait(0.45f);
        // the cover comes over (her left hand), then both hands take the file up
        Quaternion c0 = priyaFileCover.localRotation, c1 = Quaternion.Euler(0f, 0f, 0f);
        Vector3 h0 = priyaHands[0].position;
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.5f);
            priyaFileCover.localRotation = Quaternion.Slerp(c0, c1, k);
            priyaHands[0].position = Vector3.Lerp(h0, priyaFile.position + new Vector3(0.05f, 0.05f + 0.12f * Mathf.Sin(k * Mathf.PI), -0.05f), k);
            yield return null;
        }
        sfx.PlayOneShot(EvidenceFolio.Put(), 0.4f);
        priyaClosedFile = true;
        // the file to her chest: her hands on its edges, held flat against her
        Vector3 f0 = priyaFile.position; Quaternion r0 = priyaFile.rotation;
        for (float t = 0f; t < 0.7f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.7f);
            Vector3 chest = priya.transform.position + Vector3.up * 1.15f + priya.transform.forward * 0.16f;
            priyaFile.position = Vector3.Lerp(f0, chest, k);
            priyaFile.rotation = Quaternion.Slerp(r0, Quaternion.LookRotation(Vector3.up, priya.transform.forward), k);     // upright, its cover outwards
            priyaHands[0].position = priyaFile.position - priyaFile.right * 0.11f;
            priyaHands[1].position = priyaFile.position + priyaFile.right * 0.11f;
            yield return null;
        }
        priya.lean = 0f;
        priyaHoldsFile = true;
        // and now she turns to face him, the file held to her
        priya.freezeFacing = false;
        StartCoroutine(KeepFileHeld());
        yield return Wait(0.6f);
    }

    /// <summary>While she talks the file stays against her (her body turns, the file and her hands go with it).</summary>
    IEnumerator KeepFileHeld()
    {
        while (priyaHoldsFile && priya != null)
        {
            Vector3 chest = priya.transform.position + Vector3.up * 1.15f + priya.transform.forward * 0.17f;
            priyaFile.position = Vector3.Lerp(priyaFile.position, chest, Time.deltaTime * 10f);
            priyaFile.rotation = Quaternion.Slerp(priyaFile.rotation, Quaternion.LookRotation(Vector3.up, priya.transform.forward), Time.deltaTime * 10f);
            priyaHands[0].position = priyaFile.position - priyaFile.right * 0.11f;
            priyaHands[1].position = priyaFile.position + priyaFile.right * 0.11f;
            yield return null;
        }
    }

    /// <summary>The awake ones: the mother watching her children, the grandmother's beads, the man's fan; sleepers breathing.</summary>
    IEnumerator ShelterLife()
    {
        var goals = new Transform[awake.Count];
        for (int i = 0; i < awake.Count; i++) { goals[i] = new GameObject("AwakeHand" + i).transform; goals[i].SetParent(transform, false); }
        var fan = GameKit.Box(transform, "HandFan", Vector3.zero, new Vector3(0.22f, 0.005f, 0.16f), GameKit.Lit(new Color(0.75f, 0.68f, 0.5f), 0.2f)).transform;
        float t0 = Time.time;
        var phase = new float[sleepers.Count];
        for (int i = 0; i < phase.Length; i++) phase[i] = i * 1.37f;
        var baseScale = new Vector3[sleepers.Count];
        for (int i = 0; i < sleepers.Count; i++) baseScale[i] = sleepers[i].localScale;
        for (int i = 0; i < awake.Count; i++) if (awake[i] != null) awake[i].Hold(1, goals[i], 0.6f);
        while (this != null)
        {
            float t = Time.time - t0;
            // slow breathing, each at their own rate
            for (int i = 0; i < sleepers.Count; i++)
                if (sleepers[i] != null) sleepers[i].localScale = new Vector3(baseScale[i].x, baseScale[i].y * (1f + 0.035f * Mathf.Sin(t * (1.45f + i * 0.11f) + phase[i])), baseScale[i].z);
            // the mother's hand on the nearer child's blanket, moving a little now and then
            if (awake.Count > 0 && awake[0] != null && sleepers.Count > 1)
                goals[0].position = sleepers[1].position + Vector3.up * 0.12f + Vector3.right * (0.06f * Mathf.Sin(t * 0.7f)) + Vector3.forward * 0.15f;
            // the grandmother's beads: her hand in her lap, the fingers' small steady movement
            if (awake.Count > 1 && awake[1] != null)
            {
                var g = awake[1];
                goals[1].position = g.transform.position + Vector3.up * 0.62f + g.transform.forward * 0.24f + g.transform.up * 0.012f * Mathf.Sin(t * 5.2f);
            }
            // the man fans himself with a folded paper, slowly, in the heat that does not lift at night
            if (awake.Count > 2 && awake[2] != null)
            {
                var m = awake[2];
                Vector3 face = m.EyePoint() + m.transform.forward * 0.28f - Vector3.up * 0.12f;
                goals[2].position = face + m.transform.right * (0.07f * Mathf.Sin(t * 3.1f));
                fan.position = goals[2].position + Vector3.up * 0.08f;
                fan.rotation = Quaternion.LookRotation(m.transform.forward) * Quaternion.Euler(90f, 0f, 18f * Mathf.Sin(t * 3.1f));
            }
            yield return null;
        }
    }

    /// <summary>Priya's voice, low, from the top of the stairs: she is reading her file aloud to herself.</summary>
    void PriyaUpstairsVoice()
    {
        if (stairVoice == null) return;
        var c = VO("a5_priya_upstairs");
        if (c == null) return;
        var a = stairVoice.gameObject.GetComponent<AudioSource>() ?? stairVoice.gameObject.AddComponent<AudioSource>();
        a.clip = c; a.spatialBlend = 1f; a.minDistance = 1.5f; a.maxDistance = 14f; a.volume = 0.55f; a.dopplerLevel = 0f;
        a.rolloffMode = AudioRolloffMode.Logarithmic;
        var lp = stairVoice.gameObject.GetComponent<AudioLowPassFilter>() ?? stairVoice.gameObject.AddComponent<AudioLowPassFilter>();
        lp.cutoffFrequency = 2600f;                        // through the doorway and down the stairwell
        a.Play();
        sub = DialogueDB.Speaker("a5_priya_upstairs") + ":  " + DialogueDB.Text("a5_priya_upstairs");
        subUntil = Time.time + c.length + 0.3f;
    }
}
