using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// 적 종류 프리팹 생성 (GDD 8-6 #18) — 모델을 구하기 전 임시 외형(기본 적 캡슐 + 종류 색·크기)으로 행동부터 구현
//   Tools > TD Shoot > Generate Enemy Type Prefabs
//   Enemy_Dummy를 기반으로 한 프리팹 변형(Variant) Enemy_<종류>를 Assets/Project/Prefabs/Enemies에 생성 (이미 있으면 수치만 갱신)
//   종류 색 재질은 Assets/Project/Materials/Enemies/M_Enemy_<종류> (기본 적 재질 복사 + 색)
//   열린 씬의 WaveManager 일반 적 목록에 없으면 추가 (씬 저장은 직접)
//   모델을 구하면 생성된 프리팹의 외형만 교체 (행동은 EnemyBase·종류별 컴포넌트)
public static class EnemyPrefabGenerator
{
    private const string BasePrefabPath = "Assets/Project/Prefabs/Enemy_Dummy.prefab";
    private const string PrefabFolder = "Assets/Project/Prefabs/Enemies";
    private const string MaterialFolder = "Assets/Project/Materials/Enemies";

    private class Spec
    {
        public EnemyType type;
        public Color color;
        public float scale = 1f;
        public float health = 50f;
        public float moveSpeed = 3.5f;
        public float attackDamage = 10f;
        public float attackRange = 1.8f;
        public float attackCooldown = 1.2f;
        public float knockbackResistance;
        public bool useMeleeAttack = true;
        public bool useDefaultChase = true;
        public System.Type behaviour; // 종류별 행동 컴포넌트 (없으면 근접만)
        public System.Action<Component> configure; // 행동 컴포넌트 수치 (다시 실행하면 이 값으로 덮어씀)
    }

    // 임시값 (러너 = 기본 적: 체력 50·속도 3.5·공격 10·사거리 1.8·쿨 1.2)
    private static readonly List<Spec> Specs = new List<Spec>
    {
        new Spec
        {
            type = EnemyType.Brute, color = new Color(0.55f, 0.32f, 0.18f), scale = 1.6f,
            health = 200f, moveSpeed = 2.2f, attackDamage = 20f, attackRange = 2.4f, attackCooldown = 1.6f,
            knockbackResistance = 0.7f
        },
        new Spec
        {
            // 공격력 = 폭발 피해 (EnemyBomber), 사거리·쿨은 미사용
            type = EnemyType.Bomber, color = new Color(1f, 0.45f, 0.1f), scale = 0.8f,
            health = 30f, moveSpeed = 5f, attackDamage = 25f,
            useMeleeAttack = false, behaviour = typeof(EnemyBomber)
        },
        new Spec
        {
            // 공격력 = 탄 피해 (EnemyShooter), 이동도 EnemyShooter가 담당
            type = EnemyType.Shooter, color = new Color(0.2f, 0.75f, 0.55f), scale = 0.95f,
            health = 40f, moveSpeed = 3.2f, attackDamage = 12f,
            useMeleeAttack = false, useDefaultChase = false, behaviour = typeof(EnemyShooter),
            configure = c =>
            {
                EnemyShooter shooter = (EnemyShooter)c;
                shooter.preferredMaxRange = 13f; // 사거리 1.3배 (10 → 13, 사격은 15m까지)
                shooter.aimLineLength = 16f;
            }
        },
    };

    [MenuItem("Tools/TD Shoot/Generate Enemy Type Prefabs")]
    public static void Generate()
    {
        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
        if (basePrefab == null)
        {
            EditorUtility.DisplayDialog("적 프리팹 생성", $"{BasePrefabPath}을 찾지 못했습니다.", "확인");
            return;
        }

        EnsureFolder(PrefabFolder);
        EnsureFolder(MaterialFolder);

        List<GameObject> created = new List<GameObject>();
        foreach (Spec spec in Specs)
        {
            GameObject prefab = CreateOrUpdate(basePrefab, spec);
            if (prefab != null) created.Add(prefab);
        }
        AssetDatabase.SaveAssets();

        int registered = RegisterToWaveManager(created);
        Debug.Log($"[EnemyPrefabGenerator] 적 종류 프리팹 {created.Count}개 생성·갱신, WaveManager 일반 목록에 {registered}개 추가" +
                  (registered > 0 ? " — 씬을 저장하세요." : ""));
    }

    private static GameObject CreateOrUpdate(GameObject basePrefab, Spec spec)
    {
        string name = "Enemy_" + spec.type;
        string path = $"{PrefabFolder}/{name}.prefab";

        // 기존 프리팹이 있으면 그 내용을 열어 수정 (GUID 유지 → 씬 목록 참조 유지), 없으면 기본 적의 변형으로 새로 생성
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        GameObject root = exists
            ? PrefabUtility.LoadPrefabContents(path)
            : (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        root.name = name;

        try
        {
            if (!Apply(root, basePrefab, spec)) return null;
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            if (exists) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }
    }

    private static bool Apply(GameObject root, GameObject basePrefab, Spec spec)
    {
        EnemyBase enemy = root.GetComponent<EnemyBase>();
        if (enemy == null)
        {
            Debug.LogWarning($"[EnemyPrefabGenerator] {BasePrefabPath}에 EnemyBase가 없습니다.");
            return false;
        }

        root.transform.localScale = Vector3.one * spec.scale;

        Material material = GetOrCreateMaterial(basePrefab, spec);
        if (material != null)
        {
            foreach (MeshRenderer rend in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] mats = rend.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = material;
                rend.sharedMaterials = mats;
            }
        }

        SerializedObject so = new SerializedObject(enemy);
        so.FindProperty("enemyType").enumValueIndex = (int)spec.type;
        so.FindProperty("knockbackResistance").floatValue = spec.knockbackResistance;
        so.FindProperty("baseHealth").floatValue = spec.health;
        so.FindProperty("baseMoveSpeed").floatValue = spec.moveSpeed;
        so.FindProperty("baseAttackDamage").floatValue = spec.attackDamage;
        so.FindProperty("attackRange").floatValue = spec.attackRange;
        so.FindProperty("attackCooldown").floatValue = spec.attackCooldown;
        so.FindProperty("useMeleeAttack").boolValue = spec.useMeleeAttack;
        so.FindProperty("useDefaultChase").boolValue = spec.useDefaultChase;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (spec.behaviour != null)
        {
            Component behaviour = root.GetComponent(spec.behaviour);
            if (behaviour == null) behaviour = root.AddComponent(spec.behaviour);
            spec.configure?.Invoke(behaviour);
        }

        NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
        if (agent != null) agent.speed = spec.moveSpeed;
        return true;
    }

    private static Material GetOrCreateMaterial(GameObject basePrefab, Spec spec)
    {
        string path = $"{MaterialFolder}/M_Enemy_{spec.type}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            MeshRenderer baseRenderer = basePrefab.GetComponentInChildren<MeshRenderer>(true);
            if (baseRenderer == null || baseRenderer.sharedMaterial == null) return null;
            material = new Material(baseRenderer.sharedMaterial);
            AssetDatabase.CreateAsset(material, path);
        }
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", spec.color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", spec.color);
        EditorUtility.SetDirty(material);
        return material;
    }

    // 열린 씬의 WaveManager.normalEnemyPrefabs에 없으면 추가
    private static int RegisterToWaveManager(List<GameObject> prefabs)
    {
        WaveManager wave = Object.FindAnyObjectByType<WaveManager>(FindObjectsInactive.Include);
        if (wave == null)
        {
            Debug.LogWarning("[EnemyPrefabGenerator] 열린 씬에 WaveManager가 없어 목록 등록을 건너뜀 (GameplayScene을 열고 다시 실행하거나 직접 추가).");
            return 0;
        }

        SerializedObject so = new SerializedObject(wave);
        SerializedProperty list = so.FindProperty("normalEnemyPrefabs");
        int added = 0;
        foreach (GameObject prefab in prefabs)
        {
            bool exists = false;
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == prefab)
                {
                    exists = true;
                    break;
                }
            }
            if (exists) continue;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = prefab;
            added++;
        }
        if (added > 0)
        {
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(wave.gameObject.scene);
        }
        return added;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(folder);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
