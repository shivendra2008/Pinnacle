using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The start menu. First launch: asks for the player's name. After that: Resume / Start, replay any unlocked act,
/// a progress card (acts done, clues found), and a confirmed "Reset all data".
/// Drawn with IMGUI inside the story's scaled GUI matrix.
/// </summary>
public class GameMenu : IDisposable
{
    public enum Screen { Name, Main, ConfirmReset }
    public Screen screen;
    public Action<int, bool> OnPlay;          // (act 1 or 2, resume the saved progress?)

    string nameBuf = "";
    bool subscribed;
    GUIStyle sTitle, sSub, sBody, sSmall, sHead, sField, sBtnRed;

    public GameMenu()
    {
        SaveSystem.Load();
        screen = string.IsNullOrEmpty(SaveSystem.Profile.playerName) ? Screen.Name : Screen.Main;
    }

    // ---------------------------------------------------------------- input
    public void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        bool enter = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;

        if (screen == Screen.Name)
        {
            if (!subscribed) { kb.onTextInput += OnText; subscribed = true; }
            if (enter) ConfirmName();
            else if (kb.backspaceKey.wasPressedThisFrame && nameBuf.Length > 0) nameBuf = nameBuf.Substring(0, nameBuf.Length - 1);
        }
        else
        {
            Unsubscribe();
            if (screen == Screen.Main && enter) DoPrimary();
        }
    }

    void OnText(char c)
    {
        if (char.IsControl(c) || nameBuf.Length >= 16) return;
        if (char.IsLetterOrDigit(c) || c == ' ' || c == '\'' || c == '-' || c == '.') nameBuf += c;
    }

    void Unsubscribe()
    {
        if (subscribed && Keyboard.current != null) Keyboard.current.onTextInput -= OnText;
        subscribed = false;
    }

    public void Dispose() { Unsubscribe(); }

    void ConfirmName()
    {
        string n = nameBuf.Trim();
        if (n.Length == 0) return;
        SaveSystem.Profile.playerName = n;
        SaveSystem.Save();
        screen = Screen.Main;
        Unsubscribe();
    }

    // -------------------------------------------------------------- actions
    static readonly string[] ActNames = { "The Empty Room", "The Streets Know", "The File", "The Web", "Sunrise" };
    static readonly string[] Roman = { "I", "II", "III", "IV", "V" };

    static bool InProgress(int act) { return SaveSystem.InProgress(act); }

    /// <summary>
    /// The big button. CONTINUE resumes the campaign at its last checkpoint (mid-act where the act has one,
    /// otherwise the start of the current act). After the last act it is PLAY AGAIN: a new campaign, the profile kept.
    /// </summary>
    void DoPrimary()
    {
        var d = SaveSystem.Data;
        if (d.actsCompleted >= 5) { SaveSystem.NewCampaign(); Play(1, false); return; }
        int next = d.actsCompleted + 1;
        Play(next, InProgress(next));
    }

    /// <summary>
    /// An act chosen from the list. The campaign's current act restarts in the campaign; any other unlocked act is a
    /// chapter replay, played in a sandbox that never changes the campaign (endings found there still count).
    /// </summary>
    void PickAct(int act)
    {
        int current = Mathf.Min(SaveSystem.Data.actsCompleted + 1, 5);
        if (act != current || SaveSystem.Data.actsCompleted >= 5) SaveSystem.BeginReplay(act);
        Play(act, false);
    }

    void Play(int act, bool resume)
    {
        SaveSystem.Profile.sessions++;
        SaveSystem.Save();
        if (OnPlay != null) OnPlay(act, resume);
    }

    static bool Unlocked(int act)
    {
        return act == 1 || SaveSystem.Data.actsCompleted >= act - 1 || SaveSystem.Profile.highestActCompleted >= act - 1;
    }

    string PrimaryLabel()
    {
        var d = SaveSystem.Data;
        if (d.actsCompleted >= 5) return "PLAY AGAIN  -  from Act I";
        int next = d.actsCompleted + 1;
        if (next == 1 && SaveSystem.Act1InProgress) return "RESUME  -  Act I, " + d.clues.Count + " of 9 clues found";
        if (next == 1) return d.clues.Count == 0 && SaveSystem.Profile.highestActCompleted > 0 ? "NEW GAME" : "START GAME";
        return (InProgress(next) ? "RESUME  -  Act " : "CONTINUE  -  Act ") + Roman[next - 1] + ": " + ActNames[next - 1];
    }

    // ------------------------------------------------------------------ UI
    void Styles()
    {
        if (sTitle != null) return;
        sTitle = new GUIStyle(GUI.skin.label) { font = UiKit.Title, fontSize = 44, alignment = TextAnchor.MiddleCenter };
        sTitle.normal.textColor = UiKit.Cream;
        sSub = new GUIStyle(GUI.skin.label) { font = UiKit.Body, fontSize = 20, alignment = TextAnchor.MiddleCenter };
        sSub.normal.textColor = UiKit.Muted;
        sBody = new GUIStyle(GUI.skin.label) { font = UiKit.Body, fontSize = 19, wordWrap = true };
        sBody.normal.textColor = UiKit.Cream;
        sSmall = new GUIStyle(sBody) { fontSize = 15 };
        sSmall.normal.textColor = UiKit.Muted;
        sHead = new GUIStyle(sBody) { fontStyle = FontStyle.Bold, fontSize = 17 };
        sHead.normal.textColor = UiKit.Gold;
        sField = new GUIStyle(sBody) { fontSize = 30, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        sBtnRed = new GUIStyle(UiKit.ButtonStyle());
        sBtnRed.normal.textColor = new Color(1f, 0.55f, 0.5f);
    }

    public void Draw(float W, float H)
    {
        Styles();
        UiKit.Text(new Rect(0, H * 0.07f, W, 80), UiKit.Track("Shadows of Chandni"), sTitle);
        UiKit.Fill(new Rect(W / 2 - 40, H * 0.07f + 78, 80, 2), UiKit.Gold);
        GUI.Label(new Rect(0, H * 0.07f + 88, W, 30), "A non-violent mystery thriller", sSub);
        switch (screen)
        {
            case Screen.Name: DrawName(W, H); break;
            case Screen.Main: DrawMain(W, H); break;
            case Screen.ConfirmReset: DrawConfirm(W, H); break;
        }
        // which build this is (written into scripted builds by BuildPlayer; absent from Editor play and other builds)
        if (stamp == null) { var ta = Resources.Load<TextAsset>("BuildStamp"); stamp = ta != null ? ta.text.Trim() : ""; }
        if (stamp.Length > 0)
            GUI.Label(new Rect(16, H - 30, W - 32, 22), stamp, new GUIStyle(sBody) { fontSize = 13, alignment = TextAnchor.LowerLeft, normal = { textColor = new Color(UiKit.Muted.r, UiKit.Muted.g, UiKit.Muted.b, 0.85f) } });
    }
    static string stamp;

    void DrawName(float W, float H)
    {
        var r = new Rect(W / 2 - 300, H * 0.32f, 600, 300);
        UiKit.Card(r, 0.85f);
        GUI.Label(new Rect(r.x, r.y + 22, r.width, 30), "WELCOME, DETECTIVE", new GUIStyle(sHead) { alignment = TextAnchor.UpperCenter, fontSize = 22 });
        GUI.Label(new Rect(r.x + 40, r.y + 66, r.width - 80, 60), "A girl has vanished and the city is keeping secrets. Before we begin: what should we call you?", sBody);

        var f = new Rect(r.x + 90, r.y + 142, r.width - 180, 58);
        UiKit.Card(f, 0.5f);
        bool caret = ((int)(Time.realtimeSinceStartup * 2f)) % 2 == 0;
        GUI.Label(f, nameBuf.Length == 0 ? "type your name..." : nameBuf + (caret ? "|" : " "),
                  nameBuf.Length == 0 ? new GUIStyle(sField) { fontStyle = FontStyle.Italic, normal = { textColor = UiKit.Muted } } : sField);
        GUI.Label(new Rect(r.x, r.y + 208, r.width, 24), "Type your name, then press ENTER", new GUIStyle(sSmall) { alignment = TextAnchor.UpperCenter });
        if (GUI.Button(new Rect(r.x + 200, r.y + 238, 200, 44), "CONTINUE", UiKit.ButtonStyle())) ConfirmName();
    }

    void DrawMain(float W, float H)
    {
        var d = SaveSystem.Data;
        float top = H * 0.22f;

        // ---- left: actions
        var m = new Rect(W / 2 - 480, top, 440, 560);
        UiKit.Card(m, 0.85f);
        GUI.Label(new Rect(m.x + 24, m.y + 16, m.width - 48, 30), (d.actsCompleted > 0 || d.clues.Count > 0 || SaveSystem.Profile.highestActCompleted > 0 ? "Welcome back, " : "Welcome, ") + SaveSystem.Profile.playerName, new GUIStyle(sHead) { fontSize = 20 });
        if (GUI.Button(new Rect(m.x + 24, m.y + 54, m.width - 48, 60), PrimaryLabel(), UiKit.ButtonStyle())) DoPrimary();

        GUI.Label(new Rect(m.x + 24, m.y + 128, m.width - 48, 22), "CHOOSE AN ACT", sHead);
        for (int a = 1; a <= 5; a++)
        {
            bool unlocked = Unlocked(a);
            bool current = a == Mathf.Min(d.actsCompleted + 1, 5) && d.actsCompleted < 5;
            GUI.enabled = unlocked;
            string label = "ACT " + Roman[a - 1] + "  -  " + (unlocked ? ActNames[a - 1] + (current ? "   (restart)" : "   (replay)") : "locked");
            if (GUI.Button(new Rect(m.x + 24, m.y + 156 + (a - 1) * 50, m.width - 48, 42), label, UiKit.ButtonStyle())) PickAct(a);
        }
        GUI.enabled = true;
        GUI.Label(new Rect(m.x + 24, m.y + 410, m.width - 48, 40), "Replaying a chapter never changes your campaign. Restart begins your current act again.", sSmall);
        if (GUI.Button(new Rect(m.x + 24, m.y + 450, m.width - 48, 42), "RESET ALL DATA", sBtnRed)) screen = Screen.ConfirmReset;
        GUI.Label(new Rect(m.x + 24, m.y + 500, m.width - 48, 54),
            "W A S D move  |  Mouse look  |  E interact  |  1-4 answer  |  TAB detective vision  |  J notes  |  Esc pause  |  Enter skip", sSmall);

        // ---- right: progress
        var p = new Rect(W / 2 + 40, top, 440, 560);
        UiKit.Card(p, 0.85f);
        GUI.Label(new Rect(p.x + 24, p.y + 16, p.width - 48, 26), "YOUR PROGRESS", sHead);
        GUI.Label(new Rect(p.x + 24, p.y + 44, p.width - 48, 26), "Detective " + SaveSystem.Profile.playerName, new GUIStyle(sBody) { fontSize = 22, fontStyle = FontStyle.Bold });
        GUI.Label(new Rect(p.x + 24, p.y + 78, p.width - 48, 22),
            "Acts completed:  " + Mathf.Min(5, d.actsCompleted) + " of 5      Endings found:  " + SaveSystem.Profile.endingsSeen.Count + " of 3", sBody);
        UiKit.Bar(new Rect(p.x + 24, p.y + 106, p.width - 48, 12), Mathf.Min(5, d.actsCompleted) / 5f, UiKit.Gold);

        float y = p.y + 134;
        for (int a = 1; a <= 5; a++)
        {
            bool done = d.actsCompleted >= a, locked = d.actsCompleted < a - 1, prog = !done && InProgress(a);
            string state = done ? "Completed" : locked ? "Locked" : prog ? "In progress" : "Not started";
            Color col = done ? UiKit.Good : prog ? UiKit.Gold : UiKit.Muted;
            string detail;
            if (a == 1) detail = done ? "All 9 apartment clues found" : prog ? d.clues.Count + " of 9 clues found  (all 9 needed)" : "Search Priya's apartment";
            else if (a == 2) detail = done ? "The black SUV has been dealt with" : prog ? d.evidence.Count + " evidence notes" : "Follow the lead into the market";
            else if (a == 3) detail = done ? "Priya's envelope is in your hands" : prog ? "Investigating the library" : "The library and the Kali temple";
            else if (a == 4) detail = done ? (d.allyRaj ? "Raj Malhotra is on your side" : d.allyRohan ? "Rohan is on your side" : d.allyReddy ? "Inspector Reddy is on your side" : "Midnight in the city") : "Three paths through the night";
            else detail = done ? "Endings found: " + SaveSystem.Profile.endingsSeen.Count + " of 3" : prog ? "In the Curator's Gallery" : "Find Priya and choose the ending";
            ActRow(p, ref y, "ACT " + Roman[a - 1] + "  -  " + ActNames[a - 1], state, col, detail, done ? 1f : a == 1 ? d.clues.Count / 9f : 0f);
        }
        if (!string.IsNullOrEmpty(d.lastSaved))
            GUI.Label(new Rect(p.x + 24, p.yMax - 30, p.width - 48, 22), "Last saved: " + d.lastSaved + "      Evidence: " + SaveSystem.EvidenceCount, sSmall);
    }

    void ActRow(Rect card, ref float y, string title, string state, Color stateColor, string detail, float fill)
    {
        GUI.Label(new Rect(card.x + 24, y, card.width - 48, 24), title, new GUIStyle(sBody) { fontStyle = FontStyle.Bold, fontSize = 17 });
        var ss = new GUIStyle(sHead) { alignment = TextAnchor.UpperRight, fontSize = 15 };
        ss.normal.textColor = stateColor;
        GUI.Label(new Rect(card.x + 24, y + 1, card.width - 48, 24), state, ss);
        GUI.Label(new Rect(card.x + 24, y + 24, card.width - 48, 22), detail, sSmall);
        UiKit.Bar(new Rect(card.x + 24, y + 48, card.width - 48, 6), fill, stateColor);
        y += 76;
    }

    void DrawConfirm(float W, float H)
    {
        var r = new Rect(W / 2 - 280, H * 0.36f, 560, 250);
        UiKit.Card(r, 0.92f);
        GUI.Label(new Rect(r.x, r.y + 22, r.width, 30), "RESET ALL DATA?", new GUIStyle(sHead) { alignment = TextAnchor.UpperCenter, fontSize = 24, normal = { textColor = UiKit.Danger } });
        GUI.Label(new Rect(r.x + 40, r.y + 68, r.width - 80, 90),
            "This erases your name, your clues, your evidence and every completed act. It cannot be undone.", new GUIStyle(sBody) { alignment = TextAnchor.UpperCenter });
        if (GUI.Button(new Rect(r.x + 50, r.y + 170, 220, 50), "YES, ERASE EVERYTHING", sBtnRed))
        {
            SaveSystem.Reset();
            nameBuf = "";
            screen = Screen.Name;
        }
        if (GUI.Button(new Rect(r.xMax - 270, r.y + 170, 220, 50), "CANCEL", UiKit.ButtonStyle())) screen = Screen.Main;
    }
}
