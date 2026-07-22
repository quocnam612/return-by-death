using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace ReturnByDeath.Patches
{
    [HarmonyPatch(typeof(HoarderBugAI))]
    internal static class HoarderBugPatch
    {
        internal static void ReleaseItemFromAllBugs(GrabbableObject targetItem)
        {
            if (targetItem == null) return;

            HoarderBugAI[] allBugs = Object.FindObjectsByType<HoarderBugAI>(FindObjectsSortMode.None);

            foreach (HoarderBugAI bug in allBugs)
            {
                if (bug == null || bug.isEnemyDead) continue;

                // 1. Nếu bọ đang nhắm tới mục tiêu là món đồ này
                if (bug.targetItem == targetItem)
                {
                    bug.targetItem = null;
                }

                // 2. Nếu bọ đang CẦM món đồ này
                if (bug.heldItem != null && bug.heldItem.itemGrabbableObject == targetItem)
                {
                    if (bug.IsServer)
                    {
                        bug.DropItemServerRpc(
                            targetItem.GetComponent<NetworkObject>(), 
                            bug.transform.position, 
                            false
                        );
                    }
                    bug.heldItem = null;
                }

                // 3. Reset trạng thái tức giận chuẩn theo source code HoarderBugAI
                bug.angryTimer = 0f;
                bug.angryAtPlayer = null;

                // 4. Tìm trong danh sách HoarderBugItems tĩnh của game để reset status nếu item bị đánh cắp
                for (int i = 0; i < HoarderBugAI.HoarderBugItems.Count; i++)
                {
                    if (HoarderBugAI.HoarderBugItems[i].itemGrabbableObject == targetItem)
                    {
                        // Đưa status về Returned hoặc xóa khỏi danh sách theo dõi của bọ
                        HoarderBugAI.HoarderBugItems[i].status = HoarderBugItemStatus.Returned;
                    }
                }
            }
        }
    }
}