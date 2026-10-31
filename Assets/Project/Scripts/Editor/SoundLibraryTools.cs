using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 사운드 목록 에셋 생성
//   Create Sound Library: Assets/Project/Resources/SoundLibrary.asset 생성 (이미 있으면 선택만)
//   Fill Sound Library (Vefects) (V3a): Vefects 동봉 효과음(단발 23개, 반복용 루프 제외)을 비어 있는 칸에만 연결 + 칸별 음량·간격
//     채워진 칸(직접 고른 소리)은 건드리지 않음 — 들어 보고 Inspector에서 교체·음량 조절
//   Apply BGM Loops: Assets/BGM/Loops 배경음 6곡을 음악 칸에 연결 (스트리밍·Vorbis 가져오기 설정)
public static class SoundLibraryTools
{
    private const string Folder = "Assets/Project/Resources";
    private const string AssetPath = Folder + "/" + SoundLibrary.ResourcePath + ".asset";
    private const string Combat = "Assets/Vefects/Flipbook VFX Bundle Lite URP/Combat Flipbook VFX URP/Audio/WAV/Sergi/";
    private const string Flipbook = "Assets/Vefects/Flipbook VFX Bundle Lite URP/Flipbook VFX URP/Audio/WAV/";
    private const string Pixel = "Assets/Vefects/Flipbook VFX Bundle Lite URP/Pixel Craft VFX URP/Audio/WAV/Sergi/";

    [MenuItem("Tools/TD Shoot/Create Sound Library")]
    public static void CreateLibrary()
    {
        SoundLibrary lib = LoadOrCreate();
        Selection.activeObject = lib;
        EditorGUIUtility.PingObject(lib);
    }

    [MenuItem("Tools/TD Shoot/Fill Sound Library (Vefects)")]
    public static void FillVefects()
    {
        SoundLibrary lib = LoadOrCreate();
        int filled = 0;

        // 플레이어
        filled += Fill(lib.dash, 0.6f, 0.1f, Combat + "SFX_Vefects_Directional_Dust_03_Fast_One_Shot_01.wav");
        filled += Fill(lib.grenadeExplosion, 0.9f, 0.08f, Flipbook + "Vefects_SFX_Impact_01.wav", Flipbook + "Vefects_SFX_Shrapnel_02.wav");
        filled += Fill(lib.grenadeReady, 0.6f, 0.2f, Combat + "SFX_Vefects_Radial_Charge_Skill_Ready_One_Shot_01.wav");
        filled += Fill(lib.lifesteal, 0.45f, 0.15f, Flipbook + "Vefects_SFX_Liquid_Puff_01.wav");

        // 적
        filled += Fill(lib.enemyHit, 0.4f, 0.06f, Combat + "SFX_Vefects_Hit_03_One_Shot.wav", Combat + "SFX_Vefects_Hit_04_One_Shot.wav");
        filled += Fill(lib.enemyCritHit, 0.6f, 0.08f, Pixel + "SFX_Vefects_Impact_Sparks_02.wav");
        filled += Fill(lib.enemyDeath, 0.6f, 0.06f, Pixel + "SFX_Vefects_Vanish_02.wav");
        filled += Fill(lib.eliteDeath, 0.9f, 0.2f, Flipbook + "Vefects_SFX_Lightning_01.wav");
        filled += Fill(lib.enemyAttack, 0.6f, 0.1f, Combat + "SFX_Vefects_Radial_Spilky_Hit_One_Shot_02.wav");
        filled += Fill(lib.eliteIncoming, 0.7f, 0.3f, Combat + "SFX_Vefects_Electric_Impact_02_One_Shot_01.wav");
        filled += Fill(lib.execute, 0.7f, 0.08f, Pixel + "SFX_Vefects_Sword_Slash_02.wav");

        // 적 행동
        filled += Fill(lib.shooterFire, 0.55f, 0.08f, Pixel + "SFX_Vefects_Projectile_Burst_03.wav");
        filled += Fill(lib.chargerWindup, 0.6f, 0.2f, Flipbook + "Vefects_SFX_Charge_01.wav");
        filled += Fill(lib.chargerImpact, 0.8f, 0.15f, Pixel + "SFX_Vefects_Projectile_Burst_Impact_03.wav");
        filled += Fill(lib.bomberFuse, 0.6f, 0.15f, Flipbook + "Vefects_SFX_Fire_Charge_01.wav");
        filled += Fill(lib.bomberExplosion, 0.9f, 0.08f, Flipbook + "Vefects_SFX_Fire_Burst_01.wav");

        // 진행
        filled += Fill(lib.crateOpened, 0.8f, 0.2f, Pixel + "SFX_Vefects_Chest_Open_02.wav");
        filled += Fill(lib.extractionSuccess, 0.8f, 0.5f, Pixel + "SFX_Vefects_Respawn_02.wav");

        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        Selection.activeObject = lib;
        EditorGUIUtility.PingObject(lib);
        Debug.Log($"[SoundLibraryTools] 빈 칸 {filled}개에 Vefects 효과음 연결 (이미 채워진 칸은 그대로) -> {AssetPath}");
    }

    // 수류탄 충전 알림 (사용자 2026-10-31: 차오르는 소리가 어색 → 알림 느낌). 합성음 2종 중 선택, 이 칸만 덮어씀
    //   Chime = 두 음 상승 '띠링' 0.55초 / Beep = 짧은 '삑삑' 0.3초 (Assets/Project/Audio/SFX)
    private const string SfxFolder = "Assets/Project/Audio/SFX/";

    [MenuItem("Tools/TD Shoot/Grenade Ready Sound - Chime")]
    public static void GrenadeReadyChime() => SetGrenadeReady("SFX_GrenadeReady_Chime.wav");

    [MenuItem("Tools/TD Shoot/Grenade Ready Sound - Beep")]
    public static void GrenadeReadyBeep() => SetGrenadeReady("SFX_GrenadeReady_Beep.wav");

    private static void SetGrenadeReady(string fileName)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxFolder + fileName);
        if (clip == null)
        {
            Debug.LogWarning($"[SoundLibraryTools] 효과음 없음: {SfxFolder}{fileName}");
            return;
        }
        SoundLibrary lib = LoadOrCreate();
        lib.grenadeReady.clips = new[] { clip };
        lib.grenadeReady.volume = 0.5f;
        lib.grenadeReady.pitchRandom = 0f;
        lib.grenadeReady.minInterval = 0.2f;
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SoundLibraryTools] 수류탄 충전 알림 = {fileName}");
    }

    // 배경음 연결: Assets/BGM/Loops (원본에서 루프 길이로 자르고 음량을 -16 LUFS로 맞춘 wav)
    //   가져오기 설정 = 스트리밍·Vorbis, 음악 칸은 덮어씀 (정비 칸은 비움 → 전투 곡을 먹먹하게)
    private const string BgmFolder = "Assets/BGM/Loops/";
    private const float BgmVolume = 0.6f;

    [MenuItem("Tools/TD Shoot/Apply BGM Loops")]
    public static void ApplyBgm()
    {
        SoundLibrary lib = LoadOrCreate();
        int linked = 0;
        linked += SetMusic(lib.mainMenu, "BGM_MainMenu.wav");
        linked += SetMusic(lib.combat, "BGM_Combat.wav");
        linked += SetMusic(lib.finalRound, "BGM_FinalFloor.wav");
        linked += SetMusic(lib.resultClear, "BGM_ResultClear.wav");
        linked += SetMusic(lib.resultFail, "BGM_ResultFail.wav");
        linked += SetMusic(lib.tutorial, "BGM_Tutorial.wav");
        lib.maintenance.clip = null;

        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        Selection.activeObject = lib;
        EditorGUIUtility.PingObject(lib);
        Debug.Log($"[SoundLibraryTools] 배경음 {linked}/6칸 연결, 정비 = 전투 곡 먹먹하게 -> {AssetPath}");
    }

    private static int SetMusic(MusicEntry entry, string fileName)
    {
        string path = BgmFolder + fileName;
        AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[SoundLibraryTools] 배경음 없음: {path}");
            return 0;
        }
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        if (settings.loadType != AudioClipLoadType.Streaming || settings.compressionFormat != AudioCompressionFormat.Vorbis)
        {
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
        entry.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        entry.volume = BgmVolume;
        return entry.clip != null ? 1 : 0;
    }

    private static SoundLibrary LoadOrCreate()
    {
        SoundLibrary lib = AssetDatabase.LoadAssetAtPath<SoundLibrary>(AssetPath);
        if (lib != null) return lib;
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Project", "Resources");
        lib = ScriptableObject.CreateInstance<SoundLibrary>();
        AssetDatabase.CreateAsset(lib, AssetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SoundLibraryTools] 사운드 목록 생성 -> {AssetPath}");
        return lib;
    }

    // 칸이 비어 있을 때만 채움 (음량·최소 간격도 함께), 채웠으면 1
    private static int Fill(SoundEntry entry, float volume, float minInterval, params string[] paths)
    {
        if (!entry.IsEmpty) return 0;
        List<AudioClip> clips = new List<AudioClip>();
        foreach (string path in paths)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null) clips.Add(clip);
            else Debug.LogWarning($"[SoundLibraryTools] 효과음 없음: {path}");
        }
        if (clips.Count == 0) return 0;
        entry.clips = clips.ToArray();
        entry.volume = volume;
        entry.minInterval = minInterval;
        return 1;
    }
}
