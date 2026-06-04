using UnityEngine;

public static class RigidbodyVelocityUtils
{
    public static void SetLinearVelocity(Rigidbody rb, Vector3 v)
    {
//#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
//#else
//        rb.velocity = v;
//#endif
    }
    public static void SetPlanarLinearVelocity(Rigidbody rb, Vector3 planarXZ, float keepY) =>SetLinearVelocity(rb, new Vector3(planarXZ.x, keepY, planarXZ.z));
}
