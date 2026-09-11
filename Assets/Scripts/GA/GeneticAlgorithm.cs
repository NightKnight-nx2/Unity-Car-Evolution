using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarEvolution.GA
{
    /// <summary>
    /// Classic generational GA: elitism + tournament selection + uniform
    /// crossover + gaussian mutation. Operates on flat float[] genomes, so it
    /// has no idea it's evolving neural network weights specifically.
    /// </summary>
    public class GeneticAlgorithm
    {
        public readonly int populationSize;
        public readonly int weightCount;
        public int elitismCount;
        public float mutationRate;
        public float mutationStrength;
        public int tournamentSize = 4;

        readonly System.Random rng;

        public GeneticAlgorithm(int populationSize, int weightCount, int elitismCount,
            float mutationRate, float mutationStrength, int seed = 0)
        {
            this.populationSize = populationSize;
            this.weightCount = weightCount;
            this.elitismCount = Mathf.Clamp(elitismCount, 0, populationSize);
            this.mutationRate = mutationRate;
            this.mutationStrength = mutationStrength;
            rng = seed == 0 ? new System.Random() : new System.Random(seed);
        }

        public List<Genome> CreateInitialPopulation()
        {
            var pop = new List<Genome>(populationSize);
            for (int i = 0; i < populationSize; i++)
                pop.Add(new Genome(RandomWeights()));
            return pop;
        }

        float[] RandomWeights()
        {
            var w = new float[weightCount];
            for (int i = 0; i < weightCount; i++)
                w[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
            return w;
        }

        /// <summary>Takes a population whose .fitness has already been evaluated and produces the next generation.</summary>
        public List<Genome> Evolve(List<Genome> evaluated)
        {
            evaluated.Sort((a, b) => b.fitness.CompareTo(a.fitness));

            var next = new List<Genome>(populationSize);

            for (int i = 0; i < elitismCount && i < evaluated.Count; i++)
                next.Add(evaluated[i].Clone());

            while (next.Count < populationSize)
            {
                Genome parentA = TournamentSelect(evaluated);
                Genome parentB = TournamentSelect(evaluated);
                float[] childWeights = Crossover(parentA.weights, parentB.weights);
                Mutate(childWeights);
                next.Add(new Genome(childWeights));
            }

            return next;
        }

        Genome TournamentSelect(List<Genome> pop)
        {
            Genome best = null;
            for (int i = 0; i < tournamentSize; i++)
            {
                var candidate = pop[rng.Next(pop.Count)];
                if (best == null || candidate.fitness > best.fitness)
                    best = candidate;
            }
            return best;
        }

        float[] Crossover(float[] a, float[] b)
        {
            var child = new float[a.Length];
            for (int i = 0; i < a.Length; i++)
                child[i] = rng.NextDouble() < 0.5 ? a[i] : b[i];
            return child;
        }

        void Mutate(float[] weights)
        {
            for (int i = 0; i < weights.Length; i++)
            {
                if (rng.NextDouble() < mutationRate)
                    weights[i] += GaussianRandom() * mutationStrength;
            }
        }

        float GaussianRandom()
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = rng.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2));
        }
    }
}
