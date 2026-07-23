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

            if (HUDManager.Instance != null)
            {
                if (HUDManager.Instance.itemSlotIcons != null)
                {
                    for (int i = 0; i < HUDManager.Instance.itemSlotIcons.Length; i++)
                    {
                        if (HUDManager.Instance.itemSlotIcons[i] != null)
                        {
                            HUDManager.Instance.itemSlotIcons[i].enabled = false;
                        }
                    }
                }
                if (HUDManager.Instance.itemOnlySlotIcon != null)
                {
                    HUDManager.Instance.itemOnlySlotIcon.enabled = false;
                }
            }

            for (int i = 0; i < localPlayer.ItemSlots.Length; i++)
            {
                localPlayer.ItemSlots[i] = null;
            }
            localPlayer.ItemOnlySlot = null;

            float calculatedWeight = 1f;
            GrabbableObject[] allWorldItems = UnityEngine.Object.FindObjectsByType<GrabbableObject>(FindObjectsSortMode.None);

            // 1. RESTORE WORLD SCRAPS
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

            // 2. RESTORE HELD ITEMS
            foreach (var savedData in savedHeldScraps)
            {
                GrabbableObject item = Array.Find(allWorldItems, x =>
                    x != null &&
                    x.GetComponent<NetworkObject>() != null &&
                    x.GetComponent<NetworkObject>().NetworkObjectId == savedData.NetworkObjectId);

                bool isSpentFlashbang = false;
                if (item is StunGrenadeItem grenade)
                {
                    if (grenade.pinPulled || grenade.hasExploded || grenade.itemUsedUp || grenade.deactivated)
                    {
                        isSpentFlashbang = true;
                    }
                }

                if (item == null || isSpentFlashbang)
                {
                    if (item != null)
                    {
                        UnityEngine.Object.Destroy(item.gameObject);
                        item = null;
                    }

                    item = RecreatenAndSpawnItem(savedData, localPlayer);

                    if (item != null && item.GetComponent<NetworkObject>() != null)
                    {
                        savedData.NetworkObjectId = item.GetComponent<NetworkObject>().NetworkObjectId;
                    }
                }

                if (item == null) continue;

                item.deactivated = false;
                item.itemUsedUp = false;

                if (item is StunGrenadeItem freshGrenade)
                {
                    freshGrenade.pinPulled = false;
                    freshGrenade.hasExploded = false;
                    freshGrenade.itemUsedUp = false;
                    freshGrenade.deactivated = false;
                }

                HoarderBugPatch.ReleaseItemFromAllBugs(item);

                if (item.isHeld && item.playerHeldBy != null && item.playerHeldBy != localPlayer)
                {
                    continue;
                }

                int slot = savedData.SlotIndex;

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

                if (slot >= 0 && slot < localPlayer.ItemSlots.Length)
                {
                    localPlayer.ItemSlots[slot] = item;
                    SetupItemOnPlayer(localPlayer, item, isEquipped: false);
                    RestoreItemBatteryAndState(item, savedData);

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
                }
            }

            localPlayer.carryWeight = calculatedWeight;
            if (StartOfRound.Instance != null)
            {
                StartOfRound.Instance.SendChangedWeightEvent();
            }

            localPlayer.StartCoroutine(DelayedSlotEquipRoutine(localPlayer, activeSlot, isHoldingUtilSlot));
        }

        private static void RestoreItemBatteryAndState(GrabbableObject item, ScrapCheckpointData savedData)
        {
            if (item == null || savedData == null) return;

            item.deactivated = false;
            item.itemUsedUp = false;

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

        private static GrabbableObject RecreatenAndSpawnItem(ScrapCheckpointData savedData, PlayerControllerB localPlayer)
        {
            Item targetItemProperty = null;

            // 1. Quét trong AllItemsList
            if (StartOfRound.Instance != null && StartOfRound.Instance.allItemsList != null)
            {
                targetItemProperty = StartOfRound.Instance.allItemsList.itemsList.Find(x =>
                    x != null && (x.itemId == savedData.ItemItemId ||
                    (savedData.ItemName != "Unknown" && string.Equals(x.itemName, savedData.ItemName, StringComparison.OrdinalIgnoreCase))));
            }

            // 2. Quét trong BuyableItems của Terminal (Nơi chứa Flashbang, Walkie-talkie...)
            if (targetItemProperty == null)
            {
                Terminal terminal = UnityEngine.Object.FindObjectOfType<Terminal>();
                if (terminal != null && terminal.buyableItemsList != null)
                {
                    targetItemProperty = Array.Find(terminal.buyableItemsList, x =>
                        x != null && (x.itemId == savedData.ItemItemId || string.Equals(x.itemName, savedData.ItemName, StringComparison.OrdinalIgnoreCase)));
                }
            }

            if (targetItemProperty == null || targetItemProperty.spawnPrefab == null) return null;

            GameObject spawnedObj = UnityEngine.Object.Instantiate(
                targetItemProperty.spawnPrefab,
                localPlayer.localItemHolder.position,
                Quaternion.identity,
                StartOfRound.Instance.propsContainer
            );

            GrabbableObject grabbable = spawnedObj.GetComponent<GrabbableObject>();
            if (grabbable != null)
            {
                grabbable.scrapValue = savedData.ScrapValue;
                grabbable.itemProperties = targetItemProperty;

                if (grabbable is StunGrenadeItem grenade)
                {
                    grenade.pinPulled = false;
                    grenade.hasExploded = false;
                    grenade.itemUsedUp = false;
                    grenade.deactivated = false;
                }

                grabbable.deactivated = false;
                grabbable.itemUsedUp = false;

                NetworkObject netObj = spawnedObj.GetComponent<NetworkObject>();
                if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                {
                    if (!netObj.IsSpawned) netObj.Spawn();
                }
            }

            return grabbable;
        }

        private static void ResetItemToWorldPosition(PlayerControllerB localPlayer, GrabbableObject item, WorldScrapCheckpointData worldData)
        {
            item.isHeld = false;
            item.isPocketed = false;
            item.heldByPlayerOnServer = false;
            item.playerHeldBy = null;
            item.parentObject = null;
            item.deactivated = false;
            item.itemUsedUp = false;

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