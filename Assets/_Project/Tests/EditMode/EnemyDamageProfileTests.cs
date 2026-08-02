using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Presentation;

namespace WorldBuilder.Tests
{
    public sealed class EnemyDamageProfileTests
    {
        private GameObject enemy;
        private Health health;
        private EnemyDamageProfile profile;

        [SetUp]
        public void SetUp()
        {
            enemy = new GameObject("Profile Test Enemy");
            health = enemy.AddComponent<Health>();
            health.ConfigureWithFloor(88f, 1f);
            profile = enemy.AddComponent<EnemyDamageProfile>();
            profile.Configure(EnemyCombatVariant.CombatLabDummy);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(enemy);
            FloatingDamageNumberOverlay overlay =
                Object.FindFirstObjectByType<
                    FloatingDamageNumberOverlay>();
            if (overlay != null)
            {
                if (overlay.gameObject.name ==
                    "Floating Damage Numbers")
                {
                    Object.DestroyImmediate(overlay.gameObject);
                }
                else
                {
                    Object.DestroyImmediate(overlay);
                }
            }
        }

        [Test]
        public void ProfileClearsLegacyHealthFloor()
        {
            Assert.That(health.Minimum, Is.Zero);
            Assert.That(health.Maximum, Is.EqualTo(100f));
            Assert.That(profile.IsAlive, Is.True);
        }

        [Test]
        public void FullDrawBowHeadshotDeals105AndKillsInOneHit()
        {
            float appliedDamage = 0f;
            health.Damaged += request =>
                appliedDamage = request.Amount;

            ApplyBowHit(HumanoidHitRegion.Head);

            Assert.That(
                appliedDamage,
                Is.EqualTo(105f).Within(0.01f));
            Assert.That(health.IsAlive, Is.False);
        }

        [Test]
        public void FullDrawBowTorsoHitDeals58AndKillsOnSecondHit()
        {
            ApplyBowHit(HumanoidHitRegion.Torso);
            Assert.That(
                health.Current,
                Is.EqualTo(42f).Within(0.01f));
            Assert.That(health.IsAlive, Is.True);

            ApplyBowHit(HumanoidHitRegion.Torso);

            Assert.That(health.IsAlive, Is.False);
        }

        [Test]
        public void FullDrawBowLimbHitDeals27Point5AndKillsOnFourthHit()
        {
            ApplyBowHit(HumanoidHitRegion.Limb);
            Assert.That(
                health.Current,
                Is.EqualTo(72.5f).Within(0.01f));
            for (int hit = 0; hit < 2; hit++)
            {
                ApplyBowHit(HumanoidHitRegion.Limb);
            }
            Assert.That(health.IsAlive, Is.True);

            ApplyBowHit(HumanoidHitRegion.Limb);

            Assert.That(health.IsAlive, Is.False);
        }

        [Test]
        public void DormantCombatLabDummyCannotDieUntilResetForActivation()
        {
            profile.ConfigureDormantTrainingDummy();
            for (int hit = 0; hit < 6; hit++)
            {
                ApplyBowHit(HumanoidHitRegion.Head);
            }

            Assert.That(health.IsAlive, Is.True);
            Assert.That(health.Current, Is.EqualTo(100f));
            Assert.That(health.Minimum, Is.EqualTo(100f));

            profile.Configure(
                EnemyCombatVariant.CombatLabDummy);
            ApplyBowHit(HumanoidHitRegion.Head);

            Assert.That(health.IsAlive, Is.False);
        }

        [Test]
        public void SwordDamageIsLethalAgainstAnEnemyProfile()
        {
            profile.ReceiveDamage(
                HumanoidHitRegion.Torso,
                new DamageRequest(
                    enemy,
                    20f,
                    enemy.transform.position,
                    Vector3.forward,
                    "prototype-sword"));

            Assert.That(health.IsAlive, Is.False);
        }

        [Test]
        public void DamageCreatesReadableFloatingNumber()
        {
            ApplyBowHit(HumanoidHitRegion.Torso);

            FloatingDamageNumberPresenter presenter =
                enemy.GetComponent<
                    FloatingDamageNumberPresenter>();
            Assert.That(presenter, Is.Not.Null);
            Assert.That(
                presenter.ActiveNumberCount,
                Is.EqualTo(1));
            Assert.That(
                FloatingDamageNumberOverlay.
                    MinimumFontSize,
                Is.EqualTo(14));
            Assert.That(
                FloatingDamageNumberOverlay.
                    MaximumFontSize,
                Is.EqualTo(18));
            FloatingDamageNumberOverlay overlay =
                Object.FindFirstObjectByType<
                    FloatingDamageNumberOverlay>();
            Assert.That(overlay, Is.Not.Null);
            Assert.That(
                overlay.transform.IsChildOf(enemy.transform),
                Is.False,
                "Lethal Raid numbers must outlive the enemy ragdoll.");
        }

        [Test]
        public void CombatLabAndRaidProfilesRemainDistinctAndKillable()
        {
            profile.Configure(EnemyCombatVariant.RaidEnemy);

            Assert.That(
                profile.Variant,
                Is.EqualTo(EnemyCombatVariant.RaidEnemy));
            Assert.That(profile.HeadHitsToKill, Is.EqualTo(1));
            Assert.That(profile.TorsoHitsToKill, Is.EqualTo(2));
            Assert.That(profile.LimbHitsToKill, Is.EqualTo(4));
            Assert.That(health.Minimum, Is.Zero);
        }

        [Test]
        public void PreciseHitboxesFollowTheFinalPresentedRaidPose()
        {
            DefaultExecutionOrder hitboxOrder =
                typeof(HumanoidDamageHitboxRig).GetCustomAttribute<
                    DefaultExecutionOrder>();
            DefaultExecutionOrder stanceOrder =
                typeof(AimStanceLocomotionPresenter).GetCustomAttribute<
                    DefaultExecutionOrder>();
            DefaultExecutionOrder weaponOrder =
                typeof(TwoSlotWeaponPresenter).GetCustomAttribute<
                    DefaultExecutionOrder>();

            Assert.That(hitboxOrder, Is.Not.Null);
            Assert.That(stanceOrder, Is.Not.Null);
            Assert.That(weaponOrder, Is.Not.Null);
            Assert.That(
                hitboxOrder.order,
                Is.GreaterThan(stanceOrder.order));
            Assert.That(
                hitboxOrder.order,
                Is.GreaterThan(weaponOrder.order),
                "Raid damage shapes must update after the final visible bow and locomotion pose.");
        }

        [Test]
        public void RaidMovementCapsuleDoesNotReplacePreciseArrowHitboxes()
        {
            CharacterController movementCollider =
                enemy.AddComponent<CharacterController>();
            GameObject preciseHitbox =
                new GameObject("Precise Torso Hitbox");
            preciseHitbox.transform.SetParent(enemy.transform, false);
            preciseHitbox.AddComponent<BoxCollider>();
            preciseHitbox.AddComponent<HumanoidDamageZone>()
                .Configure(HumanoidHitRegion.Torso);

            Assert.That(
                HumanoidDamageHitboxRig.
                    IsRedundantMovementCollider(movementCollider),
                Is.True,
                "Raid arrows must query the same anatomical colliders used by the controller-free Combat Lab dummy.");
            Assert.That(
                HumanoidDamageHitboxRig.
                    IsRedundantMovementCollider(
                        preciseHitbox.GetComponent<BoxCollider>()),
                Is.False);
        }

        [Test]
        public void DamageServicePrioritizesAnatomicalZoneOverRootHealth()
        {
            GameObject hitbox = new GameObject("Head Hitbox");
            hitbox.transform.SetParent(enemy.transform, false);
            HumanoidDamageZone zone =
                hitbox.AddComponent<HumanoidDamageZone>();
            zone.Configure(HumanoidHitRegion.Head);

            bool applied = DamageService.TryApply(
                hitbox,
                BowRequest());

            Assert.That(applied, Is.True);
            Assert.That(health.IsAlive, Is.False);
        }

        [Test]
        public void RootColliderHitStillUsesEnemyProfileInsteadOfRawHealth()
        {
            DamageRequest headshot = new DamageRequest(
                enemy,
                100f,
                enemy.transform.position + Vector3.up * 1.8f,
                Vector3.forward,
                "prototype-bow");

            bool applied = DamageService.TryApply(enemy, headshot);

            Assert.That(applied, Is.True);
            Assert.That(health.IsAlive, Is.False);
        }

        private void ApplyBowHit(HumanoidHitRegion region)
        {
            profile.ReceiveDamage(region, BowRequest());
        }

        private DamageRequest BowRequest()
        {
            return new DamageRequest(
                enemy,
                100f,
                enemy.transform.position + Vector3.up,
                Vector3.forward,
                "prototype-bow");
        }
    }
}
