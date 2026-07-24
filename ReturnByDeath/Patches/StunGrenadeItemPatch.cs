using System.Collections.Generic;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace ReturnByDeath.Patches
{
    [HarmonyPatch(typeof(StunGrenadeItem))]
    public static class StunGrenadeItemPatch
    {
        // Danh sách chứa các Prefab visual nổ (bao gồm vết cháy đen dưới sàn) do Local Player ném
        public static List<GameObject> localPlayerExplosionEffects = new List<GameObject>();

        [HarmonyPostfix]
        [HarmonyPatch("ExplodeStunGrenade")]
        private static void TrackLocalPlayerGrenadeExplosion(StunGrenadeItem __instance)
        {
            if (GameNetworkManager.Instance == null || GameNetworkManager.Instance.localPlayerController == null)
                return;

            var localPlayer = GameNetworkManager.Instance.localPlayerController;

            // Kiểm tra xem quả nổ này có xuất phát từ Local Player không (dùng đúng field playerThrownBy từ ILSpy)
            // Lưu ý: AccessTools.Field được dùng nếu playerThrownBy là private field trong Assembly-CSharp gốc
            PlayerControllerB thrownBy = AccessTools.Field(typeof(StunGrenadeItem), "playerThrownBy")?.GetValue(__instance) as PlayerControllerB;

            bool isLocalPlayer = __instance.IsOwner ||
                                 __instance.playerHeldBy == localPlayer ||
                                 thrownBy == localPlayer;

            if (isLocalPlayer)
            {
                Vector3 explosionPos = __instance.transform.position;

                // Quét tìm đúng Prefab clone stunGrenadeExplosion vừa sinh ra tại vị trí nổ
                GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                foreach (GameObject obj in allObjects)
                {
                    if (obj == null) continue;

                    // Bắt tất cả Clone sinh ra tại đúng vị trí nổ trong frame này (bao gồm stunGrenadeExplosion(Clone))
                    if (obj.name.EndsWith("(Clone)") && Vector3.Distance(obj.transform.position, explosionPos) < 1.5f)
                    {
                        if (!localPlayerExplosionEffects.Contains(obj))
                        {
                            localPlayerExplosionEffects.Add(obj);
                        }
                    }
                }
            }
        }

        // Gọi hàm này khi Player Respawn (Return By Death)
        public static void ClearLocalPlayerFlashbangDecals()
        {
            for (int i = localPlayerExplosionEffects.Count - 1; i >= 0; i--)
            {
                GameObject effectObj = localPlayerExplosionEffects[i];
                if (effectObj != null)
                {
                    // Tắt hết Renderer lập tức để biến mất trên màn hình ngay lập tức
                    Renderer[] renderers = effectObj.GetComponentsInChildren<Renderer>(true);
                    foreach (Renderer r in renderers)
                    {
                        if (r != null) r.enabled = false;
                    }

                    Object.Destroy(effectObj);
                }
            }
            localPlayerExplosionEffects.Clear();
        }
    }
}