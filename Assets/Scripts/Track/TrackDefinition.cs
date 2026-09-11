using UnityEngine;

namespace CarEvolution.Track
{
    /// <summary>
    /// Data-only description of a track: a 2D centerline + width. TrackBuilder
    /// turns this into walls, road mesh and checkpoints. Adding a new track
    /// is just adding a new asset of this type - no new code required.
    /// </summary>
    [CreateAssetMenu(fileName = "TrackDefinition", menuName = "Car Evolution/Track Definition")]
    public class TrackDefinition : ScriptableObject
    {
        public string trackName = "Wide Oval";
        public float trackWidth = 10f;
        public bool closedLoop = true;
        public Vector2[] centerline;
    }
}
