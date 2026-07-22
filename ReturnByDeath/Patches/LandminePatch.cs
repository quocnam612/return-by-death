using HarmonyLib;
using UnityEngine;

namespace ReturnByDeath.Patches
{
    [HarmonyPatch(typeof(Landmine))]
    public static class LandminePatch
    {
        public static Landmine lastMineSteppedByLocalPlayer = null;

        [HarmonyPrefix]
        [HarmonyPatch("OnTriggerEnter")]
        private static void DetectLocalPlayerSteppedOnMine(Landmine __instance, Collider other)
        {
            if (GameNetworkManager.Instance != null && GameNetworkManager.Instance.localPlayerController != null)
            {
                if (other.gameObject == GameNetworkManager.Instance.localPlayerController.gameObject)
                {
                    lastMineSteppedByLocalPlayer = __instance;
                }
            }
        }

        /// <summary>
        /// Khôi phục trạng thái mìn hoàn toàn như lúc chưa nổ
        /// </summary>
        public static void ResetMineFully(Landmine mine)
        {
            if (mine == null) return;

            // 1. Reset các cờ logic trong Landmine.cs
            mine.hasExploded = false;

            // 2. Bật lại Trigger Collider (CỰC KỲ QUAN TRỌNG để nhận diện lần dẫm sau)
            Collider mineCollider = mine.GetComponent<Collider>();
            if (mineCollider != null)
            {
                mineCollider.enabled = true;
            }

            // 3. Bật lại tất cả Mesh visual của mìn
            Renderer[] renderers = mine.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                r.enabled = true;
            }

            // 4. Reset Animator về đúng State Idle ban đầu
            if (mine.mineAnimator != null)
            {
                mine.mineAnimator.ResetTrigger("detonate");
                mine.mineAnimator.ResetTrigger("press");
                mine.mineAnimator.Rebind();
                mine.mineAnimator.Update(0f);
                mine.mineAnimator.Play("Idle", 0, 0f);
            }

            // 5. Chạy lại Idle Audio & Sound effect bíp
            mine.StartCoroutine("StartIdleAnimation");
            if (mine.mineAudio != null && !mine.mineAudio.isPlaying)
            {
                mine.mineAudio.Play();
            }
        }
    }
}