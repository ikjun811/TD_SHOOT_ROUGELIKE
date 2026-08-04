using JUTPS.JUInputSystem;
using UnityEngine;

namespace JUTPS.CameraSystems.AimAssistent
{
    /// <summary>
    /// Aim assistent for <see cref="TPSCameraController"/>.
    /// </summary>
    [AddComponentMenu("JU TPS/Cameras/Aim Assistant System/JU Aim Assistent")]
    [RequireComponent(typeof(TPSCameraController))]
    [DisallowMultipleComponent]
    public class JUCameraAimAssistent : MonoBehaviour
    {
        [System.Serializable]
        private struct TargetSettings
        {
            [Tooltip("Targets layer.")]
            public LayerMask Layer;

            [Tooltip("Filter tag, optional. Used to aim only on specific objects.")]
            public string[] Tags;
        }

        [System.Serializable]
        private struct ForceSettings
        {
            [Tooltip("Aim Assistent force.")]
            public float Force;

            [Tooltip("Force multiplier when the player is on aiming mode.")]
            public float AimingForceMultplier;
        }

        private float _outOfViewTimer;
        private float _preserveTargetToleranceTimer;
        private float _useLookInput;

        private double _lastCheckPhysicsTime;
        private TPSCameraController _cameraController;
        private Vector2 _smoothLookInput;

        [Tooltip("Max allowed distance of aim assistent.")]
        [SerializeField] private float _maxDistance;

        [Tooltip("Max allowed distance of aim assistent when player is on aiming mode.")]
        [SerializeField] private float _maxDistanceAimMode;

        [Tooltip("Aim Assistent settings for mouse.")]
        [SerializeField] private ForceSettings _mouseSettings;

        [Tooltip("Aim Assistent settings for gamepad.")]
        [SerializeField] private ForceSettings _gamepadSettings;

        [Space]
        [Tooltip("Max allowed time to follow the current target when out of view.")]
        [SerializeField] private float _outOfViewTolerance;

        [Space]
        [Tooltip("Obstacles layer")]
        [SerializeField] private LayerMask _obstaclesLayer;

        [Header("Targets to aim.")]
        [SerializeField] private TargetSettings[] _targets;

        /// <summary>
        /// Current following target. 
        /// </summary>
        public Collider CurrentObjectToAim { get; private set; }

        /// <summary>
        /// The camera controller.
        /// </summary>
        public TPSCameraController CameraController
        {
            get
            {
                FindCameraController();
                return _cameraController;
            }
        }

        private void Reset()
        {
            _maxDistance = 100;
            _maxDistanceAimMode = 300;
            _outOfViewTolerance = 1f;

            _mouseSettings = new ForceSettings
            {
                AimingForceMultplier = 1f,
                Force = 0.1f,
            };

            _gamepadSettings = new ForceSettings
            {
                AimingForceMultplier = 1f,
                Force = 0.2f,
            };

            LayerMask[] obstaclesLayer = new LayerMask[]
            {
                LayerMask.NameToLayer("Default"),
                LayerMask.NameToLayer("Wall"),
                LayerMask.NameToLayer("Walls"),
                LayerMask.NameToLayer("wall"),
                LayerMask.NameToLayer("walls"),
                LayerMask.NameToLayer("Obstacle"),
                LayerMask.NameToLayer("Obstacles"),
                LayerMask.NameToLayer("obstacle"),
                LayerMask.NameToLayer("obstacles")
            };

            for (int i = 0; i < obstaclesLayer.Length; i++)
            {
                if (obstaclesLayer[i] != -1)
                    _obstaclesLayer |= 1 << obstaclesLayer[i];
            }

            _targets = new TargetSettings[2];

            LayerMask[] bodyPartLayers = new LayerMask[]
            {
                LayerMask.NameToLayer("Bone"),
                LayerMask.NameToLayer("Bones"),
                LayerMask.NameToLayer("bone"),
                LayerMask.NameToLayer("bones"),
            };

            LayerMask[] vehiclesLayer = new LayerMask[] {
                LayerMask.NameToLayer("VehicleMeshCollider"),
                LayerMask.NameToLayer("Vehicles"),
                LayerMask.NameToLayer("vehicles"),
                LayerMask.NameToLayer("Vehicle"),
                LayerMask.NameToLayer("vehicle"),
            };

            for (int i = 0; i < bodyPartLayers.Length; i++)
            {
                if (bodyPartLayers[i] != -1)
                    _targets[0].Layer |= 1 << bodyPartLayers[i];
            }

            for (int i = 0; i < vehiclesLayer.Length; i++)
            {
                if (vehiclesLayer[i] != -1)
                    _targets[1].Layer |= 1 << vehiclesLayer[i];
            }

            _targets[1].Tags = new string[]
            {
                "Vehicle"
            };
        }

        private void OnValidate()
        {
            _maxDistance = Mathf.Max(_maxDistance, 10f);
            _maxDistanceAimMode = Mathf.Max(_maxDistanceAimMode, 10);

            _outOfViewTolerance = Mathf.Clamp(_outOfViewTolerance, 0, 5f);
            _gamepadSettings.Force = Mathf.Clamp(_gamepadSettings.Force, 0, 5f);
            _gamepadSettings.AimingForceMultplier = Mathf.Clamp(_gamepadSettings.AimingForceMultplier, 0, 10f);
            _mouseSettings.Force = Mathf.Clamp(_mouseSettings.Force, 0, 5f);
            _mouseSettings.AimingForceMultplier = Mathf.Clamp(_mouseSettings.AimingForceMultplier, 0, 10f);
        }

        private void Start()
        {
            FindCameraController();
        }

        private void FixedUpdate()
        {
            float deltaTime = Time.fixedDeltaTime;
            float smoothInput = Mathf.Clamp01(deltaTime * 4);
            Vector2 lookInput = new Vector2(CameraController.SmoothedMouseX, CameraController.SmoothedMouseY);

            if (Mathf.Abs(lookInput.x) > Mathf.Abs(_smoothLookInput.x))
            {
                _smoothLookInput.x = lookInput.x;
            }
            else
            {
                _smoothLookInput.x = Mathf.Lerp(_smoothLookInput.x, lookInput.x, smoothInput);
            }

            if (Mathf.Abs(lookInput.y) > Mathf.Abs(_smoothLookInput.y))
            {
                _smoothLookInput.y = lookInput.y;
            }
            else
            {
                _smoothLookInput.y = Mathf.Lerp(_smoothLookInput.y, lookInput.y, smoothInput);
            }

            if (lookInput.x == 0)
            {
                _smoothLookInput.x = 0;
            }
            if (lookInput.y == 0)
            {
                _smoothLookInput.x = 0;
            }

            Vector3 cameraPos = CameraController.mCamera.transform.position;

            // Aim to target.
            if (CurrentObjectToAim)
            {
                ForceSettings settings = JUInputManager.IsUsingGamepad ? _gamepadSettings : _mouseSettings;
                float force = settings.Force;
                if (CameraController.Aiming)
                {
                    force *= settings.AimingForceMultplier;
                }

                Vector3 targetPos = CurrentObjectToAim.bounds.center;
                Quaternion lookToTarget = Quaternion.LookRotation((targetPos - cameraPos).normalized);
                Vector3 lookToTargetEuler = lookToTarget.eulerAngles;

                Quaternion currentCameraRotation = Quaternion.Euler(CameraController.rotxtarget, CameraController.rotytarget, 0);

                // Force based on distance and angle to have constant rotation speed and be smooth even on high distances (useful for snipers).
                float targetDistance = Vector3.Distance(cameraPos, targetPos);
                float angleToTarget = Vector3.Angle(currentCameraRotation * Vector3.forward, lookToTarget * Vector3.forward);
                float lookSpeed = (targetDistance * CameraController.mCamera.fieldOfView * 0.1f) / angleToTarget;
                lookSpeed *= deltaTime;

                float lookSpeedX = Mathf.Clamp01(lookSpeed);
                float lookSpeedY = Mathf.Clamp01(lookSpeed);

                lookSpeedX = Mathf.Clamp01(lookSpeedX * force);
                lookSpeedY = Mathf.Clamp01(lookSpeedY * force);

                // Continue aiming if is looking.
                // But, if is looking let's smooth the aiming force when close to the center to avoid flickering
                // caused by looking input.
                {
                    float smooth = Mathf.Max(1, force) * 3;

                    // Smooth only if target is closest to aim center.
                    float smoothX = lookSpeedX * Mathf.Clamp01(angleToTarget / smooth);
                    float smoothY = lookSpeedY * Mathf.Clamp01(angleToTarget / smooth);

                    // Smooth only if is looking.
                    lookSpeedX = Mathf.Lerp(lookSpeedX, smoothX, _useLookInput);
                    lookSpeedY = Mathf.Lerp(lookSpeedY, smoothY, _useLookInput);
                }

                float rotX = Mathf.LerpAngle(CameraController.rotxtarget, lookToTargetEuler.x, lookSpeedX);
                float rotY = Mathf.LerpAngle(CameraController.rotytarget, lookToTargetEuler.y, lookSpeedY);
                CameraController.SetCameraRotation(rotX, rotY, SmoothRotate: false);
            }

            _useLookInput -= Time.deltaTime * 4;
            _useLookInput = Mathf.Clamp01(_useLookInput);
            if (lookInput.magnitude > 0.02f)
            {
                _useLookInput = 1f;
            }

            if (CurrentObjectToAim && JUIgnoreAimAssist.CheckIfIgnored(CurrentObjectToAim.gameObject))
            {
                CurrentObjectToAim = null;
                return;
            }

            // Works only on aim/fire mode.
            TPSCameraController.PlayerStates cameraState = CameraController.CurrentState;
            if (cameraState != TPSCameraController.PlayerStates.FireMode && cameraState != TPSCameraController.PlayerStates.Aiming)
            {
                CurrentObjectToAim = null;
                return;
            }

            if (CurrentObjectToAim)
            {
                Vector3 targetPos = CurrentObjectToAim.bounds.center;
                float targetDistance = Vector3.Distance(cameraPos, targetPos);
                if (targetDistance > (CameraController.Aiming ? _maxDistanceAimMode : _maxDistance))
                {
                    CurrentObjectToAim = null;
                }
            }

            // Target isn't visible, we can ignore it after some time.
            if (CurrentObjectToAim && (!IsOnCameraRay(CurrentObjectToAim) || HasObstacle(CurrentObjectToAim)))
            {
                // The player is not looking to this object, we can ignore it.
                if (!IsClosestViewCenter(CurrentObjectToAim))
                {
                    CurrentObjectToAim = null;
                }

                _outOfViewTimer += deltaTime;
                if (_outOfViewTimer > _outOfViewTolerance)
                {
                    CurrentObjectToAim = null;
                }
            }
            else
            {
                _outOfViewTimer = 0;
            }

            // Don't need to update raycasts too often.
            var time = Time.timeAsDouble;
            if (time - _lastCheckPhysicsTime < 0.1f)
            {
                return;
            }

            _lastCheckPhysicsTime = time;

            FindObjectToAim();
        }

        private void FindCameraController()
        {
            if (_cameraController)
            {
                return;
            }

            _cameraController = GetComponent<TPSCameraController>();
        }

        private void FindObjectToAim()
        {
            Transform camera = _cameraController.mCamera.transform;
            Vector3 start = camera.position;
            Vector3 forward = camera.forward;
            float deltaTime = Time.deltaTime;

            foreach (var target in _targets)
            {
                float distance = CameraController.Aiming ? _maxDistanceAimMode : _maxDistance;
                if (Physics.Raycast(start, forward, out RaycastHit hit, distance, target.Layer, QueryTriggerInteraction.Ignore))
                {
                    Collider hitCollider = hit.collider;

                    if (HasObstacle(start, hitCollider.bounds.center))
                    {
                        continue;
                    }

                    // This object can't be aimed.
                    if (JUIgnoreAimAssist.CheckIfIgnored(hitCollider.gameObject))
                    {
                        continue;
                    }

                    // Filter by tag.
                    if (target.Tags.Length > 0)
                    {
                        bool tagFound = false;
                        foreach (var tag in target.Tags)
                        {
                            if (hitCollider.CompareTag(tag))
                            {
                                tagFound = true;
                                break;
                            }
                        }

                        if (!tagFound)
                        {
                            continue;
                        }
                    }

                    if (!hitCollider)
                    {
                        continue;
                    }

                    // Must preserve the current target for some time even if is hitting other collider to have better stability.
                    // Change the current taget only after some time if the player force to it or if the current target is out of the ray.
                    if (CurrentObjectToAim && hitCollider != CurrentObjectToAim)
                    {
                        _preserveTargetToleranceTimer += deltaTime;
                        if (_preserveTargetToleranceTimer < 0.1f)
                        {
                            return;
                        }
                    }
                    else
                    {
                        _preserveTargetToleranceTimer = 0;
                    }

                    CurrentObjectToAim = hitCollider;
                    break;
                }
            }
        }

        private bool HasObstacle(Collider target)
        {
            if (!target)
            {
                return false;
            }

            Vector3 start = CameraController.mCamera.transform.position;
            Vector3 end = target.bounds.center;
            return HasObstacle(start, end);
        }

        private bool HasObstacle(Vector3 start, Vector3 end)
        {
            return Physics.Linecast(start, end, _obstaclesLayer, QueryTriggerInteraction.Ignore);
        }

        private bool IsOnCameraRay(Collider target)
        {
            Vector3 start = CameraController.mCamera.transform.position;
            Vector3 direction = CameraController.mCamera.transform.forward;
            return target.bounds.IntersectRay(new Ray(start, direction));
        }

        private bool IsClosestViewCenter(Collider target)
        {
            Vector3 targetPos = target.bounds.center;
            Vector3 cameraPos = CameraController.mCamera.transform.position;
            Vector3 forward = CameraController.mCamera.transform.forward;
            Vector3 direction = (targetPos - cameraPos).normalized;
            float angleToCenter = Vector3.Angle(direction, forward) * Vector3.Distance(targetPos, cameraPos);
            return angleToCenter < 40;
        }
    }
}