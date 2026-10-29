using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 보급 상자 외형 연결 (사용자 선택 2026-10-29: Warehouse Kit Crate Long + Horrors 소총)
//   Apply Supply Crate Model: 열린 씬에 SupplyCrateManager가 없으면 만들고(있으면 실행 중 자동 생성 안 함) 상자·무기 프리팹 연결 (씬 저장은 직접)
public static class SupplyCrateTools
{
    private const string CratePath = "Assets/SciFi Warehouse Kit/Prefabs/Props/Crates Barrels Pallets/Crate Long.prefab";
    private const string WeaponPath = "Assets/Horrors(pack)/Character_Soldier/Prefab/Rifle.prefab";

    [MenuItem("Tools/TD Shoot/Apply Supply Crate Model")]
    public static void Apply()
    {
        GameObject crate = AssetDatabase.LoadAssetAtPath<GameObject>(CratePath);
        GameObject weapon = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPath);
        if (crate == null)
        {
            EditorUtility.DisplayDialog("Supply Crate", $"{CratePath}을 찾지 못했습니다.", "확인");
            return;
        }
        if (weapon == null) Debug.LogWarning($"[SupplyCrateTools] 무기 프리팹 없음: {WeaponPath} — 상자만 연결합니다.");

        SupplyCrateManager manager = Object.FindAnyObjectByType<SupplyCrateManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            GameObject go = new GameObject("SupplyCrateManager");
            Undo.RegisterCreatedObjectUndo(go, "Create SupplyCrateManager");
            manager = go.AddComponent<SupplyCrateManager>();
        }

        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("crateModel").objectReferenceValue = crate;
        so.FindProperty("crateWeaponModel").objectReferenceValue = weapon;
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Selection.activeObject = manager.gameObject;
        Debug.Log("[SupplyCrateTools] 보급 상자 = Crate Long + 소총 연결 — 씬을 저장하세요.", manager);
    }
}
