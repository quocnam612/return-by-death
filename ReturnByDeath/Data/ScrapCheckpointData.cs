namespace ReturnByDeath.Data
{
    internal class ScrapCheckpointData
    {
        internal ulong NetworkObjectId { get; set; }
        internal string ItemName { get; set; }
        internal int SlotIndex { get; set; }
        internal UnityEngine.Vector3 Position { get; set; }
        internal UnityEngine.Quaternion Rotation { get; set; }
        internal bool IsHeld { get; set; }
        internal ulong HeldByPlayerClientId { get; set; }
        internal int ScrapValue { get; set; }
        internal int ShotgunAmmo { get; set; }
        internal bool IsBeingUsed { get; set; }

        internal bool HasBattery { get; set; }
        internal float BatteryCharge { get; set; } = 1f;
        internal bool IsBatteryEmpty { get; set; }

        internal int ItemItemId { get; set; }
    }
}