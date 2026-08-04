using System.Collections;
using System.Collections.Generic;
using JU;
using UnityEngine;

namespace JUTPS.ArmorSystem
{
    [AddComponentMenu("JU TPS/Armor System/Damageable Body Part")]
    public class DamageableBodyPart : MonoBehaviour
    {
        public float DamageMultiplier = 1;
        public Armor ArmorProtecting;

        public IHealth Health { get; private set; }

        private void Start()
        {
            Health = GetComponentInParent<IHealth>();
        }
        public float DoDamage(IHealth.DamageInfo damageInfo)
        {
            if (Health == null)
            {
                Debug.LogWarning("Could not do damage as the Health variable is null");
                return 0;
            }

            damageInfo.Damage *= DamageMultiplier;

            Health.DoDamage(damageInfo);
            if (ArmorProtecting != null && ArmorProtecting.enabled)
            {
                ArmorProtecting.DoDamageOnArmor(damageInfo.Damage);
            }

            return damageInfo.Damage;
        }
    }

}