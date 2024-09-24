using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class Sphere : MonoBehaviour
{

    Vector3 velocity, acceleration, previousVelocity, previousAcceleration, previousPosition;
    public float mass = 1.0f;
    float gravity = 9.81f;
    float timeOfImpact = 0.0f;
    float CoefficientOfRestitution = 0.8f;

    public float Radius { get { return transform.localScale.x / 2.0f; } private set { transform.localScale = value * 2 * Vector3.one; } }

  
    // Start is called before the first frame update
    void Start()
    {
        previousVelocity = velocity;

        previousAcceleration = acceleration;

        previousPosition = transform.position;
    }

    // Update is called once per frame
    public void Update()
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
        float currentDistance = planeScript.distanceFromSphere(this);
        float previousDistance = Vector3.Dot(previousPosition - planeScript.Position, planeScript.Normal) - Radius;

        print("Distance between Sphere and Plane:" + currentDistance + "Old Distance between Sphere and Plane: " + previousDistance);

        // Calculate Time of Impact
        // At time t0, (Plane Position - Sphere Position) normalised - radius = d0
        // At time t1, (Plane Position - Sphere Position) normalised - radius = d1

        // d(deltaTime) = d1
        // d(0) = d0 =) d(t) = d0 + mt
        //              d(t) = do + (d1-d0) * (t/deltaTime)
        // For what t is d(t) = 0
        // 0 = d0 + (d1-d0) * t/deltaTime
        // ToI = -d0/(d1-d0) * deltaTime

        // Step 1: Time of Impact
        timeOfImpact = -previousDistance / (currentDistance - previousDistance) * Time.deltaTime;

        Vector3 ImpactPosition = previousPosition + timeOfImpact * velocity;

        // Step 2: New Velocity
        Vector3 impactVelocity = previousVelocity += acceleration * timeOfImpact;

        //Step 3: Position of Impact
        Vector3 positionOfImpact = previousPosition + (timeOfImpact * impactVelocity);

        positionOfImpact -= impactVelocity * timeOfImpact; 

        /*velocity = -(CoefficientOfRestitution * velocity);*/
        Vector3 y = Utili.Parallel(impactVelocity, planeScript.Normal);
        Vector3 x = Utili.Perpendicular(impactVelocity, planeScript.Normal);

        Vector3 newVelocity = (x - CoefficientOfRestitution * y);

        velocity = newVelocity;
    }

    public bool isCollidingWith(Sphere otherSphere)
    {
        return (otherSphere.Radius + Radius) > (Vector3.Distance(otherSphere.transform.position, transform.position));
    }

    internal void resolveCollisionWith(Sphere sphere2)
    {

        float distance = Vector3.Distance(sphere2.transform.position, transform.position) - (sphere2.Radius + Radius);
        float oldDistance = Vector3.Distance(sphere2.previousPosition, previousPosition) - (sphere2.Radius + Radius);

        print("Distance between Spheres:" + distance + "Old Distance between Spheres: " + oldDistance);


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
