using System.Collections;
using System.Collections.Generic;
using JU;
using UnityEngine;

namespace JUTPS.Utilities
{
    [AddComponentMenu("JU TPS/Utilities/Damage Trigger")]
    public class JUDamageTrigger : MonoBehaviour
    {
        [SerializeField] private float Damage = 5;
        [SerializeField] private string CharacterTag;

        private void OnTriggerEnter(Collider other)
        {
            if (CharacterTag != "")
            {
                if (other.gameObject.CompareTag(CharacterTag))
                {
                    if(other.TryGetComponent(out IHealth health))
                    {
                        health.DoDamage(Damage);
                    }
                }
            }
            else
            {
                if (other.TryGetComponent(out IHealth health))
                {
                    health.DoDamage(Damage);
                }
            }
        }
    }
}