using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Café Irani (Act IV, variant A), the scripted part: sitting down with Raj, the phone he hands over (a fixed beat,
/// whatever is asked after it), the optional questions, the moment the conversation turns dangerous, the kitchen
/// (a searcher who stops to listen at a rattling pan, a local checkpoint only when he actually sees Arjun), the alleys
/// and the hide, and the dhaba.
/// </summary>
public partial class Act4Story
{
    // ---- set up in BuildCafe
    Transform rajChair, arjunChair, watcherChair, newspaper;
    Vector3 rajSeat, arjunSeatPos, watcherSeat;
    const float CafeTableTop = 0.765f;
    // ---- read by the play-test route
    bool phoneHanded, askedWhere, askedPrice, dangerNoticed, rajAskedTrust;
    int potRattles, kitchenDetections, alleyRetries;
    string kitchenState = "";
    Transform searcherK; CharacterNPC searcherNpc; Light searcherTorch;
    float searcherAlert;            // > 0: listening after a rattle (seconds left)

    // =============================================================== sounds
    static AudioClip rattleClip, scrapeClip, rustleClip, panRingClip;
    static AudioClip Rattle()
    {
        if (rattleClip != null) return rattleClip;
        // a steel pan knocked against a brass pot: two inharmonic rings and a rattle of re-contacts as it settles
        const int SR = 44100; int n = (int)(SR * 1.5f); var d = new float[n]; var r = new System.Random(77);
        float[] f = { 1320f, 2093f, 2817f, 3910f, 713f };
        float[] dec = { 3.2f, 4.1f, 5.5f, 7f, 2.4f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR, s = 0f;
            for (int k = 0; k < f.Length; k++) s += Mathf.Sin(2f * Mathf.PI * f[k] * t * (1f + 0.0015f * Mathf.Sin(t * 9f))) * Mathf.Exp(-t * dec[k]) / (k + 1.5f);
            // re-contacts as it rocks and settles: clicks at shrinking intervals
            float hit = 0f;
            foreach (float at in new[] { 0f, 0.16f, 0.29f, 0.39f, 0.46f, 0.51f, 0.545f })
                if (t >= at && t < at + 0.012f) hit += (float)(r.NextDouble() * 2 - 1) * (1f - (t - at) / 0.012f) * (1f - at * 1.4f);
            d[i] = s * 0.8f + hit * 0.7f;
        }
        Norm(d, 0.75f);
        rattleClip = AudioClip.Create("PanRattle", n, 1, SR, false); rattleClip.SetData(d, 0);
        return rattleClip;
    }
    static AudioClip PanRing()
    {
        if (panRingClip != null) return panRingClip;
        const int SR = 44100; int n = (int)(SR * 2.4f); var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            d[i] = (Mathf.Sin(2f * Mathf.PI * 880f * t) * Mathf.Exp(-t * 1.6f) + 0.5f * Mathf.Sin(2f * Mathf.PI * 2213f * t) * Mathf.Exp(-t * 2.8f)) * Mathf.Clamp01(t / 0.004f);
        }
        Norm(d, 0.5f);
        panRingClip = AudioClip.Create("PanRing", n, 1, SR, false); panRingClip.SetData(d, 0);
        return panRingClip;
    }
    static AudioClip Scrape()
    {
        if (scrapeClip != null) return scrapeClip;
        // a wooden chair leg dragged on tile: a stick-slip judder in filtered noise
        const int SR = 44100; int n = (int)(SR * 0.55f); var d = new float[n]; var r = new System.Random(5); float a = 0f, b = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR, w = (float)(r.NextDouble() * 2 - 1);
            a += (w - a) * 0.25f; b += (a - b) * 0.35f;
            float judder = 0.55f + 0.45f * Mathf.Sign(Mathf.Sin(2f * Mathf.PI * (38f + 30f * t) * t));
            float env = Mathf.Clamp01(t / 0.03f) * Mathf.Clamp01((0.55f - t) / 0.12f);
            d[i] = (b * 3f + Mathf.Sin(2f * Mathf.PI * 190f * t) * 0.2f) * judder * env;
        }
        Norm(d, 0.7f);
        scrapeClip = AudioClip.Create("ChairScrape", n, 1, SR, false); scrapeClip.SetData(d, 0);
        return scrapeClip;
    }
    static AudioClip Rustle()
    {
        if (rustleClip != null) return rustleClip;
        const int SR = 44100; int n = (int)(SR * 0.7f); var d = new float[n]; var r = new System.Random(9); float hp = 0f, prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR, w = (float)(r.NextDouble() * 2 - 1);
            hp = 0.6f * (hp + w - prev); prev = w;               // crisp: high-passed noise in bursts
            float burst = Mathf.PerlinNoise(t * 22f, 0.4f);
            d[i] = hp * burst * burst * Mathf.Clamp01(t / 0.02f) * Mathf.Clamp01((0.7f - t) / 0.2f);
        }
        Norm(d, 0.55f);
        rustleClip = AudioClip.Create("PaperRustle", n, 1, SR, false); rustleClip.SetData(d, 0);
        return rustleClip;
    }

    /// <summary>A one-shot sound from a place in the world (so it can be heard round a corner, from the left, close by).</summary>
    void At(Vector3 p, AudioClip c, float vol, float minDist = 1.2f, float maxDist = 18f)
    {
        var go = new GameObject("Sound_" + c.name); go.transform.SetParent(transform, false); go.transform.position = p;
        var a = go.AddComponent<AudioSource>();
        a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Logarithmic; a.minDistance = minDist; a.maxDistance = maxDist;
        a.dopplerLevel = 0f; a.clip = c; a.volume = vol; a.Play();
        Destroy(go, c.length + 0.2f);
    }

    // =============================================================== the flow
    IEnumerator CafeRun()
    {
        AddSpot(rajSpot, Vector3.up * 1.2f, "Sit down with Raj Malhotra", () => phase == Phase.Free && stageA == 0, () => StartCoroutine(RajTalk()));
        StartCoroutine(OwnerWork());
        StartCoroutine(WatcherReads());
        yield return OpenAct();
        Teleport(new Vector3(-3.6f, 0.1f, -3.4f), 40f);
        yield return Narrate("a4a_vo");
        BeginFree("Meet the caller at the corner table", raj.transform.position + Vector3.up * 1.4f, "Corner table");
        yield return new WaitUntil(() => stageA >= 1);

        yield return Kitchen();
        yield return Alleys();
        yield return Dhaba();
        yield return CloseAct("", "");
    }

    // ------------------------------------------------------------- Raj
    int stageA;
    float standEye = -1f;

    IEnumerator RajTalk()
    {
        BeginBusy();
        stageA = -1;
        if (standEye < 0f) standEye = cam.localPosition.y;
        // he looks up as Arjun pulls out the chair and sits down opposite him
        raj.lookTarget = cam;
        At(arjunChair != null ? arjunChair.position : arjunSeatPos, Scrape(), 0.35f);
        if (arjunChair != null) StartCoroutine(Nudge(arjunChair, new Vector3(0f, 0f, 0.12f), 0.35f));
        yield return Glide(arjunSeatPos, 0f, 6f, 1.15f, standEye - 0.47f);
        yield return Talk("a4a_raj_1", raj);
        yield return Talk("a4a_arjun_why");
        yield return Talk("a4a_raj_2", raj);
        // ---- the phone: handed over here, whatever Arjun asks next, as Raj's recorded answer to the question of trust
        yield return Talk("a4a_q_a");
        rajAskedTrust = true;
        yield return RajPhone();
        // ---- what else Arjun wants to know: optional, any order, or none (trust is already asked)
        bool[] asked = { true, false, false };
        while (true)
        {
            yield return Choose(new[] { "Trust him?", "Priya", "His price", "Time to go" },
                                new[] { DialogueDB.Text("a4a_q_a"), DialogueDB.Text("a4a_q_b"), DialogueDB.Text("a4a_q_c"), "Leave it there. Not here, not now: we should go." },
                                new[] { asked[0], asked[1], asked[2], false }, new[] { asked[0], asked[1], asked[2], false });
            int c = menuResult;
            if (c == 3) break;
            asked[c] = true;
            yield return Talk(c == 0 ? "a4a_q_a" : c == 1 ? "a4a_q_b" : "a4a_q_c");
            // how he takes the question, before he answers it
            var away = new GameObject("RajGlance").transform; away.SetParent(transform, false);
            away.position = c == 0 ? new Vector3(3.2f, CafeTableTop, 2.25f)              // the phone, now on Arjun's side
                          : c == 1 ? new Vector3(-3.6f, 1.6f, -3.6f)                     // the entrance: is anyone listening?
                          : new Vector3(3.2f, CafeTableTop, 2.95f);                      // his own hands
            raj.lookTarget = away;
            yield return Wait(c == 1 ? 0.8f : 0.65f);
            raj.lookTarget = cam;
            yield return Wait(0.2f);
            Destroy(away.gameObject);
            yield return Talk(c == 0 ? "a4a_raj_a" : c == 1 ? "a4a_raj_b" : "a4a_raj_c", raj);
            if (c == 0) rajAskedTrust = true;
            if (c == 1) AddEvidence("Raj: Priya is at the old NGO shelter by the river. He told Deshpande's men she went to Nagpur.");
            if (c == 2) AddEvidence("Raj will testify against Deshpande in exchange for immunity.");
            if (asked[0] && asked[1] && asked[2]) break;
        }
        askedWhere = asked[1]; askedPrice = asked[2];
        EndTalk();
        // ---- it has turned dangerous: give the player the moment to see it before anyone says so
        yield return Danger();
        yield return Talk("a4a_raj_made", raj);
        EndTalk();
        stageA = 1;
    }

    IEnumerator Nudge(Transform t, Vector3 by, float dur)
    {
        Vector3 a = t.position, b = a + t.rotation * by;
        for (float k = 0f; k < dur; k += Time.deltaTime) { t.position = Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, k / dur)); yield return null; }
        t.position = b;
    }

    /// <summary>A phone: a dark slab with a lit screen (the recordings app open).</summary>
    Transform MakePhone()
    {
        var p = new GameObject("RajPhone").transform; p.SetParent(transform, false);
        GameKit.Box(p, "PhoneBody", Vector3.zero, new Vector3(0.072f, 0.0085f, 0.152f), GameKit.Lit(new Color(0.05f, 0.05f, 0.06f), 0.75f, 0.3f));
        var scr = GameKit.Box(p, "PhoneScreen", new Vector3(0f, 0.0046f, 0f), new Vector3(0.064f, 0.0008f, 0.138f), GameKit.Glow(new Color(0.15f, 0.2f, 0.25f), new Color(0.35f, 0.55f, 0.75f), 1.1f));
        scr.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var bar = GameKit.Box(p, "PhoneWave", new Vector3(0f, 0.0052f, 0.02f), new Vector3(0.05f, 0.0004f, 0.012f), GameKit.Glow(new Color(0.6f, 0.4f, 0.2f), new Color(1f, 0.7f, 0.3f), 1.6f));
        bar.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return p;
    }

    /// <summary>Raj takes the phone out of his jacket, lays it on the table and slides it across while he says what is on it.</summary>
    IEnumerator RajPhone()
    {
        var phone = MakePhone();
        var goal = new GameObject("RajHandGoal").transform; goal.SetParent(transform, false);
        Vector3 hand = raj.HandPosition(1);
        Vector3 inside = raj.transform.position + Vector3.up * 1.0f + raj.transform.forward * 0.12f - raj.transform.right * 0.06f;   // his inside pocket
        Vector3 onTable = new Vector3(3.24f, CafeTableTop + 0.035f, 2.92f);
        Vector3 across = new Vector3(3.2f, CafeTableTop + 0.035f, 2.28f);
        goal.position = hand;
        raj.Hold(1, goal, 0.3f);
        raj.lookTarget = phone;
        phone.gameObject.SetActive(false);
        StartCoroutine(Talk("a4a_raj_a", raj));            // "You shouldn't. Not yet. But I'm going to give you something..."
        yield return MoveGoal(goal, hand, inside, 0.55f);
        phone.gameObject.SetActive(true);
        phone.SetParent(goal, false); phone.localPosition = new Vector3(0f, -0.03f, 0.02f); phone.localRotation = Quaternion.Euler(70f, 0f, 0f);
        yield return MoveGoal(goal, inside, onTable + Vector3.up * 0.03f, 0.6f, 0.05f);
        phone.SetParent(transform, true);
        phone.position = onTable; phone.rotation = Quaternion.Euler(0f, 180f, 0f);            // screen up, its top towards Arjun
        sfx.PlayOneShot(EvidenceFolio.Put(), 0.5f);
        raj.lookTarget = cam;
        // Arjun's eyes go down to it: the hand-over is seen, not just heard
        StartCoroutine(Turn(Vector3.Lerp(onTable, across, 0.6f), 0.7f));
        // two fingers on it, and it slides across the table
        for (float t = 0f; t < 0.7f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.7f);
            phone.position = Vector3.Lerp(onTable, across, k);
            goal.position = phone.position + Vector3.up * 0.03f + new Vector3(0f, 0f, 0.05f);
            yield return null;
        }
        At(phone.position, EvidenceFolio.Slide(), 0.45f);
        raj.Release(1, 0.6f);
        // his voice runs on; wait for it, and the phone stays where Arjun can see it
        while (dlgText == DialogueDB.Text("a4a_raj_a") && raj.IsTalking) yield return null;
        phoneHanded = true;
        SaveSystem.Data.hasRecordings = true;
        AddEvidence("RAJ'S PHONE: six months of recorded calls with Deshpande - forgeries, bribes, the plan to destroy the originals.");
        Toast("You took RAJ'S PHONE: the recordings", 3.5f);
        // Arjun picks it up
        Vector3 from = phone.position;
        for (float t = 0f; t < 0.45f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.45f);
            phone.position = Vector3.Lerp(from, cam.position + cam.forward * 0.25f - cam.up * 0.3f + cam.right * 0.12f, k);
            yield return null;
        }
        phone.gameObject.SetActive(false);
        Destroy(goal.gameObject);
        EndTalk();
        yield return Turn(raj.EyePoint(), 0.6f);            // and back up to him
    }

    /// <summary>The watcher reads his paper at the corner table, turning a page now and then.</summary>
    IEnumerator WatcherReads()
    {
        if (watcher == null || newspaper == null) yield break;
        var l = new GameObject("WatcherHandL").transform; l.SetParent(transform, false);
        var r = new GameObject("WatcherHandR").transform; r.SetParent(transform, false);
        watcherHands = new[] { l, r };
        float t0 = Time.time;
        while (this != null && watcher != null && !dangerStarted)
        {
            PlaceHandsOnPaper();
            if (Time.time - t0 > 9f) { t0 = Time.time + Random.Range(0f, 5f); At(newspaper.position, Rustle(), 0.25f, 0.8f, 8f); }
            yield return null;
        }
    }
    Transform[] watcherHands;
    bool dangerStarted;

    void PlaceHandsOnPaper()
    {
        if (watcherHands == null || newspaper == null) return;
        float hw = newspaper.lossyScale.x * 0.5f;
        watcherHands[0].position = newspaper.position - newspaper.right * hw * 0.92f - newspaper.up * 0.05f;
        watcherHands[1].position = newspaper.position + newspaper.right * hw * 0.92f - newspaper.up * 0.05f;
        if (watcher != null) { watcher.Hold(0, watcherHands[0], 0.5f); watcher.Hold(1, watcherHands[1], 0.5f); }
    }

    /// <summary>
    /// Raj's eyes go past Arjun; across the terrace the watcher folds his paper, pushes his chair back and takes out a
    /// phone. Arjun's head is free for a moment (he is still seated): if the player looks round, the camera is not turned
    /// for them; if not, it turns once the moment has passed.
    /// </summary>
    IEnumerator Danger()
    {
        dangerStarted = true;
        var watcherHead = watcher != null ? watcher.EyePoint() : new Vector3(-3.9f, 1.3f, 3.2f);
        raj.lookTarget = watcher != null ? HeadOf(watcher) : null;
        // a beat with his eyes past Arjun's shoulder before anything else moves
        yield return Wait(0.9f);
        // the paper folds and goes down
        if (newspaper != null)
        {
            At(newspaper.position, Rustle(), 0.6f, 1f, 12f);
            Vector3 s0 = newspaper.localScale, p0 = newspaper.position;
            Vector3 lap = watcher != null ? watcher.transform.position + Vector3.up * 0.62f + watcher.transform.forward * 0.25f : p0;
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.8f);
                newspaper.localScale = new Vector3(Mathf.Lerp(s0.x, s0.x * 0.5f, Mathf.Clamp01(k * 1.6f)), s0.y, s0.z);
                newspaper.position = Vector3.Lerp(p0, lap, Mathf.Clamp01(k * 1.4f - 0.4f));
                PlaceHandsOnPaper();
                yield return null;
            }
        }
        yield return Wait(0.35f);
        // the chair goes back on the tiles as he stands
        if (watcher != null)
        {
            if (watcherChair != null) { At(watcherChair.position, Scrape(), 0.9f, 1.5f, 16f); StartCoroutine(Nudge(watcherChair, new Vector3(0f, 0f, -0.22f), 0.35f)); }
            watcher.Release(0, 0.4f);
            watcher.Stand();
            yield return Wait(0.5f);
            // a phone to his ear, eyes on the corner table
            var ear = new GameObject("WatcherEar").transform; ear.SetParent(transform, false);
            var wphone = MakePhone(); wphone.SetParent(ear, false); wphone.localPosition = new Vector3(0f, 0f, 0f); wphone.localRotation = Quaternion.Euler(0f, 0f, 90f);
            ear.position = watcher.HandPosition(1);
            watcher.Hold(1, ear, 0.4f);
            watcher.lookTarget = HeadOf(raj);
            watcher.freezeFacing = true;
            Vector3 earAt = watcher.EyePoint() + watcher.transform.right * 0.09f - watcher.transform.forward * 0.04f - Vector3.up * 0.06f;
            StartCoroutine(MoveGoal(ear, ear.position, earAt, 0.6f, 0.05f));
        }
        // the moment to notice: Arjun can turn his head (seated, he does not move)
        PriyaRoomPlayer.LookOnly = true; PriyaRoomPlayer.LookCentre = player.Yaw; PriyaRoomPlayer.LookRange = 110f;
        float held = 0f;
        for (float t = 0f; t < 2.6f && held < 0.45f; t += Time.deltaTime)
        {
            if (GameState.Paused) { yield return null; t -= Time.deltaTime; continue; }
            Vector3 to = watcherHead - cam.position;
            held = Vector3.Angle(cam.forward, to) < 16f ? held + Time.deltaTime : 0f;
            yield return null;
        }
        PriyaRoomPlayer.LookOnly = false;
        dangerNoticed = held >= 0.45f;
        if (!dangerNoticed) yield return Turn(watcherHead, 0.75f);
        else yield return Wait(0.2f);
        raj.lookTarget = cam;
    }

    // ------------------------------------------------------------- the kitchen
    readonly List<Transform> potObjs = new List<Transform>();
    readonly List<float> potQuiet = new List<float>();
    static readonly Vector3[] KitchenCover = { new Vector3(-3.4f, 0f, 0f), new Vector3(-1.25f, 0f, 0.35f), new Vector3(2.15f, 0f, -0.75f) };

    IEnumerator Kitchen()
    {
        yield return FadeOut(2f);
        PriyaRoomPlayer.Surface = 0;
        var camLocal = cam.localPosition; camLocal.y = standEye > 0f ? standEye : 1.62f; cam.localPosition = camLocal;      // standing again
        raj.Stand(K + new Vector3(-3.6f, 0f, -0.6f), true);
        raj.StopWalking();
        raj.transform.position = K + new Vector3(-3.6f, 0f, -0.6f);
        raj.lookTarget = null;
        Teleport(K + new Vector3(-3.4f, 0.1f, 0f), 90f);
        rajFollow = true; StartCoroutine(RajFollows());
        yield return FadeIn(1.5f);
        BeginBusy();
        yield return Talk("a4a_arjun_kitchen");
        EndTalk();
        BeginFree("Cross the kitchen to the back exit. Keep out of the torchlight", K + new Vector3(3.6f, 1.2f, 0), "Back exit");
        int checkpoint = 0;
        float since = 0f; bool coming = false, here = false;
        kitchenState = "quiet";
        while (true)
        {
            if (phase != Phase.Free) { yield return null; continue; }
            Vector3 pp = player.transform.position;
            if (Near(K + new Vector3(3.6f, 0, 0), 0.9f)) break;
            for (int i = 0; i < KitchenCover.Length; i++) if (i > checkpoint && Near(K + KitchenCover[i], 0.9f)) checkpoint = i;
            since += Time.deltaTime;
            // footsteps outside the door he came through: someone is coming (2.5 s before he appears)
            if (!coming && (checkpoint >= 1 || since > 14f || potRattles > 0))
            {
                coming = true;
                StartCoroutine(SearcherSteps(K + new Vector3(-4.6f, 0f, 0.4f), 2.5f));
                StartCoroutine(SearcherArrives(2.5f));
            }
            here = searcherNpc != null;
            // a pot touched: it rattles where it is and settles; if he is there he stops and listens
            for (int i = 0; i < potObjs.Count; i++)
            {
                var pt = potObjs[i];
                if (pt == null || Time.time < potQuiet[i]) continue;
                Vector3 d = pt.position - pp; d.y = 0f;
                if (d.magnitude > 0.42f) continue;
                potQuiet[i] = Time.time + 2.5f;
                potRattles++;
                At(pt.position + Vector3.up * 0.15f, Rattle(), 0.9f, 1f, 22f);
                StartCoroutine(Settle(pt));
                if (here) { searcherAlert = 3.2f; heardAt = pt.position; StartCoroutine(Raj("a4a_raj_wait")); }
            }
            // seen: in his torchlight, nothing between them, for long enough
            if (here && Seen(Time.deltaTime))
            {
                kitchenDetections++;
                kitchenState = "detected";
                BeginBusy();
                if (searcherNpc != null) searcherNpc.lookTarget = cam;
                Overheard(searcherNpc, "a4a_searcher_found");
                yield return Wait(1.1f);
                yield return FadeOut(3f);
                // back to the last cover reached (at least the rack: never back in front of him at the door), Raj at his
                // shoulder, the searcher at the door again, calm
                checkpoint = Mathf.Max(checkpoint, 1);
                Vector3 cp = K + KitchenCover[checkpoint] + Vector3.up * 0.1f;
                Teleport(cp, 90f);
                raj.transform.position = cp - Vector3.right * 0.8f - Vector3.up * 0.1f;
                ResetSearcher();
                exposure = 0f; searcherAlert = 0f;
                yield return FadeIn(2.5f);
                BeginBusy();
                yield return Talk("a4a_raj_clang", raj);
                EndTalk();
                BeginFree("Cross the kitchen to the back exit. Keep out of the torchlight", K + new Vector3(3.6f, 1.2f, 0), "Back exit");
                kitchenState = "retry";
            }
            yield return null;
        }
        kitchenState = "out";
        if (searcherNpc != null) { Destroy(searcherNpc.gameObject); searcherNpc = null; }
    }

    IEnumerator Raj(string id) { Overheard(raj, id); yield break; }

    static Transform HeadOf(CharacterNPC n) { if (n == null) return null; var h = n.HeadForFace; return h != null ? h : n.transform; }

    /// <summary>A line spoken by someone in the scene, from where they are (so it carries direction), with its subtitle;
    /// it does not hold the scene up.</summary>
    void Overheard(CharacterNPC n, string id)
    {
        if (n == null || n.IsTalking) return;
        var c = VO(id);
        if (c != null) n.Say(c);
        sub = DialogueDB.Speaker(id) + ":  " + DialogueDB.Text(id);
        subUntil = Time.time + (c != null ? c.length + 0.4f : 2.5f);
    }

    /// <summary>A pot that was knocked rocks on its base and settles.</summary>
    IEnumerator Settle(Transform pt)
    {
        Quaternion r0 = pt.rotation; Vector3 axis = Vector3.Cross(Vector3.up, Random.onUnitSphere).normalized;
        for (float t = 0f; t < 0.7f; t += Time.deltaTime)
        {
            float a = 9f * Mathf.Exp(-t * 5f) * Mathf.Sin(t * 30f);
            pt.rotation = Quaternion.AngleAxis(a, axis) * r0;
            yield return null;
        }
        pt.rotation = r0;
    }

    IEnumerator SearcherSteps(Vector3 from, float dur)
    {
        for (float t = 0f; t < dur; t += 0.52f)
        {
            At(from + Vector3.right * (t / dur) * 0.6f, stepClip, 0.7f, 1f, 14f);
            yield return Wait(0.52f);
        }
    }

    Vector3 searcherPost, heardAt; float exposure, sweepT;

    IEnumerator SearcherArrives(float delay)
    {
        yield return Wait(delay);
        searcherPost = K + new Vector3(-3.55f, 0f, 0.15f);
        searcherNpc = Npc(searcherPost, 90f, NpcStyle.Thug(), "KitchenSearcher", false);
        searcherNpc.freezeFacing = false;
        var torchGoal = new GameObject("SearcherTorchHand").transform; torchGoal.SetParent(searcherNpc.transform, false);
        torchGoal.localPosition = new Vector3(0.2f, 1.25f, 0.38f);
        searcherNpc.Hold(1, torchGoal, 0.4f);
        GameKit.Cyl(torchGoal, "Torch", new Vector3(0f, 0f, 0.06f), 0.02f, 0.18f, GameKit.Lit(new Color(0.1f, 0.1f, 0.11f), 0.5f, 0.5f)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var lg = new GameObject("SearcherTorch"); lg.transform.SetParent(torchGoal, false); lg.transform.localPosition = new Vector3(0f, 0f, 0.16f);
        searcherTorch = lg.AddComponent<Light>();
        searcherTorch.type = LightType.Spot; searcherTorch.spotAngle = 44f; searcherTorch.innerSpotAngle = 18f;
        searcherTorch.range = 11f; searcherTorch.intensity = 9f; searcherTorch.color = new Color(1f, 0.95f, 0.85f);
        CinematicLook.Shadows(searcherTorch, 0.9f, 0.02f, 0.4f);
        searcherK = searcherNpc.transform;
        StartCoroutine(SearcherMind());
    }

    void ResetSearcher()
    {
        if (searcherNpc == null) return;
        searcherNpc.StopWalking();
        searcherNpc.transform.position = searcherPost;
        searcherNpc.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        sweepT = 0f;
    }

    /// <summary>He stands in the doorway sweeping the kitchen with his torch; a rattle turns him towards it, he takes a few
    /// steps and stands listening, then goes back to the doorway.</summary>
    IEnumerator SearcherMind()
    {
        while (searcherNpc != null && this != null)
        {
            if (searcherAlert > 0f)
            {
                kitchenState = "listening";
                var n = searcherNpc;
                Vector3 to = heardAt - n.transform.position; to.y = 0f;
                // turn to the sound, a few slow steps towards it, then stand still and listen
                Quaternion face = Quaternion.LookRotation(to.normalized);
                for (float t = 0f; t < 0.4f; t += Time.deltaTime) { if (n == null) yield break; n.transform.rotation = Quaternion.Slerp(n.transform.rotation, face, t / 0.4f); yield return null; }
                Overheard(n, "a4a_searcher_listen");
                yield return Wait(0.6f);
                Vector3 step = n.transform.position + to.normalized * Mathf.Min(1.6f, to.magnitude - 0.8f);
                yield return n.WalkTo(step, 0.7f, 4f);
                while (searcherAlert > 0f) { searcherAlert -= Time.deltaTime; yield return null; }
                if (n == null) yield break;
                yield return n.WalkTo(searcherPost, 0.8f, 5f);
                kitchenState = "sweeping";
                continue;
            }
            kitchenState = "sweeping";
            // the sweep: left and right across the kitchen, slowly, pausing at each end
            sweepT += Time.deltaTime;
            float yaw = 90f + 42f * Mathf.Sin(sweepT * 2f * Mathf.PI / 7.5f);
            searcherNpc.transform.rotation = Quaternion.Slerp(searcherNpc.transform.rotation, Quaternion.Euler(0f, yaw, 0f), Time.deltaTime * 3f);
            yield return null;
        }
    }

    /// <summary>True once Arjun has been in clear sight long enough to be recognised: in the torch's beam out to 10 m, or
    /// close in front of the searcher (within 2.4 m, in front of him), wherever the beam happens to point.</summary>
    bool Seen(float dt)
    {
        if (searcherTorch == null || searcherNpc == null) return false;
        Vector3 eye = searcherNpc.EyePoint();
        Vector3 head = cam.position;
        Vector3 to = head - eye;
        float dist = to.magnitude;
        if (dist < 0.05f) return false;
        Vector3 beamTo = head - searcherTorch.transform.position;
        bool inBeam = beamTo.magnitude < 10f && Vector3.Angle(searcherTorch.transform.forward, beamTo) < searcherTorch.spotAngle * 0.5f + 4f;
        Vector3 flat = Vector3.ProjectOnPlane(to, Vector3.up);
        bool close = dist < 2.4f && Vector3.Angle(searcherNpc.transform.forward, flat) < 75f;
        bool clear = (inBeam || close) && !Physics.Linecast(eye + to / dist * 0.3f, head - to / dist * 0.35f, ~0, QueryTriggerInteraction.Ignore);
        exposure = clear ? exposure + dt * (searcherAlert > 0f || close ? 2.2f : 1f) : Mathf.Max(0f, exposure - dt * 0.8f);
        return exposure > 0.55f;
    }

    // ------------------------------------------------------------- the alleys
    IEnumerator Alleys()
    {
        yield return FadeOut(2f);
        PriyaRoomPlayer.Surface = 1;
        Teleport(A + new Vector3(0, 0.1f, -8f), 0f);
        raj.transform.position = A + new Vector3(0.3f, 0f, -9f);
        yield return FadeIn(1.5f);
        BeginFree("Find the way to the main road", A + new Vector3(0, 1.5f, 1f), "The fork");
        StartCoroutine(CuratorFlash("flash2_01", "flash2_02", "flash2_03"));
        bool deadEndSaid = false;
        while (true)
        {
            Vector3 p = player.transform.position;
            if (!deadEndSaid && p.x < A.x - 7.5f)
            {
                deadEndSaid = true; BeginBusy();
                yield return Talk("a4a_arjun_dead"); EndTalk();
                BeginFree("Take the other way at the fork", A + new Vector3(5f, 1.5f, 1.1f), "East alley");
            }
            if (p.x > A.x + 8f && p.z > A.z + 3.2f) break;
            yield return null;
        }
        // ---- hide from the searchers (each try starts clean: standing, unlocked, Raj on his feet, no torch left over)
        Vector3 rajCover = A + new Vector3(8.4f, 0f, 5.6f);          // the dumpster's far side from the corner they come round
        while (true)
        {
            hidden = false;
            var dumpster = transform.Find("Dumpster");
            var hideSpot = AddSpot(dumpster ?? transform, Vector3.zero, "Hide behind the dumpster", () => !hidden && phase == Phase.Free, () => { hidden = true; StartCoroutine(HideBehindDumpster()); }, 3f, 0.9f);
            BeginBusy();
            sfx.PlayOneShot(stepClip, 0.6f);
            yield return Talk("a4a_arjun_hide"); EndTalk();
            BeginFree("Hide behind the dumpster!", A + new Vector3(8.35f, 1.3f, 7f), "Dumpster");
            var t1 = Npc(A + new Vector3(9f, 0f, 16f), 180f, NpcStyle.Thug(), "Searcher1", false);
            var t2 = Npc(A + new Vector3(8.6f, 0f, 17.2f), 180f, NpcStyle.Thug(), "Searcher2", false);
            var torch = PL(t1.transform.position + Vector3.up * 1.4f, new Color(0.95f, 0.95f, 1f), 3f, 6f);
            t1.StartCoroutine(t1.WalkTo(A + new Vector3(9f, 0f, 1.2f), 1.5f));     // run on the NPCs, so removing them is safe
            t2.StartCoroutine(t2.WalkTo(A + new Vector3(8.6f, 0f, 2.0f), 1.5f));
            bool caught = false, warned = false, rajHid = false;
            while (t1 != null && t1.transform.position.z > A.z + 2f)
            {
                torch.transform.position = t1.transform.position + Vector3.up * 1.4f + Vector3.back * 0.5f;
                float dist = Vector3.Distance(player.transform.position, t1.transform.position);
                if (!warned && dist < 9f) { warned = true; StartCoroutine(Narrate("a4a_thug_search")); }
                if (!hidden && dist < 6f) { caught = true; break; }
                // Raj steps into cover once, behind the dumpster beside Arjun, and stays down there
                if (hidden && rajFollow && !rajHid) { rajHid = true; StartCoroutine(RajTakesCover(rajCover)); }
                yield return null;
            }
            spots.Remove(hideSpot);
            if (t1 != null) Destroy(t1.gameObject);
            if (t2 != null) Destroy(t2.gameObject);
            if (torch != null) Destroy(torch.gameObject);
            if (!caught) break;
            alleyRetries++;
            BeginBusy();
            yield return Talk("a4a_caught");
            EndTalk();
            Toast("They saw you. Get behind the dumpster before they reach the corner.", 4f);
            yield return FadeOut(2f);
            // the retry starts clean: standing, Raj on his feet beside Arjun, nothing locked
            hidden = false;
            var cl = cam.localPosition; cl.y = standEye > 0f ? standEye : 1.62f; cam.localPosition = cl;
            PriyaRoomPlayer.LookOnly = false;
            raj.Stand(A + new Vector3(4f, 0f, 1.1f), true);
            raj.StopWalking();
            raj.transform.position = A + new Vector3(4f, 0f, 1.1f);
            Teleport(A + new Vector3(5f, 0.1f, 1.1f), 90f);
            yield return FadeIn(1.5f);
            BeginFree("Back to the north run. Find cover before they come round the corner", A + new Vector3(9f, 1.5f, 3.6f), "North run");
            yield return new WaitUntil(() => player.transform.position.x > A.x + 8f && player.transform.position.z > A.z + 3.2f);
        }
        yield return Crouch(false);
        if (raj.IsSeated) raj.Stand(rajCover + Vector3.right * 0.4f);
        PriyaRoomPlayer.LookOnly = false;
    }

    /// <summary>Arjun gets down on the dumpster's far side from the corner they will come round.</summary>
    IEnumerator HideBehindDumpster()
    {
        PriyaRoomPlayer.Locked = true;
        yield return Glide(A + new Vector3(9.2f, 0.1f, 5.55f), -8f, -4f, 0.45f);
        yield return Crouch(true);
    }

    /// <summary>Raj steps in behind the dumpster and crouches there, holding still (set once, not every frame).</summary>
    IEnumerator RajTakesCover(Vector3 at)
    {
        rajFollow = false;
        raj.StopWalking();
        yield return raj.WalkTo(at, 2.4f, 2.5f);
        // down on his heels, facing the wall, his head turned to the corner they will come round
        raj.Sit(at + Vector3.up * 0.36f, 0f, 0.28f);
        raj.lookTarget = transform.Find("Dumpster");
        // Arjun is hidden too: his head can turn, his body stays down
        PriyaRoomPlayer.LookOnly = true; PriyaRoomPlayer.LookCentre = player.Yaw; PriyaRoomPlayer.LookRange = 80f;
    }

    // ------------------------------------------------------------- the dhaba
    IEnumerator Dhaba()
    {
        BeginFree("Reach the dhaba and blend in", A + new Vector3(9f, 1.8f, 17f), "Dhaba");
        rajFollow = true; StartCoroutine(RajFollows());
        yield return new WaitUntil(() => Near(A + new Vector3(9f, 0, 17f), 3f));
        BeginBusy();
        rajFollow = false;
        raj.StopWalking();
        // they take the empty charpai like anyone stopping for chai: Raj on its edge, Arjun on the plastic chair facing him
        Vector3 seat = A + new Vector3(8.62f, 0.5f, 18.4f);
        Vector3 front = seat + Quaternion.Euler(0f, -120f, 0f) * Vector3.forward * 0.35f; front.y = 0f;
        yield return raj.WalkTo(front, 1.4f, 4f);
        raj.Sit(seat, -120f, 0.45f);
        raj.lookTarget = cam;
        yield return Glide(A + new Vector3(7.6f, 0.1f, 17.75f), 57f, 8f, 1.1f, (standEye > 0f ? standEye : 1.62f) - 0.47f);
        yield return Talk("a4a_raj_dhaba", raj);
        if (!askedWhere)
        {
            // the location lead is the same whichever questions were asked
            yield return Talk("a4a_raj_where", raj);
            AddEvidence("Raj: Priya is at the old NGO shelter by the river. He told Deshpande's men she went to Nagpur.");
        }
        EndTalk();
        SaveSystem.Data.allyRaj = true; SaveSystem.Save();
        yield return Narrate("a4a_vo_end");
    }

    IEnumerator RajFollows()
    {
        while (rajFollow && raj != null)
        {
            if (raj.IsSeated) { yield return null; continue; }
            Vector3 target = player.transform.position - player.transform.forward * 1.1f;
            target.y = 0f;
            if (Vector3.Distance(raj.transform.position, target) > 1.0f && !hidden)
                yield return raj.WalkTo(target, Vector3.Distance(raj.transform.position, target) > 4f ? 3.2f : 1.6f, 3f);
            else yield return new WaitForSeconds(0.2f);
        }
    }

    IEnumerator Crouch(bool down)
    {
        float y0 = cam.localPosition.y, y1 = down ? 0.85f : (standEye > 0f ? standEye : 1.62f);
        for (float t = 0; t < 0.4f; t += Time.deltaTime)
        {
            var p = cam.localPosition; p.y = Mathf.Lerp(y0, y1, t / 0.4f); cam.localPosition = p;
            yield return null;
        }
        var e = cam.localPosition; e.y = y1; cam.localPosition = e;
        if (down) PriyaRoomPlayer.Locked = true; else PriyaRoomPlayer.Locked = phase != Phase.Free;
        if (!down) PriyaRoomPlayer.LookOnly = false;
    }

    /// <summary>A small radio playing an old film tune, band-limited like a cheap speaker, and men talking low.</summary>
    IEnumerator DhabaSound()
    {
        yield return null;
        const int SR = 22050;
        // the radio: a slow pentatonic tune with a drone and a soft hand-drum, through a 300 Hz - 3 kHz "speaker"
        int n = SR * 12; var d = new float[n];
        float[] scale = { 0f, 2f, 4f, 7f, 9f, 12f, 14f };
        var rnd = new System.Random(19); float lp = 0f, hp = 0f, prevX = 0f;
        int[] tune = new int[24]; for (int i = 0; i < tune.Length; i++) tune[i] = rnd.Next(scale.Length);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            int note = tune[(int)(t / 0.5f) % tune.Length];
            float f = 293.7f * Mathf.Pow(2f, scale[note] / 12f), ph = t % 0.5f;
            float mel = (Mathf.Sin(2f * Mathf.PI * f * t) + 0.4f * Mathf.Sin(4f * Mathf.PI * f * t)) * Mathf.Clamp01(ph / 0.03f) * Mathf.Exp(-ph * 2.2f);
            float drone = 0.25f * Mathf.Sin(2f * Mathf.PI * 146.8f * t) + 0.15f * Mathf.Sin(2f * Mathf.PI * 220f * t);
            float beat = t % 0.25f; float drum = Mathf.Sin(2f * Mathf.PI * (110f - beat * 200f) * beat) * Mathf.Exp(-beat * 30f) * ((int)(t / 0.25f) % 4 == 2 ? 0.4f : 0.8f);
            float crackle = rnd.NextDouble() < 0.0008 ? (float)(rnd.NextDouble() - 0.5) : 0f;
            float x = mel * 0.5f + drone + drum * 0.5f + crackle;
            hp = 0.95f * (hp + x - prevX); prevX = x; lp += (hp - lp) * 0.45f;
            d[i] = lp;
        }
        Norm(d, 0.5f);
        var radio = AudioClip.Create("DhabaRadio", n, 1, SR, false); radio.SetData(d, 0);
        var rs = new GameObject("DhabaRadioSound").AddComponent<AudioSource>();
        rs.transform.SetParent(transform, false); rs.transform.position = dhabaRadio;
        rs.clip = radio; rs.loop = true; rs.spatialBlend = 1f; rs.minDistance = 1.2f; rs.maxDistance = 16f; rs.volume = 0.32f; rs.dopplerLevel = 0f; rs.Play();
        // men talking low: syllable-rate bursts of voiced noise at three pitches, round the charpais
        int m = SR * 9; var v = new float[m]; float a1 = 0f, a2 = 0f;
        for (int i = 0; i < m; i++)
        {
            float t = i / (float)SR, w = (float)(rnd.NextDouble() * 2 - 1);
            a1 += (w - a1) * 0.08f; a2 += (a1 - a2) * 0.3f;
            float s = 0f;
            for (int k = 0; k < 3; k++)
            {
                float rate = 3.6f + k * 0.9f, on = Mathf.PerlinNoise(t * 0.35f + k * 7f, k) > 0.48f ? 1f : 0f;
                float syl = Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * rate * t + k)) * on;
                s += syl * (a2 * 3f + 0.25f * Mathf.Sin(2f * Mathf.PI * (105f + k * 23f) * t));
            }
            v[i] = s;
        }
        Norm(v, 0.4f);
        var talk = AudioClip.Create("DhabaTalk", m, 1, SR, false); talk.SetData(v, 0);
        var ts = new GameObject("DhabaTalkSound").AddComponent<AudioSource>();
        ts.transform.SetParent(transform, false); ts.transform.position = A + new Vector3(7.5f, 1.1f, 19.2f);
        ts.clip = talk; ts.loop = true; ts.spatialBlend = 1f; ts.minDistance = 2f; ts.maxDistance = 18f; ts.volume = 0.3f; ts.dopplerLevel = 0f; ts.Play();
    }

    /// <summary>The dhaba at midnight: drivers eating and talking, the cook at the tandoor, a radio, nobody looking up.</summary>
    IEnumerator DhabaLife(CharacterNPC[] drivers, Transform[] cups)
    {
        var goals = new Transform[drivers.Length];
        for (int i = 0; i < drivers.Length; i++)
        {
            if (drivers[i] == null) continue;
            goals[i] = new GameObject("DriverHand" + i).transform; goals[i].SetParent(transform, false);
        }
        var next = new float[drivers.Length];
        for (int i = 0; i < next.Length; i++) next[i] = 2f + i * 3.5f;
        while (this != null)
        {
            for (int i = 0; i < drivers.Length; i++)
            {
                var n = drivers[i];
                if (n == null || cups[i] == null || Time.time < next[i]) continue;
                next[i] = Time.time + Random.Range(7f, 13f);
                StartCoroutine(Sip(n, goals[i], cups[i]));
            }
            yield return null;
        }
    }

    /// <summary>A driver lifts his glass of chai, drinks, and puts it back where it was.</summary>
    IEnumerator Sip(CharacterNPC n, Transform goal, Transform cup)
    {
        Vector3 rest = cup.position;
        goal.position = n.HandPosition(1);
        n.Hold(1, goal, 0.35f);
        yield return MoveGoal(goal, goal.position, rest + Vector3.up * 0.05f, 0.6f);
        cup.SetParent(goal, true);
        Vector3 mouth = n.EyePoint() - Vector3.up * 0.1f + n.transform.forward * 0.1f;
        yield return MoveGoal(goal, goal.position, mouth, 0.7f, 0.03f);
        yield return Wait(Random.Range(0.8f, 1.4f));
        yield return MoveGoal(goal, goal.position, rest + Vector3.up * 0.05f, 0.7f, 0.03f);
        cup.SetParent(transform, true);
        cup.position = rest; cup.rotation = Quaternion.identity;
        n.Release(1, 0.5f);
    }
}
