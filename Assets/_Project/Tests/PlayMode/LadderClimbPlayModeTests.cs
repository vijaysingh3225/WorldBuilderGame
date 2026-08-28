using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Tests.PlayMode
{
    public sealed class LadderClimbPlayModeTests
    {
        [UnityTest]
        public IEnumerator PlayerCompletesClimbAndRestoresController()
        {
            GameObject player = new GameObject("Ladder Test Player");
            player.SetActive(false);
            try
            {
                player.tag = "Player";
                CharacterController controller =
                    player.AddComponent<CharacterController>();
                player.AddComponent<PlayerInputSource>();
                player.AddComponent<Health>();
                ThirdPersonMotor motor =
                    player.AddComponent<ThirdPersonMotor>();
                player.SetActive(true);

                Vector3 bottom = Vector3.zero;
                Vector3 top = new Vector3(0.8f, 1.5f, 0.5f);
                Assert.That(
                    motor.TryBeginLadderClimb(
                        bottom,
                        top,
                        Vector3.forward),
                    Is.True);
                Assert.That(controller.enabled, Is.False);

                float timeout = Time.time + 2.5f;
                while (motor.IsClimbingLadder && Time.time < timeout)
                {
                    yield return null;
                }

                Assert.That(motor.IsClimbingLadder, Is.False);
                Assert.That(controller.enabled, Is.True);
                Assert.That(
                    Vector3.Distance(player.transform.position, top),
                    Is.LessThan(0.02f));
            }
            finally
            {
                Object.Destroy(player);
            }
        }
    }
}
