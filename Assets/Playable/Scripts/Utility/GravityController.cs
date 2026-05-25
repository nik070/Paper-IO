using UnityEngine;

/// <summary>
/// GravityController — attach this to any persistent GameObject (e.g. GameManager).
/// Lets you set Unity's global Physics.gravity to any preset axis or a fully custom vector,
/// all from the Inspector or via code at runtime.
/// </summary>
public class GravityController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Enums
    // ─────────────────────────────────────────────────────────────────────────

    public enum GravityAxis
    {
        /// <summary>Standard downward gravity (0, -1, 0)</summary>
        Down,
        /// <summary>Upward gravity (0, +1, 0)</summary>
        Up,
        /// <summary>Gravity pulls toward +X</summary>
        Right,
        /// <summary>Gravity pulls toward -X</summary>
        Left,
        /// <summary>Gravity pulls toward +Z</summary>
        Forward,
        /// <summary>Gravity pulls toward -Z</summary>
        Back,
        /// <summary>Use the custom vector defined in customGravityDirection</summary>
        Custom
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Gravity Settings")]
    [Tooltip("Choose a preset axis or 'Custom' to supply your own direction.")]
    [SerializeField] private GravityAxis gravityAxis = GravityAxis.Down;

    [Tooltip("Magnitude (strength) of gravity in m/s².  Unity default is 9.81.")]
    [SerializeField] private float gravityStrength = 9.81f;

    [Header("Custom Direction (only used when Axis = Custom)")]
    [Tooltip("Normalized direction for custom gravity.  Will be normalized automatically.")]
    [SerializeField] private Vector3 customGravityDirection = Vector3.down;

    [Header("Runtime Options")]
    [Tooltip("Apply the configured gravity immediately when the scene starts.")]
    [SerializeField] private bool applyOnStart = true;

    [Tooltip("Expose a smooth-transition time (seconds).  0 = instant snap.")]
    [SerializeField] [Min(0f)] private float transitionDuration = 0f;

    // ─────────────────────────────────────────────────────────────────────────
    // Private state
    // ─────────────────────────────────────────────────────────────────────────

    private Vector3 _targetGravity;
    private Vector3 _originGravity;
    private float   _transitionTimer = 0f;
    private bool    _isTransitioning = false;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        if (applyOnStart)
            SetGravity(gravityAxis, gravityStrength, customGravityDirection);
    }

    private void Update()
    {
        if (!_isTransitioning) return;

        _transitionTimer += Time.deltaTime;
        float t = Mathf.Clamp01(_transitionTimer / transitionDuration);
        Physics.gravity = Vector3.Lerp(_originGravity, _targetGravity, t);

        if (t >= 1f)
        {
            Physics.gravity = _targetGravity;
            _isTransitioning = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Set gravity using one of the preset axes.
    /// </summary>
    /// <param name="axis">The axis preset to use.</param>
    /// <param name="strength">Gravity magnitude in m/s².</param>
    public void SetGravity(GravityAxis axis, float strength = 9.81f)
    {
        SetGravity(axis, strength, customGravityDirection);
    }

    /// <summary>
    /// Set gravity using one of the preset axes, with an optional custom direction fallback.
    /// </summary>
    public void SetGravity(GravityAxis axis, float strength, Vector3 customDir)
    {
        gravityAxis       = axis;
        gravityStrength   = strength;
        customGravityDirection = customDir;

        Vector3 direction = ResolveDirection(axis, customDir);
        ApplyGravity(direction.normalized * strength);
    }

    /// <summary>
    /// Set gravity using a fully custom direction vector.
    /// The vector will be normalized; its magnitude is replaced by <paramref name="strength"/>.
    /// </summary>
    /// <param name="direction">The desired gravity direction (need not be normalized).</param>
    /// <param name="strength">Gravity magnitude in m/s².</param>
    public void SetCustomGravity(Vector3 direction, float strength = 9.81f)
    {
        gravityAxis            = GravityAxis.Custom;
        customGravityDirection = direction;
        gravityStrength        = strength;
        ApplyGravity(direction.normalized * strength);
    }

    /// <summary>
    /// Instantly restore Unity's default gravity (0, -9.81, 0).
    /// </summary>
    public void ResetToDefault()
    {
        SetGravity(GravityAxis.Down, 9.81f);
    }

    /// <summary>
    /// Returns the current gravity vector being applied.
    /// </summary>
    public Vector3 CurrentGravity => Physics.gravity;

    /// <summary>
    /// Returns the current gravity strength (magnitude).
    /// </summary>
    public float CurrentStrength => Physics.gravity.magnitude;

    // ─────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private Vector3 ResolveDirection(GravityAxis axis, Vector3 custom)
    {
        switch (axis)
        {
            case GravityAxis.Down:    return Vector3.down;
            case GravityAxis.Up:      return Vector3.up;
            case GravityAxis.Right:   return Vector3.right;
            case GravityAxis.Left:    return Vector3.left;
            case GravityAxis.Forward: return Vector3.forward;
            case GravityAxis.Back:    return Vector3.back;
            case GravityAxis.Custom:  return custom.sqrMagnitude > 0f ? custom : Vector3.down;
            default:                  return Vector3.down;
        }
    }

    private void ApplyGravity(Vector3 target)
    {
        _targetGravity = target;

        if (transitionDuration <= 0f)
        {
            Physics.gravity  = target;
            _isTransitioning = false;
        }
        else
        {
            _originGravity   = Physics.gravity;
            _transitionTimer = 0f;
            _isTransitioning = true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Editor helper — live preview in Play Mode when values change in Inspector
    // ─────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        SetGravity(gravityAxis, gravityStrength, customGravityDirection);
    }
#endif
}
