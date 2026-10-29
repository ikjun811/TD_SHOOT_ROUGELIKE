using System.Collections.Generic;
using UnityEngine;
using TMPro;
using JU;

// 피해량 숫자 이펙트 매니저 (전투 씬 로드 시 GameplayBootstrap이 자동 생성, 씬 배치 불필요)
// JU 전역 피해 이벤트(IHealth.OnAnyDamaged)를 받아 맞은 위치에 숫자를 띄움
// 치명타 여부는 CriticalBullet이 RegisterCriticalHit으로 알려 주며, 순서가 보장되지 않아 LateUpdate에서 한꺼번에 처리
// 분위기 맞춤 (사용자 채택 2026-10-29, A+B): 대상마다 숫자 1개에 피해를 누적(연사·산탄에도 화면이 깔끔), 머리 위 체력바 오른쪽에 붙어 따라감,
//   holdTime 동안 안 맞으면 위로 살짝 흐르며 사라짐. 작고 얇은 바랜 뼈색 글자, 맞을 때마다 짧게 떨림,
//   치명타가 섞이면 숫자 전체가 호박빛 빨강 + 순간 맥박. 플레이어 피해는 짙은 핏빛
public class DamageNumberManager : MonoBehaviour
{
    public static DamageNumberManager Instance { get; private set; }

    [Header("Animation")]
    [SerializeField] private float holdTime = 1f;          // 마지막 피격 뒤 유지 (이 안에 또 맞으면 누적)
    [SerializeField] private float fadeTime = 0.35f;
    [SerializeField] private float fadeRise = 0.4f;        // 사라지며 위로 흐르는 거리 (m)
    [SerializeField] private float fontSize = 3.5f;        // 이전 5 (70%)
    [SerializeField] private float critPulse = 1.35f;      // 치명타 순간 크기 배율
    [SerializeField] private float shakeTime = 0.08f;
    [SerializeField] private float shakeAmount = 0.06f;
    [SerializeField] private float sideOffset = 0.85f;     // 체력바 오른쪽으로 (카메라 기준, m)
    [SerializeField] private float headOffset = 0.25f;     // 콜라이더 꼭대기 위

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
        public Component target;     // 따라갈 대상 (사라지면 마지막 위치에서 흐려짐)
        public Collider targetCollider;
        public Vector3 anchor;       // 마지막 기준 위치
        public float total;          // 누적 피해
        public bool crit;
        public bool isPlayer;
        public float lastHitTime;
        public float pulse;          // 치명타 맥박 (1 → 0)
        public float shake;          // 떨림 남은 시간
    }

    private readonly List<IHealth.DamageResultInfo> pendingDamages = new List<IHealth.DamageResultInfo>();
    private readonly List<CritHit> pendingCrits = new List<CritHit>();
    private readonly List<Popup> activePopups = new List<Popup>();
    private readonly Dictionary<Component, Popup> popupByTarget = new Dictionary<Component, Popup>();
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
            AddDamage(target, result.RealDamage, isCrit);
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

    // 대상의 누적 숫자에 더함 (유지 중이 아니면 새 숫자)
    private void AddDamage(Component target, float damage, bool isCrit)
    {
        if (!popupByTarget.TryGetValue(target, out Popup popup) || popup.text == null || !popup.text.gameObject.activeSelf)
        {
            popup = popupPool.Count > 0 ? popupPool.Pop() : CreatePopup();
            popup.text.gameObject.SetActive(true);
            popup.target = target;
            popup.targetCollider = target.GetComponent<Collider>();
            popup.total = 0f;
            popup.crit = false;
            popup.isPlayer = target.CompareTag("Player");
            popup.pulse = 0f;
            popup.anchor = GetAnchor(popup);
            popupByTarget[target] = popup;
            activePopups.Add(popup);
        }

        popup.total += damage;
        popup.lastHitTime = Time.time;
        popup.shake = shakeTime;
        if (isCrit)
        {
            popup.crit = true;
            popup.pulse = 1f;
        }
        popup.text.text = Mathf.RoundToInt(popup.total).ToString();
        popup.text.color = popup.isPlayer ? playerDamageColor : (popup.crit ? critDamageColor : enemyDamageColor);
    }

    // 머리(콜라이더 꼭대기) 위, 카메라 기준 오른쪽 (체력바와 겹치지 않게)
    private Vector3 GetAnchor(Popup popup)
    {
        if (popup.target == null) return popup.anchor;
        Vector3 top = popup.targetCollider != null && popup.targetCollider.enabled
            ? new Vector3(popup.target.transform.position.x, popup.targetCollider.bounds.max.y, popup.target.transform.position.z)
            : popup.target.transform.position + Vector3.up * 2f;
        Vector3 right = mainCamera != null ? mainCamera.transform.right : Vector3.right;
        return top + Vector3.up * headOffset + right * sideOffset;
    }

    private Popup CreatePopup()
    {
        GameObject obj = new GameObject("DamageNumber", typeof(TextMeshPro));
        obj.transform.SetParent(transform, false);

        TextMeshPro tmp = obj.GetComponent<TextMeshPro>();
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
        float now = Time.time;
        for (int i = activePopups.Count - 1; i >= 0; i--)
        {
            Popup popup = activePopups[i];
            float since = now - popup.lastHitTime;
            float fade = Mathf.Clamp01((since - holdTime) / fadeTime);

            if (fade >= 1f)
            {
                popup.text.gameObject.SetActive(false);
                activePopups.RemoveAt(i);
                if (popup.target != null && popupByTarget.TryGetValue(popup.target, out Popup current) && current == popup) popupByTarget.Remove(popup.target);
                popup.target = null;
                popupPool.Push(popup);
                continue;
            }

            // 대상을 따라감 (대상이 사라졌으면 마지막 위치)
            if (popup.target != null) popup.anchor = GetAnchor(popup);
            Vector3 pos = popup.anchor + Vector3.up * (fadeRise * fade);

            // 맞을 때 짧은 떨림
            if (popup.shake > 0f)
            {
                popup.shake -= Time.deltaTime;
                pos += (Vector3)(Random.insideUnitCircle * shakeAmount);
            }

            Transform tr = popup.text.transform;
            tr.position = pos;
            if (mainCamera != null) tr.rotation = mainCamera.transform.rotation;

            // 치명타 맥박: 크게 → 원래 크기, 순간 흰빛 쪽으로 밝게
            popup.pulse = Mathf.Max(0f, popup.pulse - Time.deltaTime / 0.25f);
            float ease = popup.pulse * popup.pulse;
            popup.text.fontSize = fontSize * Mathf.Lerp(1f, critPulse, ease);
            Color baseColor = popup.isPlayer ? playerDamageColor : (popup.crit ? critDamageColor : enemyDamageColor);
            Color c = Color.Lerp(baseColor, Color.white, ease * 0.6f);
            c.a = 1f - fade;
            popup.text.color = c;
        }
    }
}
