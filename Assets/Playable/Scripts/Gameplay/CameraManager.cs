using Unity.Cinemachine;
using Core;
using UnityEngine;
using System.Collections;
using DG.Tweening;

public class CameraManager : SingletonBehaviour<CameraManager>
{
    [Header("Cinemachine")]
    [SerializeField] private CinemachineCamera gameCamera;

    [Header("Mini Map")]
    [SerializeField] private Camera miniMap;
    [SerializeField] private float miniMapOrthoSize = 30;

    // Minimap display mode tracking
    private MinimapDisplayMode currentMinimapMode = MinimapDisplayMode.All;
    private int originalMinimapCullingMask;
    private bool cullingMaskCaptured = false;

    private void Awake()
    {
        CaptureOriginalCullingMask();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Z))
        {
            TriggerZoom();
        }
    }

    void Start()
    {
        // Follow Offset transition
        // ChangeFollowOffsetY(200f, 60f, 0.5f);

        ChangeFOVOverTime(60, 75, 80);
    }

    private void CaptureOriginalCullingMask()
    {
        if (!cullingMaskCaptured && miniMap != null)
        {
            originalMinimapCullingMask = miniMap.cullingMask;
            cullingMaskCaptured = true;
        }
    }

    /// <summary>
    /// Apply camera settings from spawn pattern
    /// </summary>
    public void ApplyPatternSettings(float cameraOrthoSize, float cameraMaxOrthoSize, float mapScale)
    {
        // Apply game camera ortho size
        if (cameraOrthoSize > 0f && gameCamera != null)
        {
            gameCamera.Lens.OrthographicSize = cameraOrthoSize;
        }

        // Apply base and max ortho size to CinemachineController
        if (CinemachineController.Instance != null)
        {
            if (cameraOrthoSize > 0f)
            {
                CinemachineController.Instance.SetBaseOrthoSize(cameraOrthoSize);
            }
            if (cameraMaxOrthoSize > 0f)
            {
                CinemachineController.Instance.SetMaxOrthoSize(cameraMaxOrthoSize);
            }
        }

        // Apply minimap ortho size (scaled by mapScale)
        if (miniMap != null)
        {
            miniMap.orthographicSize = miniMapOrthoSize * mapScale;
        }
    }

    /// <summary>
    /// Apply minimap display mode from spawn pattern
    /// </summary>
    public void ApplyMinimapDisplayMode(MinimapDisplayMode mode)
    {
        SetMinimapDisplayMode(mode);
    }

    /// <summary>
    /// Set minimap display mode at runtime
    /// </summary>
    public void SetMinimapDisplayMode(MinimapDisplayMode mode)
    {
        if (miniMap == null)
        {
            Debug.LogWarning("[CameraManager] miniMap camera is not assigned!");
            return;
        }

        // Ensure we have captured the original culling mask
        CaptureOriginalCullingMask();

        currentMinimapMode = mode;

        if (mode == MinimapDisplayMode.Player)
        {
            // Hide enemy territory layer from minimap
            int enemyLayer = LayerMask.NameToLayer("EnemyTerritory");
            if (enemyLayer < 0)
            {
                Debug.LogWarning("[CameraManager] Layer 'EnemyTerritory' not found! Create it in Tags and Layers.");
                return;
            }
            miniMap.cullingMask = originalMinimapCullingMask & ~(1 << enemyLayer);
            Debug.Log($"[CameraManager] Minimap set to Player mode. Culling mask: {miniMap.cullingMask}");
        }
        else
        {
            // Show all - restore original culling mask
            miniMap.cullingMask = originalMinimapCullingMask;
            Debug.Log($"[CameraManager] Minimap set to All mode. Culling mask: {miniMap.cullingMask}");
        }
    }

    /// <summary>
    /// Get current minimap display mode
    /// </summary>
    public MinimapDisplayMode GetMinimapDisplayMode()
    {
        return currentMinimapMode;
    }

    public void ChangeFollowOffsetY(float startY, float endY, float duration)
    {
        if (gameCamera == null)
        {
            Debug.LogWarning("[CameraManager] GameCamera not assigned!");
            return;
        }

        var follow = gameCamera.GetComponent<CinemachineFollow>();

        if (follow == null)
        {
            Debug.LogWarning("[CameraManager] CinemachineFollow not found!");
            return;
        }

        StartCoroutine(FollowOffsetTransition(follow, startY, endY, duration));
    }

    private IEnumerator FollowOffsetTransition(CinemachineFollow follow, float startY, float endY, float duration)
    {
        float timer = 0f;

        Vector3 offset = follow.FollowOffset;
        offset.y = startY;
        follow.FollowOffset = offset;

        while (timer < duration)
        {

            timer += Time.deltaTime;
            float t = timer / duration;

            offset.y = Mathf.Lerp(startY, endY, t);
            follow.FollowOffset = offset;

            yield return null;
        }

        offset.y = endY;
        follow.FollowOffset = offset;
    }

    public void ChangeFOVOverTime(float startFOV = 40f, float endFOV = 50f, float duration = 60f)
    {
        if (gameCamera == null)
        {
            Debug.LogWarning("[CameraManager] GameCamera not assigned!");
            return;
        }

        // StopAllCoroutines();
        StartCoroutine(FOVTransition(startFOV, endFOV, duration));
    }

    private IEnumerator FOVTransition(float startFOV, float endFOV, float duration)
    {
        float timer = 0f;

        while (timer < duration)
        {

            timer += Time.deltaTime;
            float t = timer / duration;
            if (isZooming == false)
            {
                gameCamera.Lens.FieldOfView = Mathf.Lerp(startFOV, endFOV, t);
            }
            yield return null;
        }

        gameCamera.Lens.FieldOfView = endFOV;
    }

    bool isZooming = false;
    public void TriggerZoom()
    {
        isZooming = true;
        float originalFOV = gameCamera.Lens.FieldOfView;
        float zoomFOV = 40;


        DOTween.To(() => gameCamera.Lens.FieldOfView, x => gameCamera.Lens.FieldOfView = x, zoomFOV, 0.3f).OnComplete(() =>
        {
            DOTween.To(() => gameCamera.Lens.FieldOfView, x => gameCamera.Lens.FieldOfView = x, originalFOV, 0.3f)
            .SetDelay(2f)
            .OnComplete(() => isZooming = false);
        });

    }
}

public enum MinimapDisplayMode
{
    All,
    Player
}
