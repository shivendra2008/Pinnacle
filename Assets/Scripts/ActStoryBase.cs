using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Switches between acts: builds the next act's world under a fresh root, keeps the player, frees the old act.</summary>
public static class ActLoader
{
    public static void Load(int act, PriyaRoomBuilder builder, PriyaRoomPlayer player, bool resume)
    {
        var old = builder.ExtraRoot;
        var root = new GameObject("Act" + act);
        player.transform.SetParent(root.transform, true);
        builder.ExtraRoot = root;
        builder.Root.gameObject.SetActive(false);
        if (old != null) UnityEngine.Object.Destroy(old);
        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (act == 2)
        {
            if (!resume) SaveSystem.ResetAct(2);
            var m = root.AddComponent<MarketStory>();
            m.builder = builder; m.player = player; m.resume = resume;
            return;
        }
        if (!resume) SaveSystem.ResetAct(act);      // a fresh start must not inherit an old run's progress
        ActStoryBase story;
        if (act == 3) story = root.AddComponent<Act3Story>();
        else if (act == 4) story = root.AddComponent<Act4Story>();
        else story = root.AddComponent<Act5Story>();
        story.builder = builder; story.player = player; story.resume = resume;
    }
}

/// <summary>Character looks for the later acts.</summary>
public static class Cast
{
    static Color C(float r, float g, float b) { return new Color(r, g, b); }

    static NpcStyle Casual(float h, Color shirt, Color pants, Color skin, Color hair)
    {
        return new NpcStyle { model = "Characters/Casual", height = h }
            .Set("skin_darker", skin * 0.85f).Set("skin", skin).Set("hair", hair).Set("eyebrows", hair).Set("eye", C(0.03f, 0.02f, 0.02f))
            .Set("lightbrown", shirt).Set("lightblue", pants).Set("red_dark", C(0.15f, 0.1f, 0.08f)).Set("white", C(0.4f, 0.36f, 0.32f));
    }

    static NpcStyle Woman(float h, Color dress, Color skin, Color hair, Color trim)
    {
        return new NpcStyle { model = "Characters/Kamala", height = h }
            .Set("skin", skin).Set("red", hair).Set("green", dress).Set("gold", trim).Set("brown", C(0.05f, 0.04f, 0.04f));
    }

    static NpcStyle Suit(float h, Color suit, Color shirt, Color skin, Color hair)
    {
        return new NpcStyle { model = "Characters/Suit", height = h }
            .Set("skin", skin).Set("hair", hair).Set("eyebrows", hair).Set("eye", C(0.02f, 0.02f, 0.02f))
            .Set("suit", suit).Set("white", shirt).Set("tie", suit * 0.6f).Set("black", C(0.03f, 0.03f, 0.03f));
    }

    public static NpcStyle Sharma() { return Casual(1.62f, C(0.55f, 0.5f, 0.35f), C(0.35f, 0.32f, 0.25f), C(0.5f, 0.33f, 0.22f), C(0.75f, 0.75f, 0.75f)).Rb("Security_Male_01"); }
    public static NpcStyle Pandit() { return Casual(1.6f, C(0.95f, 0.5f, 0.1f), C(0.95f, 0.92f, 0.85f), C(0.55f, 0.36f, 0.24f), C(0.85f, 0.85f, 0.85f)).Rb("Male_Adult_15", C(1f, 0.62f, 0.26f)); }
    public static NpcStyle Devotee(int seed) { return seed % 2 == 0 ? NpcStyle.CrowdWoman(seed) : NpcStyle.Crowd(seed); }
    public static NpcStyle Raj() { return Suit(1.76f, C(0.32f, 0.33f, 0.36f), C(0.85f, 0.85f, 0.82f), C(0.62f, 0.44f, 0.3f), C(0.15f, 0.12f, 0.1f)).Rb("Business_Male_05"); }
    public static NpcStyle Rohan() { return Casual(1.72f, C(0.2f, 0.4f, 0.7f), C(0.15f, 0.15f, 0.2f), C(0.6f, 0.42f, 0.29f), C(0.05f, 0.04f, 0.04f)).Rb("Male_Adult_17"); }
    public static NpcStyle Kamala() { return NpcStyle.Kamala(); }
    public static NpcStyle Reddy() { return NpcStyle.Reddy(); }
    public static NpcStyle Priya() { return Woman(1.62f, C(0.93f, 0.92f, 0.88f), C(0.62f, 0.43f, 0.3f), C(0.04f, 0.03f, 0.03f), C(0.75f, 0.2f, 0.2f)).Rb("Female_Adult_03"); }
    public static NpcStyle Deshpande() { return Woman(1.6f, C(0.1f, 0.42f, 0.32f), C(0.64f, 0.46f, 0.33f), C(0.12f, 0.1f, 0.1f), C(0.95f, 0.78f, 0.3f)).Rb("Business_Female_03", C(0.75f, 0.95f, 0.85f)); }
    public static NpcStyle Judge() { return Suit(1.72f, C(0.04f, 0.04f, 0.05f), C(0.95f, 0.95f, 0.95f), C(0.55f, 0.38f, 0.26f), C(0.7f, 0.7f, 0.7f)).Rb("Male_Adult_03", C(0.5f, 0.5f, 0.52f)); }
    public static NpcStyle Lawyer() { return Suit(1.75f, C(0.06f, 0.06f, 0.07f), C(0.95f, 0.95f, 0.95f), C(0.6f, 0.42f, 0.3f), C(0.1f, 0.08f, 0.08f)).Rb("Business_Male_03"); }
    public static NpcStyle Curator() { return Suit(1.95f, C(0.16f, 0.03f, 0.05f), C(0.12f, 0.05f, 0.06f), C(0.72f, 0.6f, 0.52f), C(0.72f, 0.72f, 0.74f)).Rb("Business_Male_04", C(0.8f, 0.62f, 0.62f)); }
    public static NpcStyle Vikram() { return NpcStyle.Vikram(); }
    public static NpcStyle Owner() { return Casual(1.6f, C(0.9f, 0.9f, 0.88f), C(0.3f, 0.28f, 0.25f), C(0.68f, 0.52f, 0.4f), C(0.8f, 0.8f, 0.8f)).Rb("Male_Adult_03"); }
}

/// <summary>
/// Shared machinery for Acts III-V: an act is a coroutine script (Run) that alternates scripted beats (dialogue,
/// choices, puzzles, cutscenes) with free exploration that waits for the player to do something.
/// Provides voiced dialogue with subtitles, numbered choices, a code-entry puzzle, document cards, look-at
/// interaction with prompts, an objective + marker, evidence notes (J), pause, fades and title cards.
/// </summary>
public abstract class ActStoryBase : MonoBehaviour
{
    public PriyaRoomBuilder builder;
    public PriyaRoomPlayer player;
    public bool resume;

    protected Transform cam;
    protected Camera camComp;

    protected enum Phase { Intro, Free, Busy, Ending }
    protected Phase phase = Phase.Intro;

    protected abstract int ActNumber { get; }
    protected abstract string ActTitle { get; }
    protected abstract string ActSubtitle { get; }
    protected abstract void BuildWorld();
    protected abstract IEnumerator Run();
    protected virtual LookPreset Look { get { return LookPreset.Market; } }

    // audio
    protected AudioSource ambience, sfx, voice;
    protected AudioClip clickClip, dingClip, buzzClip, ringClip, stepClip;

    // ui state
    protected float fade = 1f, fadeTarget = 1f, fadeSpeed = 0.6f;
    protected string objective = "";
    protected Vector3? objectiveTarget; protected string objectiveName = "";
    string prompt = "";
    string sub; float subUntil;
    string toast; float toastUntil; bool toastBig;
    string titleA, titleB; float titleStart, titleEnd;
    protected string dlgSpeaker, dlgText; string[] dlgOptions; bool[] dlgDisabled; protected int menuResult;
    protected bool skipAll;
    bool paused, savedLocked, journalOpen;
    // document card
    string cardTitle, cardBody; bool cardOpen;
    // code entry
    bool codeOpen, codeDigits, codeHelpShown; string codeTitle, codeHint, codeBuf = "", codeMsg = "", codeHelpTitle, codeHelpBody; int codeMax;
    protected bool codeResult;
    // credits
    protected string[] creditLines; protected float creditsStart = -1f;
    GUIStyle sSub, sTitle, sTitle2, sSmall, sPanel, sHead, sPrompt, sMono;

    // ------------------------------------------------------------ interaction spots
    protected class Spot
    {
        public Transform t; public Vector3 offset; public string label;
        public Func<bool> active; public Action act; public float range = 3.2f, radius = 0.35f;
    }
    protected readonly List<Spot> spots = new List<Spot>();

    protected Spot AddSpot(Transform t, Vector3 offset, string label, Func<bool> active, Action act, float range = 3.2f, float radius = 0.35f)
    {
        var s = new Spot { t = t, offset = offset, label = label, active = active, act = act, range = range, radius = radius };
        spots.Add(s);
        return s;
    }

    // ====================================================================== LIFE
    protected virtual void Start()
    {
        cam = player.cam;
        camComp = cam.GetComponent<Camera>();
        PriyaRoomPlayer.Locked = true;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        SetupAudio();
        BuildWorld();
        CinematicLook.Apply(transform, camComp, Look);
        StartCoroutine(Run());
    }

    protected virtual void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame && !codeOpen && !cardOpen && phase != Phase.Ending) SetPaused(!paused);
        if (paused) return;
        if (kb != null && kb.jKey.wasPressedThisFrame && phase == Phase.Free) journalOpen = !journalOpen;
        if (kb != null && kb.enterKey.wasPressedThisFrame && phase == Phase.Busy && dlgOptions == null && !codeOpen && !cardOpen) skipAll = true;
        if (phase == Phase.Free) skipAll = false;
        fade = Mathf.MoveTowards(fade, fadeTarget, fadeSpeed * Time.deltaTime);
        UpdateInteraction();
        if (creditsStart > 0 && kb != null && kb.rKey.wasPressedThisFrame && Time.time - creditsStart > 3f) builder.Build();
    }

    void SetPaused(bool p)
    {
        paused = p;
        if (p)
        {
            savedLocked = PriyaRoomPlayer.Locked; PriyaRoomPlayer.Locked = true;
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

    protected virtual void OnDestroy()
    {
        Time.timeScale = 1f; AudioListener.pause = false;
        if (Keyboard.current != null) Keyboard.current.onTextInput -= OnCodeChar;
    }

    // ================================================================= HELPERS
    protected static AudioClip VO(string id) { return Resources.Load<AudioClip>("VO/" + id); }

    protected void Toast(string t, float dur = 4f, bool big = false) { toast = t; toastUntil = Time.time + dur; toastBig = big; }
    protected void ShowTitle(string a, string b, float dur) { titleA = a; titleB = b; titleStart = Time.time; titleEnd = Time.time + dur; }

    protected void AddEvidence(string e)
    {
        if (SaveSystem.Data.evidence.Contains(e)) return;
        SaveSystem.AddEvidence(e);
        sfx.PlayOneShot(dingClip, 0.5f);
        Toast("Added to the case file  (J)", 3f);
    }

    protected IEnumerator W(float seconds)
    {
        float t = 0;
        while (t < seconds && !skipAll) { t += Time.deltaTime; yield return null; }
    }

    protected IEnumerator Wait(float seconds) { yield return new WaitForSeconds(seconds); }

    protected bool Near(Vector3 p, float r)
    {
        Vector3 d = player.transform.position - p; d.y = 0;
        return d.magnitude < r;
    }

    protected IEnumerator Turn(Vector3 worldTarget, float dur)
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

    protected void Teleport(Vector3 pos, float yaw, float pitch = 0f)
    {
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = pos;
        cc.enabled = true;
        player.SetLook(yaw, pitch);
    }

    protected IEnumerator FadeOut(float speed = 1.2f) { fadeSpeed = speed; fadeTarget = 1f; while (fade < 0.99f) yield return null; }
    protected IEnumerator FadeIn(float speed = 1.0f) { fadeSpeed = speed; fadeTarget = 0f; yield return null; }

    /// <summary>Hand control to the player with an objective (and an optional gold marker).</summary>
    protected void BeginFree(string obj, Vector3? target = null, string targetName = "")
    {
        dlgSpeaker = null; dlgText = null; dlgOptions = null;
        AutoFocus.FocusOn = null;
        objective = obj; objectiveTarget = target; objectiveName = targetName;
        phase = Phase.Free;
        PriyaRoomPlayer.Locked = false;
    }

    protected void BeginBusy()
    {
        phase = Phase.Busy;
        PriyaRoomPlayer.Locked = true;
        journalOpen = false;
        prompt = "";
    }

    /// <summary>Act title card and fade-in.</summary>
    protected IEnumerator OpenAct()
    {
        phase = Phase.Intro;
        fade = 1f; fadeTarget = 1f;
        StartCoroutine(RampAmbience(0f, 0.8f, 4f));
        yield return W(0.5f);
        ShowTitle(ActTitle, ActSubtitle, 4.2f);
        yield return W(4.4f);
        fadeSpeed = skipAll ? 3f : 0.5f; fadeTarget = 0f;
        titleEnd = skipAll ? 0f : titleEnd;
        skipAll = false;
        phase = Phase.Busy;
    }

    /// <summary>Finish this act: save it as completed and move on to the next one.</summary>
    protected IEnumerator CloseAct(string nextTitle, string nextSubtitle)
    {
        BeginBusy();
        objective = ""; objectiveTarget = null;
        SaveSystem.CompleteAct(ActNumber);
        yield return FadeOut(0.6f);
        StartCoroutine(RampAmbience(ambience.volume, 0f, 1.5f));
        yield return new WaitForSeconds(1.5f);
        ActLoader.Load(ActNumber + 1, builder, player, false);
    }

    protected IEnumerator RampAmbience(float from, float to, float dur)
    {
        float t = 0;
        while (t < dur) { t += Time.deltaTime; ambience.volume = Mathf.Lerp(from, to, t / dur); yield return null; }
        ambience.volume = to;
    }

    // ================================================================ DIALOGUE
    /// <summary>A voiced line in the dialogue panel. E / Space skips it, Enter skips the whole conversation.</summary>
    protected IEnumerator Talk(string id, CharacterNPC npc = null)
    {
        dlgSpeaker = DialogueDB.Speaker(id); dlgText = DialogueDB.Text(id); dlgOptions = null;
        if (npc != null) AutoFocus.FocusOn = npc.transform;
        if (skipAll) { yield break; }
        var clip = VO(id);
        float dur = clip != null ? clip.length + 0.35f : Mathf.Max(2.2f, dlgText.Length * 0.06f);
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

    /// <summary>A voiced line shown as a subtitle (narration, phone calls), the player keeps looking around.</summary>
    protected IEnumerator Narrate(string id)
    {
        if (skipAll) yield break;
        var clip = VO(id);
        string text = DialogueDB.Speaker(id) + ":  " + DialogueDB.Text(id);
        float dur = clip != null ? clip.length + 0.4f : Mathf.Max(2.5f, text.Length * 0.06f);
        sub = text; subUntil = Time.time + dur;
        if (clip != null) { voice.Stop(); voice.clip = clip; voice.Play(); }
        yield return null;
        float t = 0;
        while (t < dur)
        {
            var kb = Keyboard.current;
            if (skipAll || (kb != null && kb.spaceKey.wasPressedThisFrame)) { voice.Stop(); break; }
            t += Time.deltaTime;
            yield return null;
        }
        sub = null;
    }

    /// <summary>Numbered options under the current line; waits for 1-4. Result in menuResult.</summary>
    protected IEnumerator Choose(string[] options, bool[] disabled = null)
    {
        dlgOptions = options; dlgDisabled = disabled;
        if (dlgText == null) { dlgText = " "; }
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
                else if (kb.digit5Key.wasPressedThisFrame) c = 4;
                if (c >= 0 && c < options.Length && (disabled == null || !disabled[c])) { menuResult = c; sfx.PlayOneShot(clickClip, 0.5f); }
            }
            yield return null;
        }
        dlgOptions = null; dlgDisabled = null;
    }

    protected void EndTalk() { dlgSpeaker = null; dlgText = null; dlgOptions = null; AutoFocus.FocusOn = null; }

    /// <summary>A document / photo / letter shown on screen until the player presses E, Space or Enter.</summary>
    protected IEnumerator ShowCard(string title, string body)
    {
        cardTitle = title; cardBody = body; cardOpen = true;
        sfx.PlayOneShot(clickClip, 0.5f);
        yield return null; yield return null;
        while (true)
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) break;
            yield return null;
        }
        cardOpen = false;
        yield return null;
    }

    /// <summary>A code / password puzzle. Sets codeResult = true when solved, false if the player backs out (Esc).</summary>
    protected IEnumerator EnterCode(string title, string hint, string answer, int maxLen, bool digitsOnly, string helpTitle = null, string helpBody = null)
    {
        codeOpen = true; codeTitle = title; codeHint = hint; codeBuf = ""; codeMax = maxLen; codeDigits = digitsOnly; codeMsg = "";
        codeHelpTitle = helpTitle; codeHelpBody = helpBody; codeHelpShown = false; codeResult = false;
        Cursor.lockState = CursorLockMode.Locked;
        if (Keyboard.current != null) Keyboard.current.onTextInput += OnCodeChar;
        yield return null;
        while (true)
        {
            var kb = Keyboard.current;
            if (kb == null) { yield return null; continue; }
            if (kb.escapeKey.wasPressedThisFrame) break;
            if (kb.backspaceKey.wasPressedThisFrame && codeBuf.Length > 0) codeBuf = codeBuf.Substring(0, codeBuf.Length - 1);
            if (helpBody != null && kb.tabKey.wasPressedThisFrame) codeHelpShown = !codeHelpShown;
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                if (string.Equals(codeBuf.Trim(), answer, StringComparison.OrdinalIgnoreCase))
                {
                    codeResult = true; sfx.PlayOneShot(dingClip, 0.7f);
                    codeMsg = "ACCESS GRANTED";
                    yield return new WaitForSeconds(0.7f);
                    break;
                }
                codeMsg = "Incorrect. Try again.";
                sfx.PlayOneShot(buzzClip, 0.6f);
                codeBuf = "";
            }
            yield return null;
        }
        if (Keyboard.current != null) Keyboard.current.onTextInput -= OnCodeChar;
        codeOpen = false;
        yield return null;
    }

    void OnCodeChar(char c)
    {
        if (!codeOpen || char.IsControl(c) || codeBuf.Length >= codeMax) return;
        if (codeDigits ? char.IsDigit(c) : char.IsLetterOrDigit(c)) codeBuf += char.ToUpperInvariant(c);
    }

    // ============================================================= INTERACTION
    void UpdateInteraction()
    {
        prompt = "";
        if (phase != Phase.Free || PriyaRoomPlayer.Locked || journalOpen) return;   // no interacting through the open case file
        Spot best = null; float bestScore = float.MaxValue;
        Vector3 o = cam.position, f = cam.forward;
        foreach (var s in spots)
        {
            if (s.t == null || !s.t.gameObject.activeInHierarchy) continue;
            if (s.active != null && !s.active()) continue;
            Vector3 c = s.t.position + s.offset;
            Vector3 toC = c - o; float d = toC.magnitude;
            if (d > s.range || d < 0.05f || Vector3.Dot(toC, f) <= 0f) continue;
            float perp = Vector3.Cross(f, toC).magnitude;
            float allowed = s.radius + d * 0.05f + 0.05f;
            if (perp > allowed) continue;
            if (Blocked(o, c, s.t)) continue;
            float score = perp / allowed;
            if (score < bestScore) { bestScore = score; best = s; }
        }
        if (best == null) return;
        prompt = "[E]  " + best.label;
        var kb = Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame)
        {
            sfx.PlayOneShot(clickClip, 0.6f);
            best.act();
        }
    }

    static bool Blocked(Vector3 o, Vector3 p, Transform owner)
    {
        Vector3 dir = p - o; float len = dir.magnitude;
        if (len < 0.1f) return false;
        foreach (var h in Physics.RaycastAll(o, dir / len, len, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform == owner || h.collider.transform.IsChildOf(owner) || owner.IsChildOf(h.collider.transform)) continue;
            if (h.collider.GetComponentInParent<PriyaRoomPlayer>() != null) continue;
            if (h.distance < len - 0.3f) return true;
        }
        return false;
    }

    // =================================================================== AUDIO
    const int SR = 44100;

    protected virtual AudioClip AmbienceClip() { return MakeNoiseBed(0.02f, 0.06f, 0.35f); }

    void SetupAudio()
    {
        var go = new GameObject("ActAudio");
        go.transform.SetParent(transform, false);
        ambience = go.AddComponent<AudioSource>(); ambience.loop = true; ambience.volume = 0f; ambience.spatialBlend = 0f;
        ambience.clip = AmbienceClip(); ambience.Play();
        sfx = go.AddComponent<AudioSource>();
        voice = go.AddComponent<AudioSource>();
        clickClip = Tone(880f, 0.1f, 55f, 0.5f);
        dingClip = Ding();
        buzzClip = Tone(140f, 0.3f, 6f, 0.4f, true);
        ringClip = Ring();
        stepClip = Step();
    }

    /// <summary>A seamless 8 s bed of filtered noise: lp = low rumble amount, hum = mains hum, air = hiss.</summary>
    protected static AudioClip MakeNoiseBed(float hum, float air, float rumble)
    {
        int n = SR * 8, tail = 4000;
        var g = new float[n + tail];
        var r = new System.Random(7);
        float a = 0, b = 0, c = 0;
        for (int i = 0; i < g.Length; i++)
        {
            float w = (float)(r.NextDouble() * 2 - 1);
            a += (w - a) * 0.02f; b += (a - b) * 0.05f; c += (w - c) * 0.3f;
            float t = i / (float)SR;
            g[i] = b * 5f * rumble + (w - c) * air + Mathf.Sin(2f * Mathf.PI * 50f * t) * hum;
        }
        var d = new float[n];
        for (int k = 0; k < n; k++) d[k] = k < tail ? g[k] * (k / (float)tail) + g[n + k] * (1f - k / (float)tail) : g[k];
        Norm(d, 0.4f);
        var clip = AudioClip.Create("bed", n, 1, SR, false); clip.SetData(d, 0); return clip;
    }

    protected static AudioClip Tone(float f, float len, float decay, float vol, bool square = false)
    {
        int n = (int)(SR * len); var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR, s = Mathf.Sin(2f * Mathf.PI * f * t);
            d[i] = (square ? Mathf.Sign(s) * 0.6f : s) * Mathf.Exp(-t * decay) * vol;
        }
        var c = AudioClip.Create("tone", n, 1, SR, false); c.SetData(d, 0); return c;
    }

    static AudioClip Ding()
    {
        int n = SR * 6 / 10; var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            d[i] = (Mathf.Sin(2f * Mathf.PI * 784f * t) * Mathf.Exp(-t * 7f) + (t > 0.09f ? Mathf.Sin(2f * Mathf.PI * 1175f * (t - 0.09f)) * Mathf.Exp(-(t - 0.09f) * 6f) : 0f)) * 0.35f;
        }
        var c = AudioClip.Create("ding", n, 1, SR, false); c.SetData(d, 0); return c;
    }

    static AudioClip Ring()
    {
        int n = SR * 22 / 10; var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float burst = (t % 1.1f) < 0.7f ? 1f : 0f;
            d[i] = (Mathf.Sin(2f * Mathf.PI * 880f * t) + Mathf.Sin(2f * Mathf.PI * 1175f * t)) * 0.22f * burst * (Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 22f * t)) * 0.5f + 0.5f);
        }
        var c = AudioClip.Create("ring", n, 1, SR, false); c.SetData(d, 0); return c;
    }

    static AudioClip Step()
    {
        int n = SR / 3; var d = new float[n]; var r = new System.Random(9);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            d[i] = Mathf.Sin(2f * Mathf.PI * 75f * t) * Mathf.Exp(-t * 22f) + (float)(r.NextDouble() * 2 - 1) * Mathf.Exp(-t * 45f) * 0.35f;
        }
        Norm(d, 0.8f);
        var c = AudioClip.Create("step", n, 1, SR, false); c.SetData(d, 0); return c;
    }

    protected static void Norm(float[] d, float peak)
    {
        float m = 0.0001f;
        foreach (var v in d) m = Mathf.Max(m, Mathf.Abs(v));
        for (int i = 0; i < d.Length; i++) d[i] *= peak / m;
    }

    // ============================================================ WORLD HELPERS
    protected GameObject B(string n, Vector3 pos, Vector3 size, Color c, bool col = false, float smooth = 0.25f, float metal = 0f)
    { return GameKit.Box(transform, n, pos, size, GameKit.Lit(c, smooth, metal), col); }
    protected GameObject Cy(string n, Vector3 pos, float r, float h, Color c, float smooth = 0.3f, float metal = 0f)
    { return GameKit.Cyl(transform, n, pos, r, h, GameKit.Lit(c, smooth, metal)); }
    protected GameObject Sp(string n, Vector3 pos, float d, Color c, float smooth = 0.3f)
    { return GameKit.Sphere(transform, n, pos, d, GameKit.Lit(c, smooth)); }
    protected GameObject Glow(string n, Vector3 pos, Vector3 size, Color c, float intensity = 2f)
    { return GameKit.Box(transform, n, pos, size, GameKit.Glow(c * 0.5f, c, intensity)); }
    protected Light PL(Vector3 pos, Color c, float intensity, float range)
    { return GameKit.PointLight(transform, pos, c, intensity, range); }

    protected CharacterNPC Npc(Vector3 pos, float yaw, NpcStyle style, string name, bool lookAtPlayer = true)
    {
        var n = CharacterNPC.Create(transform, pos, style, name);
        n.transform.rotation = Quaternion.Euler(0, yaw, 0);
        if (lookAtPlayer) n.lookTarget = cam; else n.freezeFacing = true;
        return n;
    }

    /// <summary>A room: floor, ceiling, four walls (all solid) of the given inner size, centred at c.</summary>
    protected void Room(string name, Vector3 c, Vector3 size, Color wall, Color floor, string floorTex = "stone", string wallTex = "plaster", bool ceiling = true)
    {
        float t = 0.25f;
        var f = B(name + "_Floor", c + new Vector3(0, -0.05f, 0), new Vector3(size.x + 2 * t, 0.1f, size.z + 2 * t), floor, true, 0.4f);
        GameKit.Surface(f, floorTex, 1.2f, 1f, 1f, floor);
        if (ceiling) GameKit.Surface(B(name + "_Ceiling", c + new Vector3(0, size.y + 0.05f, 0), new Vector3(size.x + 2 * t, 0.1f, size.z + 2 * t), wall * 0.95f, true), wallTex, 2f);
        GameKit.Surface(B(name + "_WallN", c + new Vector3(0, size.y / 2f, size.z / 2f + t / 2f), new Vector3(size.x + 2 * t, size.y, t), wall, true), wallTex, 2f);
        GameKit.Surface(B(name + "_WallS", c + new Vector3(0, size.y / 2f, -size.z / 2f - t / 2f), new Vector3(size.x + 2 * t, size.y, t), wall, true), wallTex, 2f);
        GameKit.Surface(B(name + "_WallE", c + new Vector3(size.x / 2f + t / 2f, size.y / 2f, 0), new Vector3(t, size.y, size.z), wall, true), wallTex, 2f);
        GameKit.Surface(B(name + "_WallW", c + new Vector3(-size.x / 2f - t / 2f, size.y / 2f, 0), new Vector3(t, size.y, size.z), wall, true), wallTex, 2f);
    }

    /// <summary>An invisible wall (keeps the player inside open areas).</summary>
    protected void Fence(Vector3 center, Vector3 size)
    {
        var go = new GameObject("Fence");
        go.transform.SetParent(transform, false);
        go.transform.position = center;
        go.AddComponent<BoxCollider>().size = size;
    }

    /// <summary>Four invisible walls around a rectangular area (centre, width x, depth z).</summary>
    protected void FenceArea(Vector3 c, float w, float d)
    {
        Fence(c + new Vector3(0, 2f, d / 2f), new Vector3(w, 4f, 0.4f));
        Fence(c + new Vector3(0, 2f, -d / 2f), new Vector3(w, 4f, 0.4f));
        Fence(c + new Vector3(w / 2f, 2f, 0), new Vector3(0.4f, 4f, d));
        Fence(c + new Vector3(-w / 2f, 2f, 0), new Vector3(0.4f, 4f, d));
    }

    protected void SetSun(Color c, float intensity, Vector3 euler)
    {
        Light sun = null;
        foreach (var l in FindObjectsByType<Light>())
            if (l.type == LightType.Directional && l.enabled) { sun = l; break; }
        if (sun == null)
        {
            var go = new GameObject("Sun"); go.transform.SetParent(transform, false);
            sun = go.AddComponent<Light>(); sun.type = LightType.Directional;
        }
        sun.color = c; sun.intensity = intensity; sun.transform.rotation = Quaternion.Euler(euler);
        CinematicLook.Shadows(sun, 1f, 0.04f, 0.8f);
    }

    /// <summary>A big unlit gradient quad used as a sky behind windows / over rooftops.</summary>
    protected GameObject SkyQuad(Vector3 pos, Vector3 euler, Vector2 size, Color bottom, Color top)
    {
        var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++) tex.SetPixel(0, y, Color.Lerp(bottom, top, Mathf.Pow(y / 63f, 0.8f)));
        tex.Apply();
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "Sky"; Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(transform, false);
        q.transform.position = pos; q.transform.rotation = Quaternion.Euler(euler);
        q.transform.localScale = new Vector3(size.x, size.y, 1);
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        var m = new Material(sh != null ? sh : Shader.Find("Unlit/Texture"));
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        m.mainTexture = tex;
        q.GetComponent<Renderer>().sharedMaterial = m;
        return q;
    }

    // ====================================================================== UI
    void Styles()
    {
        if (sSub != null) return;
        sSub = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        sTitle = new GUIStyle(GUI.skin.label) { fontSize = 48, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        sTitle2 = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        sSmall = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
        sPanel = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
        sHead = new GUIStyle(sPanel) { fontStyle = FontStyle.Bold, fontSize = 17 };
        sPrompt = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        UiKit.Skin(sSub, sTitle, sTitle2, sSmall, sPanel, sHead, sPrompt);
        sMono = new GUIStyle(sPanel) { fontSize = 38, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        sMono.normal.textColor = new Color(0.55f, 1f, 0.6f);
    }

    protected virtual void OnGUI()
    {
        GUI.depth = -10;
        Styles();
        float s = Mathf.Max(0.7f, Screen.height / 900f);
        var saved = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
        float W = Screen.width / s, H = Screen.height / s;

        UiKit.Letterbox(W, H, !paused && creditsStart <= 0 && (phase == Phase.Intro || !string.IsNullOrEmpty(dlgText) || cardOpen));

        if (phase == Phase.Free && !paused)
        {
            UiKit.Crosshair(W, H);
            if (objectiveTarget.HasValue) DrawMarker(s, W, H);
            if (!string.IsNullOrEmpty(prompt)) UiKit.Prompt(W, H, prompt);
            if (!string.IsNullOrEmpty(objective))
            {
                var r = new Rect(20, 18, Mathf.Min(640, W * 0.55f), 62);
                UiKit.Card(r, 0.5f);
                GUI.Label(new Rect(r.x + 12, r.y + 6, r.width - 24, 20), UiKit.Track("Objective"), sHead);
                GUI.Label(new Rect(r.x + 12, r.y + 27, r.width - 24, 34), objective, sSmall);
            }
            UiKit.Text(new Rect(0, H - 30, W - 20, 24), "E  Interact      J  Case file      Esc  Pause", new GUIStyle(sSmall) { alignment = TextAnchor.LowerRight, fontSize = 14 });
        }

        if (Time.time < toastUntil && !string.IsNullOrEmpty(toast) && !paused)
        {
            float tw = toastBig ? 660 : 620;
            var r = new Rect(W / 2 - tw / 2, toastBig ? H * 0.2f : 96, tw, toastBig ? 56 : 40);      // below the objective box, not on top of it
            UiKit.Card(r, 0.75f);
            GUI.Label(new Rect(r.x, r.y + (toastBig ? 10 : 6), r.width, 40), toast, new GUIStyle(sPrompt) { fontSize = toastBig ? 24 : 18 });
        }

        if (Time.time < subUntil && !string.IsNullOrEmpty(sub) && !paused)
        {
            float h = sSub.CalcHeight(new GUIContent(sub), W * 0.72f) + 16;
            var r = new Rect(W * 0.14f, H - 60 - h, W * 0.72f, h);
            UiKit.Card(r, 0.55f);
            GUI.Label(new Rect(r.x + 8, r.y + 8, r.width - 16, h - 8), sub, sSub);
        }

        if (!string.IsNullOrEmpty(dlgText) && !paused) DrawDialogue(W, H);
        if (cardOpen && !paused) DrawCard(W, H);
        if (codeOpen && !paused) DrawCode(W, H);
        if (journalOpen && !paused) DrawJournal(W, H);

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
        if (creditsStart > 0) DrawCredits(W, H);
        if (paused) DrawPause(W, H);
        GUI.matrix = saved;
    }

    void DrawMarker(float s, float W, float H)
    {
        Vector3 sp = camComp.WorldToScreenPoint(objectiveTarget.Value);
        if (sp.z <= 0.1f) { UiKit.Text(new Rect(0, H * 0.3f, W, 28), "Turn around: " + objectiveName, new GUIStyle(sPrompt) { fontSize = 18 }); return; }
        float x = Mathf.Clamp(sp.x / s, 40, W - 40), y = Mathf.Clamp((Screen.height - sp.y) / s, 60, H - 80);
        var m = GUI.matrix;
        GUIUtility.RotateAroundPivot(45f, new Vector2(x, y));
        var old = GUI.color; GUI.color = new Color(1f, 0.8f, 0.2f, 0.95f);
        GUI.DrawTexture(new Rect(x - 7, y - 7, 14, 14), Texture2D.whiteTexture);
        GUI.color = old; GUI.matrix = m;
        UiKit.Text(new Rect(x - 140, y - 38, 280, 24), objectiveName + "   " + Vector3.Distance(cam.position, objectiveTarget.Value).ToString("0") + " m", new GUIStyle(sSmall) { alignment = TextAnchor.UpperCenter, fontSize = 15 });
    }

    void DrawDialogue(float W, float H)
    {
        float textH = sPanel.CalcHeight(new GUIContent(dlgText), W * 0.7f - 40);
        float optH = 34;
        if (dlgOptions != null) { optH = 12; foreach (var o in dlgOptions) optH += sPanel.CalcHeight(new GUIContent("Press 1:   " + o), W * 0.7f - 70) + 6; }
        float ph = 50 + textH + optH + 10;
        var r = new Rect(W * 0.15f, H - ph - 30, W * 0.7f, ph);
        UiKit.Card(r, 0.84f);
        GUI.Label(new Rect(r.x + 20, r.y + 10, r.width - 40, 26), dlgSpeaker, sHead);
        GUI.Label(new Rect(r.x + 20, r.y + 38, r.width - 40, textH + 4), dlgText, sPanel);
        float y = r.y + 46 + textH;
        if (dlgOptions != null)
        {
            for (int i = 0; i < dlgOptions.Length; i++)
            {
                bool dis = dlgDisabled != null && i < dlgDisabled.Length && dlgDisabled[i];
                var st = new GUIStyle(sPanel);
                st.normal.textColor = dis ? new Color(0.55f, 0.55f, 0.55f) : new Color(1f, 0.9f, 0.6f);
                string t = "Press " + (i + 1) + ":   " + dlgOptions[i];
                float oh = sPanel.CalcHeight(new GUIContent(t), W * 0.7f - 70);
                GUI.Label(new Rect(r.x + 30, y, r.width - 60, oh), t, st);
                y += oh + 6;
            }
        }
        else GUI.Label(new Rect(r.x + 30, y + 4, r.width - 60, 26), "[E] / [Space] next line   |   [Enter] skip conversation", sSmall);
    }

    void DrawCard(float W, float H)
    {
        UiKit.Card(new Rect(0, 0, W, H), 0.45f);
        float bh = sPanel.CalcHeight(new GUIContent(cardBody), 640) + 110;
        var r = new Rect(W / 2 - 360, H / 2 - bh / 2, 720, bh);
        UiKit.Card(r, 0.92f);
        GUI.Label(new Rect(r.x + 40, r.y + 22, r.width - 80, 30), cardTitle, new GUIStyle(sHead) { fontSize = 22 });
        GUI.Label(new Rect(r.x + 40, r.y + 62, r.width - 80, bh - 100), cardBody, sPanel);
        GUI.Label(new Rect(r.x + 40, r.yMax - 34, r.width - 80, 24), "[E] close", new GUIStyle(sSmall) { alignment = TextAnchor.UpperRight });
    }

    void DrawCode(float W, float H)
    {
        UiKit.Card(new Rect(0, 0, W, H), 0.4f);
        var r = new Rect(W / 2 - 340, H * 0.25f, 680, 330);
        UiKit.Card(r, 0.95f);
        GUI.Label(new Rect(r.x + 36, r.y + 20, r.width - 72, 30), codeTitle, new GUIStyle(sHead) { fontSize = 22 });
        GUI.Label(new Rect(r.x + 36, r.y + 58, r.width - 72, 70), codeHint, sPanel);
        var f = new Rect(r.x + 120, r.y + 138, r.width - 240, 70);
        UiKit.Card(f, 0.9f);
        string shown = "";
        for (int i = 0; i < codeMax; i++) shown += (i < codeBuf.Length ? codeBuf[i].ToString() : "_") + (i < codeMax - 1 ? " " : "");
        GUI.Label(f, shown, sMono);
        var ms = new GUIStyle(sHead) { alignment = TextAnchor.UpperCenter };
        ms.normal.textColor = codeMsg.StartsWith("ACCESS") ? UiKit.Good : UiKit.Danger;
        GUI.Label(new Rect(r.x, r.y + 216, r.width, 26), codeMsg, ms);
        GUI.Label(new Rect(r.x + 36, r.y + 250, r.width - 72, 50),
            "Type, then ENTER   |   Backspace delete   |   Esc step away" + (codeHelpBody != null ? "   |   TAB " + codeHelpTitle : ""), new GUIStyle(sSmall) { alignment = TextAnchor.UpperCenter });
        if (codeHelpShown && codeHelpBody != null)
        {
            var h = new Rect(W / 2 - 300, r.yMax + 16, 600, sPanel.CalcHeight(new GUIContent(codeHelpBody), 540) + 64);
            UiKit.Card(h, 0.95f);
            GUI.Label(new Rect(h.x + 30, h.y + 14, h.width - 60, 26), codeHelpTitle, sHead);
            GUI.Label(new Rect(h.x + 30, h.y + 44, h.width - 60, h.height - 50), codeHelpBody, sPanel);
        }
    }

    void DrawJournal(float W, float H)
    {
        var d = SaveSystem.Data;
        var notes = new List<UiKit.Note>();
        for (int i = d.evidence.Count - 1; i >= 0; i--)          // newest first
            notes.Add(new UiKit.Note("Evidence " + (i + 1).ToString("00"), d.evidence[i]));
        UiKit.CaseFile(W, H, "Detective " + d.playerName,
            "Apartment clues  " + d.clues.Count + " / 9\nEvidence notes  " + d.evidence.Count,
            notes, "Nothing recorded yet. Talk to people and examine what you find.");
    }

    void DrawCredits(float W, float H)
    {
        var old = GUI.color; GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = old;
        float t = Time.time - creditsStart;
        float y = H - t * 42f;
        foreach (var line in creditLines)
        {
            bool head = line.StartsWith("#");
            string txt = head ? line.Substring(1) : line;
            var st = head ? new GUIStyle(sTitle) { fontSize = 34 } : new GUIStyle(sTitle2) { fontSize = 20 };
            float h = head ? 56 : 32;
            if (y > -60 && y < H + 20) GUI.Label(new Rect(0, y, W, h), txt, st);
            y += h;
        }
        if (t > 3f) UiKit.Text(new Rect(0, H - 40, W, 28), "Press R for the main menu", new GUIStyle(sSmall) { alignment = TextAnchor.UpperCenter });
    }

    void DrawPause(float W, float H)
    {
        UiKit.Card(new Rect(0, 0, W, H), 0.7f);
        GUI.Label(new Rect(0, H * 0.2f, W, 60), UiKit.Track("Paused"), sTitle);
        if (GUI.Button(new Rect(W / 2 - 130, H * 0.36f, 260, 46), "Resume", UiKit.ButtonStyle())) SetPaused(false);
        if (GUI.Button(new Rect(W / 2 - 130, H * 0.36f + 58, 260, 46), "Quit to main menu", UiKit.ButtonStyle())) { SetPaused(false); builder.Build(); }
        GUI.Label(new Rect(W / 2 - 330, H * 0.36f + 130, 660, 90),
            "W A S D walk  |  Mouse look  |  E interact  |  1-4 answer\nJ case notes  |  Space skip line  |  Enter skip conversation  |  F2 graphics", new GUIStyle(sPanel) { alignment = TextAnchor.UpperCenter });
    }
}
