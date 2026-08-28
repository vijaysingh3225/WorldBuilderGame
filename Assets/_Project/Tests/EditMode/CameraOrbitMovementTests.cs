using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class CameraOrbitMovementTests
    {
        [Test]
        public void MiddleMouseOrbitPreservesHeldMovementDirection()
        {
            GameObject player = new GameObject("orbit-movement-player");
            GameObject cameraObject = new GameObject("orbit-camera");
            try
            {
                ThirdPersonMotor motor =
                    player.AddComponent<ThirdPersonMotor>();
                cameraObject.transform.rotation = Quaternion.identity;
                SetPrivateField(
                    motor,
                    "cameraTransform",
                    cameraObject.transform);

                InvokePrivate(
                    motor,
                    "UpdateMovementCameraBasis",
                    true);
                Vector3 beforePan = (Vector3)InvokePrivate(
                    motor,
                    "ToWorldDirection",
                    Vector2.up);

                cameraObject.transform.rotation =
                    Quaternion.Euler(0f, 120f, 0f);
                InvokePrivate(
                    motor,
                    "UpdateMovementCameraBasis",
                    true);
                Vector3 duringPan = (Vector3)InvokePrivate(
                    motor,
                    "ToWorldDirection",
                    Vector2.up);

                Assert.That(motor.MovementCameraBasisLocked, Is.True);
                Assert.That(
                    Vector3.Angle(beforePan, duringPan),
                    Is.LessThan(0.001f),
                    "Orbiting the camera must not rotate held movement input or trigger reversal braking.");

                InvokePrivate(
                    motor,
                    "UpdateMovementCameraBasis",
                    false);
                Vector3 releaseFrame = (Vector3)InvokePrivate(
                    motor,
                    "ToWorldDirection",
                    Vector2.up);
                Assert.That(motor.MovementCameraBasisLocked, Is.True);
                Assert.That(
                    Vector3.Angle(beforePan, releaseFrame),
                    Is.LessThan(0.001f),
                    "The release frame must remain stable while the camera restores its yaw.");

                cameraObject.transform.rotation = Quaternion.identity;
                InvokePrivate(
                    motor,
                    "UpdateMovementCameraBasis",
                    false);
                Vector3 afterOrbit = (Vector3)InvokePrivate(
                    motor,
                    "ToWorldDirection",
                    Vector2.up);
                Assert.That(motor.MovementCameraBasisLocked, Is.False);
                Assert.That(
                    Vector3.Angle(beforePan, afterOrbit),
                    Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void InspectionFacingLockDoesNotDisableSprint()
        {
            Assert.That(
                ThirdPersonMotor.CalculateSprintAllowed(
                    true,
                    true,
                    false,
                    true),
                Is.True,
                "Middle-mouse inspection must preserve toggled sprinting.");
            Assert.That(
                ThirdPersonMotor.CalculateSprintAllowed(
                    true,
                    true,
                    true,
                    true),
                Is.False,
                "A real combat aim must retain the deliberate walk-speed limit.");
            Assert.That(
                ThirdPersonMotor.CalculateSprintAllowed(
                    false,
                    false,
                    false,
                    true),
                Is.True);
        }

        [Test]
        public void SprintInputTogglesAndPlayerDamageCancelsItBriefly()
        {
            GameObject player = new GameObject("damage-sprint-player")
            {
                tag = "Player"
            };
            try
            {
                player.AddComponent<CharacterController>();
                PlayerInputSource input =
                    player.AddComponent<PlayerInputSource>();
                Health health = player.AddComponent<Health>();
                health.Configure(100f);
                ThirdPersonMotor motor =
                    player.AddComponent<ThirdPersonMotor>();

                input.RequestSprintToggle();
                Assert.That(input.SprintToggleActive, Is.True);
                input.RequestSprintToggle();
                Assert.That(input.SprintToggleActive, Is.False);

                input.RequestSprintToggle();
                health.ReceiveDamage(
                    new DamageRequest(
                        null,
                        10f,
                        player.transform.position,
                        Vector3.back,
                        "sprint-interrupt-test"));

                Assert.That(
                    input.SprintToggleActive,
                    Is.False,
                    "Any applied player damage must cancel the latched sprint input.");
                Assert.That(
                    motor.IsSprintInterrupted,
                    Is.True,
                    "Damage must briefly enforce walk speed even if sprint is toggled again immediately.");
                Assert.That(
                    ThirdPersonMotor.DefaultDamageSprintInterruption,
                    Is.InRange(0.2f, 0.5f),
                    "The interruption should be readable without making sprint unavailable for long.");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void FasterPlayerWalkPreservesTheAuthoredWalkGait()
        {
            GameObject player =
                new GameObject("faster-walk-player");
            try
            {
                ThirdPersonMotor motor =
                    player.AddComponent<ThirdPersonMotor>();
                motor.ConfigureWalkSpeed(
                    ThirdPersonMotor.DefaultPlayerWalkSpeed);

                Assert.That(
                    motor.WalkSpeed,
                    Is.EqualTo(
                            ThirdPersonMotor.DefaultWalkSpeed *
                            1.5f)
                        .Within(0.001f));
                Assert.That(
                    motor.SprintSpeed,
                    Is.EqualTo(
                            ThirdPersonMotor.DefaultSprintSpeed)
                        .Within(0.001f),
                    "Increasing walk speed must not change sprint speed.");

                SetPrivateField(
                    motor,
                    "horizontalVelocity",
                    Vector3.forward * motor.WalkSpeed);
                SetPrivateField(
                    motor,
                    "targetHorizontalSpeed",
                    motor.WalkSpeed);

                Assert.That(
                    motor.AnimationHorizontalSpeed,
                    Is.EqualTo(ThirdPersonMotor.DefaultWalkSpeed)
                        .Within(0.001f),
                    "The faster player must still select the authored walk animation rather than blending toward jog.");
                Assert.That(
                    motor.WalkGaitPlaybackScale,
                    Is.EqualTo(1.5f).Within(0.001f),
                    "Walk cadence must increase in proportion to travel speed to prevent foot sliding.");

                SetPrivateField(
                    motor,
                    "horizontalVelocity",
                    Vector3.forward * motor.SprintSpeed);
                SetPrivateField(
                    motor,
                    "targetHorizontalSpeed",
                    motor.SprintSpeed);
                Assert.That(
                    motor.AnimationHorizontalSpeed,
                    Is.EqualTo(motor.SprintSpeed).Within(0.001f));
                Assert.That(
                    motor.WalkGaitPlaybackScale,
                    Is.EqualTo(1f).Within(0.001f),
                    "Sprint cadence must remain unchanged.");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static void SetPrivateField(
            object target,
            string name,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static object InvokePrivate(
            object target,
            string name,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                name,
                BindingFlags.Instance |
                BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(target, arguments);
        }
    }
}
