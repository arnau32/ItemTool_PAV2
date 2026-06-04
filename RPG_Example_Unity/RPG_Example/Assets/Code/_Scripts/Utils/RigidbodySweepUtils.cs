using UnityEngine;

public static class RigidbodySweepUtils
{
    private const float SkinWidth = 0.04f;

    private const float MinMoveDistance = 0.001f;

    public static Vector3 ClipDelta(Rigidbody rb, Vector3 delta, WallSlideDetector wallDetector)
    {
        if (wallDetector == null) return delta;

        Vector3 flatDelta = new Vector3(delta.x, 0f, delta.z);
        float moveDistance = flatDelta.magnitude;

        if (moveDistance < MinMoveDistance) return delta;

        Vector3 moveDir = flatDelta / moveDistance;

        float radius = wallDetector.CastRadius;
        float height = wallDetector.CastHeight;
        Vector3 rbPos = rb.position;
        Vector3 capsuleBot = rbPos + Vector3.up * radius;
        Vector3 capsuleTop = rbPos + Vector3.up * height;
        LayerMask wallMask = wallDetector.WallLayerMask;

        if (!Physics.CapsuleCast(capsuleBot, capsuleTop, radius, moveDir, out RaycastHit hit, moveDistance + SkinWidth,
                wallMask, QueryTriggerInteraction.Ignore))
        {
            return delta;
        }

        float safeDistance = Mathf.Max(0f, hit.distance - SkinWidth);

        if (safeDistance < MinMoveDistance)
        {
            // Already touching — slide the full flat delta along the wall normal.
            return new Vector3(SlideXZ(flatDelta, hit.normal).x, delta.y, SlideXZ(flatDelta, hit.normal).z);
        }

        // Travel safeDistance toward the wall, slide the remaining delta.
        Vector3 clipped = moveDir * safeDistance;
        Vector3 remainder = flatDelta - clipped;
        Vector3 slid = SlideXZ(remainder, hit.normal);

        return new Vector3(clipped.x + slid.x, delta.y, clipped.z + slid.z);
    }

    // Projects v onto the XZ plane whose normal is the wall normal.
    // Flattens the normal to XZ so vertical wall geometry doesn't push the player up/down.
    private static Vector3 SlideXZ(Vector3 v, Vector3 wallNormal)
    {
        Vector3 flat = new Vector3(wallNormal.x, 0f, wallNormal.z);
        if (flat.sqrMagnitude < 0.0001f) return Vector3.zero;
        return Vector3.ProjectOnPlane(v, flat.normalized);
    }
}