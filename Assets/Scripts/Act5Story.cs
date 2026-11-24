using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Act V - "Sunrise" (4:00-6:00 AM) and the finale.
///  1. The NGO shelter in the old school by the river: families asleep downstairs, Priya upstairs.
///  2. The Curator's Gallery: the world freezes; a candlelit hall outside time, three caskets on red velvet.
///     Bronze = Justice (needs 18+ pieces of evidence), Bone = Sacrifice (always), Iron = Revolution (needs
///     Raj's recordings or the Municipal emails). The chosen casket opens, the others crumble to dust.
///  3. The ending: Justice (the courtroom, with whichever allies you made), Sacrifice (Arjun alone, a reprieve),
///     or Revolution (everything leaked, a city on fire with outrage). Epilogue, the Curator's verdict, credits.
/// </summary>
public partial class Act5Story : ActStoryBase
{
    protected override int ActNumber { get { return 5; } }
    protected override string ActTitle { get { return "ACT V  -  SUNRISE"; } }
    protected override string ActSubtitle { get { return "The NGO shelter by the river   |   4:00 AM"; } }
    protected override LookPreset Look { get { return LookPreset.Apartment; } }
    // the Gallery's confirmation and the endings' lines on the compass and phrase subtitles, as in Acts I-IV
    protected override bool UseCompass { get { return true; } }

    public const int EvidenceForJustice = 18;

    static readonly Vector3 U = new Vector3(30f, 0f, 0f);     // upstairs room
    static readonly Vector3 G = new Vector3(0f, 0f, 80f);     // the Gallery
    static readonly Vector3 C = new Vector3(0f, 0f, 160f);    // the courtroom
    static readonly Vector3 T = new Vector3(80f, 0f, 0f);     // a rooftop terrace (dawn / dusk)

    CharacterNPC priya, resident, curator, judge, lawyer, deshpande;
    Transform stairs;
    readonly Transform[] caskets = new Transform[3];
    readonly Transform[] lids = new Transform[3];
    readonly Light[] casketLights = new Light[3];
    CuratorGallery gallery;          // the shared Gallery (the Curator's interludes happen in the same hall)
    CuratorSound gallerySound;       // rain and thunder in the hall (only while we are in it)
    bool inGallery; float galleryLevel;
    GameObject skyQuad, sun;
    const float TerraceSkyRotation = 0f;      // turn the panorama (degrees) if the sunrise is not where the terrace faces
    Transform book; TextMesh bookLabel;
    int chosen = -1;
    float white;               // white-out overlay (the world dissolving)
    AudioClip shelterBed, galleryBed, courtBed, cheerClip, chantClip, gavelClip;

    // ===================================================================== WORLD
    protected override void BuildWorld()
    {
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.07f, 0.07f, 0.1f); RenderSettings.fogDensity = 0.01f;
        CinematicLook.Ambient(new Color(0.36f, 0.38f, 0.5f), new Color(0.26f, 0.25f, 0.3f), new Color(0.1f, 0.09f, 0.1f));
        SetSun(new Color(0.55f, 0.62f, 0.95f), 0.4f, new Vector3(45f, 30f, 0f));
        camComp.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
        PriyaRoomPlayer.Surface = 0;
        // each place under its own root, so the one Arjun is not in can be switched off: with no occlusion culling the
        // Gallery's candles, shelving and brass (about 1 M triangles, most of them shadow casters) were drawn behind the
        // shelter's walls, and the reverse
        shelterSet = Group("ShelterSet", BuildShelter);
        gallerySet = Group("GallerySet", BuildGallery);
        gallerySet.SetActive(false);
        // the courtroom and the roof are built after the choice, during the white-out (EnsureEndingSets)
        shelterBed = MakeNoiseBed(0.0f, 0.05f, 0.25f);
        galleryBed = Ticking();
        courtBed = MakeNoiseBed(0.01f, 0.08f, 0.4f);
        cheerClip = Crowd(4f, false);
        chantClip = Crowd(6f, true);
        gavelClip = Tone(170f, 0.35f, 22f, 0.8f, true);
        ambience.clip = shelterBed; ambience.Play();
    }

    GameObject shelterSet, gallerySet;

    static Transform Deep(Transform t, string n)
    {
        if (t.name == n) return t;
        foreach (Transform c in t) { var r = Deep(c, n); if (r != null) return r; }
        return null;
    }

    /// <summary>Builds a place and moves everything it made under one root (its lights, audio and characters with it).</summary>
    GameObject Group(string name, System.Action build)
    {
        var before = new HashSet<Transform>();
        foreach (Transform c in transform) before.Add(c);
        build();
        var root = new GameObject(name);
        root.transform.SetParent(transform, false);
        var made = new List<Transform>();
        foreach (Transform c in transform) if (!before.Contains(c) && c != root.transform) made.Add(c);
        foreach (var c in made) c.SetParent(root.transform, true);
        return root;
    }

    /// <summary>Switches the places on and off at a cut (in a black or white frame): only where Arjun is is drawn.</summary>
    void Show(bool shelter, bool galleryOn)
    {
        if (shelterSet != null) shelterSet.SetActive(shelter);
        if (gallerySet != null) gallerySet.SetActive(galleryOn);
    }

    void BuildShelter()
    {
        Room("Shelter", Vector3.zero, new Vector3(14f, 3.4f, 10f), new Color(0.62f, 0.7f, 0.6f), new Color(0.5f, 0.45f, 0.4f), "stone", "facade");
        B("Blackboard", new Vector3(0, 1.7f, 4.95f), new Vector3(4f, 1.3f, 0.05f), new Color(0.08f, 0.15f, 0.1f), false, 0.3f);
        GameKit.Text(transform, "GOVT. PRIMARY SCHOOL  -  1931", new Vector3(0, 2.75f, 4.9f), Quaternion.identity, 0.14f, new Color(0.85f, 0.85f, 0.75f));
        // an old schoolroom people are living in: desks pushed aside, trunks and bundles, water pots, a few chairs
        Tex(B("BoardFrame", new Vector3(0, 1.7f, 4.97f), new Vector3(4.2f, 1.5f, 0.04f), new Color(0.35f, 0.22f, 0.12f), false), "wood", 0.9f, new Color(0.35f, 0.22f, 0.12f));
        Tex(B("ChalkRail", new Vector3(0, 1.02f, 4.9f), new Vector3(4.2f, 0.04f, 0.1f), new Color(0.35f, 0.22f, 0.12f), false), "wood", 0.9f, new Color(0.35f, 0.22f, 0.12f));
        for (int i = 0; i < 5; i++) Prop("SchoolChair_01", new Vector3(-6.3f + (i % 2) * 0.25f, i > 2 ? 0.45f : 0f, 1.2f - (i % 3) * 0.75f), 80f + i * 9f, 1f, i > 2 ? new Vector3(0, 0, 4f) : (Vector3?)null);
        Prop("metal_office_desk", new Vector3(5.6f, 0, -3.9f), 180f, 0.9f);
        Prop("ceiling_fan", new Vector3(-3f, 3.4f, 0f), 0f); Prop("ceiling_fan", new Vector3(3f, 3.4f, 0f), 45f);
        Prop("wall_clock", new Vector3(3.2f, 2.5f, 4.97f), 180f);
        Prop("wooden_crate_02", new Vector3(-0.8f, 0, 4.2f), 90f); Prop("cardboard_box_01", new Vector3(-0.8f, 0.46f, 4.2f), 20f);
        Prop("cardboard_box_01", new Vector3(0.6f, 0, 4.35f), 70f); Prop("cardboard_box_01", new Vector3(1.2f, 0, 4.3f), 10f, 0.9f);
        Prop("barrel_03", new Vector3(-4.2f, 0, 4.3f), 0f); Prop("brass_pot_01", new Vector3(-3.3f, 0, 4.3f), 0f, 1.4f); Prop("metal_jug", new Vector3(-2.9f, 0, 4.1f), 40f, 1.3f);
        Prop("plastic_jerrycan", new Vector3(-4.9f, 0, 4.4f), 120f);
        Prop("plastic_monobloc_chair_01", new Vector3(3.2f, 0, 1.6f), 250f); Prop("plastic_monobloc_chair_01", new Vector3(0.9f, 0, 0.2f), 60f);
        Prop("pot_enamel_01", new Vector3(2.4f, 0, 0.7f), 0f, 1.4f); Prop("brass_pot_02", new Vector3(1.6f, 0, 1.3f), 0f, 1.1f);
        BuildSleepers();
        for (int i = 0; i < 4; i++) B("StackedBench", new Vector3(-6.4f, 0.25f + i * 0.45f, 3.5f), new Vector3(0.6f, 0.4f, 2f), new Color(0.35f, 0.22f, 0.12f), i == 0);
        if (Prop("vintage_oil_lamp", new Vector3(2f, 0f, 1f), 0f, 0.6f) == null) Glow("Lantern", new Vector3(2f, 0.3f, 1f), new Vector3(0.14f, 0.2f, 0.14f), new Color(1f, 0.7f, 0.35f), 4f);
        else Glow("LanternFlame", new Vector3(2f, 0.3f, 1f), new Vector3(0.03f, 0.06f, 0.03f), new Color(1f, 0.7f, 0.35f), 8f);
        PL(new Vector3(2f, 1f, 1f), new Color(1f, 0.7f, 0.4f), 2f, 7f);
        PL(new Vector3(-3f, 3f, -2f), new Color(0.6f, 0.7f, 1f), 0.9f, 9f);
        B("StairFrame", new Vector3(6.2f, 1.15f, 4.95f), new Vector3(1.3f, 2.3f, 0.1f), new Color(0.3f, 0.2f, 0.12f));
        GameKit.Text(transform, "UPSTAIRS", new Vector3(6.2f, 2.5f, 4.88f), Quaternion.identity, 0.1f, new Color(0.9f, 0.85f, 0.7f));
        stairs = new GameObject("Stairs").transform; stairs.SetParent(transform, false); stairs.position = new Vector3(6.2f, 1.1f, 4.8f);
        resident = Npc(new Vector3(4.6f, 0f, 3.4f), 200f, NpcStyle.CrowdWoman(1300), "Resident");
        BuildStairLight();

        // ---- upstairs: Priya's room
        Room("Upstairs", U, new Vector3(6f, 3f, 5f), new Color(0.72f, 0.7f, 0.62f), new Color(0.45f, 0.4f, 0.35f), "wood_floor", "facade");
        B("Mattress", U + new Vector3(-1.2f, 0.08f, 1f), new Vector3(1.0f, 0.16f, 2f), new Color(0.75f, 0.8f, 0.9f), true, 0.05f);
        for (int i = 0; i < 4; i++) B("LegalFiles", U + new Vector3(0.75f + (i % 2) * 0.45f, 0.08f + (i / 2) * 0.16f, 1.75f), new Vector3(0.4f, 0.15f, 0.32f), new Color(0.75f, 0.68f, 0.5f), false, 0.1f);
        B("Biscuits", U + new Vector3(-0.4f, 0.04f, 0.2f), new Vector3(0.18f, 0.05f, 0.08f), new Color(0.85f, 0.15f, 0.12f), false, 0.3f);
        if (Prop("vintage_oil_lamp", U + new Vector3(-0.5f, 0f, 1.3f), 0f, 0.5f) == null) Glow("OilLamp", U + new Vector3(-0.5f, 0.1f, 1.3f), new Vector3(0.1f, 0.12f, 0.1f), new Color(1f, 0.6f, 0.2f), 6f);
        else Glow("OilLampFlame", U + new Vector3(-0.5f, 0.26f, 1.3f), new Vector3(0.025f, 0.05f, 0.025f), new Color(1f, 0.6f, 0.2f), 9f);
        Prop("wooden_crate_02", U + new Vector3(1.9f, 0, 0.4f), 0f); Prop("metal_jug", U + new Vector3(1.9f, 0.46f, 0.2f), 30f); Prop("pot_enamel_01", U + new Vector3(1.85f, 0.46f, 0.7f), 0f);
        Prop("SchoolChair_01", U + new Vector3(0.9f, 0, 0.5f), 200f);
        Prop("cardboard_box_01", U + new Vector3(2.3f, 0, 1.9f), 15f); Prop("cardboard_box_01", U + new Vector3(2.3f, 0.34f, 1.9f), 50f, 0.9f);
        Tex(B("WindowFrame", U + new Vector3(1.5f, 1.7f, 2.46f), new Vector3(1.4f, 1.2f, 0.05f), new Color(0.3f, 0.2f, 0.12f), false), "wood", 0.9f, new Color(0.3f, 0.2f, 0.12f));
        PL(U + new Vector3(-0.5f, 0.6f, 1.2f), new Color(1f, 0.62f, 0.3f), 2.2f, 6f).gameObject.AddComponent<FlickerLight>();
        Glow("RiverWindow", U + new Vector3(1.5f, 1.7f, 2.47f), new Vector3(1.2f, 1f, 0.04f), new Color(0.2f, 0.28f, 0.5f), 1.2f);
        priya = Npc(U + new Vector3(-0.15f, 0f, 1.05f), 160f, Cast.Priya(), "Priya Kapoor");
        BuildPriyasDesk();
    }

    void BuildGallery()
    {
        // the hall itself (shelves, bookcase, chandeliers, candles, moonlight): the same room as the interludes.
        // The bookcase has a gap tonight: Shadows of Chandni is out on the table.
        gallery = CuratorGallery.Build(transform, G, false, false);
        gallery.SetLights(0f);
        // three caskets: bronze, bone, iron (Act5Story.Gallery), and the thing this player's Act IV left them with
        BuildCaskets();
        BuildRouteExhibit();
        GameKit.Sphere(transform, "PriyaFigurine", G + new Vector3(2.7f, 1.05f, 1.8f), 0.12f, GameKit.Lit(new Color(0.75f, 0.55f, 0.4f), 0.4f));
        // the book the Curator writes the ending in: Shadows of Chandni, taken down from the bookcase
        var sb = StoryBookModel.Make(transform, "");
        sb.root.localPosition = G + new Vector3(-2.7f, 0.945f + StoryBookModel.T / 2f, 2.1f);
        sb.root.localRotation = Quaternion.LookRotation(Vector3.right, Vector3.forward);    // lying flat, cover up, readable from the hall
        book = sb.root;
        bookLabel = GameKit.Solid(GameKit.Text(transform, "", G + new Vector3(-2.7f, 0.945f + StoryBookModel.T + 0.012f, 1.85f), Quaternion.Euler(90f, 0, 0), 0.03f, new Color(0.95f, 0.8f, 0.4f)));
        curator = Npc(G + new Vector3(0f, 0f, 3.5f), 180f, Cast.Curator(), "The Curator", false);
        // the same performance layers as in his interludes: planted feet, visemes on his lines, blinks, an eyeline
        // on Arjun (he is talking to him, so looking into the player's eyes is meant here)
        curator.EnableHero();
        curator.Eyeline(cam.position);
        gallery.exposure.subject = () => curator != null ? curator.EyePoint() : transform.position + G;
        gallerySound = CuratorSound.Make(transform, G + new Vector3(11.9f, 5.3f, 1.7f), G + new Vector3(0f, 3f, 2f));
        gallerySound.voice = curator.VoiceSource;
        gallerySound.rainLevel = 0f;                 // the shelter comes first; the rain fades in with the Gallery
        gallery.storm.Thunder += (km, st) => { if (inGallery) gallerySound.Thunder(km, st); };
    }

    void BuildCourtroom()
    {
        var panel = new Color(0.42f, 0.26f, 0.15f);
        Room("Court", C, new Vector3(16f, 6f, 20f), panel, new Color(0.35f, 0.25f, 0.18f), "wood_floor", "wood");
        B("JudgePlatform", C + new Vector3(0, 0.4f, 8.5f), new Vector3(7f, 0.8f, 2.6f), panel * 0.8f, true, 0.4f);
        B("JudgeDesk", C + new Vector3(0, 1.4f, 7.5f), new Vector3(5f, 1.2f, 0.6f), panel * 0.7f, true, 0.5f);
        GameKit.Text(transform, "SATYAMEVA JAYATE", new Vector3(C.x, 4.8f, C.z + 9.9f), Quaternion.identity, 0.3f, new Color(0.95f, 0.8f, 0.4f));
        // a cast brass seal on a wooden roundel instead of a gold ball
        var brassM = GameKit.Lit(new Color(0.83f, 0.62f, 0.22f), 0.8f, 1f);
        var seal = VehicleKit.Part(transform, "Emblem", VehicleKit.Lathe(new[] { new Vector2(0f, 0.62f), new Vector2(0.04f, 0.66f), new Vector2(0.07f, 0.6f), new Vector2(0.07f, 0.5f), new Vector2(0.1f, 0.46f), new Vector2(0.12f, 0.2f), new Vector2(0.16f, 0.0f) }, 32), brassM, C + new Vector3(0, 3.6f, 9.96f));
        seal.transform.localRotation = Quaternion.Euler(0, 90f, 0);                 // lathe axis X -> out of the wall (-Z)
        Tex(B("EmblemBoard", C + new Vector3(0, 3.6f, 9.97f), new Vector3(1.7f, 1.7f, 0.04f), panel * 0.6f, false), "wood", 0.9f, panel * 0.6f);
        Prop("ArmChair_01", C + new Vector3(0f, 0.8f, 9.25f), 180f, 1.25f);
        Prop("wall_clock", C + new Vector3(-7.97f, 4.2f, 6f), 90f, 1.6f);
        Prop("ceiling_fan", C + new Vector3(-3.5f, 6f, 2f), 0f, 1.3f); Prop("ceiling_fan", C + new Vector3(3.5f, 6f, 2f), 30f, 1.3f);
        Prop("ceiling_fan", C + new Vector3(-3.5f, 6f, -5f), 60f, 1.3f); Prop("ceiling_fan", C + new Vector3(3.5f, 6f, -5f), 10f, 1.3f);
        // counsel tables and the bar that separates the court from the public
        foreach (float tx in new[] { -2.6f, 2.6f })
        {
            if (Prop("WoodenTable_01", C + new Vector3(tx, 0, 5.0f), 0f, 1.36f) == null) Tex(B("CounselTable", C + new Vector3(tx, 0.72f, 5.0f), new Vector3(2.2f, 0.06f, 0.9f), panel, true), "wood", 0.9f, panel);
            Prop("painted_wooden_chair_01", C + new Vector3(tx - 0.5f, 0, 4.1f), 0f);
            if (tx > 0) Prop("painted_wooden_chair_01", C + new Vector3(tx + 0.5f, 0, 4.1f), 0f);       // the lawyer stands on the other side
            Tex(B("BarRail", C + new Vector3(tx * 1.6f, 0.95f, 0.2f), new Vector3(6.2f, 0.08f, 0.1f), panel * 0.8f, true), "wood", 0.9f, panel * 0.8f);
            for (int b = 0; b < 9; b++) B("Baluster", C + new Vector3(tx * 1.6f - 2.9f + b * 0.72f, 0.48f, 0.2f), new Vector3(0.06f, 0.9f, 0.06f), panel * 0.7f, false, 0.4f);
        }
        Prop("brass_pot_01", C + new Vector3(2.2f, 0.75f, 5.0f), 0f, 0.7f); Prop("jug_01", C + new Vector3(-2.3f, 0.75f, 5.1f), 40f);
        for (int i = 0; i < 4; i++) Prop("hanging_picture_frame_01", C + new Vector3(-7.96f, 3.2f, -7f + i * 3.4f), 90f, 1.5f);
        B("WitnessBox", C + new Vector3(4.5f, 0.55f, 5.5f), new Vector3(1.4f, 1.1f, 1.4f), panel * 0.9f, true, 0.4f);
        for (int r = 0; r < 4; r++)
            foreach (float x in new[] { -4f, 4f })
            {
                // a proper public bench (seat, back, end panels) instead of a solid block; the block stays as the collider
                Vector3 bp = C + new Vector3(x, 0, -1f - r * 2f);
                Hide(B("Bench", bp + new Vector3(0, 0.45f, 0), new Vector3(5f, 0.9f, 0.5f), panel * 0.9f, true, 0.4f));
                Tex(B("BenchSeat", bp + new Vector3(0, 0.45f, 0.03f), new Vector3(5f, 0.06f, 0.44f), panel, false), "wood", 0.9f, panel);
                Tex(B("BenchBack", bp + new Vector3(0, 0.78f, -0.21f), new Vector3(5f, 0.5f, 0.05f), panel, false), "wood", 0.9f, panel);
                Tex(B("BenchRail", bp + new Vector3(0, 1.05f, -0.21f), new Vector3(5.06f, 0.06f, 0.09f), panel * 0.8f, false), "wood", 0.9f, panel * 0.8f);
                foreach (float ex in new[] { -2.47f, -0.82f, 0.82f, 2.47f })
                    Tex(B("BenchEnd", bp + new Vector3(ex, 0.4f, -0.02f), new Vector3(0.06f, 0.8f, 0.46f), panel * 0.85f, false), "wood", 0.9f, panel * 0.85f);
            }
        for (int i = 0; i < 4; i++)
        {
            Glow("CourtLamp", C + new Vector3(-6f + i * 4f, 5.8f, 0), new Vector3(1.4f, 0.06f, 0.2f), new Color(1f, 0.95f, 0.85f), 2f);
            PL(C + new Vector3(-6f + i * 4f, 5f, 0), new Color(1f, 0.92f, 0.8f), 2.2f, 10f);
        }
        for (int i = 0; i < 3; i++) Glow("HighWindow", C + new Vector3(7.97f, 4f, -6f + i * 6f), new Vector3(0.04f, 1.6f, 1.2f), new Color(1f, 0.85f, 0.6f), 1.5f);
        // the endings play at the bench and the counsel tables, beyond the reach of the hall's four lamps: two more over
        // the well and a soft light on the bench, morning light through the high windows on the advocates' side
        for (int i = 0; i < 2; i++)
        {
            Glow("CourtLamp", C + new Vector3(-2.5f + i * 5f, 5.8f, 5.2f), new Vector3(1.4f, 0.06f, 0.2f), new Color(1f, 0.95f, 0.85f), 2f);
            PL(C + new Vector3(-2.5f + i * 5f, 4.6f, 5.2f), new Color(1f, 0.93f, 0.82f), 2.4f, 8f);
        }
        var benchLight = new GameObject("BenchLight").AddComponent<Light>(); benchLight.transform.SetParent(transform, false);
        benchLight.transform.position = C + new Vector3(0f, 4.8f, 4.6f); benchLight.transform.LookAt(C + new Vector3(0f, 2.2f, 8.6f));
        benchLight.type = LightType.Spot; benchLight.spotAngle = 70f; benchLight.innerSpotAngle = 30f; benchLight.range = 9f; benchLight.intensity = 3f; benchLight.color = new Color(1f, 0.92f, 0.8f);
        CinematicLook.Shadows(benchLight, 0.7f, 0.03f, 0.5f);
        var sunIn = new GameObject("CourtMorning").AddComponent<Light>(); sunIn.transform.SetParent(transform, false);
        sunIn.transform.position = C + new Vector3(7.6f, 4.2f, 3f); sunIn.transform.LookAt(C + new Vector3(0f, 0.8f, 4.5f));
        sunIn.type = LightType.Spot; sunIn.spotAngle = 55f; sunIn.range = 14f; sunIn.intensity = 4f; sunIn.color = new Color(1f, 0.86f, 0.62f);
        CinematicLook.Shadows(sunIn, 0.8f, 0.03f, 0.5f);
        judge = Npc(C + new Vector3(0f, 0.8f, 8.6f), 180f, Cast.Judge(), "Judge");
        lawyer = Npc(C + new Vector3(-2f, 0f, 4f), 0f, Cast.Lawyer(), "Lawyer");
        deshpande = Npc(C + new Vector3(-3f, 0f, 1.2f), 10f, Cast.Deshpande(), "Councilor Deshpande", false);
        for (int i = 0; i < 10; i++)
            Npc(C + new Vector3((i % 2 == 0 ? -1 : 1) * (2.5f + (i % 3)), 0f, -1.6f - (i / 2) * 2f), 0f, i % 3 == 0 ? NpcStyle.CrowdWoman(1400 + i) : NpcStyle.Crowd(1400 + i), "Spectator" + i, false);
    }

    void BuildTerrace()
    {
        GameKit.Surface(B("Rooftop", T + new Vector3(0, -0.05f, 0), new Vector3(12f, 0.1f, 10f), new Color(0.5f, 0.47f, 0.44f), true, 0.3f), "stone", 1.2f);
        foreach (var w in new[] { new Vector4(0, 5, 12, 0.3f), new Vector4(0, -5, 12, 0.3f), new Vector4(6, 0, 0.3f, 10), new Vector4(-6, 0, 0.3f, 10) })
            B("Parapet", T + new Vector3(w.x, 0.5f, w.y), new Vector3(w.z, 1f, w.w), new Color(0.75f, 0.6f, 0.5f), true, 0.1f);
        // the black ribbed plastic water tank on every Indian roof, on its brick stand
        Tex(B("TankStand", T + new Vector3(-4f, 0.25f, -3f), new Vector3(1.5f, 0.5f, 1.5f), Color.white, true), "brick", 1.4f, new Color(0.8f, 0.6f, 0.5f));
        Turned("WaterTank", T + new Vector3(-4f, 0.5f, -3f), new[]
        {
            new Vector2(0f, 0.6f), new Vector2(0.12f, 0.64f), new Vector2(0.24f, 0.6f), new Vector2(0.36f, 0.64f), new Vector2(0.48f, 0.6f), new Vector2(0.6f, 0.64f),
            new Vector2(0.72f, 0.6f), new Vector2(0.84f, 0.64f), new Vector2(0.96f, 0.6f), new Vector2(1.1f, 0.5f), new Vector2(1.2f, 0.26f), new Vector2(1.28f, 0.24f), new Vector2(1.3f, 0.0f),
        }, GameKit.Lit(new Color(0.03f, 0.03f, 0.035f), 0.5f), 24);
        Prop("plastic_monobloc_chair_01", T + new Vector3(1.6f, 0, 1.2f), 200f); Prop("plastic_monobloc_chair_01", T + new Vector3(2.7f, 0, 1.5f), 150f);
        Prop("potted_plant_02", T + new Vector3(-5.2f, 0, 4.2f), 0f); Prop("potted_plant_02", T + new Vector3(5.1f, 0, 4.1f), 120f, 1.1f);
        Prop("planter_box_01", T + new Vector3(3.6f, 0, 4.5f), 0f); Prop("barrel_03", T + new Vector3(5.2f, 0, -4.2f), 0f); Prop("plastic_jerrycan", T + new Vector3(-2.6f, 0, -3.6f), 60f);
        foreach (float px in new[] { 0.05f, 3.95f }) Cy("LinePole", T + new Vector3(px, 0.95f, -3f), 0.025f, 1.9f, new Color(0.35f, 0.3f, 0.25f), 0.4f, 0.5f);
        B("ClothesLine", T + new Vector3(2f, 1.8f, -3f), new Vector3(4f, 0.02f, 0.02f), new Color(0.2f, 0.2f, 0.2f));
        for (int i = 0; i < 4; i++) B("Washing", T + new Vector3(0.6f + i * 0.9f, 1.4f, -3f), new Vector3(0.5f, 0.7f, 0.02f), Color.HSVToRGB(i * 0.2f, 0.5f, 0.85f), false, 0.1f);
        var rnd = new System.Random(31);
        for (int i = 0; i < 24; i++)
        {
            float x = -60f + i * 5.2f, h = 4f + (float)rnd.NextDouble() * 18f;
            B("CityBlock", T + new Vector3(x, -10f + h / 2f, 45f + (float)rnd.NextDouble() * 10f), new Vector3(4.5f, h, 4f), new Color(0.2f, 0.16f, 0.15f), false, 0.1f);
        }
        GameKit.Cone(transform, "TempleSpire", T + new Vector3(-8f, 2f, 42f), 3f, 9f, GameKit.Lit(new Color(0.28f, 0.18f, 0.14f), 0.1f), 8);
    }

    void SetTerraceSky(Color bottom, Color top, Color sunColor, float sunHeight)
    {
        if (skyQuad != null) Destroy(skyQuad);
        if (sun != null) Destroy(sun);
        // behind the city's blocks (z 45-55 from the roof) and short of the Gallery (z 66-90 in world space): at T + 80 this
        // 220 m backdrop stood inside the Gallery, and the Curator's last lines played in front of it as a blank wall
        skyQuad = SkyQuad(T + new Vector3(0, 15f, 62f), Vector3.zero, new Vector2(220f, 70f), bottom, top);
        sun = GameKit.Sphere(transform, "Sun", T + new Vector3(10f, sunHeight, 78f), 9f, GameKit.Unlit(sunColor));
    }

    // ===================================================================== AUDIO
    AudioClip Ticking()
    {
        const int SR = 44100; int n = SR * 2; var d = new float[n];
        for (int k = 0; k < 2; k++)
            for (int i = 0; i < 1500; i++)
            {
                int idx = k * SR + i; float t = i / (float)SR;
                d[idx] = Mathf.Sin(2f * Mathf.PI * (k == 0 ? 2200f : 1800f) * t) * Mathf.Exp(-t * 300f) * 0.6f;
            }
        var c = AudioClip.Create("tick", n, 1, SR, false); c.SetData(d, 0); return c;
    }

    AudioClip Crowd(float len, bool chant)
    {
        const int SR = 44100; int n = (int)(SR * len); var d = new float[n];
        var r = new System.Random(5); float a = 0, b = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR, w = (float)(r.NextDouble() * 2 - 1);
            a += (w - a) * 0.12f; b += (a - b) * 0.12f;
            float env = chant ? (0.4f + 0.6f * Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 1.2f * t))) : Mathf.Clamp01(t * 3f) * Mathf.Clamp01((len - t) * 0.8f);
            d[i] = (a - b) * 6f * env;
        }
        Norm(d, 0.5f);
        var c = AudioClip.Create("crowd", n, 1, SR, false); c.SetData(d, 0); return c;
    }

    // ====================================================================== FLOW
    protected override IEnumerator Run()
    {
        bool residentSpoke = false, climbed = false, met = false;
        AddSpot(stairs, Vector3.zero, "Climb the stairs", () => phase == Phase.Free && !climbed, () => climbed = true);
        AddSpot(priya.transform, Vector3.up * 1.5f, "Talk to Priya", () => phase == Phase.Free && climbed && !met, () => met = true);
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            string[] n = { "Examine the bronze casket", "Examine the bone casket", "Examine the iron casket" };
            AddSpot(caskets[i], Vector3.up * 0.3f, n[i], () => phase == Phase.Free && chosen < 0, () => StartCoroutine(ExamineCasket(idx)), 3.5f, 0.45f);
        }

        // ---- the shelter (skipped when resuming at the Gallery checkpoint)
        bool atGallery = resume && SaveSystem.Data.act5Stage >= 1;
        standEye = cam.localPosition.y;
        yield return OpenAct();
        if (!atGallery)
        {
        Teleport(new Vector3(-6f, 0.1f, -4f), 50f);
        yield return Narrate("a5_vo_intro");
        BeginFree("Find Priya - she is upstairs", stairs.position + Vector3.up * 0.5f, "Stairs");
        bool voiceHeard = false;
        while (!climbed)
        {
            if (!residentSpoke && Near(resident.transform.position, 3f))
            {
                residentSpoke = true;
                StartCoroutine(SpeakNear(resident, "a5_resident"));
            }
            // at the foot of the stairs: the lamp's pool on the steps, and her voice from above
            if (!voiceHeard && Near(stairs.position, 5.5f) && (!residentSpoke || Time.time > subUntil)) { voiceHeard = true; PriyaUpstairsVoice(); }
            yield return null;
        }
        BeginBusy();
        sfx.PlayOneShot(stepClip, 0.6f);
        yield return FadeOut(2f);
        // up the stairs: her voice from below stops, and its subtitle with it
        if (stairVoice != null && stairVoice.GetComponent<AudioSource>() != null) stairVoice.GetComponent<AudioSource>().Stop();
        sub = null;
        Teleport(U + new Vector3(1.6f, 0.1f, -1.6f), -40f);
        yield return FadeIn(1.2f);
        BeginFree("Talk to Priya", priya.transform.position + Vector3.up * 1.9f, "Priya");
        // she hears him before he speaks: within a couple of steps she looks up, shuts the file and holds it to her
        yield return new WaitUntil(() => met || Near(priya.transform.position, 2.2f));
        BeginBusy();
        StartCoroutine(Turn(priya.EyePoint(), 0.9f));
        yield return PriyaGuardsTheFile();
        yield return Turn(priya.EyePoint(), 0.4f);
        yield return Talk("a5_priya_1", priya);
        yield return Talk("a5_arjun_mother");
        yield return Talk("a5_priya_2", priya);
        yield return Talk("a5_priya_3", priya);
        yield return Talk("a5_priya_4", priya);
        EndTalk();
        skipAll = false;
        // checkpoint: Priya has asked her question; Continue from here goes straight to the Gallery
        SaveSystem.Data.act5Stage = 1; SaveSystem.Save();
        }

        // ---- the world stops: the room sinks into the oil lamp's flame, and goes out with it
        voice.Stop();
        if (!atGallery) yield return WorldStops();
        ambience.pitch = 1f;
        fade = 1f; fadeTarget = 1f; white = 0f;
        Show(false, true);
        Teleport(G + new Vector3(0, 0.1f, -3.2f), 0f, -4f);
        ambience.clip = galleryBed; ambience.volume = 0.5f; ambience.Play();
        inGallery = true; galleryLevel = 0f;
        CuratorGallery.Mood(camComp, 0f);
        yield return new WaitForSeconds(1.5f);
        sfx.PlayOneShot(Tone(3000f, 0.15f, 25f, 0.3f), 0.6f);                   // a match strikes
        fadeSpeed = 0.35f; fadeTarget = 0f;
        yield return LightGallery(2.4f);            // the candle nearest the middle of the view first: where the lamp's flame was
        yield return new WaitForSeconds(0.8f);
        yield return Turn(curator.EyePoint(), 1f);
        yield return Talk("gal_1", curator);
        yield return Talk("gal_2", curator);
        yield return Talk("gal_3", curator);
        yield return Talk("gal_4", curator);
        yield return Talk("gal_5", curator);
        EndTalk();
        BeginFree("Read the inscriptions and choose a casket", G + new Vector3(0, 1.6f, 2f), "The three caskets");
        yield return new WaitUntil(() => chosen >= 0);

        // ---- the choice
        BeginBusy();
        yield return Talk("gal_chosen", curator);
        EndTalk();
        StartCoroutine(OpenCasket(chosen));
        for (int i = 0; i < 3; i++) if (i != chosen) StartCoroutine(Crumble(i));
        yield return new WaitForSeconds(2.2f);
        for (float t = 0; t < 1.5f; t += Time.deltaTime) { white = t / 1.5f; yield return null; }
        EnsureEndingSets();                         // built behind the white, not when the act opened
        // Arjun's answer to Priya's question, in his recorded voice, over the white as the Gallery lets go
        skipAll = false;
        yield return Narrate(chosen == 0 ? "a5_arjun_justice" : chosen == 1 ? "a5_arjun_sacrifice" : "a5_arjun_revolution");
        // the ending is recorded when its credits roll (RollCredits), so quitting half-way through it does not count it

        inGallery = false;
        if (chosen == 0) yield return JusticeEnding();
        else if (chosen == 1) yield return SacrificeEnding();
        else yield return RevolutionEnding();
    }

    /// <summary>The shelter holds still: its lights sink to the oil lamp's flame while the sound slows and drops away,
    /// the view settles on the flame, and the flame goes out. In the Gallery the first candle lights where it was.</summary>
    IEnumerator WorldStops()
    {
        var flame = Deep(transform, "OilLampFlame") ?? Deep(transform, "OilLamp");
        Vector3 fp = flame != null ? flame.position : U + new Vector3(-0.5f, 0.26f, 1.3f);
        var ls = new List<Light>(); var i0 = new List<float>();
        foreach (var l in GetComponentsInChildren<Light>())
        {
            if (l.type == LightType.Directional || Vector3.Distance(l.transform.position, U) > 7f) continue;
            var fl = l.GetComponent<FlickerLight>(); if (fl != null) fl.enabled = false;
            ls.Add(l); i0.Add(l.intensity);
        }
        Color a0 = RenderSettings.ambientSkyColor, a1 = RenderSettings.ambientEquatorColor, a2 = RenderSettings.ambientGroundColor;
        float v0 = ambience.volume;
        StartCoroutine(Turn(fp, 2.4f));
        for (float t = 0f; t < 2.6f; t += Time.deltaTime)
        {
            float k = t / 2.6f, dim = (1f - k) * (1f - k);
            for (int i = 0; i < ls.Count; i++) if (ls[i] != null) ls[i].intensity = i0[i] * dim;
            RenderSettings.ambientSkyColor = a0 * dim; RenderSettings.ambientEquatorColor = a1 * dim; RenderSettings.ambientGroundColor = a2 * dim;
            ambience.pitch = Mathf.Lerp(1f, 0.3f, k); ambience.volume = Mathf.Lerp(v0, 0.08f, k);
            yield return null;
        }
        yield return Wait(0.6f);                      // the flame alone in the dark
        if (flame != null) flame.gameObject.SetActive(false);
        fade = 1f; fadeTarget = 1f;
        ambience.volume = v0;
        for (int i = 0; i < ls.Count; i++) if (ls[i] != null) ls[i].intensity = i0[i];
        RenderSettings.ambientSkyColor = a0; RenderSettings.ambientEquatorColor = a1; RenderSettings.ambientGroundColor = a2;
        if (flame != null) flame.gameObject.SetActive(true);
    }

    IEnumerator SpeakNear(CharacterNPC n, string id)
    {
        var clip = VO(id);
        if (clip != null) n.Say(clip);
        Toast(DialogueDB.Speaker(id) + ":  " + DialogueDB.Text(id), clip != null ? clip.length + 1f : 4f);
        yield break;
    }

    /// <summary>The Gallery's candles and chandeliers light one after another, and the hall comes up out of the dark.</summary>
    IEnumerator LightGallery(float dur)
    {
        var flames = new List<GameObject>(gallery.flames); var lights = gallery.lights;
        flames.Sort((a, b) => Vector3.Angle(cam.forward, a.transform.position - cam.position).CompareTo(Vector3.Angle(cam.forward, b.transform.position - cam.position)));
        foreach (var f in flames) f.SetActive(false);
        int fi = 0;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float k = t / dur;
            for (int i = 0; i < lights.Count; i++) lights[i].level = Mathf.Clamp01(k * lights.Count - i * 0.8f);
            while (fi < Mathf.RoundToInt(k * flames.Count)) flames[fi++].SetActive(true);
            galleryLevel = k;
            yield return null;
        }
        gallery.SetLights(1f);
        galleryLevel = 1f;
    }

    // The Act I room writes its ambient light every frame; inside the Gallery the Gallery's own light wins.
    void LateUpdate()
    {
        if (inGallery) CuratorGallery.Mood(null, galleryLevel);
        if (gallery != null) gallery.storm.autoStrikes = inGallery;
        if (gallerySound != null) gallerySound.rainLevel = Mathf.MoveTowards(gallerySound.rainLevel, inGallery ? 1f : 0f, Time.deltaTime * 0.8f);
        if (inGallery && curator != null && cam != null) curator.Eyeline(cam.position);
    }

    bool Sealed(int i)
    {
        var d = SaveSystem.Data;
        if (i == 0) return SaveSystem.EvidenceCount < EvidenceForJustice;
        if (i == 2) return !(d.hasRecordings || d.hasEmails);
        return false;
    }

    IEnumerator Crumble(int i)
    {
        var tr = caskets[i];
        var ps = new GameObject("Dust").AddComponent<ParticleSystem>();
        ps.transform.SetParent(transform, false);
        ps.transform.position = tr.position + Vector3.up * 0.25f;
        var main = ps.main; main.startLifetime = 1.5f; main.startSpeed = 0.6f; main.startSize = 0.06f;
        main.startColor = new Color(0.7f, 0.65f, 0.55f, 0.8f); main.gravityModifier = 0.3f; main.loop = false; main.maxParticles = 300;
        var em = ps.emission; em.rateOverTime = 0; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 220) });
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(0.8f, 0.4f, 0.5f);
        var sd = Shader.Find("Sprites/Default");
        if (sd != null) ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = new Material(sd);
        ps.Play();
        Vector3 s0 = tr.localScale;
        for (float t = 0; t < 1.2f; t += Time.deltaTime)
        {
            tr.localScale = s0 * (1f - t / 1.2f);
            if (casketLights[i] != null) casketLights[i].intensity = Mathf.Lerp(1.2f, 0f, t / 1.2f);
            yield return null;
        }
        tr.gameObject.SetActive(false);
    }

    // =================================================================== ENDINGS
    IEnumerator ToCourtroom(Vector3 local, float yaw)
    {
        ambience.clip = courtBed; ambience.volume = 0.6f; ambience.Play();
        RenderSettings.fogColor = new Color(0.12f, 0.1f, 0.08f);
        CinematicLook.NoSky(camComp, new Color(0.02f, 0.02f, 0.05f));
        CinematicLook.Ambient(new Color(0.6f, 0.55f, 0.5f), new Color(0.45f, 0.38f, 0.32f), new Color(0.15f, 0.12f, 0.1f));
        Teleport(C + local, yaw);
        yield return null;
    }

    IEnumerator ToTerrace(Color bottom, Color top, Color sunColor, float sunHeight, float yaw)
    {
        SetTerraceSky(bottom, top, sunColor, sunHeight);
        // a real photographed sunrise replaces the painted backdrop (the painted one stays as the fallback)
        if (CinematicLook.Sky(camComp, "the_sky_is_on_fire", 1.0f, TerraceSkyRotation, 1f))
        {
            if (skyQuad != null) Destroy(skyQuad);
            if (sun != null) Destroy(sun);
        }
        RenderSettings.fogColor = Color.Lerp(bottom, top, 0.4f);
        CinematicLook.Ambient(Color.Lerp(top, Color.white, 0.4f), Color.Lerp(bottom, Color.white, 0.3f), new Color(0.2f, 0.15f, 0.12f));
        SetSun(sunColor, 1.6f, new Vector3(8f, 180f, 0f));
        Teleport(T + new Vector3(0, 0.1f, -1.5f), yaw);
        yield return null;
    }

    IEnumerator Gavel()
    {
        for (int i = 0; i < 3; i++) { sfx.PlayOneShot(gavelClip, 0.9f); yield return new WaitForSeconds(0.45f); }
    }

    IEnumerator WhiteIn()
    {
        for (float t = 0; t < 1.2f; t += Time.deltaTime) { white = 1f - t / 1.2f; yield return null; }
        white = 0f;
    }

    /// <summary>The Curator's verdict on the ending (Curator script 05-07). The last Gallery scene: he has no candle.</summary>
    IEnumerator BackToGallery(string bookTitle, string[] curatorLines, bool darken)
    {
        yield return FadeOut(0.8f);
        // leave the roof's sky behind: its backdrop and sun, and the photographed sky on the camera
        if (skyQuad != null) Destroy(skyQuad);
        if (sun != null) Destroy(sun);
        CinematicLook.NoSky(camComp, new Color(0.02f, 0.02f, 0.05f));
        ambience.clip = galleryBed; ambience.volume = 0.5f; ambience.Play();
        inGallery = true; galleryLevel = 1f;
        gallery.SetLights(1f);
        CuratorGallery.Mood(camComp, 1f);
        SetSun(new Color(0.55f, 0.62f, 0.95f), 0.1f, new Vector3(45f, 30f, 0f));
        Show(false, true);
        Teleport(G + new Vector3(-1.2f, 0.1f, -0.6f), 10f, 5f);
        var ce = cam.localPosition; ce.y = standEye > 0f ? standEye : 1.62f; cam.localPosition = ce;
        bookLabel.text = bookTitle;
        yield return FadeIn(0.8f);
        yield return Turn(curator.EyePoint(), 0.8f);
        for (int i = 0; i < curatorLines.Length; i++)
        {
            if (i == curatorLines.Length - 1) StartCoroutine(Turn(book.position, 1.6f));     // "The book is closed."
            yield return Talk(curatorLines[i], curator);
        }
        EndTalk();
        // "The candle is out": the hall goes dark (slowly, for the quiet ending)
        float dur = darken ? 2.5f : 1.4f;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float k = 1f - t / dur;
            gallery.SetLights(k); galleryLevel = k;
            yield return null;
        }
        gallery.SetLights(0f); galleryLevel = 0f;
        yield return new WaitForSeconds(1.5f);
    }

    void RollCredits(string ending, bool silent)
    {
        var d = SaveSystem.Data;
        SaveSystem.RecordEnding(chosen);
        SaveSystem.CompleteAct(5);
        if (silent) ambience.Stop();
        creditLines = new[]
        {
            "#SHADOWS OF CHANDNI", "A non-violent mystery thriller", "", "",
            "Ending reached:  " + ending, "Endings discovered:  " + SaveSystem.Profile.endingsSeen.Count + " of 3", "Evidence gathered:  " + SaveSystem.EvidenceCount, "", "",
            "#THANK YOU FOR PLAYING", "Detective " + SaveSystem.Profile.playerName, "", "",
            "#CAST", "Arjun Mehra  -  the detective", "Priya Kapoor  -  the advocate", "Kamala Kapoor  -  her mother", "Rohan Kapoor  -  her brother",
            "Vikram Sahu  -  the chai seller", "Bhaskar  -  the auto driver", "Inspector Sonal Reddy  -  Crime Branch", "Raj Malhotra  -  the builder",
            "Harishankar  -  who keeps the temple's accounts", "Councilor Meghna Deshpande", "and The Curator", "", "",
            "#MADE WITH", "Unity 6  -  Universal Render Pipeline", "Character models: Microsoft Rocketbox (MIT), Quaternius (CC0)", "Voices: macOS speech synthesis",
            "Storm and Gallery sounds: Wikimedia Commons recordings by", "CutSomeSlackenstein and Work With Sounds / Torsten Nilsson (CC BY 4.0),", "stephan, cori, ezwa and planish (public domain)", "", "",
            "#Some secrets are worth uncovering.",
        };
        creditsStart = Time.time;
        phase = Phase.Ending;
    }

    // ======================================================================== UI
    protected override void OnGUI()
    {
        base.OnGUI();
        if ((!string.IsNullOrEmpty(stampA) || phoneAt > 0f) && creditsStart <= 0f && !GameState.Paused)
        {
            float sc = Mathf.Max(0.7f, Screen.height / 900f);
            var m = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(sc, sc, 1f));
            DrawStamp(Screen.width / sc, Screen.height / sc);
            DrawPhone(Screen.width / sc, Screen.height / sc);
            GUI.matrix = m;
        }
        if (white > 0.001f)
        {
            var old = GUI.color; GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(white));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
