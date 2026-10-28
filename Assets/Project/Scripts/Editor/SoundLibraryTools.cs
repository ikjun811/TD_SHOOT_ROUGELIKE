using UnityEditor;
using UnityEngine;

// 사운드 목록 에셋 생성
//   Create Sound Library: Assets/Project/Resources/SoundLibrary.asset 생성 (이미 있으면 선택만)
public static class SoundLibraryTools
{
    private const string Folder = "Assets/Project/Resources";
    private const string AssetPath = Folder + "/" + SoundLibrary.ResourcePath + ".asset";

    [MenuItem("Tools/TD Shoot/Create Sound Library")]
    public static void CreateLibrary()
    {
        SoundLibrary lib = AssetDatabase.LoadAssetAtPath<SoundLibrary>(AssetPath);
        if (lib == null)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Project", "Resources");
            lib = ScriptableObject.CreateInstance<SoundLibrary>();
            AssetDatabase.CreateAsset(lib, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SoundLibraryTools] 사운드 목록 생성 -> {AssetPath}");
        }
        Selection.activeObject = lib;
        EditorGUIUtility.PingObject(lib);
    }
}
