using Core;
using Gameplay;
using UnityEngine;

/// <summary>
/// Controls the "hook" intro sequence: a model falls from above and when it
/// reaches the player zone, the camera switches from the Hook camera to the
/// Game camera, the falling model is deactivated, and the real character
/// skin is revealed.
///
/// The player character spawns at runtime, so this controller waits until
/// the player is available before hiding its SkinRoot.
/// </summary>
public class HookController : MonoBehaviour
{
    [Header("Hook Model")]
    [Tooltip("The model that falls from the sky during the hook intro.")]
    [SerializeField] private GameObject hookModel;

    [Header("Landing Settings")]
    [Tooltip("How close the hook model's Z needs to be to the landing Z before " +
             "it counts as 'landed'. Increase if your model is large.")]
    [SerializeField] private float arrivalThreshold = 1f;

    [Tooltip("The world-space Z the hook model falls toward. " +
             "If autoDetect is on, this is set to the player's Z at runtime.")]
    [SerializeField] private float landingZ;

    [Tooltip("If true, landingZ is automatically set to the player character's Z position.")]
    [SerializeField] private bool autoDetectLandingZ = true;

    private bool _hasLanded;
    private bool _playerReady;
    public Character _player;

    private void Start()
    {
        // Make sure the hook model is visible
        if (hookModel != null)
        {
            hookModel.SetActive(true);
        }

        // Start with the Hook camera active
        if (CinemachineController.Instance != null)
        {
            CinemachineController.Instance.ChangeCamera(CmCameraType.Hook);
        }
    }

    private void Update()
    {
        if (_hasLanded)
        {
            return;
        }

        // Wait for the player to be spawned at runtime before doing anything
        if (!_playerReady)
        {
            TryFindPlayer();
            return;
        }

        if (hookModel == null)
        {
            return;
        }

        float hookZ = hookModel.transform.position.z;
        float distanceZ = Mathf.Abs(hookZ - landingZ);

        // Check if the falling model has reached the landing Z
        if (distanceZ <= arrivalThreshold)
        {
            Debug.Log($"[HookController] Hook landed! Hook Z = {hookZ}, Landing Z = {landingZ}, Distance = {distanceZ}");
            OnHookLanded();
        }
    }

    private void TryFindPlayer()
    {
        // Try GameManager first
        if (GameManager.Instance != null && GameManager.Instance.Player != null)
        {
            _player = GameManager.Instance.Player;
        }

        // Fallback to CollisionManager
        if (_player == null && CollisionManager.Instance != null)
        {
            CollisionManager.Instance.TryGetPlayer(out _player);
        }

        // Player not spawned yet — wait for next frame
        if (_player == null)
        {
            return;
        }

        // Player is ready — set up landing target
        _playerReady = true;

        if (autoDetectLandingZ)
        {
            landingZ = _player.transform.position.z;
        }

        Debug.Log($"[HookController] Player found: {_player.name}. Landing Z = {landingZ}");
    }

    private void OnHookLanded()
    {
        _hasLanded = true;

        // 1. Deactivate the falling hook model
        hookModel.SetActive(false);

        // 2. Enable the real character skin (spawned at runtime from prefab)
        if (_player != null)
        {
            // Method A: Via the serialized visual reference
            if (_player.Visual != null && _player.Visual.SkinRoot != null)
            {
                _player.Visual.SkinRoot.gameObject.SetActive(true);
                _player.Visual.SkinRoot.localScale = Vector3.one; // Ensure it wasn't scaled to 0
            }

            // Method B (Fallback): Direct name search in case the reference was lost on the prefab
            Transform directSkinRoot = _player.transform.Find("SkinRoot");
            if (directSkinRoot != null)
            {
                directSkinRoot.gameObject.SetActive(true);
                directSkinRoot.localScale = Vector3.one;
            }
        }

        // 3. Switch camera from Hook to Game
        if (CinemachineController.Instance != null)
        {
            CinemachineController.Instance.ChangeCamera(CmCameraType.Game);
        }

        Debug.Log("[HookController] Transition complete: Hook OFF → Skin ON → Game Camera");
    }
}
