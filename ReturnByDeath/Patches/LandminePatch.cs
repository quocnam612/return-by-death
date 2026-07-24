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

        public static void ResetMineFully(Landmine mine)
        {
            if (mine == null) return;

            // 1. Reset các cờ logic trong Landmine.cs
            mine.hasExploded = false;

            // 2. Bật lại Trigger Collider
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

            // Dọn vết cháy nổ của mìn tại chỗ
            ClearDecalMarksAroundPosition(mine.transform.position);
        }

        public static void ClearDecalMarksAroundPosition(Vector3 position)
        {
            // Quét và xóa các vết đốm đen (Scorched Decals / Blast Marks) do Mìn & Flashbang sinh ra
            GameObject[] allGameObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (GameObject obj in allGameObjects)
            {
                if (obj == null) continue;

                string nameLower = obj.name.ToLower();
                if (nameLower.Contains("scorch") || nameLower.Contains("decal") || nameLower.Contains("blast") || nameLower.Contains("stunedeffect"))
                {
                    // Tránh xóa decal cố định của map, chỉ xóa các clone sinh ra trong trận
                    if (obj.name.EndsWith("(Clone)"))
                    {
                        if (Vector3.Distance(obj.transform.position, position) < 15f)
                        {
                            Object.Destroy(obj);
                        }
                    }
                }
            }
        }
    }
}