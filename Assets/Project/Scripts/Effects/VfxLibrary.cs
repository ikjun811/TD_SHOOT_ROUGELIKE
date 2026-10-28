using UnityEngine;

// 이펙트 한 칸: 프리팹 여러 개 중 무작위 1개, 또는 playAll이면 전부 겹쳐 재생 (비어 있으면 호출 쪽의 기존 연출)
[System.Serializable]
public class VfxEntry
{
    public GameObject[] prefabs;
    [Tooltip("켜면 무작위 1개 대신 전부 겹쳐 재생 (폭발 = 폭발 + 충격 + 파편)")]
    public bool playAll;
    [Tooltip("프리팹별 크기 배율 (prefabs와 같은 순서, 없으면 1) — 에셋마다 원래 크기가 달라 겹칠 때 맞춤")]
    public float[] prefabScales;
    [Tooltip("크기 배율 (호출 쪽 배율과 곱함)")]
    public float scale = 1f;
    [Tooltip("생성 위치 높이 보정 (m)")]
    public float yOffset;
    [Tooltip("재생 속도 배율")]
    public float speed = 1f;

    public bool IsEmpty
    {
        get
        {
            if (prefabs == null) return true;
            foreach (GameObject prefab in prefabs)
            {
                if (prefab != null) return false;
            }
            return true;
        }
    }

    public float GetPrefabScale(int index)
    {
        return prefabScales != null && index >= 0 && index < prefabScales.Length && prefabScales[index] > 0f ? prefabScales[index] : 1f;
    }

    public GameObject Pick()
    {
        if (IsEmpty) return null;
        GameObject picked = null;
        for (int i = 0; i < 8 && picked == null; i++) picked = prefabs[Random.Range(0, prefabs.Length)];
        if (picked != null) return picked;
        foreach (GameObject prefab in prefabs)
        {
            if (prefab != null) return prefab;
        }
        return null;
    }
}

// 파티클 이펙트 칸 모음 (SoundLibrary와 같은 방식) — Resources/VfxLibrary 자동 로드, 없으면 전부 빈 칸(기존 연출)
// 만들기: Tools > TD Shoot > Create VFX Library (Vefects) — 에셋 생성 + Vefects 프리팹 연결
// 재생: VfxSpawner.Play(VfxLibrary.Current.칸, 위치[, 배율])
[CreateAssetMenu(fileName = "VfxLibrary", menuName = "GameData/VFX Library")]
public class VfxLibrary : ScriptableObject
{
    public const string ResourcePath = "VfxLibrary";

    [Header("전투 핵심")]
    [Tooltip("폭발 공용 (수류탄·착탄 폭발·재장전 폭발·대시 폭탄·위기 반격). 호출 배율 0.5(수류탄) = 칸 배율 그대로")]
    public VfxEntry explosion = new VfxEntry();
    [Tooltip("봄버 자폭·처치 폭발 (비우면 폭발 공용)")]
    public VfxEntry bomberExplosion = new VfxEntry();
    [Tooltip("적 피격 (맞은 위치) — 불꽃 타격이 어울리지 않아 비워 둠 (사용자 결정, 번쩍임·피해 숫자로 충분)")]
    public VfxEntry enemyHit = new VfxEntry();
    [Tooltip("적 사망 — 디졸브 시작 때 (비우면 코드 검은 연기)")]
    public VfxEntry enemyDeath = new VfxEntry();

    [Header("적 행동 표현")]
    public VfxEntry shooterMuzzle = new VfxEntry();
    public VfxEntry chargerDust = new VfxEntry();
    public VfxEntry chargerImpact = new VfxEntry();
    public VfxEntry eliteSpawn = new VfxEntry();

    private static VfxLibrary current;

    public static VfxLibrary Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<VfxLibrary>(ResourcePath);
                if (current == null)
                {
                    current = CreateInstance<VfxLibrary>();
                    current.hideFlags = HideFlags.DontSave;
                }
            }
            return current;
        }
    }
}
