using UnityEngine;
using System.Collections;
namespace Core
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private FollowTarget _followTarget;
        [SerializeField] private GameObject _background;

        public Camera Camera => _camera;
        public FollowTarget FollowTarget => _followTarget;
        public GameObject Background => _background;


        public Camera cam;

    [Header("Zoom Settings")]
    [SerializeField] private float normalSize = 20f;
    [SerializeField] private float zoomSize = 30f;

    [SerializeField] private float zoomInSpeed = 5f;
    [SerializeField] private float zoomOutSpeed = 3f;

    [SerializeField] private float holdTime = 1.5f;

    private Coroutine zoomRoutine;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Z))
        {
            ZoomEffect();
        }
    }

    public void ZoomEffect()
    {
        if (zoomRoutine != null)
            StopCoroutine(zoomRoutine);

        zoomRoutine = StartCoroutine(ZoomCoroutine());
    }

    IEnumerator ZoomCoroutine()
    {
        // Smooth Zoom Out
        while (Mathf.Abs(cam.orthographicSize - zoomSize) > 0.05f)
        {
            cam.orthographicSize = Mathf.Lerp(
                cam.orthographicSize,
                zoomSize,
                zoomInSpeed * Time.deltaTime
            );

            yield return null;
        }

        cam.orthographicSize = zoomSize;

        // Hold
        yield return new WaitForSeconds(holdTime);

        // Smooth Zoom Back
        while (Mathf.Abs(cam.orthographicSize - normalSize) > 0.05f)
        {
            cam.orthographicSize = Mathf.Lerp(
                cam.orthographicSize,
                normalSize,
                zoomOutSpeed * Time.deltaTime
            );

            yield return null;
        }

        cam.orthographicSize = normalSize;
    }
    }

}
