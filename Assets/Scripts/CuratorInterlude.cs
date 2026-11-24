using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The Curator's interludes, played as cutscenes in his Gallery between the acts (dialogue: Tools/acts_dialogue.py).
///   scene 0 (before Act I)  "Welcome", the prologue: he walks the hall to the bookcase, takes down "Shadows of
///                           Chandni", turns, and speaks to the investigator; the storm answers; he blows the candle out.
///   scene 1-3 (after Acts II-IV): he is in his hall with the candle and the book; he walks to his table, lays the book
///                           down and opens it, and speaks; each scene has an exhibit on the table.
/// The performance is carried by CharacterNPC's hero layers (CharacterNPC.Hero.cs): grounded walking with planted
/// feet, hands that really hold the candle and the book (authored grips, wrist and finger poses), visemes timed to
/// the voice, blinks, an eyeline chosen per beat. This script only directs: where he goes, what his hands do, where
/// he looks, and how it is filmed.
/// Filming: shots are built around the beats, not one per line. Each shot has a lens (focal length on a Super 35
/// sensor; the field of view follows from it and does not animate), an aperture for the depth of field, focus on his
/// actual eyes, framing from his animated eye position, and a camera that never sits inside the furniture. A shot
/// may hold over several lines; the cut comes with an action, a new idea, or a reaction.
/// Enter skips the whole scene, Space or E skips one line, Esc pauses (everything freezes, sound and mouth included).
/// The Act V Gallery is the same room; there the Curator speaks without a candle (Act5Story).
/// </summary>
[DefaultExecutionOrder(500)]      // after the characters: the camera frames the pose that will be drawn
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
        if (scene == 0 || scene == 1) builder.RoomEffectsOwner = root;
        if (old != null) Destroy(old);
        GameState.ResetForNewScene();
        s.builder = builder; s.player = player; s.scene = scene; s.onDone = next; s.resume = false;
        return s;
    }

    // ------------------------------------------------- the Act I flat, kept as it was during the prologue
    bool returnToRoom;
    UnityEngine.Rendering.AmbientMode sAmbMode; Color sSky, sEq, sGround, sBg; float sRefl, sFogDen, sAmbInt; bool sFog; FogMode sFogMode; Color sFogCol;
    Material sSkybox; CameraClearFlags sClear;
    DepthOfField sDof; float sDofDist;
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
    Vector3 Dir(Vector3 local) { return transform.TransformDirection(local); }

    // ------------------------------------------------------------------ cast & set
    CuratorGallery gal;
    CharacterNPC curator;
    StoryBookModel book;
    Transform candle, focus, exhibit;
    GameObject candleFlame; Light candleLight; CandleFlicker candleFlicker; FlameWobble flameWobble;
    float lastGalleryLevel = -1f;
    float lightLevel;              // hall lights 0..1 (mirrored into the ambient every frame)
    bool done;
    AudioSource tick;
    AudioClip droneClip, tickClip, stingClip, boomClip, matchClip, blowClip, pageClip;
    CuratorSound sound;
    int pagesTurned;
    float bedLevel, tickLevel;     // the score bed and the clock; the sound mix ducks both under his voice

    // places (Gallery space)
    static readonly Vector3 TableSpot = new Vector3(0.3f, 0f, 3.05f);       // behind the table, facing the hall
    static readonly Vector3 ShelfSpot = new Vector3(CuratorGallery.ShelfBookSlot.x - 0.15f, 0f, CuratorGallery.ShelfFront - 0.42f);
    static readonly Vector3 BookOnTable = new Vector3(0.12f, 0.945f + StoryBookModel.T / 2f, 2.48f);   // within his reach from TableSpot, leaning
    Vector3 bookRest;                    // where the book really lies once he has put it down (zero until then)
    Vector3 BookRest() { return bookRest != Vector3.zero ? bookRest : At(BookOnTable); }
    static readonly Vector3 ExhibitSpot = new Vector3(1.05f, 0.945f, 2.25f);
    static readonly Vector3 Visitor = new Vector3(0.25f, 1.64f, 0.55f);     // the investigator's eyes, across the table
    static readonly Vector3 EastWindow = new Vector3(11.9f, 5.3f, 1.7f);

    // poses in the Curator's own space (he is 1.95 m): palm of the left hand under the candle; the book in the right
    static readonly Vector3 CandleHold = new Vector3(-0.2f, 1.19f, 0.3f), CandleRaised = new Vector3(-0.05f, 1.47f, 0.28f);
    static readonly Pose BookCarry = new Pose(new Vector3(0.13f, 1.06f, 0.3f), Quaternion.LookRotation(Vector3.left, Vector3.up));
    static readonly Pose BookShow = new Pose(new Vector3(0.11f, 1.2f, 0.34f), Quaternion.LookRotation(Vector3.left, Vector3.up));
    static readonly Pose CandleGrip = new Pose(new Vector3(0f, -0.018f, -0.012f), Quaternion.LookRotation(Vector3.forward, Vector3.up));

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
        curator.EnableHero();
        curator.AddObstacle(new Bounds(transform.TransformPoint(gal.tableTop.center), gal.tableTop.size));
        curator.Eyeline(At(Visitor));

        candle = BuildCandle();
        curator.Hold(candle, 0, CandleGrip);
        curator.HandSnap(0, CandleGoal(CandleHold));

        if (scene == 0) book = gal.shelfBook;          // still on the shelf: he fetches it
        else
        {
            book = StoryBookModel.Make(transform, Chapter());
            curator.Hold(book.root, 1, StoryBookModel.Grip("spineTop"));
            curator.HandSnap(1, BookGoal(BookCarry));
        }
        if (book != null) book.pageText.text = Chapter();
        BuildExhibit();

        var f = new GameObject("FocusPoint"); f.transform.SetParent(transform, false); focus = f.transform;
        if (!returnToRoom) Teleport(At(new Vector3(0f, 0.1f, -13f)), 0f);

        droneClip = Drone(); tickClip = Tick(); stingClip = Sting(); boomClip = Tone(48f, 2.4f, 1.6f, 0.9f);
        matchClip = Noise(0.35f, 0.5f, 18f, true); blowClip = Noise(0.7f, 0.12f, 5f, false); pageClip = Noise(0.25f, 0.35f, 14f, true);
        ambience.clip = droneClip; ambience.Play();
        tick = gameObject.AddComponent<AudioSource>(); tick.clip = tickClip; tick.loop = true; tick.volume = 0f; tick.Play();

        // sound: every event has a cause (feet, hands, the storm)
        sound = CuratorSound.Make(transform, At(EastWindow), At(new Vector3(0f, 3f, 2f)));
        sound.clearThunderFilter = scene <= 1;
        sound.voice = curator.VoiceSource;
        sound.SetFallback("steps_wood", stepClip); sound.SetFallback("page", pageClip); sound.SetFallback("candle_blow", blowClip);
        sound.SetFallback("match", matchClip); sound.SetFallback("book_down", Tone(140f, 0.2f, 25f, 0.5f)); sound.SetFallback("book_take", pageClip);
        sound.SetFallback("book_open", pageClip); sound.SetFallback("thunder_near", boomClip); sound.SetFallback("thunder_far", boomClip);
        curator.FootContact += (foot, pos, strength) => { if (!done) sound.Step(pos, strength, OnRunner(pos)); };
        gal.storm.Thunder += (km, s) => { if (!done) sound.Thunder(km, s); };
        gal.exposure.subject = () => curator != null ? curator.EyePoint() : transform.position;
    }

    bool OnRunner(Vector3 world) { var l = transform.InverseTransformPoint(world); return Mathf.Abs(l.x) < 1.2f && l.z > -14f && l.z < 3f; }

    HandGoal CandleGoal(Vector3 bodyPos)
    {
        // palm up under the dish, fingers forward and a little inwards: the candle stays upright
        return HandGoal.Body(curator.transform, bodyPos, Quaternion.LookRotation(new Vector3(0.28f, 0f, 1f), Vector3.up), Fingers.Dish).Elbow(new Vector3(0.7f, -1f, -0.45f));
    }

    HandGoal BookGoal(Pose bookInBody)
    {
        // the hand goes wherever puts the book at this pose (in his space): the grip decides the hand
        var g = new HandGoal { fingers = Fingers.Edge, elbow = new Vector3(0.85f, -0.7f, -0.35f) };
        var bodyT = curator.transform; var bt = book.root;
        g.palm = () => curator.PalmFor(bt, new Pose(bodyT.TransformPoint(bookInBody.position), bodyT.rotation * bookInBody.rotation));
        return g;
    }

    Transform BuildCandle()
    {
        // a brass chamberstick: a dish with a raised rim, the socket, the candle, and a ring handle on the side
        var c = new GameObject("CuratorCandle").transform;
        c.SetParent(transform, false);
        var brass = GameKit.Lit(new Color(0.72f, 0.52f, 0.22f), 0.72f, 1f);
        var wax = GameKit.Lit(new Color(0.93f, 0.9f, 0.82f), 0.3f);
        var dish = VehicleKit.Part(c, "Dish", VehicleKit.Lathe(new[] { new Vector2(0f, 0.004f), new Vector2(0.004f, 0.05f), new Vector2(0.008f, 0.058f), new Vector2(0.014f, 0.062f), new Vector2(0.018f, 0.06f), new Vector2(0.006f, 0.052f), new Vector2(0.004f, 0.0f) }, 28), brass, Vector3.zero);
        dish.transform.localRotation = Quaternion.Euler(0, 0, 90f);
        GameKit.Cyl(c, "Socket", new Vector3(0, 0.02f, 0), 0.016f, 0.03f, brass);
        GameKit.Cyl(c, "Wax", new Vector3(0, 0.105f, 0), 0.0115f, 0.14f, wax);
        GameKit.Box(c, "Drip", new Vector3(0.011f, 0.13f, 0.004f), new Vector3(0.005f, 0.035f, 0.005f), wax);
        GameKit.Cyl(c, "Wick", new Vector3(0, 0.181f, 0), 0.0012f, 0.008f, GameKit.Lit(new Color(0.08f, 0.06f, 0.05f), 0.2f));
        var ring = GameKit.Box(c, "HandleRing", new Vector3(-0.072f, 0.012f, 0), new Vector3(0.026f, 0.022f, 0.006f), brass);
        ring.name = "Handle";
        // the flame: a hot small core, a soft orange body you can see through, a blue foot on the wick
        candleFlame = new GameObject("Flame");
        candleFlame.transform.SetParent(c, false);
        candleFlame.transform.localPosition = new Vector3(0, 0.197f, 0);
        candleFlame.transform.localScale = new Vector3(0.018f, 0.04f, 0.018f);
        FlamePart(candleFlame.transform, new Vector3(0, -0.05f, 0), new Vector3(0.45f, 0.42f, 0.45f), GameKit.Glow(new Color(1f, 0.92f, 0.75f), new Color(1f, 0.86f, 0.62f), 4.5f));
        FlamePart(candleFlame.transform, Vector3.zero, Vector3.one, Additive(new Color(1f, 0.52f, 0.14f) * 0.9f));
        FlamePart(candleFlame.transform, new Vector3(0, -0.42f, 0), new Vector3(0.5f, 0.16f, 0.5f), Additive(new Color(0.18f, 0.28f, 0.95f) * 0.55f));
        flameWobble = candleFlame.AddComponent<FlameWobble>();
        candleFlame.SetActive(false);
        candleLight = GameKit.PointLight(c, new Vector3(0, 0.25f, 0.015f), new Color(1f, 0.76f, 0.52f), 0f, 3.6f);
        CinematicLook.Shadows(candleLight, 0.9f, 0.015f, 0.35f);   // his shadow climbs the bookcase behind him
        candleLight.shadowResolution = UnityEngine.Rendering.LightShadowResolution.VeryHigh;
        candleLight.shadowNearPlane = 0.06f;
        candleFlicker = candleLight.gameObject.AddComponent<CandleFlicker>(); candleFlicker.full = 1.25f; candleFlicker.level = 0f; candleFlicker.dipChance = 0.12f;
        return c;
    }

    static void FlamePart(Transform parent, Vector3 pos, Vector3 scale, Material m)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localScale = scale;
        var r = g.GetComponent<Renderer>(); r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
    }

    /// <summary>A glowing, see-through material (added light, no depth write) for the flame's body.</summary>
    static Material Additive(Color c)
    {
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        var m = new Material(sh);
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 2f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.SetColor("_BaseColor", c);
        return m;
    }

    /// <summary>The page under the one being turned (`turned` pages already turned). Never blank: an insert holds on it.</summary>
    string NextPage(int turned)
    {
        if (scene == 3)
            return turned == 0 ? "...nearly four.\n\nThe hour when\ncities confess.\n" : "WHAT YOU CAN PROVE\n\n1. ____________\n2. ____________\n3. ____________\n";
        return turned == 0 ? "...she kept the number\nfor a person,\nnot a machine.\n\n" : "...and the door\nstayed shut.\n\n";
    }

    string Chapter()
    {
        string[] c = { "CHAPTER ONE\n\nThe Empty Room", "CHAPTER TWO\n\nThe Streets Know", "CHAPTER THREE\n\nThe File", "CHAPTER FOUR\n\nThe Web" };
        return c[Mathf.Clamp(scene, 0, 3)] + "\n\n" + (scene == 0 ? "a key, a note,\na door no one knocked on" : scene == 1 ? "a kettle, a drive,\na black car" : scene == 2 ? "a deed, a cage,\na question at the temple" : "three doors,\none witness");
    }

    // A small object on the table for each scene, filmed in an insert when he mentions it.
    void BuildExhibit()
    {
        exhibit = new GameObject("Exhibit").transform;
        exhibit.SetParent(transform, false);
        exhibit.localPosition = ExhibitSpot;
        var brass = GameKit.Lit(new Color(0.78f, 0.58f, 0.24f), 0.55f, 0.6f);       // worn brass: it takes the candle, not only reflections
        var lamp = GameKit.PointLight(transform, ExhibitSpot + new Vector3(-0.15f, 0.32f, 0.36f), new Color(1f, 0.68f, 0.42f), 0.35f, 1.6f);
        var lf = lamp.gameObject.AddComponent<CandleFlicker>(); lf.full = 0.35f; lf.dipChance = 0.2f;
        gal.lights.Add(lf);
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
    /// <summary>
    /// Test benches for the capture tool only (Assets/Scripts/Playtest/CuratorCapture.cs sets it from PINNACLE_LAB):
    /// "walk", "hands", "face", "storm". Null in the game.
    /// </summary>
    public static string Lab;

    protected override IEnumerator Run()
    {
        phase = Phase.Intro;                 // keeps the cinema bars up for the whole scene
        fade = 1f; fadeTarget = 1f;
        DetachCamera();
        StartCoroutine(!string.IsNullOrEmpty(Lab) ? LabRun(Lab) : scene == 0 ? Welcome() : Return());
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
        if (scene <= 1) { LineNow = null; TestSkipLine = false; }
        fadeSpeed = 2.5f; fadeTarget = 1f;
        while (fade < 0.99f) yield return null;
        yield return new WaitForSeconds(0.3f);
        Finish();
    }

    /// <summary>His first appearance: the walk to the bookcase, the book, the welcome, the storm, the candle.</summary>
    IEnumerator Welcome()
    {
        yield return new WaitForSeconds(0.8f);
        // a match in the dark, then the flame in his hand; he is looking down at it
        sound.Play("match", candle.position, -6f, 0.4f);
        curator.Eyeline(candle.position + Vector3.up * 0.2f);
        curator.Expression("neutral", 0.01f);
        yield return new WaitForSeconds(0.25f);
        Ignite();
        fade = 0f; fadeTarget = 0f;
        StartCoroutine(BedRamp(0f, 0.45f, 4f));
        // 1. the flame, and the face above it: a slow pull back and tilt up, his eyes on the flame
        Cut(new Shot(85f, 2f)
        {
            pos = t => Flame() + Fwd() * Mathf.Lerp(0.75f, 2.6f, Ease(t / 5.4f)) + Right() * Mathf.Lerp(0.05f, 0.12f, Ease(t / 5.4f)) + Vector3.up * Mathf.Lerp(0.02f, 0.45f, Ease(t / 5.4f)),
            look = t => Vector3.Lerp(Flame() + Vector3.up * 0.03f, Eyes() - Vector3.up * 0.12f, Ease((t - 1.0f) / 4.2f)),
            screen = new Vector2(0.5f, 0.55f), follow = 0.2f, handheld = 0.06f,
            focusOn = t => t < 2.0f ? Flame() : Eyes(),
        });
        yield return new WaitForSeconds(2.3f);
        curator.Expression("knowing", 1.5f);
        curator.Eyeline(At(new Vector3(-4.6f, 1.7f, 5f)));       // he looks up the hall, to where he is going
        yield return new WaitForSeconds(1.0f);

        // 2. the hall comes up around him, candle by candle: a high, slow, composed wide; a distant flash in the windows
        StartCoroutine(LightHall(4.5f));
        // The storm schedules its own thunder after the flash. Let the hall open in room tone.
        Cut(new Shot(24f, 8f)
        {
            pos = t => At(new Vector3(Mathf.Lerp(7.2f, 6.2f, Ease(t / 8f)), Mathf.Lerp(6.4f, 5.2f, Ease(t / 8f)), Mathf.Lerp(-12.4f, -11.2f, Ease(t / 8f)))),
            look = t => At(new Vector3(-2.2f, 1.2f, 1.5f)),
            screen = new Vector2(0.5f, 0.45f), follow = 2.5f, handheld = 0f,
            focusOn = t => curator.transform.position + Vector3.up * 1.4f,
        });
        yield return new WaitForSeconds(1.2f);
        var walk = StartCoroutine(curator.HeroWalk(new Vector3(-4.6f, 0f, 5.0f), 1.15f));
        yield return new WaitForSeconds(2.6f);
        gal.storm.Strike(4.2f, 0.7f);
        yield return new WaitForSeconds(1.2f);

        // 3. walking the hall: a restrained side track at his height, a few steps behind his pace
        Cut(new Shot(35f, 4f)
        {
            pos = t => Track(curator.transform.position, Vector3.left * 3.6f + Vector3.forward * -0.8f, 1.45f),
            look = t => Eyes(), screen = new Vector2(0.55f, 0.72f), follow = 0.45f, handheld = 0.1f,
        });
        yield return walk;
        curator.Eyeline(At(gal.shelfBookPos));
        walk = StartCoroutine(curator.HeroWalk(ShelfSpot, 1.05f, transform.eulerAngles.y));
        // 4. he arrives at the bookcase: seen along the shelves, he stops and turns to it
        Cut(new Shot(32f, 3.5f)
        {
            pos = t => At(new Vector3(4.1f, 1.55f, 6.4f)),
            look = t => Eyes(), screen = new Vector2(0.42f, 0.72f), follow = 0.7f, handheld = 0.05f,
        });
        yield return walk;
        tickLevel = 0.18f;

        // 5. the reach, over his shoulder; 6. the insert: the hand finds the book, tips it out, draws it free
        Cut(new Shot(35f, 2.8f)
        {
            pos = t => At(ShelfSpot + new Vector3(-1.35f, 1.62f, -1.05f)),
            look = t => Vector3.Lerp(Eyes(), At(gal.shelfBookPos) + Vector3.up * 0.1f, 0.5f),
            screen = new Vector2(0.55f, 0.6f), follow = 0.6f, handheld = 0.05f,
            focusOn = t => book.root.position,
        });
        yield return TakeBookFromShelf();

        // 7. he turns to us with the book (stepping round), the cover towards the lens
        curator.Eyeline(At(Visitor) + Vector3.up * 0.05f);
        Cut(new Shot(40f, 2.8f)
        {
            pos = t => At(ShelfSpot + new Vector3(0.3f, 1.62f, -2.6f)),
            look = t => Eyes(), screen = new Vector2(0.5f, 0.68f), follow = 0.5f, handheld = 0.06f,
        });
        yield return curator.HeroTurn(transform.eulerAngles.y + 180f);
        curator.HandTo(1, BookGoal(BookShow), 0.9f, 0.02f);
        yield return new WaitForSeconds(0.6f);

        // "There you are." - straight into the lens, close
        Shot direct = Framed(75f, 2f, 0.4f, 0.6f, 0f, -0.03f, 0.08f, 10f);
        Cut(direct);
        yield return new WaitForSeconds(0.25f);
        curator.Eyeline(cam.position);                     // the one place he looks into the lens
        yield return Line("cur0_01");
        yield return new WaitForSeconds(0.35f);
        // the introduction: a steadier medium close-up; he talks to the investigator, not the camera
        curator.Eyeline(At(Visitor));
        Cut(MCU(55f, 1.6f));               // 29 degrees round from the lens-axis close-up: a new angle, not a jump
        yield return Line("cur0_02");
        yield return Pause(0.4f);
        yield return Line("cur0_03");
        yield return Pause(0.5f);
        // he walks down to his table while he talks; the camera backs away in front of him
        yield return WalkAndTalk(TableSpot, "cur0_04");
        yield return SetBookDown();
        // the welcome proper
        Cut(MCU(60f, 0.12f));
        yield return Line("cur0_05");
        yield return Pause(0.35f);
        yield return Line("cur0_06");
        yield return Pause(0.4f);
        // her things: he turns a page, filmed from our side of the table
        yield return TurnPage("cur0_07");
        Cut(ThreeQuarter());
        yield return Line("cur0_08");
        yield return Pause(0.3f);
        yield return Line("cur0_09");
        yield return Pause(0.3f);
        // the note, then the storm answers: the flash comes in through the east window, he stops and looks at it
        Cut(Medium(-1f));
        yield return Line("cur0_10");
        yield return Lightning(0.95f);
        yield return Line("cur0_11");
        yield return Pause(0.4f);
        yield return Exhibit("cur0_12");
        // the landing: close, low, slowly closer
        curator.Expression("intent", 0.8f);
        Cut(PushIn());
        yield return Line("cur0_13");
        yield return Pause(0.5f);
        yield return Line("cur0_14");
        curator.Expression("knowing", 0.8f);
        yield return BlowOut("cur0_15");
        Finish();
    }

    /// <summary>The later visits: he is in his hall, the candle lit, the book in his hand.</summary>
    IEnumerator Return()
    {
        Ignite();
        StartCoroutine(LightHall(0.01f));
        tickLevel = 0.16f;
        StartCoroutine(BedRamp(0f, 0.45f, 3f));
        curator.Eyeline(book.root.position + Vector3.up * 0.1f);            // reading, until he notices us
        // open wide: the hall, and him in it
        float side = scene == 2 ? -1f : 1f;
        Cut(new Shot(28f, 5.6f)
        {
            pos = t => At(new Vector3(side * Mathf.Lerp(6.3f, 5.4f, Ease(t / 9f)), Mathf.Lerp(2.5f, 2.1f, Ease(t / 9f)), Mathf.Lerp(-5.8f, -4.4f, Ease(t / 9f)))),
            look = t => Eyes(), screen = new Vector2(0.5f, 0.62f), follow = 1.2f, handheld = 0.04f,
        });
        fadeSpeed = 0.6f; fadeTarget = 0f;
        yield return new WaitForSeconds(scene == 1 ? 0.9f : 1.6f);
        gal.storm.Strike(3.4f, 0.6f);
        yield return new WaitForSeconds(scene == 1 ? 0.9f : 1.6f);
        curator.Eyeline(At(Visitor));

        var lines = Lines();
        string exhibitLine = scene == 1 ? "cur1_02" : scene == 2 ? "cur2_03" : "cur3_03";
        string bookLine = scene == 3 ? "cur3_07" : null;
        string stormLine = scene == 1 ? "cur1_06" : scene == 2 ? "cur2_08" : "cur3_02";
        int beat = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            string id = lines[i];
            bool last = i == lines.Count - 1;
            if (i == 0) { yield return WalkAndTalk(TableSpot, id); yield return SetBookDown(); continue; }
            if (last) { yield return BlowOut(id); break; }
            // an insert or a storm beat ends its shot: the next line always opens a new set-up on him
            if (id == exhibitLine || (scene == 3 && id.StartsWith("cur3_") && !char.IsDigit(id[5]))) { yield return Exhibit(id); beat = 0; continue; }
            if (id == bookLine) { yield return TurnPage(null); yield return TurnPage(id); beat = 0; continue; }
            if (id == stormLine) { Cut(Medium(-1f)); yield return Lightning(scene == 3 ? 0.8f : 1.4f); yield return Line(id); yield return Pause(0.4f); beat = 0; continue; }
            // the rest: a new set-up every two lines, closer as the scene goes on; the last before the candle is the closest
            if (beat % 2 == 0)
            {
                bool nearEnd = i >= lines.Count - 3;
                Cut(nearEnd ? PushIn() : (beat / 2) % 2 == 0 ? MCU(60f, 0.12f) : ThreeQuarter());
                if (nearEnd) curator.Expression("intent", 0.8f);
            }
            beat++;
            yield return Line(id);
            yield return Pause(0.4f);
        }
        Finish();
    }

    // ------------------------------------------------------------------ test benches (capture tool only)
    IEnumerator LabRun(string lab)
    {
        Ignite();
        StartCoroutine(LightHall(0.01f));
        bedLevel = 0.3f;
        fade = 0f; fadeTarget = 0f;
        yield return null;
        if (lab == "walk")
        {
            // starts, cruising, a curve, stops, turns on the spot: filmed low at the feet, then wide
            Cut(new Shot(35f, 5.6f) { pos = t => Track(curator.transform.position, Vector3.right * 2.6f, 0.45f), look = t => curator.transform.position + Vector3.up * 0.35f, screen = new Vector2(0.5f, 0.42f), follow = 0.3f, handheld = 0f });
            yield return new WaitForSeconds(0.8f);
            yield return curator.HeroWalk(new Vector3(-4.6f, 0f, 5.0f), 1.15f);
            yield return new WaitForSeconds(0.6f);
            Cut(new Shot(24f, 8f) { pos = t => At(new Vector3(4.5f, 1.4f, 2.0f)), look = t => curator.transform.position + Vector3.up * 0.9f, screen = new Vector2(0.5f, 0.45f), follow = 0.6f, handheld = 0f });
            yield return curator.HeroWalk(ShelfSpot, 1.05f, transform.eulerAngles.y);
            yield return new WaitForSeconds(0.8f);
            Cut(new Shot(28f, 5.6f) { pos = t => At(ShelfSpot + new Vector3(0.4f, 0.6f, -2.4f)), look = t => curator.transform.position + Vector3.up * 0.5f, screen = new Vector2(0.5f, 0.42f), follow = 0.5f, handheld = 0f });
            yield return curator.HeroTurn(transform.eulerAngles.y + 180f);
            yield return new WaitForSeconds(0.8f);
            yield return curator.HeroTurn(transform.eulerAngles.y + 90f);
            yield return new WaitForSeconds(0.6f);
            yield return curator.HeroTurn(transform.eulerAngles.y + 180f);
            Cut(new Shot(35f, 5.6f) { pos = t => Track(curator.transform.position, Vector3.left * 2.4f, 0.5f), look = t => curator.transform.position + Vector3.up * 0.45f, screen = new Vector2(0.5f, 0.42f), follow = 0.3f, handheld = 0f });
            yield return curator.HeroWalk(TableSpot, 1.05f, transform.eulerAngles.y + 180f);
            yield return new WaitForSeconds(1.5f);
        }
        else if (lab == "hands")
        {
            if (scene == 0)
            {
                curator.transform.position = At(ShelfSpot); curator.transform.rotation = transform.rotation;
                yield return new WaitForSeconds(0.6f);
                Cut(new Shot(35f, 2.8f) { pos = t => At(ShelfSpot + new Vector3(-1.35f, 1.62f, -1.05f)), look = t => Vector3.Lerp(Eyes(), At(gal.shelfBookPos), 0.5f), screen = new Vector2(0.55f, 0.6f), follow = 0.6f, handheld = 0f, focusOn = t => book.root.position });
                yield return TakeBookFromShelf();
                yield return curator.HeroTurn(transform.eulerAngles.y + 180f);
                yield return new WaitForSeconds(0.5f);
                curator.transform.position = At(TableSpot);
                yield return new WaitForSeconds(0.5f);
            }
            yield return SetBookDown();
            yield return TurnPage(null);
            yield return Exhibit("cur0_12");
            yield return BlowOut("cur0_15");
        }
        else if (lab == "face")
        {
            curator.transform.position = At(TableSpot); curator.transform.rotation = transform.rotation * Quaternion.Euler(0, 180f, 0);
            Cut(Framed(85f, 4f, 0.32f, 0.6f, 0f, -0.04f, 0f, 12f, 0f));
            yield return new WaitForSeconds(1.0f);
            for (int v = 0; v < VisemeTrack.Count; v++) { HeroFace.DebugViseme = v; yield return new WaitForSeconds(0.5f); }
            HeroFace.DebugViseme = -1;
            curator.Blink(); yield return new WaitForSeconds(0.6f);
            yield return Line("cur0_02");
            yield return Line("cur0_06");
            Cut(MCU(60f, 0.12f));
            yield return Line("cur0_13");
            curator.Expression("smile", 0.5f); yield return new WaitForSeconds(1.2f);
            curator.Expression("grave", 0.5f); yield return new WaitForSeconds(1.2f);
            curator.Expression("knowing", 0.5f); yield return new WaitForSeconds(0.8f);
        }
        else if (lab == "storm")
        {
            curator.transform.position = At(TableSpot); curator.transform.rotation = transform.rotation * Quaternion.Euler(0, 180f, 0);
            Cut(Medium(-1f));
            yield return new WaitForSeconds(1.5f);
            yield return Lightning(0.9f);
            Cut(new Shot(24f, 8f) { pos = t => At(new Vector3(-4.5f, 2.2f, -4f)), look = t => At(new Vector3(2f, 1.5f, 3f)), screen = new Vector2(0.5f, 0.5f), follow = 2f, handheld = 0f });
            yield return new WaitForSeconds(1f);
            gal.storm.Strike(0.6f, 1f);
            yield return new WaitForSeconds(4f);
            Cut(PushIn());
            gal.storm.Strike(2.5f, 0.8f);
            yield return Line("cur0_11");
            yield return new WaitForSeconds(2f);
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
                string answer = d.panditAnswer == 0 ? "cur2_truth" : d.panditAnswer == 1 ? "cur2_people" : "cur2_both";
                if (i == 6 && d.panditAnswer >= 0 && Voiced(answer)) l.Add(answer);
            }
        }
        else
        {
            string ally = d.branch == 0 && d.allyRaj ? "cur3_raj" : d.branch == 1 && d.allyRohan ? "cur3_rohan" : d.branch == 2 && d.allyReddy ? "cur3_reddy" : null;
            if (ally != null && !Voiced(ally)) ally = null;          // the recorded general line instead
            for (int i = 1; i <= 10; i++) l.Add(i == 3 && ally != null ? ally : "cur3_" + i.ToString("00"));
        }
        return l;
    }

    /// <summary>Whether a reaction line can join its scene: in a scene the actor recorded (VoTiming has its lines), only a
    /// recorded reaction does, so his voice never turns into text-to-speech mid-scene.</summary>
    static bool Voiced(string id)
    {
        return VoTiming.Get("cur2_01") == null || VoTiming.Get(id) != null;
    }

    // ------------------------------------------------------------------ performance beats
    /// <summary>Reach, hook the top of the spine, tip the book out, take it by the spine, draw it out, bring it to him.</summary>
    IEnumerator TakeBookFromShelf()
    {
        var b = book.root;
        curator.Eyeline(b.position + Vector3.up * 0.05f);
        yield return curator.HandToCo(1, HandGoal.On(b, StoryBookModel.Grip("hook"), Fingers.Hook).Elbow(new Vector3(0.6f, -0.6f, -0.2f)), 1.5f, 0.08f);     // unhurried: a high shelf
        // insert: the hand, the spine, the title catching the candle
        Vector3 spine = b.TransformPoint(new Vector3(0, 0, -StoryBookModel.W / 2f));
        Vector3 bookAt = b.position;
        Cut(new Shot(40f, 4f)
        {
            pos = t => spine + Dir(new Vector3(0.78f, -0.1f, -0.32f)),
            look = t => Vector3.Lerp(bookAt, b.position, 0.6f) + Vector3.up * 0.08f + Dir(Vector3.back) * 0.05f, screen = new Vector2(0.5f, 0.5f), follow = 0.6f, handheld = 0.04f,
            focusOn = t => b.position,
        });
        sound.Play("book_take", b.position, -12f, 0.6f);
        sfx.PlayOneShot(stingClip, 0.35f);
        // tip it out about its bottom front edge; the fingers stay on it
        Vector3 pivotLocal = new Vector3(0, -StoryBookModel.H / 2f, -StoryBookModel.W / 2f);
        Vector3 pivot = b.TransformPoint(pivotLocal); Quaternion r0 = b.rotation; Vector3 p0 = b.position;
        Vector3 axis = b.right;
        for (float t = 0; t < 0.9f; )
        {
            if (Time.deltaTime > 0f) t += Time.deltaTime;
            var q = Quaternion.AngleAxis(-17f * Ease(t / 0.9f), axis);
            b.SetPositionAndRotation(pivot + q * (p0 - pivot), q * r0);
            yield return null;
        }
        // take the top of it, now clear of its neighbours, and from now on it goes where the hand goes
        yield return curator.HandToCo(1, HandGoal.On(b, StoryBookModel.Grip("spineTop"), Fingers.Edge).Elbow(new Vector3(0.75f, -0.5f, -0.25f)), 0.5f, 0.015f);
        curator.Attach(b, 1);
        // the wrist cannot always bend far enough at the high shelf, so the book is taken where the hand really is and
        // the hand settles into the proper grip on the spine while it draws the book out (a slide, never a jump)
        curator.Regrip(b, CharacterNPC.GripOffset(StoryBookModel.Grip("spineTop")), 0.6f);
        // draw it out towards him, straightening it as it clears the shelf
        Pose palm = curator.PalmPose(1);
        var outPose = new Pose(palm.position - Dir(Vector3.forward) * 0.27f + Vector3.up * 0.02f, Quaternion.AngleAxis(17f, axis) * palm.rotation);
        yield return curator.HandToCo(1, HandGoal.World(outPose, Fingers.Edge).Elbow(new Vector3(0.7f, -0.8f, -0.3f)), 0.95f, 0f);
        // and brings it in front of him, hanging from his hand, the cover towards whoever he faces
        curator.HandTo(1, BookGoal(BookCarry), 1.1f, 0.03f);
        curator.Eyeline(At(Visitor) + Vector3.up * 0.2f);
        yield return new WaitForSeconds(1.15f);
    }

    IEnumerator BedRamp(float from, float to, float dur)
    {
        for (float t = 0; t < dur; ) { if (Time.deltaTime > 0f) t += Time.deltaTime; bedLevel = Mathf.Lerp(from, to, t / dur); yield return null; }
        bedLevel = to;
    }

    IEnumerator Pause(float s) { for (float t = 0; t < s && !skipAll; ) { if (Time.deltaTime > 0f) t += Time.deltaTime; yield return null; } }

    /// <summary>He walks somewhere while speaking a line; the camera backs away in front of him.</summary>
    IEnumerator WalkAndTalk(Vector3 to, string id)
    {
        curator.Eyeline(At(Visitor));
        var walk = StartCoroutine(curator.HeroWalk(to, 1.05f, transform.eulerAngles.y + 180f));
        Cut(new Shot(35f, 2.8f)
        {
            pos = t => EyesLevel(curator.transform.position + Fwd() * 3.3f - Right() * 0.4f, -0.25f),
            look = t => Eyes(), screen = new Vector2(0.52f, 0.7f), follow = 0.35f, handheld = 0.1f,
        });
        var line = StartCoroutine(Line(id));
        yield return walk;
        yield return line;
        yield return new WaitForSeconds(0.35f);
    }

    /// <summary>At his table: the book is laid flat on the velvet, facing us, and he opens the cover.</summary>
    IEnumerator SetBookDown()
    {
        if (book == null) yield break;
        var b = book.root;
        // high, from our side of the table: the book, his hands, his face bowed over them
        Cut(new Shot(28f, 4f)
        {
            pos = t => At(new Vector3(-0.75f, 2.2f, 0.75f)),
            look = t => Vector3.Lerp(At(BookOnTable), Eyes(), 0.42f), screen = new Vector2(0.5f, 0.52f), follow = 0.8f, handheld = 0.05f,
            focusOn = t => At(BookOnTable),
        });
        curator.Eyeline(At(BookOnTable));
        curator.lean = 22f;                  // a real bow: the table is low and the book goes down at arm's length
        // flat, cover up, its top towards him so it reads from our side; spine on his right, where the hand is
        Quaternion flat = transform.rotation * Quaternion.LookRotation(Vector3.right, Vector3.forward);
        Vector3 rest = At(BookOnTable);
        // tilt it down to a hand's breadth above the velvet...
        yield return curator.HandToCo(1, PlaceGoal(b, new Pose(rest + Vector3.up * 0.07f, flat * Quaternion.AngleAxis(-6f, Vector3.forward))), 1.2f, 0.03f);
        // ...lower it until its far edge touches the velvet, the near end still in his hand and raised (as people lay a
        // book down: the hand never has to reach the table itself)...
        const float tip = 11f;
        Quaternion tilted = flat * Quaternion.AngleAxis(-tip, Vector3.forward);
        Vector3 farEdge = rest + flat * new Vector3(-StoryBookModel.T / 2f, -StoryBookModel.H / 2f, 0f);       // on the velvet
        Vector3 touch = farEdge + tilted * new Vector3(StoryBookModel.T / 2f, StoryBookModel.H / 2f, 0f) + Vector3.up * 0.002f;
        yield return curator.HandToCo(1, PlaceGoal(b, new Pose(touch, tilted)), 0.5f, 0f);
        curator.Detach(b, transform);
        // ...and lets it tip flat about that edge. A gap here means the hand fell short of the touch; the book then
        // settles down from where it is, never sideways, and the later shots aim at where it really lies
        float gap = Vector3.Distance(b.position, touch);
        if (gap > 0.02f) Debug.Log(string.Format("[Curator] book set down {0:F3} m from its spot", gap));
        Vector3 shift = rest - touch; shift.y = 0f;          // tipping flat moves the centre this much along the table
        Vector3 here = new Vector3(b.position.x + shift.x, rest.y, b.position.z + shift.z);
        Vector3 p0 = b.position; Quaternion r0 = b.rotation;
        for (float t = 0; t < 0.22f; )
        {
            if (Time.deltaTime > 0f) t += Time.deltaTime;
            float k = t / 0.22f; k *= k;                 // it falls the last few degrees: slow, then faster
            b.SetPositionAndRotation(Vector3.Lerp(p0, here, k), Quaternion.Slerp(r0, flat, k));
            yield return null;
        }
        b.SetPositionAndRotation(here, flat);
        bookRest = here;
        sound.Play("book_down", here, -10f, 0.6f);
        // the fingers slide out from under the spine edge
        Pose palm = curator.PalmPose(1);
        yield return curator.HandToCo(1, HandGoal.World(new Pose(palm.position - Dir(Vector3.right) * 0.07f + Vector3.up * 0.035f, palm.rotation), Fingers.Relaxed), 0.35f, 0f);
        yield return OpenCover();
        curator.HandFree(1, 1.0f);
        curator.lean = 4f;
        curator.Eyeline(At(Visitor));
        yield return new WaitForSeconds(0.4f);
    }

    HandGoal PlaceGoal(Transform prop, Pose worldPose)
    {
        var g = new HandGoal { fingers = Fingers.Edge, elbow = new Vector3(0.8f, -1f, -0.2f) };
        g.palm = () => curator.PalmFor(prop, worldPose);
        return g;
    }

    /// <summary>The right hand reaches across, lifts the cover by its edge and lays it open.</summary>
    IEnumerator OpenCover()
    {
        var cov = book.cover;
        yield return curator.HandToCo(1, HandGoal.On(cov, StoryBookModel.CoverGrip(), Fingers.Pinch).Elbow(new Vector3(0.4f, -1f, -0.2f)), 0.75f, 0.05f);
        sound.Play("book_open", book.root.position, -12f, 0.6f);
        Quaternion c0 = cov.localRotation;
        for (float t = 0; t < 0.85f; )
        {
            if (Time.deltaTime > 0f) t += Time.deltaTime;
            cov.localRotation = c0 * Quaternion.Euler(0, 112f * Ease(t / 0.85f), 0);
            yield return null;
        }
        // let go: it falls the rest of the way open
        curator.HandFree(1, 0.9f);
        for (float t = 0; t < 0.42f; )
        {
            if (Time.deltaTime > 0f) t += Time.deltaTime;
            float k = t / 0.42f;
            cov.localRotation = c0 * Quaternion.Euler(0, 112f + 67f * k * k, 0);
            yield return null;
        }
        cov.localRotation = c0 * Quaternion.Euler(0, 179f, 0);
        sound.Play("page", book.root.position, -18f, 0.6f);
    }

    /// <summary>He turns a page for us (an insert from our side): pinch the corner, carry it over, let it fall.</summary>
    IEnumerator TurnPage(string id)
    {
        if (book == null) { if (id != null) yield return Line(id); yield break; }
        Cut(new Shot(40f, 4f)
        {
            pos = t => BookRest() + Dir(new Vector3(-0.05f, 0.72f, -0.62f)),
            look = t => BookRest() + Vector3.up * 0.02f + Dir(Vector3.forward) * 0.08f, screen = new Vector2(0.5f, 0.45f), follow = 1.5f, handheld = 0.04f,
            focusOn = t => BookRest(),
        });
        curator.Eyeline(BookRest());
        curator.lean = 18f;
        // the page he lifts carries the words we were reading; the next page is under it
        var leaf = book.PageLeaf(pagesTurned, book.pageText.text);
        book.pageText.text = NextPage(pagesTurned);
        // the corner nearest him (the page's top) at the free edge
        Func<Pose> corner = () =>
        {
            Vector3 c = leaf.Point(0.96f, 0.9f);
            Quaternion f = leaf.Frame(0.96f);
            // pinch: fingers along the page towards the spine and over it, the palm facing down onto it
            return new Pose(c + f * new Vector3(0.012f, 0.0f, 0.03f), f * Quaternion.LookRotation(Vector3.back + Vector3.right * 0.4f, Vector3.left));
        };
        yield return curator.HandToCo(1, new HandGoal { palm = corner, fingers = Fingers.Pinch, elbow = new Vector3(0.5f, -1f, -0.2f) }, 0.7f, 0.04f);
        sound.Play("page", book.root.position, -14f, 0.6f);
        Coroutine line = id != null ? StartCoroutine(Line(id)) : null;
        for (float t = 0; t < 0.95f; )
        {
            if (Time.deltaTime > 0f) t += Time.deltaTime;
            float a = 150f * Ease(t / 0.95f);
            leaf.Shape(a, 40f * Mathf.Sin(a * Mathf.Deg2Rad));
            yield return null;
        }
        curator.HandFree(1, 0.9f);
        for (float t = 0; t < 0.4f; )
        {
            if (Time.deltaTime > 0f) t += Time.deltaTime;
            float k = t / 0.4f;
            leaf.Shape(150f + 30f * k * k, 40f * Mathf.Sin(150f * Mathf.Deg2Rad) * (1f - k));
            yield return null;
        }
        leaf.Shape(179.5f, 0f);
        sound.Play("page", book.root.position, -20f, 0.6f);
        pagesTurned++;
        curator.lean = 4f;
        curator.Eyeline(At(Visitor));
        if (line != null) yield return line;
        yield return Pause(0.3f);
    }

    /// <summary>The exhibit on the table, in an insert, while he speaks about it.</summary>
    IEnumerator Exhibit(string id)
    {
        // framed from what is really on the table (the exhibits differ in size from a key to a deed): the exhibit's
        // width fills a little over half the frame, seen from our side, 35 degrees down, drifting slightly closer
        var b = new Bounds(exhibit.position, Vector3.zero);
        foreach (var r in exhibit.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        float radius = Mathf.Max(0.06f, Mathf.Max(b.extents.x, b.extents.z));
        const float lens = 65f;
        float dist = radius / (0.55f * (24.89f / (2f * lens)));
        Vector3 from = Dir(new Vector3(-0.5f, 0f, -0.62f).normalized * Mathf.Cos(35f * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(35f * Mathf.Deg2Rad));
        Vector3 centre = b.center;
        Cut(new Shot(lens, 4f)
        {
            pos = t => centre + from * dist * Mathf.Lerp(1f, 0.92f, Ease(t / 8f)),
            look = t => centre, screen = new Vector2(0.5f, 0.5f), follow = 0f, handheld = 0.03f,
            focusOn = t => centre,
        });
        curator.Eyeline(exhibit.position);
        yield return Line(id);
        curator.Eyeline(At(Visitor));
        yield return Pause(0.35f);
    }

    /// <summary>
    /// The storm answers: a strike `km` away. The cold light comes in through the east window across the table and
    /// his face. His eyes go to the window first, then his head; his brows lift; he waits for the thunder to arrive
    /// (about 3 s per km) and roll, smiles slightly, and turns back to us.
    /// </summary>
    IEnumerator Lightning(float km)
    {
        yield return Pause(0.35f);
        gal.storm.Strike(km, 1f);
        Vector3 window = At(EastWindow + new Vector3(0f, -2.2f, 0f));
        curator.Glance(window, 0.6f);
        yield return Pause(0.22f);
        curator.Eyeline(window);
        curator.Expression("raised", 0.25f);
        float thunderAt = km * 1000f / 343f;
        yield return Pause(Mathf.Max(0.9f, thunderAt - 0.35f));
        curator.Expression("knowing", 1.2f);
        yield return Pause(1.1f);
        curator.Eyeline(At(Visitor));
        yield return Pause(0.55f);
    }

    /// <summary>The last line, the candle raised to his face, and then darkness.</summary>
    IEnumerator BlowOut(string id)
    {
        curator.lean = 0f;
        curator.Eyeline(At(Visitor));
        Cut(Framed(65f, 2.2f, 0.55f, 0.66f, 8f, -0.14f, 0.15f, 12f, 0.06f));
        yield return curator.HandToCo(0, CandleGoal(CandleRaised), 1.3f, 0.02f);
        yield return Line(id);
        yield return Pause(0.5f);
        // he leans to the flame, rounds his lips and blows; the flame bends away and gutters out
        curator.lean = 5f;
        curator.Eyeline(Flame());
        curator.Expression("blow", 0.22f);
        yield return Pause(0.3f);
        sound.Play("candle_blow", Flame(), -6f, 0.3f);
        Vector3 away = (Flame() - curator.EyePoint()); away.y = 0;
        flameWobble.gustDir = away.normalized;
        for (float t = 0; t < 0.38f; )
        {
            if (Time.deltaTime > 0f) t += Time.deltaTime;
            float k = t / 0.38f;
            flameWobble.gust = Mathf.Clamp01(k * 2f); candleFlicker.gust = Mathf.Clamp01(k * 1.6f);
            candleFlicker.level = 1f - k * k;
            candleFlame.transform.localScale = new Vector3(0.018f, 0.04f, 0.018f) * (1f - k * 0.7f);
            yield return null;
        }
        candleFlame.SetActive(false); candleFlicker.level = 0f; candleFlicker.gust = 0f;
        Smoke(candle.TransformPoint(new Vector3(0, 0.19f, 0)));
        curator.Expression("knowing", 0.5f);
        curator.lean = 0f;
        // every flame in the hall goes out with his, in a wave away from him; only the night is left at the windows
        StartCoroutine(SnuffHall(1.1f));
        tickLevel = 0f;
        sfx.PlayOneShot(boomClip, 0.45f);
        StartCoroutine(BedRamp(bedLevel, 0f, 1.8f));
        curator.Eyeline(At(Visitor));
        yield return new WaitForSeconds(1.4f);
        fadeSpeed = 1.0f; fadeTarget = 1f;
        StartCoroutine(BedsOut(2.0f));               // the rain and the room go with the picture, not after it
        while (fade < 0.99f) yield return null;
        yield return new WaitForSeconds(1.0f);
    }

    IEnumerator BedsOut(float dur)
    {
        for (float t = 0; t < dur && sound != null; )
        {
            if (Time.deltaTime > 0f) t += Time.deltaTime;
            sound.master = 1f - Ease(t / dur);
            yield return null;
        }
    }

    // ------------------------------------------------------------------ dialogue
    /// <summary>Play-test only: does what Space does to the current line (the harness cannot press keys in batch mode).</summary>
    public static bool TestSkipLine;
    /// <summary>The line being spoken now (null between lines), for the play-test.</summary>
    public string LineNow { get; private set; }

    IEnumerator Line(string id)
    {
        var clip = VO(id);
        string text = DialogueDB.Text(id);
        float dur = clip != null ? clip.length + 0.2f : Mathf.Max(2.4f, text.Length * 0.065f);
        if (clip != null) curator.Say(clip);
        sub = DialogueDB.Speaker(id) + ":  " + text; subUntil = Time.time + dur + 0.1f;
        LineNow = id;
        yield return null;
        for (float t = 0; t < dur; )
        {
            var kb = Keyboard.current;
            if (skipAll) break;
            if (Time.timeScale > 0f && (TestSkipLine || GameInput.InteractPressed || (kb != null && kb.spaceKey.wasPressedThisFrame))) { TestSkipLine = false; curator.Silence(); break; }
            t += Time.deltaTime;                     // 0 while paused: the line waits with the sound
            yield return null;
        }
        sub = null;
        LineNow = null;
    }

    // ------------------------------------------------------------------ the camera
    /// <summary>
    /// A camera set-up. Positions and aim are functions of the time since the cut, so a shot can track a moving
    /// subject; `look` is placed at `screen` (0..1 from the left, 0..1 from the bottom) and followed with a lag of
    /// `follow` seconds (no jitter from the animation); `lens` is a focal length on a Super 35 sensor and does not
    /// change inside a shot; `fstop` sets the depth of field; focus is on his eyes unless `focusOn` says otherwise.
    /// </summary>
    class Shot
    {
        public float lens, fstop;
        public Func<float, Vector3> pos, look, focusOn;
        public Vector2 screen = new Vector2(0.5f, 0.6f);
        public float follow = 0.4f, handheld = 0.1f;
        public Shot(float lens, float fstop) { this.lens = lens; this.fstop = fstop; }
    }

    Shot shot; float shotT; bool camOn, newCut;
    Vector3 aimSmooth, aimVel, posNow; float focusNow;
    Quaternion earlyCameraRotation;
    Vector3 camLocal0; float fov0, near0; bool phys0; float focal0; Vector2 sensor0; Camera.GateFitMode gate0;
    DepthOfField dof; float dofFocal0, dofAperture0;
    public int Cuts { get; private set; }

    void DetachCamera()
    {
        if (scene <= 1) earlyCameraRotation = cam.localRotation;
        camLocal0 = cam.localPosition; fov0 = camComp.fieldOfView; near0 = camComp.nearClipPlane;
        phys0 = camComp.usePhysicalProperties; focal0 = camComp.focalLength; sensor0 = camComp.sensorSize; gate0 = camComp.gateFit;
        player.enabled = false;
        var cc = player.GetComponent<CharacterController>(); if (cc != null) cc.enabled = false;
        cam.SetParent(transform, true);
        AutoFocus.FocusOn = focus;
        var af = cam.GetComponent<AutoFocus>();
        dof = af != null ? af.dof : null;
        if (dof != null) { dofFocal0 = dof.focalLength.value; dofAperture0 = dof.aperture.value; }
        camComp.usePhysicalProperties = true;
        camComp.sensorSize = new Vector2(24.89f, 18.66f);          // Super 35
        camComp.gateFit = Camera.GateFitMode.Horizontal;
        camComp.nearClipPlane = 0.03f;
    }

    void Cut(Shot s)
    {
        shot = s; shotT = 0f; newCut = true; camOn = true; Cuts++;
    }

    void LateUpdate()
    {
        if (done) return;          // finished: the next scene's lighting is already in place
        // Early galleries own their ambient settings, so a static shot needs no repeated lighting writes.
        if (scene > 1 || !Mathf.Approximately(lastGalleryLevel, lightLevel))
        {
            CuratorGallery.Mood(null, lightLevel);
            lastGalleryLevel = lightLevel;
        }
        float duck = sound != null ? sound.DuckGain : 1f;
        if (ambience != null) ambience.volume = bedLevel * duck;
        if (tick != null) tick.volume = tickLevel * duck;
        if (!camOn || shot == null || cam == null) return;
        float dt = Time.deltaTime;
        shotT += dt;
        Vector3 want = shot.pos(shotT), aim = shot.look(shotT);
        // never inside the furniture or the walls: pull in towards the subject if something is in the way
        Vector3 toCam = want - aim;
        if (toCam.sqrMagnitude > 0.01f && Physics.SphereCast(aim, 0.08f, toCam.normalized, out var hit, toCam.magnitude, ~0, QueryTriggerInteraction.Ignore)
            && !hit.collider.transform.IsChildOf(curator.transform))
            want = aim + toCam.normalized * Mathf.Max(0.25f, hit.distance - 0.1f);
        if (newCut) { aimSmooth = aim; aimVel = Vector3.zero; posNow = want; }
        else if (dt > 0f)
        {
            aimSmooth = shot.follow <= 0.01f ? aim : Vector3.SmoothDamp(aimSmooth, aim, ref aimVel, shot.follow, 50f, dt);
            posNow = want;
        }
        camComp.focalLength = shot.lens;
        float vfov = camComp.fieldOfView, hfov = Camera.VerticalToHorizontalFieldOfView(vfov, camComp.aspect);
        // put the subject at its place in the frame
        Quaternion r = Quaternion.LookRotation(aimSmooth - posNow);
        float yawOff = Mathf.Atan((shot.screen.x - 0.5f) * 2f * Mathf.Tan(hfov * 0.5f * Mathf.Deg2Rad)) * Mathf.Rad2Deg;
        float pitchOff = Mathf.Atan((shot.screen.y - 0.5f) * 2f * Mathf.Tan(vfov * 0.5f * Mathf.Deg2Rad)) * Mathf.Rad2Deg;
        r = r * Quaternion.Euler(pitchOff, -yawOff, 0f);
        // a little life in the hand-held shots (slow, small; never on the locked-off ones)
        float t = Time.time;
        if (shot.handheld > 0f)
        {
            Vector3 n = new Vector3(Mathf.PerlinNoise(t * 0.23f, 1.7f) - 0.5f, Mathf.PerlinNoise(3.1f, t * 0.19f) - 0.5f, Mathf.PerlinNoise(t * 0.17f, 8.3f) - 0.5f);
            r = r * Quaternion.Euler(n.x * shot.handheld * 2f, n.y * shot.handheld * 2f, n.z * shot.handheld);
        }
        cam.SetPositionAndRotation(posNow, r);
        // focus on the eyes (or the shot's subject), pulled smoothly; snapped on a cut
        Vector3 fp = shot.focusOn != null ? shot.focusOn(shotT) : Eyes();
        float fd = Vector3.Dot(fp - posNow, r * Vector3.forward);
        focusNow = newCut ? fd : Mathf.Lerp(focusNow, fd, 1f - Mathf.Exp(-dt / 0.18f));
        focus.position = posNow + (r * Vector3.forward) * focusNow - Vector3.up * 1.5f;     // AutoFocus measures to FocusOn + 1.5 m
        if (dof != null) { dof.focalLength.value = shot.lens; dof.aperture.value = shot.fstop; }
        if (scene <= 1 && newCut && dof != null) dof.focusDistance.value = Mathf.Max(0.1f, fd);
        if (newCut) AutoFocus.SnapNext = true;
        newCut = false;
    }

    // framing helpers
    Vector3 Eyes() { return curator.EyePoint(); }
    Vector3 Flame() { return candleFlame != null ? candleFlame.transform.position : candle.position + Vector3.up * 0.2f; }
    Vector3 Fwd() { Vector3 f = curator.transform.forward; f.y = 0; return f.normalized; }
    Vector3 Right() { Vector3 r = curator.transform.right; r.y = 0; return r.normalized; }
    static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    /// <summary>A camera position at his eye height plus `dy` (the height follows his animated eyes).</summary>
    Vector3 EyesLevel(Vector3 p, float dy) { p.y = Eyes().y + dy; return p; }
    Vector3 Track(Vector3 subject, Vector3 offsetLocal, float height)
    {
        Vector3 p = subject + Dir(offsetLocal); p.y = transform.position.y + height; return p;
    }

    /// <summary>Vertical field of view of a lens (mm) on the Super 35 gate, at this screen's aspect.</summary>
    float VFov(float lens)
    {
        float h = 2f * Mathf.Atan(24.89f / (2f * lens));
        return 2f * Mathf.Atan(Mathf.Tan(h * 0.5f) / Mathf.Max(1f, camComp.aspect)) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// A framing on his eyes, solved from the lens: `frameH` is how much of him the frame shows (metres, top to bottom,
    /// at his distance), his eyes sit at `eyesY` (0 bottom .. 1 top; the bars cover the outer 8.5%), the camera is
    /// `azimuth` degrees round from straight in front of him (+ towards his left) at his eye height + `dy`. `push`
    /// (0..1) moves it closer over `pushTime` seconds without changing the lens.
    /// Sizes for him (1.95 m): eyes 0.16, close-up 0.36, medium close-up 0.62, medium 1.0, wide-medium 1.6.
    /// </summary>
    Shot Framed(float lens, float fstop, float frameH, float eyesY, float azimuth, float dy, float push = 0f, float pushTime = 12f, float handheld = 0.08f)
    {
        float dist = frameH / (2f * Mathf.Tan(VFov(lens) * 0.5f * Mathf.Deg2Rad));
        float sx = 0.5f + Mathf.Clamp(azimuth / 90f, -1f, 1f) * 0.12f;          // leave room on the side he faces
        return new Shot(lens, fstop)
        {
            pos = t =>
            {
                float d = dist * (1f - push * Ease(t / pushTime));
                Vector3 dir = Quaternion.AngleAxis(-azimuth, Vector3.up) * Fwd();
                return EyesLevel(Eyes() + dir * d, dy);
            },
            look = t => Eyes(), screen = new Vector2(sx, eyesY), follow = 0.5f, handheld = handheld,
        };
    }

    Shot MCU(float lens, float side) { return Framed(lens, 2.4f, 0.62f, 0.64f, side * 18f, -0.04f); }
    Shot ThreeQuarter() { return Framed(50f, 2.8f, 0.85f, 0.66f, 34f, -0.12f); }
    /// <summary>Medium, from his right (away from the east window), so the lightning comes across his face.</summary>
    Shot Medium(float side) { return Framed(40f, 2.8f, 1.0f, 0.67f, side * 28f, -0.18f); }
    Shot PushIn() { return Framed(85f, 2f, 0.42f, 0.6f, 16f, -0.08f, 0.22f, 16f, 0.05f); }     // off the MCU's axis, same side of the line

    // ------------------------------------------------------------------ helpers
    void Ignite()
    {
        candleFlame.SetActive(true);
        candleFlicker.level = 1f;
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
            gal.storm.moonLevel = Mathf.Lerp(1f, 0.45f, k);
            lightLevel = Mathf.Lerp(l0, 0f, k);
            yield return null;
        }
        gal.SetLights(0f);
        lightLevel = 0f;
    }

    void Smoke(Vector3 at)
    {
        // a thin grey wisp off the wick: many faint, overlapping puffs (single ones read as beads), widening as it
        // climbs and curls, gone in a few seconds
        var ps = new GameObject("CandleSmoke").AddComponent<ParticleSystem>();
        ps.transform.SetParent(transform, false);
        ps.transform.position = at;
        ps.transform.rotation = Quaternion.LookRotation(Vector3.up);
        var main = ps.main; main.loop = false; main.duration = 1.8f; main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3f); main.startSpeed = 0.1f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.032f); main.startColor = new Color(0.62f, 0.62f, 0.64f, 0.1f); main.maxParticles = 400;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = new ParticleSystem.MinMaxCurve(150f, AnimationCurve.EaseInOut(0, 1, 1, 0));
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 3f; sh.radius = 0.002f;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 4f));
        var col = ps.colorOverLifetime; col.enabled = true;
        var grad = new Gradient(); grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0f, 0), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.45f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;
        var nz = ps.noise; nz.enabled = true; nz.strength = 0.05f; nz.frequency = 1.2f; nz.scrollSpeed = 0.3f; nz.octaveCount = 2;
        var mat = CuratorGallery.SoftParticle();
        if (mat != null) ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
        ps.Play();
    }

    void Finish()
    {
        if (done) return;
        done = true;
        shot = null; camOn = false;
        sub = null;
        AutoFocus.FocusOn = null;
        RestoreCamera();
        var cc = player.GetComponent<CharacterController>(); if (cc != null) cc.enabled = true;
        player.enabled = true;
        fade = 1f; fadeTarget = 1f;
        if (gal != null && gal.profile != null) LookController.Unregister(gal.profile);
        if (sound != null) sound.FadeOutAndDie(0.3f);         // a skip must not cut the rain off mid-sample
        if (returnToRoom)
        {
            RestoreRoom();
            if (builder.ExtraRoot == gameObject) builder.ExtraRoot = null;
            Destroy(gameObject);
        }
        if (onDone != null) onDone();
    }

    /// <summary>Arjun gets his eyes back: his camera, his lens, his depth of field.</summary>
    void RestoreCamera()
    {
        cam.SetParent(player.transform, false);
        cam.localPosition = camLocal0; cam.localRotation = scene <= 1 ? earlyCameraRotation : Quaternion.identity;
        camComp.usePhysicalProperties = phys0; camComp.focalLength = focal0; camComp.sensorSize = sensor0; camComp.gateFit = gate0;
        camComp.fieldOfView = fov0; camComp.nearClipPlane = near0;
        if (dof != null) { dof.focalLength.value = dofFocal0; dof.aperture.value = dofAperture0; }
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
        if (gal != null && gal.profile != null) LookController.Unregister(gal.profile);
        if (!done && cam != null && player != null && cam.parent == transform)      // quit mid-scene: never take the camera down with us
        {
            RestoreCamera();
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

    // ------------------------------------------------------------------ sound (synthesised placeholders and the score bed)
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
