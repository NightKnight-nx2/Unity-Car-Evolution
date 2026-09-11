using UnityEngine;
using CarEvolution.Car;
using CarEvolution.Track;

namespace CarEvolution.Simulation
{
    /// <summary>All the knobs an experiment needs, exposed to the Inspector.</summary>
    public class SimulationConfig : MonoBehaviour
    {
        [Header("Evolution")]
        [Tooltip("0 = run forever.")]
        public int maxGenerations = 0;
        public int populationSize = 40;
        [Range(3, 7)] public int sensorCount = 5;
        public int hiddenLayerSize = 8;
        [Range(0f, 1f)] public float mutationRate = 0.08f;
        public float mutationStrength = 0.5f;
        public int elitismCount = 2;
        public int tournamentSize = 4;

        [Header("Generation Timing")]
        public float generationTimeLimit = 30f;

        [Header("Persistence")]
        [Tooltip("Save the population's weights to disk after every generation, so progress survives Stop/Play and Editor restarts.")]
        public bool saveProgress = true;
        [Tooltip("On Start, load a previously-saved population for this track instead of starting from random weights (if a save file exists and its weight count still matches sensorCount/hiddenLayerSize).")]
        public bool loadSavedProgress = true;
        [Tooltip("Unique name for the save file, so different experiments on the same track don't overwrite each other. Leave blank to key the save off the track's name.")]
        public string saveSlotName = "";

        [Header("World")]
        public TrackDefinition track;
        public VehicleProfile vehicleProfile;
        public CarAgent carPrefab;
    }
}
