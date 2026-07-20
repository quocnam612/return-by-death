using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace ReturnByDeath.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    internal static class PlayerControllerPatch
    {
        private struct PlayerCheckpoint
        {
            internal Vector3 Position;
            internal Quaternion Rotation;
            internal float SprintMeter;
            internal int Health;
            internal bool CriticallyInjured;
            internal bool BleedingHeavily;
        }

        private static PlayerCheckpoint checkpoint;
        private static bool hasCheckpoint;

        internal static void SaveLocalPlayerCheckpoint()
        {
            PlayerControllerB player = GameNetworkManager.Instance != null
                ? GameNetworkManager.Instance.localPlayerController
                : null;

            if (player == null || player.isPlayerDead || player.health <= 0) return;

            checkpoint = new PlayerCheckpoint
            {
                Position = player.transform.position,
                Rotation = player.transform.rotation,
                SprintMeter = player.sprintMeter,
                Health = player.health,
                CriticallyInjured = player.criticallyInjured,
                BleedingHeavily = player.bleedingHeavily
            };
            hasCheckpoint = true;
            ReturnByDeathBase.Instance.mls.LogInfo($"Checkpoint saved at position {checkpoint.Position}, rotation {checkpoint.Rotation}, health {checkpoint.Health}, sprint meter {checkpoint.SprintMeter}");
        }

        [HarmonyPrefix]
        [HarmonyPatch("DamagePlayer")]
        private static bool RestoreCheckpointInsteadOfLethalDamage(PlayerControllerB __instance, int damageNumber)
        {
            if (!ReturnByDeathAppliesTo(__instance) || damageNumber < __instance.health) return true;
            return !RestoreCheckpoint(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch("KillPlayer")]
        private static bool RestoreCheckpointInsteadOfDeath(PlayerControllerB __instance)
        {
            if (!ReturnByDeathAppliesTo(__instance)) return true;
            return !RestoreCheckpoint(__instance);
        }

        private static bool RestoreCheckpoint(PlayerControllerB player)
        {
            if (!hasCheckpoint) return false;

            player.TeleportPlayer(checkpoint.Position);
            player.transform.rotation = checkpoint.Rotation;
            player.sprintMeter = checkpoint.SprintMeter;
            player.health = checkpoint.Health;
            player.criticallyInjured = checkpoint.CriticallyInjured;
            player.bleedingHeavily = checkpoint.BleedingHeavily;
            player.takingFallDamage = false;

            if (HUDManager.Instance != null)
                HUDManager.Instance.UpdateHealthUI(checkpoint.Health, hurtPlayer: false);

            ReturnByDeathBase.Instance.mls.LogInfo("Lethal damage prevented; restored the last checkpoint.");
            return true;
        }

        private static bool ReturnByDeathAppliesTo(PlayerControllerB player)
        {
            return ReturnByDeathBase.EnableReturnByDeath != null
                   && ReturnByDeathBase.EnableReturnByDeath.Value
                   && GameNetworkManager.Instance != null
                   && GameNetworkManager.Instance.localPlayerController == player;
        }
    }
}
