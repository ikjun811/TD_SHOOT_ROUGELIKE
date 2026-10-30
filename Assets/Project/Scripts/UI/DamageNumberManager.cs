using System.Collections.Generic;
using UnityEngine;
using TMPro;
using JU;

// 피해량 숫자 이펙트 매니저 (전투 씬 로드 시 GameplayBootstrap이 자동 생성, 씬 배치 불필요)
// JU 전역 피해 이벤트(IHealth.OnAnyDamaged)를 받아 맞은 위치에 숫자를 띄움
// 치명타 여부는 CriticalBullet이 RegisterCriticalHit으로 알려 주며, 순서가 보장되지 않아 LateUpdate에서 한꺼번에 처리
// 개별 표시 (사용자 결정 2026-10-30: 대상별 합계 방식 롤백) — 맞을 때마다 숫자 1개, 맞은 위치에서 떠올라 사라짐.
//   외형은 분위기 맞춤 값 유지(2026-10-29): 작고 얇은 바랜 뼈색 글자, 치명타는 호박빛 빨강·크게·순간 흰빛, 플레이어 피해는 짙은 핏빛
public class DamageNumberManager : MonoBehaviour
{
    public static DamageNumberManager Instance { get; private set; }

    [Header("Animation")]
    [SerializeField] private float lifetime = 0.8f;
    [SerializeField] private float riseSpeed = 1.5f;
    [SerializeField] private float randomSpread = 0.3f;    // 여러 발 동시 명중 시 겹침 방지
    [SerializeField] private float fontSize = 3.5f;
    [SerializeField] private float critSizeMultiplier = 1.35f;

    [Header("Colors")]
    [SerializeField] private Color enemyDamageColor = new Color(0.85f, 0.82f, 0.75f);  // 바랜 뼈색
    [SerializeField] private Color critDamageColor = new Color(1f, 0.45f, 0.2f);       // 호박빛 빨강
    [SerializeField] private Color playerDamageColor = new Color(0.75f, 0.1f, 0.08f);  // 짙은 핏빛

    private struct CritHit
    {
        public JUHealth health;
        public float damage;
    }

    private class Popup
    {
        public TextMeshPro text;
        public float timer;
        public Color color;
        public float baseSize;
        public bool crit;
    }

    private readonly List<IHealth.DamageResultInfo> pendingDamages = new List<IHealth.DamageResultInfo>();
    private readonly List<CritHit> pendingCrits = new List<CritHit>();
    private readonly List<Popup> activePopups = new List<Popup>();
    private readonly Stack<Popup> popupPool = new Stack<Popup>();
    private Camera mainCamera;

    // GameplayBootstrap이 전투 씬 로드마다 호출
    public static void EnsureCreated()
    {
        if (Instance == null) new GameObject("DamageNumberManager").AddComponent<DamageNumberManager>();
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnEnable()
    {
        IHealth.OnAnyDamaged += OnAnyDamaged;
    }

    private void OnDisable()
    {
        IHealth.OnAnyDamaged -= OnAnyDamaged;
    }

    // CriticalBullet이 치명타 탄 명중 시 호출
    public static void RegisterCriticalHit(JUHealth health, float damage)
    {
        if (health == null) return;
        AudioManager.PlaySfx(AudioManager.Library.enemyCritHit, health.transform.position + Vector3.up);
        if (Instance == null) return;
        Instance.pendingCrits.Add(new CritHit { health = health, damage = damage });
    }

    private void OnAnyDamaged(IHealth.DamageResultInfo result)
    {
        if (result.RealDamage <= 0f) return;
        pendingDamages.Add(result);
    }

    private void LateUpdate()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        foreach (IHealth.DamageResultInfo result in pendingDamages)
        {
            Component target = result.DamagedObject as Component;
            if (target == null) continue;

            bool isCrit = ConsumeCritHit(target, result.SuggestiveDamage);
            bool isPlayer = target.CompareTag("Player");

            Vector3 pos = result.HitPosition != Vector3.zero ? result.HitPosition : target.transform.position + Vector3.up * 1.5f;
            pos += new Vector3(Random.Range(-randomSpread, randomSpread), 0.3f, Random.Range(-randomSpread, randomSpread));

            Color color = isPlayer ? playerDamageColor : (isCrit ? critDamageColor : enemyDamageColor);
            SpawnPopup(pos, Mathf.RoundToInt(result.RealDamage).ToString(), color, isCrit ? fontSize * critSizeMultiplier : fontSize, isCrit);
        }
        pendingDamages.Clear();
        pendingCrits.Clear();

        UpdatePopups();
    }

    // 같은 대상·같은 피해량의 치명타 기록이 있으면 하나 소모하고 true
    private bool ConsumeCritHit(Component target, float damage)
    {
        for (int i = 0; i < pendingCrits.Count; i++)
        {
            CritHit hit = pendingCrits[i];
            if (hit.health != null && hit.health.gameObject == target.gameObject && Mathf.Approximately(hit.damage, damage))
            {
                pendingCrits.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    private void SpawnPopup(Vector3 position, string label, Color color, float size, bool crit)
    {
        Popup popup = popupPool.Count > 0 ? popupPool.Pop() : CreatePopup();
        popup.text.gameObject.SetActive(true);
        popup.text.transform.position = position;
        popup.text.text = label;
        popup.text.fontSize = size;
        popup.text.color = color;
        popup.color = color;
        popup.baseSize = size;
        popup.crit = crit;
        popup.timer = 0f;
        activePopups.Add(popup);
    }

    private Popup CreatePopup()
    {
        GameObject obj = new GameObject("DamageNumber", typeof(TextMeshPro));
        obj.transform.SetParent(transform, false);

        TextMeshPro tmp = obj.GetComponent<TextMeshPro>();
        UITheme.Current.ApplyFont(tmp, UIFontRole.Heading); // 외곽선(재질) 설정 전에 폰트부터
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Normal;
        tmp.outlineWidth = 0.12f;
        tmp.outlineColor = new Color32(0, 0, 0, 200);
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.GetComponent<MeshRenderer>().sortingOrder = 20;

        return new Popup { text = tmp };
    }

    private void UpdatePopups()
    {
        for (int i = activePopups.Count - 1; i >= 0; i--)
        {
            Popup popup = activePopups[i];
            popup.timer += Time.deltaTime;
            float t = popup.timer / lifetime;

            if (t >= 1f)
            {
                popup.text.gameObject.SetActive(false);
                activePopups.RemoveAt(i);
                popupPool.Push(popup);
                continue;
            }

            Transform tr = popup.text.transform;
            tr.position += Vector3.up * riseSpeed * Time.deltaTime;
            if (mainCamera != null) tr.rotation = mainCamera.transform.rotation;

            // 처음엔 살짝 크게 튀었다가 원래 크기로 (치명타는 순간 흰빛), 후반 절반 동안 페이드 아웃
            float pop = 1f - Mathf.Clamp01(t * 5f);
            popup.text.fontSize = popup.baseSize * Mathf.Lerp(1f, 1.3f, pop);
            Color c = popup.crit ? Color.Lerp(popup.color, Color.white, pop * pop * 0.6f) : popup.color;
            c.a = t < 0.5f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.5f) * 2f);
            popup.text.color = c;
        }
    }
}
