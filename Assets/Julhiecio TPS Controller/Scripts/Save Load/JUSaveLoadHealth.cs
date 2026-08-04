using JUTPS;
using UnityEngine;

namespace JU.SaveLoad
{
    /// <summary>
    /// Load and save data for <see cref="IHealth"/>.
    /// </summary>
    [RequireComponent(typeof(IHealth))]
    [AddComponentMenu("JU TPS/Save Load/JU Save Load Health")]
    public class JUSaveLoadHealth : JUSaveLoadComponent
    {
        private IHealth _health;

        private const string VALUE_KEY = "Health";
        private const string MAX_VALUE_KEY = "Max Health";

        /// <inheritdoc/>
        public JUSaveLoadHealth() : base()
        {
        }

        /// <inheritdoc/>
        protected override void Awake()
        {
            _health = GetComponent<IHealth>();

            base.Awake();
        }

        /// <inheritdoc/>
        public override void Save()
        {
            base.Save();

            SetValue(VALUE_KEY, _health.Health);
            SetValue(MAX_VALUE_KEY, _health.MaxHealth);
        }

        /// <inheritdoc/>
        public override void Load()
        {
            base.Load();

            _health.SetMaxHealth(GetValue(MAX_VALUE_KEY, _health.MaxHealth));
            _health.SetHealth(GetValue(VALUE_KEY, _health.MaxHealth));
        }

        /// <inheritdoc/>
        protected override void OnExitPlayMode()
        {
            base.OnExitPlayMode();

            _health = null;
        }
    }
}