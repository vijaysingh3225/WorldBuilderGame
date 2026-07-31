using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;

namespace WorldBuilder.Tests
{
    public sealed class EnemyPerceptionTests
    {
        private GameObject player;
        private GameObject enemy;
        private GameObject obstruction;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(obstruction);
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(player);
        }

        [Test]
        public void DormantTrainingDummyRequiresExplicitActivation()
        {
            player = CreateTarget(
                "Player",
                new Vector3(0f, 0f, -20f));
            enemy = CreateEnemy(Vector3.zero);
            Health enemyHealth = enemy.GetComponent<Health>();
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();

            enemyHealth.ReceiveDamage(
                new DamageRequest(
                    player,
                    10f,
                    enemy.transform.position,
                    Vector3.forward,
                    "prototype-bow"));

            Assert.That(brain.IsActivated, Is.False);
            Assert.That(brain.IsAlerted, Is.False);
            Assert.That(brain.HasVisualContact, Is.False);
            Assert.That(enemyHealth.Current, Is.EqualTo(78f));

            SetPrivateField(
                brain,
                "target",
                player.transform);
            brain.ActivateForDiagnostics();

            Assert.That(brain.IsActivated, Is.True);
            Assert.That(brain.IsAlerted, Is.True);
            Assert.That(
                brain.LastKnownPosition,
                Is.EqualTo(player.transform.position));
        }

        [Test]
        public void DormantArenaEnemyAlertsWhenDamaged()
        {
            player = CreateTarget(
                "Player",
                new Vector3(0f, 0f, -20f));
            enemy = CreateEnemy(Vector3.zero);
            Health enemyHealth = enemy.GetComponent<Health>();
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
            brain.ConfigureAsTrainingDummy(
                requireManualActivation: false);

            enemyHealth.ReceiveDamage(
                new DamageRequest(
                    player,
                    10f,
                    enemy.transform.position,
                    Vector3.forward,
                    "prototype-bow"));

            Assert.That(brain.IsActivated, Is.True);
            Assert.That(brain.IsAlerted, Is.True);
            Assert.That(brain.HasVisualContact, Is.False);
            Assert.That(enemyHealth.Current, Is.EqualTo(78f));
            Assert.That(
                brain.LastKnownPosition.z,
                Is.EqualTo(-10f).Within(0.01f));
        }

        [Test]
        public void SolidCoverBlocksCurrentVisualContact()
        {
            player = CreateTarget(
                "Player",
                new Vector3(500f, 0f, 506f));
            enemy = CreateEnemy(
                new Vector3(500f, 0f, 500f));
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
            brain.Configure(player.transform);
            Physics.SyncTransforms();

            bool initialSight = EvaluateSight(brain);
            Assert.That(
                initialSight,
                Is.True,
                DescribeSightRay(brain));

            obstruction = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            obstruction.name = "Sight Obstruction";
            obstruction.transform.position =
                new Vector3(500f, 0.75f, 503f);
            obstruction.transform.localScale =
                new Vector3(2f, 2f, 0.6f);
            Physics.SyncTransforms();

            Assert.That(EvaluateSight(brain), Is.False);
        }

        [Test]
        public void LosingSightPreservesTheLastVisiblePosition()
        {
            player = CreateTarget(
                "Player",
                new Vector3(500f, 0f, 506f));
            enemy = CreateEnemy(
                new Vector3(500f, 0f, 500f));
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
            brain.Configure(player.transform);
            Physics.SyncTransforms();

            UpdatePerception(brain);
            Assert.That(brain.HasVisualContact, Is.True);
            Vector3 lastVisible = brain.LastKnownPosition;

            obstruction = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            obstruction.transform.position =
                new Vector3(500f, 0.75f, 503f);
            obstruction.transform.localScale =
                new Vector3(2f, 2f, 0.6f);
            player.transform.position =
                new Vector3(501f, 0f, 506f);
            Physics.SyncTransforms();

            UpdatePerception(brain);

            Assert.That(brain.IsAlerted, Is.True);
            Assert.That(brain.HasVisualContact, Is.False);
            Assert.That(brain.LastKnownPosition, Is.EqualTo(lastVisible));
        }

        private static GameObject CreateTarget(
            string name,
            Vector3 position)
        {
            var target = new GameObject(name);
            if (name == "Player")
            {
                target.tag = "Player";
                target.layer = 2;
            }
            target.transform.position = position;
            CapsuleCollider collider =
                target.AddComponent<CapsuleCollider>();
            collider.height = 2f;
            collider.radius = 0.3f;
            collider.center = Vector3.up;
            return target;
        }

        private static GameObject CreateEnemy(Vector3 position)
        {
            var root = new GameObject("Enemy");
            root.transform.position = position;
            CharacterController controller =
                root.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.25f;
            Health health = root.AddComponent<Health>();
            health.ConfigureWithFloor(88f, 0f);
            EnemyBrain brain = root.AddComponent<EnemyBrain>();
            InvokePrivate(brain, "Awake");
            return root;
        }

        private static bool EvaluateSight(EnemyBrain brain)
        {
            MethodInfo targetPointMethod =
                ResolvePrivateMethod(
                    "ResolveTargetChestPoint");
            MethodInfo sightMethod =
                ResolvePrivateMethod("CanSeeTarget");
            Vector3 targetPoint = (Vector3)
                targetPointMethod.Invoke(brain, null);
            return (bool)sightMethod.Invoke(
                brain,
                new object[] { targetPoint });
        }

        private static void UpdatePerception(EnemyBrain brain)
        {
            MethodInfo targetPointMethod =
                ResolvePrivateMethod(
                    "ResolveTargetChestPoint");
            Vector3 targetPoint = (Vector3)
                targetPointMethod.Invoke(brain, null);
            ResolvePrivateMethod("UpdatePerception").Invoke(
                brain,
                new object[] { targetPoint });
        }

        private static void InvokePrivate(
            EnemyBrain brain,
            string methodName)
        {
            ResolvePrivateMethod(methodName).Invoke(
                brain,
                null);
        }

        private static void SetPrivateField(
            EnemyBrain brain,
            string fieldName,
            object value)
        {
            FieldInfo field = typeof(EnemyBrain).GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(brain, value);
        }

        private static MethodInfo ResolvePrivateMethod(
            string methodName)
        {
            MethodInfo method = typeof(EnemyBrain).GetMethod(
                methodName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method;
        }

        private static string DescribeSightRay(EnemyBrain brain)
        {
            Vector3 origin = (Vector3)
                ResolvePrivateMethod("ResolveSightOrigin")
                    .Invoke(brain, null);
            Vector3 targetPoint = (Vector3)
                ResolvePrivateMethod("ResolveTargetChestPoint")
                    .Invoke(brain, null);
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                (targetPoint - origin).normalized,
                Vector3.Distance(origin, targetPoint) + 0.15f,
                ~0,
                QueryTriggerInteraction.Ignore);
            string names = string.Empty;
            for (int index = 0; index < hits.Length; index++)
            {
                names +=
                    $"{hits[index].collider.name}:" +
                    $"{hits[index].distance:0.00} ";
            }

            return
                $"origin={origin}; target={targetPoint}; " +
                $"forward={brain.transform.forward}; hits={names}";
        }
    }
}
