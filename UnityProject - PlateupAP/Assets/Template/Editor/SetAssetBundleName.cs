using UnityEngine;
using UnityEditor;
using System.IO;

public class SetAssetBundleName : MonoBehaviour
{
    [MenuItem("Tools/Set AssetBundle Name")]
    static void SetBundleName()
    {
        string bundleName = "mod.assets"; // Change this if needed
        string[] assetGUIDs = AssetDatabase.FindAssets("t:Prefab t:AudioClip t:TextAsset");

        foreach (string guid in assetGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                importer.assetBundleName = bundleName;
                Debug.Log($"Assigned {bundleName} to {path}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("AssetBundle names set successfully!");
    }
}
