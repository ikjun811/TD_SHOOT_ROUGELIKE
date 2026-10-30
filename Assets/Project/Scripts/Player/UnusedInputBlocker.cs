using JUTPS;
using JUTPS.JUInputSystem;
using UnityEngine;
using UnityEngine.InputSystem;

// 안 쓰는 JU 입력 끄기 (사용자 2026-10-30 — 쓰지 않는 키 점검) — GameplayBootstrap이 플레이어에 부착
//   JU 입력 에셋(Player Character Inputs)·코드는 그대로 두고, 실행 중 해당 InputAction만 Disable
//   JUCharacterController가 켜질 때마다(OnEnable — 정비 후 조작 복구 등) 모든 입력을 다시 켜므로 매 프레임 확인해 끔
//   실행 순서를 JU보다 앞(-200)으로 → 다시 켜진 프레임에도 JU가 읽기 전에 꺼짐
//   쓰는 키: 이동 WASD, 조준 마우스, 사격 왼쪽 클릭, 대시 Space, 재장전 R, 무기 교체 E, 수류탄 오른쪽 클릭(GrenadeSkill), ESC(일시정지)
[DefaultExecutionOrder(-200)]
public class UnusedInputBlocker : MonoBehaviour
{
    [Header("끌 JU 입력 (켜면 막음)")]
    [Tooltip("Q — 이전 무기 (E 하나로 2개 교체)")]
    public bool blockPreviousItem = true;
    [Tooltip("마우스 휠 굴리기 — 무기 교체 (휠 클릭 즉시 해제·실수로 굴려 무기가 바뀌는 문제)")]
    public bool blockScrollSwitch = true;
    [Tooltip("숫자키 1~0 — 슬롯 무기 선택 (장착 슬롯이 2개뿐이고 E로 충분)")]
    public bool blockSlotKeys = true;
    [Tooltip("C — 앉기")]
    public bool blockCrouch = true;
    [Tooltip("Z — 엎드리기")]
    public bool blockProne = true;
    [Tooltip("Ctrl — 구르기 (대시가 대신함)")]
    public bool blockRoll = true;
    [Tooltip("F — 상호작용 (JU 상호작용 오브젝트 없음)")]
    public bool blockInteract = true;
    [Tooltip("Tab — JU 인벤토리 (정비 화면이 대신함)")]
    public bool blockInventory = true;
    [Tooltip("Shift — 달리기 (미사용, 사용자 2026-10-30)")]
    public bool blockRun = true;
    [Tooltip("마우스 오른쪽 — JU 조준 자세 (미사용, 오른쪽 클릭은 수류탄 투척 — 2026-10-31)")]
    public bool blockAim = true;

    private JUCharacterController controller;

    public static void EnsureOnPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && player.GetComponent<UnusedInputBlocker>() == null) player.AddComponent<UnusedInputBlocker>();
    }

    private void Awake()
    {
        controller = GetComponent<JUCharacterController>();
    }

    private void Update()
    {
        JUPlayerCharacterInputAsset inputs = controller != null ? controller.Inputs : null;
        if (inputs == null) return;

        var move = inputs.Movement;
        var items = inputs.ItemSwitching;
        if (blockPreviousItem) Block(items.EquipePreviousItemAction);
        if (blockScrollSwitch) Block(items.EquipItemScrollAction);
        if (blockSlotKeys)
        {
            Block(items.EquipSlot1Action); Block(items.EquipSlot2Action); Block(items.EquipSlot3Action);
            Block(items.EquipSlot4Action); Block(items.EquipSlot5Action); Block(items.EquipSlot6Action);
            Block(items.EquipSlot7Action); Block(items.EquipSlot8Action); Block(items.EquipSlot9Action);
            Block(items.EquipSlot10Action);
        }
        if (blockCrouch) Block(move.CrouchAction);
        if (blockProne) Block(move.ProneAction);
        if (blockRoll) Block(move.RollAction);
        if (blockInteract) Block(inputs.InteractAction);
        if (blockInventory) Block(inputs.OpenInventoryAction);
        if (blockRun) Block(move.RunAction);
        if (blockAim) Block(move.AimAction);
    }

    private static void Block(InputAction action)
    {
        if (action != null && action.enabled) action.Disable();
    }
}
