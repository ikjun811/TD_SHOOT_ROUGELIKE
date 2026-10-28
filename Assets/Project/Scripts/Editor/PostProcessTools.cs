using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 전투 씬 후처리 프로파일 생성
//   Create Post Process Profile: Assets/Project/Resources/PostProcessProfile.asset을 기본값으로 생성 (이미 있으면 선택만)
//   만든 뒤 Inspector에서 블룸·비네트·색 조정 값을 바꾸면 다음 플레이부터 반영 (플레이 중에도 바로 반영)
//   Apply Render Quality Settings: SSAO 세기·반경 상향 (렌더러 에셋)
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

    // 렌더 품질 보강 (2026-10-29): 현재 URP 에셋의 렌더러들에 있는 SSAO 세기·반경 상향 (탑다운에서 물체가 바닥에 붙어 보이게)
    //   톤매핑·SMAA·그림자 거리는 GameplayPostProcess가 실행 중 적용
    private const float SsaoIntensity = 0.9f;
    private const float SsaoRadius = 0.6f;

    [MenuItem("Tools/TD Shoot/Apply Render Quality Settings")]
    public static void ApplyRenderQuality()
    {
        UniversalRenderPipelineAsset asset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (asset == null)
        {
            EditorUtility.DisplayDialog("렌더 품질", "URP 에셋을 찾지 못했습니다.", "확인");
            return;
        }

        int changed = 0;
        SerializedProperty list = new SerializedObject(asset).FindProperty("m_RendererDataList");
        for (int i = 0; list != null && i < list.arraySize; i++)
        {
            ScriptableRendererData data = list.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
            if (data == null) continue;
            foreach (ScriptableRendererFeature feature in data.rendererFeatures)
            {
                if (feature == null || feature.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                SerializedObject so = new SerializedObject(feature);
                SerializedProperty intensity = so.FindProperty("m_Settings.Intensity");
                SerializedProperty radius = so.FindProperty("m_Settings.Radius");
                if (intensity == null || radius == null) continue;
                intensity.floatValue = SsaoIntensity;
                radius.floatValue = SsaoRadius;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(feature);
                changed++;
                Debug.Log($"[PostProcessTools] {data.name} SSAO 세기 {SsaoIntensity}·반경 {SsaoRadius}");
            }
        }
        AssetDatabase.SaveAssets();
        if (changed == 0) Debug.LogWarning("[PostProcessTools] SSAO 렌더러 기능을 찾지 못했습니다 (렌더러 에셋에 Screen Space Ambient Occlusion 추가 필요).");
    }
}
