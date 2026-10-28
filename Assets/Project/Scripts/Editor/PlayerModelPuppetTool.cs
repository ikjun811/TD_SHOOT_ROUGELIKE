using UnityEditor;
using UnityEngine;

// 플레이어 외형 모델 (C2) — 씬에서 플레이어(JUCharacterController)를 선택하고 메뉴 실행
//   모델을 플레이어 자식으로 넣고(로컬 0) 레이어 = JU 몸 메시 레이어, PlayerModelPuppet 부착
//   JU 캐릭터 구조(뼈대·무기·래그돌)는 건드리지 않음. 되돌리기 = 이 자식 오브젝트 삭제. 씬 저장은 직접
//   Add Player Model Puppet: SciFi 우주 군인 (재질 M_Player_*) — 이전 플레이어 외형, 이제 슈터 적용으로 이동 예정
//   Replace Player Model (Horror Soldier): 기존 외형 모델(PlayerModelPuppet 자식)을 지우고 Horrors(pack) 군인 Humanoid 프리팹으로 교체 (사용자 결정)
//     재질은 프리팹 그대로(몸 변형 1·장비 1) — Built-in Standard라 먼저 URP 변환 필요
public static class PlayerModelPuppetTool
{
    private const string ModelPath = "Assets/SciFi_Space_Soldier/Resources/Space_Soldier/Meshes/Soldier.FBX";
    private const string MaterialFolder = "Assets/Project/Characters/SpaceSoldier/Materials";
    private const string ObjectName = "PlayerModel (Soldier)";

    private const string HorrorSoldierPath = "Assets/Horrors(pack)/Character_Soldier/Prefab/Soldier_Humanoid.prefab";
    private const string HorrorObjectName = "PlayerModel (HorrorSoldier)";

    [MenuItem("Tools/TD Shoot/Add Player Model Puppet")]
    public static void Add()
    {
        Transform root = GetSelectedPlayer();
        if (root == null) return;
        if (root.Find(ObjectName) != null)
        {
            EditorUtility.DisplayDialog("Player Model", $"이미 '{ObjectName}'이 있습니다. 다시 만들려면 먼저 삭제하세요.", "확인");
            return;
        }

        GameObject model = CreateModel(root, ModelPath, ObjectName);
        if (model == null) return;

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
        Debug.Log($"[PlayerModel] '{ObjectName}' 추가 (재질 {matCount}개). 씬 저장은 직접.", model);
    }

    [MenuItem("Tools/TD Shoot/Replace Player Model (Horror Soldier)")]
    public static void ReplaceWithHorrorSoldier()
    {
        Transform root = GetSelectedPlayer();
        if (root == null) return;

        // 기존 외형 모델 제거 (Undo 가능)
        int removed = 0;
        foreach (PlayerModelPuppet old in root.GetComponentsInChildren<PlayerModelPuppet>(true))
        {
            Undo.DestroyObjectImmediate(old.gameObject);
            removed++;
        }

        GameObject model = CreateModel(root, HorrorSoldierPath, HorrorObjectName);
        if (model == null) return;

        int builtIn = 0;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (m != null && m.shader != null && !m.shader.name.StartsWith("Universal Render Pipeline")) builtIn++;
            }
        }
        if (builtIn > 0)
        {
            Debug.LogWarning($"[PlayerModel] URP가 아닌 재질 {builtIn}개 — Horrors(pack) 재질을 선택하고 Edit > Rendering > Materials > Convert Selected Built-in Materials to URP 실행 필요 (분홍색으로 보임)", model);
        }
        Debug.Log($"[PlayerModel] 기존 외형 {removed}개 제거 → '{HorrorObjectName}' 추가. 씬 저장은 직접.", model);
    }

    private static Transform GetSelectedPlayer()
    {
        GameObject selected = Selection.activeGameObject;
        JUTPS.JUCharacterController character = selected != null ? selected.GetComponentInParent<JUTPS.JUCharacterController>() : null;
        if (character == null)
        {
            EditorUtility.DisplayDialog("Player Model", "씬에서 플레이어(JUCharacterController가 있는 오브젝트)를 선택하세요.", "확인");
            return null;
        }
        return character.transform;
    }

    // 모델을 플레이어 자식으로 생성 + 레이어 맞춤 + PlayerModelPuppet 부착
    private static GameObject CreateModel(Transform root, string assetPath, string objectName)
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (modelAsset == null)
        {
            EditorUtility.DisplayDialog("Player Model", $"{assetPath}를 찾지 못했습니다.", "확인");
            return null;
        }

        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root);
        Undo.RegisterCreatedObjectUndo(model, "Add Player Model Puppet");
        model.name = objectName;
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

        Animator animator = model.GetComponent<Animator>();
        if (animator == null || animator.avatar == null || !animator.avatar.isHuman) Debug.LogWarning("[PlayerModel] 모델에 Humanoid Avatar가 있는 Animator가 없음", model);
        Undo.AddComponent<PlayerModelPuppet>(model);

        Selection.activeGameObject = model;
        return model;
    }
}
