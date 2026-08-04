using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using JU;

namespace JUTPS.CameraSystems.AimAssistent
{
    /// <summary>
    /// Useful for objects that must be ignored by a <see cref="JUCameraAimAssistent"/>.
    /// </summary>
    [AddComponentMenu("JU TPS/Cameras/Aim Assistant System/JU Ignore Aim Assistent")]
    [DisallowMultipleComponent]
    public class JUIgnoreAimAssist : MonoBehaviour
    {
        private readonly static HashSet<GameObject> _toIgnore = new HashSet<GameObject>();

        private HashSet<JUIgnoreAimAssist> _childs;

        [Tooltip("If true, this object will be ignored by Aim Assistent only after die.\nThis option requires a component that inherit from " + nameof(IIsDead))]
        [SerializeField] private bool _onlyOnDeath;

        public JUIgnoreAimAssist()
        {
            _childs = new HashSet<JUIgnoreAimAssist>();
        }

        private void Awake()
        {
            if (_onlyOnDeath)
            {
                enabled = false;

                IIsDead death = GetComponent<IIsDead>();
                Debug.Assert(death != null, $"{nameof(JUIgnoreAimAssist)}:The object {name} is marked to be ignored by Aim Assistent only on Die, but theresn't a {nameof(IIsDead)} added to the object.");

                death.OnDeath += OnDeath;

                IOnResetHealth resetHealth = GetComponent<IOnResetHealth>();
                if (resetHealth != null)
                {
                    resetHealth.OnResetHealth.AddListener(OnRevive);
                }
            }

            RefreshChildrenList();
        }

        private void OnEnable()
        {
            _toIgnore.Add(gameObject);
            SetChildrenEnabled(true);
        }

        private void OnDisable()
        {
            _toIgnore.Remove(gameObject);
            SetChildrenEnabled(false);
        }

        private void OnTransformChildrenChanged()
        {
            RefreshChildrenList();
        }

        private void RefreshChildrenList()
        {
            Profiler.BeginSample(nameof(RefreshChildrenList));

            // Must update children here because this is called
            // if a new object was added or removed from the hierarchy.
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                var ignore = child.GetComponent<JUIgnoreAimAssist>();
                if (!ignore)
                {
                    ignore = child.gameObject.AddComponent<JUIgnoreAimAssist>();
                }

                // Sync the state for all children.
                ignore.enabled = enabled;

                _childs.Add(ignore);
            }

            Profiler.EndSample();
        }

        private void SetChildrenEnabled(bool enabled)
        {
            Profiler.BeginSample(nameof(SetChildrenEnabled));

            foreach (var child in _childs)
            {
                if (!child)
                {
                    continue;
                }

                child.enabled = enabled;
            }

            Profiler.EndSample();
        }

        private void OnDeath()
        {
            if (_onlyOnDeath)
            {
                enabled = true;
            }
        }

        private void OnRevive()
        {
            if (_onlyOnDeath)
            {
                enabled = false;
            }
        }

        /// <summary>
        /// Return true if the object must be ignored by <see cref="JUCameraAimAssistent"/>.
        /// </summary>
        /// <param name="obj">The gameObject to check.</param>
        /// <returns></returns>
        public static bool CheckIfIgnored(GameObject obj)
        {
            return _toIgnore.Contains(obj);
        }
    }
}