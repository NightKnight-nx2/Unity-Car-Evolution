using UnityEngine;

namespace CarEvolution.Sensors
{
    /// <summary>
    /// Fires N raycasts fanned out in front of the car and returns normalized
    /// distances (0 = wall right at the car, 1 = nothing hit within range).
    /// This is the only thing the neural network "sees".
    /// </summary>
    public class CarSensors : MonoBehaviour
    {
        [Header("Sensor Setup")]
        [Range(3, 7)] public int sensorCount = 5;
        public float sensorLength = 20f;
        [Tooltip("Total angle (degrees) the sensor fan covers, centered on forward.")]
        public float fieldOfView = 180f;
        public LayerMask wallMask;
        public Transform sensorOrigin;

        [HideInInspector] public Vector3[] lastHitPoints;
        [HideInInspector] public float[] lastReadings;

        void Awake()
        {
            if (sensorOrigin == null) sensorOrigin = transform;
            lastHitPoints = new Vector3[sensorCount];
            lastReadings = new float[sensorCount];
        }

        public float[] Sense()
        {
            if (lastReadings == null || lastReadings.Length != sensorCount)
            {
                lastReadings = new float[sensorCount];
                lastHitPoints = new Vector3[sensorCount];
            }

            Vector3 origin = sensorOrigin.position;
            float startAngle = -fieldOfView * 0.5f;
            float step = sensorCount > 1 ? fieldOfView / (sensorCount - 1) : 0f;

            for (int i = 0; i < sensorCount; i++)
            {
                float angle = startAngle + step * i;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * sensorOrigin.forward;

                if (Physics.Raycast(origin, dir, out RaycastHit hit, sensorLength, wallMask))
                {
                    lastReadings[i] = hit.distance / sensorLength;
                    lastHitPoints[i] = hit.point;
                }
                else
                {
                    lastReadings[i] = 1f;
                    lastHitPoints[i] = origin + dir * sensorLength;
                }
            }

            return lastReadings;
        }

        void OnDrawGizmosSelected()
        {
            if (lastHitPoints == null) return;
            Gizmos.color = Color.red;
            Vector3 origin = sensorOrigin != null ? sensorOrigin.position : transform.position;
            foreach (var p in lastHitPoints)
                Gizmos.DrawLine(origin, p);
        }
    }
}
