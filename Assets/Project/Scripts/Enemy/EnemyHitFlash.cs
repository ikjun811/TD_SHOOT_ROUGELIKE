using System.Collections.Generic;
using UnityEngine;

// 적 피격 번쩍임 (EnemyBase가 Start에서 부착, 피격마다 Flash 호출)
// 맞은 순간 몸의 MeshRenderer·SkinnedMeshRenderer 재질을 흰색 단색(Resources/Shaders/HitFlash)으로 잠깐 바꿨다가 되돌림
//   - 렌더러 목록은 부착 시점에 한 번 기록 → 이후 생기는 실루엣 복제본·체력바는 대상 아님 (EnemyBase가 그보다 먼저 부착)
//   - 재질 배열은 미리 만들어 두고 교체만 (피격마다 할당 없음)
//   - 게임 시간 기준 → 히트 스톱(B2) 중에는 번쩍임이 유지됨
//   - 경고 번쩍임(FlashAlert, 붉은색): 봄버 자폭 예고 등
//   - 실루엣(SilhouetteRenderer)이 몸체 재질 뒤에 덧붙이는 재질은 SetTrailingMaterials로 받아 세 재질 세트 끝에 유지 (번쩍임 중에도 실루엣 보임)
public class EnemyHitFlash : MonoBehaviour
{
    private const string ShaderName = "TDShoot/HitFlash";
    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    private static Material flashMaterial;
    private static Material alertMaterial;

    public float flashDuration = 0.06f;
    [ColorUsage(false, true)] public Color flashColor = new Color(1.5f, 1.5f, 1.5f, 1f);
    [ColorUsage(false, true)] public Color alertColor = new Color(2f, 0.25f, 0.15f, 1f);

    private readonly List<Renderer> renderers = new List<Renderer>();
    private readonly List<Material[]> originalMaterials = new List<Material[]>();
    private readonly List<Material[]> flashMaterials = new List<Material[]>();
    private readonly List<Material[]> alertMaterials = new List<Material[]>();
    private readonly List<int> baseCounts = new List<int>(); // 원래 재질 수 (덧붙인 재질 제외)
    private float flashEndTime = -1f;
    private List<Material[]> currentSet; // null = 원래 재질

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
        alertMaterials.Clear();
        baseCounts.Clear();

        Material mat = GetFlashMaterial();
        if (mat == null) return;
        Material alert = GetAlertMaterial();

        foreach (Renderer rend in GetComponentsInChildren<Renderer>(true))
        {
            if (!(rend is SkinnedMeshRenderer) && !(rend is MeshRenderer)) continue; // 파티클·트레일·라인 등 제외
            Material[] original = rend.sharedMaterials;
            Material[] flash = new Material[original.Length];
            Material[] alertSet = new Material[original.Length];
            for (int i = 0; i < flash.Length; i++)
            {
                flash[i] = mat;
                alertSet[i] = alert;
            }

            renderers.Add(rend);
            originalMaterials.Add(original);
            flashMaterials.Add(flash);
            alertMaterials.Add(alertSet);
            baseCounts.Add(original.Length);
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

    private Material GetAlertMaterial()
    {
        if (alertMaterial != null) return alertMaterial;
        alertMaterial = new Material(flashMaterial) { name = "HitFlashAlert", hideFlags = HideFlags.DontSave };
        alertMaterial.SetColor(FlashColorId, alertColor);
        return alertMaterial;
    }

    // 실루엣 재질을 원래·흰색·붉은색 세트 끝에 붙임(null = 뗌) 후 지금 세트를 다시 적용. 이 컴포넌트가 관리하지 않는 렌더러면 false
    public bool SetTrailingMaterials(Renderer rend, Material[] trailing)
    {
        int i = renderers.IndexOf(rend);
        if (i < 0) return false;
        originalMaterials[i] = WithTrailing(originalMaterials[i], baseCounts[i], trailing);
        flashMaterials[i] = WithTrailing(flashMaterials[i], baseCounts[i], trailing);
        alertMaterials[i] = WithTrailing(alertMaterials[i], baseCounts[i], trailing);
        rend.sharedMaterials = (currentSet ?? originalMaterials)[i];
        return true;
    }

    private static Material[] WithTrailing(Material[] set, int baseCount, Material[] trailing)
    {
        int extra = trailing != null ? trailing.Length : 0;
        Material[] result = new Material[baseCount + extra];
        System.Array.Copy(set, result, baseCount);
        for (int k = 0; k < extra; k++) result[baseCount + k] = trailing[k];
        return result;
    }

    // 피격 (흰색)
    public void Flash()
    {
        Show(flashMaterials, flashDuration);
    }

    // 경고 (붉은색)
    public void FlashAlert(float duration)
    {
        Show(alertMaterials, duration);
    }

    private void Show(List<Material[]> set, float duration)
    {
        if (renderers.Count == 0) return;
        flashEndTime = Time.time + duration;
        if (currentSet == set) return;
        Apply(set);
    }

    private void Update()
    {
        if (currentSet != null && Time.time >= flashEndTime) Apply(null);
    }

    private void Apply(List<Material[]> set)
    {
        currentSet = set;
        List<Material[]> source = set ?? originalMaterials;
        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] != null) renderers[i].sharedMaterials = source[i];
        }
    }
}
