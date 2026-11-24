using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Routes for Acts IV and V (23 Nov 2026), in any build (old builds lack some fields: those checks become notes).
///   cafeminute  Café Irani from chapter select: one take from control to Raj's reaction to the first reply (the counter,
///               a sidestep along the biscuit case, sitting down with Raj, the first decision, his reaction)
///   cafefull    the same, then the rest of Act IV A: the follow-ups (or none), the danger beat, the kitchen (a rattle, the
///               searcher, one detection and its checkpoint), the alleys (caught three times, then hidden), the dhaba
///   house       Act IV B: the landing and the nameplate, Kamala, the clues, the laptop read and closed, Rohan's stop,
///               Kamala's look, the confrontation on the compass, the ally
///   reddy       Act IV C with PINNACLE_ROUTE_EVIDENCE=full|noid|noplate|heard|minimal: the folio's pieces, every input
///               device the route can drive, pause, held keys, back, locked pieces, weak pieces and their answers,
///               her signature, her ID, the last question on the compass, the ally
///   act5        Act V with PINNACLE_ROUTE_CASE=j17|j18|rec|emails|neither and PINNACLE_ROUTE_ENDING=0|1|2
///               (PINNACLE_ROUTE_RESUME=1 enters at the Gallery checkpoint; PINNACLE_ROUTE_QUIT=1 quits to the menu
///               before committing and resumes at the checkpoint): the shelter, the Gallery, every casket's card,
///               a step back, the ending, the credits, R back to the menu
/// PINNACLE_ROUTE_FAST=1 skips spoken lines (Space / Enter) where the run is about logic, not footage.
/// </summary>
public partial class PlayRoute
{
    bool Fast => Env("PINNACLE_ROUTE_FAST", "") == "1" || (Env("PINNACLE_ROUTE_SKIPTO", "") == "ending" && !endingPlaying);
    bool endingPlaying;               // PINNACLE_ROUTE_SKIPTO=ending: lines skipped until the casket is chosen, then all played
    static readonly string[] Clues = { "1_Laptop", "2_StickyNote", "3_NewspaperClippings", "4_HalfPackedSuitcase", "5_TwoChaiCups", "6_TornPhotograph", "7_BurnerPhone", "8_Diary", "9_BrokenWindowLatch" };

    IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
    IEnumerator Secs(float s) { double t0 = Now; while (Now - t0 < s) yield return null; }

    /// <summary>Waits until the act gives control back, pressing Space now and then in fast runs (and E on cards).</summary>
    IEnumerator UntilFree(object st, float timeout, string what)
    {
        double t0 = Now, lastSkip = Now;
        while (!FreeIn(st) && Now - t0 < timeout)
        {
            if (GetBool(st, "cardOpen")) { yield return Secs(Fast ? 0.6f : 2.2f); Tap(Key.E); yield return Frames(3); continue; }
            if (Fast && Now - lastSkip > 1.2 && Get(st, "dlgOptions") == null && !GetBool(st, "codeOpen")) { Tap(Key.Space); lastSkip = Now; }
            yield return null;
        }
        Check(FreeIn(st), what);
    }

    T Story<T>() where T : Component { return FindAnyObjectByType<T>(); }

    /// <summary>The player's look-only flag (seated, hidden), read by reflection: older builds do not have it.</summary>
    static bool LookOnlyNow() { var f = typeof(PriyaRoomPlayer).GetField("LookOnly", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static); return f != null && f.GetValue(null) is bool b && b; }

    IEnumerator EnterAct4(int branch, Action<SaveData> seed)
    {
        var d = SaveSystem.Data;
        d.actsCompleted = Mathf.Max(d.actsCompleted, 3);
        d.currentAct = 3;
        seed?.Invoke(d);
        d.branch = branch;
        SaveSystem.Save();
        section = "entry";
        tRequest = Now;
        StartSampling();
        Call(story, "OnMenuPlay", 4, false);
        Stamp("request");
        while (Story<Act4Story>() == null && Now - tRequest < 30f) yield return null;
        Check(Story<Act4Story>() != null, "Act IV loads (branch " + branch + ")");
    }

    void EnsurePad()
    {
        if (pad != null) return;
        try { pad = InputSystem.AddDevice<Gamepad>("RouteGamepad"); pad.MakeCurrent(); }
        catch (Exception e) { Note("no virtual gamepad: " + e.Message); }
    }

    IEnumerator PadPress(GamepadButton b)
    {
        if (pad == null) yield break;
        InputSystem.QueueStateEvent(pad, new GamepadState { buttons = (uint)(1 << (int)b) });
        yield return Frames(2);
        InputSystem.QueueStateEvent(pad, new GamepadState());
        yield return Frames(2);
    }

    // ================================================================== CAFÉ IRANI
    IEnumerator CafeMinute() { yield return CafeOpening(false); }
    IEnumerator CafeFull() { yield return CafeOpening(true); }

    IEnumerator CafeOpening(bool full)
    {
        yield return EnterAct4(0, null);
        var a4 = Story<Act4Story>();
        if (a4 == null) yield break;
        yield return UntilFree(a4, 70f, "the café hands over control");
        Stamp("first_control");
        var raj = Get(a4, "raj") as CharacterNPC;
        Transform caseT = FindDeep(a4.transform, "DisplayCase");
        Vector3 casePos = caseT != null ? caseT.position : new Vector3(2.9f, 1.0f, -2.72f);
        // ---- one take: from control to Raj's reaction
        Segment("cafeminute", true);
        section = "cafe_minute";
        yield return LookHold(casePos + Vector3.up * 0.15f, 70f, 0.8f);
        yield return WalkToIn(a4, new Vector3(2.4f, 0.1f, -1.55f), 0.3f, 14f);
        yield return LookHold(casePos + Vector3.up * 0.12f, 60f, 0.6f);
        // sideways along the case, looking in through the glass
        SyncLook();
        for (float t = 0; t < 1.3f; t += Time.deltaTime) { Hold(Key.A, true); AngleTo(casePos + Vector3.up * 0.1f, out float y, out float x); Steer(y, x, 90f, 90f); yield return null; }
        Hold(Key.A, false);
        for (float t = 0; t < 2.6f; t += Time.deltaTime) { Hold(Key.D, true); AngleTo(casePos + Vector3.up * 0.1f, out float y, out float x); Steer(y, x, 90f, 90f); yield return null; }
        Hold(Key.D, false);
        if (shots) yield return StillShot("cafe_case_sidestep");
        // sitting down with Raj
        yield return Use(a4, "Sit down with Raj", 1.6f);
        yield return WaitFor(() => StoryPhase(a4) == "Busy", 3f, "E sits Arjun down with Raj");
        if (raj != null && Has(raj, "seated")) Check(Prop(raj, "IsSeated") is bool rs && rs, "Raj is seated at his table");
        double sat = Now;
        while (Now - sat < 3f) yield return null;
        float eye = player.cam.position.y;
        Note("seated eye height " + eye.ToString("F2") + " m");
        // trust is asked before the phone (and greyed on the dial); his price leaves the location for the dhaba to volunteer
        yield return Dial(a4, 2, "Raj's first question", raj);
        if (Has(a4, "phoneHanded")) Check(GetBool(a4, "phoneHanded") && SaveSystem.Data.hasRecordings, "the phone was handed over before the first question");
        // his reaction to the reply
        yield return WaitFor(() => raj != null && raj.IsTalking, 15f, "Raj answers");
        yield return Secs(5f);
        Segment("cafeminute", false);
        if (!full) yield break;

        // ---- the rest of the conversation: in this build the follow-ups are optional ("Time to go")
        var c = CompassOf(a4);
        yield return WaitFor(() => DialOpen(c) || Get(a4, "dlgOptions") is string[] || GetBool(a4, "dangerStarted"), 40f, "the next decision (or the danger) comes");
        if (DialOpen(c) && (Get(c, "labels") as string[])?.Length == 4) yield return Dial(a4, 3, "leave it there", raj);
        else
            for (int k = 0; k < 2 && (DialOpen(c) || Get(a4, "dlgOptions") is string[]); k++)
            { yield return Dial(a4, k + 1, "an old-build follow-up", raj); yield return WaitFor(() => DialOpen(c) || Get(a4, "stageA") is int sa && sa >= 1 || GetBool(a4, "dangerStarted"), 40f, "next"); }
        if (Has(a4, "dangerStarted"))
        {
            yield return WaitFor(() => GetBool(a4, "dangerStarted"), 20f, "the conversation turns dangerous");
            yield return WaitFor(() => LookOnlyNow(), 6f, "Arjun's head is free for a moment");
            var watcher = Get(a4, "watcher") as CharacterNPC;
            bool look = Env("PINNACLE_ROUTE_LOOK", "1") == "1";
            if (look && watcher != null)
            {
                // the player turns to look: by mouse, as a player would
                double l0 = Now;
                while (LookOnlyNow() && Now - l0 < 2.6)
                {
                    AngleTo(watcher.EyePoint(), out float y, out float x);
                    float dy = Mathf.DeltaAngle(player.Yaw, y);
                    mouseDelta = new Vector2(Mathf.Clamp(dy * 3f, -40f, 40f), Mathf.Clamp((player.Pitch - x) * 3f, -20f, 20f));
                    yield return null;
                }
                mouseDelta = Vector2.zero;
            }
            yield return WaitFor(() => !LookOnlyNow(), 5f, "the moment passes");
            Check(GetBool(a4, "dangerNoticed") == look, "danger noticed by the player: " + GetBool(a4, "dangerNoticed") + " (looked: " + look + "; the camera turns for them only if they did not)");
        }
        yield return WaitFor(() => Get(a4, "stageA") is int sb && sb >= 1, 30f, "the café part ends");

        // ---- the kitchen
        section = "kitchen";
        Segment("kitchen", true);
        yield return UntilFree(a4, 30f, "the kitchen hands over control");
        var pots = Get(a4, "potObjs") as List<Transform>;
        if (pots != null && pots.Count > 0)
        {
            // brush the nearest pot on purpose: it rattles where it is, and someone outside hears it
            Transform near = null; float best = 99f;
            foreach (var p in pots) { if (p == null) continue; float dd = Vector3.Distance(p.position, player.transform.position); if (dd < best) { best = dd; near = p; } }
            if (near != null) { yield return WalkToIn(a4, near.position + (player.transform.position - near.position).normalized * 0.3f, 0.2f, 6f); }
            yield return Secs(0.4f);
            Check(Get(a4, "potRattles") is int pr && pr >= 1, "a pot touched rattles (" + Get(a4, "potRattles") + " rattles) and the act does not reset");
            yield return WaitFor(() => Get(a4, "searcherNpc") != null, 6f, "the searcher comes to the door after footsteps");
            // stand in the open in his torchlight: a real detection, and the local checkpoint
            int det0 = Get(a4, "kitchenDetections") is int d0 ? d0 : 0;
            var k = new Vector3(20f, 0f, 0f);
            PlaceAt(k + new Vector3(-2.6f, 0.1f, -0.2f), 270f, 0f);
            yield return WaitFor(() => (Get(a4, "kitchenDetections") is int d1 ? d1 : 0) > det0, 12f, "standing in his torchlight gets Arjun seen");
            yield return UntilFree(a4, 25f, "back at the last cover after the detection");
            Note("after detection: player at " + player.transform.position.ToString("F1") + ", state " + Get(a4, "kitchenState"));
            // then from cover to cover while his torch is turned away
            Vector3[] path = { k + new Vector3(-1.25f, 0.1f, 0.35f), k + new Vector3(0.2f, 0.1f, 1.05f), k + new Vector3(2.15f, 0.1f, -0.75f), k + new Vector3(3.6f, 0.1f, 0f) };
            foreach (var w in path) { yield return WalkToIn(a4, w, 0.35f, 10f); if (!FreeIn(a4)) yield return UntilFree(a4, 25f, "control after a detection on the way"); }
            if (Vector3.Distance(player.transform.position, k + new Vector3(3.6f, 0.1f, 0f)) > 1.2f) { Note("placed at the kitchen exit"); PlaceAt(k + new Vector3(3.55f, 0.1f, 0f), 90f, 0f); }
        }
        Segment("kitchen", false);
        // ---- the alleys: three times in the open, then hidden
        section = "alleys";
        var A = new Vector3(40f, 0f, 0f);
        yield return UntilFree(a4, 30f, "the alleys hand over control");
        yield return WalkToIn(a4, A + new Vector3(0f, 0.1f, 1.1f), 0.5f, 14f);
        yield return WalkToIn(a4, A + new Vector3(5f, 0.1f, 1.1f), 0.5f, 10f);
        yield return WalkToIn(a4, A + new Vector3(9f, 0.1f, 3.8f), 0.5f, 10f);
        for (int retry = 0; retry < 3; retry++)
        {
            int r0 = Get(a4, "alleyRetries") is int ar ? ar : 0;
            yield return WaitFor(() => (Get(a4, "alleyRetries") is int ar2 ? ar2 : 0) > r0, 40f, "caught in the open (try " + (retry + 1) + ")");
            yield return UntilFree(a4, 30f, "control back after being caught (try " + (retry + 1) + ")");
            var rj = Get(a4, "raj") as CharacterNPC;
            Check(!PriyaRoomPlayer.Locked && !LookOnlyNow() && Mathf.Abs(player.cam.localPosition.y - (Get(a4, "standEye") is float se && se > 0f ? se : 1.62f)) < 0.05f
                  && (rj == null || !(Prop(rj, "IsSeated") is bool rjs && rjs)) && FindDeep(a4.transform, "Searcher1") == null,
                  "after try " + (retry + 1) + ": standing, unlocked, Raj on his feet, no searcher or torch left over (eye " + player.cam.localPosition.y.ToString("F2") + ")");
            yield return WalkToIn(a4, A + new Vector3(9f, 0.1f, 3.8f), 0.5f, 10f);
        }
        yield return Use(a4, "Hide behind the dumpster", 2.0f);
        var raj2 = Get(a4, "raj") as CharacterNPC;
        // once he is down in cover (his walk and his crouch done), he holds still: measured across the ground
        yield return WaitFor(() => raj2 == null || (Prop(raj2, "IsSeated") is bool rsd && rsd), 6f, "Raj gets down in cover");
        yield return Secs(1.2f);
        Vector3 rajAt = raj2 != null ? raj2.transform.position : Vector3.zero;
        float drift = 0f; double h0 = Now;
        while (Now - h0 < 5f) { if (raj2 != null) { var dv = raj2.transform.position - rajAt; dv.y = 0f; drift = Mathf.Max(drift, dv.magnitude); } yield return null; }
        Check(raj2 == null || ((Prop(raj2, "IsSeated") is bool r2s && r2s) && drift < 0.05f), "Raj crouches in cover and holds still (moved " + (drift * 100f).ToString("F1") + " cm while hidden)");
        // ---- the dhaba
        section = "dhaba";
        yield return UntilFree(a4, 60f, "the searchers pass and control returns");
        yield return WalkToIn(a4, A + new Vector3(9f, 0.1f, 15f), 0.6f, 20f);
        yield return WalkToIn(a4, A + new Vector3(9f, 0.1f, 17f), 0.6f, 8f);
        yield return WaitFor(() => SaveSystem.Data.allyRaj, 90f, "Raj is an ally after the dhaba");
        bool lead = SaveSystem.Data.evidence.Contains("Raj: Priya is at the old NGO shelter by the river. He told Deshpande's men she went to Nagpur.");
        Check(lead && SaveSystem.Data.hasRecordings, "the recordings and the location lead are both in the case file whichever questions were asked");
        yield return WaitFor(() => SaveSystem.Data.actsCompleted >= 4, 40f, "Act IV is recorded as complete");
    }

    // ================================================================== THE KAPOOR HOUSE
    IEnumerator House()
    {
        yield return EnterAct4(1, null);
        var a4 = Story<Act4Story>();
        if (a4 == null) yield break;
        Segment("house", true);
        yield return UntilFree(a4, 70f, "the house hands over control (on the landing)");
        var plate = FindDeep(a4.transform, "NameplateMount");
        Check(plate != null, "flat 301's KAPOOR nameplate is beside the open door (as in Act I)");
        if (plate != null) { yield return LookHold(plate.position, 60f, 1.0f); if (shots) yield return StillShot("house_landing_nameplate"); }
        yield return WalkToIn(a4, new Vector3(-3.6f, 0.1f, -3.0f), 0.4f, 10f);
        yield return UntilFree(a4, 80f, "Kamala's account ends and the search begins");
        if (shots) { yield return LookHold(new Vector3(-1.2f, 0.9f, -3.5f), 60f, 0.8f); yield return StillShot("house_almirah_trail"); }
        yield return Use(a4, "Examine the boot print", 1.4f);
        yield return UntilFree(a4, 30f, "the print noted");
        yield return Use(a4, "Pick up the visitor pass", 1.3f);
        yield return UntilFree(a4, 30f, "the pass noted");
        yield return Use(a4, "Look at Rohan's laptop", 1.2f);
        yield return UntilFree(a4, 40f, "the email read; control back");
        Check(!GetBool(a4, "confronting") && SaveSystem.Data.hasEmails, "reading the laptop does not start the confession (emails saved)");
        if (Has(a4, "laptopClosed"))
        {
            yield return Use(a4, "Close the laptop", 1.2f);
            yield return WaitFor(() => GetBool(a4, "laptopClosed"), 5f, "the laptop closes");
            yield return Secs(1.8f);
            Check(GetBool(a4, "rohanStoppedAtClick") && GetBool(a4, "kamalaLookedAtRohan"), "Rohan stops at the click; Kamala looks at him before anyone speaks");
            yield return UntilFree(a4, 10f, "the room is the player's again");
            yield return Secs(2f);
            Check(!GetBool(a4, "confronting") && FreeIn(a4), "silence: nothing starts until the player turns to Rohan");
            yield return Use(a4, "Ask Rohan about the emails", 1.4f);
        }
        var rohan = Get(a4, "rohan") as CharacterNPC;
        yield return Dial(a4, 0, "Rohan's question", rohan);
        yield return WaitFor(() => SaveSystem.Data.allyRohan, 120f, "Rohan is an ally");
        Segment("house", false);
        yield return WaitFor(() => SaveSystem.Data.actsCompleted >= 4, 40f, "Act IV is recorded as complete");
    }

    // ================================================================== INSPECTOR REDDY
    static object FolioOf(object st) { return Get(st, "folio"); }
    static bool FolioOpen(object f) { return f != null && Prop(f, "IsOpen") is bool b && b; }
    static int FolioFocus(object f) { return f != null && Prop(f, "Focused") is int i ? i : -9; }

    IEnumerator Reddy()
    {
        string ev = Env("PINNACLE_ROUTE_EVIDENCE", "full");
        yield return EnterAct4(2, d =>
        {
            d.hasIdCard = ev == "full" || ev == "noplate" || ev == "heard";
            d.hasPlate = ev == "full" || ev == "noid";
            d.clues.Clear();
            foreach (var c in Clues) if (ev != "minimal" || c != "3_NewspaperClippings") d.clues.Add(c);
            d.currentAct = 2;
            if (ev == "heard" || ev == "full") SaveRules.AddEvidence(d, "A black SUV followed Priya's auto.", 2);
            d.currentAct = 3;
        });
        var a4 = Story<Act4Story>();
        if (a4 == null) yield break;
        yield return UntilFree(a4, 70f, "the police street hands over control");
        var reddy = Get(a4, "reddy") as CharacterNPC;
        Segment("reddy", true);
        yield return Use(a4, "Talk to Inspector Reddy", 2.0f);
        // lip-sync on her lines: the claim before the folio (P, B, M in "Priya Kapoor", "crime", "missing persons")
        yield return WaitFor(() => reddy != null && reddy.IsTalking && Prop(reddy, "VoiceSource") is AudioSource va && va.clip != null && va.clip.name == "a4c_reddy_r1", 60f, "her claim is spoken");
        yield return MouthWatch(reddy, "lip-sync, Reddy's claim", () => !reddy.IsTalking, 14f);
        Check(lastMouth.x > 40 && lastMouth.z >= 2, "lip-sync: her claim drives the mouth from its track with lip closures (" + lastMouth.x + " viseme frames, " + lastMouth.z + " closures)");
        var f = FolioOf(a4);
        if (f == null)
        {
            // the committed game: the numbered panel; the sign-out sheet (2), then Priya's files (3), then the question
            Note("no evidence folio in this build: answered with numbers on the panel");
            for (int round = 0; round < 2; round++)
            {
                yield return WaitFor(() => Get(a4, "dlgOptions") is string[], 90f, "the evidence panel opens (round " + (round + 1) + ")");
                if (shots && round == 0) yield return StillShot("reddy_panel_before");
                yield return Secs(1.5f);
                Tap(round == 0 ? Key.Digit2 : Key.Digit3);
                yield return WaitFor(() => !(Get(a4, "dlgOptions") is string[]), 5f, "the panel closes");
            }
            yield return WaitFor(() => Get(a4, "dlgOptions") is string[] || DialOpen(CompassOf(a4)), 90f, "the last question comes");
            yield return Dial(a4, 1, "Reddy's last question", reddy);
            yield return WaitFor(() => SaveSystem.Data.allyReddy, 90f, "Reddy is an ally");
            Segment("reddy", false);
            yield break;
        }
        // hold Space across the folio's opening: it must not present anything
        Hold(Key.Space, true);
        yield return WaitFor(() => FolioOpen(f), 90f, "the evidence goes on the counter and the folio opens");
        yield return Secs(0.5f);
        Hold(Key.Space, false);
        var items = Prop(f, "Items") as System.Collections.IList;
        var names = new List<string>(); var keys = new List<string>();
        if (items != null) foreach (var it in items) { names.Add(Get(it, "name") as string); keys.Add(Get(it, "key") as string); }
        int want = ev == "full" || ev == "heard" ? 5 : ev == "minimal" ? 2 : 4;          // heard: ID, sheet, files, cuttings, Bhaskar
        Check(items != null && items.Count == want, "pieces on the counter: " + (items != null ? items.Count : 0) + " (wanted " + want + " for '" + ev + "'): " + string.Join(" | ", names));
        Check(FolioOpen(f) && FolioFocus(f) == -1, "Space held across the opening presented nothing and picked nothing");
        if (shots) yield return StillShot("reddy_folio_open_" + ev);
        // a number only picks up (inspects)
        Tap(Key.Digit2);
        yield return Frames(3);
        Check(FolioOpen(f) && FolioFocus(f) == 1, "a number key picks up a piece and does not present it (focus " + FolioFocus(f) + ")");
        // a number and Enter in the same moment: it inspects, it does not present
        Hold(Key.Digit1, true); Hold(Key.Enter, true);
        yield return Frames(2);
        Hold(Key.Digit1, false); Hold(Key.Enter, false);
        yield return Frames(3);
        Check(FolioOpen(f), "one input never both inspects and presents (Digit1 + Enter together left the folio open, focus " + FolioFocus(f) + ")");
        // pause: nothing behind the menu, the same piece in hand after
        int before = FolioFocus(f);
        Tap(Key.Escape); yield return Frames(3);
        Check(GameState.Paused, "Escape pauses over the folio");
        Tap(Key.RightArrow); yield return Frames(3); Tap(Key.Enter); yield return Frames(3);
        Check(FolioOpen(f) && FolioFocus(f) == before, "keys pressed while paused do nothing");
        Tap(Key.Escape); yield return Frames(3);
        Check(!GameState.Paused && FolioOpen(f) && FolioFocus(f) == before, "resume returns to the open folio with the same piece picked up");
        // back puts it down
        Tap(Key.Backspace); yield return Frames(3);
        Check(FolioOpen(f) && FolioFocus(f) == -1, "Backspace puts the piece down again");
        // the mouse: sideways travel picks up, a click presents a WEAK piece first (Priya's files in round 1)
        int usb = keys.IndexOf("usb");
        if (vm != null && usb >= 0)
        {
            for (int i = 0; i <= usb; i++) { mouseDelta = new Vector2(80f, 0f); yield return Frames(2); mouseDelta = Vector2.zero; yield return Frames(3); }
            Check(FolioFocus(f) == usb, "sideways mouse travel walks the pieces (focus " + FolioFocus(f) + ", wanted " + usb + ")");
            yield return Secs(0.35f);
            mouseClick = true;
            yield return Frames(4);
            Check(!FolioOpen(f), "the click presents the piece in hand");
            yield return WaitFor(() => reddy != null && reddy.IsTalking, 8f, "Reddy answers the weak piece");
            var vs = Prop(reddy, "VoiceSource") as AudioSource;
            Check(vs != null && vs.clip != null && vs.clip.name == "a4c_weak1_usb", "her answer says what it fails to establish (" + (vs != null && vs.clip != null ? vs.clip.name : "-") + ")");
            // pause one second in: the voice and the mouth hold where they are, and go on from there
            yield return Secs(1.0f);
            Tap(Key.Escape);
            yield return WaitFor(() => GameState.Paused, 2f, "the pause takes effect");
            yield return Frames(2);                                  // measured from the moment the pause holds
            float tBefore = vs != null ? vs.time : 0f, jawBefore = MouthDegOf(reddy);
            double pz = Time.realtimeSinceStartupAsDouble; float jawDrift = 0f;
            while (Time.realtimeSinceStartupAsDouble - pz < 1.0) { jawDrift = Mathf.Max(jawDrift, Mathf.Abs(MouthDegOf(reddy) - jawBefore)); yield return null; }
            float tPaused = vs != null ? vs.time : 0f;
            Tap(Key.Escape); yield return Frames(3);
            Check(GameState.Paused == false && Mathf.Abs(tPaused - tBefore) < 0.12f && jawDrift < 1.5f, "lip-sync: paused mid-line, her voice holds (" + tBefore.ToString("F2") + " -> " + tPaused.ToString("F2") + " s) and her jaw stays put (moved " + jawDrift.ToString("F1") + " deg)");
            // skip mid-word: the mouth settles to rest
            yield return Secs(0.6f);
            Tap(Key.Space);
            double sk = Now; float settle = -1f;
            while (Now - sk < 1.0) { if (MouthModeOf(reddy) == 0 && Mathf.Abs(MouthDegOf(reddy)) < 0.3f) { settle = (float)(Now - sk); break; } yield return null; }
            Check(settle >= 0f && settle < 0.3f, "lip-sync: cut off mid-word, her mouth is at rest in " + (settle < 0f ? "more than 1" : settle.ToString("F2")) + " s");
            yield return WaitFor(() => FolioOpen(f), 20f, "the folio comes back without her question read again");
        }
        // the keyboard: right to the sign-out sheet, settle, Enter
        int so = keys.IndexOf("signout");
        Tap(Key.Backspace); yield return Frames(2);
        for (int i = 0; i <= so; i++) { Tap(Key.RightArrow); yield return Frames(3); }
        Check(FolioFocus(f) == so, "the arrows walk the pieces (focus " + FolioFocus(f) + ")");
        if (shots) yield return StillShot("reddy_signout_inspected");
        yield return Secs(0.35f);
        Tap(Key.Enter);
        yield return WaitFor(() => reddy != null && reddy.IsTalking, 10f, "she reacts to her own signature");
        var v2 = Prop(reddy, "VoiceSource") as AudioSource;
        Check(v2 != null && v2.clip != null && v2.clip.name == "a4c_reddy_sig", "she recognises her signature (" + (v2 != null && v2.clip != null ? v2.clip.name : "-") + ")");
        yield return MouthWatch(reddy, "lip-sync, the next line after a skipped one", () => !reddy.IsTalking, 6f);
        Check(lastMouth.x > 10, "lip-sync: after a skipped line the next one drives her mouth again (" + lastMouth.x + " viseme frames, " + lastMouth.z + " closures)");
        // round 2: the accepted sheet is locked; the gamepad presents the files
        yield return WaitFor(() => FolioOpen(f) && (Get(a4, "reddyRound") is int rr && rr == 2), 60f, "round 2's folio opens");
        yield return Secs(0.4f);
        Tap(Key.Backspace); yield return Frames(2);
        for (int i = 0; i <= so; i++) { Tap(Key.RightArrow); yield return Frames(3); }
        yield return Secs(0.35f);
        Tap(Key.Enter); yield return Frames(4);
        Check(FolioOpen(f), "a piece already accepted is locked: Enter does not present it again");
        EnsurePad();
        if (pad != null)
        {
            Tap(Key.Backspace); yield return Frames(2);
            int target = keys.IndexOf("usb");
            for (int i = 0; i <= target; i++) yield return PadPress(GamepadButton.DpadRight);
            Check(FolioFocus(f) == target, "the d-pad walks the pieces (focus " + FolioFocus(f) + ")");
            yield return Secs(0.35f);
            yield return PadPress(GamepadButton.South);
            Check(!FolioOpen(f), "the pad's south button presents");
        }
        else { Tap(Key.Digit3); yield return Secs(0.35f); Tap(Key.Enter); }
        Note("Arduino input: not driven by the route (no live board); GameInput.Look and the D4 option button feed the folio's code path");
        yield return WaitFor(() => SaveSystem.Data.allyReddy || Get(a4, "dlgOptions") is string[] || DialOpen(CompassOf(a4)), 90f, "the last question comes");
        yield return Dial(a4, 1, "Reddy's last question", reddy);
        yield return WaitFor(() => SaveSystem.Data.allyReddy, 90f, "Reddy is an ally");
        var log = Get(a4, "presentedLog") as List<string>;
        Note("presented: " + (log != null ? string.Join(", ", log) : "-"));
        Segment("reddy", false);
        yield return WaitFor(() => SaveSystem.Data.actsCompleted >= 4, 40f, "Act IV is recorded as complete");
    }

    // ================================================================== ACT V
    IEnumerator ActFive()
    {
        string cs = Env("PINNACLE_ROUTE_CASE", "j18");
        int ending = int.Parse(Env("PINNACLE_ROUTE_ENDING", cs == "neither" ? "1" : cs == "j18" ? "0" : "2"), CultureInfo.InvariantCulture);
        bool resume = Env("PINNACLE_ROUTE_RESUME", "") == "1";
        var d = SaveSystem.Data;
        d.actsCompleted = 4; d.currentAct = 4;
        SaveSystem.ResetAct(5);
        d.clues.Clear(); d.evidence.Clear(); d.evidenceActs.Clear();
        int total = cs == "j18" ? 18 : cs == "j17" ? 17 : 12;
        foreach (var c in Clues) if (d.clues.Count < Mathf.Min(9, total)) d.clues.Add(c);
        for (int i = 0; d.clues.Count + d.evidence.Count < total; i++) SaveRules.AddEvidence(d, "Route evidence " + (i + 1), 2 + i % 3);
        d.hasRecordings = cs == "j18" || cs == "j17" || cs == "rec";
        d.hasEmails = cs == "emails";
        d.allyRaj = cs == "j18" || cs == "rec"; d.allyRohan = cs == "emails"; d.allyReddy = cs == "j17";
        d.branch = d.allyRaj ? 0 : d.allyRohan ? 1 : d.allyReddy ? 2 : 0;
        d.act5Stage = resume ? 1 : 0;
        int seen0 = SaveSystem.Profile.endingsSeen.Count;
        SaveSystem.Save();
        Note("case " + cs + ": evidence " + SaveSystem.EvidenceCount + ", recordings " + d.hasRecordings + ", emails " + d.hasEmails + ", ending wanted " + ending);
        tRequest = Now; section = "entry"; StartSampling();
        Call(story, "OnMenuPlay", 5, resume);
        Act5Story a5 = null;
        while (a5 == null && Now - tRequest < 30f) { a5 = Story<Act5Story>(); yield return null; }
        Check(a5 != null, "Act V loads" + (resume ? " at the Gallery checkpoint" : ""));
        if (a5 == null) yield break;
        if (!resume)
        {
            Segment("shelter", true);
            yield return UntilFree(a5, 60f, "the shelter hands over control");
            Stamp("first_control");
            section = "shelter";
            if (census) Census(a5.transform, "census_act5_shelter.txt");
            if (shots) { yield return LookHold(new Vector3(-3.6f, 0.4f, 3.4f), 50f, 0.8f); yield return StillShot("shelter_sleepers"); }
            yield return WalkToIn(a5, new Vector3(4.2f, 0.1f, 2.2f), 0.6f, 20f);
            var sv = Get(a5, "stairVoice") as Transform;
            yield return Secs(1.5f);
            Check(sv != null && sv.GetComponent<AudioSource>() != null && sv.GetComponent<AudioSource>().isPlaying, "Priya's voice comes down the stairs from the lamp at the top");
            yield return Use(a5, "Climb the stairs", 1.2f);
            yield return UntilFree(a5, 20f, "upstairs");
            section = "upstairs";
            var priya = Get(a5, "priya") as CharacterNPC;
            if (priya != null) yield return WalkToIn(a5, priya.transform.position + (player.transform.position - priya.transform.position).normalized * 1.9f, 0.3f, 10f);
            yield return WaitFor(() => GetBool(a5, "priyaClosedFile"), 8f, "she shuts the file when she sees him");
            yield return WaitFor(() => GetBool(a5, "priyaHoldsFile"), 4f, "and holds it to her before her first line");
            if (shots) yield return StillShot("shelter_priya_file");
            yield return WaitFor(() => priya != null && priya.IsTalking, 6f, "her first line");
            Segment("shelter", false);
            if (Fast) { yield return Secs(2f); Tap(Key.Enter); }
        }
        Segment("gallery", true);
        section = "to_gallery";
        yield return UntilFree(a5, Fast ? 150f : 320f, "the Gallery hands over control");        // every line played: Priya, the way in, his welcome
        Stamp(resume ? "first_control" : "gallery_control");
        section = "gallery";
        if (census) Census(a5.transform, "census_act5_gallery.txt");
        Check(SaveSystem.Data.act5Stage == 1, "the Gallery checkpoint is saved");
        // every casket's card: sealed or open by the rules, and a step back on the first open one
        bool[] sealedWant = { SaveSystem.EvidenceCount < 18, false, !(d.hasRecordings || d.hasEmails) };
        string[] labels = { "Examine the bronze casket", "Examine the bone casket", "Examine the iron casket" };
        bool steppedBack = false;
        for (int i = 0; i < 3; i++)
        {
            if (i == ending) continue;
            yield return Use(a5, labels[i], 1.3f);
            yield return WaitFor(() => GetBool(a5, "cardOpen"), 30f, labels[i] + ": its card opens");
            string card = Get(a5, "cardBody") as string ?? "";
            Check(card.Contains("SEALED") == sealedWant[i], labels[i] + ": " + (sealedWant[i] ? "sealed" : "open") + " as the rules say (evidence " + SaveSystem.EvidenceCount + ")");
            if (shots) yield return StillShot("gallery_card_" + i);
            yield return Secs(1.2f); Tap(Key.E); yield return Frames(3);
            if (!sealedWant[i])
            {
                var c = CompassOf(a5);
                yield return WaitFor(() => DialOpen(c) || Get(a5, "dlgOptions") is string[], 30f, "the confirmation opens");
                yield return Dial(a5, 1, "step back from " + labels[i], null);
                steppedBack = true;
            }
            yield return UntilFree(a5, 30f, "back at the table after " + labels[i]);
        }
        Check(Get(a5, "chosen") is int ch0 && ch0 < 0, "inspecting and stepping back chose nothing" + (steppedBack ? " (stepped back once)" : ""));
        if (Env("PINNACLE_ROUTE_QUIT", "") == "1")
        {
            // quit to the menu before committing, then Continue: the Gallery again, nothing recorded
            Call(a5, "SetPaused", true); yield return Frames(3);
            Call(a5, "SetPaused", false); Call(builder, "Build");
            yield return Secs(2.5f);
            story = FindAnyObjectByType<PriyaRoomStory>();
            builder = FindAnyObjectByType<PriyaRoomBuilder>();
            if (builder != null && builder.PlayerCtrl != null) player = builder.PlayerCtrl;       // the menu rebuilt the player
            Call(story, "OnMenuPlay", 5, true);
            a5 = null; double q0 = Now;
            while (a5 == null && Now - q0 < 30f) { a5 = Story<Act5Story>(); yield return null; }
            yield return UntilFree(a5, 150f, "Continue enters the Gallery again");
            Check(SaveSystem.Profile.endingsSeen.Count == seen0, "quitting before committing records no ending");
        }
        // the chosen casket
        yield return Use(a5, labels[ending], 1.3f);
        yield return WaitFor(() => GetBool(a5, "cardOpen"), 30f, "the chosen casket's card");
        yield return Secs(1.2f); Tap(Key.E);
        yield return WaitFor(() => DialOpen(CompassOf(a5)) || Get(a5, "dlgOptions") is string[], 30f, "its confirmation opens");
        yield return Dial(a5, 0, "open " + labels[ending], null);
        yield return WaitFor(() => Get(a5, "chosen") is int ch && ch == ending, 5f, "the casket is chosen");
        Segment("gallery", false);
        Segment("ending", true);
        section = "ending";
        endingPlaying = true;
        // the ending plays; cards are closed, decisions (none expected) answered, lines skipped in fast runs
        double e0 = Now, lastSkip = Now;
        while ((Get(a5, "creditsStart") is float cst ? cst : -1f) <= 0f && Now - e0 < 420f)
        {
            if (GetBool(a5, "cardOpen")) { yield return Secs(Fast ? 0.8f : 3.5f); Tap(Key.E); yield return Frames(3); }
            if (Fast && Now - lastSkip > 1.5) { Tap(Key.Space); lastSkip = Now; }
            yield return null;
        }
        Segment("ending", false);
        Note("ending log: " + (Get(a5, "endingLog") as string));
        Check((Get(a5, "creditsStart") is float cs2 ? cs2 : -1f) > 0f, "the credits roll");
        Check(SaveSystem.Profile.endingsSeen.Contains(ending), "the ending is recorded when its credits roll (" + string.Join(",", SaveSystem.Profile.endingsSeen) + ")");
        yield return Secs(4f);
        Tap(Key.R);
        yield return Secs(2.5f);
        Check(Story<Act5Story>() == null && FindAnyObjectByType<PriyaRoomStory>() != null, "R after the credits returns to the title menu");
    }

    // ================================================================== Act III stills
    IEnumerator LibraryStills(object a3)
    {
        // the two bookcases nearest the aisle, from where Arjun walks past them
        PlaceAt(new Vector3(0.0f, 0.1f, -4.2f), -25f, 4f);
        yield return Frames(6);
        yield return StillShot("library_case_left");
        PlaceAt(new Vector3(0.2f, 0.1f, -3.6f), 30f, 6f);
        yield return Frames(6);
        yield return StillShot("library_case_right");
        PlaceAt(new Vector3(-5f, 0.1f, -9.4f), 40f, 0f);
        yield return Frames(4);
    }

    IEnumerator TempleStills(object a3)
    {
        var T = new Vector3(-90f, 0f, 0f);
        Vector3 murti = T + new Vector3(0f, 2.55f, 11.2f);
        // a devotee's eye height (1.6 m) on the plinth, straight on, and three-quarter from the steps
        Vector3 back = player.transform.position; float by = player.Yaw;
        PlaceAt(T + new Vector3(0f, 1.25f, 7.0f), 0f, 0f);
        yield return LookHold(murti, 120f, 0.3f);
        yield return StillShot("temple_murti_front");
        PlaceAt(T + new Vector3(-1.4f, 1.25f, 7.2f), 0f, 0f);
        yield return LookHold(murti, 120f, 0.3f);
        yield return StillShot("temple_murti_threequarter");
        PlaceAt(T + new Vector3(-2.9f, 0.1f, -9.6f), 0f, 0f);
        yield return LookHold(T + new Vector3(-4.45f, 1.4f, -8.5f), 120f, 0.3f);
        yield return StillShot("temple_ganesha");
        PlaceAt(T + new Vector3(5.5f, 0.1f, -10.2f), 180f, 0f);
        yield return LookHold(T + new Vector3(5.5f, 1.1f, -12.4f), 120f, 0.3f);
        yield return StillShot("temple_harishankar_desk");
        PlaceAt(back, by, 0f);
        yield return Frames(4);
    }
}
