using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class UtilsNagu
{
    public const float EPSILON_DIR = 1e-4f; // 0.0001 = magnitude
    public const float EPSILON_DIR_SQR = EPSILON_DIR * EPSILON_DIR; // sqrMagnitude

    public const float EPSILON_POS = 1e-3f; // 0.001 magnitude
    public const float EPSILON_POS_SQR = EPSILON_POS * EPSILON_POS; // sqrMagnitude
    public const float EPSILON_THROTTLE = 1e-2f; // 0.01f
    public static bool HasDirection(Vector3 v) => v.sqrMagnitude > EPSILON_DIR_SQR;

    public static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    public static Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
    {
        return v.sqrMagnitude > EPSILON_DIR_SQR ? v.normalized : fallback;
    }

    public static Vector3 GetCameraForwardNormalized(Transform cam)
    {
        Vector3 forward = cam.forward;
        forward.y = 0;
        return forward.normalized;
    }

    public static Vector3 GetCameraRightNormalized(Transform cam)
    {
        Vector3 right = cam.right;
        right.y = 0;
        return right.normalized;
    }

    public static Vector3 GetRandomNavmeshPoint(Vector3 point, float area)
    {
        Vector3 randomDir = Random.insideUnitSphere * area;
        randomDir += point;
        NavMeshHit hit;
        Vector3 finalPos = Vector3.zero;
        if (NavMesh.SamplePosition(randomDir, out hit, area, 1))
        {
            finalPos = hit.position;
        }

        return finalPos;
    }

    public static float Bell(float x, float mu, float sigma)
    {
        float z = (x - mu) / Mathf.Max(UtilsNagu.EPSILON_DIR, sigma);
        return Mathf.Exp(-0.5f * z * z); // 0..1 pico en mu
    }

    public static float Arc(float angAbs, float half)
    {
        return Mathf.Clamp01(1f - angAbs / half); // 0..1 pico frente a 0°
    }

    public static float SmoothStepInv(float x, float a, float b)
    {
        // smoothstep invertible
        return Mathf.Clamp01((b - x) / Mathf.Max(UtilsNagu.EPSILON_DIR, (b - a)));
    }

    public static void AddToList<T>(List<T> list, T obj)
    {
        if (!list.Contains(obj))
        {
            list.Add(obj);
        }
    }

    public static void RemoveFromList<T>(List<T> list, T obj)
    {
        if (list.Contains(obj))
        {
            list.Remove(obj);
        }
    }

    public static void RemoveAllInactive(ref List<GameObject> list)
    {
        List<GameObject> withoutInactive = new List<GameObject>();
        foreach (GameObject item in list)
        {
            if (item != null)
            {
                if (item.activeSelf)
                {
                    withoutInactive.Add(item);
                }
            }
        }

        list = withoutInactive;
    }

    public static bool DoListsMatch<T>(List<T> list1, List<T> list2)
    {
        var areListsEqual = true;

        if (list1.Count != list2.Count)
            return false;

        //list1.Sort(); // Sort list one
        //list2.Sort(); // Sort list two

        for (var i = 0; i < list1.Count; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(list2[i], list1[i]))
            {
                areListsEqual = false;
            }
        }

        return areListsEqual;
    }

    public static bool ListContainsAnotherListFromStart<T>(List<T> listaPrincipal, List<T> listaSecundaria)
    {
        if (listaSecundaria.Count > listaPrincipal.Count)
        {
            return false;
        }

        for (int i = 0; i < listaSecundaria.Count; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(listaPrincipal[i], listaSecundaria[i]))
            {
                return false;
            }
        }

        return true;
    }
}