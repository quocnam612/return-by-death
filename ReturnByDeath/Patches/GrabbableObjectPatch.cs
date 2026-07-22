using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GameNetcodeStuff;
using HarmonyLib;
using ReturnByDeath.Data;
using Unity.Netcode;
using UnityEngine;

namespace ReturnByDeath.Patches
{
    [HarmonyPatch(typeof(GrabbableObject))]
    internal static class GrabbableObjectPatch
    {
        private static readonly MethodInfo SwitchToItemSlotMethod =
            AccessTools.Method(typeof(PlayerControllerB), "SwitchToItemSlot", new Type[] { typeof(int), typeof(GrabbableObject) });

        internal static void RestoreAllScrapData(
            PlayerControllerB localPlayer,
            List<ScrapCheckpointData> savedHeldScraps,
            List<WorldScrapCheckpointData> savedWorldScraps,
            int activeSlot,
            bool isHoldingUtilSlot = false)
        {
            if (localPlayer == null || savedHeldScraps == null) return;

            if (localPlayer.ItemSlots == null || localPlayer.ItemSlots.Length == 0) return;
            if (activeSlot < 0 || activeSlot >= localPlayer.ItemSlots.Length)
            {
                activeSlot = Mathf.Clamp(activeSlot, 0, localPlayer.ItemSlots.Length - 1);
            }

            // 1. Un-equip & thả sạch đồ đang cầm
            localPlayer.DropAllHeldItems();
            localPlayer.isGrabbingObjectAnimation = false;
            localPlayer.inSpecialInteractAnimation = false;
            localPlayer.disableInteract = false;
            localPlayer.twoHanded = false;
            localPlayer.twoHandedAnimation = false;

            if (localPlayer.playerBodyAnimator != null)
            {
                localPlayer.playerBodyAnimator.SetBool("TwoHandedItem", false);
                localPlayer.playerBodyAnimator.SetBool("grab", false);
                localPlayer.playerBodyAnimator.SetBool("cancelHoldingTwoHanded", true);
            }

            // Clear UI Slots
            if (HUDManager.Instance != null && HUDManager.Instance.itemSlotIcons != null)
            {
                for (int i = 0; i < HUDManager.Instance.itemSlotIcons.Length; i++)
                {
                    if (HUDManager.Instance.itemSlotIcons[i] != null)
                    {
                        HUDManager.Instance.itemSlotIcons[i].enabled = false;
                    }
                }
            }

            if (HUDManager.Instance != null && HUDManager.Instance.itemOnlySlotIcon != null)
            {
                HUDManager.Instance.itemOnlySlotIcon.enabled = false;
            }

            for (int i = 0; i < localPlayer.ItemSlots.Length; i++)
            {
                localPlayer.ItemSlots[i] = null;
            }
            localPlayer.ItemOnlySlot = null;

            float calculatedWeight = 1f;
            GrabbableObject[] allWorldItems = UnityEngine.Object.FindObjectsByType<GrabbableObject>(FindObjectsSortMode.None);

            // 2. TRẢ CÁC ITEM TRÊN BẢN ĐỒ VỀ VỊ TRÍ GỐC KHI SAVE CHECKPOINT
            if (savedWorldScraps != null)
            {
                foreach (var worldData in savedWorldScraps)
                {
                    GrabbableObject worldItem = Array.Find(allWorldItems, x =>
                        x != null &&
                        x.GetComponent<NetworkObject>() != null &&
                        x.GetComponent<NetworkObject>().NetworkObjectId == worldData.NetworkObjectId);

                    if (worldItem == null) continue;

                    if (worldItem.isHeld && worldItem.playerHeldBy != null && worldItem.playerHeldBy != localPlayer)
                    {
                        continue;
                    }

                    bool isHeldInCheckpoint = savedHeldScraps.Exists(h => h.NetworkObjectId == worldData.NetworkObjectId);

                    if (!isHeldInCheckpoint)
                    {
                        ResetItemToWorldPosition(localPlayer, worldItem, worldData);
                    }
                }
            }

            // 3. RESTORE CÁC ITEM TRONG HÀNH TRANG CHECKPOINT
            foreach (var savedData in savedHeldScraps)
            {
                GrabbableObject item = Array.Find(allWorldItems, x =>
                    x != null &&
                    x.GetComponent<NetworkObject>() != null &&
                    x.GetComponent<NetworkObject>().NetworkObjectId == savedData.NetworkObjectId);

                if (item == null) continue;

                HoarderBugPatch.ReleaseItemFromAllBugs(item);

                if (item.isHeld && item.playerHeldBy != null && item.playerHeldBy != localPlayer)
                {
                    continue;
                }

                int slot = savedData.SlotIndex;

                // --- UTIL SLOT (50) ---
                if (slot == 50)
                {
                    localPlayer.ItemOnlySlot = item;
                    SetupItemOnPlayer(localPlayer, item, isEquipped: false);
                    RestoreItemBatteryAndState(item, savedData);

                    if (HUDManager.Instance != null && HUDManager.Instance.itemOnlySlotIcon != null && item.itemProperties != null)
                    {
                        HUDManager.Instance.itemOnlySlotIcon.sprite = item.itemProperties.itemIcon;
                        HUDManager.Instance.itemOnlySlotIcon.enabled = true;
                    }
                    continue;
                }

                // --- STANDARD SLOTS ---
                if (slot >= 0 && slot < localPlayer.ItemSlots.Length)
                {
                    localPlayer.ItemSlots[slot] = item;
                    SetupItemOnPlayer(localPlayer, item, isEquipped: false);

                    if (item.itemProperties != null)
                    {
                        calculatedWeight += Mathf.Max(0f, item.itemProperties.weight - 1f);
                    }

                    if (HUDManager.Instance != null &&
                        HUDManager.Instance.itemSlotIcons != null &&
                        slot < HUDManager.Instance.itemSlotIcons.Length)
                    {
                        if (item.itemProperties != null && item.itemProperties.itemIcon != null)
                        {
                            HUDManager.Instance.itemSlotIcons[slot].sprite = item.itemProperties.itemIcon;
                            HUDManager.Instance.itemSlotIcons[slot].enabled = true;
                        }
                    }

                    if (item is ShotgunItem shotgun)
                    {
                        shotgun.shellsLoaded = savedData.ShotgunAmmo;
                    }

                    RestoreItemBatteryAndState(item, savedData);
                }
            }

            localPlayer.carryWeight = calculatedWeight;

            // 4. Kích hoạt Fast Swap Slot
            localPlayer.StartCoroutine(DelayedSlotEquipRoutine(localPlayer, activeSlot, isHoldingUtilSlot));
        }

        private static void ResetItemToWorldPosition(PlayerControllerB localPlayer, GrabbableObject item, WorldScrapCheckpointData worldData)
        {
            item.isHeld = false;
            item.isPocketed = false;
            item.heldByPlayerOnServer = false;
            item.playerHeldBy = null;
            item.parentObject = null;

            // Transform về vị trí cũ trên mặt đất
            item.transform.position = worldData.Position;
            item.transform.rotation = worldData.Rotation;
            item.targetFloorPosition = item.transform.localPosition;

            if (worldData.IsInElevator && StartOfRound.Instance != null)
            {
                item.transform.SetParent(StartOfRound.Instance.elevatorTransform, worldPositionStays: true);
            }
            else if (StartOfRound.Instance != null)
            {
                item.transform.SetParent(StartOfRound.Instance.propsContainer, worldPositionStays: true);
            }

            localPlayer.SetItemInElevator(worldData.IsInShipRoom, worldData.IsInElevator, item);
            item.EnablePhysics(true);
            item.EnableItemMeshes(true);
        }

        private static void RestoreItemBatteryAndState(GrabbableObject item, ScrapCheckpointData savedData)
        {
            if (savedData.HasBattery && item.insertedBattery != null)
            {
                item.insertedBattery.charge = savedData.BatteryCharge;
                item.insertedBattery.empty = savedData.IsBatteryEmpty || savedData.BatteryCharge <= 0f;
            }

            item.isBeingUsed = savedData.IsBeingUsed;

            if (item is FlashlightItem flashlight)
            {
                bool shouldBeOn = savedData.IsBeingUsed && (item.insertedBattery == null || !item.insertedBattery.empty);
                flashlight.SwitchFlashlight(shouldBeOn);
            }
        }

        private static IEnumerator DelayedSlotEquipRoutine(PlayerControllerB localPlayer, int targetSlot, bool isHoldingUtilSlot)
        {
            yield return null;

            if (isHoldingUtilSlot && localPlayer.ItemOnlySlot != null)
            {
                GrabbableObject utilItem = localPlayer.ItemOnlySlot;

                localPlayer.currentlyHeldObjectServer = utilItem;
                localPlayer.isHoldingObject = true;

                if (localPlayer.playerBodyAnimator != null)
                {
                    localPlayer.playerBodyAnimator.SetBool("GrabValidated", true);
                    localPlayer.playerBodyAnimator.SetBool("cancelHolding", false);
                    localPlayer.playerBodyAnimator.SetTrigger("SwitchHoldAnimation");
                }

                SwitchToItemSlotMethod?.Invoke(localPlayer, new object[] { 50, utilItem });
            }
            else
            {
                GrabbableObject targetItem = (targetSlot >= 0 && targetSlot < localPlayer.ItemSlots.Length)
                    ? localPlayer.ItemSlots[targetSlot]
                    : null;

                if (targetItem != null)
                {
                    localPlayer.currentlyHeldObjectServer = targetItem;
                    localPlayer.isHoldingObject = true;

                    if (localPlayer.playerBodyAnimator != null)
                    {
                        localPlayer.playerBodyAnimator.SetBool("GrabValidated", true);
                        localPlayer.playerBodyAnimator.SetBool("cancelHolding", false);

                        bool isTwoHanded = targetItem.itemProperties != null &&
                            (targetItem.itemProperties.twoHanded || targetItem.itemProperties.twoHandedAnimation);

                        if (isTwoHanded)
                        {
                            localPlayer.playerBodyAnimator.SetTrigger("SwitchHoldAnimationTwoHanded");
                        }
                        else
                        {
                            localPlayer.playerBodyAnimator.SetTrigger("SwitchHoldAnimation");
                        }
                    }
                }

                SwitchToItemSlotMethod?.Invoke(localPlayer, new object[] { targetSlot, targetItem });
            }

            localPlayer.disableInteract = false;
        }

        private static void SetupItemOnPlayer(PlayerControllerB localPlayer, GrabbableObject item, bool isEquipped)
        {
            item.EnablePhysics(false);
            item.hasHitGround = false;
            item.isHeld = true;
            item.heldByPlayerOnServer = true;
            item.playerHeldBy = localPlayer;

            Transform targetHolder = localPlayer.localItemHolder;
            item.parentObject = targetHolder;
            item.transform.SetParent(targetHolder);
            item.transform.localPosition = item.itemProperties.positionOffset;
            item.transform.localRotation = Quaternion.Euler(item.itemProperties.rotationOffset);

            item.isPocketed = !isEquipped;
            item.EnableItemMeshes(isEquipped);
        }
    }
}