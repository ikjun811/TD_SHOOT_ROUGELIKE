using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 수류탄 외형 교체 (사용자 결정 2026-10-29: JU Granade → Horrors(pack) Granade, 적·플레이어 모델과 화풍 통일)
//   Apply Horror Grenade Model: 열린 씬 GrenadeSkill의 grenadeModel 교체 + grenadeModelSize = 기존 모델의 가장 긴 변(같은 크기로 보이게)
//   대시 폭탄도 GrenadeSkill.CreateGrenadeVisual을 써서 함께 바뀜 (씬 저장은 직접)
public static class GrenadeModelTools
{
    private const string ModelPath = "Assets/Horrors(pack)/Character_Soldier/Prefab/Granade.prefab";
    private const float FallbackSize = 0.22f;

    [MenuItem("Tools/TD Shoot/Apply Horror Grenade Model")]
    public static void Apply()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
        {
            EditorUtility.DisplayDialog("Grenade Model", $"{ModelPath}을 찾지 못했습니다.", "확인");
            return;
        }

        GrenadeSkill skill = Object.FindAnyObjectByType<GrenadeSkill>(FindObjectsInactive.Include);
        if (skill == null)
        {
            EditorUtility.DisplayDialog("Grenade Model", "열린 씬에 GrenadeSkill이 없습니다.", "확인");
            return;
        }

        SerializedObject so = new SerializedObject(skill);
        SerializedProperty modelProp = so.FindProperty("grenadeModel");
        SerializedProperty sizeProp = so.FindProperty("grenadeModelSize");

        // 기존 모델 크기를 재서 같은 크기로 (이미 교체돼 있거나 없으면 기본값)
        GameObject previous = modelProp.objectReferenceValue as GameObject;
        float size = FallbackSize;
        if (previous != null && previous != model)
        {
            float measured = MeasureLongest(previous);
            if (measured > 0.01f) size = measured;
        }
        else if (sizeProp.floatValue > 0f)
        {
            size = sizeProp.floatValue;
        }

        modelProp.objectReferenceValue = model;
        sizeProp.floatValue = size;
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(skill.gameObject.scene);
        Selection.activeObject = skill.gameObject;
        Debug.Log($"[GrenadeModelTools] 수류탄 모델 = Horrors Granade, 크기 {size:0.###}m (이전: {(previous != null ? previous.name : "없음")}) — 씬을 저장하세요.", skill);
    }

    // 프리팹을 잠깐 만들어 렌더러 바운드의 가장 긴 변
    private static float MeasureLongest(GameObject prefab)
    {
        GameObject temp = Object.Instantiate(prefab);
        temp.hideFlags = HideFlags.HideAndDontSave;
        temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        try
        {
            Renderer[] renderers = temp.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return 0f;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        }
        finally
        {
            Object.DestroyImmediate(temp);
        }
    }
}
