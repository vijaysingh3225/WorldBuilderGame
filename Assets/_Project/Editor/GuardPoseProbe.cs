using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WorldBuilder.Gameplay.Presentation;

namespace WorldBuilder.Editor
{
    public static class GuardPoseProbe
    {
        public static void OpenCombatLab()
        {
            EditorSceneManager.OpenScene(CombatLabSceneBuilder.ScenePath);
        }

        public static void Probe()
        {
            EditorSceneManager.OpenScene(CombatLabSceneBuilder.ScenePath);
            ShortSwordBlockPresenter presenter =
                Object.FindFirstObjectByType<ShortSwordBlockPresenter>();
            Animator animator =
                presenter != null ? presenter.GetComponent<Animator>() : null;
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                HumanoidAnimationSetup.ShortSwordBlockPath);
            if (animator == null || clip == null)
            {
                Debug.LogError("GUARD_PROBE_MISSING");
                return;
            }

            Transform leftHand =
                animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform rightHand =
                animator.GetBoneTransform(HumanBodyBones.RightHand);
            AnimationMode.StartAnimationMode();
            try
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(
                    animator.gameObject,
                    clip,
                    0.55f * clip.length);
                AnimationMode.EndSampling();
                Debug.Log(
                    $"GUARD_PROBE spread=" +
                    $"{Vector3.Distance(leftHand.position, rightHand.position):F4}");
            }
            finally
            {
                AnimationMode.StopAnimationMode();
            }
        }
    }
}
