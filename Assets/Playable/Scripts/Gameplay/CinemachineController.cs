using Unity.Cinemachine;
using Core;
using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;

public class CinemachineController : SingletonBehaviour<CinemachineController>
{
    [SerializeField] private List<CmCameraVariable> cmCameraVariables;
    [SerializeField] private float maxOrthoSize;
    [SerializeField] private float orthoTweenSpeed = 5f;

    [Header("Manual Zoom Settings")]
    [Tooltip("The orthographic size when you manually zoom in.")]
    [SerializeField] private float manualZoomInSize = 10f;
    [Tooltip("The orthographic size when you manually zoom out.")]
    [SerializeField] private float manualZoomOutSize = 25f;
    [Tooltip("How fast the manual zoom transitions (higher = faster).")]
    [SerializeField] private float manualZoomSpeed = 30f;

    private Tween orthoTween;
    private float baseOrthoSize;
    private CinemachineCamera mainCamera;

    private void Start()
    {
        // Initialize main camera (Game) and base size
        CmCameraVariable gameCam = cmCameraVariables.FirstOrDefault(x => x.type == CmCameraType.Game);
        if (gameCam != null && gameCam.cmCamera != null)
        {
            mainCamera = gameCam.cmCamera;
            baseOrthoSize = mainCamera.Lens.OrthographicSize;
        }
    }

    private void Update()
    {
        if (mainCamera == null) return;

        float zoomInput = Input.mouseScrollDelta.y;

        // Use GetKeyDown so you just tap the key once to toggle
        if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Q)) zoomInput = 1f;
        if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.W)) zoomInput = -1f;

        if (zoomInput > 0f)
        {
            // Scroll Up / Plus Key -> Zoom In fast
            baseOrthoSize = manualZoomInSize;

            orthoTween?.Kill();
            orthoTween = DOTween.To(() => mainCamera.Lens.OrthographicSize, x => mainCamera.Lens.OrthographicSize = x, manualZoomInSize, manualZoomSpeed)
                .SetSpeedBased()
                .SetEase(Ease.OutCubic);
        }
        else if (zoomInput < 0f)
        {
            // Scroll Down / Minus Key -> Zoom Out fast
            baseOrthoSize = manualZoomOutSize;

            orthoTween?.Kill();
            orthoTween = DOTween.To(() => mainCamera.Lens.OrthographicSize, x => mainCamera.Lens.OrthographicSize = x, manualZoomOutSize, manualZoomSpeed)
                .SetSpeedBased()
                .SetEase(Ease.OutCubic);
        }
    }

    [Button]
    public void ChangeCamera(CmCameraType cameraType)
    {
        CmCameraVariable targetVar = cmCameraVariables.FirstOrDefault(cm => cm.type == cameraType);
        if (targetVar.cmCamera != null)
        {
            StartCoroutine(ChangeCameraWithDelay(targetVar));
        }
    }

    private IEnumerator ChangeCameraWithDelay(CmCameraVariable targetVar)
    {
        if (targetVar.delayTime > 0f)
            yield return new WaitForSeconds(targetVar.delayTime);

        foreach (var cmCameraVariable in cmCameraVariables)
        {
            if (cmCameraVariable.type == targetVar.type)
            {
                cmCameraVariable.cmCamera.Priority = 1;

                CinemachineBrain brain = null;
                if (Camera.main != null)
                {
                    brain = Camera.main.GetComponent<CinemachineBrain>();
                }
                if (brain == null)
                {
                    brain = UnityEngine.Object.FindObjectOfType<CinemachineBrain>();
                }

                if (brain != null)
                {
                    brain.DefaultBlend.Time = cmCameraVariable.blendTime;
                    brain.DefaultBlend.Style = (CinemachineBlendDefinition.Styles)cmCameraVariable.blendType;
                }
            }
            else
            {
                cmCameraVariable.cmCamera.Priority = 0;
            }
        }
    }

    private Coroutine tempFocusCoroutine;
    private Transform defaultAimTarget;

    public void SetCameraTarget(CmCameraType cameraType, Transform aimTarget)
    {
        CmCameraVariable cameraVariable = cmCameraVariables.FirstOrDefault(cmVar => cmVar.type == cameraType);

        if (cameraVariable != null && cameraVariable.cmCamera != null)
        {
            defaultAimTarget = aimTarget;
            
            // Only update immediately if not currently in a temporary focus
            if (tempFocusCoroutine == null)
            {
                cameraVariable.cmCamera.Follow = aimTarget;
                cameraVariable.cmCamera.LookAt = aimTarget;
            }
        }
    }

    public void FocusTemporarily(CmCameraType cameraType, Transform tempTarget, float duration)
    {
        CmCameraVariable cameraVariable = cmCameraVariables.FirstOrDefault(cmVar => cmVar.type == cameraType);
        if (cameraVariable != null && cameraVariable.cmCamera != null)
        {
            if (tempFocusCoroutine != null) StopCoroutine(tempFocusCoroutine);
            tempFocusCoroutine = StartCoroutine(FocusTemporarilyCoroutine(cameraVariable.cmCamera, tempTarget, duration));
        }
    }

    private IEnumerator FocusTemporarilyCoroutine(CinemachineCamera cmCam, Transform tempTarget, float duration)
    {
        // Capture the original target if we haven't already
        if (defaultAimTarget == null) defaultAimTarget = (Transform)cmCam.Follow;

        cmCam.Follow = tempTarget;
        cmCam.LookAt = tempTarget;

        yield return new WaitForSeconds(duration);

        if (defaultAimTarget != null)
        {
            cmCam.Follow = defaultAimTarget;
            cmCam.LookAt = defaultAimTarget;
        }
        tempFocusCoroutine = null;
    }

    public void StopCameraFollow(CmCameraType cameraType)
    {
        CmCameraVariable cameraVariable = cmCameraVariables.FirstOrDefault(cmVar => cmVar.type == cameraType);
        if (cameraVariable != null && cameraVariable.cmCamera != null)
            cameraVariable.cmCamera.Follow = null;
    }

    public void StopCameraLookAt(CmCameraType cameraType)
    {
        CmCameraVariable cameraVariable = cmCameraVariables.FirstOrDefault(cmVar => cmVar.type == cameraType);
        if (cameraVariable != null && cameraVariable.cmCamera != null)
            cameraVariable.cmCamera.LookAt = null;
    }

    public void CameraShake(CmCameraType cameraType, float intensity, float frequence, float time)
    {
        CmCameraVariable cameraVariable = cmCameraVariables.FirstOrDefault(cmVar => cmVar.type == cameraType);
        if (cameraVariable != null && cameraVariable.cmCamera != null)
            StartCoroutine(ShakeCorotinue(cameraVariable.cmCamera, intensity, frequence, time));
    }

    IEnumerator ShakeCorotinue(CinemachineCamera camera, float intensity, float frequence, float time)
    {
        CinemachineBasicMultiChannelPerlin cmMCP = camera.GetComponent<CinemachineBasicMultiChannelPerlin>();
        if (cmMCP != null)
        {
            cmMCP.AmplitudeGain = intensity;
            cmMCP.FrequencyGain = frequence;

            yield return new WaitForSeconds(time);

            cmMCP.AmplitudeGain = 0;
            cmMCP.FrequencyGain = 0;
        }
    }

    public void SetMainCameraOrthoSize(float value)
    {
        if (mainCamera == null)
            return;

        float clampedValue = Mathf.Clamp01(value);
        float targetSize = Mathf.Lerp(baseOrthoSize, maxOrthoSize, clampedValue);

        orthoTween?.Kill();
        orthoTween = DOTween
            .To(() => mainCamera.Lens.OrthographicSize, x => mainCamera.Lens.OrthographicSize = x, targetSize, orthoTweenSpeed)
            .SetSpeedBased()
            .SetEase(Ease.Linear);
    }

    /// <summary>
    /// Sets camera ortho size based on coverage percentage.
    /// Adds additional ortho size linearly based on coverage.
    /// </summary>
    /// <param name="coveragePercent">Current coverage (0-100)</param>
    /// <param name="additionalOrthoPerCoverage">Additional ortho size at 100% coverage (0 = no change)</param>
    public void SetCoverageBasedOrthoSize(float coveragePercent, float additionalOrthoPerCoverage)
    {
        if (mainCamera == null)
            return;

        // Skip if no additional scaling configured
        if (additionalOrthoPerCoverage <= 0f)
            return;

        float coverageNormalized = Mathf.Clamp01(coveragePercent / 100f);
        float additionalSize = coverageNormalized * additionalOrthoPerCoverage;
        float targetSize = baseOrthoSize + additionalSize;

        // Clamp to max if set
        if (maxOrthoSize > 0)
            targetSize = Mathf.Min(targetSize, maxOrthoSize);

        orthoTween?.Kill();
        orthoTween = DOTween
            .To(() => mainCamera.Lens.OrthographicSize, x => mainCamera.Lens.OrthographicSize = x, targetSize, orthoTweenSpeed)
            .SetSpeedBased()
            .SetEase(Ease.Linear);
    }

    public void SetMaxOrthoSize(float value)
    {
        maxOrthoSize = value;
    }

    public void SetBaseOrthoSize(float value)
    {
        baseOrthoSize = value;
    }

    /// <summary>
    /// Set camera ortho size for hook phase (instant, no transition)
    /// </summary>
    public void SetHookPhaseOrthoSize(float targetSize)
    {
        if (mainCamera == null)
            return;

        orthoTween?.Kill();
        mainCamera.Lens.OrthographicSize = targetSize;
    }

    /// <summary>
    /// Reset camera ortho size to base value (after hook phase)
    /// </summary>
    public void ResetToBaseOrthoSize()
    {
        if (mainCamera == null)
            return;

        orthoTween?.Kill();
        orthoTween = DOTween
            .To(() => mainCamera.Lens.OrthographicSize, x => mainCamera.Lens.OrthographicSize = x, baseOrthoSize, orthoTweenSpeed)
            .SetSpeedBased()
            .SetEase(Ease.Linear);
    }

    /// <summary>
    /// Sets camera ortho size based on trail length.
    /// Starts from baseOrthoSize and increases, capped at maxOrthoSize.
    /// </summary>
    /// <param name="trailLength">Current trail length in world units</param>
    /// <param name="orthoPerUnit">Additional ortho size per unit of trail length</param>
    public void SetTrailBasedOrthoSize(float trailLength, float orthoPerUnit)
    {
        if (mainCamera == null) return;

        float targetSize = baseOrthoSize + (trailLength * orthoPerUnit);

        if (maxOrthoSize > 0)
            targetSize = Mathf.Min(targetSize, maxOrthoSize);

        orthoTween?.Kill();
        orthoTween = DOTween
            .To(() => mainCamera.Lens.OrthographicSize, x => mainCamera.Lens.OrthographicSize = x, targetSize, orthoTweenSpeed)
            .SetSpeedBased()
            .SetEase(Ease.Linear);
    }
}

[System.Serializable]
public class CmCameraVariable
{
    public CmCameraType type;
    public CinemachineCamera cmCamera;
    public CinemachineBlendDefinition.Styles blendType = CinemachineBlendDefinition.Styles.Linear;
    public float blendTime = 0.5f;
    public float delayTime = 0;
}

public enum CmCameraType
{
    Game,
    Hook
}
