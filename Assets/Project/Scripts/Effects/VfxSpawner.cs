using UnityEngine;

// 이펙트 프리팹 1회 재생 (VfxLibrary 칸) — Vefects 프리팹은 데모용으로 전부 반복(looping)이라 생성 직후 반복을 끄고
//   크기는 부모 배율을 따르도록(Hierarchy), 가장 긴 파티클이 끝날 시간 뒤 제거. 게임 시간 기준 (히트 스톱 중 함께 느려짐)
public static class VfxSpawner
{
    // 칸이 비어 있으면 false (호출 쪽 기존 연출로)
    public static bool Play(VfxEntry entry, Vector3 position, float scale = 1f, Quaternion? rotation = null)
    {
        if (entry == null || entry.IsEmpty) return false;
        if (entry.playAll)
        {
            for (int i = 0; i < entry.prefabs.Length; i++)
            {
                if (entry.prefabs[i] != null) Spawn(entry, entry.prefabs[i], entry.GetPrefabScale(i), position, scale, rotation);
            }
            return true;
        }

        GameObject prefab = entry.Pick();
        if (prefab == null) return false;
        Spawn(entry, prefab, entry.GetPrefabScale(System.Array.IndexOf(entry.prefabs, prefab)), position, scale, rotation);
        return true;
    }

    private static void Spawn(VfxEntry entry, GameObject prefab, float prefabScale, Vector3 position, float scale, Quaternion? rotation)
    {
        GameObject vfx = Object.Instantiate(prefab, position + Vector3.up * entry.yOffset, rotation ?? prefab.transform.rotation);
        vfx.transform.localScale = prefab.transform.localScale * (entry.scale * prefabScale * scale);

        // 프리팹에 든 소리는 끔 — Vefects 데모 프리팹은 반복(Loop) AudioSource라 효과음(SoundLibrary)과 겹치고,
        //   제거 타이머가 게임 시간이라 정비(시간 정지) 중에는 계속 반복되던 문제 (2026-10-30). 소리는 호출 쪽 AudioManager가 담당
        foreach (AudioSource source in vfx.GetComponentsInChildren<AudioSource>(true))
        {
            source.Stop();
            source.enabled = false;
        }

        float speed = Mathf.Max(0.05f, entry.speed);
        float lifetime = 0.5f;
        foreach (ParticleSystem ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpeed *= speed;
            float end = (main.startDelay.constantMax + main.duration + main.startLifetime.constantMax) / speed;
            lifetime = Mathf.Max(lifetime, end);
        }
        Object.Destroy(vfx, lifetime + 0.2f);
    }
}
