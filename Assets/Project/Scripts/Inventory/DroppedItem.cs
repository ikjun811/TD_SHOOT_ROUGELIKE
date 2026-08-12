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

    private void Start()
    {
        SetupVisuals();
    }

    private void Update()
    {
        // 회전 연출
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
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

        // 마테리얼 및 라이트 색상 변경
        if (itemRenderer != null) itemRenderer.material.color = rarityColor;
        if (itemLight != null) itemLight.color = rarityColor;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 플레이어에 닿으면 인벤토리에 추가 후 삭제
        if (other.CompareTag("Player"))
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
}