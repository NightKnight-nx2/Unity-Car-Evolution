namespace CarEvolution.GA
{
    [System.Serializable]
    public class Genome
    {
        public float[] weights;
        public float fitness;

        public Genome(float[] weights)
        {
            this.weights = weights;
            fitness = 0f;
        }

        public Genome Clone()
        {
            var copy = new float[weights.Length];
            System.Array.Copy(weights, copy, weights.Length);
            return new Genome(copy);
        }
    }
}
