using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Sphere : MonoBehaviour
{

    Vector3 velocity, acceleration;
    public float mass = 1.0f;
    float gravity = 9.81f;
    float CoefficientOfRestitution = 0.8f;

    public float Radius { get { return transform.localScale.x / 2.0f; } private set { transform.localScale = value * 2 * Vector3.one; } }

  
    // Start is called before the first frame update
    void Start()
    {
        Vector3 previousVelocity = velocity;

        Vector3 previousAcceleration = acceleration;

        Vector3 previousPosition = transform.position;
    }

    // Update is called once per frame
    void Update()
    {

        acceleration = gravity * Vector3.down;

        velocity += acceleration * Time.deltaTime;

        transform.position += velocity * Time.deltaTime;

        if (isCollidingWith(this))
        {
            //Vector3 v1 = (Utili.Parallel(velocity, plane.Normal)) + (Utili.Parallel(velocity, plane.Normal));
            //Vector3 v2 = (Utili.Perpendicular(velocity, plane.Normal)) + (Utili.Perpendicular(velocity, plane.Normal));
        }
    }

    public void resolveCollisionWith(PlaneScript planeScript)
    {
        transform.position -= velocity * Time.deltaTime;
        /*velocity = -(CoefficientOfRestitution * velocity);*/
        Vector3 y = Utili.Parallel(velocity, planeScript.Normal);
        Vector3 x = Utili.Perpendicular(velocity, planeScript.Normal);

        Vector3 newVelocity = (x - CoefficientOfRestitution * y);

        velocity = newVelocity;
    }

    public bool isCollidingWith(Sphere otherSphere)
    {
        return (otherSphere.Radius + Radius) > (Vector3.Distance(otherSphere.transform.position, transform.position));
    }

    internal void resolveCollisionWith(Sphere sphere2)
    {
        // Calculate Time of Impact
        

        SpherePhysics sphere;
        Vector3 normal = (transform.position - sphere2.transform.position).normalized;

        Vector3 sphere1Parallel = Utili.Parallel(velocity, normal);
        Vector3 sphere2Parallel = Utili.Parallel(sphere2.velocity, normal);

        Vector3 sphere1Perp = Utili.Perpendicular(velocity, normal);
        Vector3 sphere2Perp = Utili.Perpendicular(sphere2.velocity, normal);

        Vector3 u1 = sphere1Parallel;
        Vector3 u2 = sphere2Parallel;

        Vector3 v1 = ((mass - sphere2.mass) / (mass + sphere2.mass)) * u1 + (sphere2.mass * 2) / (mass + sphere2.mass) * u2;
        Vector3 v2 = (-(mass - sphere2.mass) / (mass + sphere2.mass)) * u2 + (mass * 2) / (mass + sphere2.mass) * u1;

        velocity = sphere1Perp + v1 * CoefficientOfRestitution;
        sphere2.slaveCollisionResolution(sphere2.transform.position, sphere2Perp + v2 * sphere2.CoefficientOfRestitution);

        //SpherePhysics sphere = (Utili.Parallel(velocity, sphere.Normal)) + (Utili.Parallel(velocity, sphere2.Normal));
        //sphere2 = Utili.Perpendicular(velocity, plane.Normal) + (Utili.Perpendicular(velocity, plane.Normal));    }
    }

    private void slaveCollisionResolution(Vector3 position, Vector3 newVelocity)
    {
        transform.position = position;
        velocity = newVelocity;
    }
}
