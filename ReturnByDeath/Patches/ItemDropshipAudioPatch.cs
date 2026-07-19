using HarmonyLib;
using System;
using UnityEngine;

namespace ReturnByDeath.Patches
{
    [HarmonyPatch(typeof(ItemDropship), "Start")]
    internal class ItemDropshipAudioPatch
    {
        [HarmonyPostfix]
        static void Postfix(ItemDropship __instance)
        {
            if (!ReturnByDeathBase.EnableRingSFX.Value)
            {
                return;
            }

            if (ReturnByDeathBase.SoundFX == null || ReturnByDeathBase.SoundFX.Count == 0)
            {
                return;
            }

            AudioSource[] sources = __instance.GetComponentsInChildren<AudioSource>(true);

            foreach (AudioSource source in sources)
            {
                if (source.clip == null) continue;

                string clipName = source.clip.name;

                if (clipName.Equals("IcecreamTruckV2VehicleDeliveryVer", StringComparison.OrdinalIgnoreCase))
                {
                    source.clip = ReturnByDeathBase.SoundFX[3];
                }
                else if (clipName.Equals("IcecreamTruckV2VehicleDeliveryVerFar", StringComparison.OrdinalIgnoreCase))
                {
                    source.clip = ReturnByDeathBase.SoundFX[4];
                }
                else if (clipName.Equals("IcecreamTruckV2", StringComparison.OrdinalIgnoreCase))
                {
                    source.clip = ReturnByDeathBase.SoundFX[3];
                }
                else if (clipName.Equals("IcecreamTruckFar", StringComparison.OrdinalIgnoreCase))
                {
                    source.clip = ReturnByDeathBase.SoundFX[4];
                }
            }
        }
    }
}