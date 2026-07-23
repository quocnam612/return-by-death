using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace ReturnByDeath.Patches
{
    [HarmonyPatch(typeof(EnemyAI))]
    internal static class EnemyAIPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("PlayerIsTargetable")]
        private static void PlayerIsTargetablePatch(PlayerControllerB playerScript, ref bool __result)
        {
            if (playerScript != null && PlayerControllerPatch.IsPlayerUntargetable(playerScript))
            {
                __result = false;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("GetAllPlayersInLineOfSight")]
        private static void GetAllPlayersInLineOfSightPatch(EnemyAI __instance, float width, int range, Transform eyeObject, float proximityCheck, ref PlayerControllerB[] __result)
        {
            if (__result == null || __result.Length == 0) return;

            // Loại bỏ player đang trong trạng thái Untargetable 3s
            __result = __result.Where(p => p != null && !PlayerControllerPatch.IsPlayerUntargetable(p)).ToArray();
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnCollideWithPlayer")]
        private static bool PreventEnemyCollisionDamage(Collider other)
        {
            PlayerControllerB player = other.GetComponent<PlayerControllerB>();
            if (player != null && PlayerControllerPatch.IsPlayerUntargetable(player))
            {
                return false; // Chặn va chạm cắn/chém trực tiếp
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(SpringManAI))]
    internal static class SpringManAIPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("OnCollideWithPlayer")]
        private static bool PreventCoilHeadKill(Collider other)
        {
            PlayerControllerB player = other.GetComponent<PlayerControllerB>();
            if (player != null && PlayerControllerPatch.IsPlayerUntargetable(player))
            {
                return false; // Coil-Head va chạm sẽ không đập chết được trong 3s
            }
            return true;
        }
    }
}