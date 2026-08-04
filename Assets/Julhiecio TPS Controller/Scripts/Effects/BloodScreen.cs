using JU;
using UnityEngine;
using UnityEngine.UI;

namespace JUTPS.FX
{
    [AddComponentMenu("JU TPS/FX/Blood Screen")]
    [RequireComponent(typeof(Image))]
    public class BloodScreen : MonoBehaviour
    {
        private static BloodScreen _instance;

        public static BloodScreen Instance;
        private IHealth _playerHealth;
        private Image img;
        private float healthvalue;
        private Color currentColor;

        private void Start()
        {
            Debug.Assert(_instance == null || _instance == this, $"{nameof(BloodScreen)}: ({name}) Multiple instances are not allowed.");

            var player = GameObject.FindGameObjectWithTag("Player");
            _playerHealth = JUGameManager.PlayerController.GetComponent<IHealth>();
            img = GetComponent<Image>();
            Instance = this;

#if UNITY_6000_3_OR_NEWER
            IHealth.OnAnyDamaged += OnHealthDamaged;
#endif
        }

        private void OnDestroy()
        {
#if UNITY_6000_3_OR_NEWER
            IHealth.OnAnyDamaged -= OnHealthDamaged;
#endif
        }

        private void Update()
        {
            if (_playerHealth == null)
            {
                return;
            }

            healthvalue = Mathf.Lerp(healthvalue, _playerHealth.NormalizedHealth, 15 * Time.deltaTime);
            currentColor = Color.Lerp(Color.white, Color.clear, healthvalue);
            img.color = Color.Lerp(img.color, currentColor, 5 * Time.deltaTime);
        }

        private void PlayerHasHited()
        {
            img.color = Color.white;
        }

        public static void Show()
        {
            if (Instance == null)
            {
                return;
            }

            Instance.PlayerHasHited();
        }

#if UNITY_6000_3_OR_NEWER
        private void OnHealthDamaged(IHealth.DamageResultInfo info)
        {
            if (info.DamagedObject is MonoBehaviour script && script.gameObject == JUGameManager.PlayerController.gameObject)
            {
                _playerHealth = info.DamagedObject;
                PlayerHasHited();
            }
        }
#endif
    }
}