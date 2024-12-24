using UnityEngine;

public class RandomizeSpherePositions : MonoBehaviour
{
    // Range limits for position randomization
    private float xMin = -20f;
    private float xMax = 20f;
    private float yMin = 5f;
    private float yMax = 20f;
    private float zMin = -10f;
    private float zMax = 25f;

    private float velocityXMin = 0f; 
    private float velocityXMax = 10f;
    private float velocityZMin = 0f; 
    private float velocityZMax = 10f;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            // Find all objects with a SpherePhysics component
            SpherePhysics[] spherePhysicsObjects = FindObjectsOfType<SpherePhysics>();

            foreach (var spherePhysics in spherePhysicsObjects)
            {
                // Randomize the position for each object
                Vector3 randomPosition = new Vector3(
                    Random.Range(xMin, xMax),
                    Random.Range(yMin, yMax),
                    Random.Range(zMin, zMax)
                );

                // Apply the new position to the object
                spherePhysics.transform.position = randomPosition;

                // Randomize the velocity for each object in X and Z axes (Y velocity stays the same)
                Vector3 currentVelocity = spherePhysics.velocity;  
                Vector3 randomVelocity = new Vector3(
                    Random.Range(velocityXMin, velocityXMax),  
                    currentVelocity.y, 
                    Random.Range(velocityZMin, velocityZMax)  
                );

                // Apply the new velocity to the object
                spherePhysics.velocity = randomVelocity;
            }
        }
    }
}
