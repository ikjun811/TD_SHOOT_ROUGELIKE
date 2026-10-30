using System.Collections.Generic;
using UnityEngine;

// 라운드가 바뀔 때 남아 있으면 안 되는 일회성 오브젝트 (사용자 2026-10-31: 탈출 직전 수류탄 이펙트가 다음 라운드 새 맵에 보임)
//   수류탄·집속 소형 폭탄·대시 폭탄·폭발 이펙트는 게임 시간으로 제거되는데, 탈출 슬로모션 → 정비(시간 정지)를 거치며 남아
//   다음 라운드 시작 순간 예전 위치에서 터지거나 보였음. 생성 시 Register → WaveManager.RestartWaveSpawning(다음 라운드 시작)에서 ClearAll
public static class RoundCleanup
{
    private static readonly List<GameObject> objects = new List<GameObject>();

    public static void Register(GameObject obj)
    {
        if (obj == null) return;
        if (objects.Count > 256) objects.RemoveAll(o => o == null); // 이미 제거된 것 정리
        objects.Add(obj);
    }

    public static void ClearAll()
    {
        int cleared = 0;
        foreach (GameObject obj in objects)
        {
            if (obj == null) continue;
            Object.Destroy(obj);
            cleared++;
        }
        objects.Clear();
        if (cleared > 0) Debug.Log($"RoundCleanup: 지난 라운드 수류탄·폭발 이펙트 {cleared}개 정리");
    }
}
