using UnityEngine;
using UnityEngine.UI;
using CarEvolution.Simulation;

namespace CarEvolution.Benchmark
{
    /// <summary>
    /// Side-by-side comparison of two PopulationManager runs, e.g.
    /// "50 gen / 3 sensors" vs "500 gen / 7 sensors". Drop two
    /// PopulationManagers (each with its own SimulationConfig + track) into
    /// the scene and point this at both to get a live completion-rate report.
    ///
    /// This is the basic version described in the roadmap: run both
    /// side-by-side in the same scene. A later pass can add "run headlessly
    /// and only render the summary" for faster batch comparisons.
    /// </summary>
    public class BenchmarkReportUI : MonoBehaviour
    {
        public PopulationManager runA;
        public PopulationManager runB;
        public string runALabel = "A: 50 gen / 3 sensors";
        public string runBLabel = "B: 500 gen / 7 sensors";
        public Text reportText;

        void Update()
        {
            if (reportText == null) return;

            reportText.text =
                $"{runALabel}\n  Gen {SafeGen(runA)} - Completion {SafeRate(runA):P0} - Best {SafeBest(runA):F0}\n\n" +
                $"{runBLabel}\n  Gen {SafeGen(runB)} - Completion {SafeRate(runB):P0} - Best {SafeBest(runB):F0}";
        }

        int SafeGen(PopulationManager m) => m != null ? m.CurrentGeneration : 0;
        float SafeRate(PopulationManager m) => m != null ? m.CompletionRate : 0f;
        float SafeBest(PopulationManager m) => m != null ? m.BestFitnessEver : 0f;
    }
}
