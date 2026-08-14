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

    private void Start()
    {
        SetupVisuals();
    }

    private void Update()
    {
        if (isFlyingToPlayer && targetPlayer != null)
        {
            Vector3 targetPos = targetPlayer.position + Vector3.up * 1.0f;

            // 플레이어를 향해 가속 비행
            transform.position = Vector3.MoveTowards(transform.position, targetPos, currentFlySpeed * Time.deltaTime);
            currentFlySpeed += flyAcceleration * Time.deltaTime;

            // 크기 축소 연출
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, 4f * Time.deltaTime);

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
            rarityColor = moduleData.rarity switch
            {
                RarityType.Common => Color.white,
                RarityType.Rare => new Color(0.2f, 0.6f, 1f),
                RarityType.Elite => new Color(0.7f, 0.3f, 0.9f),
                RarityType.Legendary => new Color(1f, 0.8f, 0.1f),
                _ => Color.white
            };
        }

        if (itemRenderer != null) itemRenderer.material.color = rarityColor;
        if (itemLight != null) itemLight.color = rarityColor;
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
            }
            else if (category == ItemCategory.Module && moduleData != null)
            {
                PlayerInventory.Instance.AddModule(moduleData);
            }
        }

        Destroy(gameObject);
    }
}