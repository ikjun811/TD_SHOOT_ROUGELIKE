using UnityEngine;

// 소리 한 칸: 여러 파일 중 무작위 1개 재생 (비어 있으면 무음)
[System.Serializable]
public class SoundEntry
{
    public AudioClip[] clips;
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("음높이 무작위 폭 (0.05 = 0.95~1.05배)")]
    [Range(0f, 0.5f)] public float pitchRandom = 0.05f;
    [Tooltip("같은 소리 최소 재생 간격(초) — 산탄 12발 적중처럼 한꺼번에 몰리면 1번만")]
    public float minInterval = 0.05f;

    public bool IsEmpty
    {
        get
        {
            if (clips == null) return true;
            foreach (AudioClip clip in clips)
            {
                if (clip != null) return false;
            }
            return true;
        }
    }

    public AudioClip PickClip()
    {
        if (IsEmpty) return null;
        AudioClip clip = null;
        // null 칸이 섞여 있어도 유효한 파일이 나올 때까지 (최대 8회 후 첫 유효 파일)
        for (int i = 0; i < 8 && clip == null; i++) clip = clips[Random.Range(0, clips.Length)];
        if (clip != null) return clip;
        foreach (AudioClip c in clips)
        {
            if (c != null) return c;
        }
        return null;
    }
}

// 배경음 한 칸 (파일 1개, 반복)
[System.Serializable]
public class MusicEntry
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 0.7f;
}

// 게임 전체 소리 목록 — Resources/SoundLibrary 에셋을 AudioManager가 자동으로 불러옴 (없으면 전부 무음)
// 에셋 만들기: Tools > TD Shoot > Create Sound Library (Assets/Project/Resources/SoundLibrary.asset)
[CreateAssetMenu(fileName = "SoundLibrary", menuName = "GameData/Sound Library")]
public class SoundLibrary : ScriptableObject
{
    public const string ResourcePath = "SoundLibrary";

    [Header("3D 효과음 (위치 있는 소리)")]
    [Tooltip("0 = 2D, 1 = 완전 3D. 카메라가 높이 떠 있어 거리 감쇄보다 좌우 위치감 위주로")]
    [Range(0f, 1f)] public float spatialBlend3D = 0.6f;
    public float minDistance3D = 15f;
    public float maxDistance3D = 60f;

    [Header("배경음")]
    public float musicFadeTime = 1.5f;
    public MusicEntry mainMenu = new MusicEntry();
    public MusicEntry combat = new MusicEntry();
    public MusicEntry finalRound = new MusicEntry();
    public MusicEntry maintenance = new MusicEntry();
    public MusicEntry resultClear = new MusicEntry();
    public MusicEntry resultFail = new MusicEntry();

    [Header("플레이어")]
    public SoundEntry playerHurt = new SoundEntry();
    public SoundEntry playerDeath = new SoundEntry();
    public SoundEntry playerLowHealth = new SoundEntry();
    public SoundEntry dash = new SoundEntry();
    public SoundEntry grenadeThrow = new SoundEntry();
    [Tooltip("수류탄·착탄 폭발·재장전 폭발·대시 폭탄 등 폭발 공용")]
    public SoundEntry grenadeExplosion = new SoundEntry();
    public SoundEntry grenadeReady = new SoundEntry();
    public SoundEntry lifesteal = new SoundEntry();

    [Header("적")]
    public SoundEntry enemyHit = new SoundEntry();
    public SoundEntry enemyCritHit = new SoundEntry();
    public SoundEntry enemyDeath = new SoundEntry();
    public SoundEntry eliteDeath = new SoundEntry();
    public SoundEntry enemyAttack = new SoundEntry();
    public SoundEntry eliteIncoming = new SoundEntry();
    public SoundEntry execute = new SoundEntry();

    [Header("적 행동")]
    public SoundEntry shooterFire = new SoundEntry();
    public SoundEntry chargerWindup = new SoundEntry();
    public SoundEntry chargerImpact = new SoundEntry();
    public SoundEntry bomberFuse = new SoundEntry();
    [Tooltip("봄버 자폭·처치 폭발 (비우면 폭발 공용 grenadeExplosion)")]
    public SoundEntry bomberExplosion = new SoundEntry();

    [Header("진행")]
    public SoundEntry roundStart = new SoundEntry();
    public SoundEntry crateOpening = new SoundEntry();
    public SoundEntry crateOpened = new SoundEntry();
    public SoundEntry itemDrop = new SoundEntry();
    public SoundEntry itemDropLegendary = new SoundEntry();
    public SoundEntry itemPickup = new SoundEntry();
    public SoundEntry extractionEnter = new SoundEntry();
    public SoundEntry extractionSuccess = new SoundEntry();
    public SoundEntry gameClear = new SoundEntry();
    public SoundEntry gameOver = new SoundEntry();

    [Header("UI")]
    public SoundEntry uiClick = new SoundEntry();
    public SoundEntry uiHover = new SoundEntry();
    public SoundEntry uiModulePickUp = new SoundEntry();
    public SoundEntry uiModulePlace = new SoundEntry();
    public SoundEntry uiModuleRotate = new SoundEntry();
    public SoundEntry uiInvalid = new SoundEntry();
    public SoundEntry uiEquipWeapon = new SoundEntry();
    public SoundEntry uiPanelOpen = new SoundEntry();
}
