using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.Build.Reporting;
using UnityEditor.Playables;
using UnityEngine;

public class SpherePhysics : MonoBehaviour
{
    public Vector3 previousVelocity, previousPosition;
    public Vector3 velocity, acceleration;
    public float mass = 1.0f;
    float gravity = 9.81f;
    float coefficientOfRestitution = 0.8f;

    public float Radius { get { return transform.localScale.x / 2.0f; } private set { transform.localScale = value * 2 * Vector3.one; } }

    void Start()
    {
    }

    void Update()
    {
        previousVelocity = velocity;
        previousPosition = transform.position;

        acceleration = gravity * Vector3.down;

        velocity += acceleration * Time.deltaTime;

        transform.position += velocity * Time.deltaTime;
    }

    public void ResolveCollisionWith(PlaneScript planeScript)
    {
        float currentDistance = planeScript.distanceFromSphere(this);
        float previousDistance = Vector3.Dot(previousPosition - planeScript.Position, planeScript.Normal) - Radius;

        //DEBUG
        print("Distance:" + currentDistance + "Old Distance: " + previousDistance);

        float timeOfImpact = -previousDistance / (currentDistance - previousDistance) * Time.deltaTime;

        Vector3 positionOfImpact = previousPosition + (timeOfImpact * velocity);

        Vector3 velocityAtImpact = previousVelocity + (acceleration * timeOfImpact);

        Vector3 normalComponent = Util.ProjectVectorOntoNormal(velocityAtImpact, planeScript.Normal);
        Vector3 perpendicularComponent = Util.ExtractComponentPerpendicularToNormal(velocityAtImpact, planeScript.Normal);

        Vector3 newVelocity = (perpendicularComponent - coefficientOfRestitution * normalComponent);

        float timeRemaining = Time.deltaTime - timeOfImpact;

        velocity = newVelocity + acceleration * timeRemaining;

        if (Vector3.Dot(velocity, planeScript.Normal) < 0)
        {
            velocity = Util.ExtractComponentPerpendicularToNormal(velocity, planeScript.Normal);
        };

        transform.position = positionOfImpact + velocity * timeRemaining;
    }

    public bool isCollidingWith(SpherePhysics otherSphere)
    {
        return Vector3.Distance(otherSphere.transform.position, transform.position) < (otherSphere.Radius + Radius);
    }

    public void ResolveCollisionWith(SpherePhysics sphere2)
    {
        float currentSpherePlaneDistance = Vector3.Distance(sphere2.transform.position, transform.position) - (sphere2.Radius + Radius);
        float previousSpherePlaneDistance = Vector3.Distance(sphere2.previousPosition, previousPosition) - (sphere2.Radius + Radius);

        float timeOfImpact = -previousSpherePlaneDistance / (currentSpherePlaneDistance - previousSpherePlaneDistance) * Time.deltaTime;
        print("TOI: " + timeOfImpact + "deltaTime: " + Time.deltaTime);

        Vector3 sphere1AtImpact = previousPosition + velocity * timeOfImpact;
        Vector3 sphere2AtImpact = sphere2.previousPosition + sphere2.velocity * timeOfImpact;

        Vector3 Sphere1VelocityAtImpact = previousVelocity + (acceleration * timeOfImpact);
        Vector3 sphere2VelocityAtImpact = sphere2.previousVelocity + (sphere2.acceleration * timeOfImpact);

        Vector3 collisionNormal = (sphere1AtImpact - sphere2AtImpact).normalized;

        Vector3 sphere1ParallelToNormal = Util.ProjectVectorOntoNormal(Sphere1VelocityAtImpact, collisionNormal);
        Vector3 sphere1PerpendicularToNormal = Util.ExtractComponentPerpendicularToNormal(Sphere1VelocityAtImpact, collisionNormal);
        Vector3 sphere2ParallelToNormal = Util.ProjectVectorOntoNormal(sphere2VelocityAtImpact, collisionNormal);
        Vector3 sphere2PerpendicularToNormal = Util.ExtractComponentPerpendicularToNormal(sphere2VelocityAtImpact, collisionNormal);

        Vector3 prevParallelVelocity1 = sphere1ParallelToNormal;
        Vector3 prevParallelVelocity2 = sphere2ParallelToNormal;

        Vector3 parallelVelocity1 = ((mass - sphere2.mass) / (mass + sphere2.mass)) * prevParallelVelocity1 + ((sphere2.mass * 2) / (mass + sphere2.mass)) * prevParallelVelocity2;
        Vector3 parallelVelocity2 = (-(mass - sphere2.mass) / (mass + sphere2.mass)) * prevParallelVelocity2 + ((mass * 2) / (mass + sphere2.mass)) * prevParallelVelocity1;

        velocity = sphere1PerpendicularToNormal + parallelVelocity1 * coefficientOfRestitution;
        Vector3 sphere1VelocityAfterImpact = sphere1PerpendicularToNormal + parallelVelocity1 * coefficientOfRestitution;
        Vector3 sphere2VelocityAfterImpact = sphere2PerpendicularToNormal + parallelVelocity2 * coefficientOfRestitution;


        float timeRemaining = Time.deltaTime - timeOfImpact;

        velocity = sphere1VelocityAfterImpact + acceleration * timeRemaining;
        Vector3 sphere2Velocity = sphere2VelocityAfterImpact + sphere2.acceleration * timeRemaining;

        transform.position = sphere1AtImpact + sphere1VelocityAfterImpact * timeRemaining;

         
        Vector3 sphere2ResolvedPosition = sphere2AtImpact + sphere2VelocityAfterImpact * timeRemaining;

        if (Vector3.Distance(transform.position, sphere2ResolvedPosition) < (Radius + sphere2.Radius))
        { 
            print("Not good"); 
        }

        sphere2.slaveCollisionResolution(sphere2ResolvedPosition, sphere2Velocity);
    }

    private void slaveCollisionResolution(Vector3 position, Vector3 newVelocity)
    {
        transform.position = position;
        velocity = newVelocity;
    }
}