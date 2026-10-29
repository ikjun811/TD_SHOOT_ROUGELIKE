using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// SciFi_TopDown_SpaceStation 에셋 연결 (가독성 작업, 사용자 선택 2026-10-29)
//   Apply SpaceStation Floor (Square/Octagon/Hex): 열린 씬 BspArenaGenerator의 바닥 텍스처 칸에 알베도·노멀·텍스처 한 장 크기 연결 (씬 저장은 직접)
//     사각 = TilesDark2(5x5 회색 사각, 사용자 선택 2026-10-29) / 팔각 = TilesDark1(4x4 짙은 팔각+마름모) / 육각 = hexDark(이전 기본)
//   텍스처만 가져다 우리 URP Lit 재질에 씀 → 에셋의 2019년 LWRP 셰이더 그래프 변환 여부와 무관
//   Apply SpaceStation Obstacles (사용자 결정): 직선·ㄱ자 벽 = 내부 벽 2종 / 기둥 = 기둥·상자·통 / 대형 = 상자 더미(컨테이너 등 제외) /
//     소품 최대 높이 2.5m (벽·기둥 4m를 세로로 줄임) / 외곽 벽 = 내부 벽(변형 _B 20%)·모서리 기둥, 1.5m 묻어 2.5m만 보이게
//     LWRP 프리팹 사용 (분홍 아님 확인), 바닥 판이 붙은 방·복도 조각은 우리 바닥과 겹쳐 제외
//     배치 밀도 (사용자: 공간에 비해 장애물이 작아 빈 공간이 많음): 구역당 2~4개·벽 6~10m·기둥 3x3·간격 2m·더미 3~4x2~3·대형 35%
//     (구역 중앙 빈 공간·구역 사이 통로는 그대로)
public static class SpaceStationKitTools
{
    private const string FloorFolder = "Assets/SciFi_TopDown_SpaceStation/TDSpaceStation_Resources/TDSpaceStation_Textures/Floors";

    private const string PrefabFolder = "Assets/SciFi_TopDown_SpaceStation/TDSpaceStation_Prefabs_LWRP";
    private const string PropFolder = PrefabFolder + "/TDSpaceStation_Props";
    private const float PropMaxHeight = 2.5f;
    private const float BoundarySink = 1.5f; // 벽 4m - 1.5m = 보이는 높이 2.5m

    [MenuItem("Tools/TD Shoot/Apply SpaceStation Obstacles")]
    public static void ApplyObstacles()
    {
        BspArenaGenerator generator = Object.FindAnyObjectByType<BspArenaGenerator>(FindObjectsInactive.Include);
        if (generator == null)
        {
            EditorUtility.DisplayDialog("SpaceStation Obstacles", "열린 씬에 BspArenaGenerator가 없습니다 (GameplayScene을 여세요).", "확인");
            return;
        }

        GameObject wall = Load($"{PrefabFolder}/SpaceStation_Room_A_Wall_Internal_LWRP.prefab");
        GameObject wallB = Load($"{PrefabFolder}/SpaceStation_Room_A_Wall_Internal_B_LWRP.prefab");
        GameObject column = Load($"{PrefabFolder}/SpaceStation_Room_A_Column_LWRP.prefab");
        GameObject crate = Load($"{PropFolder}/SpaceStation_Crate_A_LWRP.prefab");
        GameObject barrel = Load($"{PropFolder}/SpaceStation_Barrel_A_LWRP.prefab");
        if (wall == null || column == null || crate == null)
        {
            EditorUtility.DisplayDialog("SpaceStation Obstacles", "우주정거장 LWRP 프리팹을 찾지 못했습니다 (콘솔 확인).", "확인");
            return;
        }

        SerializedObject so = new SerializedObject(generator);
        SetArray(so, "wallPropPrefabs", wall, wallB);
        SetArray(so, "pillarPropPrefabs", column, crate, barrel);
        SetArray(so, "largePropPrefabs");
        SetArray(so, "largePilePrefabs", crate);
        so.FindProperty("propMaxHeight").floatValue = PropMaxHeight;
        SetArray(so, "boundaryWallPrefabs", wall);
        SetArray(so, "boundaryAccentPrefabs", wallB);
        so.FindProperty("boundaryCornerPrefab").objectReferenceValue = column;
        so.FindProperty("kitWallSink").floatValue = BoundarySink;

        // 배치 밀도·크기
        so.FindProperty("obstaclesPerLeaf").vector2IntValue = new Vector2Int(2, 4);
        so.FindProperty("wallLengthRange").vector2IntValue = new Vector2Int(6, 10);
        so.FindProperty("pillarSize").intValue = 3;
        so.FindProperty("obstacleSpacing").intValue = 2;
        so.FindProperty("pileCountX").vector2IntValue = new Vector2Int(3, 4);
        so.FindProperty("pileCountZ").vector2IntValue = new Vector2Int(2, 3);
        so.FindProperty("largeObstacleChance").floatValue = 0.35f;
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
        Debug.Log("[SpaceStationKit] 장애물·외곽 벽 = 우주정거장 소품 (벽·기둥 2.5m, 대형 = 상자 더미), 밀도 구역당 2~4·벽 6~10m·기둥 3x3·간격 2 — 씬을 저장하세요. 미리 보기: 생성기 우클릭 Generate", generator);
    }

    private static GameObject Load(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) Debug.LogWarning($"[SpaceStationKit] 프리팹 없음: {path}");
        return prefab;
    }

    private static void SetArray(SerializedObject so, string property, params GameObject[] values)
    {
        SerializedProperty list = so.FindProperty(property);
        int count = 0;
        foreach (GameObject v in values) if (v != null) count++;
        list.arraySize = count;
        int i = 0;
        foreach (GameObject v in values)
        {
            if (v == null) continue;
            list.GetArrayElementAtIndex(i++).objectReferenceValue = v;
        }
    }

    // 텍스처 한 장 = worldSize m (사각 7.5m → 칸 1.5m, 팔각 6m → 칸 1.5m, 육각 5m = 에셋 원래 월드 UV)
    [MenuItem("Tools/TD Shoot/Apply SpaceStation Floor (Square)")]
    public static void ApplyFloorSquare() => ApplyFloor("TilesDark2", 7.5f, "사각 타일");

    [MenuItem("Tools/TD Shoot/Apply SpaceStation Floor (Octagon)")]
    public static void ApplyFloorOctagon() => ApplyFloor("TilesDark1", 6f, "짙은 팔각 타일");

    [MenuItem("Tools/TD Shoot/Apply SpaceStation Floor (Hex)")]
    public static void ApplyFloorHex() => ApplyFloor("hexDark", 5f, "짙은 육각");

    private static void ApplyFloor(string textureName, float worldSize, string label)
    {
        BspArenaGenerator generator = Object.FindAnyObjectByType<BspArenaGenerator>(FindObjectsInactive.Include);
        if (generator == null)
        {
            EditorUtility.DisplayDialog("SpaceStation Floor", "열린 씬에 BspArenaGenerator가 없습니다 (GameplayScene을 여세요).", "확인");
            return;
        }

        string albedoPath = $"{FloorFolder}/HexagonalFloor_{textureName}_basecolor.tga";
        string normalPath = $"{FloorFolder}/HexagonalFloor_{textureName}_normal.tga";
        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        if (albedo == null)
        {
            EditorUtility.DisplayDialog("SpaceStation Floor", $"{albedoPath}을 찾지 못했습니다.", "확인");
            return;
        }

        SerializedObject so = new SerializedObject(generator);
        so.FindProperty("useGridFloor").boolValue = true;
        so.FindProperty("floorTexture").objectReferenceValue = albedo;
        so.FindProperty("floorNormalMap").objectReferenceValue = normal;
        so.FindProperty("floorTextureWorldSize").floatValue = worldSize;
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
        Debug.Log($"[SpaceStationKit] 바닥 = {label} (한 장 {worldSize}m, 노멀 {(normal != null ? "있음" : "없음")}) — 씬을 저장하세요.", generator);
    }

    // 탈출 구역 꾸밈 (탈출 구역 연출 2단계): 열린 씬 ExtractionZone에 ExtractionZoneDecor 부착 + 프리팹 연결 (씬 저장은 직접)
    [MenuItem("Tools/TD Shoot/Apply SpaceStation Extraction Decor")]
    public static void ApplyExtractionDecor()
    {
        ExtractionZone zone = Object.FindAnyObjectByType<ExtractionZone>(FindObjectsInactive.Include);
        if (zone == null)
        {
            EditorUtility.DisplayDialog("Extraction Decor", "열린 씬에 ExtractionZone이 없습니다 (GameplayScene을 여세요).", "확인");
            return;
        }

        ExtractionZoneDecor decor = zone.GetComponent<ExtractionZoneDecor>();
        if (decor == null) decor = Undo.AddComponent<ExtractionZoneDecor>(zone.gameObject);

        SerializedObject so = new SerializedObject(decor);
        so.FindProperty("padPrefab").objectReferenceValue = Load(PrefabFolder + "/SpaceStation_Room_A_Floor_LWRP.prefab");
        so.FindProperty("beaconPrefab").objectReferenceValue = Load(PropFolder + "/SpaceStation_Light_A_LWRP.prefab");
        so.FindProperty("consolePrefab").objectReferenceValue = Load(PropFolder + "/SpaceStation_Panel_A_LWRP.prefab");
        so.FindProperty("ventPrefab").objectReferenceValue = Load(PropFolder + "/SpaceStation_FloorVent_A_LWRP.prefab");
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(zone.gameObject.scene);
        Selection.activeObject = zone.gameObject;
        Debug.Log("[SpaceStationKit] 탈출 구역 꾸밈 연결 (착륙장 바닥·표지등·콘솔·환풍구) — 씬을 저장하세요.", zone);
    }
}
