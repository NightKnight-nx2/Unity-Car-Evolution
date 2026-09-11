using UnityEngine;
using UnityEngine.UI;
using CarEvolution.Simulation;

namespace CarEvolution.UI
{
    /// <summary>Shows generation number + best fitness, and draws the current best car's sensor rays.</summary>
    public class SimulationHUD : MonoBehaviour
    {
        public PopulationManager populationManager;
        public Text generationText;
        public Text fitnessText;
        [Tooltip("Optional: shows 'Alive: X/Y' + this generation's current best fitness. Useful for diagnosing whether the whole population is dying almost instantly (Alive hits 0 within a second or two of every generation) vs. surviving but just not learning yet.")]
        public Text statusText;
        public LineRenderer[] sensorRayVisuals;

        void Update()
        {
            if (populationManager == null) return;

            if (generationText != null)
                generationText.text = $"Generation: {populationManager.CurrentGeneration}";
            if (fitnessText != null)
                fitnessText.text = $"Best fitness: {populationManager.BestFitnessEver:F1}";
            if (statusText != null)
            {
                string line = $"Alive: {populationManager.AliveCount}/{populationManager.PopulationCount}   Current best: {populationManager.BestFitnessLive:F1}";
                var best = populationManager.BestAgent;
                if (best != null)
                {
                    Vector3 p = best.transform.position;
                    line += $"\nBest car: speed={best.DebugCurrentSpeed:F2}  stuckTimer={best.DebugStuckTimer:F1}  dist={best.DistanceFitness:F1}  pos=({p.x:F1},{p.z:F1})";
                }
                statusText.text = line;
            }

            DrawBestAgentSensors();
        }

        void DrawBestAgentSensors()
        {
            var best = populationManager.BestAgent;
            if (best == null || sensorRayVisuals == null) return;

            var hits = best.LastSensorHitPoints;
            if (hits == null) return;

            for (int i = 0; i < sensorRayVisuals.Length; i++)
            {
                if (sensorRayVisuals[i] == null) continue;
                if (i < hits.Length)
                {
                    sensorRayVisuals[i].gameObject.SetActive(true);
                    sensorRayVisuals[i].positionCount = 2;
                    sensorRayVisuals[i].SetPosition(0, best.transform.position);
                    sensorRayVisuals[i].SetPosition(1, hits[i]);
                }
                else
                {
                    sensorRayVisuals[i].gameObject.SetActive(false);
                }
            }
        }
    }
}
