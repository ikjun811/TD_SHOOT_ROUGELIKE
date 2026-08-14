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

    [Header("Magnet Fly Juice")]
    private bool isFlyingToPlayer = false;
    private Transform targetPlayer;
    private float currentFlySpeed = 8f;
    private float flyAcceleration = 35f; // 날아오면서 점점 빨라지는 가속도

    private void Start()
    {
        SetupVisuals();
    }

    private void Update()
    {
        // 플레이어를 향해 빨려 들어가는 비행 연출 중일 때
        if (isFlyingToPlayer && targetPlayer != null)
        {
            Vector3 targetPos = targetPlayer.position + Vector3.up * 1.0f; // 가슴 높이

            // 플레이어를 향해 가속 비행
            transform.position = Vector3.MoveTowards(transform.position, targetPos, currentFlySpeed * Time.deltaTime);
            currentFlySpeed += flyAcceleration * Time.deltaTime;

            // 다가올수록 크기가 작아지는 쏙 흡수 연출
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, 3f * Time.deltaTime);

            // 플레이어에 도착하면 획득 처리 및 파괴
            if (Vector3.Distance(transform.position, targetPos) < 0.5f)
            {
                CollectAndDestroy();
            }
        }
        else
        {
            // 평소 기본 빙글빙글 회전
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        }
    }

    // ItemDropManager에서 호출하는 세팅 함수들
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

    // 자석 흡수 시작 함수
    public void StartFlyingToPlayer(Transform player)
    {
        targetPlayer = player;
        isFlyingToPlayer = true;

        // 충돌체 무력화 (날아오는 동안 추가 충돌 방지)
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
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
        // 직접 발로 걸어서 주웠을 때
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