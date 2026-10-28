using System.Collections.Generic;
using UnityEngine;

// 엘리트 등장 예고 (D1) — WaveManager가 엘리트 스폰 위치에 생성, duration 뒤 onComplete로 실제 생성
//   눈에 보이는 표시는 없음: 엘리트는 화면 밖에서만 생성돼 바닥 원이 보이지 않아 제거 (사용자 결정)
//   예고 중 위치는 EliteOffscreenIndicator가 화면 가장자리 큰 맥박 화살표로, 문구는 AlertBanner 한 줄 알림으로 표시
//   게임 시간 기준 (정비 중 정지), 라운드 종료 시 WaveManager.DespawnAllActiveEnemies가 ClearAll → 생성 취소
public class EliteSpawnTelegraph : MonoBehaviour
{
    private static readonly List<EliteSpawnTelegraph> active = new List<EliteSpawnTelegraph>();

    public static IReadOnlyList<EliteSpawnTelegraph> Active => active;

    private float duration;
    private float elapsed;
    private System.Action<Vector3> onComplete;

    public float Progress => duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;

    // radius는 예전 바닥 원 크기 (호출 쪽 호환용, 사용 안 함)
    public static EliteSpawnTelegraph Create(Vector3 position, float duration, float radius, System.Action<Vector3> onComplete)
    {
        GameObject go = new GameObject("EliteSpawnTelegraph");
        go.transform.position = position;
        EliteSpawnTelegraph telegraph = go.AddComponent<EliteSpawnTelegraph>();
        telegraph.duration = duration;
        telegraph.onComplete = onComplete;
        return telegraph;
    }

    // 생성 취소 (라운드 종료)
    public static void ClearAll()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (active[i] != null) Destroy(active[i].gameObject);
        }
        active.Clear();
    }

    private void OnEnable()
    {
        active.Add(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed < duration) return;

        System.Action<Vector3> callback = onComplete;
        onComplete = null;
        Vector3 position = transform.position;
        Destroy(gameObject);
        callback?.Invoke(position);
    }
}
