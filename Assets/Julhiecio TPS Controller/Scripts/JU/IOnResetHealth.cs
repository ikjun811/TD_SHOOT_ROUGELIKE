using UnityEngine.Events;

namespace JU
{
    /// <summary>
    /// Called if the object reset your life/revive.
    /// Useful for respawn / reset life power ups.
    /// </summary>
    public interface IOnResetHealth
    {
        /// <summary>
        /// Called if the object reset your life/revive.
        /// </summary>
        public UnityEvent OnResetHealth { get; }
    }
}

