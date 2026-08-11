using UnityEngine;

/// <summary>
/// Spinning sanding wheel for an angle grinder. Rotates around its local Y axis while
/// <see cref="SetSpinning"/> is true. Driven by <see cref="sanddisktrig"/> on the parent
/// (held + trigger). Includes spin-up / spin-down smoothing for a more realistic feel.
/// </summary>
public class sanddisk : MonoBehaviour
{
    [Header("Spin")]
    [Tooltip("Top spin speed in degrees per second when fully spun up.")]
    public float maxDegreesPerSecond = 1800f;

    [Tooltip("If true, spins clockwise when viewed from above (+Y looking down). If false, counter-clockwise.")]
    public bool clockwise = true;

    [Header("Smoothing")]
    [Tooltip("Degrees/second added per second while spinning up (0 = instant).")]
    public float spinUpAccel = 4000f;

    [Tooltip("Degrees/second removed per second while spinning down (0 = instant stop).")]
    public float spinDownAccel = 3000f;

    float _currentSpeed;
    bool _spinning;

    /// <summary>External enable/disable from <see cref="sanddisktrig"/>.</summary>
    public void SetSpinning(bool spinning)
    {
        _spinning = spinning;
    }

    /// <summary>True while the disk is actually rotating (speed &gt; 0).</summary>
    public bool IsSpinning => _currentSpeed > 0.01f;

    /// <summary>Current spin speed in degrees per second (always positive).</summary>
    public float CurrentDegreesPerSecond => _currentSpeed;

    void Update()
    {
        float target = _spinning ? Mathf.Max(0f, maxDegreesPerSecond) : 0f;

        if (Mathf.Approximately(_currentSpeed, target))
        {
            _currentSpeed = target;
        }
        else if (_currentSpeed < target)
        {
            float accel = spinUpAccel <= 0f ? float.PositiveInfinity : spinUpAccel;
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, target, accel * Time.deltaTime);
        }
        else
        {
            float decel = spinDownAccel <= 0f ? float.PositiveInfinity : spinDownAccel;
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, target, decel * Time.deltaTime);
        }

        if (_currentSpeed <= 0f)
            return;

        float dir = clockwise ? -1f : 1f;
        transform.Rotate(0f, dir * _currentSpeed * Time.deltaTime, 0f, Space.Self);
    }
}
