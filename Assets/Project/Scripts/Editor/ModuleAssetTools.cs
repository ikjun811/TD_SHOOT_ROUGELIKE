using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 모듈 에셋 정리·등록 (A5a, 2026-10-19)
//   Remove Unused Module Rarities: 운영하지 않는 레어도의 작동·특수 모듈 에셋을 휴지통으로 (작동 = 엘리트만, 특수 = 희귀·레전더리만)
//   Register All Modules to Drop Manager: Modules 폴더의 모든 모듈을 열린 씬의 ItemDropManager.allModules에 등록 (씬 저장은 직접)
public static class ModuleAssetTools
{
    private const string ModulesFolder = "Assets/Project/Scripts/ScriptableObjects/Modules";
    private const string MechanismFolder = ModulesFolder + "/Mechanism";
    private const string SpecialFolder = ModulesFolder + "/Special";

    [MenuItem("Tools/TD Shoot/Remove Unused Module Rarities")]
    public static void RemoveUnusedRarities()
    {
        List<string> targets = new List<string>();
        CollectUnused(MechanismFolder, MechanismModuleGenerator.Rarities, targets);
        CollectUnused(SpecialFolder, SpecialModuleGenerator.Rarities, targets);

        if (targets.Count == 0)
        {
            Debug.Log("[ModuleAssetTools] 정리할 모듈 에셋이 없습니다.");
            return;
        }

        string preview = string.Join("\n", targets.Take(15).Select(System.IO.Path.GetFileNameWithoutExtension));
        if (targets.Count > 15) preview += $"\n... 외 {targets.Count - 15}개";
        if (!EditorUtility.DisplayDialog("모듈 레어도 정리",
            $"운영하지 않는 레어도의 모듈 {targets.Count}개를 휴지통으로 옮깁니다.\n(작동: 엘리트만 유지 / 특수: 희귀·레전더리만 유지)\n\n{preview}",
            "옮기기", "취소"))
        {
            return;
        }

        List<string> failed = new List<string>();
        AssetDatabase.MoveAssetsToTrash(targets.ToArray(), failed);
        AssetDatabase.Refresh();
        Debug.Log($"[ModuleAssetTools] 휴지통으로 {targets.Count - failed.Count}개 이동, 실패 {failed.Count}개. 이어서 Register All Modules to Drop Manager를 실행하세요.");
        foreach (string path in failed) Debug.LogWarning($"[ModuleAssetTools] 이동 실패: {path}");
    }

    private static void CollectUnused(string folder, RarityType[] keepRarities, List<string> result)
    {
        if (!AssetDatabase.IsValidFolder(folder)) return;

        foreach (string guid in AssetDatabase.FindAssets("t:ModuleDataSO", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModuleDataSO module = AssetDatabase.LoadAssetAtPath<ModuleDataSO>(path);
            if (module == null || module.category == ModuleCategory.Unique) continue; // 유니크는 레전더리 전용이라 대상 아님
            if (!keepRarities.Contains(module.rarity)) result.Add(path);
        }
    }

    [MenuItem("Tools/TD Shoot/Register All Modules to Drop Manager")]
    public static void RegisterAllModules()
    {
        ItemDropManager manager = Object.FindAnyObjectByType<ItemDropManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            EditorUtility.DisplayDialog("모듈 등록", "열린 씬에 ItemDropManager가 없습니다. GameplayScene을 열고 다시 실행하세요.", "확인");
            return;
        }

        List<ModuleDataSO> modules = AssetDatabase.FindAssets("t:ModuleDataSO", new[] { ModulesFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path)
            .Select(AssetDatabase.LoadAssetAtPath<ModuleDataSO>)
            .Where(m => m != null)
            .ToList();

        SerializedObject so = new SerializedObject(manager);
        SerializedProperty list = so.FindProperty("allModules");
        list.arraySize = modules.Count;
        for (int i = 0; i < modules.Count; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = modules[i];
        }
        so.ApplyModifiedProperties(); // 실행 취소(Ctrl+Z) 기록 포함

        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);

        int stat = modules.Count(m => m.category == ModuleCategory.Stat);
        int mechanism = modules.Count(m => m.category == ModuleCategory.Mechanism);
        int special = modules.Count(m => m.category == ModuleCategory.Special);
        int unique = modules.Count(m => m.category == ModuleCategory.Unique);
        Debug.Log($"[ModuleAssetTools] ItemDropManager에 모듈 {modules.Count}개 등록 (스탯 {stat} / 작동 {mechanism} / 특수 {special} / 유니크 {unique}). 씬을 저장하세요.");
    }
}
