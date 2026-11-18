using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The Curator's interludes, played as cutscenes in his Gallery between the acts (dialogue: Tools/acts_dialogue.py).
///   scene 0 (before Act I)  "Welcome", the prologue. His first appearance: he walks to the bookcase, takes down
///                           "Shadows of Chandni", turns and speaks to the investigator by the light of his candle.
///                           Afterwards the game returns to Priya's flat and Act I begins.
///   scene 1 (after Act II), scene 2 (after Act III), scene 3 (after Act IV): he walks to the table with the candle
///                           and the book, and each scene has an exhibit (a key, a broken kettle, the deed, three figures).
/// Each scene ends the same way: he raises the candle to his face, blows it out, and the hall goes dark.
/// The camera leaves Arjun's head for these scenes and works as a film camera: hard cuts between set-ups (wide,
/// medium, low candlelit close-up, orbit, profile, eyes-only close-up, inserts) with a slight handheld drift.
/// Enter skips the whole scene, Space skips one line, Esc pauses.
/// The Act V Gallery is the same room; there the Curator speaks without a candle.
/// </summary>
public class CuratorInterlude : ActStoryBase
{
    public int scene;
    public Action onDone;

    protected override int ActNumber { get { return scene + 1; } }
    protected override string ActTitle { get { return ""; } }
    protected override string ActSubtitle { get { return ""; } }
    protected override LookPreset Look { get { return LookPreset.Apartment; } }

    /// <summary>
    /// Plays interlude `scene` (0 = the prologue before Act I, 1-3 = after Acts II-IV), then calls next.
    /// returnToRoom: leave the Act I flat standing (the Gallery is built far away from it) and put everything back
    /// afterwards, so Act I can start; otherwise the previous act's world is thrown away, as between acts.
    /// </summary>
    public static CuratorInterlude Play(int scene, PriyaRoomBuilder builder, PriyaRoomPlayer player, Action next, bool returnToRoom = false)
    {
        var old = builder.ExtraRoot;
        var root = new GameObject("CuratorInterlude_" + scene);
        CuratorInterlude s;
        if (returnToRoom)
        {
            root.transform.position = new Vector3(0f, 0f, -400f);           // well away from the flat
            s = root.AddComponent<CuratorInterlude>();
            s.returnToRoom = true;
            s.Snapshot(player);
        }
        else
        {
            player.transform.SetParent(root.transform, true);
            builder.Root.gameObject.SetActive(false);
            s = root.AddComponent<CuratorInterlude>();
        }
        builder.ExtraRoot = root;
        if (old != null) Destroy(old);
        GameState.ResetForNewScene();
        s.builder = builder; s.player = player; s.scene = scene; s.onDone = next; s.resume = false;
        return s;
    }

    // ------------------------------------------------- the Act I flat, kept as it was during the prologue
    bool returnToRoom;
    UnityEngine.Rendering.AmbientMode sAmbMode; Color sSky, sEq, sGround, sBg; float sRefl, sFogDen, sAmbInt; bool sFog; FogMode sFogMode; Color sFogCol;
    Material sSkybox; CameraClearFlags sClear;
    UnityEngine.Rendering.Universal.DepthOfField sDof; float sDofDist;
    readonly List<Light> sSuns = new List<Light>();

    void Snapshot(PriyaRoomPlayer p)
    {
        sAmbMode = RenderSettings.ambientMode; sSky = RenderSettings.ambientSkyColor; sEq = RenderSettings.ambientEquatorColor; sGround = RenderSettings.ambientGroundColor;
        sRefl = RenderSettings.reflectionIntensity; sAmbInt = RenderSettings.ambientIntensity; sFog = RenderSettings.fog; sFogMode = RenderSettings.fogMode; sFogCol = RenderSettings.fogColor; sFogDen = RenderSettings.fogDensity;
        sSkybox = RenderSettings.skybox;
        var c = p.cam.GetComponent<Camera>(); sClear = c.clearFlags; sBg = c.backgroundColor;
        var af = p.cam.GetComponent<AutoFocus>(); if (af != null) { sDof = af.dof; sDofDist = af.defaultDistance; }
        // the flat's sun would shine straight through the Gallery
        foreach (var l in FindObjectsByType<Light>())
            if (l.type == LightType.Directional && l.enabled) { l.enabled = false; sSuns.Add(l); }
    }

    void RestoreRoom()
    {
        RenderSettings.ambientMode = sAmbMode; RenderSettings.ambientSkyColor = sSky; RenderSettings.ambientEquatorColor = sEq; RenderSettings.ambientGroundColor = sGround;
        RenderSettings.reflectionIntensity = sRefl; RenderSettings.ambientIntensity = sAmbInt; RenderSettings.fog = sFog; RenderSettings.fogMode = sFogMode; RenderSettings.fogColor = sFogCol; RenderSettings.fogDensity = sFogDen;
        RenderSettings.skybox = sSkybox;
        camComp.clearFlags = sClear; camComp.backgroundColor = sBg;
        var af = cam.GetComponent<AutoFocus>(); if (af != null && sDof != null) { af.dof = sDof; af.defaultDistance = sDofDist; }
        foreach (var l in sSuns) if (l != null) l.enabled = true;
    }

    /// <summary>A point given in the Gallery's own coordinates, in the world.</summary>
    Vector3 At(Vector3 local) { return transform.TransformPoint(local); }

    // ------------------------------------------------------------------ cast & set
    CuratorGallery gal;
    CharacterNPC curator;
    StoryBookModel book;
    Transform leftGoal, rightGoal, candle, focus, exhibit;
    GameObject candleFlame; Light candleLight;
    float lightLevel;              // hall lights 0..1 (mirrored into the ambient every frame)
    bool done;
    AudioSource tick;
    AudioClip droneClip, tickClip, stingClip, boomClip, matchClip, blowClip, pageClip;

    static readonly Vector3 TableSpot = new Vector3(0.3f, 0f, 3.05f);       // behind the table, facing the hall
    static readonly Vector3 ShelfSpot = new Vector3(CuratorGallery.ShelfBookSlot.x, 0f, CuratorGallery.ShelfFront - 0.52f);
    static readonly Vector3 BookOnTable = new Vector3(0.02f, 0.945f + StoryBookModel.T / 2f, 2.42f);
    static readonly Vector3 ExhibitSpot = new Vector3(1.05f, 0.945f, 2.25f);
    static readonly Vector3 CandleHold = new Vector3(-0.21f, 1.08f, 0.3f), CandleRaised = new Vector3(-0.05f, 1.44f, 0.27f);
    static readonly Vector3 BookHold = new Vector3(0.22f, 1.06f, 0.3f);

    protected override void BuildWorld()
    {
        gal = CuratorGallery.Build(transform, Vector3.zero, scene == 0, true);
        CuratorGallery.Mood(camComp, 0f);
        gal.SetLights(0f);
        lightLevel = 0f;
        PriyaRoomPlayer.Surface = 0;

        // the hall remembers: each time he returns, the draped figures stand a little nearer to his table
        for (int i = 0; i < gal.drapes.Count; i++)
        {
            var d = gal.drapes[i];
            d.localPosition = Vector3.Lerp(d.localPosition, new Vector3(Mathf.Sign(d.localPosition.x) * 3.2f, 0f, d.localPosition.z * 0.5f + 2f), 0.13f * scene);
        }
        if (scene == 3) gal.drapes[1].localPosition = new Vector3(3.4f, 0f, 5.6f);     // and tonight one is right behind him

        Vector3[] starts = { new Vector3(-6.2f, 0f, -3.4f), new Vector3(-2.6f, 0f, 7.3f), new Vector3(3.2f, 0f, 7.5f), new Vector3(-0.6f, 0f, 7.9f) };
        float[] yaws = { 20f, 140f, 220f, 180f };
        curator = Npc(starts[scene], yaws[scene], Cast.Curator(), "The Curator", false);
        curator.VoiceFlat(true);
        curator.lookTarget = null;

        leftGoal = Goal("LeftHandGoal", CandleHold);
        rightGoal = Goal("RightHandGoal", BookHold);
        candle = BuildCandle(leftGoal);
        curator.Hold(0, leftGoal, 0.02f);

        if (scene == 0) book = gal.shelfBook;          // still on the shelf: he fetches it
        else
        {
            book = StoryBookModel.Make(transform, Chapter());
            HoldBook(0.02f);
        }
        if (book != null) book.pageText.text = Chapter();
        BuildExhibit();

        var f = new GameObject("FocusPoint"); f.transform.SetParent(transform, false); focus = f.transform;
        if (!returnToRoom) Teleport(At(new Vector3(0f, 0.1f, -13f)), 0f);

        droneClip = Drone(); tickClip = Tick(); stingClip = Sting(); boomClip = Tone(48f, 2.4f, 1.6f, 0.9f);
        matchClip = Noise(0.35f, 0.5f, 18f, true); blowClip = Noise(0.7f, 0.12f, 5f, false); pageClip = Noise(0.25f, 0.35f, 14f, true);
        ambience.clip = droneClip; ambience.Play();
        tick = gameObject.AddComponent<AudioSource>(); tick.clip = tickClip; tick.loop = true; tick.volume = 0f; tick.Play();
    }

    Transform Goal(string n, Vector3 local)
    {
        var g = new GameObject(n).transform;
        g.SetParent(curator.transform, false);
        g.localPosition = local;
        return g;
    }

    Transform BuildCandle(Transform hand)
    {
        var c = new GameObject("CuratorCandle").transform;
        c.SetParent(hand, false);
        c.localPosition = new Vector3(0.02f, 0.03f, 0.05f);
        var brass = GameKit.Lit(new Color(0.75f, 0.55f, 0.22f), 0.75f, 1f);
        GameKit.Cyl(c, "Dish", Vector3.zero, 0.065f, 0.012f, brass);
        GameKit.Cyl(c, "Socket", new Vector3(0, 0.02f, 0), 0.022f, 0.03f, brass);
        var ring = GameKit.Box(c, "Handle", new Vector3(-0.07f, 0.0f, 0), new Vector3(0.03f, 0.012f, 0.02f), brass);
        GameKit.Cyl(c, "Wax", new Vector3(0, 0.1f, 0), 0.017f, 0.14f, GameKit.Lit(new Color(0.93f, 0.9f, 0.82f), 0.25f));
        GameKit.Box(c, "Drip", new Vector3(0.016f, 0.13f, 0.004f), new Vector3(0.006f, 0.04f, 0.006f), GameKit.Lit(new Color(0.93f, 0.9f, 0.82f), 0.35f));
        candleFlame = GameKit.Sphere(c, "Flame", new Vector3(0, 0.195f, 0), 0.03f, GameKit.Glow(new Color(1f, 0.8f, 0.4f), new Color(1f, 0.72f, 0.32f), 10f));
        candleFlame.transform.localScale = new Vector3(0.022f, 0.045f, 0.022f);
        candleFlame.AddComponent<FlameWobble>();
        candleFlame.SetActive(false);
        candleLight = GameKit.PointLight(c, new Vector3(0, 0.26f, 0.02f), new Color(1f, 0.66f, 0.34f), 0f, 4.5f);
        candleLight.shadows = LightShadows.Soft;          // his shadow climbs the bookcase behind him
        var fl = candleLight.gameObject.AddComponent<CandleFlicker>(); fl.full = 2.6f; fl.level = 0f; fl.dipChance = 0.15f;
        ring.name = "Handle";
        return c;
    }

    void HoldBook(float blend)
    {
        book.root.SetParent(rightGoal, true);
        book.root.localPosition = new Vector3(0.0f, 0.1f, 0.06f);
        book.root.localRotation = Quaternion.Euler(0, -90f, 0);       // cover towards whoever he is talking to
        curator.Hold(1, rightGoal, blend);
    }

    string Chapter()
    {
        string[] c = { "CHAPTER ONE\n\nThe Empty Room", "CHAPTER TWO\n\nThe Streets Know", "CHAPTER THREE\n\nThe File", "CHAPTER FOUR\n\nThe Web" };
        return c[Mathf.Clamp(scene, 0, 3)] + "\n\n" + (scene == 0 ? "a key, a note,\na door no one knocked on" : scene == 1 ? "a kettle, a drive,\na black car" : scene == 2 ? "a deed, a cage,\na priest's question" : "three doors,\none witness");
    }

    // A small object on the table for each scene, filmed in an insert when he mentions it.
    void BuildExhibit()
    {
        exhibit = new GameObject("Exhibit").transform;
        exhibit.SetParent(transform, false);
        exhibit.localPosition = ExhibitSpot;
        var brass = GameKit.Lit(new Color(0.78f, 0.58f, 0.24f), 0.7f, 1f);
        if (scene == 0)          // the key to Priya's flat
        {
            exhibit.localRotation = Quaternion.Euler(0, 35f, 0);
            GameKit.Cyl(exhibit, "KeyBow", new Vector3(0, 0.006f, -0.045f), 0.022f, 0.008f, brass);
            GameKit.Box(exhibit, "KeyShaft", new Vector3(0, 0.006f, 0.01f), new Vector3(0.008f, 0.008f, 0.08f), brass);
            GameKit.Box(exhibit, "KeyBit", new Vector3(0.008f, 0.006f, 0.04f), new Vector3(0.014f, 0.007f, 0.016f), brass);
            GameKit.Box(exhibit, "KeyTag", new Vector3(-0.04f, 0.003f, -0.07f), new Vector3(0.04f, 0.003f, 0.06f), GameKit.Lit(new Color(0.85f, 0.8f, 0.68f), 0.1f));
            GameKit.Solid(GameKit.Text(exhibit, "3-B", new Vector3(-0.04f, 0.0055f, -0.07f), Quaternion.Euler(90f, 0, 0), 0.014f, new Color(0.2f, 0.05f, 0.04f)));
        }
        else if (scene == 1)     // Vikram's kettle, knocked on its side, the lid off
        {
            var aluminium = GameKit.Lit(new Color(0.62f, 0.6f, 0.56f), 0.55f, 0.85f);
            var body = Turned("KettleBody", Vector3.zero, new[] { new Vector2(0f, 0.07f), new Vector2(0.03f, 0.085f), new Vector2(0.1f, 0.08f), new Vector2(0.14f, 0.05f), new Vector2(0.15f, 0.0f) }, aluminium, 20);
            body.transform.SetParent(exhibit, false);
            body.transform.localPosition = new Vector3(0, 0.08f, 0); body.transform.localRotation = Quaternion.Euler(0, 0, 0);    // lying on its side
            var spout = GameKit.Box(exhibit, "Spout", new Vector3(0.0f, 0.14f, 0.09f), new Vector3(0.018f, 0.018f, 0.1f), aluminium);
            spout.transform.localRotation = Quaternion.Euler(-30f, 0, 0);
            var lid = GameKit.Cyl(exhibit, "Lid", new Vector3(-0.16f, 0.006f, 0.1f), 0.05f, 0.01f, aluminium);
            lid.transform.localRotation = Quaternion.Euler(0, 0, 8f);
            for (int i = 0; i < 5; i++)
                GameKit.Box(exhibit, "Dent", new Vector3(0.05f + i * 0.02f, 0.165f - i * 0.004f, -0.02f + i * 0.015f), new Vector3(0.012f, 0.004f, 0.01f), GameKit.Lit(new Color(0.3f, 0.28f, 0.25f), 0.3f));
        }
        else if (scene == 2)     // the original deed, 1942, under a red seal
        {
            exhibit.localRotation = Quaternion.Euler(0, -12f, 0);
            GameKit.Box(exhibit, "Deed", new Vector3(0, 0.002f, 0), new Vector3(0.21f, 0.002f, 0.3f), GameKit.Lit(new Color(0.8f, 0.72f, 0.52f), 0.1f));
            GameKit.Solid(GameKit.Text(exhibit, "DEED OF GRANT\n1942", new Vector3(0, 0.0035f, 0.09f), Quaternion.Euler(90f, 0, 0), 0.018f, new Color(0.18f, 0.08f, 0.04f)));
            GameKit.Solid(GameKit.Text(exhibit, "Basti Nagar, in perpetuity", new Vector3(0, 0.0035f, 0.02f), Quaternion.Euler(90f, 0, 0), 0.009f, new Color(0.22f, 0.1f, 0.05f)));
            GameKit.Cyl(exhibit, "Seal", new Vector3(0.05f, 0.004f, -0.09f), 0.022f, 0.005f, GameKit.Lit(new Color(0.55f, 0.05f, 0.04f), 0.5f));
        }
        else                     // three figures: a builder, a boy, a woman in khaki
        {
            Color[] coat = { new Color(0.32f, 0.33f, 0.36f), new Color(0.2f, 0.4f, 0.7f), new Color(0.55f, 0.48f, 0.3f) };
            for (int i = 0; i < 3; i++)
            {
                var p = new Vector3(-0.12f + i * 0.12f, 0, 0);
                var m = GameKit.Lit(coat[i], 0.5f);
                GameKit.Cyl(exhibit, "FigureBase", p + new Vector3(0, 0.005f, 0), 0.03f, 0.01f, GameKit.Lit(new Color(0.15f, 0.1f, 0.07f), 0.6f));
                GameKit.Cone(exhibit, "Figure", p + new Vector3(0, 0.01f, 0), 0.025f, i == 1 ? 0.07f : 0.09f, m, 12);
                GameKit.Sphere(exhibit, "FigureHead", p + new Vector3(0, i == 1 ? 0.085f : 0.105f, 0), 0.026f, GameKit.Lit(new Color(0.75f, 0.6f, 0.5f), 0.4f));
            }
        }
    }

    // ------------------------------------------------------------------ flow
    protected override IEnumerator Run()
    {
        phase = Phase.Intro;                 // keeps the cinema bars up for the whole scene
        fade = 1f; fadeTarget = 1f;
        DetachCamera();
        StartCoroutine(scene == 0 ? Welcome() : Return());
        while (!done && !skipAll) yield return null;
        if (!done)
        {
            StopAllCoroutines();
            StartCoroutine(SkipOut());
        }
    }

    IEnumerator SkipOut()
    {
        curator.Silence(); sub = null;
        fadeSpeed = 2.5f; fadeTarget = 1f;
        while (fade < 0.99f) yield return null;
        yield return new WaitForSeconds(0.3f);
        Finish();
    }

    /// <summary>His first appearance (after Act I): the walk to the bookcase and the book.</summary>
    IEnumerator Welcome()
    {
        yield return new WaitForSeconds(0.8f);
        // a match in the dark, then the flame in his hand
        sfx.PlayOneShot(matchClip, 0.7f);
        yield return new WaitForSeconds(0.25f);
        Ignite(1f);
        fade = 0f; fadeTarget = 0f;
        StartCoroutine(RampAmbience(0f, 0.55f, 4f));
        Cut(Move(5.5f,
            k => candle.position + curator.transform.forward * Mathf.Lerp(0.42f, 1.7f, k) + curator.transform.right * Mathf.Lerp(0.08f, 0.35f, k) + Vector3.up * Mathf.Lerp(0.02f, 0.45f, k),
            k => Vector3.Lerp(candle.position + Vector3.up * 0.18f, Head() - Vector3.up * 0.15f, k), 18f, 30f));
        yield return new WaitForSeconds(5.2f);

        // the hall lights up around him, candle by candle; a high, slow crane over it all
        StartCoroutine(LightHall(4.5f));
        sfx.PlayOneShot(boomClip, 0.5f);
        Cut(Move(7f, k => At(new Vector3(Mathf.Lerp(8.5f, 6f, k), Mathf.Lerp(7.5f, 4.6f, k), Mathf.Lerp(-12.5f, -9.5f, k))),
            k => Vector3.Lerp(At(new Vector3(-3f, 1f, 0f)), curator.transform.position + Vector3.up * 1.2f, 0.5f + k * 0.3f), 44f, 38f));
        StartCoroutine(Footsteps());
        var walk = StartCoroutine(curator.WalkTo(new Vector3(-4.6f, 0f, 5.0f), 0.85f));
        yield return new WaitForSeconds(4.5f);
        // tracking beside him, low, as he walks the length of the hall
        Cut(Move(99f, k => curator.transform.position + new Vector3(2.4f, 1.0f, -1.1f), k => Head() - Vector3.up * 0.25f, 34f, 34f));
        yield return walk;
        walk = StartCoroutine(curator.WalkTo(ShelfSpot, 0.8f));
        StartCoroutine(Footsteps());
        // over his shoulder as he reaches the bookcase
        Cut(Move(6f, k => curator.transform.position + new Vector3(-1.2f, 1.85f, -1.5f) + Vector3.forward * k * 0.4f,
            k => Vector3.Lerp(Head(), At(gal.shelfBookPos), 0.4f + 0.5f * k), 30f, 27f));
        yield return walk;
        yield return TurnTo(0f, 0.6f);
        curator.lookTarget = book.root;
        tick.volume = 0.25f;

        // the insert: his hand finds the spine, tilts the book out, and the title catches the candle
        Vector3 spine = At(gal.shelfBookPos + new Vector3(0, 0, -StoryBookModel.W / 2f));
        Cut(Move(6.5f, k => spine + new Vector3(-0.56f + 0.1f * k, 0.07f, -0.21f + 0.03f * k), k => spine + Vector3.up * 0.01f, 22f, 18f, () => spine));
        yield return new WaitForSeconds(0.6f);
        rightGoal.SetParent(transform, true);
        rightGoal.position = curator.HandPosition(1);
        curator.Hold(1, rightGoal, 0.6f);
        Vector3 top = spine + new Vector3(0, StoryBookModel.H / 2f - 0.03f, -0.02f);
        yield return Glide(rightGoal, top, 1.1f);
        sfx.PlayOneShot(stingClip, 0.55f);
        // tilt the top of the book out, then slide it free
        Quaternion r0 = book.root.localRotation; Vector3 p0 = book.root.localPosition;
        for (float t = 0; t < 0.9f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0, 1, t / 0.9f);
            book.root.localRotation = r0 * Quaternion.Euler(-16f * k, 0, 0);
            book.root.localPosition = p0 + new Vector3(0, 0.01f * k, -0.035f * k);
            rightGoal.position = book.root.TransformPoint(new Vector3(0, StoryBookModel.H / 2f - 0.03f, -StoryBookModel.W / 2f - 0.02f));
            yield return null;
        }
        p0 = book.root.localPosition;
        for (float t = 0; t < 0.8f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0, 1, t / 0.8f);
            book.root.localPosition = p0 + new Vector3(0, 0, -0.22f * k);
            rightGoal.position = book.root.TransformPoint(new Vector3(0, StoryBookModel.H / 2f - 0.03f, -StoryBookModel.W / 2f - 0.02f));
            yield return null;
        }
        yield return new WaitForSeconds(0.4f);
        // into his hand, cover out, and he turns around
        rightGoal.SetParent(curator.transform, true);
        book.root.SetParent(rightGoal, true);
        Vector3 g0 = rightGoal.localPosition, b0 = book.root.localPosition; Quaternion q0 = book.root.localRotation;
        Cut(Move(3f, k => At(ShelfSpot + new Vector3(0.6f, 1.25f, -1.9f + 0.2f * k)), k => Head() - Vector3.up * 0.3f, 32f, 29f));
        StartCoroutine(TurnTo(180f, 1.6f));
        for (float t = 0; t < 1.4f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0, 1, t / 1.4f);
            rightGoal.localPosition = Vector3.Lerp(g0, BookHold, k);
            book.root.localPosition = Vector3.Lerp(b0, new Vector3(0f, 0.1f, 0.06f), k);
            book.root.localRotation = Quaternion.Slerp(q0, Quaternion.Euler(0, -90f, 0), k);
            yield return null;
        }
        yield return new WaitForSeconds(0.3f);
        curator.lookTarget = cam;

        // "There you are." - straight into the lens
        yield return Shot('E', "cur0_01");
        yield return Shot('M', "cur0_02");
        yield return Shot('H', "cur0_03");
        // he walks down to his table while he talks
        yield return WalkAndTalk(TableSpot, "cur0_04");
        yield return SetBookDown();
        yield return Shot('B', "cur0_05");
        yield return Shot('L', "cur0_06");
        yield return Shot('O', "cur0_07");
        yield return Shot('P', "cur0_08");
        yield return Shot('M', "cur0_09");
        yield return FlipPages(1);
        yield return Shot('B', "cur0_10");
        yield return Shot('E', "cur0_11");
        yield return Shot('X', "cur0_12");
        yield return Shot('W', "cur0_13");
        yield return Shot('L', "cur0_14");
        yield return BlowOut("cur0_15");
        Finish();
    }

    /// <summary>The later visits: he is already in his hall, the candle lit, the book in his hand.</summary>
    IEnumerator Return()
    {
        Ignite(0.01f);
        StartCoroutine(LightHall(0.01f));
        tick.volume = 0.22f;
        StartCoroutine(RampAmbience(0f, 0.55f, 3f));
        curator.lookTarget = cam;
        // open wide: the hall, and him in it, reading
        float side = scene == 2 ? -1f : 1f;
        Cut(Move(5f, k => At(new Vector3(side * Mathf.Lerp(6.5f, 4.8f, k), Mathf.Lerp(2.6f, 2.0f, k), Mathf.Lerp(-6f, -3.5f, k))), k => curator.transform.position + Vector3.up * 1.3f, 36f, 32f));
        fadeSpeed = 0.6f; fadeTarget = 0f;
        yield return new WaitForSeconds(3.6f);

        var lines = Lines();
        string exhibitLine = scene == 1 ? "cur1_02" : scene == 2 ? "cur2_03" : "cur3_03";
        string bookLine = scene == 3 ? "cur3_07" : null;
        string[] cycle = { "M", "L", "O", "P", "W", "E", "H", "M", "L", "O", "P" };
        int c = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            string id = lines[i];
            bool last = i == lines.Count - 1;
            if (i == 0) { yield return WalkAndTalk(TableSpot, id); yield return SetBookDown(); continue; }
            if (last) { yield return BlowOut(id); break; }
            if (id == exhibitLine || (scene == 3 && id.StartsWith("cur3_") && !char.IsDigit(id[5]))) { yield return Shot('X', id); continue; }
            if (id == bookLine) { yield return FlipPages(5); yield return Shot('B', id); continue; }
            yield return Shot(cycle[c++ % cycle.Length][0], id);
        }
        Finish();
    }

    List<string> Lines()
    {
        var d = SaveSystem.Data;
        var l = new List<string>();
        if (scene == 1) for (int i = 1; i <= 8; i++) l.Add("cur1_" + i.ToString("00"));
        else if (scene == 2)
        {
            for (int i = 1; i <= 11; i++)
            {
                l.Add("cur2_" + i.ToString("00"));
                if (i == 6 && d.panditAnswer >= 0) l.Add(d.panditAnswer == 0 ? "cur2_truth" : d.panditAnswer == 1 ? "cur2_people" : "cur2_both");
            }
        }
        else
        {
            string ally = d.branch == 0 && d.allyRaj ? "cur3_raj" : d.branch == 1 && d.allyRohan ? "cur3_rohan" : d.branch == 2 && d.allyReddy ? "cur3_reddy" : null;
            for (int i = 1; i <= 10; i++) l.Add(i == 3 && ally != null ? ally : "cur3_" + i.ToString("00"));
        }
        return l;
    }

    // ------------------------------------------------------------------ beats
    /// <summary>One line of dialogue over one camera set-up.</summary>
    IEnumerator Shot(char kind, string id)
    {
        Cut(SetUp(kind));
        yield return Line(id);
        yield return new WaitForSeconds(0.45f);         // let it land
    }

    IEnumerator SetUp(char kind)
    {
        Transform tr = curator.transform;
        switch (kind)
        {
            case 'M':       // medium close-up, slow push in
                return Move(9f, k => Head() + tr.forward * Mathf.Lerp(1.6f, 1.25f, k) - tr.right * 0.32f - Vector3.up * 0.12f, k => Head() - Vector3.up * 0.1f, 30f, 27f);
            case 'L':       // low and close, the candle lighting him from below
                return Move(9f, k => Head() + tr.forward * Mathf.Lerp(1.05f, 0.85f, k) + tr.right * 0.22f - Vector3.up * 0.42f, k => Head() - Vector3.up * 0.06f, 26f, 24f);
            case 'O':       // a slow arc around him
                return Move(10f, k =>
                {
                    float a = Mathf.Lerp(38f, -14f, k) * Mathf.Deg2Rad;
                    return tr.position + (tr.forward * Mathf.Cos(a) + tr.right * Mathf.Sin(a)) * 1.9f + Vector3.up * 1.55f;
                }, k => Head() - Vector3.up * 0.2f, 32f, 32f);
            case 'P':       // profile, the bookcase dark behind him
                return Move(9f, k => Head() + tr.right * -1.05f + tr.forward * Mathf.Lerp(0.3f, 0.5f, k) - Vector3.up * 0.05f, k => Head() + tr.forward * 0.25f - Vector3.up * 0.05f, 28f, 26f);
            case 'E':       // the eyes, very close
                return Move(8f, k => Head() + tr.forward * Mathf.Lerp(0.72f, 0.6f, k) + Vector3.up * 0.02f, k => Head() + Vector3.up * 0.03f, 19f, 17f);
            case 'H':       // high and wide: the hall around him
                return Move(10f, k => tr.position + tr.forward * Mathf.Lerp(5.5f, 4.6f, k) + tr.right * Mathf.Lerp(-2.2f, -1.2f, k) + Vector3.up * Mathf.Lerp(4.6f, 4.0f, k), k => tr.position + Vector3.up * 1.1f, 40f, 37f);
            case 'W':       // from behind one of the draped figures, as if something in the hall were watching him
                {
                    Transform dr = gal.drapes[0];
                    float best = float.MaxValue;
                    foreach (var dd in gal.drapes) { float dist = (dd.position - tr.position).sqrMagnitude; if (dist < best && dist > 4f) { best = dist; dr = dd; } }
                    return Move(10f, k =>
                    {
                        Vector3 to = (tr.position - dr.position); to.y = 0; to.Normalize();
                        Vector3 sideV = Vector3.Cross(Vector3.up, to);
                        return dr.position - to * 0.55f + sideV * Mathf.Lerp(0.42f, 0.5f, k) + Vector3.up * 1.62f;
                    }, k => Head() - Vector3.up * 0.2f, 30f, 28f, () => Head());
                }
            case 'B':       // over his shoulder, down onto the open book
                return Move(9f, k => BookWorld() + new Vector3(Mathf.Lerp(-0.25f, -0.18f, k), Mathf.Lerp(0.62f, 0.5f, k), Mathf.Lerp(0.55f, 0.42f, k)), k => BookWorld() + Vector3.forward * 0.02f, 30f, 27f, () => BookWorld());
            case 'X':       // insert on the exhibit
                return Move(9f, k => exhibit.position + new Vector3(Mathf.Lerp(-0.35f, -0.25f, k), Mathf.Lerp(0.3f, 0.22f, k), Mathf.Lerp(-0.42f, -0.32f, k)), k => exhibit.position + Vector3.up * 0.04f, 24f, 21f, () => exhibit.position);
        }
        return Move(9f, k => Head() + tr.forward * 1.5f, k => Head(), 30f, 30f);
    }

    Vector3 BookWorld() { return book != null ? book.root.position : transform.TransformPoint(BookOnTable); }

    /// <summary>He walks somewhere while speaking a line; the camera backs away in front of him.</summary>
    IEnumerator WalkAndTalk(Vector3 to, string id)
    {
        curator.lookTarget = cam;
        StartCoroutine(Footsteps());
        var walk = StartCoroutine(curator.WalkTo(to, 0.75f));
        Cut(Move(99f, k => curator.transform.position + curator.transform.forward * 2.3f + curator.transform.right * 0.45f + Vector3.up * 1.4f, k => Head() - Vector3.up * 0.2f, 32f, 32f));
        var line = StartCoroutine(Line(id));
        yield return walk;
        yield return TurnTo(180f, 0.7f);
        yield return line;
        yield return new WaitForSeconds(0.35f);
    }

    /// <summary>At his table: the book goes down on the velvet and he opens it at tonight's chapter.</summary>
    IEnumerator SetBookDown()
    {
        if (book == null) yield break;
        Cut(Move(99f, k => curator.transform.position + new Vector3(-0.55f, 2.15f, 0.55f), k => BookWorld(), 32f, 29f, () => BookWorld()));
        curator.lean = 16f;
        Vector3 rest = transform.TransformPoint(BookOnTable);
        Quaternion flat = transform.rotation * Quaternion.LookRotation(Vector3.left, Vector3.back);
        Vector3 bp = book.root.position; Quaternion bq = book.root.rotation;
        book.root.SetParent(transform, true);
        for (float t = 0; t < 1.2f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0, 1, t / 1.2f);
            book.root.position = Vector3.Lerp(bp, rest, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.06f;
            book.root.rotation = Quaternion.Slerp(bq, flat, k);
            rightGoal.position = book.root.position + Vector3.up * 0.06f + curator.transform.forward * -0.1f;
            yield return null;
        }
        sfx.PlayOneShot(Tone(140f, 0.2f, 25f, 0.5f), 0.5f);
        // open the cover
        rightGoal.SetParent(transform, true);
        Quaternion c0 = book.cover.localRotation;
        for (float t = 0; t < 1f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0, 1, t / 1f);
            book.cover.localRotation = c0 * Quaternion.Euler(0, 179f * k, 0);
            rightGoal.position = book.cover.TransformPoint(new Vector3(0.02f, 0, StoryBookModel.W * 0.9f));
            yield return null;
        }
        sfx.PlayOneShot(pageClip, 0.5f);
        curator.Release(1, 0.6f);
        curator.lean = 4f;
        rightGoal.SetParent(curator.transform, true);
        yield return new WaitForSeconds(0.5f);
    }

    IEnumerator FlipPages(int n)
    {
        if (book == null) yield break;
        Cut(SetUp('B'));
        curator.lean = 12f;
        for (int i = 0; i < n; i++)
        {
            var leaf = book.PageLeaf();
            sfx.PlayOneShot(pageClip, 0.45f);
            for (float t = 0; t < 0.5f; t += Time.deltaTime)
            {
                leaf.localRotation = Quaternion.Euler(0, 178f * Mathf.SmoothStep(0, 1, t / 0.5f), 0);
                yield return null;
            }
            Destroy(leaf.gameObject);
            yield return new WaitForSeconds(0.12f);
        }
        curator.lean = 4f;
        yield return new WaitForSeconds(0.3f);
    }

    /// <summary>The last line, the candle raised to his face, and then darkness.</summary>
    IEnumerator BlowOut(string id)
    {
        curator.lean = 0f;
        Vector3 from = leftGoal.localPosition;
        Cut(Move(99f, k => Head() + curator.transform.forward * Mathf.Lerp(0.95f, 0.72f, k) - Vector3.up * 0.1f, k => Head() - Vector3.up * 0.12f, 22f, 19f));
        for (float t = 0; t < 1.2f; t += Time.deltaTime)
        {
            leftGoal.localPosition = Vector3.Lerp(from, CandleRaised, Mathf.SmoothStep(0, 1, t / 1.2f));
            yield return null;
        }
        yield return Line(id);
        yield return new WaitForSeconds(0.6f);
        curator.lean = 6f;
        yield return new WaitForSeconds(0.25f);
        sfx.PlayOneShot(blowClip, 0.8f);
        Smoke(candleFlame.transform.position);
        var fl = candleLight.GetComponent<CandleFlicker>();
        for (float t = 0; t < 0.35f; t += Time.deltaTime)
        {
            float k = 1f - t / 0.35f;
            candleFlame.transform.localScale = new Vector3(0.022f, 0.045f, 0.022f) * k;
            fl.level = k;
            yield return null;
        }
        candleFlame.SetActive(false); fl.level = 0f;
        // every flame in the hall goes out with his, in a wave away from him
        StartCoroutine(SnuffHall(0.9f));
        tick.volume = 0f;
        sfx.PlayOneShot(boomClip, 0.6f);
        StartCoroutine(RampAmbience(ambience.volume, 0f, 1.6f));
        yield return new WaitForSeconds(1.0f);
        fadeSpeed = 1.2f; fadeTarget = 1f;
        while (fade < 0.99f) yield return null;
        yield return new WaitForSeconds(1.2f);
    }

    // ------------------------------------------------------------------ dialogue
    IEnumerator Line(string id)
    {
        var clip = VO(id);
        string text = DialogueDB.Text(id);
        float dur = clip != null ? clip.length + 0.25f : Mathf.Max(2.4f, text.Length * 0.065f);
        if (clip != null) curator.Say(clip);
        sub = DialogueDB.Speaker(id) + ":  " + text; subUntil = Time.time + dur + 0.1f;
        yield return null;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            var kb = Keyboard.current;
            if (skipAll) break;
            if (Time.timeScale > 0f && (GameInput.InteractPressed || (kb != null && kb.spaceKey.wasPressedThisFrame))) { curator.Silence(); break; }
            yield return null;
        }
        sub = null;
    }

    // ------------------------------------------------------------------ the camera
    Coroutine shotCo;
    Vector3 camPos, camAim, camFocus; float camFov = 30f; bool camOn;
    Vector3 camLocal0; float fov0;

    void DetachCamera()
    {
        camLocal0 = cam.localPosition; fov0 = camComp.fieldOfView;
        player.enabled = false;
        var cc = player.GetComponent<CharacterController>(); if (cc != null) cc.enabled = false;
        cam.SetParent(transform, true);
        AutoFocus.FocusOn = focus;
    }

    void Cut(IEnumerator shot)
    {
        if (shotCo != null) StopCoroutine(shotCo);
        shotCo = StartCoroutine(shot);
    }

    /// <summary>
    /// A camera move: position and aim are functions of the eased shot time (0..1), so a shot can track a moving
    /// subject. After `dur` it holds its last framing (still tracking) until the next cut.
    /// </summary>
    IEnumerator Move(float dur, Func<float, Vector3> pos, Func<float, Vector3> aim, float fovA, float fovB, Func<Vector3> focusOn = null)
    {
        for (float t = 0; ; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / dur);
            float e = k * k * (3f - 2f * k);
            camPos = pos(e); camAim = aim(e); camFov = Mathf.Lerp(fovA, fovB, e);
            camFocus = focusOn != null ? focusOn() : Head();
            camOn = true;
            yield return null;
        }
    }

    void LateUpdate()
    {
        if (done) return;          // finished: the next scene's lighting is already in place
        // the Act I room keeps writing its own ambient light every frame; the Gallery has its own
        CuratorGallery.Mood(null, lightLevel);
        if (!camOn || done || cam == null) return;
        float t = Time.time;
        // a gentle handheld drift
        Vector3 drift = new Vector3(Mathf.PerlinNoise(t * 0.3f, 1.7f) - 0.5f, Mathf.PerlinNoise(3.1f, t * 0.27f) - 0.5f, Mathf.PerlinNoise(t * 0.21f, 8.3f) - 0.5f);
        Vector3 dir = camAim - camPos;
        if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
        cam.position = camPos;
        cam.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(drift.y * 0.9f, drift.x * 1.1f, drift.z * 0.9f);
        camComp.fieldOfView = camFov;
        focus.position = camFocus - Vector3.up * 1.5f;     // AutoFocus measures to FocusOn + 1.5 m
    }

    Vector3 Head() { return curator.transform.position + Vector3.up * 1.79f; }

    // ------------------------------------------------------------------ helpers
    IEnumerator TurnTo(float yaw, float dur)
    {
        Quaternion a = curator.transform.rotation, b = Quaternion.Euler(0, yaw, 0);
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            curator.transform.rotation = Quaternion.Slerp(a, b, Mathf.SmoothStep(0, 1, t / dur));
            yield return null;
        }
        curator.transform.rotation = b;
    }

    IEnumerator Glide(Transform tr, Vector3 to, float dur)
    {
        Vector3 from = tr.position;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            tr.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t / dur));
            yield return null;
        }
        tr.position = to;
    }

    IEnumerator Footsteps()
    {
        yield return null;
        while (curator != null && curator.IsWalking)
        {
            sfx.PlayOneShot(stepClip, 0.22f);
            yield return new WaitForSeconds(0.62f);
        }
    }

    void Ignite(float unused)
    {
        candleFlame.SetActive(true);
        candleLight.GetComponent<CandleFlicker>().level = 1f;
    }

    IEnumerator LightHall(float dur)
    {
        var order = new List<int>();
        for (int i = 0; i < gal.lights.Count; i++) order.Add(i);
        Vector3 c = curator.transform.position;
        order.Sort((a, b) => (gal.lights[a].transform.position - c).sqrMagnitude.CompareTo((gal.lights[b].transform.position - c).sqrMagnitude));
        for (int f = 0; f < gal.flames.Count; f++) gal.flames[f].SetActive(false);
        int flameIdx = 0;
        for (float t = 0; t <= dur; t += Time.deltaTime)
        {
            float k = dur > 0.05f ? t / dur : 1f;
            for (int i = 0; i < order.Count; i++) gal.lights[order[i]].level = Mathf.Clamp01(k * order.Count - i * 0.8f);
            int want = Mathf.RoundToInt(k * gal.flames.Count);
            while (flameIdx < want && flameIdx < gal.flames.Count) gal.flames[flameIdx++].SetActive(true);
            lightLevel = k;
            if (dur <= 0.05f) break;
            yield return null;
        }
        gal.SetLights(1f);
        lightLevel = 1f;
    }

    IEnumerator SnuffHall(float dur)
    {
        float l0 = lightLevel;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float k = t / dur;
            for (int i = 0; i < gal.lights.Count; i++) gal.lights[i].level = Mathf.Clamp01(1f - k * 1.6f + (i % 5) * 0.08f);
            for (int i = 0; i < gal.flames.Count; i++) if (gal.flames[i].activeSelf && UnityEngine.Random.value < k * 0.25f) gal.flames[i].SetActive(false);
            foreach (var m in gal.moonLights) if (m != null) m.intensity = Mathf.Lerp(9f, 1.5f, k);
            lightLevel = Mathf.Lerp(l0, 0f, k);
            yield return null;
        }
        gal.SetLights(0f);
        lightLevel = 0f;
    }

    void Smoke(Vector3 at)
    {
        var ps = new GameObject("CandleSmoke").AddComponent<ParticleSystem>();
        ps.transform.SetParent(transform, false);
        ps.transform.position = at;
        ps.transform.rotation = Quaternion.LookRotation(Vector3.up);
        var main = ps.main; main.loop = false; main.duration = 1.5f; main.startLifetime = 2.2f; main.startSpeed = 0.18f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f); main.startColor = new Color(0.75f, 0.72f, 0.7f, 0.35f); main.maxParticles = 120;
        var em = ps.emission; em.rateOverTime = 45f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 6f; sh.radius = 0.005f;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.4f, 1, 2.5f));
        var nz = ps.noise; nz.enabled = true; nz.strength = 0.08f; nz.frequency = 1.2f;
        var sd = Shader.Find("Sprites/Default");
        if (sd != null) ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = new Material(sd);
        ps.Play();
    }

    void Finish()
    {
        if (done) return;
        done = true;
        if (shotCo != null) StopCoroutine(shotCo);
        sub = null;
        AutoFocus.FocusOn = null;
        // Arjun gets his eyes back
        cam.SetParent(player.transform, false);
        cam.localPosition = camLocal0; cam.localRotation = Quaternion.identity;
        camComp.fieldOfView = fov0;
        var cc = player.GetComponent<CharacterController>(); if (cc != null) cc.enabled = true;
        player.enabled = true;
        fade = 1f; fadeTarget = 1f;
        if (returnToRoom)
        {
            RestoreRoom();
            if (builder.ExtraRoot == gameObject) builder.ExtraRoot = null;
            Destroy(gameObject);
        }
        if (onDone != null) onDone();
    }

    protected override void Update()
    {
        base.Update();
        var kb = Keyboard.current;
        if (!done && kb != null && Time.timeScale > 0f && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) skipAll = true;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (AutoFocus.FocusOn == focus) AutoFocus.FocusOn = null;
        if (!done && cam != null && player != null && cam.parent == transform)      // quit mid-scene: never take the camera down with us
        {
            cam.SetParent(player.transform, false);
            cam.localPosition = camLocal0; cam.localRotation = Quaternion.identity;
            player.enabled = true;
        }
    }

    protected override void OnGUI()
    {
        base.OnGUI();
        if (done || Time.timeScale <= 0f || fade > 0.98f) return;
        float s = Mathf.Max(0.7f, Screen.height / 900f);
        var st = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13 * s), alignment = TextAnchor.LowerRight };
        st.normal.textColor = new Color(1f, 1f, 1f, 0.4f);
        GUI.Label(new Rect(0, Screen.height - 34 * s, Screen.width - 20 * s, 26 * s), "Enter  skip scene      Space  next line", st);
    }

    // ------------------------------------------------------------------ sound
    const int SR = 44100;

    static AudioClip Drone()
    {
        int n = SR * 12; var d = new float[n]; var r = new System.Random(3); float a = 0, b = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR, w = (float)(r.NextDouble() * 2 - 1);
            a += (w - a) * 0.01f; b += (a - b) * 0.02f;
            float swell = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * t / 12f);       // one slow breath per loop
            d[i] = (Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 55.5f * t) * 0.45f
                    + Mathf.Sin(2f * Mathf.PI * 77.8f * t) * 0.18f * swell + Mathf.Sin(2f * Mathf.PI * 233.1f * t) * 0.03f * swell) * swell + b * 9f;
        }
        Norm(d, 0.5f);
        var c = AudioClip.Create("drone", n, 1, SR, false); c.SetData(d, 0); return c;
    }

    static AudioClip Tick()
    {
        int n = SR * 2; var d = new float[n];
        for (int k = 0; k < 2; k++)
            for (int i = 0; i < 1600; i++)
            {
                float t = i / (float)SR;
                d[k * SR + i] = Mathf.Sin(2f * Mathf.PI * (k == 0 ? 2100f : 1700f) * t) * Mathf.Exp(-t * 280f) * 0.6f + Mathf.Sin(2f * Mathf.PI * 420f * t) * Mathf.Exp(-t * 90f) * 0.25f;
            }
        var c = AudioClip.Create("tick", n, 1, SR, false); c.SetData(d, 0); return c;
    }

    static AudioClip Sting()
    {
        int n = SR * 3; var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR, env = Mathf.Clamp01(t * 6f) * Mathf.Exp(-t * 1.1f);
            d[i] = (Mathf.Sin(2f * Mathf.PI * 41f * t) * 0.8f + Mathf.Sin(2f * Mathf.PI * 1480f * t) * 0.12f + Mathf.Sin(2f * Mathf.PI * 1568f * t) * 0.12f + Mathf.Sin(2f * Mathf.PI * 740f * t * (1f + 0.002f * Mathf.Sin(30f * t))) * 0.08f) * env;
        }
        Norm(d, 0.8f);
        var c = AudioClip.Create("sting", n, 1, SR, false); c.SetData(d, 0); return c;
    }

    static AudioClip Noise(float len, float cutoff, float decay, bool crackle)
    {
        int n = (int)(SR * len); var d = new float[n]; var r = new System.Random(11); float lp = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR, w = (float)(r.NextDouble() * 2 - 1);
            lp += (w - lp) * cutoff;
            float env = Mathf.Clamp01(t * 40f) * Mathf.Exp(-t * decay);
            d[i] = (crackle ? (w - lp) * (r.NextDouble() < 0.02 ? 3f : 1f) : lp) * env;
        }
        Norm(d, 0.7f);
        var c = AudioClip.Create("noise", n, 1, SR, false); c.SetData(d, 0); return c;
    }
}
