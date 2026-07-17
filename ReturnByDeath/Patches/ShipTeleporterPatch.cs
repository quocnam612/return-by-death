using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReturnByDeath.Patches
{
    [HarmonyPatch(typeof(ShipTeleporter))]
    internal class ShipTeleporterPatch
    {
        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        static void OverrideAudio(ShipTeleporter __instance)
        {
            __instance.teleporterBeamUpSFX = ReturnByDeathBase.SoundFX[0];
        }
    }
}
