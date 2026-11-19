using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Reads and writes the campaign (SaveData) and the player profile (ProfileData), both in persistentDataPath:
///   shadows_of_chandni_save.json      the active campaign
///   shadows_of_chandni_profile.json   name, endings discovered, chapters unlocked, chapter-start snapshots
/// Every write goes to a .tmp file first and then replaces the real file, keeping the previous version as .bak.
/// A crash or power cut mid-write therefore leaves either the old file or the new one, never a half-written one;
/// if the main file is unreadable the .bak is used and the broken file is kept as .corrupt.
///
/// Chapter replay runs in a sandbox: SaveSystem.Data is then a copy (the campaign as it was when that act began),
/// nothing is written to the campaign file, and only profile unlocks (endings) are kept. Going back to the main
/// menu reloads the real campaign. The rules are in SaveModel.cs; the semantics are in Docs/SAVES.md.
/// </summary>
public static class SaveSystem
{
    static SaveData data;
    static ProfileData profile;
    static bool replay;

    // PINNACLE_SAVE_DIR redirects the saves (the automated play-test uses it so it never touches a real save)
    static string Dir => string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PINNACLE_SAVE_DIR")) ? Application.persistentDataPath : Environment.GetEnvironmentVariable("PINNACLE_SAVE_DIR");
    static string CampaignPath => System.IO.Path.Combine(Dir, "shadows_of_chandni_save.json");
    static string ProfilePath => System.IO.Path.Combine(Dir, "shadows_of_chandni_profile.json");

    public static SaveData Data { get { if (data == null) Load(); return data; } }
    public static ProfileData Profile { get { if (profile == null) Load(); return profile; } }

    /// <summary>True while a chapter is being replayed: the campaign on disk is not touched.</summary>
    public static bool IsReplay => replay;

    /// <summary>Loads the campaign and the profile from disk (leaving any chapter replay).</summary>
    public static void Load()
    {
        replay = false;
        profile = Read<ProfileData>(ProfilePath) ?? new ProfileData();
        data = Read<SaveData>(CampaignPath) ?? new SaveData();
        bool migrated = SaveRules.Migrate(data, profile);
        bool cleanD = SaveRules.Validate(data), cleanP = SaveRules.Validate(profile);
        if (!cleanD || !cleanP) Debug.LogWarning("Save: repaired out-of-range values in the save files.");
        if (migrated || !cleanD || !cleanP) { SaveProfile(); SaveCampaign(); }
    }

    public static void Save()
    {
        SaveProfile();
        if (!replay) SaveCampaign();
    }

    static void SaveCampaign()
    {
        if (data == null || replay) return;
        data.version = SaveData.CurrentVersion;
        data.lastSaved = DateTime.Now.ToString("d MMM yyyy, HH:mm");
        WriteAtomic(CampaignPath, JsonUtility.ToJson(data, true));
    }

    static void SaveProfile()
    {
        if (profile == null) return;
        profile.version = ProfileData.CurrentVersion;
        WriteAtomic(ProfilePath, JsonUtility.ToJson(profile, true));
    }

    /// <summary>Erase everything: the campaign and the profile (name, endings, unlocks).</summary>
    public static void Reset()
    {
        replay = false;
        data = new SaveData(); profile = new ProfileData();
        foreach (var p in new[] { CampaignPath, ProfilePath })
            foreach (var f in new[] { p, p + ".bak", p + ".tmp" })
                try { if (File.Exists(f)) File.Delete(f); } catch (Exception e) { Debug.LogWarning("Could not delete " + f + ": " + e.Message); }
    }

    /// <summary>New Game: a fresh campaign from Act I. The profile (name, endings, unlocked chapters) is kept.</summary>
    public static void NewCampaign()
    {
        replay = false;
        data = new SaveData();
        BeginAct(1);
    }

    /// <summary>
    /// Chapter replay: play `act` from its start without touching the campaign. Uses the snapshot taken when the
    /// campaign first reached that act; without one (saves from before snapshots existed) it uses the campaign
    /// with that act and everything after it cleared.
    /// </summary>
    public static void BeginReplay(int act)
    {
        var p = Profile;
        SaveData snap = null;
        if (act - 1 < p.chapterStarts.Count && !string.IsNullOrEmpty(p.chapterStarts[act - 1]))
            try { snap = JsonUtility.FromJson<SaveData>(p.chapterStarts[act - 1]); } catch (Exception) { snap = null; }
        if (snap == null) snap = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(Data));
        SaveRules.Migrate(snap, p);
        SaveRules.Validate(snap);
        SaveRules.ResetFrom(snap, act);
        data = snap;
        replay = true;
    }

    /// <summary>
    /// An act is starting from its beginning (not resumed): clear that act and everything after it, and (in the
    /// campaign) keep a snapshot so the chapter can be replayed later from exactly this state.
    /// </summary>
    public static void BeginAct(int act)
    {
        SaveRules.ResetFrom(Data, act);
        if (!replay)
        {
            var p = Profile;
            while (p.chapterStarts.Count < SaveRules.Acts) p.chapterStarts.Add("");
            p.chapterStarts[act - 1] = JsonUtility.ToJson(data);
        }
        Save();
    }

    /// <summary>Old name for BeginAct, kept so the act scripts read as before.</summary>
    public static void ResetAct(int act) { BeginAct(act); }

    public static void AddClue(string id)
    {
        if (!Data.clues.Contains(id)) Data.clues.Add(id);
        Save();
    }

    public static void CompleteAct(int act)
    {
        SaveRules.CompleteAct(Data, Profile, act);
        Save();
    }

    /// <summary>Called when an ending's credits roll (not when the casket is picked), so an interrupted ending is not counted.</summary>
    public static void RecordEnding(int ending)
    {
        SaveRules.RecordEnding(Profile, ending);
        Save();
    }

    // ---- derived state for the menu
    public static bool InProgress(int act) { return SaveRules.InProgress(Data, act); }
    public static bool Act1InProgress { get { return InProgress(1); } }
    public static bool Act2InProgress { get { return InProgress(2); } }
    public static bool Act3InProgress { get { return InProgress(3); } }

    /// <summary>Everything the detective has gathered: the apartment clues plus every evidence note since.</summary>
    public static int EvidenceCount { get { return SaveRules.EvidenceCount(Data); } }

    public static void AddEvidence(string e)
    {
        if (SaveRules.AddEvidence(Data, e, Data.currentAct)) Save();
    }

    // ------------------------------------------------------------------ files
    static T Read<T>(string path) where T : class
    {
        foreach (var f in new[] { path, path + ".bak" })
        {
            try
            {
                if (!File.Exists(f)) continue;
                string json = File.ReadAllText(f);
                if (string.IsNullOrWhiteSpace(json)) throw new Exception("empty file");
                var v = JsonUtility.FromJson<T>(json);
                if (v == null) throw new Exception("not a save file");
                if (f != path) Debug.LogWarning("Save: " + System.IO.Path.GetFileName(path) + " was unreadable; restored the backup.");
                return v;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Save: could not read " + f + " (" + e.Message + ")");
                if (f == path) try { File.Copy(f, f + ".corrupt", true); } catch (Exception) { }
            }
        }
        return null;
    }

    /// <summary>Write to path.tmp, then swap it in, keeping the previous file as path.bak.</summary>
    static void WriteAtomic(string path, string json)
    {
        string tmp = path + ".tmp", bak = path + ".bak";
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var w = new StreamWriter(fs))
            {
                w.Write(json);
                w.Flush();
                fs.Flush(true);                       // on disk before the swap
            }
            if (File.Exists(path))
            {
                try { File.Replace(tmp, path, bak); }
                catch (Exception)                     // platforms without an atomic replace: copy-then-move
                {
                    File.Copy(path, bak, true);
                    File.Delete(path);
                    File.Move(tmp, path);
                }
            }
            else File.Move(tmp, path);
        }
        catch (Exception e) { Debug.LogWarning("Could not write save " + path + ": " + e.Message); }
    }
}
