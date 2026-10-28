using System.Collections.Generic;
using UnityEngine;

// 적 피격 번쩍임 (EnemyBase가 Start에서 부착, 피격마다 Flash 호출)
// 맞은 순간 몸의 MeshRenderer·SkinnedMeshRenderer 재질을 흰색 단색(Resources/Shaders/HitFlash)으로 잠깐 바꿨다가 되돌림
//   - 렌더러 목록은 부착 시점에 한 번 기록 → 이후 생기는 실루엣 복제본·체력바는 대상 아님 (EnemyBase가 그보다 먼저 부착)
//   - 재질 배열은 미리 만들어 두고 교체만 (피격마다 할당 없음)
//   - 게임 시간 기준 → 히트 스톱(B2) 중에는 번쩍임이 유지됨
public class EnemyHitFlash : MonoBehaviour
{
    private const string ShaderName = "TDShoot/HitFlash";
    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    private static Material flashMaterial;

    public float flashDuration = 0.06f;
    [ColorUsage(false, true)] public Color flashColor = new Color(1.5f, 1.5f, 1.5f, 1f);

    private readonly List<Renderer> renderers = new List<Renderer>();
    private readonly List<Material[]> originalMaterials = new List<Material[]>();
    private readonly List<Material[]> flashMaterials = new List<Material[]>();
    private float flashEndTime = -1f;
    private bool isFlashing;

    public static EnemyHitFlash Attach(GameObject target)
    {
        EnemyHitFlash flash = target.GetComponent<EnemyHitFlash>();
        if (flash == null) flash = target.AddComponent<EnemyHitFlash>();
        flash.CollectRenderers();
        return flash;
    }

    private void CollectRenderers()
    {
        renderers.Clear();
        originalMaterials.Clear();
        flashMaterials.Clear();

        Material mat = GetFlashMaterial();
        if (mat == null) return;

        foreach (Renderer rend in GetComponentsInChildren<Renderer>(true))
        {
            if (!(rend is SkinnedMeshRenderer) && !(rend is MeshRenderer)) continue; // 파티클·트레일·라인 등 제외
            Material[] original = rend.sharedMaterials;
            Material[] flash = new Material[original.Length];
            for (int i = 0; i < flash.Length; i++) flash[i] = mat;

            renderers.Add(rend);
            originalMaterials.Add(original);
            flashMaterials.Add(flash);
        }
    }

    // 모든 적이 같은 재질 하나를 공유 (색은 첫 생성 시 값)
    private Material GetFlashMaterial()
    {
        if (flashMaterial != null) return flashMaterial;
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogWarning($"EnemyHitFlash: {ShaderName} 셰이더를 찾지 못했습니다 (Resources/Shaders/HitFlash.shader 확인).");
            return null;
        }
        flashMaterial = new Material(shader) { name = "HitFlash", hideFlags = HideFlags.DontSave };
        flashMaterial.SetColor(FlashColorId, flashColor);
        return flashMaterial;
    }

    public void Flash()
    {
        if (renderers.Count == 0) return;
        flashEndTime = Time.time + flashDuration;
        if (isFlashing) return;

        isFlashing = true;
        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] != null) renderers[i].sharedMaterials = flashMaterials[i];
        }
    }

    private void Update()
    {
        if (isFlashing && Time.time >= flashEndTime) Restore();
    }

    private void Restore()
    {
        isFlashing = false;
        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] != null) renderers[i].sharedMaterials = originalMaterials[i];
        }
    }
}
