using UnityEngine;

namespace ReturnByDeath.Data
{
    internal struct ScrapCheckpointData
    {
        internal ulong NetworkObjectId;
        internal string ItemName;
        internal int SlotIndex;
        internal Vector3 Position;
        internal Quaternion Rotation;
        internal bool IsHeld;
        internal ulong HeldByPlayerClientId;

        internal int ScrapValue;
        internal int ShotgunAmmo;
        internal bool IsApparatusInserted;

        public bool IsBeingUsed;

        internal bool HasBattery { get; set; }
        internal float BatteryCharge { get; set; }
        internal bool IsBatteryEmpty { get; set; }
    }
}