using UnityEngine;

/// <summary>
/// Shared helper for optional Physics.IgnoreCollision between snapped workpieces and guides.
/// Master switch <see cref="Enabled"/> defaults OFF so welding tip contact keeps working.
/// </summary>
public static class SnapGuideCollisionIgnore
{
    /// <summary>
    /// When false, new IgnoreCollision pairs are not applied (clears still run).
    /// Toggle via <see cref="SnapGuideCollisionIgnoreSettings"/>.
    /// </summary>
    public static bool Enabled = false;

    public static void SetIgnoredBetween(Transform workpieceRoot, Transform guideRoot, bool ignore, bool force = false)
    {
        if (workpieceRoot == null || guideRoot == null)
            return;

        SetIgnoredBetween(
            workpieceRoot.GetComponentsInChildren<Collider>(true),
            guideRoot.GetComponentsInChildren<Collider>(true),
            ignore,
            force);
    }

    public static void SetIgnoredBetween(Transform workpieceRoot, bool ignore, bool force, params Transform[] guideRoots)
    {
        if (workpieceRoot == null || guideRoots == null)
            return;

        for (int i = 0; i < guideRoots.Length; i++)
        {
            if (guideRoots[i] != null)
                SetIgnoredBetween(workpieceRoot, guideRoots[i], ignore, force);
        }
    }

    public static void SetIgnoredBetween(Collider[] workpieceColliders, Collider[] guideColliders, bool ignore, bool force = false)
    {
        if (workpieceColliders == null || guideColliders == null)
            return;

        // Applying ignores requires the master switch unless force (e.g. clamp unsnap cooldown).
        // Clearing always runs.
        if (ignore && !Enabled && !force)
            return;

        for (int i = 0; i < workpieceColliders.Length; i++)
        {
            Collider a = workpieceColliders[i];
            if (a == null)
                continue;

            for (int j = 0; j < guideColliders.Length; j++)
            {
                Collider b = guideColliders[j];
                if (b == null || a == b)
                    continue;

                Physics.IgnoreCollision(a, b, ignore);
            }
        }
    }
}
