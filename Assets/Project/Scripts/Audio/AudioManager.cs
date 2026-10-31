using System.Collections.Generic;
using UnityEngine;

// 소리 재생 매니저 (자동 생성·씬 사이 유지, 컴포넌트 연결 불필요)
//   배경음: 채널 2개를 번갈아 쓰며 서서히 전환 (unscaledDeltaTime — 정비 중 timeScale 0에서도 동작)
//   효과음: 2D·3D 소스 풀, 같은 칸은 minInterval 안에 1번만, 음높이 무작위
//   음량: 칸 음량 x GameSettings.MusicVolume / SfxVolume (전체 음량은 AudioListener) — 설정 변경이 매 프레임 반영
//   곡 선택은 MusicDirector(같은 오브젝트)가 씬·상태를 보고 자동으로, 정비 중엔 SetMusicMuffled로 먹먹하게
// 사용: AudioManager.PlaySfx(AudioManager.Library.enemyHit, 위치) / AudioManager.PlayMusic(AudioManager.Library.combat)
public class AudioManager : MonoBehaviour
{
    private const int Pool2DSize = 12;
    private const int Pool3DSize = 24;

    public static AudioManager Instance { get; private set; }

    private static SoundLibrary library;

    // Resources/SoundLibrary 에셋, 없으면 무음 기본값
    public static SoundLibrary Library
    {
        get
        {
            if (library == null)
            {
                library = Resources.Load<SoundLibrary>(SoundLibrary.ResourcePath);
                if (library == null)
                {
                    library = ScriptableObject.CreateInstance<SoundLibrary>();
                    library.hideFlags = HideFlags.DontSave;
                }
            }
            return library;
        }
    }

    private readonly AudioSource[] musicSources = new AudioSource[2];
    private readonly float[] musicEntryVolume = new float[2];
    private readonly float[] musicFade = new float[2]; // 0~1
    private int activeMusic;
    private MusicEntry currentMusic;
    private float fadeSpeed = 1f;
    private readonly AudioLowPassFilter[] musicFilters = new AudioLowPassFilter[2];
    private bool musicMuffled;
    private float muffle; // 0~1 (정비 먹먹함)
    private const float OpenCutoff = 22000f;

    private readonly List<AudioSource> pool2D = new List<AudioSource>();
    private readonly List<AudioSource> pool3D = new List<AudioSource>();
    private readonly List<float> volume2D = new List<float>();
    private readonly List<float> volume3D = new List<float>();
    private readonly Dictionary<SoundEntry, float> lastPlayTime = new Dictionary<SoundEntry, float>();
    private int next2D;
    private int next3D;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        EnsureCreated();
    }

    public static AudioManager EnsureCreated()
    {
        if (Instance != null) return Instance;
        GameObject go = new GameObject("AudioManager");
        DontDestroyOnLoad(go);
        return go.AddComponent<AudioManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        for (int i = 0; i < 2; i++)
        {
            AudioSource source = CreateSource("Music_" + i, false);
            source.loop = true;
            source.priority = 0;
            musicSources[i] = source;
            AudioLowPassFilter filter = source.gameObject.AddComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = OpenCutoff;
            musicFilters[i] = filter;
        }
        gameObject.AddComponent<MusicDirector>();
        for (int i = 0; i < Pool2DSize; i++)
        {
            pool2D.Add(CreateSource("Sfx2D_" + i, false));
            volume2D.Add(1f);
        }
        for (int i = 0; i < Pool3DSize; i++)
        {
            pool3D.Add(CreateSource("Sfx3D_" + i, true));
            volume3D.Add(1f);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private AudioSource CreateSource(string sourceName, bool spatial)
    {
        GameObject go = new GameObject(sourceName);
        go.transform.SetParent(transform, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        if (spatial)
        {
            SoundLibrary lib = Library;
            source.spatialBlend = lib.spatialBlend3D;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = lib.minDistance3D;
            source.maxDistance = lib.maxDistance3D;
            source.dopplerLevel = 0f;
        }
        else
        {
            source.spatialBlend = 0f;
        }
        return source;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        SoundLibrary lib = Library;
        muffle = Mathf.MoveTowards(muffle, musicMuffled ? 1f : 0f, dt / Mathf.Max(0.01f, lib.muffleFadeTime));
        // 차단 주파수는 귀에 고르게 들리도록 지수 보간
        float cutoff = OpenCutoff * Mathf.Pow(Mathf.Clamp(lib.maintenanceCutoff, 10f, OpenCutoff) / OpenCutoff, muffle);
        float musicVolume = GameSettings.MusicVolume * Mathf.Lerp(1f, lib.maintenanceVolume, muffle);
        for (int i = 0; i < 2; i++)
        {
            float target = i == activeMusic && currentMusic != null ? 1f : 0f;
            musicFade[i] = Mathf.MoveTowards(musicFade[i], target, fadeSpeed * dt);
            AudioSource source = musicSources[i];
            musicFilters[i].cutoffFrequency = cutoff;
            source.volume = musicEntryVolume[i] * musicFade[i] * musicVolume;
            if (musicFade[i] <= 0f && source.isPlaying && i != activeMusic) source.Stop();
        }

        // 재생 중인 효과음도 설정 변경 즉시 반영
        float sfxVolume = GameSettings.SfxVolume;
        for (int i = 0; i < pool2D.Count; i++)
        {
            if (pool2D[i].isPlaying) pool2D[i].volume = volume2D[i] * sfxVolume;
        }
        for (int i = 0; i < pool3D.Count; i++)
        {
            if (pool3D[i].isPlaying) pool3D[i].volume = volume3D[i] * sfxVolume;
        }
    }

    #region Music

    public static bool IsMusicPlaying => Instance != null && Instance.currentMusic != null;

    // 같은 곡이면 무시, null·빈 칸이면 서서히 정지
    public static void PlayMusic(MusicEntry entry, float fadeTime = -1f)
    {
        EnsureCreated().PlayMusicInternal(entry, fadeTime);
    }

    public static void StopMusic(float fadeTime = -1f)
    {
        EnsureCreated().PlayMusicInternal(null, fadeTime);
    }

    // 정비 중 먹먹하게 (로우패스 + 음량, 라이브러리 muffleFadeTime에 걸쳐 전환)
    public static void SetMusicMuffled(bool muffled)
    {
        EnsureCreated().musicMuffled = muffled;
    }

    private void PlayMusicInternal(MusicEntry entry, float fadeTime)
    {
        if (entry != null && entry.clip == null) entry = null;
        if (entry == currentMusic) return;

        if (fadeTime < 0f) fadeTime = Library.musicFadeTime;
        fadeSpeed = fadeTime > 0.01f ? 1f / fadeTime : 1000f;
        currentMusic = entry;
        if (entry == null) return; // 현재 채널만 페이드 아웃 (Update)

        activeMusic = 1 - activeMusic;
        AudioSource source = musicSources[activeMusic];
        source.clip = entry.clip;
        musicEntryVolume[activeMusic] = entry.volume;
        musicFade[activeMusic] = 0f;
        source.volume = 0f;
        source.Play();
    }

    #endregion

    #region Sfx

    // 2D 효과음 (UI·플레이어 자신)
    public static void PlaySfx(SoundEntry entry)
    {
        if (entry == null || entry.IsEmpty) return;
        EnsureCreated().PlaySfxInternal(entry, false, Vector3.zero);
    }

    // 3D 효과음 (월드 위치)
    public static void PlaySfx(SoundEntry entry, Vector3 position)
    {
        if (entry == null || entry.IsEmpty) return;
        EnsureCreated().PlaySfxInternal(entry, true, position);
    }

    private void PlaySfxInternal(SoundEntry entry, bool spatial, Vector3 position)
    {
        float now = Time.unscaledTime;
        if (lastPlayTime.TryGetValue(entry, out float last) && now - last < entry.minInterval) return;

        AudioClip clip = entry.PickClip();
        if (clip == null) return;
        lastPlayTime[entry] = now;

        List<AudioSource> pool = spatial ? pool3D : pool2D;
        List<float> volumes = spatial ? volume3D : volume2D;
        int index = spatial ? FindFreeSource(pool, ref next3D) : FindFreeSource(pool, ref next2D);

        AudioSource source = pool[index];
        volumes[index] = entry.volume;
        source.transform.position = position;
        source.clip = clip;
        source.pitch = 1f + Random.Range(-entry.pitchRandom, entry.pitchRandom);
        source.volume = entry.volume * GameSettings.SfxVolume;
        source.Play();
    }

    // 쉬는 소스 우선, 전부 재생 중이면 차례대로 가장 오래된 것부터 덮어씀
    private static int FindFreeSource(List<AudioSource> pool, ref int next)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            int index = (next + i) % pool.Count;
            if (!pool[index].isPlaying)
            {
                next = (index + 1) % pool.Count;
                return index;
            }
        }
        int oldest = next;
        next = (next + 1) % pool.Count;
        return oldest;
    }

    #endregion
}
