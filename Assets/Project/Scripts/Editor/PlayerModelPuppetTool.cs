using UnityEditor;
using UnityEngine;

// 플레이어 외형 모델 추가 (C2) — 씬에서 플레이어(JUCharacterController)를 선택하고 Tools > TD Shoot > Add Player Model Puppet
//   Soldier.FBX를 플레이어 자식으로 넣고(로컬 0) 재질 M_Player_*, 레이어 = JU 몸 메시 레이어, PlayerModelPuppet 부착
//   JU 캐릭터 구조(뼈대·무기·래그돌)는 건드리지 않음. 되돌리기 = 이 자식 오브젝트 삭제. 씬 저장은 직접
public static class PlayerModelPuppetTool
{
    private const string ModelPath = "Assets/SciFi_Space_Soldier/Resources/Space_Soldier/Meshes/Soldier.FBX";
    private const string MaterialFolder = "Assets/Project/Characters/SpaceSoldier/Materials";
    private const string ObjectName = "PlayerModel (Soldier)";

    [MenuItem("Tools/TD Shoot/Add Player Model Puppet")]
    public static void Add()
    {
        GameObject selected = Selection.activeGameObject;
        JUTPS.JUCharacterController character = selected != null ? selected.GetComponentInParent<JUTPS.JUCharacterController>() : null;
        if (character == null)
        {
            EditorUtility.DisplayDialog("Player Model", "씬에서 플레이어(JUCharacterController가 있는 오브젝트)를 선택하세요.", "확인");
            return;
        }
        Transform root = character.transform;
        if (root.Find(ObjectName) != null)
        {
            EditorUtility.DisplayDialog("Player Model", $"이미 '{ObjectName}'이 있습니다. 다시 만들려면 먼저 삭제하세요.", "확인");
            return;
        }

        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null)
        {
            EditorUtility.DisplayDialog("Player Model", $"{ModelPath}를 찾지 못했습니다.", "확인");
            return;
        }

        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root);
        Undo.RegisterCreatedObjectUndo(model, "Add Player Model Puppet");
        model.name = ObjectName;
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        // 레이어: JU 몸 메시와 같게 (콜라이더가 없어 판정에는 영향 없음)
        int layer = root.gameObject.layer;
        foreach (Transform child in root)
        {
            if (child != model.transform && child.GetComponent<SkinnedMeshRenderer>() != null) { layer = child.gameObject.layer; break; }
        }
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

        int matCount = 0;
        foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            foreach (string part in new[] { "Head", "Torso", "Legs" })
            {
                if (!smr.name.Contains(part)) continue;
                Material mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/M_Player_{part}.mat");
                if (mat != null) { smr.sharedMaterial = mat; matCount++; }
                break;
            }
        }

        Animator animator = model.GetComponent<Animator>();
        if (animator == null || animator.avatar == null) Debug.LogWarning("[PlayerModel] 모델에 Humanoid Avatar가 있는 Animator가 없음", model);
        Undo.AddComponent<PlayerModelPuppet>(model);

        Selection.activeGameObject = model;
        Debug.Log($"[PlayerModel] '{ObjectName}' 추가 (재질 {matCount}개, 레이어 {LayerMask.LayerToName(layer)}). 씬 저장은 직접.", model);
    }
}
