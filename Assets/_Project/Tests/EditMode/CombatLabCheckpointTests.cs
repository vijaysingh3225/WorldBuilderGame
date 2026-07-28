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
                Is.False,
                "The guard is a frozen authored upper-body pose without runtime IK.");
            Assert.That(blockLayer.avatarMask, Is.Not.Null);
            Assert.That(
                blockLayer.stateMachine.states.Single().state.name,
                Is.EqualTo(ShortSwordBlockPresenter.BlockStateName));
            Assert.That(
                blockLayer.stateMachine.states.Single().state.motion.name,
                Is.EqualTo(HumanoidAnimationSetup.GeneratedSwordBlockClipName));
            AnimationClip blockClip =
                blockLayer.stateMachine.states.Single().state.motion as AnimationClip;
            Assert.That(blockClip, Is.Not.Null);
            EditorCurveBinding[] guardBindings =
                AnimationUtility.GetCurveBindings(blockClip);
            Assert.That(guardBindings, Has.Length.GreaterThanOrEqualTo(30));
            Assert.That(
                guardBindings.All(binding =>
                {
                    AnimationCurve curve =
                        AnimationUtility.GetEditorCurve(blockClip, binding);
                    return curve != null &&
                        curve.length == 2 &&
                        Mathf.Approximately(curve[0].value, curve[1].value);
                }),
                Is.True,
                "Every upper-body guard channel must be constant; the pose cannot track at runtime.");
            Assert.That(
                typeof(ShortSwordBlockPresenter).GetMethod(
                    "OnAnimatorIK",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public),
                Is.Null,
                "The guard presenter must not use per-frame Animator IK.");

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
            Assert.That(
                Vector3.Distance(
                    sword.localPosition,
                    new Vector3(
                        -0.00072210626f,
                        -0.07712167f,
                        -0.068963856f)),
                Is.LessThan(0.0001f));
            Assert.That(
                Quaternion.Angle(
                    sword.localRotation,
                    new Quaternion(
                        -0.0575469f,
                        0.7047954f,
                        -0.06148468f,
                        0.70439446f)),
                Is.LessThan(0.001f));
            ShortSwordBlockPresenter blockPresenter =
                animator.GetComponent<ShortSwordBlockPresenter>();
            SerializedObject serializedBlock =
                new SerializedObject(blockPresenter);
            Quaternion guardRotation = serializedBlock
                .FindProperty("authoredGuardSwordLocalRotation")
                .quaternionValue;
            Assert.That(
                Quaternion.Angle(
                    guardRotation,
                    new Quaternion(
                        -0.28831902f,
                        0.8950361f,
                        -0.17096046f,
                        0.29420236f)),
                Is.LessThan(0.001f));
        }

        [Test]
        public void CombatLabUsesReversibleSeamlessLowPolyMannequin()
        {
            Scene scene = EditorSceneManager.OpenScene(
                CombatLabSceneBuilder.ScenePath,
                OpenSceneMode.Single);
            Assert.That(scene.IsValid(), Is.True);

            GameObject player = GameObject.Find("Player");
            Assert.That(player, Is.Not.Null);
            SkinnedMeshRenderer seamless = player
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Single(
                    renderer =>
                        renderer.name == "MannequinSeamlessLowPoly_Renderer");
            Assert.That(seamless.enabled, Is.True);
            Assert.That(seamless.sharedMesh, Is.Not.Null);
            Assert.That(
                seamless.sharedMesh.triangles.Length / 3,
                Is.EqualTo(2596));
            Assert.That(seamless.bones, Has.Length.EqualTo(53));
            Assert.That(seamless.bones, Has.All.Not.Null);
            Assert.That(seamless.sharedMaterials, Has.Length.EqualTo(1));
            Material charcoal = seamless.sharedMaterial;
            Assert.That(charcoal, Is.Not.Null);
            Color charcoalColor = charcoal.GetColor("_BaseColor");
            Assert.That(charcoalColor.r, Is.EqualTo(0.22f).Within(0.0001f));
            Assert.That(charcoalColor.g, Is.EqualTo(0.22f).Within(0.0001f));
            Assert.That(charcoalColor.b, Is.EqualTo(0.22f).Within(0.0001f));

            Animator animator = seamless.GetComponentInParent<Animator>();
            SkinnedMeshRenderer lowPolyFallback = animator
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Single(renderer => renderer.name == "MannequinLowPoly_Renderer");
            Assert.That(lowPolyFallback.enabled, Is.False);
            Assert.That(
                lowPolyFallback.sharedMesh.triangles.Length / 3,
                Is.EqualTo(1972));

            SkinnedMeshRenderer original = animator
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Single(renderer => renderer.name == "Mannequin");
            Assert.That(original.enabled, Is.False);
            Assert.That(original.sharedMesh, Is.Not.Null);

            GameObject dummy = GameObject.Find("Raider Prototype");
            Assert.That(dummy, Is.Not.Null);
            SkinnedMeshRenderer dummySeamless = dummy
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Single(
                    renderer =>
                        renderer.name == "MannequinSeamlessLowPoly_Renderer");
            Assert.That(dummySeamless.enabled, Is.True);
            Assert.That(
                dummySeamless.sharedMesh.triangles.Length / 3,
                Is.EqualTo(2596));
            Assert.That(dummySeamless.sharedMaterials, Has.Length.EqualTo(1));
            Color dummyColor =
                dummySeamless.sharedMaterial.GetColor("_BaseColor");
            Assert.That(dummyColor.r, Is.EqualTo(0.42f).Within(0.0001f));
            Assert.That(dummyColor.g, Is.EqualTo(0.035f).Within(0.0001f));
            Assert.That(dummyColor.b, Is.EqualTo(0.03f).Within(0.0001f));
            SkinnedMeshRenderer dummyOriginal = dummy
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Single(renderer => renderer.name == "Mannequin");
            Assert.That(dummyOriginal.enabled, Is.False);
        }

    }
}
