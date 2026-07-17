using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ReturnByDeath.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ReturnByDeath
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class ReturnByDeathBase : BaseUnityPlugin
    {
        private const string modGUID = "quocnam612.ReturnByDeath";
        private const string modName = "Rezero Return By Death SFX";
        private const string modVersion = "1.0.0";

        private readonly Harmony harmony = new Harmony(modGUID);

        public static ReturnByDeathBase Instance;

        internal ManualLogSource mls;

        internal static List<AudioClip> SoundFX;
        internal static AssetBundle Bundle;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            mls = BepInEx.Logging.Logger.CreateLogSource(modGUID);

            mls.LogInfo($"[{modName}] v{modVersion} is loaded!");

            harmony.PatchAll(typeof(ReturnByDeathBase));
            harmony.PatchAll(typeof(SoundManagerPatch));
            harmony.PatchAll(typeof(ShipTeleporterPatch));
            harmony.PatchAll(typeof(LethalThingsPatch));

            mls = Logger;

            SoundFX = new List<AudioClip>();
            string FolderLocation = Instance.Info.Location;
            FolderLocation = FolderLocation.TrimEnd("ReturnByDeath.dll".ToCharArray());
            Bundle = AssetBundle.LoadFromFile(FolderLocation + "rbd");
            if (Bundle != null)
            {
                mls.LogMessage($"[{modName}] Successfully loaded AssetBundle from {FolderLocation + "rbd"}");
                SoundFX = Bundle.LoadAllAssets<AudioClip>().ToList();
            }
            else
            {
                mls.LogError($"[{modName}] Failed to load AssetBundle from {FolderLocation + "rbd"}");
            }
        }
    }
}
