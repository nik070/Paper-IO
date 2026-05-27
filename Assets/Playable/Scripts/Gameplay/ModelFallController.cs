using UnityEngine;
using DG.Tweening;

public class ModelFallController : MonoBehaviour
{
    [Header("Walk Settings")]
    [Tooltip("Distance to walk forward along the X axis.")]
    public float walkDistanceX = 2f;
    public float walkDuration = 1f;

    [Header("Jump Settings")]
    [Tooltip("Distance to move along the X axis while jumping.")]
    public float jumpDistanceX = 1f;
    [Tooltip("Height of the jump.")]
    public float jumpPower = 1f;
    public float jumpDuration = 0.5f;

    [Header("Auto Start")]
    public bool playOnStart = false;

    private void Start()
    {
        if (playOnStart)
        {
            PlayFallSequence();
        }
    }

    [ContextMenu("Play Fall Sequence")]
    public void PlayFallSequence()
    {
        // Kill any existing tweens on this object to prevent overlapping animations
        transform.DOKill();

        // Calculate positions based on the current position
        Vector3 startPos = transform.position;
        Vector3 afterWalkPos = startPos + new Vector3(walkDistanceX, 0, 0);
        Vector3 afterJumpPos = afterWalkPos + new Vector3(jumpDistanceX, 0, 0);

        Sequence sequence = DOTween.Sequence();

        // 1. Walk forward on the X axis
        sequence.Append(transform.DOMove(afterWalkPos, walkDuration).SetEase(Ease.Linear));

        // 2. Make a little jump (moves slightly forward on X while jumping up on Y)
        // Adding an Ease like OutQuad or OutSine smooths out the landing so it doesn't stop abruptly
        sequence.Append(transform.DOJump(afterJumpPos, jumpPower, 1, jumpDuration).SetEase(Ease.OutQuad));

        // Optional: Add a callback when the sequence is done
        sequence.OnComplete(() =>
        {
            Debug.Log($"{gameObject.name} has finished sequence.");
            // You can add logic here like destroying the object or triggering a game over
            // Destroy(gameObject);
        });
    }
}
