using UnityEditor;
using UnityEngine;

/// <summary>
/// Import settings for the realistic avatars in Assets/Resources/Characters/RB (Microsoft Rocketbox, MIT licence):
/// normal maps are flagged as normal maps, models come in without an Animator (they are posed in code, see CharacterNPC).
/// Also re-checks the folder on editor load, so files copied in before this script existed get fixed too.
/// </summary>
public class RocketboxImport : AssetPostprocessor
{
    const string Folder = "Assets/Resources/Characters/RB";

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var ti = (TextureImporter)assetImporter;
        Apply(ti, assetPath);
    }

    static bool Apply(TextureImporter ti, string path)
    {
        bool normal = path.Contains("_normal");
        var want = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        bool changed = ti.textureType != want || ti.maxTextureSize != 1024 || ti.mipmapEnabled == false;
        ti.textureType = want;
        ti.maxTextureSize = 1024;
        ti.mipmapEnabled = true;
        ti.anisoLevel = 4;
        if (!normal) { ti.sRGBTexture = true; ti.alphaIsTransparency = path.Contains("_opacity"); }
        return changed;
    }

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var mi = (ModelImporter)assetImporter;
        mi.animationType = ModelImporterAnimationType.None;
        mi.importAnimation = false;
        mi.importBlendShapes = false;
        mi.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importNormals = ModelImporterNormals.Import;
        mi.importTangents = ModelImporterTangents.CalculateMikk;
    }

    [InitializeOnLoadMethod]
    static void Recheck()
    {
        EditorApplication.delayCall += () =>
        {
            if (!AssetDatabase.IsValidFolder(Folder)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti != null && Apply(ti, path)) ti.SaveAndReimport();
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Folder }))
            {
                var mi = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as ModelImporter;
                if (mi != null && mi.animationType != ModelImporterAnimationType.None)
                {
                    mi.animationType = ModelImporterAnimationType.None; mi.importAnimation = false; mi.importBlendShapes = false;
                    mi.SaveAndReimport();
                }
            }
        };
    }
}
