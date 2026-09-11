using System;

namespace CarEvolution.NeuralNet
{
    /// <summary>
    /// Minimal feedforward network: input -> hidden (tanh) -> output (tanh).
    /// Weights (incl. biases) are exposed as a flat float[] so the genetic
    /// algorithm can treat a whole network as a single genome, with no idea
    /// it's evolving anything neural-network shaped.
    /// </summary>
    [Serializable]
    public class NeuralNetwork
    {
        public readonly int inputCount;
        public readonly int hiddenCount;
        public readonly int outputCount;

        // weightsIH: hiddenCount * inputCount, biasesH: hiddenCount
        // weightsHO: outputCount * hiddenCount, biasesO: outputCount
        readonly float[] weightsIH;
        readonly float[] biasesH;
        readonly float[] weightsHO;
        readonly float[] biasesO;

        public NeuralNetwork(int inputCount, int hiddenCount, int outputCount)
        {
            this.inputCount = inputCount;
            this.hiddenCount = hiddenCount;
            this.outputCount = outputCount;

            weightsIH = new float[hiddenCount * inputCount];
            biasesH = new float[hiddenCount];
            weightsHO = new float[outputCount * hiddenCount];
            biasesO = new float[outputCount];
        }

        public int WeightCount => weightsIH.Length + biasesH.Length + weightsHO.Length + biasesO.Length;

        public float[] GetWeights()
        {
            float[] all = new float[WeightCount];
            int idx = 0;
            Array.Copy(weightsIH, 0, all, idx, weightsIH.Length); idx += weightsIH.Length;
            Array.Copy(biasesH, 0, all, idx, biasesH.Length); idx += biasesH.Length;
            Array.Copy(weightsHO, 0, all, idx, weightsHO.Length); idx += weightsHO.Length;
            Array.Copy(biasesO, 0, all, idx, biasesO.Length);
            return all;
        }

        public void SetWeights(float[] all)
        {
            if (all.Length != WeightCount)
                throw new ArgumentException($"Expected {WeightCount} weights, got {all.Length}");

            int idx = 0;
            Array.Copy(all, idx, weightsIH, 0, weightsIH.Length); idx += weightsIH.Length;
            Array.Copy(all, idx, biasesH, 0, biasesH.Length); idx += biasesH.Length;
            Array.Copy(all, idx, weightsHO, 0, weightsHO.Length); idx += weightsHO.Length;
            Array.Copy(all, idx, biasesO, 0, biasesO.Length);
        }

        /// <summary>inputs: sensor distances. outputs: [steer, throttle/brake], both roughly -1..1.</summary>
        public float[] Forward(float[] inputs)
        {
            if (inputs.Length != inputCount)
                throw new ArgumentException($"Expected {inputCount} inputs, got {inputs.Length}");

            float[] hidden = new float[hiddenCount];
            for (int h = 0; h < hiddenCount; h++)
            {
                float sum = biasesH[h];
                for (int i = 0; i < inputCount; i++)
                    sum += weightsIH[h * inputCount + i] * inputs[i];
                hidden[h] = (float)Math.Tanh(sum);
            }

            float[] outputs = new float[outputCount];
            for (int o = 0; o < outputCount; o++)
            {
                float sum = biasesO[o];
                for (int h = 0; h < hiddenCount; h++)
                    sum += weightsHO[o * hiddenCount + h] * hidden[h];
                outputs[o] = (float)Math.Tanh(sum);
            }

            return outputs;
        }
    }
}
