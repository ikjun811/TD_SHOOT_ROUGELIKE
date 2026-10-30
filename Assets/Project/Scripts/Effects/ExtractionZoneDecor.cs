using System.Collections.Generic;
using UnityEngine;

// 탈출 구역 꾸밈 (보유 에셋, 탈출 구역 연출 2단계) — 씬의 ExtractionZone에 부착
//   만들기: Tools > TD Shoot > Apply SpaceStation Extraction Decor (SciFi_TopDown_SpaceStation 프리팹 연결)
//   착륙장 바닥(가운데) + 표지등(링 안쪽 원 위, 위를 보게 눕힘) + 콘솔 + 바닥 환풍구, 겉모습만 (콜라이더 끔 → 판정·NavMesh 무관)
//   구역이 옮겨지면(라운드마다 재배치) 새 위치에서 다시 배치, 장애물(Walls)과 겹치는 소품은 건너뜀 (착륙장 바닥 제외)
//   구역 배율과 무관하게 따로 둔 루트에 생성
public class ExtractionZoneDecor : MonoBehaviour
{
    [Header("프리팹 (비우면 그 소품 없음)")]
    public GameObject padPrefab;
    public GameObject beaconPrefab;
    public GameObject consolePrefab;
    public GameObject ventPrefab;

    [Header("배치")]
    public float padHeight = 0.02f;           // 바닥과 겹쳐 깜빡이지 않게 살짝 위로
    public int beaconCount = 8;
    public float beaconInset = 1.3f;           // 링(구역 반경)에서 안쪽으로 (m)
    public Vector3 beaconRotation = new Vector3(90f, 0f, 0f); // 벽등(-Z가 빛나는 면)을 위로 향하게 눕힘
    public float consoleDistance = 6.2f;       // 가운데에서 거리 (착륙장 바깥, 위쪽 = +Z)
    public float consoleYawOffset = 0f;        // 콘솔 정면이 가운데를 보도록 보정 (도)
    public float ventDistance = 7f;
    public float overlapPadding = 0.2f;

    private SphereCollider trigger;
    private ExtractionZone zone;
    private Transform root;
    private Vector3 builtPosition = new Vector3(float.MaxValue, 0f, 0f);
    private int wallMask;

    private void Awake()
    {
        trigger = GetComponent<SphereCollider>();
        zone = GetComponent<ExtractionZone>();
        wallMask = LayerMask.GetMask("Walls");
    }

    private void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
    }

    private void LateUpdate()
    {
        // 튜토리얼에서 숨긴 동안 꾸밈도 끔
        bool hidden = zone != null && zone.IsHidden;
        if (root != null && root.gameObject.activeSelf == hidden) root.gameObject.SetActive(!hidden);
        if (hidden) return;

        Vector3 center = transform.position;
        center.y = 0f;
        if ((center - builtPosition).sqrMagnitude < 0.25f) return;
        builtPosition = center;
        Build(center);
    }

    private void Build(Vector3 center)
    {
        if (root != null) Destroy(root.gameObject);
        root = new GameObject("ExtractionZoneDecor").transform;
        root.position = center;
        Physics.SyncTransforms(); // 방금 재생성된 맵 장애물 위치 반영

        float radius = trigger != null ? trigger.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z) : 10f;

        // 착륙장 바닥 (겹침 검사 없음 — 장애물이 위에 올라가도 자연스러움)
        if (padPrefab != null) Place(padPrefab, center + Vector3.up * padHeight, Quaternion.identity, false);

        // 표지등: 원 위에 고르게
        if (beaconPrefab != null && beaconCount > 0)
        {
            float r = Mathf.Max(1f, radius - beaconInset);
            for (int i = 0; i < beaconCount; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / beaconCount;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Quaternion rot = Quaternion.LookRotation(-dir, Vector3.up) * Quaternion.Euler(beaconRotation);
                Place(beaconPrefab, center + dir * r, rot, true);
            }
        }

        // 콘솔: 위쪽(+Z)부터 90도씩 돌며 놓을 수 있는 자리 하나
        if (consolePrefab != null)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, 90f * i, 0f) * Vector3.forward;
                Quaternion rot = Quaternion.LookRotation(-dir, Vector3.up) * Quaternion.Euler(0f, consoleYawOffset, 0f);
                if (Place(consolePrefab, center + dir * consoleDistance, rot, true) != null) break;
            }
        }

        // 바닥 환풍구: 대각선 두 곳
        if (ventPrefab != null)
        {
            float va = Random.Range(0, 4) * 90f + 45f;
            for (int i = 0; i < 2; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, va + 180f * i, 0f) * Vector3.forward;
                Place(ventPrefab, center + dir * ventDistance + Vector3.up * 0.01f, Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f), true);
            }
        }
    }

    // 소품 생성 (콜라이더 끔), checkOverlap이면 장애물과 겹칠 때 건너뜀
    private GameObject Place(GameObject prefab, Vector3 position, Quaternion rotation, bool checkOverlap)
    {
        GameObject obj = Instantiate(prefab, position, rotation, root);
        foreach (Collider c in obj.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (Light l in obj.GetComponentsInChildren<Light>(true)) l.shadows = LightShadows.None;
        if (!TryGetBounds(obj, out Bounds bounds)) return obj;

        // 기준점이 모서리인 프리팹(착륙장 바닥 등)도 겉모습 가운데가 목표 위치에 오도록 수평 보정
        Vector3 shift = position - bounds.center;
        shift.y = 0f;
        obj.transform.position += shift;
        bounds.center += shift;

        if (checkOverlap && wallMask != 0)
        {
            Vector3 half = bounds.extents + Vector3.one * overlapPadding;
            half.y = Mathf.Max(half.y, 0.5f);
            Vector3 c = bounds.center;
            c.y = Mathf.Max(c.y, 0.6f); // 바닥(높이 0 아래)은 검사하지 않게
            if (Physics.CheckBox(c, half, Quaternion.identity, wallMask, QueryTriggerInteraction.Ignore))
            {
                Destroy(obj);
                return null;
            }
        }
        return obj;
    }

    private static readonly List<Renderer> rendererBuffer = new List<Renderer>();

    private static bool TryGetBounds(GameObject obj, out Bounds bounds)
    {
        bounds = default;
        obj.GetComponentsInChildren(rendererBuffer);
        bool has = false;
        foreach (Renderer r in rendererBuffer)
        {
            if (!has) { bounds = r.bounds; has = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return has;
    }
}
