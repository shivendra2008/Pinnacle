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
    AudioSource ambience, sfx, voice, suvAudio, vendorSrc, musicSrc;
    AudioClip[] cries;
    AudioClip clickClip, dingClip, hornClip, ringClip, engineClip;
    float nextHorn;

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
    string dlgSpeaker, dlgText; string[] dlgOptions; bool[] dlgDisabled;
    bool paused, savedLocked, journalOpen, showEnd, skipAll;
    // timed choice
    float plateCardUntil;
    bool choiceActive; float choiceDeadline; string[] choiceOptions; int choiceResult = -1;
    int menuResult = -1;
    GUIStyle sSub, sTitle, sTitle2, sSmall, sPanel, sPrompt, sHead, sBtn;

    // ====================================================================
    void Start()
    {
        cam = player.cam;
        camComp = cam.GetComponent<Camera>();
        PriyaRoomPlayer.Locked = true;
        PriyaRoomPlayer.Surface = 1;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;   // the menu left the cursor free

        world = gameObject.AddComponent<MarketWorld>();
        world.Generate();
        SetupEnvironment();
        SetupAudio();
        SetupPolish();

        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = world.PlayerStart;
        cc.enabled = true;
        player.SetLook(0f, 0f);

        SpawnPeople();
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
            moon = go.AddComponent<Light>();
            moon.type = LightType.Directional;
        }
        moon.color = new Color(0.62f, 0.68f, 1f);
        moon.intensity = 1.1f;
        moon.transform.rotation = Quaternion.Euler(48f, 25f, 0f);
        CinematicLook.Shadows(moon, 1f, 0.04f, 0.8f);
        CinematicLook.Ambient(new Color(0.36f, 0.38f, 0.58f), new Color(0.30f, 0.28f, 0.40f), new Color(0.14f, 0.12f, 0.14f), 1f);
        CinematicLook.Probe(transform, new Vector3(0f, 4f, 40f), new Vector3(12f, 12f, 92f), new Color(0.07f, 0.08f, 0.14f), 128);
    }

    void SpawnPeople()
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
            n++;
        }
        n = 0;
        foreach (var c in world.Customers)
        {
            var st = n % 3 == 0 ? NpcStyle.CrowdWoman(400 + n) : (n % 5 == 4 ? NpcStyle.Child(400 + n) : NpcStyle.Crowd(400 + n));
            AddCrowd(c.pos, c.yaw, st, "Customer" + n, true);
            n++;
        }
        // people walking up and down the lane: men, women in saris, couples, and children running about
        float[] xs = { -1.7f, -0.9f, 0.2f, 1.1f, -1.3f, 0.6f, 1.7f, -0.3f, -1.9f, 0.9f, -0.6f, 1.4f, -1.1f, 0.0f, 1.9f, -1.5f };
        float[] z0 = { 6f, 14f, 26f, 44f, 58f, 70f, 36f, 20f, 50f, 62f, 12f, 30f, 66f, 40f, 54f, 24f };
        for (int i = 0; i < xs.Length; i++)
        {
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
            yield return n.WalkTo(new Vector3(x, 0, zTarget), speed);
            zTarget = zTarget > zStart ? Mathf.Clamp(zStart - Mathf.Abs(dz), lo, hi) : Mathf.Clamp(zStart + Mathf.Abs(dz), lo, hi);
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.5f, 2.5f));
        }
    }

    // ==================================================================== UPDATE
    void Update()
    {
        var kb = Keyboard.current;

        if (kb != null && kb.escapeKey.wasPressedThisFrame && phase != Phase.Ending) SetPaused(!paused);
        if (paused) return;

        if (kb != null && kb.jKey.wasPressedThisFrame && phase != Phase.Intro) journalOpen = !journalOpen;
        // Enter = skip the whole conversation / cutscene (choices and the timed SUV decision still wait for you)
        if (kb != null && kb.enterKey.wasPressedThisFrame && dlgOptions == null && !choiceActive
            && (phase == Phase.Intro || phase == Phase.Dialogue || phase == Phase.Hook))
            skipAll = true;
        if (phase == Phase.Free || phase == Phase.Choice || phase == Phase.Outcome || phase == Phase.Ending) skipAll = false;

        fade = Mathf.MoveTowards(fade, fadeTarget, fadeSpeed * Time.deltaTime);

        if (Time.time > nextHorn && phase != Phase.Hook && phase != Phase.Choice && phase != Phase.Outcome && phase != Phase.Ending)
        {
            sfx.PlayOneShot(hornClip, UnityEngine.Random.Range(0.12f, 0.3f));
            nextHorn = Time.time + UnityEngine.Random.Range(9f, 20f);
        }

        if (phase == Phase.Ending)
        {
            if (showEnd && kb != null && kb.rKey.wasPressedThisFrame) builder.Build();
            return;
        }

        // timed choice input
        if (choiceActive)
        {
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) choiceResult = 0;
                else if (kb.digit2Key.wasPressedThisFrame) choiceResult = 1;
                else if (kb.digit3Key.wasPressedThisFrame) choiceResult = 2;
            }
        }

        UpdateInteraction();
        UpdateStoryFlow();
        AvoidVehicles();
    }

    void SetPaused(bool p)
    {
        paused = p;
        if (p)
        {
            savedLocked = PriyaRoomPlayer.Locked;
            PriyaRoomPlayer.Locked = true;
            Time.timeScale = 0f; AudioListener.pause = true;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        else
        {
            PriyaRoomPlayer.Locked = savedLocked;
            Time.timeScale = 1f; AudioListener.pause = false;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
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
        if (vikramDone && reddyResolved && cardHandled && !hookStarted && Vector3.Distance(player.transform.position, world.VikramStallFront) < 11f)
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

        var kb = Keyboard.current;
        if (act != null && kb != null && kb.eKey.wasPressedThisFrame)
        {
            sfx.PlayOneShot(clickClip, 0.6f);
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
        Toast("Evidence added to your notes (J)", 3f);
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
        float dur = clip != null ? clip.length + 0.4f : Mathf.Max(2.5f, text.Length * 0.06f);
        if (clip != null)
        {
            if (npc != null) npc.Say(clip);
            else { voice.Stop(); voice.clip = clip; voice.Play(); }
        }
        yield return null;
        float t = 0;
        while (t < dur)
        {
            var kb = Keyboard.current;
            if (skipAll || (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)))
            {
                voice.Stop(); if (npc != null) npc.Silence();
                break;
            }
            t += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>Shows numbered options under the current line and waits for 1-4.</summary>
    IEnumerator Menu(string[] options, bool[] disabled)
    {
        dlgOptions = options; dlgDisabled = disabled;
        skipAll = false;
        menuResult = -1;
        yield return null;
        while (menuResult < 0)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                int c = -1;
                if (kb.digit1Key.wasPressedThisFrame) c = 0;
                else if (kb.digit2Key.wasPressedThisFrame) c = 1;
                else if (kb.digit3Key.wasPressedThisFrame) c = 2;
                else if (kb.digit4Key.wasPressedThisFrame) c = 3;
                if (c >= 0 && c < options.Length && (disabled == null || !disabled[c])) { menuResult = c; sfx.PlayOneShot(clickClip, 0.5f); }
            }
            yield return null;
        }
        dlgOptions = null; dlgDisabled = null;
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
            Toast("Welcome back, " + SaveSystem.Data.playerName + ".   Progress restored.", 6f);
            yield break;
        }
        phase = Phase.Intro;
        fade = 1f; fadeTarget = 1f;
        StartCoroutine(RampAmbience(0f, 0.9f, 4f));
        yield return W(0.6f);
        ShowTitle("ACT II  -  THE STREETS KNOW", "Chandnipur Market District   |   7:30 PM", 4.2f);
        yield return W(4.4f);
        fadeSpeed = 0.4f; fadeTarget = 0f;
        var intro = VO("arjun_act2_intro");
        sub = "ARJUN (V.O.):  Night falls on Chandnipur, and the city changes its face. Neon, smoke, and a thousand strangers. Every shadow could be a threat now.";
        subUntil = Time.time + (intro != null ? intro.length : 7f) + 0.5f;
        if (intro != null) { voice.clip = intro; voice.Play(); }
        yield return W((intro != null ? intro.length : 7f) + 0.4f);
        if (skipAll) { voice.Stop(); fade = 0f; fadeTarget = 0f; titleEnd = 0f; skipAll = false; }
        sub = null;
        PriyaRoomPlayer.Locked = false;
        phase = Phase.Free;
        Toast("Look at Bhaskar and press E to talk. Or head straight into the market.", 7f);
    }

    IEnumerator RampAmbience(float from, float to, float dur)
    {
        float t = 0;
        while (t < dur) { t += Time.deltaTime; ambience.volume = Mathf.Lerp(from, to, t / dur); yield return null; }
        ambience.volume = to;
    }

    // =================================================================== BHASKAR
    IEnumerator BhaskarTalk()
    {
        phase = Phase.Dialogue; PriyaRoomPlayer.Locked = true; objective = "";
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
            yield return Menu(DecorateAsked(opts, new[] { asked[0], asked[1], asked[2], false }, askedA), dis);
            int c = menuResult;
            if (c == 3) break;
            asked[c] = true;
            if (c == 0)
            {
                yield return Line("ARJUN", opts[0], "q_b_a", null);
                yield return Line("BHASKAR", "Priya madam? Of course! She takes my auto sometimes. Going to the court, going to the slum. Good woman. Always pays full fare, never bargains. Why?", "b_a", bhaskar);
                yield return Line("ARJUN", "She's missing.", "q_b_a2", null);
                yield return Line("BHASKAR", "Missing? Arre bhai... actually, I drove her two nights ago. Late, around ten. She flagged me down near the temple. She was on the phone, very tense. She kept saying, don't come, I'll handle it. Then she told me to take her to the old mill, the abandoned textile mill near the river. But halfway there, she got another call, and her face went white. She said, take me back, right now. She gave me five hundred rupees for a fifty rupee ride, and said, forget this ride. You never saw me tonight.", "b_a2", bhaskar);
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

    static string[] DecorateAsked(string[] opts, bool[] asked, bool askedA)
    {
        var r = new string[opts.Length];
        for (int i = 0; i < opts.Length; i++)
        {
            r[i] = opts[i];
            if (asked[i]) r[i] += "   (asked)";
            else if ((i == 1 || i == 2) && !askedA) r[i] += "   (ask about Priya first)";
        }
        return r;
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
        phase = Phase.Dialogue; PriyaRoomPlayer.Locked = true; objective = "";
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
        yield return Menu(t1, null);
        int c1 = menuResult;
        bool goodTrust = c1 != 2;
        if (c1 == 0)
        {
            yield return Line("ARJUN", "This is your number, Vikram. Priya hid it in her apartment. She trusted you.", "q_v_1a", null);
            yield return Line("VIKRAM", "Where did you get that? ... Okay. Okay. Sit down. But keep your voice low.", "v_t1a", vikram);
        }
        else if (c1 == 1)
        {
            yield return Line("ARJUN", "Her mother is terrified, Vikram. She thinks something terrible has happened to her daughter. If you know anything, please.", "q_v_1b", null);
            yield return Line("VIKRAM", "Kamala-ji... she's a good woman. ... Alright. Sit. But if anyone asks, you're just drinking chai.", "v_t1b", vikram);
        }
        else
        {
            yield return Line("ARJUN", "I know you're lying. I can involve the police if I have to.", "q_v_1c", null);
            trust = "ANGRY";
            yield return Line("VIKRAM", "Then involve the police. I have nothing to say to you.", "v_t1c", vikram);
            yield return Line("VIKRAM", "Alright, alright. Priya came here once. She asked about the land records. That is all I know. Now please, go. I have customers.", "v_partial", vikram);
        }

        // ---------- Tier 2: warming
        if (goodTrust)
        {
            trust = "WARMING";
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
            for (int i = 0; i < 3; i++) { show[i] = q2[i] + (a2[i] ? "   (asked)" : ""); dis[i] = a2[i]; }
            show[3] = q2[3];
            yield return Menu(show, dis);
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
                yield return Line("VIKRAM", "Councilor Meghna Deshpande. She's the one behind the project. But it's not just her. There's a builder, Raj Malhotra, and people inside the police. Priya said the corruption goes all the way up. She was scared, but she wouldn't stop. That girl has a spine of steel.", "v_t2b", vikram);
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
            yield return Line("VIKRAM", "Wait. There's something else. Priya gave me this two days before she disappeared. She said, if anything happens to me, give this to someone who can use it. I didn't know what that meant until now. Take it. It has all her research. Copies of the forged documents, comparison with the originals, photographs, everything.", "v_t3a", vikram);
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

    // ===================================================================== REDDY
    void SpawnReddy()
    {
        reddy = CharacterNPC.Create(transform, world.ReddyPos, NpcStyle.Reddy(), "Inspector_Reddy");
        reddy.transform.rotation = Quaternion.Euler(0, -90f, 0);
        reddy.lookTarget = cam;
        Toast("A woman across the street is watching you...", 5f);
    }

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
        Vector3[] path = { new Vector3(3.7f, 0, 40.6f), new Vector3(5f, 0, 44f), new Vector3(8.5f, 0, 45f), new Vector3(12.2f, 0, 45f) };
        foreach (var p in path)
            yield return reddy.WalkTo(p, dropCard ? 3.2f : 1.4f);
        Destroy(reddy.gameObject); reddy = null;
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
        hookStarted = true;
        phase = Phase.Hook; PriyaRoomPlayer.Locked = true; objective = ""; objectiveTarget = null;
        if (idCard != null) { Destroy(idCard); }
        yield return Turn(vikram.transform.position + Vector3.up * 1.5f, 0.9f);

        // the phone rings
        sfx.PlayOneShot(ringClip, 0.7f);
        vikram.PlayOnce("Interact");
        yield return W(2.2f);
        yield return Line("VIKRAM", "They know you're here. Deshpande's people. They were watching the stall. You need to leave. Now.", "v_phone", vikram);
        dlgSpeaker = null; dlgText = null;

        // lights flicker, the SUV appears
        StartCoroutine(Flicker(3f));
        suvAudio.volume = 0f; suvAudio.Play();
        StartCoroutine(RampSuvAudio());
        yield return Turn(new Vector3(world.SuvStart.x, 1.5f, 70f), 1.2f);
        AutoFocus.FocusOn = world.Suv;
        world.Suv.gameObject.SetActive(true);
        float stopZ = 54f;
        yield return MoveSuv(stopZ, 6f, 2f);
        Toast("A black SUV is crawling down the lane. It's looking for someone.", 4f);
        yield return W(1.2f);

        // ----- timed choice (15 s)
        skipAll = false;
        phase = Phase.Choice;
        choiceOptions = new[]
        {
            "HIDE behind Vikram's counter",
            "WALK CALMLY into the crowd",
            "NOTE THE LICENSE PLATE",
        };
        choiceResult = -1; choiceActive = true; choiceDeadline = Time.time + 15f;
        PriyaRoomPlayer.Locked = true;
        while (choiceResult < 0 && Time.time < choiceDeadline) yield return null;
        choiceActive = false;
        if (choiceResult < 0) { choiceResult = 1; Toast("You froze... and walked on, head down.", 3f); }
        sfx.PlayOneShot(clickClip, 0.6f);

        skipAll = false;
        phase = Phase.Outcome;
        if (choiceResult == 0) yield return OutcomeHide();
        else if (choiceResult == 1) yield return OutcomeWalk();
        else yield return OutcomePlate();

        AutoFocus.FocusOn = null;
        // the SUV moves on
        StartCoroutine(SuvLeaves());
        yield return W(3.5f);

        // ----- end of Act II
        var end = VO("arjun_act2_end");
        sub = "ARJUN (V.O.):  The streets of Chandnipur know everything. The question is whether they'll tell you the truth before it's too late.";
        subUntil = Time.time + (end != null ? end.length : 7f) + 0.6f;
        if (end != null) { voice.Stop(); voice.clip = end; voice.Play(); }
        yield return W((end != null ? end.length : 7f) + 0.5f);
        fadeSpeed = 0.5f; fadeTarget = 1f;
        StartCoroutine(RampAmbience(ambience.volume, 0f, 3f));
        yield return W(2.8f);
        sub = null;
        phase = Phase.Ending;
        SaveSystem.CompleteAct(2);
        ActLoader.Load(3, builder, player, false);          // on to Act III
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

    IEnumerator Flicker(float dur)
    {
        float end = Time.time + dur;
        while (Time.time < end)
        {
            for (int i = 0; i < world.StreetLights.Count; i++)
                if (world.StreetLights[i] != null)
                    world.StreetLights[i].intensity = world.StreetLightBase[i] * (UnityEngine.Random.value < 0.35f ? 0.05f : 1f);
            yield return new WaitForSeconds(0.08f);
        }
        for (int i = 0; i < world.StreetLights.Count; i++)
            if (world.StreetLights[i] != null) world.StreetLights[i].intensity = world.StreetLightBase[i];
    }

    // ---- outcome A: hide
    IEnumerator OutcomeHide()
    {
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
    IEnumerator TrafficLoop()
    {
        yield return new WaitForSeconds(9f);
        while (true)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(11f, 20f));
            if (phase != Phase.Free || hookStarted) continue;
            bool auto = UnityEngine.Random.value < 0.5f;
            float x = UnityEngine.Random.Range(-0.1f, 0.2f);
            Transform v = auto ? world.SpawnAuto(new Vector3(x, 0, -5f), 0f)
                               : world.SpawnScooter(new Vector3(x, 0, -5f), 0f, Color.HSVToRGB(UnityEngine.Random.value, 0.7f, 0.8f));
            sfx.PlayOneShot(hornClip, 0.5f);
            float cur = 0f, top = auto ? 4.2f : 5.5f, honkAt = 0f;
            vehicles.Add(v);
            while (v != null && v.position.z < 80f && !hookStarted)
            {
                // brake for anyone (you, the crowd, the cow, another vehicle) in the way, and honk
                bool blocked = PathBlocked(v, 1f, auto ? 0.95f : 0.6f, auto ? 1.4f : 0.8f);
                cur = Mathf.MoveTowards(cur, blocked ? 0f : top, 6f * Time.deltaTime);
                v.position += new Vector3(0, 0, cur * Time.deltaTime);
                if (blocked && Time.time > honkAt) { sfx.PlayOneShot(hornClip, 0.7f); honkAt = Time.time + 1.4f; }
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
        var cols = Physics.OverlapBox(center, new Vector3(halfWidth, 0.9f, 1.6f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        foreach (var c in cols)
        {
            if (c.transform == v || c.transform.IsChildOf(v)) continue;
            if (c.GetComponentInParent<CharacterNPC>() != null || c.GetComponentInParent<PriyaRoomPlayer>() != null || c.name == "Cow") return true;
            if (c.attachedRigidbody != null) return true;                       // another vehicle or the hand cart
        }
        return false;
    }

    /// <summary>People walking near a moving or parked vehicle step out of its way, like real pedestrians do.</summary>
    void AvoidVehicles()
    {
        foreach (var n in crowd)
        {
            if (n == null || !n.IsPhysical || !n.IsWalking) continue;
            Vector3 np = n.transform.position;
            foreach (var v in vehicles) AvoidOne(n, np, v);
            AvoidOne(n, np, world.Suv);
            AvoidOne(n, np, world.Auto);
        }
    }

    static void AvoidOne(CharacterNPC n, Vector3 np, Transform v)
    {
        if (v == null) return;
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
        StartCoroutine(CartFollow(puller, cart));
        bool up = true;
        while (puller != null)
        {
            yield return puller.WalkTo(new Vector3(-1.7f, 0, up ? 66f : 10f), 0.85f);
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
        ambience.clip = Resources.Load<AudioClip>("market_ambience") ?? MakeMarketAmbience();
        ambience.Play();
        sfx = go.AddComponent<AudioSource>();
        voice = go.AddComponent<AudioSource>(); voice.spatialBlend = 0f;

        suvAudio = world.Suv.gameObject.AddComponent<AudioSource>();
        suvAudio.clip = MakeEngine(); suvAudio.loop = true; suvAudio.spatialBlend = 1f;
        suvAudio.minDistance = 4f; suvAudio.maxDistance = 45f; suvAudio.volume = 0f; suvAudio.playOnAwake = false;

        clickClip = MakeClick(); dingClip = MakeDing(); hornClip = MakeHorn(); ringClip = MakeRing();

        // hawkers' calls (3D, from the stalls) and distant tabla + tanpura music drifting from a shop near the gate
        var vgo = new GameObject("VendorVoice");
        vgo.transform.SetParent(transform, false);
        vendorSrc = vgo.AddComponent<AudioSource>();
        vendorSrc.spatialBlend = 1f; vendorSrc.minDistance = 3f; vendorSrc.maxDistance = 28f; vendorSrc.rolloffMode = AudioRolloffMode.Linear;
        cries = new AudioClip[12];
        for (int i = 0; i < cries.Length; i++) cries[i] = VO("cry_" + (i + 1));
        var mgo = new GameObject("StreetMusic");
        mgo.transform.SetParent(transform, false);
        mgo.transform.position = new Vector3(2.5f, 3f, 20f);
        musicSrc = mgo.AddComponent<AudioSource>();
        musicSrc.clip = MakeMusic(); musicSrc.loop = true; musicSrc.spatialBlend = 0.9f;
        musicSrc.minDistance = 4f; musicSrc.maxDistance = 30f; musicSrc.rolloffMode = AudioRolloffMode.Linear; musicSrc.volume = 0.45f;
        musicSrc.Play();
        nextHorn = Time.time + 6f;
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
        bool playing = phase == Phase.Free || phase == Phase.Dialogue || phase == Phase.Hook || phase == Phase.Choice || phase == Phase.Outcome;

        if (playing && !paused) UiKit.Crosshair(W, H);

        if (phase == Phase.Free && objectiveTarget.HasValue && !paused) DrawTargetMarker(s);

        if (!string.IsNullOrEmpty(prompt) && !paused)
            UiKit.Prompt(W, H, prompt);

        if (!string.IsNullOrEmpty(objective) && !paused && phase == Phase.Free)
        {
            var r = new Rect(20, 18, Mathf.Min(620, W * 0.55f), 62);
            Panel(r, 0.5f);
            GUI.Label(new Rect(r.x + 12, r.y + 6, r.width - 24, 20), "OBJECTIVE", sHead);
            GUI.Label(new Rect(r.x + 12, r.y + 27, r.width - 24, 34), objective, sSmall);
        }

        if (playing && !paused)
        {
            string hint = "E Talk / Interact   |   J Evidence notes   |   Esc Pause";
            Shadowed(new Rect(0, H - 30, W - 20, 24), hint, new GUIStyle(sSmall) { alignment = TextAnchor.LowerRight, fontSize = 14 });
        }

        if (!string.IsNullOrEmpty(trust) && phase == Phase.Dialogue && vikramTalking)
            DrawTrustMeter(W);

        if (Time.time < toastUntil && !string.IsNullOrEmpty(toast) && !paused)
        {
            float tw = toastBig ? 640 : 600;
            var r = new Rect(W / 2 - tw / 2, toastBig ? H * 0.2f : 24, tw, toastBig ? 56 : 40);
            Panel(r, 0.7f);
            GUI.Label(new Rect(r.x, r.y + (toastBig ? 10 : 6), r.width, 40), toast, new GUIStyle(sPrompt) { fontSize = toastBig ? 24 : 18 });
        }

        if (Time.time < subUntil && !string.IsNullOrEmpty(sub) && !paused)
        {
            float h = sSub.CalcHeight(new GUIContent(sub), W * 0.72f) + 16;
            var r = new Rect(W * 0.14f, H - 60 - h, W * 0.72f, h);
            Panel(r, 0.55f);
            GUI.Label(new Rect(r.x + 8, r.y + 8, r.width - 16, h - 8), sub, sSub);
        }

        if (!string.IsNullOrEmpty(dlgText) && !paused) DrawDialogue(W, H);
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
            Shadowed(new Rect(0, H * 0.34f + 76, W, 36), "Thanks for playing, " + SaveSystem.Data.playerName + ".   Acts I and II complete.   Press R for the main menu", sTitle2);
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
        float textH = sPanel.CalcHeight(new GUIContent(dlgText), W * 0.7f - 40);
        float optH = 0;
        if (dlgOptions != null)
        {
            optH = 12;
            foreach (var o in dlgOptions) optH += sPanel.CalcHeight(new GUIContent("Press 1:   " + o), W * 0.7f - 70) + 6;
        }
        else optH = 34;
        float ph = 50 + textH + optH + 10;
        var r = new Rect(W * 0.15f, H - ph - 30, W * 0.7f, ph);
        Panel(r, 0.82f);
        GUI.Label(new Rect(r.x + 20, r.y + 10, r.width - 40, 26), dlgSpeaker, sHead);
        GUI.Label(new Rect(r.x + 20, r.y + 38, r.width - 40, textH + 4), dlgText, sPanel);
        float y = r.y + 46 + textH;
        if (dlgOptions != null)
        {
            for (int i = 0; i < dlgOptions.Length; i++)
            {
                var st = new GUIStyle(sPanel);
                bool dis = dlgDisabled != null && i < dlgDisabled.Length && dlgDisabled[i];
                st.normal.textColor = dis ? new Color(0.55f, 0.55f, 0.55f) : new Color(1f, 0.9f, 0.6f);
                string t = "Press " + (i + 1) + ":   " + dlgOptions[i];
                float oh = sPanel.CalcHeight(new GUIContent(t), W * 0.7f - 70);
                GUI.Label(new Rect(r.x + 30, y, r.width - 60, oh), t, st);
                y += oh + 6;
            }
        }
        else GUI.Label(new Rect(r.x + 30, y + 4, r.width - 60, 26), "[E] / [Space] next line   |   [Enter] skip conversation", sSmall);
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
        float left = Mathf.Max(0f, choiceDeadline - Time.time);
        var r = new Rect(W * 0.2f, H * 0.3f, W * 0.6f, 250);
        Panel(r, 0.85f);
        GUI.Label(new Rect(r.x + 24, r.y + 14, r.width - 48, 30), "THE SUV IS COMING.  DECIDE NOW!", new GUIStyle(sHead) { fontSize = 22 });
        // countdown bar
        var old = GUI.color;
        GUI.color = new Color(0.25f, 0.25f, 0.25f);
        GUI.DrawTexture(new Rect(r.x + 24, r.y + 52, r.width - 48, 12), Texture2D.whiteTexture);
        GUI.color = left < 5f ? new Color(0.95f, 0.25f, 0.2f) : new Color(1f, 0.8f, 0.2f);
        GUI.DrawTexture(new Rect(r.x + 24, r.y + 52, (r.width - 48) * (left / 15f), 12), Texture2D.whiteTexture);
        GUI.color = old;
        GUI.Label(new Rect(r.xMax - 120, r.y + 14, 100, 30), left.ToString("0.0") + " s", new GUIStyle(sHead) { alignment = TextAnchor.UpperRight, fontSize = 22 });
        for (int i = 0; i < choiceOptions.Length; i++)
            GUI.Label(new Rect(r.x + 36, r.y + 84 + i * 46, r.width - 72, 40), "Press " + (i + 1) + ":   " + choiceOptions[i], new GUIStyle(sPanel) { fontSize = 22 });
        GUI.Label(new Rect(r.x + 24, r.yMax - 32, r.width - 48, 24), "If you hesitate, you'll freeze and walk on.", sSmall);
    }

    void DrawJournal(float W, float H)
    {
        Panel(new Rect(0, 0, W, H), 0.45f);
        var r = new Rect(W / 2 - 400, H / 2 - 300, 800, 600);
        Panel(r, 0.9f);
        GUI.Label(new Rect(r.x + 24, r.y + 14, r.width - 48, 34), "EVIDENCE NOTES  -  Act II", sTitle2);
        GUI.Label(new Rect(r.x + 24, r.y + 50, r.width - 48, 24), "[J] close", sHead);
        float y = r.y + 84;
        if (evidence.Count == 0)
            GUI.Label(new Rect(r.x + 24, y, r.width - 48, 40), "Nothing yet. Talk to people and look for clues in the market.", sPanel);
        foreach (var e in evidence)
        {
            float h = sPanel.CalcHeight(new GUIContent("-  " + e), r.width - 56);
            if (y + h > r.yMax - 10) break;
            GUI.Label(new Rect(r.x + 24, y, r.width - 48, h), "-  " + e, sPanel);
            y += h + 6;
        }
    }

    void DrawPause(float W, float H)
    {
        Panel(new Rect(0, 0, W, H), 0.7f);
        GUI.Label(new Rect(0, H * 0.2f, W, 60), "PAUSED", sTitle);
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
