using System.Collections.Generic;
using JUTPSEditor.JUHeader;
using UnityEngine;
using UnityEngine.Events;

#if !UNITY_6000_3_OR_NEWER
using JUTPS.FX;
#endif

namespace JU
{
    /// <summary>
    /// Health system for damageable objects.
    /// </summary>
    [AddComponentMenu("JU TPS/Third Person System/Additionals/JU Health")]
    [DisallowMultipleComponent]
    public sealed class JUHealth : MonoBehaviour, IHealth
    {
        /// <summary>
        /// Health regeneration settings.
        /// </summary>
        [System.Serializable]
        public struct HealthRegenerationSettings
        {
            /// <summary>
            /// Delay to start regenerate health get damage.
            /// </summary>
            [Min(0)] public float Delay;

            /// <summary>
            /// Speed of health regeneration.
            /// </summary>
            [Min(0)] public float Speed;
        }

        [System.Serializable]
        private class DamageEvent : UnityEvent<IHealth.DamageResultInfo> { }

        [System.Serializable]
        private class AddedHealthEvent : UnityEvent<IHealth.AddedHealthInfo> { }

        private double _lastHitTime;

        private HashSet<IHealth.BeforeDamagedAction> _beforeDamageCallbacks;

        [JUHeader("Settings")]
        [SerializeField] private float _protection;
        [SerializeField] private float _health;
        [SerializeField] private float _maxHealth;
        [SerializeField] private bool _isInvencible;
        [SerializeField] private HealthRegenerationSettings _healthRegeneration;

        [JUHeader("Effects")]

#if !UNITY_6000_3_OR_NEWER
        [SerializeField] private bool _bloodScreenEffect = false;
#endif
        [SerializeField] private GameObject _spawnOnHit;
        [SerializeField] private GameObject _spawnOnDamage;
        [SerializeField] private GameObject _spawnOnDie;

        [JUHeader("On Death Event")]
        [SerializeField] private UnityEvent _onDeath;
        [SerializeField] private DamageEvent _onDamage;
        [SerializeField] private AddedHealthEvent _onHealthAdded;
        [SerializeField] private UnityEvent _onResetHealth;

        /// <inheritdoc/>
        public event UnityAction<IHealth.DamageResultInfo> OnDamaged;

        /// <inheritdoc/>
        public event UnityAction OnDeath;

        /// <inheritdoc/>
        public event UnityAction<IHealth.AddedHealthInfo> OnHealthAdded;

        /// <inheritdoc/>
        public event UnityAction OnResetHealth;

        /// <inheritdoc/>
        public bool IsDead { get; private set; }

        /// <summary>
        /// Return true if is regenerating health.
        /// </summary>
        public bool IsRegeneratingHealth { get; private set; }

        /// <inheritdoc/>
        public float Protection
        {
            get => _protection;
            set => _protection = value;
        }

        /// <inheritdoc/>
        public float Health
        {
            get => _health;
        }

        /// <inheritdoc/>
        public float MaxHealth
        {
            get => _maxHealth;
        }

        /// <inheritdoc/>
        public float NormalizedHealth
        {
            get => MaxHealth > 0 ? Health / MaxHealth : 0;
        }

        /// <inheritdoc/>
        public bool IsInvencible
        {
            get => _isInvencible;
            set => _isInvencible = value;
        }

        /// <summary>
        /// Health regeneration Settings.
        /// </summary>
        public HealthRegenerationSettings HealthRegeneration
        {
            get => _healthRegeneration;
            set => _healthRegeneration = value;
        }

        /// <summary>
        /// Create Instance.
        /// </summary>
        public JUHealth()
        {
            _beforeDamageCallbacks = new HashSet<IHealth.BeforeDamagedAction>();
        }

        private void OnValidate()
        {
            CheckHealthState();
        }

        private void Reset()
        {
            _protection = 0;
            _health = 100;
            _maxHealth = 100;
            CheckHealthState();

#if !UNITY_6000_3_OR_NEWER
            _bloodScreenEffect = false;
#endif
        }

        private void Awake()
        {
            OnDeath += _onDeath.Invoke;
            OnDamaged += _onDamage.Invoke;
            OnHealthAdded += _onHealthAdded.Invoke;
            OnResetHealth += _onResetHealth.Invoke;
        }

        private void Start()
        {
            CheckHealthState();
        }

        private void Update()
        {
            IsRegeneratingHealth = !IsDead && (Time.timeAsDouble - _lastHitTime > _healthRegeneration.Delay) && NormalizedHealth < 1f;
            if (IsRegeneratingHealth)
            {
                _health += Mathf.Max(_healthRegeneration.Speed, 0) * Time.deltaTime;
                CheckHealthState();
            }
        }

        private void LimitHealth()
        {
            _health = Mathf.Clamp(Health, 0, MaxHealth);
        }

        /// <inheritdoc/>
        public IHealth.DamageResultInfo DoDamage(float damage)
        {
            return DoDamage(new IHealth.DamageInfo
            {
                Damage = damage
            });
        }

        /// <inheritdoc/>
        public IHealth.DamageResultInfo DoDamage(IHealth.DamageInfo damageInfo)
        {
            if (IsDead)
            {
                return new IHealth.DamageResultInfo()
                {
                    DamagedObject = this,
                    DamageOwner = damageInfo.DamageOwner,
                    HitDirection = damageInfo.HitDirection,
                    HitOriginPosition = damageInfo.HitOriginPosition,
                    HitPosition = damageInfo.HitPosition,
                    RealDamage = 0,
                    SuggestiveDamage = damageInfo.Damage,
                    WasKilled = false
                };
            }

            float suggestiveDamage = damageInfo.Damage;
            damageInfo.Damage = Mathf.Max(0, damageInfo.Damage - Protection);

            // Process on before damage.
            foreach (var onBeforeDamage in _beforeDamageCallbacks)
            {
                if (onBeforeDamage == null)
                {
                    continue;
                }

                var modifiedDamageInfo = onBeforeDamage(damageInfo);

                damageInfo = modifiedDamageInfo;
            }

            damageInfo.Damage = Mathf.Max(damageInfo.Damage, 0);

            IHealth.DamageResultInfo result = new IHealth.DamageResultInfo()
            {
                HitDirection = damageInfo.HitDirection,
                HitPosition = damageInfo.HitPosition,
                HitOriginPosition = damageInfo.HitOriginPosition,
                DamageOwner = damageInfo.DamageOwner,
                SuggestiveDamage = suggestiveDamage,
                DamagedObject = this,
            };

            result.RealDamage = damageInfo.Damage;

            if (IsInvencible)
            {
                result.RealDamage = 0;
            }

            _health -= result.RealDamage;
            CheckHealthState();

#if !UNITY_6000_3_OR_NEWER
            if (_bloodScreenEffect)
            {
                BloodScreen.Show();
            }
#endif
            _lastHitTime = Time.timeAsDouble;
            result.WasKilled = IsDead;

            GameObject spawn = result.DamageBlocked ? _spawnOnHit : _spawnOnDamage;
            if (result.HitPosition != Vector3.zero && spawn)
            {
                GameObject spawned = Instantiate(spawn, result.HitPosition, Quaternion.identity);
                spawned.SetActive(true);
                spawned.hideFlags = HideFlags.HideInHierarchy;
                Destroy(spawned, 10);
            }

            if (IsDead && _spawnOnDie)
            {
                GameObject spawned = Instantiate(_spawnOnDie, result.HitPosition != Vector3.zero ? result.HitPosition : transform.position, Quaternion.identity);
                spawned.SetActive(true);
                spawned.hideFlags = HideFlags.HideInHierarchy;
                Destroy(spawned, 10);
            }

            OnDamaged.Invoke(result);

#if UNITY_6000_3_OR_NEWER
            IHealth.OnAnyDamaged?.Invoke(result);
#endif
            return result;
        }

        private void CheckHealthState()
        {
            _protection = Mathf.Max(_protection, 0);
            LimitHealth();

            if (_health <= 0 && IsDead == false)
            {
                _health = 0;
                IsRegeneratingHealth = false;
                IsDead = true;

                OnDeath?.Invoke();
            }

            if (_health > 0 && IsDead)
            {
                ResetHealth();
            }
        }

        /// <inheritdoc/>
        [ContextMenu("Reset Health")]
        public void ResetHealth()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (_health == MaxHealth)
            {
                return;
            }

            _health = MaxHealth;
            IsDead = false;
            CheckHealthState();

            OnResetHealth?.Invoke();
        }

        /// <inheritdoc/>
        public void SetHealth(float health)
        {
            Assert(!IsDead, $"Can't set health of {name} because it's already death.");

            _health = health;
            CheckHealthState();
        }

        /// <inheritdoc/>
        public void SetMaxHealth(float maxHealth)
        {
            Assert(maxHealth > 1, "The max health can't be less than 1.");

            _maxHealth = maxHealth;
            CheckHealthState();
        }

        /// <inheritdoc/>
        public void AddHealth(IHealth.AddedHealthInfo info)
        {
            Assert(!IsDead, $"Can't add health to {name} because it's already death.");

            _health += info.HealthAdded;
            CheckHealthState();

            OnHealthAdded?.Invoke(info);
        }

        /// <inheritdoc/>
        [ContextMenu("Kill")]
        public void Kill()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (IsDead)
            {
                return;
            }

            _health = 0;
            IsDead = true;
            IsRegeneratingHealth = false;
            OnDeath?.Invoke();
        }

        /// <inheritdoc/>
        public void SubscribeOnBeforeDamaged(IHealth.BeforeDamagedAction callback)
        {
            _beforeDamageCallbacks.Add(callback);
        }

        /// <inheritdoc/>
        public void UnsubscribeOnBeforeDamaged(IHealth.BeforeDamagedAction callback)
        {
            _beforeDamageCallbacks.Remove(callback);
        }

        private void Assert(bool test, string message)
        {
#if UNITY_EDITOR
            Debug.Assert(test, $"{nameof(JUHealth)}: {name} {message}");
#endif
        }
    }
}