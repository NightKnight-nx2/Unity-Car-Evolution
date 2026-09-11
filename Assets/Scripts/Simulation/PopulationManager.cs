using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using CarEvolution.Car;
using CarEvolution.GA;
using CarEvolution.NeuralNet;
using CarEvolution.Pathfinding;

namespace CarEvolution.Simulation
{
    /// <summary>
    /// Runs the generation loop: spawn N cars from the current population,
    /// let them drive until they're all dead or time runs out, score them,
    /// evolve, repeat. Also tracks the "did it finish the track" completion
    /// rate used by the benchmark UI.
    ///
    /// For an open (point-to-point) track, all cars spawn on the exact same
    /// point and a GoalDistanceField (A* distance-to-finish, precomputed
    /// once) shapes their fitness - see CarAgent.ComputeFitness. For a
    /// closed-loop track there's no single finish point, so this falls back
    /// to the original checkpoint-index fitness.
    /// </summary>
    public class PopulationManager : MonoBehaviour
    {
        public SimulationConfig config;
        public Transform spawnPoint;

        public int CurrentGeneration { get; private set; } = 1;
        public float BestFitnessEver { get; private set; }
        public CarAgent BestAgent { get; private set; }
        public float CompletionRate => totalAgentsEvaluated == 0 ? 0f : (float)totalCompletions / totalAgentsEvaluated;

        /// <summary>How many cars in the CURRENT generation are still alive right now (diagnostic - if this hits 0 within a second or two of every generation starting, something is killing the whole population almost instantly, e.g. a spawn/geometry problem rather than a training-difficulty problem).</summary>
        public int AliveCount { get; private set; }
        public int PopulationCount => agents.Count;
        /// <summary>Best fitness among cars alive RIGHT NOW in this generation (as opposed to BestFitnessEver, which is the best ever recorded across all past generations).</summary>
        public float BestFitnessLive { get; private set; }

        GeneticAlgorithm ga;
        List<Genome> population;
        readonly List<CarAgent> agents = new List<CarAgent>();
        GoalDistanceField goalField;
        float generationTimer;
        bool running;
        int totalAgentsEvaluated;
        int totalCompletions;

        void Start()
        {
            if (config == null || config.carPrefab == null || config.track == null || config.vehicleProfile == null)
            {
                Debug.LogError("PopulationManager: config, config.carPrefab, config.track and config.vehicleProfile must all be assigned.");
                return;
            }

            // Cars shouldn't push each other around - they all spawn on the
            // exact same point now, which would otherwise make the physics
            // engine shove the whole population apart on the first frame.
            // They stay solid against everything else (walls, road), just
            // not each other.
            int carLayer = LayerMask.NameToLayer("Car");
            if (carLayer >= 0)
                Physics.IgnoreLayerCollision(carLayer, carLayer, true);
            else
                Debug.LogWarning("PopulationManager: 'Car' layer not found - re-run 'Car Evolution > 2. Build Simulation Scene' to create it, otherwise cars will collide with each other.");

            // An open track (closedLoop == false) has a real start and a
            // real finish, so build the A* distance-to-finish field once.
            // A closed loop has no distinct finish point, so leave
            // goalField null and CarAgent falls back to checkpoint fitness.
            if (!config.track.closedLoop && config.track.centerline.Length >= 2)
            {
                Vector2[] cl = config.track.centerline;
                Vector2 goal = cl[cl.Length - 1];
                goalField = new GoalDistanceField(cl, config.track.trackWidth, config.track.closedLoop, goal);
            }

            var probe = new NeuralNetwork(config.sensorCount, config.hiddenLayerSize, 2);
            int weightCount = probe.WeightCount;

            ga = new GeneticAlgorithm(config.populationSize, weightCount, config.elitismCount,
                config.mutationRate, config.mutationStrength) { tournamentSize = config.tournamentSize };

            if (!(config.loadSavedProgress && TryLoadProgress(weightCount)))
                population = ga.CreateInitialPopulation();

            SpawnGeneration();
            running = true;
        }

        /// <summary>Where this track's population save file lives (one file per track/slot under Application.persistentDataPath, so it survives Editor restarts and isn't cleaned up with Library/Temp).</summary>
        string SaveFilePath()
        {
            string slot = string.IsNullOrEmpty(config.saveSlotName) ? config.track.trackName : config.saveSlotName;
            foreach (char c in Path.GetInvalidFileNameChars())
                slot = slot.Replace(c, '_');
            return Path.Combine(Application.persistentDataPath, $"population_{slot}.json");
        }

        /// <summary>Attempts to resume a previously-saved population for this track. Returns false (leaving `population` untouched, so the caller falls back to a fresh random population) if there's no save file, it's corrupt, or its genomes don't match the current sensorCount/hiddenLayerSize.</summary>
        bool TryLoadProgress(int expectedWeightCount)
        {
            string path = SaveFilePath();
            if (!File.Exists(path)) return false;

            try
            {
                var data = JsonUtility.FromJson<PopulationSaveData>(File.ReadAllText(path));
                if (data?.genomes == null || data.genomes.Length == 0) return false;

                if (data.weightCount != expectedWeightCount)
                {
                    Debug.LogWarning($"PopulationManager: save file '{path}' has {data.weightCount} weights/genome but the current sensorCount/hiddenLayerSize needs {expectedWeightCount} - ignoring it and starting fresh. (Match the old settings, or delete the file, to resume it.)");
                    return false;
                }

                population = new List<Genome>(data.genomes);
                CurrentGeneration = Mathf.Max(1, data.generation);
                BestFitnessEver = data.bestFitnessEver;
                Debug.Log($"PopulationManager: resumed from '{path}' - generation {CurrentGeneration}, best fitness so far {BestFitnessEver:F1}.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"PopulationManager: couldn't read save file '{path}' ({e.Message}) - starting fresh.");
                return false;
            }
        }

        void SaveProgress()
        {
            if (!config.saveProgress || population == null || population.Count == 0) return;

            try
            {
                var data = new PopulationSaveData
                {
                    generation = CurrentGeneration,
                    bestFitnessEver = BestFitnessEver,
                    weightCount = ga.weightCount,
                    genomes = population.ToArray(),
                };
                File.WriteAllText(SaveFilePath(), JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"PopulationManager: failed to save progress ({e.Message}).");
            }
        }

        [Serializable]
        class PopulationSaveData
        {
            public int generation;
            public float bestFitnessEver;
            public int weightCount;
            public Genome[] genomes;
        }

        void SpawnGeneration()
        {
            Vector3 pos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

            while (agents.Count < population.Count)
            {
                var agentObj = Instantiate(config.carPrefab, pos, rot);
                agents.Add(agentObj);
            }

            // Every car starts on the exact same point/orientation. This
            // used to require spreading cars into a grid to stop them
            // exploding apart on spawn, but car-vs-car collisions are now
            // disabled entirely (see Start), so stacking them is safe.
            for (int i = 0; i < population.Count; i++)
            {
                var net = new NeuralNetwork(config.sensorCount, config.hiddenLayerSize, 2);
                net.SetWeights(population[i].weights);

                agents[i].ResetToSpawn(pos, rot);
                agents[i].Init(net, config.vehicleProfile, config.sensorCount, goalField);
            }

            generationTimer = 0f;
            AliveCount = agents.Count;
            BestFitnessLive = 0f;
        }

        void FixedUpdate()
        {
            if (!running) return;

            generationTimer += Time.fixedDeltaTime;

            bool anyAlive = false;
            int aliveCount = 0;
            CarAgent frontRunner = null;
            float bestLive = float.MinValue;

            foreach (var a in agents)
            {
                if (a.IsAlive)
                {
                    anyAlive = true;
                    aliveCount++;
                    float f = a.ComputeFitness();
                    if (f > bestLive) { bestLive = f; frontRunner = a; }
                }
            }
            AliveCount = aliveCount;
            if (frontRunner != null) { BestAgent = frontRunner; BestFitnessLive = bestLive; }

            if (!anyAlive || generationTimer >= config.generationTimeLimit)
                FinishGeneration();
        }

        void FinishGeneration()
        {
            int lastCheckpointIndex = config.track.centerline.Length - 1;
            bool useGoalField = goalField != null;

            // Normally agents.Count == population.Count exactly (SpawnGeneration
            // grows agents to match, and GA.Evolve always outputs exactly
            // populationSize genomes) - but a resumed save whose genome count
            // doesn't match the current populationSize can briefly disagree,
            // so guard the shorter of the two rather than risk an index
            // exception on population[i].
            int evalCount = Mathf.Min(agents.Count, population.Count);
            for (int i = 0; i < evalCount; i++)
            {
                population[i].fitness = agents[i].ComputeFitness();
                totalAgentsEvaluated++;

                bool completed = useGoalField
                    ? agents[i].ReachedGoal
                    : agents[i].LastCheckpointIndex >= lastCheckpointIndex;
                if (completed) totalCompletions++;
            }

            float genBest = 0f;
            foreach (var g in population) if (g.fitness > genBest) genBest = g.fitness;
            if (genBest > BestFitnessEver) BestFitnessEver = genBest;

            if (config.maxGenerations > 0 && CurrentGeneration >= config.maxGenerations)
            {
                running = false;
                SaveProgress();
                Debug.Log($"Simulation finished after {CurrentGeneration} generations. Best fitness: {BestFitnessEver:F1}, completion rate: {CompletionRate:P0}");
                return;
            }

            population = ga.Evolve(population);
            CurrentGeneration++;
            SaveProgress();
            SpawnGeneration();
        }
    }
}
