using UnityEngine;
using CarEvolution.Sensors;
using CarEvolution.NeuralNet;
using CarEvolution.Pathfinding;

namespace CarEvolution.Car
{
    /// <summary>
    /// Ties sensors + brain + controller together for one car and tracks its
    /// fitness and whether it's still alive (crashed into a wall or stuck
    /// too long).
    ///
    /// Two fitness modes, picked automatically depending on whether
    /// PopulationManager hands this agent a GoalDistanceField:
    ///  - Point-to-point maze (goalField present): fitness is driven by real
    ///    A*-measured progress toward the finish (see ComputeFitness).
    ///  - Closed-loop track (no goalField): falls back to the original
    ///    checkpoint-index based fitness.
    /// </summary>
    [RequireComponent(typeof(CarSensors))]
    [RequireComponent(typeof(CarController))]
    [RequireComponent(typeof(Rigidbody))]
    public class CarAgent : MonoBehaviour
    {
        [Header("Survival Tuning")]
        public float stuckSpeedThreshold = 0.5f;
        public float stuckTimeout = 3f;

        [Header("Goal Detection (maze mode only)")]
        [Tooltip("How close (meters) counts as reaching the finish.")]
        public float goalReachedDistance = 1.5f;

        public bool IsAlive { get; private set; } = true;
        public float DistanceFitness { get; private set; }
        public int LastCheckpointIndex { get; private set; } = -1;
        public bool CrashedIntoWall { get; private set; }
        public float ProgressFitness { get; private set; }
        public bool ReachedGoal { get; private set; }
        public float[] LastSensorReadings => sensors.lastReadings;
        public Vector3[] LastSensorHitPoints => sensors.lastHitPoints;
        /// <summary>Diagnostics only - current rigidbody speed (m/s) and how long it's been under stuckSpeedThreshold.</summary>
        public float DebugCurrentSpeed => controller != null ? controller.CurrentSpeed : -1f;
        public float DebugStuckTimer => stuckTimer;

        CarSensors sensors;
        CarController controller;
        Rigidbody rb;
        NeuralNetwork brain;
        GoalDistanceField goalField;
        Vector3 lastPosition;
        float aliveTimer;
        float stuckTimer;
        float bestDistanceToGoal;

        void Awake()
        {
            sensors = GetComponent<CarSensors>();
            controller = GetComponent<CarController>();
            rb = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// Assigns a brain + physics profile + sensor count for this run.
        /// goalField is optional - pass null for a closed-loop track with no
        /// single finish point (checkpoint-index fitness is used instead).
        /// </summary>
        public void Init(NeuralNetwork network, VehicleProfile profile, int sensorCount, GoalDistanceField goalField = null)
        {
            brain = network;
            this.goalField = goalField;
            sensors.sensorCount = sensorCount;
            controller.ApplyProfile(profile);
            lastPosition = transform.position;
            IsAlive = true;
            DistanceFitness = 0f;
            LastCheckpointIndex = -1;
            CrashedIntoWall = false;
            ProgressFitness = 0f;
            ReachedGoal = false;
            aliveTimer = 0f;
            stuckTimer = 0f;
            bestDistanceToGoal = goalField != null ? goalField.DistanceTo(transform.position) : 0f;
        }

        public void ResetToSpawn(Vector3 pos, Quaternion rot)
        {
            transform.SetPositionAndRotation(pos, rot);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            lastPosition = pos;
            IsAlive = true;
            DistanceFitness = 0f;
            LastCheckpointIndex = -1;
            CrashedIntoWall = false;
            ProgressFitness = 0f;
            ReachedGoal = false;
            aliveTimer = 0f;
            stuckTimer = 0f;
            gameObject.SetActive(true);
        }

        [Header("Off-Track Safety Net")]
        [Tooltip("A car that gets physically launched off a wall/corner at speed would otherwise never crash or go 'stuck' - it's still moving fast, just not on the track - so it'd survive forever and its bogus fitness would dominate selection. Anything that leaves this height band is killed immediately.")]
        public float minSaneHeight = -3f;
        public float maxSaneHeight = 12f;

        void FixedUpdate()
        {
            if (!IsAlive || brain == null) return;

            aliveTimer += Time.fixedDeltaTime;

            float[] inputs = sensors.Sense();
            float[] outputs = brain.Forward(inputs);
            controller.Drive(Mathf.Clamp(outputs[0], -1f, 1f), Mathf.Clamp(outputs[1], -1f, 1f));

            float moved = Vector3.Distance(transform.position, lastPosition);
            DistanceFitness += moved;
            lastPosition = transform.position;

            if (transform.position.y < minSaneHeight || transform.position.y > maxSaneHeight)
            {
                CrashedIntoWall = true; // reuse the crash penalty - flying off the track is just as bad as hitting a wall
                Kill();
                return;
            }

            if (goalField != null)
                UpdateGoalProgress();

            if (controller.CurrentSpeed < stuckSpeedThreshold)
            {
                stuckTimer += Time.fixedDeltaTime;
                if (stuckTimer > stuckTimeout) Kill();
            }
            else
            {
                stuckTimer = 0f;
            }
        }

        /// <summary>
        /// A* gives the TRUE distance through the maze to the goal (walls
        /// and all), not a straight line through them. Reward is only
        /// granted when that distance hits a new personal best - so
        /// wiggling back and forth near the start earns nothing, but real
        /// progress (even indirect, around a corner) does.
        /// </summary>
        void UpdateGoalProgress()
        {
            float d = goalField.DistanceTo(transform.position);
            if (d < bestDistanceToGoal)
            {
                ProgressFitness += bestDistanceToGoal - d;
                bestDistanceToGoal = d;
            }

            if (!ReachedGoal && d <= goalReachedDistance)
                ReachedGoal = true;
        }

        /// <summary>Called by CheckpointTrigger when this car passes a checkpoint (closed-loop tracks).</summary>
        public void OnCheckpoint(int index)
        {
            if (index > LastCheckpointIndex)
                LastCheckpointIndex = index;
        }

        public float ComputeFitness()
        {
            float crashPenalty = CrashedIntoWall ? 200f : 0f;

            if (goalField != null)
            {
                // Maze mode: real geodesic progress toward the finish is the
                // dominant signal, reaching it is a big flat bonus. Raw
                // distance moved and survival time are only small
                // tie-breakers.
                float goalBonus = ReachedGoal ? 500f : 0f;
                return goalBonus + (ProgressFitness * 10f) + (DistanceFitness * 0.02f) + (aliveTimer * 0.1f) - crashPenalty;
            }

            // Closed-loop mode: checkpoints reached is the dominant signal
            // (it's the only thing that actually proves the car followed
            // the road). Raw distance is deliberately weak - a car wedged
            // against a wall and vibrating in place racks up "distance"
            // every physics step without driving anywhere.
            return (LastCheckpointIndex * 100f) + (DistanceFitness * 0.02f) + (aliveTimer * 0.1f) - crashPenalty;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (collision.collider.CompareTag("Wall"))
            {
                CrashedIntoWall = true;
                Kill();
            }
        }

        void Kill() => IsAlive = false;
    }
}
