using UnityEngine;
using UnityEngine.SceneManagement;
using JUTPS.CharacterBrain;
using JUTPS.PhysicsScripts;
using System.Collections;

namespace JUTPS
{
    /// <summary>
    /// Can reset player or reload current scene after player dies.
    /// </summary>
    [AddComponentMenu("JU TPS/Scene Management/JU Respawn Controller")]
    public class JURespawnController : MonoBehaviour
    {
        private enum AutoRespawnModes
        {
            RespawnPlayer,
            ReloadScene,
            Disabled,
        }

        private float _respawnTimer;

        private static JURespawnController _instance;

        [SerializeField] private bool _useDebugLogs;

        [Tooltip("Reload game action on player dies.")]
        [SerializeField] private AutoRespawnModes _autoRespawnMode;

        [Tooltip("Time to respawn on player dies. \nWorks only if 'Auto Respawn Mode' is not set as 'Disabled'")]
        public float RespawnDelay;

        /// <summary>
        /// Player respawn position.
        /// </summary>
        public Vector3 RespawnPlayerPostion { get; private set; }

        /// <summary>
        /// Player respawn rotation.
        /// </summary>
        public Quaternion RespawnPlayerRotation { get; private set; }

        /// <summary>
        /// Instance.
        /// </summary>
        public static JURespawnController Instance
        {
            get
            {
                if (!_instance)
                {
                    _instance = FindObjectOfType<JURespawnController>(true);
                }

                return _instance;
            }
        }

        private void Reset()
        {
            _useDebugLogs = false;
            RespawnDelay = 2;
        }

        private void Awake()
        {
            if (_instance && _instance != this)
            {
                throw new System.Exception($"Multiple instances of {typeof(JURespawnController).Name} are not allowed.");
            }

            _instance = this;
        }

        private IEnumerator Start()
        {
            // Wait run some initial logic.
            yield return new WaitForSeconds(0.05f);

            JUCharacterBrain player = JUGameManager.PlayerController;
            RespawnPlayerPostion = player ? player.transform.position : Vector3.zero;
            RespawnPlayerRotation = player ? player.transform.rotation : Quaternion.identity;
        }

        private void Update()
        {
            if (_autoRespawnMode == AutoRespawnModes.Disabled)
            {
                return;
            }

            JUCharacterBrain player = JUGameManager.PlayerController;
            if (!player)
            {
                return;
            }

            if (player.CharacterHealth.IsDead)
            {
                _respawnTimer += Time.deltaTime;
                if (_respawnTimer > RespawnDelay)
                {

                    switch (_autoRespawnMode)
                    {
                        case AutoRespawnModes.RespawnPlayer:
                            RespawnPlayer();
                            break;
                        case AutoRespawnModes.ReloadScene:
                            ResetLevel();
                            break;
                        default:
                            throw new System.Exception("Invalid respawn option.");
                    }
                }
            }
            else
            {
                _respawnTimer = 0;
            }
        }

        /// <summary>
        /// Reload player's scene. Call it if the player is death.
        /// </summary>
        public static void ResetLevel()
        {
            if (!_instance)
            {
                return;
            }

            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>
        /// Respawn player character. Call it if the player is death.
        /// </summary>
        public static void RespawnPlayer()
        {
            if (!_instance)
            {
                return;
            }

            JUCharacterBrain player = JUGameManager.PlayerController;

            if (!player)
            {
                return;
            }

            //Reset Ragdoll
            if (player.TryGetComponent(out AdvancedRagdollController ARC))
            {
                player.anim.GetBoneTransform(HumanBodyBones.Hips).SetParent(ARC.HipsParent);
                ARC.State = AdvancedRagdollController.RagdollState.BlendToAnim;
                ARC.TimeToGetUp = 2;
                ARC.BlendAmount = 0;
                ARC.SetActiveRagdoll(false);
            }

            //Reset Position
            player.transform.position = _instance.RespawnPlayerPostion;
            player.transform.rotation = _instance.RespawnPlayerRotation;

            //Reset Health
            player.CharacterHealth.ResetHealth();

            //Reset layer
            player.gameObject.layer = 9;

            //Reset Collider
            if (player.TryGetComponent<Collider>(out var collider))
            {
                collider.isTrigger = false;
                collider.enabled = true;
            }

            //Reset Rigidbody
            if (player.TryGetComponent<Rigidbody>(out var rigidbody))
            {
                rigidbody.useGravity = true;
                rigidbody.isKinematic = false;
                rigidbody.linearVelocity = Vector3.up * player.GetComponent<Rigidbody>().linearVelocity.y;
                rigidbody.angularVelocity = Vector3.zero;
                rigidbody.constraints = RigidbodyConstraints.None | RigidbodyConstraints.FreezeRotation;
            }

            //Enable Tps Script
            player.enabled = true;
            player.enableMove();

            //Reset Movement
            player.VelocityMultiplier = 0;

            //Reset Animator
            player.anim.enabled = true;
            player.anim.SetBool(player.AnimatorParameters.Dying, false);
            player.anim.Play("Locomotion Blend Tree", 0);
            player.anim.ResetTrigger(player.AnimatorParameters.Roll);
            player.anim.ResetTrigger(player.AnimatorParameters.Punch);
            player.anim.ResetTrigger("Throw");
            player.anim.ResetTrigger(player.AnimatorParameters.ReloadLeftWeapon);
            player.anim.ResetTrigger(player.AnimatorParameters.ReloadRightWeapon);
            player.anim.ResetTrigger(player.AnimatorParameters.PullWeaponSlider);


            player.ResetDefaultLayersWeight();

            if (player.HoldableItemInUseRightHand != null)
            {
                player.SwitchToItem(-1, true);
            }

            if (_instance._useDebugLogs)
            {
                Debug.Log("Player has respawned");
            }
        }
    }
}