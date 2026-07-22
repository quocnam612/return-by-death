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
        // Cache FieldInfo của cameraUp để truy cập nhanh không bị lag
        private static readonly FieldInfo CameraUpField =
            AccessTools.Field(typeof(PlayerControllerB), "cameraUp");

        private struct PlayerCheckpoint
        {
            internal Vector3 Position;
            internal Vector3 CameraEuler;
            internal float CameraUp; // Bổ sung lưu biến cameraUp

            // Environment and audio context.
            internal bool IsInsideFactory;
            internal bool IsInHangarShipRoom;
            internal bool IsInElevator;
            internal AudioReverbTrigger CurrentAudioTrigger;

            // Survival state.
            internal float SprintMeter;
            internal int Health;
            internal float InsanityLevel;
            internal float Drunkness;
            internal float Poison;
            internal bool CriticallyInjured;
            internal bool BleedingHeavily;

            // Movement state.
            internal Transform PhysicsParent;
            internal Transform OverridePhysicsParent;
            internal float FallValue;
            internal float FallValueUncapped;
            internal bool IsCrouching;

            // Inventory state.
            internal int ActiveSlot;
            internal bool IsHoldingUtilSlot;
            internal System.Collections.Generic.List<ReturnByDeath.Data.ScrapCheckpointData> SavedScraps;
            internal List<ReturnByDeath.Data.WorldScrapCheckpointData> SavedWorldScraps;
        }

        private static PlayerCheckpoint checkpoint;
        private static bool hasCheckpoint;
        private static bool isRestoring = false;

        private static float saveTimer = 0f;
        private static readonly float SaveInterval = 60f;

        private static UnityEngine.InputSystem.InputAction manualSaveAction;
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
            if (GameNetworkManager.Instance == null
                || GameNetworkManager.Instance.localPlayerController != __instance) return;

            ReturnByDeathBase.Instance.mls.LogInfo("Local player connected; creating initial checkpoint.");
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
            PlayerControllerB player = GameNetworkManager.Instance != null
                ? GameNetworkManager.Instance.localPlayerController
                : null;

            if (player == null || player.isPlayerDead || player.health <= 0) return;

            Vector3 currentCamEuler = player.gameplayCamera != null
                ? player.gameplayCamera.transform.eulerAngles
                : player.transform.eulerAngles;

            // Lấy giá trị cameraUp private
            float currentCameraUp = (float)(CameraUpField?.GetValue(player) ?? 0f);

            ReturnByDeath.Data.PlayerScrapState scrapState = ReturnByDeath.Data.PlayerScrapState.Capture(player);

            checkpoint = new PlayerCheckpoint
            {
                Position = player.transform.position,
                CameraEuler = currentCamEuler,
                CameraUp = currentCameraUp, // Lưu góc nhìn Pitch (ngước lên/ngó xuống)
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
                FallValue = player.fallValue,
                FallValueUncapped = player.fallValueUncapped,
                IsCrouching = player.isCrouching,

                ActiveSlot = scrapState.ActiveSlotIndex,
                IsHoldingUtilSlot = scrapState.IsHoldingUtilSlot,
                SavedScraps = scrapState.HeldItems,
                SavedWorldScraps = scrapState.WorldScraps
            };

            hasCheckpoint = true;
            ReturnByDeathBase.Instance.mls.LogInfo($"Checkpoint saved at Position: {checkpoint.Position}, Camera Euler: {checkpoint.CameraEuler}, Camera Up: {checkpoint.CameraUp}");
        }

        [HarmonyPrefix]
        [HarmonyPatch("DamagePlayer")]
        private static bool RestoreCheckpointInsteadOfLethalDamage(PlayerControllerB __instance, int damageNumber)
        {
            if (!ReturnByDeathAppliesTo(__instance) || damageNumber < __instance.health) return true;
            if (!hasCheckpoint || isRestoring) return false;

            __instance.StartCoroutine(DelayedRestoreRoutine(__instance, 0.07f));
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("KillPlayer")]
        private static bool RestoreCheckpointInsteadOfDeath(PlayerControllerB __instance)
        {
            if (!ReturnByDeathAppliesTo(__instance)) return true;
            if (!hasCheckpoint || isRestoring) return false;

            __instance.StartCoroutine(DelayedRestoreRoutine(__instance, 0.07f));
            return false;
        }

        private static IEnumerator DelayedRestoreRoutine(PlayerControllerB player, float delay)
        {
            isRestoring = true;

            yield return new WaitForSeconds(delay);

            RestoreCheckpoint(player);

            isRestoring = false;
        }

        private static bool RestoreCheckpoint(PlayerControllerB player)
        {
            if (!hasCheckpoint) return false;

            // --- PARENT & CAMERA RELEASE ---
            player.transform.SetParent(StartOfRound.Instance.playersContainer);
            player.physicsParent = null;
            player.overridePhysicsParent = null;
            player.lastSyncedPhysicsParent = null;

            // --- TELEPORT & FIX ROTATION & PITCH ---
            player.TeleportPlayer(checkpoint.Position);
            player.serverPlayerPosition = checkpoint.Position;

            Vector3 targetPlayerEuler = new Vector3(0f, checkpoint.CameraEuler.y, 0f);
            player.transform.eulerAngles = targetPlayerEuler;

            if (player.thisPlayerBody != null)
            {
                player.thisPlayerBody.eulerAngles = targetPlayerEuler;
            }

            if (player.turnCompass != null)
            {
                player.turnCompass.eulerAngles = targetPlayerEuler;
            }

            // Gán lại biến cameraUp private để không bị Update đè lại góc pitch
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

            // --- AMBIENCE & LIGHTING ---
            player.isInsideFactory = checkpoint.IsInsideFactory;
            player.isInHangarShipRoom = checkpoint.IsInHangarShipRoom;
            player.isInElevator = checkpoint.IsInElevator;
            player.currentAudioTrigger = checkpoint.CurrentAudioTrigger;

            if (TimeOfDay.Instance != null)
            {
                TimeOfDay.Instance.insideLighting = checkpoint.IsInsideFactory || checkpoint.IsInHangarShipRoom;
                TimeOfDay.Instance.SetInsideLightingDimness(doNotLerp: true, checkpoint.IsInsideFactory || checkpoint.IsInHangarShipRoom);
            }

            if (checkpoint.CurrentAudioTrigger != null)
            {
                checkpoint.CurrentAudioTrigger.ChangeAudioReverbForPlayer(player);
            }

            // --- PLAYER STATS ---
            player.sprintMeter = checkpoint.SprintMeter;
            player.health = checkpoint.Health;
            player.drunkness = checkpoint.Drunkness;
            player.poison = checkpoint.Poison;
            player.criticallyInjured = checkpoint.CriticallyInjured;
            player.bleedingHeavily = checkpoint.BleedingHeavily;
            player.ResetPlayerBloodObjects(true);

            player.physicsParent = checkpoint.PhysicsParent;
            player.overridePhysicsParent = checkpoint.OverridePhysicsParent;
            player.fallValue = 0f;
            player.fallValueUncapped = 0f;
            player.externalForces = Vector3.zero;
            player.takingFallDamage = false;
            player.Crouch(checkpoint.IsCrouching);

            player.insanityLevel = checkpoint.InsanityLevel;
            player.insanitySpeedMultiplier = 1f;

            // --- RESTORE INVENTORY ---
            GrabbableObjectPatch.RestoreAllScrapData(
                player,
                checkpoint.SavedScraps,
                checkpoint.SavedWorldScraps,
                checkpoint.ActiveSlot,
                checkpoint.IsHoldingUtilSlot
            );

            // --- HUD OVERLAY ---
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.SetCracksOnVisor(checkpoint.Health);
                HUDManager.Instance.UpdateHealthUI(checkpoint.Health, hurtPlayer: false);

                HUDManager.Instance.cadaverFilter = 0f;
                HUDManager.Instance.sinkingCoveredFace = false;
                HUDManager.Instance.setUnderwaterFilter = false;

                float checkpointInsanityRatio = Mathf.Clamp01(checkpoint.InsanityLevel / player.maxInsanityLevel);

                if (StartOfRound.Instance != null)
                {
                    StartOfRound.Instance.fearLevel = 0.7f;
                    StartOfRound.Instance.fearLevelIncreasing = true;
                }

                if (HUDManager.Instance.insanityScreenFilter != null)
                {
                    HUDManager.Instance.insanityScreenFilter.weight = checkpointInsanityRatio;
                }

                if (HUDManager.Instance.HUDAnimator != null)
                {
                    HUDManager.Instance.HUDAnimator.SetBool("insanity", checkpointInsanityRatio > 0.4f);
                }

                HUDManager.Instance.HideHUD(false);
            }

            // --- RESET ENEMY AI ---
            EnemyAI[] allEnemies = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
            foreach (EnemyAI enemy in allEnemies)
            {
                if (enemy == null || enemy.isEnemyDead) continue;

                try
                {
                    if (enemy is FlowermanAI bracken)
                    {
                        bracken.StopAllCoroutines();
                        bracken.FinishKillAnimation(carryingBody: false);
                        bracken.inKillAnimation = false;
                        bracken.carryingPlayerBody = false;
                        bracken.bodyBeingCarried = null;
                        bracken.inSpecialAnimationWithPlayer = null;

                        if (bracken.enemyBehaviourStates != null && bracken.enemyBehaviourStates.Length > 1)
                        {
                            bracken.SwitchToBehaviourState(1);
                        }
                        continue;
                    }
                    else if (enemy is MaskedPlayerEnemy masked)
                    {
                        masked.StopAllCoroutines();
                        masked.FinishKillAnimation();
                        masked.inSpecialAnimationWithPlayer = null;
                        masked.targetPlayer = null;
                        masked.movingTowardsTargetPlayer = false;

                        if (masked.enemyBehaviourStates != null && masked.enemyBehaviourStates.Length > 0)
                        {
                            masked.SwitchToBehaviourState(0);
                        }
                        continue;
                    }
                    else if (enemy is ButlerBeesEnemyAI bees)
                    {
                        bees.targetPlayer = null;
                        bees.movingTowardsTargetPlayer = false;
                        continue;
                    }
                    else if (enemy is DressGirlAI girl)
                    {
                        girl.StopAllCoroutines();
                        girl.timer = 0f;
                        girl.staringTimer = 0f;
                        girl.staringInHaunt = false;
                        girl.disappearingFromStare = false;

                        if (girl.currentBehaviourStateIndex == 1)
                        {
                            girl.SwitchToBehaviourState(0);
                        }

                        if (girl.creatureAnimator != null)
                        {
                            girl.creatureAnimator.SetBool("Walk", false);
                        }

                        if (girl.heartbeatMusic != null)
                        {
                            girl.heartbeatMusic.volume = 0f;
                        }
                        girl.SFXVolumeLerpTo = 0f;

                        girl.EnableEnemyMesh(enable: false, overrideDoNotSet: true, tamperWithMeshes: true);
                        continue;
                    }

                    if (enemy.inSpecialAnimation && (enemy.targetPlayer == player || enemy.inSpecialAnimationWithPlayer == player))
                    {
                        enemy.StopAllCoroutines();
                        enemy.inSpecialAnimation = false;
                    }

                    if (enemy.targetPlayer == player)
                    {
                        enemy.targetPlayer = null;
                        enemy.movingTowardsTargetPlayer = false;
                        enemy.moveTowardsDestination = false;

                        if (enemy.currentSearch != null)
                        {
                            enemy.StopSearch(enemy.currentSearch);
                        }

                        if (enemy.enemyBehaviourStates != null && enemy.enemyBehaviourStates.Length > 0)
                        {
                            enemy.SwitchToBehaviourState(0);
                        }
                    }

                    enemy.CancelSpecialAnimationWithPlayer();
                }
                catch (System.Exception ex)
                {
                    ReturnByDeathBase.Instance.mls.LogWarning($"Error resetting {enemy.GetType().Name}: {ex.Message}");
                }
            }

            // --- INPUT & CURSOR UNLOCK ---
            player.inAnimationWithEnemy = null;
            player.inSpecialInteractAnimation = false;
            player.disableMoveInput = false;
            player.disableLookInput = false;
            player.clampLooking = false;
            player.enteringSpecialAnimation = false;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            SoundManagerPatch.PlayReturnByDeathSound();

            // --- RESET LANDMINE ---
            if (LandminePatch.lastMineSteppedByLocalPlayer != null)
            {
                Landmine mine = LandminePatch.lastMineSteppedByLocalPlayer;
                if (mine.hasExploded)
                {
                    ResetLandmine(mine);
                }
                LandminePatch.lastMineSteppedByLocalPlayer = null;
            }

            ReturnByDeathBase.Instance.mls.LogInfo("Lethal damage prevented; restored last checkpoint.");
            untargetableTimer = Time.time + 3f;
            saveTimer = 0f;
            return true;
        }

        private static bool ReturnByDeathAppliesTo(PlayerControllerB player)
        {
            return ReturnByDeathBase.EnableReturnByDeath != null
                   && ReturnByDeathBase.EnableReturnByDeath.Value
                   && GameNetworkManager.Instance != null
                   && GameNetworkManager.Instance.localPlayerController == player;
        }

        private static void ResetLandmine(Landmine mine)
        {
            if (mine == null) return;
            Vector3 minePos = mine.transform.position;

            LandminePatch.ResetMineFully(mine);
            CleanupBlastEffectAt(minePos);
        }

        private static void CleanupBlastEffectAt(Vector3 position)
        {
            if (RoundManager.Instance != null && RoundManager.Instance.mapPropsContainer != null)
            {
                Transform propsContainer = RoundManager.Instance.mapPropsContainer.transform;
                for (int i = propsContainer.childCount - 1; i >= 0; i--)
                {
                    Transform child = propsContainer.GetChild(i);
                    if (child == null) continue;

                    if (Vector3.Distance(child.position, position) < 8f)
                    {
                        string name = child.gameObject.name.ToLower();
                        if (name.Contains("explosion") || name.Contains("scorch") || name.Contains("decal") || name.Contains("blast"))
                        {
                            UnityEngine.Object.Destroy(child.gameObject);
                        }
                    }
                }
            }

            GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            foreach (GameObject go in allObjects)
            {
                if (go == null) continue;
                string name = go.name.ToLower();
                if ((name.Contains("scorch") || name.Contains("decal") || name.Contains("explosionmark")) && Vector3.Distance(go.transform.position, position) < 8f)
                {
                    UnityEngine.Object.Destroy(go);
                }
            }
        }
    }
}