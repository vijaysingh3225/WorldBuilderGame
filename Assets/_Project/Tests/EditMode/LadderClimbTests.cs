using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using WorldBuilder.Editor;
using WorldBuilder.Gameplay.Loop.Scenes;
using WorldBuilder.Gameplay.Presentation;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class LadderClimbTests
    {
        [Test]
        public void ControllerUsesLoopingFullBodyLadderLayerBelowStagger()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    HumanoidAnimationSetup.ControllerPath);

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.layers, Has.Length.EqualTo(6));
            AnimatorControllerLayer ladderLayer = controller.layers[4];
            AnimatorControllerLayer staggerLayer = controller.layers[5];
            Assert.That(
                ladderLayer.name,
                Is.EqualTo(LadderClimbPresenter.LayerName));
            Assert.That(ladderLayer.defaultWeight, Is.Zero);
            Assert.That(ladderLayer.avatarMask, Is.Null);
            Assert.That(ladderLayer.iKPass, Is.False);
            AnimatorState ladderState =
                ladderLayer.stateMachine.states.Single().state;
            Assert.That(
                ladderState.name,
                Is.EqualTo(LadderClimbPresenter.StateName));
            Assert.That(
                ladderState.motion.name,
                Is.EqualTo(LadderClimbPresenter.ClipName));
            Assert.That(ladderState.motion.isLooping, Is.True);
            Assert.That(
                staggerLayer.name,
                Is.EqualTo(HitReactionPresenter.StaggerLayerName),
                "Sword-hit reactions must retain priority over ladder motion.");
        }

        [Test]
        public void LadderPointStoresAnUpwardNormalizedTraversal()
        {
            GameObject ladder = new GameObject("Ladder Test");
            try
            {
                LadderClimbPoint point = ladder.AddComponent<LadderClimbPoint>();
                point.Configure(
                    new Vector3(1f, 2f, 3f),
                    new Vector3(2f, 8f, 4f),
                    new Vector3(4f, 3f, 0f));

                Assert.That(point.ClimbHeight, Is.EqualTo(6f));
                Assert.That(point.TopPosition.y, Is.GreaterThan(point.BottomPosition.y));
                Assert.That(point.ClimbFacing.y, Is.Zero);
                Assert.That(point.ClimbFacing.sqrMagnitude, Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(ladder);
            }
        }
    }
}
