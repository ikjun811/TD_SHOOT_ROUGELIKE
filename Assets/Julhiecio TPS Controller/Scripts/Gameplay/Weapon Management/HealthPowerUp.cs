using UnityEngine;
using JUTPS;
using JU;

namespace JUTPS.PowerUps
{

    [RequireComponent(typeof(BoxCollider))]
    [AddComponentMenu("JU TPS/Weapon System/Health Power Up")]
    public class HealthPowerUp : MonoBehaviour
    {
        [Header("Health")]
        public float HealthToAdd = 30;
        public GameObject Effect;

        void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.tag == "Player")
            {
                var health = other.GetComponent<IHealth>();
                if (health != null)
                {
                    if (health.Health == health.MaxHealth) return;

                    health.AddHealth(new IHealth.AddedHealthInfo()
                    {
                        HealthAdded = HealthToAdd,
                        Source = this
                    });

                    GameObject fx = Instantiate(Effect, transform.position, transform.rotation);
                    Destroy(fx, 5);
                    Destroy(this.gameObject, 0.1f);
                }
            }
        }
    }

}
