using System.Collections;
using UnityEngine;

public class GrenadeSkill : MonoBehaviour
{
    [Header("Grenade Skill Settings")]
    [SerializeField] private KeyCode grenadeKey = KeyCode.G;
    [SerializeField] private GameObject grenadePrefab;  // 수류탄 3D 프리팹
    [SerializeField] private Transform throwPoint;       // 수류탄 던지는 위치 (손/총구)
    [SerializeField] private float throwForce = 15f;     // 던지는 힘
    [SerializeField] private float cooldown = 10f;       // 쿨타임 (초)
    [SerializeField] private int maxCharges = 2;         // 최대 충전 횟수
    [SerializeField] private float maxCooldownReduction = 0.7f; // 쿨감 상한 (임시값, GDD 8-4)

    [Header("Current Status (Read Only)")]
    [SerializeField] private int currentCharges;
    [SerializeField] private float currentCooldownTimer;

    private float baseCooldown; // 모듈 보정 전 원래 쿨타임

    public int CurrentCharges => currentCharges;
    public float CooldownProgress => currentCooldownTimer / cooldown;

    private void Awake()
    {
        baseCooldown = cooldown;
    }

    // 테트리스 모듈 쿨감 적용 (0.1 = 10% 감소)
    public void SetCooldownReduction(float reduction)
    {
        reduction = Mathf.Clamp(reduction, 0f, maxCooldownReduction);
        cooldown = baseCooldown * (1f - reduction);
        Debug.Log($"💣 [수류탄 쿨타임] {baseCooldown}s -> {cooldown:0.##}s (-{reduction * 100f:0.#}%)");
    }

    private void Start()
    {
        currentCharges = maxCharges;
    }

    private void Update()
    {
        // 쿨타임 충전 연산
        if (currentCharges < maxCharges)
        {
            currentCooldownTimer += Time.deltaTime;
            if (currentCooldownTimer >= cooldown)
            {
                currentCooldownTimer = 0f;
                currentCharges++;
                Debug.Log($"💣 [수류탄 충전 완료] 현재 충전량: {currentCharges} / {maxCharges}");
            }
        }

        // 수류탄 투척 키 입력 (G키)
        if (Input.GetKeyDown(grenadeKey) && currentCharges > 0)
        {
            ThrowGrenade();
        }
    }

    private void ThrowGrenade()
    {
        currentCharges--;
        if (currentCharges < maxCharges && currentCooldownTimer == 0f)
        {
            currentCooldownTimer = 0f; // 충전 타이머 시작
        }

        Vector3 spawnPos = throwPoint != null ? throwPoint.position : transform.position + transform.forward + Vector3.up;
        Vector3 throwDir = (transform.forward + Vector3.up * 0.3f).normalized;

        if (grenadePrefab != null)
        {
            GameObject grenade = Instantiate(grenadePrefab, spawnPos, Quaternion.identity);
            Rigidbody rb = grenade.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddForce(throwDir * throwForce, ForceMode.Impulse);
            }
        }

        Debug.Log($"💣 [수류탄 투척!] 남은 충전량: {currentCharges} / {maxCharges}");
    }
}