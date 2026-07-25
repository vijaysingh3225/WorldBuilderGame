using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WorldBuilder.Editor;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Presentation;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class CombatLabCheckpointTests
    {
        [Test]
        public void CombatLabUsesThreeHitCc0SwordCombo()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    HumanoidAnimationSetup.ControllerPath);

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.layers, Has.Length.EqualTo(3));
            Assert.That(
                controller.layers[0].stateMachine.states
                    .Select(state => state.state.name),
                Is.SupersetOf(new[]
                {
                    "Standing Locomotion V8",
                    "Resting Tactical Crouch V5",
                    "Natural Jump Rise V2",
                    "Natural Jump Fall V2"
                }));

            AnimatorControllerLayer gripLayer = controller.layers[1];
            Assert.That(gripLayer.name, Is.EqualTo("Short Sword Ready"));
            Assert.That(gripLayer.defaultWeight, Is.EqualTo(1f));
            Assert.That(gripLayer.avatarMask, Is.Not.Null);
            Assert.That(
                gripLayer.stateMachine.defaultState.motion,
                Is.EqualTo(AssetDatabase.LoadAllAssetsAtPath(
                        HumanoidAnimationSetup.ModelPath)
                    .OfType<AnimationClip>()
                    .Single(clip => clip.name.EndsWith("|Sword_Idle"))));

            AnimatorControllerLayer attackLayer = controller.layers[2];
            Assert.That(attackLayer.name, Is.EqualTo(ShortSwordAttackPresenter.AttackLayerName));
            Assert.That(attackLayer.defaultWeight, Is.EqualTo(0f));
            Assert.That(attackLayer.avatarMask, Is.Not.Null);
            Assert.That(
                attackLayer.avatarMask.GetHumanoidBodyPartActive(
                    AvatarMaskBodyPart.Body),
                Is.True);
            Assert.That(
                attackLayer.avatarMask.GetHumanoidBodyPartActive(
                    AvatarMaskBodyPart.RightArm),
                Is.True);
            Assert.That(
                attackLayer.avatarMask.GetHumanoidBodyPartActive(
                    AvatarMaskBodyPart.LeftLeg),
                Is.False);
            Assert.That(
                attackLayer.avatarMask.GetHumanoidBodyPartActive(
                    AvatarMaskBodyPart.RightLeg),
                Is.False);
            Assert.That(
                attackLayer.stateMachine.states.Select(state => state.state.name),
                Is.EquivalentTo(new[]
                {
                    ShortSwordAttackPresenter.Hit1StateName,
                    ShortSwordAttackPresenter.Hit1RecoveryStateName,
                    ShortSwordAttackPresenter.Hit2StateName,
                    ShortSwordAttackPresenter.Hit2RecoveryStateName,
                    ShortSwordAttackPresenter.Hit3StateName
                }));
            string[] clipNames =
            {
                HumanoidAnimationSetup.SwordComboHit1ClipName,
                HumanoidAnimationSetup.SwordComboHit1RecoveryClipName,
                HumanoidAnimationSetup.SwordComboHit2ClipName,
                HumanoidAnimationSetup.SwordComboHit2RecoveryClipName,
                HumanoidAnimationSetup.SwordComboHit3ClipName
            };
            AnimationClip[] comboClips = AssetDatabase.LoadAllAssetsAtPath(
                    HumanoidAnimationSetup.SwordComboModelPath)
                .OfType<AnimationClip>()
                .Where(clip => clipNames.Any(name =>
                    clip.name.EndsWith(name, System.StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            Assert.That(
                attackLayer.stateMachine.states
                    .Select(state => state.state.motion),
                Is.EquivalentTo(comboClips));

            Scene scene = EditorSceneManager.OpenScene(
                CombatLabSceneBuilder.ScenePath,
                OpenSceneMode.Single);
            Assert.That(scene.IsValid(), Is.True);

            Assert.That(
                Object.FindFirstObjectByType<MeleeWeapon>(FindObjectsInactive.Include),
                Is.Not.Null);
            Assert.That(
                Object.FindFirstObjectByType<ShortSwordAttackPresenter>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
            Assert.That(
                Object.FindFirstObjectByType<UpperBodyAimPresenter>(
                    FindObjectsInactive.Include),
                Is.Not.Null);

            Transform sword = Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Single(transform => transform.name == "Equipped Short Sword");
            Animator animator = sword.GetComponentInParent<Animator>();
            Assert.That(animator, Is.Not.Null);
            Assert.That(
                sword.parent,
                Is.EqualTo(animator.GetBoneTransform(HumanBodyBones.RightHand)));
        }
    }
}
