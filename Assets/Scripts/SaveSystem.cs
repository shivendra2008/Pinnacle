using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Everything the game remembers between sessions.</summary>
[Serializable]
public class SaveData
{
    public int version = 1;
    public string playerName = "";
    public int actsCompleted;                         // 0..5: how many acts are finished
    public string lastSaved = "";
    public int sessions;

    // Act I: The Empty Room
    public List<string> clues = new List<string>();   // ids of the clues found so far
    public bool dlgDone, moveOnOffered, moveOnDeclined;

    // Act II: The Streets Know
    public List<string> evidence = new List<string>();
    public bool bhaskarDone, vikramDone, reddyResolved;
    public bool hasUsb, hasIdCard, hasPlate;

    // Act III: The File
    public int act3Stage;                              // 0 start, 1 USB read, 2 sign-out sheet found, 3 envelope received
    public int panditAnswer = -1;                      // 0 truth, 1 people, 2 both
    public int branch = -1;                            // Act IV path: 0 Cafe Irani (Raj), 1 Kapoor house (Rohan), 2 Inspector Reddy

    // Act IV: The Web
    public bool allyRaj, allyRohan, allyReddy, hasRecordings, hasEmails;

    // Endings
    public List<int> endingsSeen = new List<int>();   // 0 Justice, 1 Sacrifice, 2 Revolution
}

/// <summary>Reads and writes the save file (persistentDataPath/shadows_of_chandni_save.json).</summary>
public static class SaveSystem
{
    static SaveData data;
    static string Path => System.IO.Path.Combine(Application.persistentDataPath, "shadows_of_chandni_save.json");

    public static SaveData Data
    {
        get { if (data == null) Load(); return data; }
    }

    public static void Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(Path));
                if (data != null) return;
            }
        }
        catch (Exception e) { Debug.LogWarning("Could not read save: " + e.Message); }
        data = new SaveData();
    }

    public static void Save()
    {
        if (data == null) return;
        try
        {
            data.lastSaved = DateTime.Now.ToString("d MMM yyyy, HH:mm");
            File.WriteAllText(Path, JsonUtility.ToJson(data, true));
        }
        catch (Exception e) { Debug.LogWarning("Could not write save: " + e.Message); }
    }

    /// <summary>Erase everything: name, progress, completed acts.</summary>
    public static void Reset()
    {
        data = new SaveData();
        try { if (File.Exists(Path)) File.Delete(Path); } catch (Exception e) { Debug.LogWarning("Could not delete save: " + e.Message); }
    }

    public static void AddClue(string id)
    {
        if (!Data.clues.Contains(id)) Data.clues.Add(id);
        Save();
    }

    public static void CompleteAct(int act)
    {
        Data.actsCompleted = Mathf.Max(Data.actsCompleted, act);
        Save();
    }

    /// <summary>Forget the partial progress of one act (used when that act is started over).</summary>
    public static void ResetAct(int act)
    {
        if (act == 1)
        {
            Data.clues.Clear();
            Data.dlgDone = Data.moveOnOffered = Data.moveOnDeclined = false;
        }
        else if (act == 2)
        {
            Data.evidence.Clear();
            Data.bhaskarDone = Data.vikramDone = Data.reddyResolved = false;
            Data.hasUsb = Data.hasIdCard = Data.hasPlate = false;
        }
        else if (act == 3)
        {
            Data.act3Stage = 0; Data.panditAnswer = -1; Data.branch = -1;
        }
        else if (act == 4)
        {
            Data.allyRaj = Data.allyRohan = Data.allyReddy = Data.hasRecordings = Data.hasEmails = false;
        }
        Save();
    }

    // ---- derived state for the menu
    public static bool Act1InProgress { get { return Data.actsCompleted < 1 && Data.clues.Count > 0; } }
    public static bool Act2InProgress { get { return Data.actsCompleted == 1 && (Data.evidence.Count > 0 || Data.bhaskarDone || Data.vikramDone); } }
    public static bool Act3InProgress { get { return Data.actsCompleted == 2 && Data.act3Stage > 0; } }

    /// <summary>Everything the detective has gathered: the apartment clues plus every evidence note since.</summary>
    public static int EvidenceCount { get { return Data.clues.Count + Data.evidence.Count; } }

    public static void AddEvidence(string e)
    {
        if (!Data.evidence.Contains(e)) Data.evidence.Add(e);
        Save();
    }
}
