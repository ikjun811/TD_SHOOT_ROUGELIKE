using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 메인 화면 디오라마 배경 (MainMenuDiorama) — 탑다운 패닝(ArenaBackdrop)과 비교용, 둘 중 하나만 켬
//   Tools > TD Shoot > Build Main Menu Diorama: 열린 MainMenu 씬에 "MenuDiorama" 생성 (있으면 확인 후 다시 만듦 — 직접 옮긴 배치 초기화)
//     우주정거장 바닥·벽·상자·통 + Horrors 군인(조준 대기 동작) + 괴물 3종(Crazy·Fat·Prisoner, 적 컨트롤러 대기 동작) + 시체
//     조명: 천장등 지점 2(군인 위·깜빡임 1) + 손전등(군인 가슴 → 괴물, 부드러운 그림자) + 뒤쪽 붉은 비상등
//     모든 소품·인물은 렌더러 바운드 기준으로 바닥(y 0)에 세움, 인물은 키를 맞춤. 콜라이더 끔
//   Tools > TD Shoot > Main Menu Backdrop - Use Arena / Use Diorama: 둘 중 하나만 켬
//   Undo 가능, 씬 저장은 직접
public static class MainMenuDioramaTools
{
    private const string DioramaName = "MenuDiorama";
    private const string ArenaName = "ArenaBackdrop";
    private static readonly Vector3 RootPosition = new Vector3(200f, 0f, 0f); // 맵 배경과 겹치지 않는 곳

    private const string Kit = "Assets/SciFi_TopDown_SpaceStation/TDSpaceStation_Prefabs_LWRP/";
    private const string KitProps = Kit + "TDSpaceStation_Props/";
    private const string Horrors = "Assets/Horrors(pack)/";
    private const string EnemyControllers = "Assets/Project/Animations/Enemies/";
    private const string SoldierControllerPath = "Assets/Project/Animations/Menu/AC_Menu_Soldier.controller";

    [MenuItem("Tools/TD Shoot/Build Main Menu Diorama")]
    public static void Build()
    {
        MainMenuUI menuUI = Object.FindAnyObjectByType<MainMenuUI>(FindObjectsInactive.Include);
        if (menuUI == null)
        {
            EditorUtility.DisplayDialog("Main Menu", "열린 씬에서 MainMenuUI를 찾지 못했습니다. MainMenu 씬을 열어 주세요.", "확인");
            return;
        }
        Scene scene = menuUI.gameObject.scene;
        SceneManager.SetActiveScene(scene);

        GameObject existing = FindRoot(scene, DioramaName);
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("Main Menu Diorama", "MenuDiorama가 이미 있습니다. 다시 만들면 직접 옮긴 배치가 초기화됩니다.", "다시 만들기", "취소")) return;
            Undo.DestroyObjectImmediate(existing);
        }

        GameObject root = new GameObject(DioramaName);
        Undo.RegisterCreatedObjectUndo(root, "Build Main Menu Diorama");
        root.transform.position = RootPosition;
        Transform t = root.transform;

        // 바닥 3x3 (10m 판, 윗면 y 0)
        GameObject floor = Load(Kit + "SpaceStation_Room_A_Floor_LWRP.prefab");
        Transform floors = Group(t, "Floor");
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++) PlaceTop(floor, floors, new Vector3(x * 10f, 0f, 3f + z * 10f), 0f);
        }

        // 벽: 뒤쪽 한 줄 + 왼쪽 일부 (어둠 속 배경)
        GameObject wall = Load(Kit + "SpaceStation_Room_A_Wall_Internal_LWRP.prefab");
        GameObject wallB = Load(Kit + "SpaceStation_Room_A_Wall_Internal_B_LWRP.prefab");
        GameObject column = Load(Kit + "SpaceStation_Room_A_Column_LWRP.prefab");
        Transform walls = Group(t, "Walls");
        for (int i = -2; i <= 2; i++) PlaceWall(i == 1 ? wallB : wall, walls, new Vector3(i * 5f, 0f, 12.5f), false);
        for (int i = 0; i <= 2; i++) PlaceWall(wall, walls, new Vector3(-12.5f, 0f, 0f + i * 5f), true);
        Place(column, walls, new Vector3(-12.5f, 0f, 12.5f), 0f);
        Place(column, walls, new Vector3(2.5f, 0f, 12.5f), 0f);

        // 소품
        Transform props = Group(t, "Props");
        GameObject crate = Load(KitProps + "SpaceStation_Crate_A_LWRP.prefab");
        GameObject barrel = Load(KitProps + "SpaceStation_Barrel_A_LWRP.prefab");
        GameObject vent = Load(KitProps + "SpaceStation_FloorVent_A_LWRP.prefab");
        GameObject lower = Place(crate, props, new Vector3(-3.6f, 0f, 4.2f), 8f);
        if (lower != null) Place(crate, props, new Vector3(-3.6f, GetBounds(lower).max.y - RootPosition.y, 4.2f), -6f);
        Place(crate, props, new Vector3(-5.4f, 0f, 5.6f), 28f);
        Place(barrel, props, new Vector3(-6.2f, 0f, 2.6f), 0f);
        Place(barrel, props, new Vector3(5.8f, 0f, 1.4f), 40f);
        PlaceTop(vent, props, new Vector3(0.2f, 0.03f, 3.2f), 0f);
        Place(Load(Horrors + "Man_under_cloth/Prefab/Man_under_cloth.prefab"), props, new Vector3(3.4f, 0f, 2.4f), 70f);
        Place(Load(Horrors + "Man_in_the_bag/Prefab/Man_in_the_bag.prefab"), props, new Vector3(-1.8f, 0f, 7.2f), 130f);

        // 인물: 군인 → 괴물을 향함, 괴물 → 군인을 향함
        Transform actors = Group(t, "Actors");
        Vector3 soldierPos = new Vector3(1.2f, 0f, 0f);
        Vector3 crazyPos = new Vector3(4f, 0f, 7.5f);
        PlaceActor(Load(Horrors + "Character_Soldier/Prefab/Solder.prefab"), actors, "Soldier", soldierPos, YawTo(soldierPos, crazyPos), 1.85f, GetSoldierController());
        PlaceActor(Load(Horrors + "Character_Crazy/Prefab/Crazy.prefab"), actors, "Crazy", crazyPos, YawTo(crazyPos, soldierPos), 1.9f, LoadController("AC_Enemy_Runner"));
        Vector3 fatPos = new Vector3(-0.6f, 0f, 9.6f);
        PlaceActor(Load(Horrors + "Character_Fat/Prefab/Fat.prefab"), actors, "Fat", fatPos, YawTo(fatPos, soldierPos), 2.4f, LoadController("AC_Enemy_Brute"));
        Vector3 prisonerPos = new Vector3(7f, 0f, 10f);
        PlaceActor(Load(Horrors + "Character_Prisoner/Prefab/Prisoner.prefab"), actors, "Prisoner", prisonerPos, YawTo(prisonerPos, soldierPos), 1.8f, LoadController("AC_Enemy_Bomber"));

        // 조명·카메라 지점
        Transform rig = Group(t, "Rig");
        Transform lamp1 = Point(rig, "LampPoint_Soldier", new Vector3(1.2f, 0f, 0.4f));
        Transform lamp2 = Point(rig, "LampPoint_Back", new Vector3(-4.5f, 0f, 6.5f));
        Transform cameraPoint = Point(rig, "CameraPoint", new Vector3(-2.2f, 2.4f, -6.5f));
        Transform lookTarget = Point(rig, "LookTarget", new Vector3(2.2f, 1.3f, 4.5f));
        cameraPoint.rotation = Quaternion.LookRotation(lookTarget.position - cameraPoint.position);

        Light flashlight = CreateLight(rig, "Flashlight", LightType.Spot, new Vector3(1.35f, 1.45f, 0.35f), new Color(1f, 0.94f, 0.82f), 16f, 6f);
        flashlight.transform.rotation = Quaternion.LookRotation(t.TransformPoint(crazyPos + Vector3.up * 1.1f) - flashlight.transform.position);
        flashlight.spotAngle = 32f;
        flashlight.innerSpotAngle = 16f;
        flashlight.shadows = LightShadows.Soft;
        Light alarm = CreateLight(rig, "AlarmLight", LightType.Point, new Vector3(6f, 3f, 11.2f), new Color(1f, 0.12f, 0.08f), 9f, 3f);

        MainMenuDiorama view = Undo.AddComponent<MainMenuDiorama>(root);
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("cameraPoint").objectReferenceValue = cameraPoint;
        so.FindProperty("lookTarget").objectReferenceValue = lookTarget;
        SerializedProperty lamps = so.FindProperty("lampPoints");
        lamps.arraySize = 2;
        lamps.GetArrayElementAtIndex(0).objectReferenceValue = lamp1;
        lamps.GetArrayElementAtIndex(1).objectReferenceValue = lamp2;
        so.FindProperty("flashlight").objectReferenceValue = flashlight;
        so.FindProperty("alarmLight").objectReferenceValue = alarm;
        so.ApplyModifiedPropertiesWithoutUndo();

        SetActive(scene, ArenaName, false);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;
        Debug.Log("[MainMenuDiorama] MenuDiorama 생성 (ArenaBackdrop은 꺼 둠, 삭제하지 않음) — 씬 저장 후 실행해 확인. 카메라는 Rig/CameraPoint·LookTarget, 인물·소품은 직접 옮겨도 됨");
    }

    [MenuItem("Tools/TD Shoot/Main Menu Backdrop - Use Arena")]
    public static void UseArena() => Switch(true);

    [MenuItem("Tools/TD Shoot/Main Menu Backdrop - Use Diorama")]
    public static void UseDiorama() => Switch(false);

    private static void Switch(bool arena)
    {
        MainMenuUI menuUI = Object.FindAnyObjectByType<MainMenuUI>(FindObjectsInactive.Include);
        if (menuUI == null)
        {
            EditorUtility.DisplayDialog("Main Menu", "열린 씬에서 MainMenuUI를 찾지 못했습니다. MainMenu 씬을 열어 주세요.", "확인");
            return;
        }
        Scene scene = menuUI.gameObject.scene;
        bool foundArena = SetActive(scene, ArenaName, arena);
        bool foundDiorama = SetActive(scene, DioramaName, !arena);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[MainMenu] 배경 = {(arena ? "탑다운 패닝" : "디오라마")} (ArenaBackdrop {(foundArena ? "있음" : "없음")}, MenuDiorama {(foundDiorama ? "있음" : "없음")}) — 씬 저장은 직접");
    }

    #region Helpers

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root;
        }
        return null;
    }

    private static bool SetActive(Scene scene, string name, bool active)
    {
        GameObject go = FindRoot(scene, name);
        if (go == null) return false;
        Undo.RecordObject(go, "Switch Main Menu Backdrop");
        go.SetActive(active);
        return true;
    }

    private static GameObject Load(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) Debug.LogWarning($"[MainMenuDiorama] 프리팹 없음: {path}");
        return prefab;
    }

    private static RuntimeAnimatorController LoadController(string name)
    {
        RuntimeAnimatorController c = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(EnemyControllers + name + ".controller");
        if (c == null) Debug.LogWarning($"[MainMenuDiorama] 컨트롤러 없음: {name} (Generate Enemy Type Prefabs 먼저)");
        return c;
    }

    // 군인 조준 대기 동작 하나만 도는 컨트롤러
    private static RuntimeAnimatorController GetSoldierController()
    {
        AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(SoldierControllerPath);
        if (existing != null) return existing;
        AnimationClip idle = EnemyPrefabGenerator.FindClip(Horrors + "Character_Soldier/animation", "(aiming)Idle", true);
        if (idle == null) return null;
        string folder = System.IO.Path.GetDirectoryName(SoldierControllerPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Project/Animations", "Menu");
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(SoldierControllerPath);
        controller.layers[0].stateMachine.AddState("Idle").motion = idle;
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static Transform Group(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static Transform Point(Transform parent, string name, Vector3 local)
    {
        Transform p = Group(parent, name);
        p.position = RootPosition + local;
        return p;
    }

    private static Light CreateLight(Transform parent, string name, LightType type, Vector3 local, Color color, float range, float intensity)
    {
        Light light = Point(parent, name, local).gameObject.AddComponent<Light>();
        light.type = type;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        return light;
    }

    private static float YawTo(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    private static GameObject Spawn(GameObject prefab, Transform parent, float yaw)
    {
        if (prefab == null) return null;
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        return go;
    }

    // 바운드 아래 가운데를 지점에 (소품·인물)
    private static GameObject Place(GameObject prefab, Transform parent, Vector3 local, float yaw)
    {
        GameObject go = Spawn(prefab, parent, yaw);
        if (go == null) return null;
        AlignTo(go, RootPosition + local, false);
        return go;
    }

    // 바운드 윗면 가운데를 지점에 (바닥판)
    private static GameObject PlaceTop(GameObject prefab, Transform parent, Vector3 local, float yaw)
    {
        GameObject go = Spawn(prefab, parent, yaw);
        if (go == null) return null;
        AlignTo(go, RootPosition + local, true);
        return go;
    }

    // 벽: 긴 변이 원하는 축(alongZ면 Z, 아니면 X)을 따르게 회전 후 아래 가운데 정렬
    private static void PlaceWall(GameObject prefab, Transform parent, Vector3 local, bool alongZ)
    {
        GameObject go = Spawn(prefab, parent, 0f);
        if (go == null) return;
        Bounds b = GetBounds(go);
        bool longOnX = b.size.x >= b.size.z;
        if (longOnX == alongZ) go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        AlignTo(go, RootPosition + local, false);
    }

    private static void PlaceActor(GameObject prefab, Transform parent, string name, Vector3 local, float yaw, float height, RuntimeAnimatorController controller)
    {
        GameObject go = Spawn(prefab, parent, yaw);
        if (go == null) return;
        go.name = name;
        Bounds b = GetBounds(go);
        if (b.size.y > 0.01f) go.transform.localScale *= height / b.size.y;
        AlignTo(go, RootPosition + local, false);

        Animator animator = go.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            if (controller != null) animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        else
        {
            Debug.LogWarning($"[MainMenuDiorama] {name}: Animator 없음 (정지 자세로 표시)");
        }
    }

    private static void AlignTo(GameObject go, Vector3 point, bool top)
    {
        Bounds b = GetBounds(go);
        Vector3 anchor = new Vector3(b.center.x, top ? b.max.y : b.min.y, b.center.z);
        go.transform.position += point - anchor;
    }

    private static Bounds GetBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    #endregion
}
