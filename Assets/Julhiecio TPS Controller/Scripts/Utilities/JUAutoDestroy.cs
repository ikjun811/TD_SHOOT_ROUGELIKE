using JU;
using UnityEngine;
namespace JUTPS.Utilities
{
    /// <summary>
    /// Auto Destroy gameObject.
    /// </summary>
    [AddComponentMenu("JU TPS/Utilities/JU Auto Destroy")]
    public class JUAutoDestroy : MonoBehaviour
    {
        private IHealth health;

        /// <summary>
        /// The time to destroy.
        /// </summary>
        public float SecondsToDestroy;

        /// <summary>
        /// Auto destroy the object after spawn, see <see cref="SecondsToDestroy"/>.
        /// </summary>
        public bool DestroyOnStart;

        /// <summary>
        /// Auto destroy the object after die after <see cref="SecondsToDestroy"/> if have <see cref="IHealth"/> component.
        /// </summary>
        public bool DestroyOnDie;

        /// <summary>
        /// Create component instance.
        /// </summary>
        public JUAutoDestroy()
        {
            DestroyOnStart = true;
            DestroyOnDie = false;
            SecondsToDestroy = 10;
        }

        private void Start()
        {
            health = GetComponent<IHealth>();

            if (health !=null)
            {
                if (health.IsDead)
                    DestroyObject();

                else
                    health.OnDeath += TimedDestroyObject;
            }
            else if (DestroyOnStart)
                TimedDestroyObject();

        }

        /// <summary>
        /// Destroy gameObject after <see cref="SecondsToDestroy"/>.
        /// </summary>
        public void TimedDestroyObject()
        {
            Destroy(gameObject, SecondsToDestroy);
        }

        /// <summary>
        /// Destroy gameObject.
        /// </summary>
        public void DestroyObject()
        {
            Destroy(gameObject);
        }
    }
}