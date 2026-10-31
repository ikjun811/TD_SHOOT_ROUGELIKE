using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// 적 종류 프리팹 생성 (GDD 8-6 #18) — 행동은 EnemyBase·종류별 컴포넌트, 외형은 모델 또는 임시 캡슐(종류 색·크기)
//   Tools > TD Shoot > Generate Enemy Type Prefabs
//   Enemy_Base(이전 Enemy_Dummy, 몹으로는 안 씀)를 기반으로 한 프리팹 변형(Variant) Enemy_<종류>를 Assets/Project/Prefabs/Enemies에 생성 (이미 있으면 내용만 갱신, GUID 유지)
//   종류 색 재질은 Assets/Project/Materials/Enemies/M_Enemy_<종류> (기본 적 재질 복사 + 색, 캡슐 외형일 때)
//   열린 씬의 WaveManager 일반 적 목록에 없으면 추가 (씬 저장은 직접). 러너 모델이 있으면 기본 캡슐 적(Enemy_Dummy)은 일반 목록에서 뺌
//   모델 (P2): 캡슐 렌더러를 끄고 자식 "Model"로 모델 프리팹 생성 — 높이를 캡슐(2m)에 맞추고 발을 바닥에, 판정은 캡슐 콜라이더 그대로
//     컨트롤러 Assets/Project/Animations/Enemies/AC_Enemy_<종류> 생성: Locomotion(Speed 1D: 대기·걷기·달리기) + Attack0..N + Hit + Death (+ 추가 상태)
//     반복 동작(대기·걷기·달리기)은 모델 가져오기 설정의 Loop Time을 켬, EnemyAnimator 부착·연결
//     근접 타격 시점(EnemyBase.attackHitDelay) = 공격 동작 평균 길이 x 0.4 (0.2~0.8초)
//     directional(P3 슈터): Locomotion = 2D(MoveX·MoveZ, 루트 기준 속도) 대기·앞·뒤·좌·우(+앞 달리기) — 플레이어를 보며 뒷걸음·옆걸음
//     materials: 렌더러 이름에 포함된 글자 → 재질 교체 (원본이 Built-in 전용 셰이더인 모델)
//     upperBodyStates: 상체 전용 레이어(UpperBody, 아바타 마스크 = 머리·팔·손가락)에서 재생 — 상체만 있는 클립(슈터 사격)을
//       전신으로 재생하면 몸 위치 값이 비어 몸이 땅에 잠깐 들어가던 문제. 평소엔 빈 상태(영향 없음)
public static class EnemyPrefabGenerator
{
    // 모든 종류 프리팹의 변형 원본 (몹으로 쓰지 않음). 이전 이름 Enemy_Dummy → Remove Legacy Enemy Prefabs가 이동·이름 변경 (GUID 유지)
    private const string BasePrefabPath = "Assets/Project/Prefabs/Enemies/Base/Enemy_Base.prefab";
    private const string LegacyBasePrefabPath = "Assets/Project/Prefabs/Enemy_Dummy.prefab";
    // 임시 몹 (모델 교체 전 캡슐 적·엘리트 목록) + 그것만 쓰던 재질
    private static readonly string[] LegacyAssets =
    {
        "Assets/Project/Prefabs/Enemy_Dummy 1.prefab",
        "Assets/Project/Prefabs/Enemy_Dummy 2.prefab",
        "Assets/Project/Prefabs/Enemy_Dummy 3.prefab",
        "Assets/Project/Prefabs/Enemy_Dummy 4.prefab",
        "Assets/Project/Prefabs/New Material.mat",
        "Assets/Project/Prefabs/New Material 1.mat",
        "Assets/Project/Prefabs/New Material 2.mat",
    };
    private const string PrefabFolder = "Assets/Project/Prefabs/Enemies";
    private const string MaterialFolder = "Assets/Project/Materials/Enemies";
    private const string ControllerFolder = "Assets/Project/Animations/Enemies";
    private const string ModelChildName = "Model";
    private const float CapsuleHeight = 2f; // Enemy_Dummy 캡슐 높이 (중심 = 루트, 바닥 = -1)

    private class ModelSpec
    {
        public string prefabPath;
        public string clipFolder;
        // 클립은 파일 이름 끝(@ 뒤) 기준으로 찾음 (예: "Run" → Crazy@Run.FBX)
        // hit(피격 동작)는 비워 둠 — 제자리 움찔이라 이동 중 미끄러지고 연사에 걸음이 끊겨 제거 (사용자 결정, 피격 표현은 번쩍임·피해 숫자·넉백)
        public string idle, walk, run, hit, death;
        public string[] attacks = new string[0];
        public (string state, string clip, bool loop)[] extraStates = new (string, string, bool)[0];
        public float walkSpeed = 1.2f;  // 이 이동 속도(크기 보정 후)에서 걷기 100%
        public float runSpeed = 3.5f;   // 이 속도에서 달리기 100%
        public float heightScale = 1f;  // 캡슐 높이 대비 모델 키
        public float yaw;               // 모델 앞 방향 보정
        public bool directional;        // true면 walk = 앞, walkBack·walkLeft·walkRight 사용
        public string walkBack, walkLeft, walkRight;
        public (string rendererName, string materialPath)[] materials = new (string, string)[0];
        public (string state, string clip)[] upperBodyStates = new (string, string)[0];
        public bool keepCorpse = true;  // false면 사망 시 쓰러지는 동작 없이 바로 제거 (봄버: 폭발)
    }

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
        public ModelSpec model;       // 없으면 캡슐 외형
    }

    // 임시값 (러너 = 기본 적: 체력 50·속도 3.5·공격 10·사거리 1.8·쿨 1.2)
    private static readonly List<Spec> Specs = new List<Spec>
    {
        new Spec
        {
            // 러너 = Horrors(pack) Crazy (P2 첫 모델)
            type = EnemyType.Runner, color = new Color(0.85f, 0.85f, 0.85f),
            model = new ModelSpec
            {
                prefabPath = "Assets/Horrors(pack)/Character_Crazy/Prefab/Crazy.prefab",
                clipFolder = "Assets/Horrors(pack)/Character_Crazy/Animation",
                idle = "idle", walk = "Walk", run = "Run", death = "Dead",
                attacks = new[] { "Attack(1)", "Attack(2)" },
                walkSpeed = 1.2f, runSpeed = 3.5f
            }
        },
        new Spec
        {
            // 브루트 = Horrors(pack) Fat (P4) — 크기 1.6이라 크기 보정 속도 약 1.4 → 주로 걷기
            type = EnemyType.Brute, color = new Color(0.55f, 0.32f, 0.18f), scale = 1.6f,
            health = 200f, moveSpeed = 2.2f, attackDamage = 20f, attackRange = 2.4f, attackCooldown = 1.6f,
            knockbackResistance = 0.7f,
            model = new ModelSpec
            {
                prefabPath = "Assets/Horrors(pack)/Character_Fat/Prefab/Fat.prefab",
                clipFolder = "Assets/Horrors(pack)/Character_Fat/animation",
                idle = "Idel", walk = "Walk", run = "Run", death = "Dead",
                attacks = new[] { "Attack(1)", "Attack(2)", "Attack(3)" },
                walkSpeed = 1.3f, runSpeed = 3f
            }
        },
        new Spec
        {
            // 공격력 = 폭발 피해 (EnemyBomber), 사거리·쿨은 미사용
            // 봄버 = Horrors(pack) Prisoner (P4) — 자폭 예고 중 Shout(포효), 처치당해도 폭발하므로 시체 없음
            type = EnemyType.Bomber, color = new Color(1f, 0.45f, 0.1f), scale = 0.8f,
            health = 30f, moveSpeed = 5f, attackDamage = 25f,
            useMeleeAttack = false, behaviour = typeof(EnemyBomber),
            model = new ModelSpec
            {
                prefabPath = "Assets/Horrors(pack)/Character_Prisoner/Prefab/Prisoner.prefab",
                clipFolder = "Assets/Horrors(pack)/Character_Prisoner/Animation",
                idle = "Idle", walk = "Walk", run = "Run", death = "Death",
                extraStates = new[] { ("Fuse", "Shout", false) },
                walkSpeed = 1.3f, runSpeed = 3.5f,
                keepCorpse = false
            }
        },
        new Spec
        {
            // 공격력 = 탄 피해 (EnemyShooter), 이동도 EnemyShooter가 담당
            // 모델 = SciFi 우주 군인 (이전 플레이어 외형, 사용자 결정 P3) — 소총 포함 프리팹, 재질은 URP 사본(Convert Space Soldier Materials)
            type = EnemyType.Shooter, color = new Color(0.2f, 0.75f, 0.55f), scale = 0.95f,
            health = 40f, moveSpeed = 3.2f, attackDamage = 12f,
            useMeleeAttack = false, useDefaultChase = false, behaviour = typeof(EnemyShooter),
            model = new ModelSpec
            {
                prefabPath = "Assets/SciFi_Space_Soldier/Prefabs/Space_Soldier_A.prefab",
                clipFolder = "Assets/SciFi_Space_Soldier/Resources/Space_Soldier/Meshes",
                idle = "Rifle_Aiming_Idle", walk = "Rifle_Aiming_Walk_F", run = "Rifle_Run_F", death = "Death_A",
                directional = true, walkBack = "Rifle_Aiming_Walk_B", walkLeft = "Rifle_Aiming_Walk_L", walkRight = "Rifle_Aiming_Walk_R",
                upperBodyStates = new[] { ("Shoot", "Rifle_Aiming_Shoot") },
                walkSpeed = 1.5f, runSpeed = 3.2f,
                materials = new[]
                {
                    ("Head", "Assets/Project/Characters/SpaceSoldier/Materials/M_Player_Head.mat"),
                    ("Torso", "Assets/Project/Characters/SpaceSoldier/Materials/M_Player_Torso.mat"),
                    ("Legs", "Assets/Project/Characters/SpaceSoldier/Materials/M_Player_Legs.mat"),
                    ("Rifle", SpaceSoldierMaterialTools.RifleOutputPath),
                }
            },
            configure = c =>
            {
                EnemyShooter shooter = (EnemyShooter)c;
                shooter.preferredMaxRange = 13f; // 사거리 1.3배 (10 → 13, 사격은 15m까지)
                shooter.aimLineLength = 16f;
            }
        },
        new Spec
        {
            // 차저 = Horrors(pack) Mechanic (P5) — 돌진 경로 예고 → 직선 돌진, 공격력 = 돌진 피해 (EnemyCharger), 이동도 EnemyCharger가 담당
            type = EnemyType.Charger, color = new Color(0.85f, 0.2f, 0.25f), scale = 1.1f,
            health = 80f, moveSpeed = 3f, attackDamage = 25f, knockbackResistance = 0.3f,
            useMeleeAttack = false, useDefaultChase = false, behaviour = typeof(EnemyCharger),
            model = new ModelSpec
            {
                prefabPath = "Assets/Horrors(pack)/Character_Mechanic/Prefab/Mechanic.prefab",
                clipFolder = "Assets/Horrors(pack)/Character_Mechanic/Animations",
                idle = "Idle", walk = "Walk", run = "Run", death = "Dead",
                attacks = new[] { "Attack(1)", "Attack(2)", "Attack(3)" },
                walkSpeed = 1.3f, runSpeed = 3.5f
            }
        },
    };

    private static GameObject LoadBasePrefab()
    {
        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
        return basePrefab != null ? basePrefab : AssetDatabase.LoadAssetAtPath<GameObject>(LegacyBasePrefabPath);
    }

    [MenuItem("Tools/TD Shoot/Generate Enemy Type Prefabs")]
    public static void Generate()
    {
        GameObject basePrefab = LoadBasePrefab();
        if (basePrefab == null)
        {
            EditorUtility.DisplayDialog("적 프리팹 생성", $"{BasePrefabPath}을 찾지 못했습니다.", "확인");
            return;
        }

        EnsureFolder(PrefabFolder);
        EnsureFolder(MaterialFolder);

        List<GameObject> created = new List<GameObject>();
        bool runnerModel = false;
        foreach (Spec spec in Specs)
        {
            GameObject prefab = CreateOrUpdate(basePrefab, spec);
            if (prefab == null) continue;
            created.Add(prefab);
            if (spec.type == EnemyType.Runner && spec.model != null) runnerModel = true;
        }
        AssetDatabase.SaveAssets();

        int registered = RegisterToWaveManager(created, runnerModel ? basePrefab : null, out int removed);
        Debug.Log($"[EnemyPrefabGenerator] 적 종류 프리팹 {created.Count}개 생성·갱신, WaveManager 일반 목록에 {registered}개 추가" +
                  (removed > 0 ? $", 캡슐 기본 적 {removed}개 제외" : "") +
                  (registered + removed > 0 ? " — 씬을 저장하세요." : ""));
    }

    // 임시 몹 정리 (사용자 요청 2026-10-29): 원본 Enemy_Dummy는 지우면 종류 프리팹(변형)이 깨지므로 Enemies/Base/Enemy_Base로 이동만,
    // 웨이브 목록(일반·엘리트)에서 종류 프리팹 외 전부 빼고 종류 프리팹 5종 등록, 임시 몹 4개·전용 재질 3개는 휴지통으로 (씬 저장은 직접)
    [MenuItem("Tools/TD Shoot/Remove Legacy Enemy Prefabs")]
    public static void RemoveLegacy()
    {
        if (!EditorUtility.DisplayDialog("임시 몹 정리",
                "Enemy_Dummy 1~4와 전용 재질 3개를 휴지통으로 옮기고, Enemy_Dummy는 Enemies/Base/Enemy_Base(변형 원본)로 옮깁니다.\n" +
                "열린 씬의 WaveManager 목록은 적 종류 프리팹 5종만 남깁니다. 계속할까요?", "정리", "취소"))
        {
            return;
        }

        // 1. 원본 이동·이름 변경 (GUID 유지 → 종류 프리팹 변형 연결 유지)
        if (AssetDatabase.LoadAssetAtPath<GameObject>(LegacyBasePrefabPath) != null && AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath) == null)
        {
            EnsureFolder("Assets/Project/Prefabs/Enemies/Base");
            string error = AssetDatabase.MoveAsset(LegacyBasePrefabPath, BasePrefabPath);
            if (!string.IsNullOrEmpty(error)) Debug.LogWarning($"[EnemyPrefabGenerator] 원본 이동 실패: {error}");
        }

        // 2. 웨이브 목록 정리 (삭제 전에 — 참조가 Missing으로 남지 않게)
        int listChanged = CleanWaveManagerLists();

        // 3. 임시 몹·전용 재질 휴지통
        int trashed = 0;
        foreach (string path in LegacyAssets)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) == null) continue;
            if (AssetDatabase.MoveAssetToTrash(path)) trashed++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[EnemyPrefabGenerator] 임시 몹 정리: 휴지통 {trashed}개, 웨이브 목록 변경 {listChanged}건 — 씬을 저장하세요.");
    }

    // 일반 목록 = Prefabs/Enemies의 종류 프리팹만, 엘리트 목록 = 비움 (엘리트는 일반 목록에서 종류 가중치로 뽑음)
    private static int CleanWaveManagerLists()
    {
        WaveManager wave = Object.FindAnyObjectByType<WaveManager>(FindObjectsInactive.Include);
        if (wave == null)
        {
            Debug.LogWarning("[EnemyPrefabGenerator] 열린 씬에 WaveManager가 없어 목록 정리를 건너뜀 (GameplayScene을 열고 다시 실행).");
            return 0;
        }

        List<GameObject> typePrefabs = new List<GameObject>();
        foreach (Spec spec in Specs)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/Enemy_{spec.type}.prefab");
            if (prefab != null) typePrefabs.Add(prefab);
        }

        SerializedObject so = new SerializedObject(wave);
        SerializedProperty normal = so.FindProperty("normalEnemyPrefabs");
        SerializedProperty elite = so.FindProperty("eliteEnemyPrefabs");
        int changed = 0;
        for (int i = normal.arraySize - 1; i >= 0; i--)
        {
            Object value = normal.GetArrayElementAtIndex(i).objectReferenceValue;
            if (value is GameObject go && typePrefabs.Contains(go)) continue;
            normal.DeleteArrayElementAtIndex(i);
            changed++;
        }
        foreach (GameObject prefab in typePrefabs)
        {
            bool exists = false;
            for (int i = 0; i < normal.arraySize; i++)
            {
                if (normal.GetArrayElementAtIndex(i).objectReferenceValue == prefab) { exists = true; break; }
            }
            if (exists) continue;
            normal.arraySize++;
            normal.GetArrayElementAtIndex(normal.arraySize - 1).objectReferenceValue = prefab;
            changed++;
        }
        changed += elite.arraySize;
        elite.arraySize = 0;

        if (changed > 0)
        {
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(wave.gameObject.scene);
        }
        return changed;
    }

    private static GameObject CreateOrUpdate(GameObject basePrefab, Spec spec)
    {
        string name = "Enemy_" + spec.type;
        string path = $"{PrefabFolder}/{name}.prefab";

        // 모델 준비(반복 설정·컨트롤러)는 프리팹을 열기 전에 — 가져오기 다시 하기가 열린 프리팹 내용과 섞이지 않게
        AnimatorController controller = spec.model != null ? BuildController(spec) : null;
        if (spec.model != null && controller == null) return null;

        // 기존 프리팹이 있으면 그 내용을 열어 수정 (GUID 유지 → 씬 목록 참조 유지), 없으면 기본 적의 변형으로 새로 생성
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        GameObject root = exists
            ? PrefabUtility.LoadPrefabContents(path)
            : (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        root.name = name;

        try
        {
            if (!Apply(root, basePrefab, spec, controller)) return null;
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            if (exists) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }
    }

    private static bool Apply(GameObject root, GameObject basePrefab, Spec spec, AnimatorController controller)
    {
        EnemyBase enemy = root.GetComponent<EnemyBase>();
        if (enemy == null)
        {
            Debug.LogWarning($"[EnemyPrefabGenerator] {BasePrefabPath}에 EnemyBase가 없습니다.");
            return false;
        }

        root.transform.localScale = Vector3.one * spec.scale;

        // 캡슐 외형 (루트 렌더러): 모델이 있으면 끔, 없으면 종류 색
        MeshRenderer capsule = root.GetComponent<MeshRenderer>();
        if (capsule != null)
        {
            capsule.enabled = spec.model == null;
            Material material = GetOrCreateMaterial(basePrefab, spec);
            if (material != null)
            {
                Material[] mats = capsule.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = material;
                capsule.sharedMaterials = mats;
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

        ApplyModel(root, spec, controller);

        // 근접 타격 시점 = 공격 동작 평균 길이의 hitFraction (휘두르는 순간), 모델 없으면 기본값
        SerializedObject hitSo = new SerializedObject(enemy);
        hitSo.FindProperty("attackHitDelay").floatValue = GetAttackHitDelay(spec);
        hitSo.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    private const float DefaultHitDelay = 0.35f;
    private const float HitFraction = 0.4f;

    private static float GetAttackHitDelay(Spec spec)
    {
        if (spec.model == null || spec.model.attacks.Length == 0) return DefaultHitDelay;
        float total = 0f;
        int count = 0;
        foreach (string attack in spec.model.attacks)
        {
            AnimationClip clip = FindClip(spec.model.clipFolder, attack, false);
            if (clip == null) continue;
            total += clip.length;
            count++;
        }
        return count > 0 ? Mathf.Clamp(total / count * HitFraction, 0.2f, 0.8f) : DefaultHitDelay;
    }

    // 모델 자식 교체 + EnemyAnimator 연결 (모델 없으면 기존 모델·EnemyAnimator 제거)
    private static void ApplyModel(GameObject root, Spec spec, AnimatorController controller)
    {
        Transform old = root.transform.Find(ModelChildName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        EnemyAnimator enemyAnimator = root.GetComponent<EnemyAnimator>();
        if (spec.model == null)
        {
            if (enemyAnimator != null) Object.DestroyImmediate(enemyAnimator);
            return;
        }

        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(spec.model.prefabPath);
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
        model.name = ModelChildName;
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        // 크기 맞춤: 루트 기준 키를 캡슐 높이에, 발(바운드 아래)을 캡슐 바닥(-1)에
        Bounds bounds = GetRendererBounds(model);
        float rootScale = Mathf.Max(0.0001f, root.transform.lossyScale.y);
        float height = bounds.size.y / rootScale;
        float bottom = (bounds.min.y - root.transform.position.y) / rootScale;
        float fit = height > 0.01f ? CapsuleHeight * spec.model.heightScale / height : 1f;
        model.transform.localScale = Vector3.one * fit;
        model.transform.localRotation = Quaternion.Euler(0f, spec.model.yaw, 0f);
        model.transform.localPosition = new Vector3(0f, -CapsuleHeight * 0.5f - bottom * fit, 0f);

        foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = root.layer;
        foreach (Collider col in model.GetComponentsInChildren<Collider>(true)) col.enabled = false; // 판정은 캡슐만

        foreach ((string rendererName, string materialPath) in spec.model.materials)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat == null)
            {
                Debug.LogWarning($"[EnemyPrefabGenerator] {spec.type} 재질 없음: {materialPath}");
                continue;
            }
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.name.Contains(rendererName)) continue;
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        Animator animator = model.GetComponentInChildren<Animator>(true);
        if (animator == null) animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        if (animator.GetComponent<EnemyAnimationEvents>() == null) animator.gameObject.AddComponent<EnemyAnimationEvents>(); // 클립 이벤트(FootStep) 수신

        if (enemyAnimator == null) enemyAnimator = root.AddComponent<EnemyAnimator>();
        enemyAnimator.animator = animator;
        enemyAnimator.attackCount = spec.model.attacks.Length;
        enemyAnimator.hasHitState = !string.IsNullOrEmpty(spec.model.hit);
        enemyAnimator.directional = spec.model.directional;
        enemyAnimator.keepCorpse = spec.model.keepCorpse;
        EditorUtility.SetDirty(enemyAnimator);
    }

    private static Bounds GetRendererBounds(GameObject model)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(model.transform.position, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    // 컨트롤러 생성 (다시 실행하면 새로 만듦)
    private static AnimatorController BuildController(Spec spec)
    {
        ModelSpec m = spec.model;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(m.prefabPath) == null)
        {
            Debug.LogWarning($"[EnemyPrefabGenerator] {spec.type} 모델 없음: {m.prefabPath}");
            return null;
        }

        AnimationClip idle = FindClip(m.clipFolder, m.idle, true);
        AnimationClip walk = FindClip(m.clipFolder, m.walk, true);
        AnimationClip run = FindClip(m.clipFolder, m.run, true);
        AnimationClip hit = FindClip(m.clipFolder, m.hit, false);
        AnimationClip death = FindClip(m.clipFolder, m.death, false);
        if (idle == null && walk == null && run == null)
        {
            Debug.LogWarning($"[EnemyPrefabGenerator] {spec.type} 이동 애니메이션을 찾지 못했습니다 ({m.clipFolder}).");
            return null;
        }

        EnsureFolder(ControllerFolder);
        string path = $"{ControllerFolder}/AC_Enemy_{spec.type}.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        if (m.directional)
        {
            // 2D: 루트 기준 이동 방향 (x 오른쪽, z 앞)
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveZ";
            if (idle != null) tree.AddChild(idle, Vector2.zero);
            if (walk != null) tree.AddChild(walk, new Vector2(0f, m.walkSpeed));
            AnimationClip back = FindClip(m.clipFolder, m.walkBack, true);
            AnimationClip left = FindClip(m.clipFolder, m.walkLeft, true);
            AnimationClip right = FindClip(m.clipFolder, m.walkRight, true);
            if (back != null) tree.AddChild(back, new Vector2(0f, -m.walkSpeed));
            if (left != null) tree.AddChild(left, new Vector2(-m.walkSpeed, 0f));
            if (right != null) tree.AddChild(right, new Vector2(m.walkSpeed, 0f));
            if (run != null) tree.AddChild(run, new Vector2(0f, m.runSpeed));
        }
        else
        {
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            if (idle != null) tree.AddChild(idle, 0f);
            if (walk != null) tree.AddChild(walk, m.walkSpeed);
            if (run != null) tree.AddChild(run, m.runSpeed);
        }
        sm.defaultState = locomotion;

        for (int i = 0; i < m.attacks.Length; i++)
        {
            AddOneShot(sm, "Attack" + i, FindClip(m.clipFolder, m.attacks[i], false), locomotion);
        }
        if (hit != null) AddOneShot(sm, "Hit", hit, locomotion);
        foreach ((string state, string clipName, bool loop) in m.extraStates)
        {
            AnimationClip clip = FindClip(m.clipFolder, clipName, loop);
            if (loop) sm.AddState(state).motion = clip; // 반복 상태는 코드가 다른 상태로 넘길 때까지 유지
            else AddOneShot(sm, state, clip, locomotion);
        }
        if (death != null) sm.AddState("Death").motion = death; // 끝 = 마지막 자세 유지

        if (m.upperBodyStates.Length > 0) BuildUpperBodyLayer(controller, m);

        EditorUtility.SetDirty(controller);
        return controller;
    }

    // 상체 레이어: 빈 상태(기본) + 한 번 재생 후 빈 상태로 복귀하는 상태들, 마스크는 컨트롤러 하위 에셋
    private static void BuildUpperBodyLayer(AnimatorController controller, ModelSpec m)
    {
        AvatarMask mask = new AvatarMask { name = "UpperBodyMask" };
        for (AvatarMaskBodyPart part = 0; part < AvatarMaskBodyPart.LastBodyPart; part++)
        {
            bool upper = part == AvatarMaskBodyPart.Head || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                         || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers;
            mask.SetHumanoidBodyPartActive(part, upper);
        }
        AssetDatabase.AddObjectToAsset(mask, controller);

        controller.AddLayer("UpperBody");
        AnimatorControllerLayer[] layers = controller.layers;
        AnimatorControllerLayer layer = layers[layers.Length - 1];
        layer.defaultWeight = 1f;
        layer.avatarMask = mask;
        layer.blendingMode = AnimatorLayerBlendingMode.Override;
        controller.layers = layers;

        AnimatorStateMachine sm = layer.stateMachine;
        AnimatorState empty = sm.AddState("Empty");
        sm.defaultState = empty;
        foreach ((string state, string clipName) in m.upperBodyStates)
        {
            AddOneShot(sm, state, FindClip(m.clipFolder, clipName, false), empty);
        }
    }

    // 한 번 재생 후 Locomotion으로 복귀
    private static void AddOneShot(AnimatorStateMachine sm, string name, AnimationClip clip, AnimatorState back)
    {
        AnimatorState state = sm.AddState(name);
        state.motion = clip;
        AnimatorStateTransition exit = state.AddTransition(back);
        exit.hasExitTime = true;
        exit.exitTime = 0.9f;
        exit.duration = 0.15f;
    }

    // 폴더에서 이름이 "@접미사"로 끝나는 모델 파일(없으면 접미사로 끝나는 파일)의 클립, loop면 가져오기 설정 Loop Time 켬
    private static AnimationClip FindClip(string folder, string suffix, bool loop)
    {
        if (string.IsNullOrEmpty(suffix)) return null;
        string found = null;
        foreach (string guid in AssetDatabase.FindAssets("", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            if (file.EndsWith("@" + suffix, System.StringComparison.OrdinalIgnoreCase)) { found = path; break; }
            if (found == null && file.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase)) found = path;
        }
        if (found == null)
        {
            Debug.LogWarning($"[EnemyPrefabGenerator] 클립 없음: {folder} / *{suffix}");
            return null;
        }

        if (loop) SetLoop(found);
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(found))
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
        }
        return null;
    }

    private static void SetLoop(string path)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return;
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        bool changed = false;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            if (clip.loopTime) continue;
            clip.loopTime = true;
            changed = true;
        }
        if (!changed) return;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
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

    // 열린 씬의 WaveManager.normalEnemyPrefabs에 없으면 추가, removePrefab이 있으면 목록에서 뺌
    private static int RegisterToWaveManager(List<GameObject> prefabs, GameObject removePrefab, out int removed)
    {
        removed = 0;
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
        if (removePrefab != null)
        {
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue != removePrefab) continue;
                list.DeleteArrayElementAtIndex(i);
                removed++;
            }
        }
        if (added > 0 || removed > 0)
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
