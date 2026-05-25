using UnityEngine;

public class PowerUpAnimation : MonoBehaviour
{
     [Header("Floating")]
    [SerializeField] private float floatHeight = 0.25f;
    [SerializeField] private float floatSpeed = 2f;

    [Header("Scaling")]
    [SerializeField] private float scaleAmount = 0.15f;
    [SerializeField] private float scaleSpeed = 2f;

    [Header("Rotation (Optional)")]
    [SerializeField] private bool rotate = true;
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0f, 80f, 0f);

    [SerializeField] private Transform visual;

    private Vector3 startPos;
    private Vector3 startScale;
    public Transform exp;

    private void Start()
    {
        startPos = visual.position;
        startScale = visual.localScale;
    }

    private void Update()
    {
        // Float Up & Down
        float newY = startPos.y + Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        visual.position = new Vector3(startPos.x, newY, startPos.z);

        // Scale Up & Down
        float scale = 1 + Mathf.Sin(Time.time * scaleSpeed) * scaleAmount;
        visual.localScale = startScale * scale;

        // Rotate
        if (rotate)
        {
            visual.Rotate(rotationSpeed * Time.deltaTime);
        }
    }
}
