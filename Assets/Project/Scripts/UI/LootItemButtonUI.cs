using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class LootItemButtonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private WeaponDataSO weaponData;
    private ModuleDataSO moduleData;

    [Header("Mini Shape Parent")]
    [SerializeField] private Transform miniShapeParent;

    public void Setup(WeaponDataSO weapon)
    {
        weaponData = weapon;
        moduleData = null;

        ClearMiniShape();

        // 무기 아이콘 버튼 배경 및 텍스트 설정
        Image btnBg = GetComponent<Image>();
        if (btnBg != null) btnBg.color = weapon.GetRarityColor();

        TextMeshProUGUI btnText = GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null)
        {
            btnText.text = weapon.weaponName;
            btnText.fontSize = 12;
            btnText.color = Color.black;
            btnText.alignment = TextAlignmentOptions.Center;
        }
    }

    public void Setup(ModuleDataSO module)
    {
        moduleData = module;
        weaponData = null;

        // ⭐ 모듈 2D 미니 테트리스 형태 자동 생성
        BuildMiniShapeVisual(module);
    }

    private void BuildMiniShapeVisual(ModuleDataSO module)
    {
        if (module == null) return;

        // 1. 미니 모양 부모 컨테이너 자동 생성 (버튼 중앙 80x60 영역)
        if (miniShapeParent == null)
        {
            GameObject container = new GameObject("MiniShapeContainer", typeof(RectTransform), typeof(GridLayoutGroup));
            container.transform.SetParent(transform, false);
            miniShapeParent = container.transform;

            RectTransform rect = container.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(80, 60);
            rect.anchoredPosition = new Vector2(0, 8); // 살짝 위로
        }

        ClearMiniShape();

        // ⭐ 2. 버튼 상자를 넘치지 않도록 모듈 크기(width, height) 기반 동적 타일 크기 연산!
        float containerW = 76f;
        float containerH = 56f;
        float spacing = 2f;

        float maxTileW = (containerW - (spacing * (module.width - 1))) / module.width;
        float maxTileH = (containerH - (spacing * (module.height - 1))) / module.height;
        float dynamicTileSize = Mathf.Clamp(Mathf.Min(maxTileW, maxTileH), 8f, 22f); // 8px ~ 22px 가변 크기

        GridLayoutGroup grid = miniShapeParent.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = module.width;
            grid.cellSize = new Vector2(dynamicTileSize, dynamicTileSize);
            grid.spacing = new Vector2(spacing, spacing);
            grid.childAlignment = TextAnchor.MiddleCenter;
        }

        // ⭐ 3. 버튼 안쪽 배경을 어두운 색으로 바꿔 미니 타일 시인성 극대화
        Image btnBg = GetComponent<Image>();
        if (btnBg != null)
        {
            btnBg.color = new Color(0.12f, 0.12f, 0.12f, 0.95f); // 어두운 철판 배경
        }

        Color rarityColor = GetRarityColor(module.rarity);

        // ⭐ 4. 미니 타일 생성 (검은색 테두리 부착)
        for (int r = 0; r < module.height; r++)
        {
            for (int c = 0; c < module.width; c++)
            {
                int index = r * module.width + c;

                GameObject tileObj = new GameObject($"MiniTile_{c}_{r}", typeof(RectTransform), typeof(Image), typeof(Outline));
                tileObj.transform.SetParent(miniShapeParent, false);

                Image img = tileObj.GetComponent<Image>();
                img.raycastTarget = false;

                // 선명한 검은색 아웃라인 테두리 추가 ⭐
                Outline outline = tileObj.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                    outline.effectDistance = new Vector2(1, -1);
                }

                if (module.shapeGrid[index])
                {
                    img.color = rarityColor; // 쨍한 레어도 색상
                }
                else
                {
                    img.color = new Color(0, 0, 0, 0); // 빈 공간 투명
                }
            }
        }

        // ⭐ 5. 하단 이름 텍스트 설정 (레어도 색상)
        TextMeshProUGUI btnText = GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null)
        {
            btnText.text = module.moduleName;
            btnText.fontSize = 11;
            btnText.color = rarityColor; // 글씨도 레어도 색상
            btnText.alignment = TextAlignmentOptions.Bottom;
            btnText.transform.SetAsLastSibling(); // 텍스트를 가장 맨 위에 띄움
        }
    }

    private void ClearMiniShape()
    {
        if (miniShapeParent != null)
        {
            foreach (Transform child in miniShapeParent)
            {
                Destroy(child.gameObject);
            }
        }
    }

    // 버튼 클릭 시 처리
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            // 가방에서 집었다고 fromLootList: true 전달!
            if (moduleData != null && ModuleDragHandler.Instance != null)
            {
                ModuleDragHandler.Instance.StartDragModule(moduleData, fromLootList: true);
            }
            else if (weaponData != null && WeaponDragHandler.Instance != null)
            {
                WeaponDragHandler.Instance.StartDragWeapon(weaponData);
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (ItemTooltipUI.Instance == null) return;

        if (weaponData != null)
            ItemTooltipUI.Instance.ShowWeaponTooltip(weaponData);
        else if (moduleData != null)
            ItemTooltipUI.Instance.ShowModuleTooltip(moduleData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (ItemTooltipUI.Instance != null)
            ItemTooltipUI.Instance.HideTooltip();
    }

    private Color GetRarityColor(RarityType rarity)
    {
        return rarity switch
        {
            RarityType.Common => Color.white,
            RarityType.Rare => new Color(0.2f, 0.6f, 1f),
            RarityType.Elite => new Color(0.7f, 0.3f, 0.9f),
            RarityType.Legendary => new Color(1f, 0.8f, 0.1f),
            _ => Color.white
        };
    }
}