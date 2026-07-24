using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.InputSystem;

namespace ReturnByDeath.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    internal static class PlayerControllerPatch
    {
        private static readonly FieldInfo CameraUpField = AccessTools.Field(typeof(PlayerControllerB), "cameraUp");
        private static readonly FieldInfo IsJumpingField = AccessTools.Field(typeof(PlayerControllerB), "isJumping");

        private struct PlayerCheckpoint
        {
            internal Vector3 Position;
            internal Vector3 CameraEuler;
            internal float CameraUp;

            internal bool IsInsideFactory;
            internal bool IsInHangarShipRoom;
            internal bool IsInElevator;
            internal AudioReverbTrigger CurrentAudioTrigger;

            internal float SprintMeter;
            internal int Health;
            internal float InsanityLevel;
            internal float Drunkness;
            internal float Poison;
            internal bool CriticallyInjured;
            internal bool BleedingHeavily;

            internal Transform PhysicsParent;
            internal Transform OverridePhysicsParent;
            internal bool IsCrouching;

            internal int ActiveSlot;
            internal bool IsHoldingUtilSlot;
            internal List<ReturnByDeath.Data.ScrapCheckpointData> SavedScraps;
            internal List<ReturnByDeath.Data.WorldScrapCheckpointData> SavedWorldScraps;
        }

        private static PlayerCheckpoint checkpoint;
        private static bool hasCheckpoint;
        private static bool isRestoring = false;

        private static float saveTimer = 0f;
        private static readonly float SaveInterval = 60f;

        private static InputAction manualSaveAction;
        private static string lastBoundKey = "";

        private static float untargetableTimer = 0f;
        internal static bool IsPlayerUntargetable(PlayerControllerB player)
        {
            if (player == null || GameNetworkManager.Instance == null) return false;
            if (GameNetworkManager.Instance.localPlayerController == player)
            {
                return Time.time < untargetableTimer;
            }
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch("ConnectClientToPlayerObject")]
        private static void SaveInitialCheckpoint(PlayerControllerB __instance)
        {
            if (GameNetworkManager.Instance == null || GameNetworkManager.Instance.localPlayerController != __instance) return;
            SaveLocalPlayerCheckpoint();
            saveTimer = 0f;
        }

        [HarmonyPostfix]
        [HarmonyPatch("Update")]
        private static void UpdateCheckpointTimer(PlayerControllerB __instance)
        {
            if (!ReturnByDeathAppliesTo(__instance) || __instance.isPlayerDead || __instance.health <= 0 || isRestoring)
                return;

            string rawKey = ReturnByDeathBase.ManualSaveKey != null ? ReturnByDeathBase.ManualSaveKey.Value.Trim() : "F5";
            if (rawKey != lastBoundKey || manualSaveAction == null)
            {
                lastBoundKey = rawKey;
                string bindingPath = rawKey.StartsWith("<") ? rawKey : $"<Keyboard>/{rawKey.ToLower()}";

                if (manualSaveAction != null)
                {
                    manualSaveAction.Disable();
                    manualSaveAction.Dispose();
                }

                manualSaveAction = new InputAction("ManualSaveCheckpoint", binding: bindingPath);
                manualSaveAction.Enable();
            }

            if (manualSaveAction.WasPressedThisFrame())
            {
                SaveLocalPlayerCheckpoint();
                saveTimer = 0f;

                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.DisplayTip("Return By Death", $"Checkpoint Saved!", isWarning: false);
                }
                return;
            }

            saveTimer += Time.deltaTime;
            if (saveTimer >= SaveInterval)
            {
                saveTimer = 0f;
                SaveLocalPlayerCheckpoint();
            }
        }

        internal static void SaveLocalPlayerCheckpoint()
        {
            PlayerControllerB player = GameNetworkManager.Instance != null ? GameNetworkManager.Instance.localPlayerController : null;
            if (player == null || player.isPlayerDead || player.health <= 0) return;

            Vector3 currentCamEuler = player.gameplayCamera != null ? player.gameplayCamera.transform.eulerAngles : player.transform.eulerAngles;
            float currentCameraUp = (float)(CameraUpField?.GetValue(player) ?? 0f);

            ReturnByDeath.Data.PlayerScrapState scrapState = ReturnByDeath.Data.PlayerScrapState.Capture(player);

            checkpoint = new PlayerCheckpoint
            {
                Position = player.transform.position,
                CameraEuler = currentCamEuler,
                CameraUp = currentCameraUp,
                IsInsideFactory = player.isInsideFactory,
                IsInHangarShipRoom = player.isInHangarShipRoom,
                IsInElevator = player.isInElevator,
                CurrentAudioTrigger = player.currentAudioTrigger,
                SprintMeter = player.sprintMeter,
                Health = player.health,
                InsanityLevel = player.insanityLevel,
                Drunkness = player.drunkness,
                Poison = player.poison,
                CriticallyInjured = player.criticallyInjured,
                BleedingHeavily = player.bleedingHeavily,
                PhysicsParent = player.physicsParent,
                OverridePhysicsParent = player.overridePhysicsParent,
                IsCrouching = player.isCrouching,

                ActiveSlot = scrapState.ActiveSlotIndex,
                IsHoldingUtilSlot = scrapState.IsHoldingUtilSlot,
                SavedScraps = scrapState.HeldItems,
                SavedWorldScraps = scrapState.WorldScraps
            };

            hasCheckpoint = true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("DamagePlayer")]
        private static bool RestoreCheckpointInsteadOfLethalDamage(PlayerControllerB __instance, int damageNumber)
        {
            if (!ReturnByDeathAppliesTo(__instance) || damageNumber < __instance.health) return true;
            if (!hasCheckpoint || isRestoring) return false;

            __instance.StartCoroutine(DelayedRestoreRoutine(__instance));
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("KillPlayer")]
        private static bool RestoreCheckpointInsteadOfDeath(PlayerControllerB __instance)
        {
            if (!ReturnByDeathAppliesTo(__instance)) return true;
            if (!hasCheckpoint || isRestoring) return false;

            __instance.StartCoroutine(DelayedRestoreRoutine(__instance));
            return false;
        }

        private static IEnumerator DelayedRestoreRoutine(PlayerControllerB player)
        {
            isRestoring = true;

            // --- 1. RỬA SẠCH PARENT & LỰC VẬT LÝ ---
            player.transform.SetParent(StartOfRound.Instance.playersContainer);
            player.physicsParent = null;
            player.overridePhysicsParent = null;
            player.lastSyncedPhysicsParent = null;

            player.fallValue = 0f;
            player.fallValueUncapped = 0f;
            player.velocityLastFrame = Vector3.zero;
            player.externalForces = Vector3.zero;
            player.externalForceAutoFade = Vector3.zero;
            player.takingFallDamage = false;
            player.isFallingFromJump = false;
            player.isFallingNoJump = false;
            IsJumpingField?.SetValue(player, false);

            Vector3 oobPos = new Vector3(0f, -2000f, 0f);

            // Bật controller và Teleport ra vô cực (OOB)
            if (player.thisController != null) player.thisController.enabled = true;
            player.TeleportPlayer(oobPos);
            Physics.SyncTransforms();

            // Chờ 1 frame FixedUpdate với Controller BẬT để PhysX tính toán Trigger Exit
            yield return new WaitForFixedUpdate();

            // Tắt Controller tạm thời để đưa về Checkpoint chuẩn
            if (player.thisController != null) player.thisController.enabled = false;

            player.transform.position = checkpoint.Position;
            player.serverPlayerPosition = checkpoint.Position;
            Physics.SyncTransforms();

            // Bật lại Controller ở Checkpoint
            if (player.thisController != null) player.thisController.enabled = true;
            player.TeleportPlayer(checkpoint.Position);
            Physics.SyncTransforms();

            ResetAllKillLocalPlayerTriggers();

            // --- 3. CAMERA & ROTATION ---
            Vector3 targetPlayerEuler = new Vector3(0f, checkpoint.CameraEuler.y, 0f);
            player.transform.eulerAngles = targetPlayerEuler;

            if (player.thisPlayerBody != null) player.thisPlayerBody.eulerAngles = targetPlayerEuler;
            if (player.turnCompass != null) player.turnCompass.eulerAngles = targetPlayerEuler;

            CameraUpField?.SetValue(player, checkpoint.CameraUp);

            if (player.gameplayCamera != null)
            {
                if (player.cameraContainerTransform != null)
                {
                    player.gameplayCamera.transform.SetParent(player.cameraContainerTransform, false);
                    player.gameplayCamera.transform.localPosition = Vector3.zero;
                }
                player.gameplayCamera.transform.eulerAngles = checkpoint.CameraEuler;
                player.gameplayCamera.transform.localEulerAngles = new Vector3(checkpoint.CameraUp, 0f, 0f);
            }

            // --- 4. AMBIENCE & PLAYER STATS ---
            player.isInsideFactory = checkpoint.IsInsideFactory;
            player.isInHangarShipRoom = checkpoint.IsInHangarShipRoom;
            player.isInElevator = checkpoint.IsInElevator;
            player.currentAudioTrigger = checkpoint.CurrentAudioTrigger;

            if (TimeOfDay.Instance != null)
            {
                TimeOfDay.Instance.insideLighting = checkpoint.IsInsideFactory || checkpoint.IsInHangarShipRoom;
                TimeOfDay.Instance.SetInsideLightingDimness(doNotLerp: true, checkpoint.IsInsideFactory || checkpoint.IsInHangarShipRoom);
            }

            player.sprintMeter = checkpoint.SprintMeter;
            player.health = checkpoint.Health;
            player.drunkness = checkpoint.Drunkness;
            player.poison = checkpoint.Poison;
            player.criticallyInjured = checkpoint.CriticallyInjured;
            player.bleedingHeavily = checkpoint.BleedingHeavily;
            player.ResetPlayerBloodObjects(true);

            player.physicsParent = checkpoint.PhysicsParent;
            player.overridePhysicsParent = checkpoint.OverridePhysicsParent;
            player.Crouch(checkpoint.IsCrouching);

            player.insanityLevel = checkpoint.InsanityLevel;
            player.insanitySpeedMultiplier = 1f;

            // --- 5. KHÔI PHỤC INVENTORY ---
            GrabbableObjectPatch.RestoreAllScrapData(
                player,
                checkpoint.SavedScraps,
                checkpoint.SavedWorldScraps,
                checkpoint.ActiveSlot,
                checkpoint.IsHoldingUtilSlot
            );

            // --- RESTORE LANDMINES & CLEAN DECAL ---
            if (LandminePatch.lastMineSteppedByLocalPlayer != null)
            {
                LandminePatch.ResetMineFully(LandminePatch.lastMineSteppedByLocalPlayer);
                LandminePatch.lastMineSteppedByLocalPlayer = null;
            }
            else
            {
                // Nếu chết do Flashbang hoặc lý do khác, vẫn quét dọn vết cháy đen quanh vị trí chết
                LandminePatch.ClearDecalMarksAroundPosition(checkpoint.Position);
            }

            StunGrenadeItemPatch.ClearLocalPlayerFlashbangDecals();

            // --- 6. RELOAD HUD OVERLAY ---
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.HideHUD(true);

                HUDManager.Instance.SetCracksOnVisor(checkpoint.Health);
                HUDManager.Instance.UpdateHealthUI(checkpoint.Health, hurtPlayer: false);

                HUDManager.Instance.cadaverFilter = 0f;
                HUDManager.Instance.sinkingCoveredFace = false;
                HUDManager.Instance.setUnderwaterFilter = false;

                float checkpointInsanityRatio = Mathf.Clamp01(checkpoint.InsanityLevel / player.maxInsanityLevel);

                if (StartOfRound.Instance != null)
                {
                    StartOfRound.Instance.fearLevel = checkpointInsanityRatio;
                    StartOfRound.Instance.fearLevelIncreasing = false;
                }

                if (HUDManager.Instance.insanityScreenFilter != null)
                {
                    HUDManager.Instance.insanityScreenFilter.weight = checkpointInsanityRatio;
                }

                if (HUDManager.Instance.HUDAnimator != null)
                {
                    HUDManager.Instance.HUDAnimator.SetBool("insanity", checkpointInsanityRatio > 0.4f);
                    HUDManager.Instance.HUDAnimator.SetBool("biohazardDamage", false);
                }

                HUDManager.Instance.HideHUD(false);
            }

            // --- 7. RESET QUÁI & TRẠNG THÁI ANIMATION ---
            ResetAllEnemies(player);

            player.inAnimationWithEnemy = null;
            player.inSpecialInteractAnimation = false;
            player.disableMoveInput = false;
            player.disableLookInput = false;
            player.clampLooking = false;
            player.enteringSpecialAnimation = false;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            SoundManagerPatch.PlayReturnByDeathSound();

            untargetableTimer = Time.time + 3f;
            saveTimer = 0f;
            isRestoring = false;
        }

        private static bool ReturnByDeathAppliesTo(PlayerControllerB player)
        {
            return ReturnByDeathBase.EnableReturnByDeath != null
                   && ReturnByDeathBase.EnableReturnByDeath.Value
                   && GameNetworkManager.Instance != null
                   && GameNetworkManager.Instance.localPlayerController == player;
        }

        private static void ResetAllEnemies(PlayerControllerB player)
        {
            EnemyAI[] allEnemies = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
            foreach (EnemyAI enemy in allEnemies)
            {
                if (enemy == null || enemy.isEnemyDead) continue;
                try
                {
                    if (enemy.targetPlayer == player)
                    {
                        enemy.targetPlayer = null;
                        enemy.movingTowardsTargetPlayer = false;
                        enemy.moveTowardsDestination = false;
                        if (enemy.enemyBehaviourStates != null && enemy.enemyBehaviourStates.Length > 0)
                        {
                            enemy.SwitchToBehaviourState(0);
                        }
                    }
                    enemy.CancelSpecialAnimationWithPlayer();
                }
                catch { }
            }
        }

        private static void ResetAllKillLocalPlayerTriggers()
        {
            // Quét toàn bộ component KillLocalPlayer trong Scene
            KillLocalPlayer[] killTriggers = Object.FindObjectsByType<KillLocalPlayer>(FindObjectsSortMode.None);
            foreach (var trigger in killTriggers)
            {
                if (trigger == null) continue;

                // Reset flag block của KillLocalPlayer
                // (Dùng Reflection để reset cả private/public field liên quan đến trạng thái đã giết)
                var fields = AccessTools.GetDeclaredFields(typeof(KillLocalPlayer));
                foreach (var field in fields)
                {
                    if (field.FieldType == typeof(bool))
                    {
                        // Set tất cả các cờ bool (như hasKilled, killedPlayer, isDead...) về false
                        field.SetValue(trigger, false);
                    }
                }
            }

            // Quét dọn luôn InteractTrigger đính kèm hố (nếu có)
            InteractTrigger[] interactTriggers = Object.FindObjectsByType<InteractTrigger>(FindObjectsSortMode.None);
            foreach (var trigger in interactTriggers)
            {
                if (trigger == null) continue;

                // Reset trạng thái đang chạm player
                var fields = AccessTools.GetDeclaredFields(typeof(InteractTrigger));
                foreach (var field in fields)
                {
                    if (field.Name.ToLower().Contains("touching") || field.Name.ToLower().Contains("player") || field.Name.ToLower().Contains("has"))
                    {
                        if (field.FieldType == typeof(bool))
                        {
                            field.SetValue(trigger, false);
                        }
                    }
                }
            }
        }
    }
}