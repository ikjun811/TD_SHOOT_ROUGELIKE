using UnityEngine.Events;

namespace JU
{
    /// <summary>
    /// Defines a property to verify if the object is dead, useful for objects that have health.
    /// </summary>
    public interface IIsDead
    {
        /// <summary>
        /// Event called when the object dies.
        /// </summary>
        public event UnityAction OnDeath;

        /// <summary>
        /// Return true if is dead.
        /// </summary>
        bool IsDead { get; }

        /// <summary>
        /// Kills the object.
        /// </summary>
        public void Kill();
    }
}