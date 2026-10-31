using System.Collections;
using UnityEngine;

// 메인 화면 3D 배경 — 디오라마 (사용자 2026-10-31: 탑다운 패닝(MainMenuBackdrop)과 비교용 다른 스타일, 둘 중 하나만 켬)
//   지하 시설 방 한 칸: 천장등 아래 군인이 손전등으로 어둠 속 괴물들을 비추는 장면 (배치는 도구가 만들고 씬에서 직접 옮겨도 됨)
//   카메라는 cameraPoint에서 lookTarget을 보며 아주 천천히 흔들림, 손전등 미세한 떨림, 뒤쪽 붉은 비상등 맥박
//   천장등·빛줄기·먼지·분위기 색감은 전투와 같은 AtmosphereLighting 사용
//   씬 배치: Tools > TD Shoot > Build Main Menu Diorama / 전환: Main Menu Backdrop - Use Arena / Use Diorama
public class MainMenuDiorama : MonoBehaviour
{
    [Header("카메라")]
    [SerializeField] private Transform cameraPoint;
    [SerializeField] private Transform lookTarget;
    [SerializeField] private float fieldOfView = 38f;
    [Tooltip("좌우 흔들림 폭(m)과 한 번 왕복 시간(초)")]
    [SerializeField] private float driftAmplitude = 0.35f;
    [SerializeField] private float driftPeriod = 16f;

    [Header("조명")]
    [Tooltip("천장등 바닥 지점 (AtmosphereLighting이 위에 천장등·빛줄기·먼지 생성)")]
    [SerializeField] private Transform[] lampPoints;
    [Tooltip("이 번호의 천장등만 고장 난 듯 깜빡임 (-1 = 없음)")]
    [SerializeField] private int flickerLampIndex = 1;
    [SerializeField] private Light flashlight;
    [SerializeField] private float flashlightSway = 1.2f;
    [SerializeField] private Light alarmLight;
    [SerializeField] private float alarmPulsePerSecond = 0.5f;

    [Header("메뉴 쪽 그라데이션")]
    [Range(0f, 1f)] [SerializeField] private float shadeAlpha = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float shadeSolidUntil = 0.2f;
    [Range(0f, 1f)] [SerializeField] private float shadeFadeUntil = 0.5f;

    private Camera cam;
    private Quaternion flashlightBase;
    private float alarmBase;
    private Texture2D shadeTexture;

    private IEnumerator Start()
    {
        AtmosphereLighting.EnsureCreated();
        GameplayPostProcess.EnsureCreated();

        cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = false;
            cam.fieldOfView = fieldOfView;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 80f;
        }
        if (flashlight != null) flashlightBase = flashlight.transform.localRotation;
        if (alarmLight != null) alarmBase = alarmLight.intensity;
        shadeTexture = MainMenuBackdrop.CreateShadeOverlay(transform, shadeAlpha, shadeSolidUntil, shadeFadeUntil, 0f);
        UpdateCamera();

        // AtmosphereLighting.Start(분위기 Volume 등)가 먼저 돈 뒤 천장등 추가
        yield return null;
        AtmosphereLighting atmosphere = AtmosphereLighting.Instance;
        if (atmosphere != null && lampPoints != null)
        {
            for (int i = 0; i < lampPoints.Length; i++)
            {
                if (lampPoints[i] != null) atmosphere.AddLamp(lampPoints[i].position, i == flickerLampIndex);
            }
        }
    }

    private void OnDestroy()
    {
        if (shadeTexture != null) Destroy(shadeTexture);
    }

    // 메뉴 화면이라 실제 시간 기준
    private void LateUpdate()
    {
        float t = Time.unscaledTime;
        UpdateCamera();

        if (flashlight != null)
        {
            float yaw = (Mathf.PerlinNoise(t * 0.35f, 0.1f) - 0.5f) * 2f * flashlightSway;
            float pitch = (Mathf.PerlinNoise(0.7f, t * 0.3f) - 0.5f) * 2f * flashlightSway * 0.6f;
            flashlight.transform.localRotation = flashlightBase * Quaternion.Euler(pitch, yaw, 0f);
        }
        if (alarmLight != null)
        {
            float s = Mathf.Sin(t * alarmPulsePerSecond * Mathf.PI * 2f) * 0.5f + 0.5f;
            alarmLight.intensity = alarmBase * (0.2f + 0.8f * s * s);
        }
    }

    private void UpdateCamera()
    {
        if (cam == null || cameraPoint == null) return;
        float phase = Time.unscaledTime / Mathf.Max(1f, driftPeriod) * Mathf.PI * 2f;
        Vector3 offset = cameraPoint.right * (Mathf.Sin(phase) * driftAmplitude)
                       + Vector3.up * (Mathf.Sin(phase * 0.5f + 1f) * driftAmplitude * 0.3f);
        cam.transform.position = cameraPoint.position + offset;
        Vector3 target = lookTarget != null ? lookTarget.position : cameraPoint.position + cameraPoint.forward;
        cam.transform.rotation = Quaternion.LookRotation(target - cam.transform.position, Vector3.up);
    }
}
