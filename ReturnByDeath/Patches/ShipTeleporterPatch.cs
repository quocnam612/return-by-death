using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ReturnByDeath.Patches
{
    [HarmonyPatch(typeof(ShipTeleporter))]
    internal class ShipTeleporterPatch
    {
        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        static void OverrideAudio(ShipTeleporter __instance)
        {
            ApplySound(__instance);
        }

        internal static void ApplyToExisting()
        {
            foreach (ShipTeleporter teleporter in Resources.FindObjectsOfTypeAll<ShipTeleporter>())
                ApplySound(teleporter);
        }

        private static void ApplySound(ShipTeleporter teleporter)
        {
            if (!ReturnByDeathBase.EnableRbdSFX.Value) return;

            if (ReturnByDeathBase.SoundFX != null && ReturnByDeathBase.SoundFX.Count > 0 && ReturnByDeathBase.SoundFX[0] != null)
                teleporter.teleporterBeamUpSFX = ReturnByDeathBase.SoundFX[0];
        }
    }
}
