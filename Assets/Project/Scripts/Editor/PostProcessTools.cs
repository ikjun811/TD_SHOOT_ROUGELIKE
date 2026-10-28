using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 전투 씬 후처리 프로파일 생성
//   Create Post Process Profile: Assets/Project/Resources/PostProcessProfile.asset을 기본값으로 생성 (이미 있으면 선택만)
//   만든 뒤 Inspector에서 블룸·비네트·색 조정 값을 바꾸면 다음 플레이부터 반영 (플레이 중에도 바로 반영)
public static class PostProcessTools
{
    private const string Folder = "Assets/Project/Resources";
    private const string AssetPath = Folder + "/" + GameplayPostProcess.ResourcePath + ".asset";

    [MenuItem("Tools/TD Shoot/Create Post Process Profile")]
    public static void CreateProfile()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AssetPath);
        if (profile == null)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Project", "Resources");
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, AssetPath);

            GameplayPostProcess.FillDefaults(profile);
            // 효과 컴포넌트는 프로파일 에셋의 하위 에셋으로 저장해야 유지됨
            foreach (VolumeComponent component in profile.components)
            {
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PostProcessTools] 후처리 프로파일 생성 -> {AssetPath}");
        }
        Selection.activeObject = profile;
        EditorGUIUtility.PingObject(profile);
    }
}
