using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 환경설정 패널 (메인 화면): 에디터에서 배치한 드롭다운·슬라이더를 GameSettings에 연결만 함
//   해상도: 모니터 지원 목록, 고르면 즉시 적용·저장 / 음량: 전체·효과음·배경음 0~100%, 움직이면 즉시 적용·저장
//   패널이 열릴 때(OnEnable) 저장된 값으로 갱신. 컨트롤은 Tools > TD Shoot > Add Settings Controls To Main Menu로 생성 가능
public class SettingsPanelUI : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private TextMeshProUGUI masterValueText;
    [SerializeField] private TextMeshProUGUI sfxValueText;
    [SerializeField] private TextMeshProUGUI musicValueText;

    private List<Vector2Int> resolutions = new List<Vector2Int>();
    private bool bound;

    private void OnEnable()
    {
        Bind();
        Refresh();
    }

    private void Bind()
    {
        if (bound) return;
        bound = true;

        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        if (masterSlider != null) masterSlider.onValueChanged.AddListener(v => { GameSettings.MasterVolume = v; SetPercent(masterValueText, v); });
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(v => { GameSettings.SfxVolume = v; SetPercent(sfxValueText, v); });
        if (musicSlider != null) musicSlider.onValueChanged.AddListener(v => { GameSettings.MusicVolume = v; SetPercent(musicValueText, v); });
    }

    // 저장된 값으로 표시 (값 변경 이벤트 없이)
    public void Refresh()
    {
        if (resolutionDropdown != null)
        {
            resolutions = GameSettings.GetAvailableResolutions();
            Vector2Int current = GameSettings.Resolution;
            if (!resolutions.Contains(current)) resolutions.Add(current); // 목록에 없는 창 크기(에디터 등)도 표시

            List<string> options = new List<string>();
            foreach (Vector2Int r in resolutions) options.Add($"{r.x} x {r.y}");
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(options);
            resolutionDropdown.SetValueWithoutNotify(resolutions.IndexOf(current));
            resolutionDropdown.RefreshShownValue();
        }

        SetSlider(masterSlider, masterValueText, GameSettings.MasterVolume);
        SetSlider(sfxSlider, sfxValueText, GameSettings.SfxVolume);
        SetSlider(musicSlider, musicValueText, GameSettings.MusicVolume);
    }

    private void OnResolutionChanged(int index)
    {
        if (index < 0 || index >= resolutions.Count) return;
        GameSettings.SetResolution(resolutions[index]);
    }

    private static void SetSlider(Slider slider, TextMeshProUGUI valueText, float value)
    {
        if (slider == null) return;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(value);
        SetPercent(valueText, value);
    }

    private static void SetPercent(TextMeshProUGUI text, float value)
    {
        if (text != null) text.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }
}
