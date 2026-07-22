using UnityEngine;

namespace ReturnByDeath.Data
{
    internal class WorldScrapCheckpointData
    {
        internal ulong NetworkObjectId { get; set; }
        internal Vector3 Position { get; set; }
        internal Quaternion Rotation { get; set; }
        internal bool IsInShipRoom { get; set; }
        internal bool IsInElevator { get; set; }
    }
}