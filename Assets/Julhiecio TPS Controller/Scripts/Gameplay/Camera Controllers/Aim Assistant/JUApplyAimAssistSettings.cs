using JUTPS.GameSettings;
using UnityEngine;

namespace JUTPS.CameraSystems.AimAssistent
{
    /// <summary>
    /// Apply aim assist settings on <see cref="JUCameraAimAssistent"/>.
    /// </summary>
    [RequireComponent(typeof(JUCameraAimAssistent))]
    [DisallowMultipleComponent]
    public class JUApplyAimAssistSettings : MonoBehaviour
    {
        private MonoBehaviour _aimAssist;

        private void Start()
        {
            _aimAssist = GetComponent<JUCameraAimAssistent>();
            Debug.Assert(_aimAssist, $"{nameof(JUApplyAimAssistSettings)}: The object {name} does not have a {nameof(JUCameraAimAssistent)} component to apply settings.");

            _aimAssist.enabled = JUGameSettings.UseAimAssist;
            JUGameSettings.OnChangeSettings += OnChangeSettings;
        }

        private void OnDestroy()
        {
            JUGameSettings.OnChangeSettings -= OnChangeSettings;
        }

        private void OnChangeSettings()
        {
            _aimAssist.enabled = JUGameSettings.UseAimAssist;
        }
    }
}


