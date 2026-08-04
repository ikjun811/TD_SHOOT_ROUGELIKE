using UnityEngine;
using UnityEngine.Events;

namespace JU
{
    /// <summary>
    /// Interface for health systems.
    /// </summary>
    public interface IHealth : IIsDead
    {
        /// <summary>
        /// Stores information about damage to be applied to a <see cref="IHealth"/>.
        /// </summary>
        [System.Serializable]
        public struct DamageInfo
        {
            /// <summary>
            /// The damage count.
            /// </summary>
            public float Damage;

            /// <summary>
            /// The damage hit position on this health gameObject or the owner of this health system.
            /// </summary>
            public Vector3 HitPosition;

            /// <summary>
            /// The direction that the damage came from.
            /// </summary>
            public Vector3 HitDirection;

            /// <summary>
            /// The initial damage position (The position of the shot, as example for weapons or the other character that attacked this AI).
            /// </summary>
            public Vector3 HitOriginPosition;

            /// <summary>
            /// The damage origem  object, like other character that attacked this object.
            /// </summary>
            public GameObject DamageOwner;
        }

        /// <summary>
        /// Stores information about the result of a damage application.
        /// </summary>
        [System.Serializable]
        public struct DamageResultInfo
        {
            /// <summary>
            /// The suggestive damage amount (The damage amount that was intended to be applied).
            /// </summary>
            public float SuggestiveDamage;

            /// <summary>
            /// The real damage amount applied to the health.
            /// </summary>
            public float RealDamage;

            /// <summary>
            /// Was the damage fatal (killed the object).
            /// </summary>
            public bool WasKilled;

            /// <summary>
            /// The damage hit position on this health gameObject or the owner of this health system.
            /// </summary>
            public Vector3 HitPosition;

            /// <summary>
            /// The direction that the damage came from.
            /// </summary>
            public Vector3 HitDirection;

            /// <summary>
            /// The initial damage position (The position of the shot, as example for weapons or the other character that attacked this AI).
            /// </summary>
            public Vector3 HitOriginPosition;

            /// <summary>
            /// The damage origem  object, like other character that attacked this object.
            /// </summary>
            public GameObject DamageOwner;

            /// <summary>
            /// The health system that was damaged.
            /// </summary>
            public IHealth DamagedObject;

            /// <summary>
            /// Was the damage blocked (no damage applied).
            /// </summary>
            public bool DamageBlocked
            {
                get => RealDamage <= 0;
            }
        }

        /// <summary>
        /// Stores information about added health.
        /// </summary>
        [System.Serializable]
        public struct AddedHealthInfo
        {
            /// <summary>
            /// The health added amount.
            /// </summary>
            public float HealthAdded;

            /// <summary>
            /// The source object that is giving health, if have.
            /// </summary>
            public Object Source;
        }

        /// <summary>
        /// <!----> A delegate to be called before damage is applied.
        /// </summary>
        /// <param name="damageInfo"></param>
        /// <returns></returns>
        public delegate DamageInfo BeforeDamagedAction(DamageInfo damageInfo);

#if UNITY_6000_3_OR_NEWER
        /// <summary>
        /// Called if any <see cref="IHealth"/> be damaged.
        /// </summary>
        public static UnityAction<DamageResultInfo> OnAnyDamaged;
#endif

        /// <summary>
        /// Called when this health system takes damage.
        /// </summary>
        public event UnityAction<DamageResultInfo> OnDamaged;

        /// <summary>
        /// Called when health is added.
        /// </summary>
        public event UnityAction<AddedHealthInfo> OnHealthAdded;

        /// <summary>
        /// Called if health be reset to max health.
        /// </summary>
        public event UnityAction OnResetHealth;

        /// <summary>
        /// Damage reduction amount.
        /// </summary>
        public float Protection { get; set; }

        /// <summary>
        /// The health value.
        /// </summary>
        public float Health { get; }

        /// <summary>
        /// The maximum health value.
        /// </summary>
        public float MaxHealth { get; }

        /// <summary>
        /// The normalized health value (0 to 1).
        /// </summary>
        public float NormalizedHealth { get; }

        /// <summary>
        /// If true, this health can't be damaged.
        /// </summary>
        public bool IsInvencible { get; set; }

        /// <summary>
        /// Add damage to this object.
        /// </summary>
        /// <param name="info">Damage information.</param>
        /// <returns>Return information about the damage.</returns>
        public DamageResultInfo DoDamage(DamageInfo info);

        /// <summary>
        /// Add damage to this object.
        /// </summary>
        /// <param name="damage">Damage amount.</param>
        /// <returns>Return information about the damage.</returns>
        public DamageResultInfo DoDamage(float damage);

        /// <summary>
        /// Add health to this object.
        /// </summary>
        /// <param name="healthToAdd">The health to add.</param>
        /// <param name="source">The object that is giving health, if have.</param>
        public void AddHealth(AddedHealthInfo info);

        /// <summary>
        /// Set the current health value.
        /// </summary>
        /// <param name="health"></param>
        public void SetHealth(float health);

        /// <summary>
        /// Set the maximum health value.
        /// </summary>
        /// <param name="maxHealth"></param>
        public void SetMaxHealth(float maxHealth);

        /// <summary>
        /// Resets the health to the maximum health value.
        /// This will revive the object if it was dead.
        /// </summary>
        public void ResetHealth();

        /// <summary>
        /// Subscribe to be called before damage is applied.
        /// </summary>
        /// <param name="callback"></param>
        public void SubscribeOnBeforeDamaged(BeforeDamagedAction callback);

        /// <summary>
        /// Unsubscribe to stop being called before damage is applied.
        /// </summary>
        /// <param name="callback"></param>
        public void UnsubscribeOnBeforeDamaged(BeforeDamagedAction callback);
    }
}