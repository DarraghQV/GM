using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ObjectManager : MonoBehaviour
{

    List<Sphere> spheres;
    List<PlaneScript> planes;

    void Start()
    {
        spheres = FindObjectsOfType<Sphere>().ToList();
        planes = FindObjectsOfType<PlaneScript>().ToList();
    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < spheres.Count; i++)
        {
            Sphere sphere = spheres[i];
            foreach (PlaneScript plane in planes)
            {
                if (plane.isCollidingWith(sphere))
                {
                    sphere.resolveCollisionWith(plane);
                }
            }

            if (i < (spheres.Count - 1))
            {
                for (int j = i + 1; j < spheres.Count; j++)
                {
                    Sphere sphere2 = spheres[j];
                    if (sphere2.isCollidingWith(sphere))
                    {
                        sphere.resolveCollisionWith(sphere2);
                    }
                }
            }
        }
    }
}
