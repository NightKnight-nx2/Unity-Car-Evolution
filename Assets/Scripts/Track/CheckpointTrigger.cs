using UnityEngine;
using CarEvolution.Car;

namespace CarEvolution.Track
{
    [RequireComponent(typeof(Collider))]
    public class CheckpointTrigger : MonoBehaviour
    {
        public int index;

        void OnTriggerEnter(Collider other)
        {
            var agent = other.GetComponentInParent<CarAgent>();
            if (agent != null) agent.OnCheckpoint(index);
        }
    }
}
