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
            Assert.That(controller.layers, Has.Length.EqualTo(4));
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
                gripLayer.stateMachine.defaultState.motion.name,
                Does.EndWith("|Sword_Idle"));

            AnimatorControllerLayer blockLayer = controller.layers[2];
            Assert.That(blockLayer.name, Is.EqualTo(ShortSwordBlockPresenter.BlockLayerName));
            Assert.That(blockLayer.defaultWeight, Is.EqualTo(0f));
            Assert.That(
                blockLayer.iKPass,
                Is.True,
                "The restored guard uses position-only left-hand IK.");
            Assert.That(blockLayer.avatarMask, Is.Not.Null);
            Assert.That(
                blockLayer.stateMachine.states.Single().state.name,
                Is.EqualTo(ShortSwordBlockPresenter.BlockStateName));
            Assert.That(
                blockLayer.stateMachine.states.Single().state.motion.name,
                Is.EqualTo(HumanoidAnimationSetup.GeneratedSwordBlockClipName));

            AnimatorControllerLayer attackLayer = controller.layers[3];
            Assert.That(attackLayer.name, Is.EqualTo(ShortSwordAttackPresenter.AttackLayerName));
            Assert.That(attackLayer.defaultWeight, Is.EqualTo(0f));
            Assert.That(
                attackLayer.iKPass,
                Is.False,
                "The combo layer must not procedurally modify the original first attack.");
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
            Assert.That(
                attackLayer.stateMachine.states
                    .Select(state => state.state.motion.name),
                Is.EquivalentTo(clipNames.Select(name => "Armature|" + name)));

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
                Object.FindFirstObjectByType<ShortSwordBlockPresenter>(
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
