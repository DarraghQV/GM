using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Utili
{
 public static Vector3 Parallel (Vector3 v, Vector3 n)
    {
        return Vector3.Dot(v, n) * n;
    }

    public static Vector3 Perpendicular(Vector3 v, Vector3 n)
    {
        return v - Parallel(v, n);
    }
}
