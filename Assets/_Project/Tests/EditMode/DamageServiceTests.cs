using NUnit.Framework;
using UnityEngine;
using WorldBuilder.Gameplay.Combat;

namespace WorldBuilder.Tests
{
    public sealed class DamageServiceTests
    {
        private GameObject target;
        private Health health;

        [SetUp]
        public void SetUp()
        {
            target = new GameObject("Damage Target");
            health = target.AddComponent<Health>();
            health.Configure(100f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(target);
        }

        [Test]
        public void ValidDamageRequestReducesHealth()
        {
            DamageRequest request = new DamageRequest(null, 25f, Vector3.zero, Vector3.forward, "test");

            bool applied = DamageService.TryApply(target, request);

            Assert.That(applied, Is.True);
            Assert.That(health.Current, Is.EqualTo(75f));
        }

        [Test]
        public void LethalDamageRaisesDeathOnlyOnce()
        {
            int deathCount = 0;
            health.Died += _ => deathCount++;
            DamageRequest request = new DamageRequest(null, 150f, Vector3.zero, Vector3.forward, "test");

            DamageService.TryApply(target, request);
            DamageService.TryApply(target, request);

            Assert.That(health.IsAlive, Is.False);
            Assert.That(deathCount, Is.EqualTo(1));
        }

        [Test]
        public void NonPositiveDamageIsRejected()
        {
            DamageRequest request = new DamageRequest(null, 0f, Vector3.zero, Vector3.forward, "test");

            bool applied = DamageService.TryApply(target, request);

            Assert.That(applied, Is.False);
            Assert.That(health.Current, Is.EqualTo(100f));
        }
    }
}
