using System.Collections.Generic;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace ReturnByDeath.Data
{
    internal class PlayerScrapState
    {
        internal List<ScrapCheckpointData> HeldItems { get; set; } = new List<ScrapCheckpointData>();
        internal List<WorldScrapCheckpointData> WorldScraps { get; set; } = new List<WorldScrapCheckpointData>();
        internal int ActiveSlotIndex { get; set; } = 0;
        public bool IsHoldingUtilSlot { get; set; } = false;

        internal static PlayerScrapState Capture(PlayerControllerB player)
        {
            PlayerScrapState state = new PlayerScrapState();
            if (player == null) return state;

            state.ActiveSlotIndex = player.currentItemSlot;
            state.IsHoldingUtilSlot = (player.currentItemSlot == 50 ||
                (player.ItemOnlySlot != null && player.currentlyHeldObjectServer == player.ItemOnlySlot));

            // 1. Quét Standard Slots
            if (player.ItemSlots != null)
            {
                for (int i = 0; i < player.ItemSlots.Length; i++)
                {
                    GrabbableObject item = player.ItemSlots[i];
                    if (item == null) continue;

                    var netObj = item.GetComponent<NetworkObject>();
                    if (netObj == null) continue;

                    state.HeldItems.Add(CreateDataFromItem(item, i, player.actualClientId));
                }
            }

            // 2. Quét Utility Slot
            if (player.ItemOnlySlot != null)
            {
                GrabbableObject utilItem = player.ItemOnlySlot;
                var netObj = utilItem.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    state.HeldItems.Add(CreateDataFromItem(utilItem, 50, player.actualClientId));
                }
            }

            // 3. Quét TOÀN BỘ item trên thế giới để lưu vị trí ban đầu
            GrabbableObject[] allWorldItems = Object.FindObjectsByType<GrabbableObject>(FindObjectsSortMode.None);
            foreach (var item in allWorldItems)
            {
                if (item == null) continue;
                var netObj = item.GetComponent<NetworkObject>();
                if (netObj == null) continue;

                state.WorldScraps.Add(new WorldScrapCheckpointData
                {
                    NetworkObjectId = netObj.NetworkObjectId,
                    Position = item.transform.position,
                    Rotation = item.transform.rotation,
                    IsInShipRoom = item.isInShipRoom,
                    IsInElevator = item.isInElevator
                });
            }

            return state;
        }

        private static ScrapCheckpointData CreateDataFromItem(GrabbableObject item, int slotIndex, ulong clientId)
        {
            var netObj = item.GetComponent<NetworkObject>();
            int extraDataAmmo = 0;
            if (item is ShotgunItem shotgun)
            {
                extraDataAmmo = shotgun.shellsLoaded;
            }

            bool hasBattery = item.itemProperties != null && item.itemProperties.requiresBattery && item.insertedBattery != null;

            return new ScrapCheckpointData
            {
                NetworkObjectId = netObj.NetworkObjectId,
                ItemName = item.itemProperties != null ? item.itemProperties.itemName : "Unknown",
                ItemItemId = item.itemProperties != null ? item.itemProperties.itemId : -1,
                SlotIndex = slotIndex,
                Position = item.transform.position,
                Rotation = item.transform.rotation,
                IsHeld = true,
                HeldByPlayerClientId = clientId,
                ScrapValue = item.scrapValue,
                ShotgunAmmo = extraDataAmmo,
                IsBeingUsed = item.isBeingUsed,
                HasBattery = hasBattery,
                BatteryCharge = hasBattery ? item.insertedBattery.charge : 1f,
                IsBatteryEmpty = hasBattery ? item.insertedBattery.empty : false
            };
        }
    }
}