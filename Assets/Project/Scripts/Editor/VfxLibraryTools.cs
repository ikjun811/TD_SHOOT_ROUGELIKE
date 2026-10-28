using UnityEditor;
using UnityEngine;

// VFX 라이브러리 생성·Vefects 연결 (사용자 채택 2026-10-29: 전투 핵심 + 적 행동 표현)
//   Tools > TD Shoot > Create VFX Library (Vefects): Assets/Project/Resources/VfxLibrary.asset 생성(있으면 그대로),
//   비어 있는 칸에만 Vefects Flipbook VFX Bundle Lite URP 프리팹 연결 (조정한 크기·높이 값은 유지)
//   크기는 에셋 원래 크기 기준 — 플레이해 보고 Inspector에서 칸별 scale·yOffset 조정
public static class VfxLibraryTools
{
    private const string Folder = "Assets/Project/Resources";
    private const string AssetPath = Folder + "/" + VfxLibrary.ResourcePath + ".asset";
    private const string Combat = "Assets/Vefects/Flipbook VFX Bundle Lite URP/Combat Flipbook VFX URP/_ Lite/Particles/";
    private const string Flipbook = "Assets/Vefects/Flipbook VFX Bundle Lite URP/Flipbook VFX URP/_ Lite/Prefabs/";

    [MenuItem("Tools/TD Shoot/Create VFX Library (Vefects)")]
    public static void Create()
    {
        VfxLibrary library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(AssetPath);
        if (library == null)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Project", "Resources");
            library = ScriptableObject.CreateInstance<VfxLibrary>();
            AssetDatabase.CreateAsset(library, AssetPath);
        }

        // 1차 기본값 교체 (사용자 피드백): 폭발이 약함 → 폭발 + 충격 + 파편 겹치기, 피격 불꽃은 어울리지 않음 → 비움
        int filled = 0;
        if (IsExactly(library.explosion, Combat + "Lite_VFX_Explosion_02_Pivot_Loop.prefab")) library.explosion.prefabs = null;
        if (IsExactly(library.enemyHit, Combat + "Lite_VFX_Hit_03_Loop.prefab", Combat + "Lite_VFX_Hit_04_Loop.prefab")) library.enemyHit.prefabs = null;

        if (Fill(library.explosion, Combat + "Lite_VFX_Explosion_02_Pivot_Loop.prefab", Flipbook + "Lite_VFX_Impact_01.prefab", Flipbook + "Lite_VFX_Shrapnel_03.prefab") > 0)
        {
            library.explosion.playAll = true;
            library.explosion.prefabScales = new[] { ExplosionCoreScale, ExplosionImpactScale, ExplosionShrapnelScale };
            filled++;
        }
        filled += Fill(library.bomberExplosion, Flipbook + "Lite_VFX_Flame_Burst_01.prefab");
        filled += Fill(library.enemyDeath, Combat + "Lite_VFX_Dust_03_Fast_Pivot_Loop.prefab");
        filled += Fill(library.shooterMuzzle, Flipbook + "Lite_VFX_Muzzle_Smoke_01.prefab");
        filled += Fill(library.chargerDust, Flipbook + "Lite_VFX_Dust_Directional_Small_01.prefab");
        filled += Fill(library.chargerImpact, Combat + "Lite_VFX_Radial_Spiky_Hit_01_Random_Rotate_Loop.prefab");
        filled += Fill(library.eliteSpawn, Flipbook + "Lite_VFX_Charge_01.prefab");

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Selection.activeObject = library;
        EditorGUIUtility.PingObject(library);
        Debug.Log($"[VfxLibraryTools] VFX 라이브러리 {AssetPath} — 빈 칸 {filled}개에 Vefects 연결 (이미 채워진 칸은 그대로)");
    }

    // 폭발 겹치기 크기 (에셋 원래 크기: 폭발 2·충격 5·파편 4 → 반경 4m 수류탄에 맞춤)
    private const float ExplosionCoreScale = 2.5f;
    private const float ExplosionImpactScale = 0.8f;
    private const float ExplosionShrapnelScale = 0.6f;

    // 칸 내용이 정확히 이 프리팹들인지 (이전 기본값인지 — 사용자가 바꾼 칸은 건드리지 않음)
    private static bool IsExactly(VfxEntry entry, params string[] paths)
    {
        if (entry.prefabs == null || entry.prefabs.Length != paths.Length) return false;
        for (int i = 0; i < paths.Length; i++)
        {
            if (entry.prefabs[i] == null || AssetDatabase.GetAssetPath(entry.prefabs[i]) != paths[i]) return false;
        }
        return true;
    }

    // 칸이 비어 있을 때만 채움, 채웠으면 1
    private static int Fill(VfxEntry entry, params string[] paths)
    {
        if (!entry.IsEmpty) return 0;
        System.Collections.Generic.List<GameObject> prefabs = new System.Collections.Generic.List<GameObject>();
        foreach (string path in paths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) prefabs.Add(prefab);
            else Debug.LogWarning($"[VfxLibraryTools] 프리팹 없음: {path}");
        }
        if (prefabs.Count == 0) return 0;
        entry.prefabs = prefabs.ToArray();
        return 1;
    }
}
