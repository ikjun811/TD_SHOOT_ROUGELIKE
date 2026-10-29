using UnityEngine;

public class DroppedItem : MonoBehaviour
{
    public enum ItemCategory { Weapon, Module }

    [Header("Item Data")]
    public ItemCategory category;
    public WeaponDataSO weaponData;
    public ModuleDataSO moduleData;

    [Header("Visuals")]
    [SerializeField] private MeshRenderer itemRenderer;
    [SerializeField] private Light itemLight;
    [SerializeField] private float rotateSpeed = 90f;

    [Header("Magnet Fly Juice ⭐")]
    private bool isFlyingToPlayer = false;
    private Transform targetPlayer;
    private float currentFlySpeed = 10f;
    private float flyAcceleration = 40f;

    public bool IsFlyingToPlayer => isFlyingToPlayer;

    // .material 접근 시 생성되는 머티리얼 복사본 (오브젝트 삭제 시 함께 해제)
    private Material instancedMaterial;
    private GameObject dropVisual;

    // 획득 트리거가 JU 바닥·계단 검사(WhatIsGround = Default, Queries Hit Triggers 켜짐)에 잡혀
    // 아이템 위를 지날 때 이동이 걸리지 않도록 Ignore Raycast 레이어로 (ExtractionZone과 같은 처리, 트리거 획득은 그대로 동작)
    private void Awake()
    {
        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
    }

    private void Start()
    {
        SetupVisuals();
    }

    private void OnDestroy()
    {
        if (instancedMaterial != null)
        {
            Destroy(instancedMaterial);
        }
    }

    private void Update()
    {
        if (isFlyingToPlayer && targetPlayer != null)
        {
            Vector3 targetPos = targetPlayer.position + Vector3.up * 1.0f;

            // 흡수 비행은 실제 시간 기준 — 탈출 성공 슬로모션(D3)·히트 스톱 중에도 평소 속도로 빨려옴
            float dt = Time.unscaledDeltaTime;

            // 플레이어를 향해 가속 비행
            transform.position = Vector3.MoveTowards(transform.position, targetPos, currentFlySpeed * dt);
            currentFlySpeed += flyAcceleration * dt;

            // 크기 축소 연출
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, 4f * dt);

            // 도착 시 획득
            if (Vector3.Distance(transform.position, targetPos) < 0.6f)
            {
                CollectAndDestroy();
            }
        }
        else
        {
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        }
    }

    public void SetupWeapon(WeaponDataSO data)
    {
        category = ItemCategory.Weapon;
        weaponData = data;
        SetupVisuals();
    }

    public void SetupModule(ModuleDataSO data)
    {
        category = ItemCategory.Module;
        moduleData = data;
        SetupVisuals();
    }

    // 자석 연출 시작 (거리 비례 속도 보정)
    public void StartFlyingToPlayer(Transform player)
    {
        targetPlayer = player;
        isFlyingToPlayer = true;

        // 거리에 비례하여 출발 속도 연산 (멀리 있을수록 더 폭발적으로 시작)
        float distance = Vector3.Distance(transform.position, player.position);
        currentFlySpeed = Mathf.Max(12f, distance * 2.0f);

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }

    // 안전망: 공중에 떠있는 상태로 정비 단계 진입 시 강제 획득
    public void ForceInstantCollect()
    {
        CollectAndDestroy();
    }

    private void SetupVisuals()
    {
        Color rarityColor = Color.white;

        if (category == ItemCategory.Weapon && weaponData != null)
        {
            rarityColor = weaponData.GetRarityColor();
        }
        else if (category == ItemCategory.Module && moduleData != null)
        {
            rarityColor = UITheme.Current.GetRarityColor(moduleData.rarity);
        }

        if (itemRenderer != null)
        {
            if (instancedMaterial == null) instancedMaterial = itemRenderer.material;
            instancedMaterial.color = rarityColor;
        }
        if (itemLight != null) itemLight.color = rarityColor;

        // 외형 (DroppedItemVisual): 무기 = 실제 JU 무기 모델, 모듈 = 테트리스 모양 블록 — 만들어지면 기본 정육면체 숨김
        //   SetupVisuals가 여러 번 불려도(Start·Setup) 하나만 유지
        if (dropVisual != null) Destroy(dropVisual);
        dropVisual = null;
        if (category == ItemCategory.Weapon && weaponData != null) dropVisual = DroppedItemVisual.BuildWeapon(transform, weaponData.weaponPrefab);
        else if (category == ItemCategory.Module && moduleData != null) dropVisual = DroppedItemVisual.BuildModule(transform, moduleData, rarityColor);
        if (dropVisual != null)
        {
            MeshRenderer cube = GetComponent<MeshRenderer>();
            if (cube != null) cube.enabled = false;
        }
        Light glow = itemLight != null ? itemLight : GetComponentInChildren<Light>();
        if (glow != null) glow.color = rarityColor; // 레어도 색 빛 (프리팹 자식 Point Light)

        // 레어도 빛기둥 (C1)
        if (category == ItemCategory.Weapon && weaponData != null)
        {
            LootBeam.Attach(this, weaponData.rarity, false, rarityColor);
        }
        else if (category == ItemCategory.Module && moduleData != null)
        {
            LootBeam.Attach(this, moduleData.rarity, moduleData.category == ModuleCategory.Unique, rarityColor);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isFlyingToPlayer && other.CompareTag("Player"))
        {
            CollectAndDestroy();
        }
    }

    private void CollectAndDestroy()
    {
        if (PlayerInventory.Instance != null)
        {
            if (category == ItemCategory.Weapon && weaponData != null)
            {
                PlayerInventory.Instance.AddWeapon(weaponData);
                PickupNoticeUI.ShowWeapon(weaponData);
            }
            else if (category == ItemCategory.Module && moduleData != null)
            {
                PlayerInventory.Instance.AddModule(moduleData);
                PickupNoticeUI.ShowModule(moduleData);
            }
        }

        Destroy(gameObject);
    }
}