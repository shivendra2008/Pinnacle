using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Shadows of Chandni - Act II "The Streets Know": the night market.
/// Bhaskar (auto driver) -> Vikram's chai stall (three-tier trust conversation, USB drive)
/// -> the suspicious woman (Inspector Reddy) -> the black SUV with a 15 second timed choice.
/// Created by PriyaRoomStory when Act I ends.
/// </summary>
public class MarketStory : MonoBehaviour
{
    public PriyaRoomBuilder builder;
    public PriyaRoomPlayer player;
    public bool resume;                 // continue the saved Act II progress
    bool resumedMid;

    enum Phase { Intro, Free, Dialogue, Hook, Choice, Outcome, Ending }
    Phase phase = Phase.Intro;

    MarketWorld world;
    Transform cam;
    Camera camComp;
    CharacterNPC bhaskar, vikram, reddy;
    readonly List<CharacterNPC> crowd = new List<CharacterNPC>();
    readonly List<Transform> vehicles = new List<Transform>();

    // audio
    AudioSource ambience, sfx, voice, suvAudio, vendorSrc, musicSrc, phone, dialogueSource;
    readonly StoryCompass compass = new StoryCompass();
    readonly StoryCompass narration = new StoryCompass();       // its subtitle half, for the voice-over
    Collider[] trafficHits = new Collider[24];
    AudioClip[] cries;
    AudioClip clickClip, dingClip, hornClip, ringClip, engineClip;

    // story state
    bool bhaskarDone, vikramDone, reddyResolved, reddyFled, hookStarted, vikramTalking;
    float vikramDoneAt, idDropAt;
    string trust = "";
    readonly List<string> evidence = new List<string>();
    GameObject idCard;
    Vector3? objectiveTarget; string objectiveTargetName = "";

    // ui state
    float fade = 1f, fadeTarget = 1f, fadeSpeed = 0.5f;
    string prompt = "", objective = "";
    string sub; float subUntil;
    string toast; float toastUntil; bool toastBig;
    string titleA, titleB; float titleStart, titleEnd;
    string dlgSpeaker, dlgText; string[] dlgOptions;
    bool paused, savedLocked, journalOpen, showEnd, skipAll;
    // timed choice
    float plateCardUntil;
    bool choiceActive; float choiceDeadline; string[] choiceOptions; int choiceResult = -1;
    int menuResult = -1;
    GUIStyle sSub, sTitle, sTitle2, sSmall, sPanel, sPrompt, sHead, sBtn;

    // ====================================================================
    bool loading = true;

    IEnumerator Start()
    {
        cam = player.cam;
        camComp = cam.GetComponent<Camera>();
        PriyaRoomPlayer.Locked = true;
        PriyaRoomPlayer.Surface = 1;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;   // the menu left the cursor free

        world = gameObject.AddComponent<MarketWorld>();
        // Show the location card while the procedural scenery and cast are built over several frames. The camera draws
        // nothing behind the card meanwhile: it used to render the half-built lane every loading frame (6 s to load).
        int cullingMask = camComp.cullingMask;
        camComp.cullingMask = 0;
        yield return null;
        yield return world.Generate(cam);
        SetupEnvironment();
        SetupAudio();
        compass.sfx = sfx;                                    // the dial ticks softly as its needle picks a bearing
        SetupPolish();

        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = world.PlayerStart;
        cc.enabled = true;
        player.SetLook(0f, 0f);

        yield return SpawnPeople();
        // The reflection probe re-rendered itself 0.6 s and 3 s after it was made: with control handed back early the
        // second render landed mid-walk as a hitch and a visible jump in the reflections. Render it once, now that the
        // scenery and the cast are in, behind the loading card.
        foreach (var lc in GetComponentsInChildren<LookController>(true))
            if (lc.probe != null) { lc.CancelInvoke(); lc.probe.RenderProbe(); }
        // two full frames still under the card, so first-use shader work happens out of sight
        camComp.cullingMask = cullingMask;
        yield return null;
        yield return null;
        loading = false;
        if (resume) ApplyResume();
        StartCoroutine(Intro());
        StartCoroutine(TrafficLoop());
        StartCoroutine(CryLoop());
        StartCoroutine(GestureLoop());
        StartCoroutine(CartLoop());
    }

    void SetupEnvironment()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.30f, 0.28f, 0.38f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.16f, 0.12f, 0.2f);
        RenderSettings.fogDensity = 0.007f;
        camComp.backgroundColor = new Color(0.10f, 0.07f, 0.18f);

        Light moon = null;
        foreach (var l in FindObjectsByType<Light>())
            if (l.type == LightType.Directional && l.gameObject.scene.IsValid() && l.enabled) { moon = l; break; }
        if (moon == null)
        {
            var go = new GameObject("Moonlight");
            go.transform.SetParent(transform, false);
            go.transform.SetParent(transform, false);
            moon = go.AddComponent<Light>();
            moon.type = LightType.Directional;
        }
        moon.color = new Color(0.62f, 0.68f, 1f);
        moon.intensity = 1.1f;
        moon.transform.rotation = Quaternion.Euler(48f, 25f, 0f);
        CinematicLook.Shadows(moon, 1f, 0.04f, 0.8f);
        MarketShadows(true);
        CinematicLook.Ambient(new Color(0.36f, 0.38f, 0.58f), new Color(0.30f, 0.28f, 0.40f), new Color(0.14f, 0.12f, 0.14f), 1f);
        CinematicLook.Probe(transform, new Vector3(0f, 4f, 40f), new Vector3(12f, 12f, 92f), new Color(0.07f, 0.08f, 0.14f), 128);
    }

    IEnumerator SpawnPeople()
    {
        bhaskar = CharacterNPC.Create(transform, world.BhaskarPos, NpcStyle.Bhaskar(), "Bhaskar");
        bhaskar.transform.rotation = Quaternion.Euler(0, -140f, 0);
        bhaskar.lookTarget = cam;

        vikram = CharacterNPC.Create(transform, world.VikramPos, NpcStyle.Vikram(), "Vikram");
        vikram.transform.rotation = Quaternion.Euler(0, 90f, 0);
        vikram.lookTarget = cam;

        // stall-keepers behind their counters, customers haggling in front
        int n = 0;
        foreach (var v in world.Vendors)
        {
            if (n % 3 == 2) { n++; continue; }
            var st = n % 4 == 1 ? NpcStyle.CrowdWoman(300 + n) : NpcStyle.Crowd(300 + n);
            AddCrowd(v.pos, v.yaw, st, "Vendor" + n, true);
            if (n % 3 == 0) yield return null;
            n++;
        }
        n = 0;
        foreach (var c in world.Customers)
        {
            var st = n % 3 == 0 ? NpcStyle.CrowdWoman(400 + n) : (n % 5 == 4 ? NpcStyle.Child(400 + n) : NpcStyle.Crowd(400 + n));
            AddCrowd(c.pos, c.yaw, st, "Customer" + n, true);
            if (n % 3 == 0) yield return null;
            n++;
        }
        // people walking up and down the lane: men, women in saris, couples, and children running about
        float[] xs = { -1.7f, -0.9f, 0.2f, 1.1f, -1.3f, 0.6f, 1.7f, -0.3f, -1.9f, 0.9f, -0.6f, 1.4f, -1.1f, 0.0f, 1.9f, -1.5f };
        float[] z0 = { 6f, 14f, 26f, 44f, 58f, 70f, 36f, 20f, 50f, 62f, 12f, 30f, 66f, 40f, 54f, 24f };
        for (int i = 0; i < xs.Length; i++)
        {
            if (i % 3 == 0) yield return null;
            bool child = i % 7 == 5;
            var st = child ? NpcStyle.Child(500 + i) : (i % 3 == 0 ? NpcStyle.CrowdWoman(500 + i) : NpcStyle.Crowd(500 + i));
            var w = AddCrowd(new Vector3(xs[i], 0, z0[i]), 0f, st, "Walker" + i, false);
            float spd = child ? 1.7f + 0.1f * (i % 3) : 0.75f + 0.07f * (i % 6);       // kids run, adults stroll
            StartCoroutine(Patrol(w, xs[i], z0[i], i % 2 == 0 ? 14f : -14f, spd));
            if (i % 4 == 1)    // a companion walking right behind: friends / couples
            {
                var buddy = AddCrowd(new Vector3(xs[i] + 0.55f, 0, z0[i] - 0.3f), 0f, i % 8 == 1 ? NpcStyle.CrowdWoman(600 + i) : NpcStyle.Crowd(600 + i), "Buddy" + i, false);
                StartCoroutine(Patrol(buddy, xs[i] + 0.55f, z0[i] - 0.3f, i % 2 == 0 ? 14f : -14f, spd));
            }
        }
    }

    CharacterNPC AddCrowd(Vector3 pos, float yaw, NpcStyle style, string name, bool still)
    {
        var npc = CharacterNPC.Create(transform, pos, style, name);
        npc.transform.rotation = Quaternion.Euler(0, yaw, 0);
        npc.freezeFacing = true;
        // Only background shoppers use visibility culling. Their paths and colliders keep ticking off-camera.
        foreach (var skin in npc.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var bounds = skin.localBounds;
            bounds.Expand(bounds.size * 0.35f);
            skin.localBounds = bounds;
            skin.updateWhenOffscreen = false;
        }
        foreach (var animator in npc.GetComponentsInChildren<Animator>(true))
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        if (still)
        {
            var cap = npc.gameObject.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0, 0.85f, 0); cap.radius = 0.28f; cap.height = 1.7f;
        }
        if (!still) npc.MakePhysical();          // walkers collide with cars, carts, the cow and each other
        crowd.Add(npc);
        return npc;
    }

    IEnumerator Patrol(CharacterNPC n, float x, float zStart, float dz, float speed)
    {
        float lo = 3f, hi = 74f;
        float zTarget = Mathf.Clamp(zStart + dz, lo, hi);
        while (n != null)
        {
            yield return n.WalkTo(new Vector3(x, 0, zTarget), speed, -1f, false);     // stuck in the crowd: just turn back
            zTarget = zTarget > zStart ? Mathf.Clamp(zStart - Mathf.Abs(dz), lo, hi) : Mathf.Clamp(zStart + Mathf.Abs(dz), lo, hi);
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.5f, 2.5f));
        }
    }

    // ==================================================================== UPDATE
    void Update()
    {
        if (loading) return;
        var kb = Keyboard.current;

        if (kb != null && kb.escapeKey.wasPressedThisFrame && phase != Phase.Ending) SetPaused(!paused);
        if (paused) return;

        if (kb != null && kb.jKey.wasPressedThisFrame && phase == Phase.Free) journalOpen = !journalOpen;
        // Enter = skip the whole conversation / cutscene (choices and the timed SUV decision still wait for you)
        if (kb != null && kb.enterKey.wasPressedThisFrame && dlgOptions == null && !choiceActive
            && (phase == Phase.Intro || phase == Phase.Dialogue || phase == Phase.Hook))
            skipAll = true;
        if (phase == Phase.Free || phase == Phase.Choice || phase == Phase.Outcome || phase == Phase.Ending) skipAll = false;

        fade = Mathf.MoveTowards(fade, fadeTarget, fadeSpeed * Time.deltaTime);

        bool witnessSpeaking = phase == Phase.Dialogue || phase == Phase.Hook || phase == Phase.Choice;
        musicSrc.volume = Mathf.MoveTowards(musicSrc.volume, witnessSpeaking ? 0.1f : 0.3f, Time.deltaTime * 0.6f);
        vendorSrc.volume = Mathf.MoveTowards(vendorSrc.volume, witnessSpeaking ? 0.18f : 0.7f, Time.deltaTime * 2f);
        ambienceDuck = Mathf.MoveTowards(ambienceDuck, witnessSpeaking ? 0.72f : 1f, Time.deltaTime * 0.8f);
        if (ambience != null && !rampingAmbience) ambience.volume = ambienceLevel * ambienceDuck;

        if (phase == Phase.Ending)
        {
            if (showEnd && kb != null && kb.rKey.wasPressedThisFrame) builder.Build();
            return;
        }

        if (choiceActive && choiceResult < 0) choiceResult = compass.Poll();

        UpdateInteraction();
        UpdateStoryFlow();
        AvoidVehicles();
    }

    void SetPaused(bool p)
    {
        paused = p;
        GameState.SetPaused(p);
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        MarketShadows(false);
    }

    // The moon's shadows in this narrow lane: two cascades over 28 m instead of the pipeline's four over 40 m (the far
    // lane is in fog and lit by lamps that cast no shadows). Measured on the route: walk p95 54 -> 34 ms on its own.
    // The pipeline asset is shared by every scene, so its values are put back when the market goes.
    static int savedCascades = -1; static float savedShadowDistance;

    static void MarketShadows(bool on)
    {
        var asset = UniversalRenderPipeline.asset;
        if (asset == null) return;
        if (on && savedCascades < 0)
        {
            savedCascades = asset.shadowCascadeCount; savedShadowDistance = asset.shadowDistance;
            asset.shadowCascadeCount = 2; asset.shadowDistance = 28f;
        }
        else if (!on && savedCascades >= 0)
        {
            asset.shadowCascadeCount = savedCascades; asset.shadowDistance = savedShadowDistance;
            savedCascades = -1;
        }
    }

    void UpdateStoryFlow()
    {
        if (phase != Phase.Free) return;

        // Bhaskar's optional chat: if the player just walks into the market, the auto leaves
        if (!bhaskarDone && player.transform.position.z > 14f) StartCoroutine(AutoLeaves(false));

        // objective text
        if (!bhaskarDone) { objective = "Talk to Bhaskar (optional), then find Vikram's chai stall"; }
        else if (!vikramDone) { objective = "Find Vikram's chai stall - the number on Priya's sticky note"; }
        else if (!reddyResolved) { objective = "Look around the market. Someone is watching you..."; }
        else if (idCard != null) { objective = "Pick up the ID card she dropped"; }
        else { objective = "Return to Vikram's stall"; }

        if (!vikramDone) { objectiveTarget = world.VikramStallFront + Vector3.up * 2.4f; objectiveTargetName = "Vikram's chai stall"; }
        else if (!reddyResolved && reddy != null) { objectiveTarget = reddy.transform.position + Vector3.up * 1.9f; objectiveTargetName = "Suspicious woman"; }
        else if (idCard != null) { objectiveTarget = idCard.transform.position + Vector3.up * 0.5f; objectiveTargetName = "ID card"; }
        else if (reddyResolved && !hookStarted) { objectiveTarget = world.VikramStallFront + Vector3.up * 2.4f; objectiveTargetName = "Vikram's chai stall"; }
        else objectiveTarget = null;

        // Inspector Reddy notices you and bolts
        if (vikramDone && reddy != null && !reddyFled && Vector3.Distance(player.transform.position, reddy.transform.position) < 6.5f)
            StartCoroutine(ReddyFlees(true));
        else if (vikramDone && reddy != null && !reddyFled && Time.time - vikramDoneAt > 45f)
            StartCoroutine(ReddyFlees(false));

        // the hook: when she's dealt with and the player is back near the stall
        bool cardHandled = idCard == null || Time.time - idDropAt > 25f;
        if (vikramDone && reddyResolved && cardHandled && !hookStarted && Vector3.Distance(player.transform.position, world.VikramStallFront) < 4.5f)
            StartCoroutine(HookSequence());
    }

    // ============================================================ INTERACTION
    bool Aim(Vector3 p, float maxDist, out float angle)
    {
        angle = 999f;
        Vector3 d = p - cam.position;
        float dist = d.magnitude;
        if (dist > maxDist || dist < 0.05f) return false;
        angle = Vector3.Angle(cam.forward, d);
        float lim = Mathf.Clamp(Mathf.Atan2(0.5f, dist) * Mathf.Rad2Deg, 6f, 20f);
        return angle <= lim;
    }

    void UpdateInteraction()
    {
        prompt = "";
        if (phase != Phase.Free || PriyaRoomPlayer.Locked) return;

        float best = 999f, a;
        Action act = null;

        if (bhaskar != null && !bhaskarDone && Aim(bhaskar.transform.position + Vector3.up * 1.3f, 4f, out a) && a < best)
        { best = a; prompt = "[E]  Talk to Bhaskar"; act = () => StartCoroutine(BhaskarTalk()); }

        if (vikram != null && !vikramDone && Aim(vikram.transform.position + Vector3.up * 1.3f, 4.5f, out a) && a < best)
        { best = a; prompt = "[E]  Talk to Vikram"; act = () => StartCoroutine(VikramTalk()); }

        if (idCard != null && Aim(idCard.transform.position, 3.5f, out a) && a < best)
        { best = a; prompt = "[E]  Pick up the ID card"; act = () => StartCoroutine(PickUpId()); }

        if (act != null && !journalOpen && GameInput.InteractPressed)
        {
            sfx.PlayOneShot(clickClip, 0.2f);
            act();
        }
    }

    // =================================================================== HELPERS
    static AudioClip VO(string f) { return Resources.Load<AudioClip>("VO/" + f); }

    void Toast(string t, float dur = 4f, bool big = false) { toast = t; toastUntil = Time.time + dur; toastBig = big; }
    void ShowTitle(string a, string b, float dur) { titleA = a; titleB = b; titleStart = Time.time; titleEnd = Time.time + dur; }

    void AddEvidence(string e)
    {
        if (evidence.Contains(e)) return;
        evidence.Add(e);
        SaveSystem.Data.evidence = new List<string>(evidence); SaveSystem.Save();
        sfx.PlayOneShot(dingClip, 0.5f);
        // the notes are taught once and confirmed once; after that the soft chime says it (the toast repeated after every answer)
        if (evidence.Count <= 2) Toast(evidence.Count <= 1 ? "Evidence added to your notes (J)" : "Evidence noted", evidence.Count <= 1 ? 3f : 2f);
    }

    IEnumerator W(float seconds)
    {
        float t = 0;
        while (t < seconds && !skipAll) { t += Time.deltaTime; yield return null; }
    }

    IEnumerator Turn(Vector3 worldTarget, float dur)
    {
        Vector3 d = worldTarget - cam.position;
        float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
        float y0 = player.Yaw, p0 = player.Pitch, t = 0;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0, 1, t / dur);
            player.SetLook(Mathf.LerpAngle(y0, yaw, k), Mathf.Lerp(p0, pitch, k));
            yield return null;
        }
    }

    /// <summary>One spoken line in the dialogue panel. E / Space skips it.</summary>
    IEnumerator Line(string speaker, string text, string file, CharacterNPC npc)
    {
        dlgSpeaker = speaker; dlgText = text; dlgOptions = null;
        if (skipAll) { voice.Stop(); if (npc != null) npc.Silence(); yield break; }
        var clip = VO(file);
        dialogueSource = clip == null ? null : (npc != null ? npc.VoiceSource : voice);
        float dur = clip != null ? clip.length + 0.16f : Mathf.Max(2.5f, text.Length * 0.06f);
        if (clip != null)
        {
            if (npc != null) npc.Say(clip);
            else { voice.Stop(); voice.clip = clip; voice.Play(); }
        }
        yield return null;
        float t = 0;
        while (t < dur)
        {
            if (GameState.Paused) { yield return null; continue; }
            var kb = Keyboard.current;
            if (skipAll || GameInput.InteractPressed || (kb != null && kb.spaceKey.wasPressedThisFrame))
            {
                voice.Stop(); if (npc != null) npc.Silence();
                break;
            }
            t += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator Menu(string[] labels, string[] options, bool[] disabled, bool[] asked = null, string lockedHint = null)
    {
        dlgOptions = options;
        compass.Open(labels, options, disabled, asked, lockedHint);
        skipAll = false;
        menuResult = -1;
        do
        {
            yield return null;
            menuResult = compass.Poll();
        } while (menuResult < 0);
        sfx.PlayOneShot(clickClip, 0.18f);
        dlgOptions = null;
    }

    void EndDialogue()
    {
        AutoFocus.FocusOn = null;
        dlgSpeaker = null; dlgText = null; dlgOptions = null;
        PriyaRoomPlayer.Locked = false;
        phase = Phase.Free;
    }

    // ===================================================================== INTRO
    /// <summary>Restore the saved Act II state: who has been talked to, the evidence notes, where the player should stand.</summary>
    void ApplyResume()
    {
        var d = SaveSystem.Data;
        evidence.AddRange(d.evidence);
        var cc = player.GetComponent<CharacterController>();
        if (d.bhaskarDone)
        {
            resumedMid = true;
            bhaskarDone = true;
            if (bhaskar != null) { Destroy(bhaskar.gameObject); bhaskar = null; }
            if (world.Auto != null) { Destroy(world.Auto.gameObject); world.Auto = null; }
            cc.enabled = false; player.transform.position = new Vector3(-0.4f, 0.1f, 12f); cc.enabled = true;
        }
        if (d.vikramDone)
        {
            vikramDone = true; vikramDoneAt = Time.time;
            cc.enabled = false; player.transform.position = new Vector3(-0.4f, 0.1f, 27f); cc.enabled = true;
            if (!d.reddyResolved) SpawnReddy();
        }
        if (d.reddyResolved) { reddyFled = true; reddyResolved = true; }
    }

    IEnumerator Intro()
    {
        if (resumedMid)         // coming back mid-act: skip the title card and narration
        {
            phase = Phase.Free;
            fade = 1f; fadeTarget = 0f; fadeSpeed = 1.2f;
            StartCoroutine(RampAmbience(0f, 0.9f, 2f));
            PriyaRoomPlayer.Locked = false;
            Toast("Welcome back, " + SaveSystem.Profile.playerName + ".   Progress restored.", 6f);
            yield break;
        }
        phase = Phase.Intro;
        fade = 1f; fadeTarget = 1f;
        StartCoroutine(RampAmbience(0f, 0.9f, 4f));
        yield return W(0.3f);
        ShowTitle("ACT II  -  THE STREETS KNOW", "Chandnipur Market District   |   7:30 PM", 4.2f);
        fadeSpeed = 1.1f; fadeTarget = 0f;
        yield return W(0.65f);
        PriyaRoomPlayer.Locked = false;
        phase = Phase.Free;
        if (skipAll) { skipAll = false; fade = 0f; titleEnd = 0f; yield break; }
        var intro = VO("arjun_act2_intro");
        const string opening = "ARJUN (V.O.):  Night falls on Chandnipur, and the city changes its face. Neon, smoke, and a thousand strangers. Every shadow could be a threat now.";
        sub = opening;
        subUntil = Time.time + (intro != null ? intro.length : 7f);
        if (intro != null) { voice.clip = intro; voice.Play(); }
        // The narration travels with the player and yields immediately when they approach a witness.
        while (phase == Phase.Free && Time.time < subUntil)
        {
            var kb = Keyboard.current;
            if (!paused && kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) break;
            yield return null;
        }
        // a witness (or a skip) takes over: the narration fades in a few hundredths rather than cutting with a click
        if (voice.clip == intro && voice.isPlaying) StartCoroutine(FadeOutVoice(voice, intro, 0.14f));
        if (sub == opening) sub = null;
        if (phase != Phase.Free) titleEnd = 0f;
    }

    IEnumerator FadeOutVoice(AudioSource src, AudioClip clip, float dur)
    {
        float v0 = src.volume;
        for (float t = 0; t < dur && src.clip == clip && src.isPlaying; t += Time.unscaledDeltaTime) { src.volume = v0 * (1f - t / dur); yield return null; }
        if (src.clip == clip) src.Stop();
        src.volume = v0;
    }

    float ambienceLevel, ambienceDuck = 1f; bool rampingAmbience;

    IEnumerator RampAmbience(float from, float to, float dur)
    {
        float t = 0;
        rampingAmbience = true;
        while (t < dur) { t += Time.deltaTime; ambienceLevel = Mathf.Lerp(from, to, t / dur); ambience.volume = ambienceLevel * ambienceDuck; yield return null; }
        ambienceLevel = to; ambience.volume = to * ambienceDuck;
        rampingAmbience = false;
    }

    // =================================================================== BHASKAR
    IEnumerator BhaskarTalk()
    {
        phase = Phase.Dialogue; PriyaRoomPlayer.Locked = true; journalOpen = false; prompt = ""; objective = "";
        AutoFocus.FocusOn = bhaskar.transform;
        yield return Turn(bhaskar.transform.position + Vector3.up * 1.5f, 0.8f);
        bhaskar.PlayOnce("Wave");

        yield return Line("BHASKAR", "Where to, sir? Market? Good, good. Traffic is less now. The gods are kind.", "b_greet", bhaskar);

        bool askedA = false; bool[] asked = new bool[3];
        string[] opts =
        {
            "Do you know a woman named Priya Kapoor? Lives in the old quarter?",
            "What did the person on the phone sound like?",
            "Did anyone follow you?",
            "That's enough. Take me to the market.",
        };
        while (true)
        {
            var dis = new bool[] { asked[0], !askedA || asked[1], !askedA || asked[2], false };
            yield return Menu(new[] { "Ask about Priya", "The caller", "The black car", "End the ride" },
                (string[])opts.Clone(), dis, asked, "Ask him about Priya first.");
            int c = menuResult;
            if (c == 3) break;
            asked[c] = true;
            if (c == 0)
            {
                yield return Line("ARJUN", opts[0], "q_b_a", null);
                yield return Line("BHASKAR", "Priya madam? Of course! She takes my auto sometimes. Going to the court, going to the slum. Good woman. Always pays full fare, never bargains. Why?", "b_a", bhaskar);
                yield return Line("ARJUN", "She's missing.", "q_b_a2", null);
                yield return Line("BHASKAR", "Missing? Arre bhai... actually, I drove her two nights ago. Late, around ten. She flagged me down near the temple. She was on the phone, very tense. She kept saying, don't come, I'll handle it. Then she told me to take her to the old mill. You know, the abandoned textile mill near the river. But halfway there, she got another call, and her face went white. She said, take me back. Take me back, right now. I asked if everything was okay. She gave me five hundred rupees for a fifty rupee ride, and said, forget this ride. You never saw me tonight.", "b_a2", bhaskar);
                askedA = true;
                AddEvidence("Bhaskar drove Priya 2 nights ago. After a frightening call she said: \"You never saw me tonight.\"");
            }
            else if (c == 1)
            {
                yield return Line("ARJUN", opts[1], "q_b_b", null);
                yield return Line("BHASKAR", "I couldn't hear clearly, but it sounded like a man. Older. Speaking Hindi with an English accent, you know, the rich people type. Priya madam was saying, I have the documents, you can't stop me. Then later, I know what you did to the records. I don't know what it means, but it sounded serious.", "b_b", bhaskar);
                AddEvidence("The caller: an older man, rich accent. Priya: \"I know what you did to the records.\"");
            }
            else
            {
                yield return Line("ARJUN", opts[2], "q_b_c", null);
                yield return Line("BHASKAR", "Now that you mention it... there was a black car. An SUV. It was behind us for a while. I thought it was just traffic, but it turned when I turned. When Priya madam told me to go back, the SUV was gone. Maybe it was nothing. Maybe it wasn't.", "b_c", bhaskar);
                AddEvidence("A black SUV followed Priya's auto.");
            }
            if (asked[0] && asked[1] && asked[2]) break;
        }
        yield return Line("BHASKAR", "Here we are, sir, the edge of the market. If you need anything, call me. Priya madam is a good person. Find her, bhai.", "b_bye", bhaskar);
        EndDialogue();
        StartCoroutine(AutoLeaves(true));
    }

    IEnumerator AutoLeaves(bool withBhaskar)
    {
        if (bhaskarDone) yield break;
        bhaskarDone = true;
        SaveSystem.Data.bhaskarDone = true; SaveSystem.Save();
        if (bhaskar != null)
        {
            yield return bhaskar.WalkTo(new Vector3(world.AutoPos.x, 0, world.AutoPos.z), 2.2f);
            Destroy(bhaskar.gameObject); bhaskar = null;
        }
        if (world.Auto == null) yield break;
        float t = 0, v = 0;
        while (t < 14f && world.Auto != null)
        {
            if (PathBlocked(world.Auto, 1f, 0.95f, 1.4f)) { yield return null; continue; }
            v = Mathf.Min(v + 2.5f * Time.deltaTime, 6f);
            world.Auto.localPosition += new Vector3(0, 0, v * Time.deltaTime);
            t += Time.deltaTime;
            yield return null;
        }
        if (world.Auto != null) Destroy(world.Auto.gameObject);
    }

    // =================================================================== VIKRAM
    IEnumerator VikramTalk()
    {
        phase = Phase.Dialogue; PriyaRoomPlayer.Locked = true; journalOpen = false; prompt = ""; objective = "";
        objectiveTarget = null;
        vikramTalking = true;
        if (!bhaskarDone) StartCoroutine(AutoLeaves(false));
        AutoFocus.FocusOn = vikram.transform;
        yield return Turn(vikram.transform.position + Vector3.up * 1.5f, 0.8f);

        // ---------- Tier 1: cold
        trust = "COLD";
        yield return Line("VIKRAM", "Chai? Cutting or full? ... No, I don't know any Priya Kapoor. You have the wrong stall, brother.", "v_cold", vikram);
        string[] t1 =
        {
            "(Show the sticky note)  \"This is your number, Vikram. Priya hid it in her apartment. She trusted you.\"",
            "\"Her mother is terrified, Vikram. She thinks something terrible has happened to her daughter. If you know anything, please.\"",
            "\"I know you're lying. I can involve the police if I have to.\"",
        };
        yield return Menu(new[] { "Show the note", "Appeal to him", "Threaten police" }, t1, null);
        int c1 = menuResult;
        bool goodTrust = c1 != 2;
        if (c1 == 0)
        {
            yield return Line("ARJUN", "This is your number, Vikram. Priya hid it in her apartment. She trusted you.", "q_v_1a", null);
            yield return Reaction(c1);
            yield return Line("VIKRAM", "Where did you get that? ... Okay. Okay. Sit down. But keep your voice low.", "v_t1a", vikram);
        }
        else if (c1 == 1)
        {
            yield return Line("ARJUN", "Her mother is terrified, Vikram. She thinks something terrible has happened to her daughter. If you know anything, please.", "q_v_1b", null);
            yield return Reaction(c1);
            yield return Line("VIKRAM", "Kamala-ji... she's a good woman. ... Alright. Sit. But if anyone asks, you're just drinking chai.", "v_t1b", vikram);
        }
        else
        {
            yield return Line("ARJUN", "I know you're lying. I can involve the police if I have to.", "q_v_1c", null);
            trust = "ANGRY";
            yield return Reaction(c1);
            yield return Line("VIKRAM", "Then involve the police. I have nothing to say to you.", "v_t1c", vikram);
            yield return Line("VIKRAM", "Alright, alright. Priya came here once. She asked about the land records. That is all I know. Now please, go. I have customers.", "v_partial", vikram);
        }

        // ---------- Tier 2: warming
        if (goodTrust)
        {
            trust = "WARMING";
            yield return CheckLane();             // he looks down the lane both ways before he says anything that matters
            yield return Line("VIKRAM", "Priya is like a daughter to me. I've known her since she was this tall. She came to me three weeks ago. She was investigating the Renewal Project, the one that's going to destroy Basti Nagar. My home, brother. I grew up there. My father built our house with his own hands. And now Councilor Deshpande wants to bulldoze it for a shopping mall.", "v_t2_intro", vikram);
        }
        int limit = goodTrust ? 3 : 1;
        bool[] a2 = new bool[3];
        int asked2 = 0;
        string[] q2 =
        {
            "What did she find?", "Who is she afraid of?", "Where is she now?", "That's all I need. Thank you.",
        };
        while (asked2 < limit)
        {
            var show = new string[4];
            var dis = new bool[4];
            for (int i = 0; i < 3; i++) { show[i] = q2[i]; dis[i] = a2[i]; }
            show[3] = q2[3];
            yield return Menu(new[] { "The evidence", "Her fears", "Her whereabouts", "Thank Vikram" }, show, dis, a2);
            int c = menuResult;
            if (c == 3) break;
            a2[c] = true; asked2++;
            if (c == 0)
            {
                yield return Line("ARJUN", q2[0], "q_v_2a", null);
                yield return Line("VIKRAM", "She found that the land records were forged. The government claims the Basti Nagar land was acquired in 1965 under eminent domain. But Priya found the original deed from 1942. It shows the land was granted to the residents by the Maharaja himself. It was never acquired. The entire Renewal Project is illegal.", "v_t2a", vikram);
                AddEvidence("Land records forged: the original 1942 deed proves Basti Nagar was never acquired. The Renewal Project is illegal.");
            }
            else if (c == 1)
            {
                yield return Line("ARJUN", q2[1], "q_v_2b", null);
                yield return Line("VIKRAM", "Councilor Meghna Deshpande. She is the one behind the project. But it's not just her. There's a builder, Raj Malhotra, and people inside the police. Priya said the corruption goes all the way up. She was scared, but she wouldn't stop. That girl has a spine of steel.", "v_t2b", vikram);
                AddEvidence("Behind it: Councilor Meghna Deshpande, builder Raj Malhotra, and people inside the police.");
            }
            else
            {
                yield return Line("ARJUN", q2[2], "q_v_2c", null);
                yield return Line("VIKRAM", "I don't know exactly. But the last time I spoke to her, she mentioned the Kali Temple. She said, Kali Ma will protect the truth. I thought she was being poetic, but now I think she meant it literally.", "v_t2c", vikram);
                AddEvidence("Priya mentioned the Kali Temple: \"Kali Ma will protect the truth.\"");
            }
        }

        // ---------- Tier 3: trust
        if (goodTrust && asked2 >= 2)
        {
            trust = "TRUSTING";
            vikram.PlayOnce("Interact");
            yield return Line("VIKRAM", "Wait. There's something else.", "v_t3a", vikram);
            AddEvidence("USB DRIVE from Priya: forged documents, the original deed, photographs, all her research.");
            Toast("You received: USB DRIVE", 5f, true);
            SaveSystem.Data.hasUsb = true; SaveSystem.Save();
            yield return Line("ARJUN", "Priya's research. Everything she found, in one small drive. This could change everything.", "arjun_usb", null);
            yield return Line("VIKRAM", "Be careful, Mr. Mehra. The people behind this, they don't play games. Two days ago, two men came to my stall. They asked if a woman had been asking questions about the land records. I said no. They broke my kettle and told me to keep saying no. These are not people you argue with.", "v_t3b", vikram);
        }
        else
        {
            yield return Line("VIKRAM", "Go carefully, brother. And drink some chai next time.", "v_bye", vikram);
            if (!goodTrust) Toast("Vikram didn't trust you enough to share everything.", 5f);
        }

        vikramTalking = false;
        vikramDone = true;
        SaveSystem.Data.vikramDone = true; SaveSystem.Save();
        vikramDoneAt = Time.time;
        EndDialogue();
        SpawnReddy();
    }

    /// <summary>Before he confides, Vikram checks the lane: a look toward the far gate, a look back toward the entrance, then
    /// back to Arjun (his head turns are eased; about 1.4 s in all, skipped with the conversation).</summary>
    IEnumerator CheckLane()
    {
        if (skipAll || vikram == null) yield break;
        var keep = vikram.lookTarget;
        var probe = new GameObject("VikramLooks").transform;
        probe.SetParent(transform, false);
        dlgText = "..."; dlgSpeaker = "VIKRAM"; dialogueSource = null;
        foreach (var at in new[] { new Vector3(0.3f, 1.6f, 8f), new Vector3(0.3f, 1.6f, -7f) })
        {
            probe.position = vikram.transform.position + at;
            vikram.lookTarget = probe;
            float t = 0f, dur = at.z > 0 ? 0.75f : 0.55f;
            while (t < dur && !skipAll) { if (!GameState.Paused) t += Time.deltaTime; yield return null; }
        }
        vikram.lookTarget = keep;
        Destroy(probe.gameObject);
        float b = 0f;
        while (b < 0.15f && !skipAll) { if (!GameState.Paused) b += Time.deltaTime; yield return null; }
    }

    /// <summary>
    /// The camera stays on Vikram for a beat after Arjun's challenge, so his reaction reads before he answers: the note
    /// makes him look down at it, an appeal lets him soften, a threat makes him straighten and stare. No line plays.
    /// </summary>
    IEnumerator Reaction(int choice)
    {
        if (skipAll || vikram == null) yield break;
        dlgSpeaker = "VIKRAM"; dlgText = choice == 0 ? "..." : choice == 1 ? "..." : "...";
        dialogueSource = null;
        var keep = vikram.lookTarget;
        if (choice == 0) { vikram.lookTarget = null; vikram.PlayOnce("Interact"); }
        float t = 0f;
        while (t < 0.75f && !skipAll) { if (!GameState.Paused) t += Time.deltaTime; yield return null; }
        vikram.lookTarget = keep;
    }

    // ===================================================================== REDDY
    void SpawnReddy()
    {
        reddy = CharacterNPC.Create(transform, world.ReddyPos, NpcStyle.Reddy(), "Inspector_Reddy");
        reddy.transform.rotation = Quaternion.Euler(0, -90f, 0);
        reddy.lookTarget = cam;
        if (reddyBike == null) reddyBike = world.SpawnBike(new Vector3(5.3f, 0f, 46.4f), -105f, new Color(0.08f, 0.08f, 0.1f));   // her way out
        Toast("A woman across the street is watching you...", 5f);
    }

    Transform reddyBike;

    IEnumerator ReddyFlees(bool dropCard)
    {
        reddyFled = true;
        if (dropCard)
        {
            idCard = GameKit.Box(transform, "ID_Card", world.IdDropPos, new Vector3(0.14f, 0.012f, 0.09f), GameKit.Glow(new Color(0.8f, 0.85f, 0.95f), new Color(0.6f, 0.8f, 1f), 1.5f));
            idCard.transform.rotation = Quaternion.Euler(0, 25f, 0);
            idDropAt = Time.time;
            GameKit.PointLight(idCard.transform, Vector3.up * 0.3f, new Color(0.6f, 0.8f, 1f), 0.8f, 2f);
        }
        reddy.freezeFacing = true;
        if (reddyBike == null) reddyBike = world.SpawnBike(new Vector3(5.3f, 0f, 46.4f), -105f, new Color(0.08f, 0.08f, 0.1f));
        var bike = reddyBike; reddyBike = null;

        // she breaks for the motorbike parked at the mouth of the side alley (running if she has been made)
        float pace = dropCard ? 3.7f : 1.5f;
        yield return reddy.WalkTo(new Vector3(3.7f, 0, 41.2f), pace);
        StartCoroutine(reddy.WalkTo(new Vector3(4.55f, 0, 45.6f), pace));
        yield return null;
        while (reddy != null && reddy.IsWalking)          // steps down off the shop plinth into the alley
        {
            var lp = reddy.transform.localPosition;
            if (lp.x > 3.95f) { lp.y = Mathf.MoveTowards(lp.y, 0f, Time.deltaTime * 1.2f); reddy.transform.localPosition = lp; }
            yield return null;
        }

        // swings a leg over, kicks it off the stand, lights on
        reddy.Ride(bike, new Vector3(0, 0.84f, -0.32f), new Vector3(0.22f, 0.34f, -0.02f), new Vector3(0.3f, 1.06f, 0.34f));
        var eng = bike.gameObject.AddComponent<AudioSource>();
        eng.clip = engineClip; eng.loop = true; eng.spatialBlend = 1f; eng.minDistance = 4f; eng.maxDistance = 45f; eng.rolloffMode = AudioRolloffMode.Linear;
        eng.volume = 0f; eng.pitch = 0.7f; eng.Play();
        float yaw = bike.eulerAngles.y, roll = 9f;
        for (float t = 0; t < 0.9f; t += Time.deltaTime)
        {
            roll = Mathf.Lerp(9f, 0f, Mathf.SmoothStep(0, 1, t / 0.7f));
            bike.rotation = Quaternion.Euler(0, yaw, roll);
            eng.volume = Mathf.Lerp(0f, 0.55f, t / 0.9f);
            yield return null;
        }
        world.BikeLights(bike);
        yield return new WaitForSeconds(0.25f);

        // out of the alley, a hard left into the lane, and away through the far gate
        vehicles.Add(bike);
        var wheels = new List<Transform>();
        foreach (Transform ch in bike) if (ch.name == "Wheel") wheels.Add(ch);
        Vector3[] wp = { new Vector3(1.6f, 0, 47.0f), new Vector3(0.4f, 0, 51f), new Vector3(0.2f, 0, 63f), new Vector3(0f, 0, 82f), new Vector3(0f, 0, 106f) };
        float speed = 0f, top = dropCard ? 12.5f : 7.5f, lean = 0f; int wi = 0;
        while (bike != null && wi < wp.Length)
        {
            float dt = Time.deltaTime;
            Vector3 to = wp[wi] - bike.position; to.y = 0;
            if (to.magnitude < Mathf.Max(1.2f, speed * 0.25f)) { wi++; continue; }
            float want = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            float turn = Mathf.DeltaAngle(yaw, want);
            float maxTurn = Mathf.Lerp(150f, 55f, speed / top) * dt;
            float step = Mathf.Clamp(turn, -maxTurn, maxTurn);
            yaw += step;
            // ease off in the corner and for anyone in the way, then wind it open on the straight
            float target = Mathf.Abs(turn) > 25f ? Mathf.Min(top, 4.5f) : top;
            speed = Mathf.MoveTowards(speed, target, (speed < target ? 5.5f : 9f) * dt);
            lean = Mathf.Lerp(lean, Mathf.Clamp(-step / Mathf.Max(dt, 0.0001f) * speed * 0.028f, -30f, 30f), dt * 6f);
            bike.rotation = Quaternion.Euler(0, yaw, lean);
            Vector3 np = bike.position + Quaternion.Euler(0, yaw, 0) * Vector3.forward * speed * dt;
            np.y = Mathf.MoveTowards(np.y, np.x > 3.3f && np.x < 4.05f ? 0.18f : 0f, dt * 2.5f);      // bumps over the plinth at the alley mouth
            bike.position = np;
            foreach (var w in wheels) if (w != null) w.Rotate(Vector3.right, speed * dt / 0.3f * Mathf.Rad2Deg, Space.Self);
            eng.pitch = 0.7f + speed / top * 1.1f;
            eng.volume = 0.55f;
            yield return null;
        }
        vehicles.Remove(bike);
        if (bike != null) Destroy(bike.gameObject);       // she is on it
        reddy = null;
        reddyResolved = true;
        SaveSystem.Data.reddyResolved = true; SaveSystem.Save();
        if (dropCard) Toast("She dropped something. Look near the newspaper stand.", 6f);
    }

    IEnumerator PickUpId()
    {
        if (idCard == null) yield break;
        phase = Phase.Dialogue; PriyaRoomPlayer.Locked = true;
        Destroy(idCard); idCard = null;
        SaveSystem.Data.hasIdCard = true; SaveSystem.Save();
        AddEvidence("ID card: INSPECTOR SONAL REDDY, Crime Branch, Chandnipur Police. She was following you, or Vikram.");
        Toast("ID CARD:  INSPECTOR SONAL REDDY  -  Crime Branch", 5f, true);
        yield return Line("ARJUN", "A cop. Plainclothes. She's been following me, or following Vikram. Whose side is she on?", "arjun_reddy", null);
        EndDialogue();
    }

    // ====================================================================== HOOK
    IEnumerator HookSequence()
    {
        hookStarted = true; journalOpen = false; prompt = "";
        phase = Phase.Hook; PriyaRoomPlayer.Locked = true; objective = ""; objectiveTarget = null;
        if (idCard != null) { Destroy(idCard); }
        yield return Turn(vikram.transform.position + Vector3.up * 1.5f, 0.9f);

        // he greets Arjun like any customer (they are being watched) and the stall phone cuts across him
        var greet = VO("v_cold");
        if (greet != null && !skipAll)
        {
            vikram.Say(greet); dialogueSource = vikram.VoiceSource;
            dlgSpeaker = "VIKRAM"; dlgText = "Chai? Cutting or full?";
            yield return W(1.3f);                     // "Chai? Cutting or full?" ends at 1.27 s in the recording
            vikram.Silence();
        }
        dlgText = null;
        phone.PlayOneShot(ringClip, 0.55f);
        vikram.PlayOnce("Interact");
        yield return W(1.1f);
        yield return Line("VIKRAM", "They know you're here. Deshpande's people. They were watching the stall. You need to leave. Now.", "v_phone", vikram);
        dlgSpeaker = null; dlgText = null;

        // lights flicker, the SUV appears
        StartCoroutine(DimStallLamp());
        yield return Turn(new Vector3(world.SuvStart.x, 1.5f, 70f), 1.2f);
        AutoFocus.FocusOn = world.Suv;
        world.Suv.gameObject.SetActive(true);
        suvAudio.volume = 0f; suvAudio.Play();
        StartCoroutine(RampSuvAudio());
        float stopZ = 54f;
        yield return MoveSuv(stopZ, 6f, 2f);
        // tilt down a little once it has stopped, so the SUV sits above the timed dial instead of under its labels
        if (world.Suv != null) yield return Turn(new Vector3(world.Suv.position.x, -1.1f, world.Suv.position.z), 0.6f);
        yield return W(0.6f);

        // ----- timed choice (15 s)
        skipAll = false;
        phase = Phase.Choice;
        choiceOptions = new[]
        {
            "HIDE behind Vikram's counter",
            "WALK CALMLY into the crowd",
            "NOTE THE LICENSE PLATE",
        };
        compass.Open(new[] { "Hide", "Walk calmly", "Read the plate" }, choiceOptions);
        choiceResult = -1; choiceActive = true; choiceDeadline = Time.time + 15f;
        PriyaRoomPlayer.Locked = true;
        while (choiceResult < 0 && Time.time < choiceDeadline) yield return null;
        choiceActive = false;
        if (choiceResult < 0) { choiceResult = 1; Toast("You froze... and walked on, head down.", 3f); }
        sfx.PlayOneShot(clickClip, 0.2f);

        skipAll = false;
        phase = Phase.Outcome;
        if (choiceResult == 0) yield return OutcomeHide();
        else if (choiceResult == 1) yield return OutcomeWalk();
        else yield return OutcomePlate();

        AutoFocus.FocusOn = null;
        // the SUV moves on (a shorter hold before the closing line: it sat on 3.5 s of nothing)
        StartCoroutine(SuvLeaves());
        yield return W(2.4f);

        // ----- end of Act II
        var end = VO("arjun_act2_end");
        sub = "ARJUN (V.O.):  The streets of Chandnipur know everything. The question is whether they'll tell you the truth before it's too late.";
        subUntil = Time.time + (end != null ? end.length : 7f) + 0.6f;
        if (end != null) { voice.Stop(); voice.clip = end; voice.Play(); }
        yield return W((end != null ? end.length : 7f) + 0.5f);
        fadeSpeed = 0.5f; fadeTarget = 1f;
        StartCoroutine(RampAmbience(ambienceLevel, 0f, 3f));
        yield return W(2.8f);
        sub = null;
        phase = Phase.Ending;
        SaveSystem.CompleteAct(2);
        var b = builder; var p = player;
        CuratorInterlude.Play(1, b, p, () => ActLoader.Load(3, b, p, false));     // the Curator, then Act III
    }

    IEnumerator MoveSuv(float targetZ, float speed, float easeOut)
    {
        var s = world.Suv;
        float waited = 0f, total = 0f;
        while (s != null && s.localPosition.z > targetZ + 0.05f)
        {
            total += Time.deltaTime;
            if (total > 25f) { s.localPosition = new Vector3(s.localPosition.x, s.localPosition.y, targetZ); break; }   // safety: it always arrives
            // wait briefly if someone is standing in the way, but never for more than 2 seconds
            if (waited < 2f && PathBlocked(s, -1f, 1.1f, 2.4f)) { waited += Time.deltaTime; yield return null; continue; }
            float remain = s.localPosition.z - targetZ;
            float v = Mathf.Min(speed, Mathf.Max(1.2f, remain / easeOut * 2f));
            s.localPosition += new Vector3(0, 0, -v * Time.deltaTime);
            yield return null;
        }
    }

    IEnumerator SuvLeaves()
    {
        var s = world.Suv;
        float v = 1.5f;
        float waitedOut = 0f;
        while (s != null && s.localPosition.z > -9f)
        {
            if (waitedOut < 2f && PathBlocked(s, -1f, 1.1f, 2.4f)) { waitedOut += Time.deltaTime; yield return null; continue; }
            v = Mathf.Min(v + 2f * Time.deltaTime, 7f);
            s.localPosition += new Vector3(0, 0, -v * Time.deltaTime);
            suvAudio.volume = Mathf.Max(0.1f, suvAudio.volume - Time.deltaTime * 0.05f);
            yield return null;
        }
        if (s != null) Destroy(s.gameObject);
    }

    IEnumerator RampSuvAudio()
    {
        for (float t = 0; t < 4f; t += Time.deltaTime) { suvAudio.volume = Mathf.Lerp(0f, 0.9f, t / 4f); yield return null; }
    }

    IEnumerator DimStallLamp()
    {
        // One voltage sag at the tea stall sells the SUV beat without flashing the whole lane.
        var lamp = world.StallLamp;
        if (lamp == null) yield break;

        for (float t = 0; t < 1.4f; t += Time.deltaTime)
        {
            world.StallVoltage = 1f - 0.38f * Mathf.Pow(Mathf.Sin(Mathf.PI * t / 1.4f), 2f);
            yield return null;
        }
        world.StallVoltage = 1f;
    }

    // ---- outcome A: hide
    IEnumerator OutcomeHide()
    {
        // he scrambles to the gate end of Vikram's counter before he ducks (he used to crouch wherever he stood)
        var cc = player.GetComponent<CharacterController>();
        Vector3 cover = new Vector3(-2.35f, player.transform.position.y, 32.45f);
        for (float t = 0; t < 3f; t += Time.deltaTime)
        {
            Vector3 d = cover - player.transform.position; d.y = 0;
            if (d.magnitude < 0.15f) break;
            cc.Move(Vector3.ClampMagnitude(d, 2.6f * Time.deltaTime) + Vector3.down * 0.5f * Time.deltaTime);
            yield return null;
        }
        held = false;                 // the anchor follows him to cover
        Toast("You duck behind the counter...", 3f);
        // crouch
        float y0 = cam.localPosition.y;
        for (float t = 0; t < 0.8f; t += Time.deltaTime)
        {
            var p = cam.localPosition; p.y = Mathf.Lerp(y0, 0.78f, t / 0.8f); cam.localPosition = p;
            yield return null;
        }
        // two men step out
        yield return MoveSuv(47f, 3f, 2f);
        var thugs = new List<CharacterNPC>();
        for (int i = 0; i < 2; i++)
        {
            var th = CharacterNPC.Create(transform, new Vector3(-0.5f + i * 1.7f, 0, 48.5f), NpcStyle.Thug(), "Thug" + i);
            th.freezeFacing = true;
            thugs.Add(th);
        }
        StartCoroutine(thugs[0].WalkTo(new Vector3(1.9f, 0, 41f), 1.6f));
        yield return thugs[1].WalkTo(new Vector3(-1.7f, 0, 42f), 1.6f);
        yield return W(0.8f);
        var clip = VO("thug_overhear");
        sub = "MAN IN DARK CLOTHES:  The councilor wants the file by tomorrow morning. Find the man who is asking questions.";
        subUntil = Time.time + (clip != null ? clip.length : 5f) + 0.4f;
        if (clip != null) thugs[1].Say(clip);
        yield return W((clip != null ? clip.length : 5f) + 0.6f);
        AddEvidence("Overheard: \"The councilor wants the file by tomorrow morning.\" Deshpande's men are hunting the file.");
        Toast("You overheard them. They don't find you.", 4f);
        foreach (var th in thugs) StartCoroutine(thugs[0] == th ? th.WalkTo(new Vector3(0.2f, 0, 47.5f), 1.8f) : th.WalkTo(new Vector3(0.9f, 0, 47.5f), 1.8f));
        yield return W(3.2f);
        foreach (var th in thugs) if (th != null) Destroy(th.gameObject);
        // stand back up
        for (float t = 0; t < 0.8f; t += Time.deltaTime)
        {
            var p = cam.localPosition; p.y = Mathf.Lerp(0.78f, y0, t / 0.8f); cam.localPosition = p;
            yield return null;
        }
    }

    // ---- outcome B: walk calmly
    IEnumerator OutcomeWalk()
    {
        Toast("You pull your collar up and walk into the crowd...", 3.5f);
        var cc = player.GetComponent<CharacterController>();
        StartCoroutine(MoveSuv(41f, 2.2f, 1.5f));
        Vector3 goal = new Vector3(-1.7f, 0, 43f);
        float timeout = 0;
        yield return Turn(new Vector3(-0.4f, 1.5f, 50f), 0.6f);
        while (timeout < 8f)
        {
            Vector3 d = goal - player.transform.position; d.y = 0;
            if (d.magnitude < 0.4f) break;
            cc.Move(d.normalized * 1.6f * Time.deltaTime);
            timeout += Time.deltaTime;
            yield return null;
        }
        yield return Turn(world.Suv != null ? world.Suv.position + new Vector3(0, 1.5f, 0) : new Vector3(1.5f, 1.5f, 42f), 0.7f);
        yield return Line("ARJUN", "One of them is photographing me. They know my face now.", "arjun_photo", null);
        dlgSpeaker = null; dlgText = null;
        AddEvidence("Deshpande's men photographed you. They know your face.");
        Toast("You've been identified.", 4f);
    }

    // ---- outcome C: licence plate
    IEnumerator OutcomePlate()
    {
        Toast("You focus on the licence plate...", 2.5f);
        Vector3 plate = world.PlateText != null ? world.PlateText.position : world.Suv.position + new Vector3(0, 0.5f, -2.4f);
        AutoFocus.FocusOn = world.PlateText;
        yield return Turn(plate, 0.9f);
        float f0 = camComp.fieldOfView;
        for (float t = 0; t < 1.6f; t += Time.deltaTime)
        {
            camComp.fieldOfView = Mathf.Lerp(f0, 22f, Mathf.SmoothStep(0, 1, t / 1.6f));
            // keep aiming at the plate as the SUV creeps closer
            if (world.PlateText != null)
            {
                Vector3 d = world.PlateText.position - cam.position;
                player.SetLook(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg);
            }
            yield return null;
        }
        plateCardUntil = Time.time + 7f;
        Toast("LICENCE PLATE NOTED", 6f, true);
        SaveSystem.Data.hasPlate = true; SaveSystem.Save();
        yield return new WaitForSeconds(1.2f);                                   // let the player actually read it
        yield return Line("ARJUN", "M H, one two, A B, four four seven one. Got it.", "arjun_plate", null);
        dlgSpeaker = null; dlgText = null;
        AddEvidence("Black SUV licence plate: MH-12-AB-4471 (to be traced to Deshpande's office).");
        yield return new WaitForSeconds(2.2f);
        for (float t = 0; t < 0.8f; t += Time.deltaTime)
        {
            camComp.fieldOfView = Mathf.Lerp(22f, f0, t / 0.8f);
            yield return null;
        }
        camComp.fieldOfView = f0;
    }

    // ==================================================== a lane full of life
    /// <summary>Autos and scooters honk their way up the lane every so often (and stop if you stand in the way).</summary>
    static AudioSource SpatialSource(Transform parent, string name, Vector3 position, float range)
    {
        var point = new GameObject(name).transform;
        point.SetParent(parent, false);
        point.position = position;
        var source = point.gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false; source.spatialBlend = 1f; source.dopplerLevel = 0.2f;
        source.minDistance = 1.8f; source.maxDistance = range;
        source.rolloffMode = AudioRolloffMode.Linear;
        return source;
    }

    IEnumerator TrafficLoop()
    {
        yield return new WaitForSeconds(9f);
        while (true)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(11f, 20f));
            if (phase != Phase.Free || hookStarted) continue;
            bool auto = UnityEngine.Random.value < 0.5f;
            float x = UnityEngine.Random.Range(-0.4f, -0.15f);      // left of centre: clear of the auto parked at the lane entrance
            Transform v = auto ? world.SpawnAuto(new Vector3(x, 0, -5f), 0f)
                               : world.SpawnScooter(new Vector3(x, 0, -5f), 0f, Color.HSVToRGB(UnityEngine.Random.value, 0.7f, 0.8f));
            var horn = SpatialSource(v, "Horn", v.position + Vector3.up * 0.75f, 22f);
            float cur = 0f, top = auto ? 4.2f : 5.5f, honkAt = 0f, blockedFor = 0f; int honks = 0;
            vehicles.Add(v);
            while (v != null && v.position.z < 80f && !hookStarted)
            {
                // brake for anyone (you, the crowd, the cow, another vehicle) in the way
                bool blocked = PathBlocked(v, 1f, auto ? 0.72f : 0.45f, auto ? 1.4f : 0.9f);
                cur = Mathf.MoveTowards(cur, blocked ? 0f : top, 6f * Time.deltaTime);
                v.position += new Vector3(0, 0, cur * Time.deltaTime);
                if (!blocked) { blockedFor = 0f; honks = 0; }
                else
                {
                    blockedFor += Time.deltaTime;
                    // a couple of impatient taps, then the driver just waits: nobody leans on the horn for a minute
                    // ...and not over a conversation: the horn masked the witness (the driver waits for Arjun instead)
                    if (honks < 2 && blockedFor > 1.2f && Time.time > honkAt && phase == Phase.Free)
                    {
                        horn.PlayOneShot(hornClip, 0.38f); honks++;
                        honkAt = Time.time + UnityEngine.Random.Range(3.5f, 6f);
                    }
                    // properly stuck and nobody is watching: give up on this run
                    if (blockedFor > 20f && Vector3.Distance(v.position, player.transform.position) > 14f) break;
                }
                yield return null;
            }
            vehicles.Remove(v);
            if (v != null) Destroy(v.gameObject);
        }
    }

    // ======================================================= vehicles vs people
    /// <summary>Is a person, animal or other vehicle in front of this vehicle? (vehicles drive along the Z axis)</summary>
    bool PathBlocked(Transform v, float dirSign, float halfWidth, float halfLength)
    {
        Vector3 center = v.position + new Vector3(0, 0.9f, dirSign * (halfLength + 1.6f));
        int count;
        // A crowded intersection can fill the buffer. Grow and retry so a pedestrian is never dropped.
        while ((count = Physics.OverlapBoxNonAlloc(center, new Vector3(halfWidth, 0.9f, 1.6f), trafficHits,
                   Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) == trafficHits.Length)
            System.Array.Resize(ref trafficHits, trafficHits.Length * 2);
        for (int i = 0; i < count; i++)
        {
            var c = trafficHits[i];
            if (c.transform == v || c.transform.IsChildOf(v)) continue;
            if (c.GetComponentInParent<CharacterNPC>() != null || c.GetComponentInParent<PriyaRoomPlayer>() != null || c.name == "Cow") return true;
            if (c.attachedRigidbody != null) return true;                       // another vehicle or the hand cart
        }
        return false;
    }

    /// <summary>People walking near a moving or parked vehicle step out of its way, like real pedestrians do.</summary>
    Transform cartT;

    void AvoidVehicles()
    {
        Vector3 pp = player.transform.position;
        foreach (var n in crowd)
        {
            if (n == null || !n.IsPhysical || !n.IsWalking) continue;
            Vector3 np = n.transform.position;
            foreach (var v in vehicles) AvoidOne(n, np, v);
            if (cartT != null && n.name != "CartMan") AvoidOne(n, np, cartT);     // the hand cart is dragged, not driven: walk round it
            AvoidOne(n, np, world.Suv);
            AvoidOne(n, np, world.Auto);
            AvoidPlayer(n, np, pp);
        }
    }

    /// <summary>
    /// Walkers step round Arjun as they would round anyone standing at a stall. They used to walk into him, and his
    /// controller's own collision resolution shoved him down the lane: 12 m during Vikram's conversation on the route,
    /// which carried the camera off Vikram's face.
    /// </summary>
    static void AvoidPlayer(CharacterNPC n, Vector3 np, Vector3 pp)
    {
        float dz = np.z - pp.z;
        if (dz < -2.4f || dz > 2.4f || Mathf.Abs(np.x - pp.x) > 0.85f) return;
        float side = np.x >= pp.x ? 1f : -1f;
        float target = Mathf.Clamp(pp.x + side * 1.1f, -2.3f, 2.3f);
        if (Mathf.Abs(target - pp.x) < 0.8f) target = Mathf.Clamp(pp.x - side * 1.1f, -2.3f, 2.3f);
        n.NudgeX(target, 1.6f);
    }

    // While a conversation, the phone call or the SUV choice holds Arjun, he stays exactly where he stood.
    Vector3 heldAt; bool held;

    void LateUpdate()
    {
        if (loading || player == null) return;
        bool hold = PriyaRoomPlayer.Locked && (phase == Phase.Dialogue || phase == Phase.Hook || phase == Phase.Choice);
        if (!hold) { held = false; return; }
        var p = player.transform.position;
        if (!held)
        {
            held = true; heldAt = p;
            if (p.y > 0.3f)        // already lifted onto someone when the conversation began: back down to the street
                foreach (var hit in Physics.RaycastAll(p + Vector3.up * 0.5f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.collider.GetComponentInParent<CharacterNPC>() == null && hit.collider.GetComponentInParent<PriyaRoomPlayer>() == null && hit.point.y < heldAt.y)
                        heldAt.y = hit.point.y;
        }
        if ((p - heldAt).sqrMagnitude < 1e-6f) return;
        // a walker pressing into him could also lift him (1.4 m up on one run, his eyes over Vikram's head)
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = heldAt;
        cc.enabled = true;
    }

    static void AvoidOne(CharacterNPC n, Vector3 np, Transform v)
    {
        if (v == null || !v.gameObject.activeInHierarchy) return;
        Vector3 vp = v.position;
        float dz = np.z - vp.z;
        if (dz > -6.5f && dz < 6.5f && Mathf.Abs(np.x - vp.x) < 1.5f)
        {
            float side = np.x >= vp.x ? 1f : -1f;
            n.NudgeX(Mathf.Clamp(vp.x + side * 1.9f, -2.3f, 2.3f), 2.4f);
        }
    }

    /// <summary>A man hauls a cart of vegetables up and down the lane.</summary>
    IEnumerator CartLoop()
    {
        var puller = AddCrowd(new Vector3(-1.7f, 0, 10f), 0f, NpcStyle.Crowd(777), "CartMan", false);
        var cart = world.SpawnHandCart(new Vector3(-1.7f, 0, 8.3f));
        cartT = cart;
        // the cart is moved by hand every frame; it must not shove its own puller sideways when he turns
        var pullerCol = puller.GetComponent<Collider>();
        if (pullerCol != null) foreach (var cc in cart.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(cc, pullerCol);
        StartCoroutine(CartFollow(puller, cart));
        bool up = true;
        while (puller != null)
        {
            yield return puller.WalkTo(new Vector3(-1.7f, 0, up ? 66f : 10f), 0.85f, -1f, false);
            up = !up;
            yield return new WaitForSeconds(2.5f);
        }
    }

    IEnumerator CartFollow(CharacterNPC n, Transform cart)
    {
        while (n != null && cart != null)
        {
            Vector3 p = n.transform.position - n.transform.forward * 1.7f; p.y = 0;
            cart.position = p;
            cart.rotation = Quaternion.Euler(0, n.transform.eulerAngles.y, 0);
            yield return null;
        }
    }

    /// <summary>Hawkers shout their wares from the stalls, shoppers haggle.</summary>
    IEnumerator CryLoop()
    {
        yield return new WaitForSeconds(7f);
        while (true)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(5f, 10f));
            if (phase == Phase.Dialogue || phase == Phase.Hook || phase == Phase.Choice || phase == Phase.Outcome || phase == Phase.Ending) continue;
            if (world.Vendors.Count == 0 || cries == null || cries.Length == 0) continue;
            var spot = world.Vendors[UnityEngine.Random.Range(0, world.Vendors.Count)];
            var clip = cries[UnityEngine.Random.Range(0, cries.Length)];
            if (clip == null) continue;
            vendorSrc.transform.position = spot.pos + Vector3.up * 1.5f;
            vendorSrc.PlayOneShot(clip, 0.85f);
        }
    }

    /// <summary>Stall-keepers and shoppers gesture and wave now and then.</summary>
    IEnumerator GestureLoop()
    {
        yield return new WaitForSeconds(5f);
        while (true)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(2f, 4f));
            if (crowd.Count == 0) continue;
            var n = crowd[UnityEngine.Random.Range(0, crowd.Count)];
            if (n == null || n.IsWalking || n.IsTalking) continue;
            n.PlayOnce(UnityEngine.Random.value < 0.6f ? "Interact" : "Wave");
        }
    }

    // ===================================================================== AUDIO
    const int SR = 44100;

    void SetupAudio()
    {
        var go = new GameObject("MarketAudio");
        go.transform.SetParent(transform, false);
        ambience = go.AddComponent<AudioSource>();
        ambience.loop = true; ambience.spatialBlend = 0f; ambience.volume = 0f;
        ambience.clip = Resources.Load<AudioClip>("market_ambience") ?? Resources.Load<AudioClip>("Audio/Market/evening_lane") ?? MakeMarketAmbience();
        ambience.Play();
        sfx = go.AddComponent<AudioSource>();
        voice = go.AddComponent<AudioSource>(); voice.spatialBlend = 0f;

        suvAudio = world.Suv.gameObject.AddComponent<AudioSource>();
        engineClip = MakeEngine();
        suvAudio.clip = engineClip; suvAudio.loop = true; suvAudio.spatialBlend = 1f;
        suvAudio.minDistance = 4f; suvAudio.maxDistance = 45f; suvAudio.volume = 0f; suvAudio.playOnAwake = false;

        clickClip = MakeClick(); dingClip = MakeDing(); hornClip = MakeHorn(); ringClip = MakeRing();

        // hawkers' calls (3D, from the stalls) and distant tabla + tanpura music drifting from a shop near the gate
        var vgo = new GameObject("VendorVoice");
        vgo.transform.SetParent(transform, false);
        vendorSrc = vgo.AddComponent<AudioSource>();
        vendorSrc.spatialBlend = 1f; vendorSrc.minDistance = 3f; vendorSrc.maxDistance = 28f; vendorSrc.rolloffMode = AudioRolloffMode.Linear;
        cries = new AudioClip[12];
        for (int i = 0; i < cries.Length; i++) cries[i] = VO("cry_" + (i + 1));
        // the music comes out of the transistor radio on the recharge stall's counter (it used to float 3 m up in the
        // lane, mostly unpanned): fully positional, falling off like a small speaker, no Doppler
        var mgo = new GameObject("StallRadio");
        mgo.transform.SetParent(transform, false);
        mgo.transform.position = world.RadioPos;
        musicSrc = mgo.AddComponent<AudioSource>();
        musicSrc.clip = Resources.Load<AudioClip>("Audio/Market/stall_radio") ?? MakeMusic(); musicSrc.loop = true; musicSrc.spatialBlend = 1f;
        musicSrc.minDistance = 1.4f; musicSrc.maxDistance = 30f; musicSrc.rolloffMode = AudioRolloffMode.Logarithmic; musicSrc.dopplerLevel = 0f;
        musicSrc.spread = 40f; musicSrc.volume = 0.3f;
        musicSrc.Play();
        phone = SpatialSource(world.transform, "StallPhone", world.VikramPos + Vector3.up * 1.1f, 14f);
    }

    AudioClip MakeMarketAmbience()
    {
        int n = SR * 8, tail = 4000;
        float[] g = new float[n + tail];
        var r = new System.Random(11);
        float a1 = 0, a2 = 0, b1 = 0, b2 = 0, hp = 0;
        for (int i = 0; i < g.Length; i++)
        {
            float w = (float)(r.NextDouble() * 2 - 1);
            a1 += (w - a1) * 0.08f; a2 += (a1 - a2) * 0.08f;           // low-passed
            b1 += (w - b1) * 0.02f; b2 += (b1 - b2) * 0.02f;
            float murmur = (a2 - b2) * 9f;                              // band-limited "crowd"
            hp += (w - hp) * 0.4f;
            float sizzle = (w - hp) * 0.18f;                            // frying hiss
            float t = i / (float)SR;
            float mod = 0.65f + 0.2f * Mathf.Sin(2f * Mathf.PI * 3f * t / 8f * 8f / 8f) + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.375f * t);
            float bass = b2 * 6f;
            g[i] = murmur * mod + sizzle * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 0.25f * t)) + bass * 0.5f;
        }
        float[] d = new float[n];
        for (int k = 0; k < n; k++)
        {
            if (k < tail) { float a = k / (float)tail; d[k] = g[k] * a + g[n + k] * (1f - a); }
            else d[k] = g[k];
        }
        Norm(d, 0.5f);
        var c = AudioClip.Create("market", n, 1, SR, false);
        c.SetData(d, 0);
        return c;
    }

    /// <summary>8 s loop: tanpura drone (Sa-Pa-Sa-Sa) over a tabla groove, softened to sound like a shop radio down the lane.</summary>
    AudioClip MakeMusic()
    {
        int n = SR * 8;
        var d = new float[n];
        float[] f = { 196f, 261.6f, 261.6f, 130.8f };
        for (int rep = 0; rep < 4; rep++)
            for (int k = 0; k < 4; k++)
            {
                int start = Mathf.RoundToInt((rep * 2f + k * 0.5f) * SR);
                for (int i = 0; i < SR * 2 && start + i < n; i++)
                {
                    float t = i / (float)SR, env = Mathf.Exp(-t * 1.3f) * Mathf.Clamp01(t * 40f);
                    float sum = 0;
                    for (int h = 1; h <= 6; h++) sum += Mathf.Sin(2f * Mathf.PI * f[k] * h * t) / h;
                    d[start + i] += sum * env * (1f + 0.25f * Mathf.Sin(2f * Mathf.PI * 5.5f * t)) * 0.16f;
                }
            }
        var r = new System.Random(8);
        for (int b = 0; b < 12; b++)                      // 12 beats of 2/3 s = 8 s
        {
            float t0 = b * (8f / 12f);
            bool bayan = b % 4 == 0 || b % 4 == 2;
            for (int half = 0; half < 2; half++)
            {
                int start = Mathf.RoundToInt((t0 + half * (4f / 12f)) * SR);
                bool dayanHit = half == 0 || b % 3 == 1;
                for (int i = 0; i < SR / 3 && start + i < n; i++)
                {
                    float t = i / (float)SR, v = 0;
                    if (half == 0 && bayan) v += Mathf.Sin(2f * Mathf.PI * (68f + 70f * Mathf.Exp(-t * 18f)) * t) * Mathf.Exp(-t * 8f) * 0.7f;
                    if (dayanHit)
                    {
                        v += Mathf.Sin(2f * Mathf.PI * 330f * t) * Mathf.Exp(-t * 20f) * 0.4f;
                        v += Mathf.Sin(2f * Mathf.PI * 660f * t) * Mathf.Exp(-t * 28f) * 0.18f;
                        v += (float)(r.NextDouble() * 2 - 1) * Mathf.Exp(-t * 70f) * 0.2f;
                    }
                    d[start + i] += v;
                }
            }
        }
        float lp = 0;                                      // soften: it is meant to be far away
        for (int i = 0; i < n; i++) { lp += (d[i] - lp) * 0.35f; d[i] = lp; }
        Norm(d, 0.5f);
        var c = AudioClip.Create("music", n, 1, SR, false);
        c.SetData(d, 0);
        return c;
    }

    AudioClip MakeEngine()
    {
        int n = SR * 2;
        float[] d = new float[n];
        var r = new System.Random(3);
        float lp = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float saw = ((t * 46f) % 1f) * 2f - 1f;               // 46 Hz: exactly 92 cycles in 2 s -> seamless
            lp += ((float)(r.NextDouble() * 2 - 1) - lp) * 0.03f;
            d[i] = saw * 0.5f + Mathf.Sin(2f * Mathf.PI * 92f * t) * 0.25f + lp * 2f;
        }
        Norm(d, 0.5f);
        var c = AudioClip.Create("engine", n, 1, SR, false);
        c.SetData(d, 0);
        return c;
    }

    AudioClip MakeHorn()
    {
        int n = SR * 4 / 10;
        float[] d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float s = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 410f * t)) * 0.5f + Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 520f * t)) * 0.4f;
            d[i] = s * Mathf.Clamp01(t * 60f) * Mathf.Clamp01((0.4f - t) * 40f) * 0.4f;
        }
        var c = AudioClip.Create("horn", n, 1, SR, false);
        c.SetData(d, 0);
        return c;
    }

    AudioClip MakeRing()
    {
        int n = SR * 22 / 10;
        float[] d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float burst = (t % 1.1f) < 0.7f ? 1f : 0f;                // two rings
            float trill = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 22f * t)) * 0.5f + 0.5f;
            d[i] = (Mathf.Sin(2f * Mathf.PI * 880f * t) + Mathf.Sin(2f * Mathf.PI * 1175f * t)) * 0.25f * burst * trill;
        }
        var c = AudioClip.Create("ring", n, 1, SR, false);
        c.SetData(d, 0);
        return c;
    }

    AudioClip MakeClick()
    {
        int n = SR / 10;
        float[] d = new float[n];
        for (int i = 0; i < n; i++) { float t = i / (float)SR; d[i] = Mathf.Sin(2f * Mathf.PI * 880f * t) * Mathf.Exp(-t * 55f) * 0.5f; }
        var c = AudioClip.Create("click", n, 1, SR, false);
        c.SetData(d, 0);
        return c;
    }

    AudioClip MakeDing()
    {
        int n = SR * 6 / 10;
        float[] d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float a = Mathf.Sin(2f * Mathf.PI * 784f * t) * Mathf.Exp(-t * 7f);
            float b = t > 0.09f ? Mathf.Sin(2f * Mathf.PI * 1175f * (t - 0.09f)) * Mathf.Exp(-(t - 0.09f) * 6f) : 0f;
            d[i] = (a + b) * 0.35f;
        }
        var c = AudioClip.Create("ding", n, 1, SR, false);
        c.SetData(d, 0);
        return c;
    }

    static void Norm(float[] d, float peak)
    {
        float m = 0.0001f;
        for (int i = 0; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
        float k = peak / m;
        for (int i = 0; i < d.Length; i++) d[i] *= k;
    }

    // ==================================================================== POLISH
    void SetupPolish()
    {
        CinematicLook.Apply(transform, camComp, LookPreset.Market);
    }

    // ======================================================================== UI
    void InitStyles()
    {
        if (sSub != null) return;
        sSub = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        sSub.normal.textColor = new Color(1f, 0.95f, 0.85f);
        sTitle = new GUIStyle(GUI.skin.label) { fontSize = 48, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        sTitle.normal.textColor = new Color(1f, 0.82f, 0.4f);
        sTitle2 = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        sTitle2.normal.textColor = new Color(0.9f, 0.85f, 0.75f);
        sSmall = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.UpperLeft, wordWrap = true };
        sSmall.normal.textColor = new Color(1f, 0.9f, 0.7f);
        sPanel = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.UpperLeft, wordWrap = true };
        sPanel.normal.textColor = Color.white;
        sHead = new GUIStyle(sPanel) { fontStyle = FontStyle.Bold, fontSize = 17 };
        sHead.normal.textColor = new Color(1f, 0.82f, 0.4f);
        sPrompt = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        sPrompt.normal.textColor = new Color(1f, 0.85f, 0.4f);
        UiKit.Skin(sSub, sTitle, sTitle2, sSmall, sPanel, sHead, sPrompt);
        sBtn = UiKit.ButtonStyle();
    }

    static void Panel(Rect r, float alpha) { UiKit.Card(r, alpha); }

    static void Shadowed(Rect r, string t, GUIStyle s) { UiKit.Text(r, t, s); }

    void OnGUI()
    {
        GUI.depth = -10;
        InitStyles();
        float s = Mathf.Max(0.7f, Screen.height / 900f);
        var savedMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
        float W = Screen.width / s, H = Screen.height / s;
        if (loading)
        {
            UiKit.Fill(new Rect(0, 0, W, H), UiKit.Ink);
            GUI.Label(new Rect(0, H * 0.45f, W, 55), "Chandnipur Market District", sTitle2);
            GUI.matrix = savedMatrix;
            return;
        }
        UiKit.Letterbox(W, H, !paused && (phase == Phase.Dialogue || phase == Phase.Choice || compass.IsOpen));
        if (phase == Phase.Free && !paused) UiKit.Crosshair(W, H);

        if (phase == Phase.Free && objectiveTarget.HasValue && !paused) DrawTargetMarker(s);

        if (!string.IsNullOrEmpty(prompt) && !paused)
            UiKit.Prompt(W, H, prompt);

        if (!string.IsNullOrEmpty(objective) && !paused && phase == Phase.Free)
        {
            var r = new Rect(20, 18, Mathf.Min(620, W * 0.55f), 62);
            Panel(r, 0.5f);
            GUI.Label(new Rect(r.x + 12, r.y + 6, r.width - 24, 20), UiKit.Track("Objective"), sHead);
            GUI.Label(new Rect(r.x + 12, r.y + 27, r.width - 24, 34), objective, sSmall);
        }

        if (phase == Phase.Free && !paused)
        {
            string hint = "E Talk / Interact   |   J Evidence notes   |   Esc Pause";
            Shadowed(new Rect(0, H - 30, W - 20, 24), hint, new GUIStyle(sSmall) { alignment = TextAnchor.LowerRight, fontSize = 14 });
        }

        if (!paused && !string.IsNullOrEmpty(trust) && phase == Phase.Dialogue && vikramTalking)
            DrawTrustMeter(W);

        if (Time.time < toastUntil && !string.IsNullOrEmpty(toast) && !paused)
        {
            float tw = toastBig ? 640 : 600;
            var r = new Rect(W / 2 - tw / 2, toastBig ? H * 0.2f : 96, tw, toastBig ? 56 : 40);
            Panel(r, 0.7f);
            GUI.Label(new Rect(r.x, r.y + (toastBig ? 10 : 6), r.width, 40), toast, new GUIStyle(sPrompt) { fontSize = toastBig ? 24 : 18 });
        }

        if (Time.time < subUntil && !string.IsNullOrEmpty(sub) && !paused && string.IsNullOrEmpty(dlgText))
        {
            // narration speaks through the same phrase subtitles as the conversations
            int cut = sub.IndexOf(":  ");
            string who = cut > 0 ? sub.Substring(0, cut) : null, what = cut > 0 ? sub.Substring(cut + 3) : sub;
            narration.Speak(W, H, who, what, voice != null && voice.isPlaying ? voice : null);
        }

        // an open decision draws even when the line before it has cleared (the SUV's timed dial draws in DrawChoice)
        if (!choiceActive && (compass.IsOpen || !string.IsNullOrEmpty(dlgText)) && !paused) DrawDialogue(W, H);
        if (!paused) compass.Afterglow(W, H);
        if (phase == Phase.Intro && !paused && fade < 0.9f)
            Shadowed(new Rect(0, H - 44, W, 26), "Enter: skip intro", new GUIStyle(sSmall) { alignment = TextAnchor.UpperCenter, fontSize = 15 });
        if (choiceActive && !paused) DrawChoice(W, H);
        if (Time.time < plateCardUntil && !paused) DrawPlateCard(W, H);

        if (fade > 0.001f)
        {
            var old = GUI.color; GUI.color = new Color(0, 0, 0, Mathf.Clamp01(fade));
            GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = old;
        }

        if (Time.time < titleEnd && !string.IsNullOrEmpty(titleA))
        {
            float a = Mathf.Clamp01(Mathf.Min((Time.time - titleStart) / 1.2f, (titleEnd - Time.time) / 1.2f));
            var old = GUI.color; GUI.color = new Color(1, 1, 1, a);
            GUI.Label(new Rect(0, H * 0.38f, W, 70), titleA, sTitle);
            GUI.Label(new Rect(0, H * 0.38f + 76, W, 36), titleB, sTitle2);
            GUI.color = old;
        }

        if (showEnd)
        {
            Shadowed(new Rect(0, H * 0.34f, W, 70), "END OF DEMO", sTitle);
            Shadowed(new Rect(0, H * 0.34f + 76, W, 36), "Thanks for playing, " + SaveSystem.Profile.playerName + ".   Acts I and II complete.   Press R for the main menu", sTitle2);
        }

        if (journalOpen && !paused && phase != Phase.Ending) DrawJournal(W, H);
        if (paused) DrawPause(W, H);

        GUI.matrix = savedMatrix;
    }

    void DrawTargetMarker(float s)
    {
        Vector3 sp = camComp.WorldToScreenPoint(objectiveTarget.Value);
        float dist = Vector3.Distance(cam.position, objectiveTarget.Value);
        float W = Screen.width / s, H = Screen.height / s;
        if (sp.z <= 0.1f)
        {
            Shadowed(new Rect(0, H * 0.3f, W, 28), "Turn around: " + objectiveTargetName, new GUIStyle(sPrompt) { fontSize = 18 });
            return;
        }
        float x = Mathf.Clamp(sp.x / s, 40, W - 40), y = Mathf.Clamp((Screen.height - sp.y) / s, 60, H - 80);
        var m = GUI.matrix;
        GUIUtility.RotateAroundPivot(45f, new Vector2(x, y));
        var old = GUI.color; GUI.color = new Color(1f, 0.8f, 0.2f, 0.95f);
        GUI.DrawTexture(new Rect(x - 7, y - 7, 14, 14), Texture2D.whiteTexture);
        GUI.color = old;
        GUI.matrix = m;
        Shadowed(new Rect(x - 130, y - 38, 260, 24), objectiveTargetName + "   " + dist.ToString("0") + " m", new GUIStyle(sSmall) { alignment = TextAnchor.UpperCenter, fontSize = 15 });
    }

    void DrawTrustMeter(float W)
    {
        float v = trust == "ANGRY" ? 0.1f : trust == "COLD" ? 0.25f : trust == "WARMING" ? 0.6f : 1f;
        var r = new Rect(W / 2 - 160, 20, 320, 54);
        Panel(r, 0.65f);
        GUI.Label(new Rect(r.x + 12, r.y + 4, r.width - 24, 22), "VIKRAM'S TRUST:  " + trust, sHead);
        var old = GUI.color;
        GUI.color = new Color(0.25f, 0.25f, 0.25f, 1f);
        GUI.DrawTexture(new Rect(r.x + 12, r.y + 32, r.width - 24, 10), Texture2D.whiteTexture);
        GUI.color = trust == "ANGRY" ? new Color(0.9f, 0.25f, 0.2f) : new Color(1f, 0.8f, 0.2f);
        GUI.DrawTexture(new Rect(r.x + 12, r.y + 32, (r.width - 24) * v, 10), Texture2D.whiteTexture);
        GUI.color = old;
    }

    void DrawDialogue(float W, float H)
    {
        if (compass.IsOpen) compass.Draw(W, H, dlgSpeaker);
        else if (dlgOptions == null) compass.Speak(W, H, dlgSpeaker, dlgText, dialogueSource);
    }

    /// <summary>A big, readable licence plate while the camera is zoomed on the SUV.</summary>
    void DrawPlateCard(float W, float H)
    {
        var r = new Rect(W / 2 - 230, H * 0.62f, 460, 120);
        Panel(r, 0.8f);
        GUI.Label(new Rect(r.x, r.y + 8, r.width, 24), "BLACK SUV  -  LICENCE PLATE", new GUIStyle(sHead) { alignment = TextAnchor.UpperCenter });
        var plate = new Rect(r.x + 50, r.y + 38, r.width - 100, 64);
        var old = GUI.color; GUI.color = new Color(0.97f, 0.9f, 0.55f);
        GUI.DrawTexture(plate, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, GUI.color, Vector4.zero, new Vector4(8, 8, 8, 8));
        GUI.color = old;
        var ps = new GUIStyle(sTitle) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
        ps.normal.textColor = new Color(0.08f, 0.08f, 0.1f);
        GUI.Label(plate, "MH-12-AB-4471", ps);
    }

    void DrawChoice(float W, float H)
    {
        compass.Draw(W, H, "THE BLACK SUV", Mathf.Clamp01((choiceDeadline - Time.time) / 15f));
    }

    void DrawJournal(float W, float H)
    {
        var notes = new List<UiKit.Note>();
        for (int i = evidence.Count - 1; i >= 0; i--)
            notes.Add(new UiKit.Note("Evidence " + (i + 1).ToString("00"), evidence[i]));
        UiKit.CaseFile(W, H, "The Streets Know", "Act II\nEvidence notes  " + evidence.Count, notes,
            "Nothing yet. Talk to people and look for clues in the market.");
    }

    void DrawPause(float W, float H)
    {
        Panel(new Rect(0, 0, W, H), 0.7f);
        GUI.Label(new Rect(0, H * 0.2f, W, 60), UiKit.Track("Paused"), sTitle);
        if (GUI.Button(new Rect(W / 2 - 130, H * 0.36f, 260, 46), "Resume", sBtn)) SetPaused(false);
        if (GUI.Button(new Rect(W / 2 - 130, H * 0.36f + 58, 260, 46), "Quit to main menu", sBtn))
        {
            SetPaused(false);
            builder.Build();
            return;
        }
        var r = new Rect(W / 2 - 330, H * 0.36f + 128, 660, 150);
        Panel(r, 0.4f);
        GUI.Label(new Rect(r.x + 20, r.y + 10, r.width - 40, 100),
            "W A S D walk  |  Mouse look  |  Shift walk faster\nE talk / interact  |  1 2 3 4 choose answers  |  Enter skip conversation\nJ evidence notes  |  Esc resume\nF2 graphics quality (Cinematic / Balanced / Performance)", sPanel);
    }
}
