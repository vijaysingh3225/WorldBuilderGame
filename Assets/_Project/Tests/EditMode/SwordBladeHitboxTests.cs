using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Input;
using WorldBuilder.Gameplay.Presentation;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class SwordBladeHitboxTests
    {
        [Test]
        public void SweptVisibleBlade_DamagesOnFirstCrossing_OnlyOncePerSwing()
        {
            GameObject owner = new GameObject("Sword Owner");
            GameObject blade = new GameObject("Visible Blade");
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                owner.AddComponent<PlayerInputSource>();
                owner.AddComponent<Health>();
                MeleeWeapon weapon = owner.AddComponent<MeleeWeapon>();

                blade.transform.SetParent(owner.transform, false);
                blade.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                weapon.ConfigureBlade(blade.transform, 0.78f, 0.06f);

                target.name = "Sweep Target";
                target.transform.position = new Vector3(1f, 0.39f, 0f);
                target.transform.localScale = Vector3.one * 0.1f;
                Health targetHealth = target.AddComponent<Health>();
                int damageEvents = 0;
                targetHealth.Damaged += _ => damageEvents++;
                Physics.SyncTransforms();

                Assert.That(weapon.BeginSwing(), Is.True);
                weapon.OpenBladeDamageWindow();
                InvokeLateUpdate(weapon);
                Assert.That(targetHealth.Current, Is.EqualTo(100f));

                blade.transform.position = new Vector3(2f, 0f, 0f);
                Physics.SyncTransforms();
                InvokeLateUpdate(weapon);

                Assert.That(targetHealth.Current, Is.EqualTo(40f));
                Assert.That(damageEvents, Is.EqualTo(1));

                InvokeLateUpdate(weapon);
                Assert.That(targetHealth.Current, Is.EqualTo(40f));
                Assert.That(damageEvents, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void AnatomicalHitboxesShareOneDamageEventPerSwing()
        {
            GameObject owner = new GameObject("Sword Owner");
            GameObject blade = new GameObject("Visible Blade");
            GameObject target = new GameObject("Humanoid Target");
            GameObject torso = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                owner.AddComponent<PlayerInputSource>();
                owner.AddComponent<Health>();
                MeleeWeapon weapon = owner.AddComponent<MeleeWeapon>();
                blade.transform.SetParent(owner.transform, false);
                blade.transform.SetPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);
                weapon.ConfigureBlade(
                    blade.transform,
                    0.78f,
                    0.08f);

                Health targetHealth = target.AddComponent<Health>();
                target.AddComponent<EnemyDamageProfile>()
                    .Configure(EnemyCombatVariant.CombatLabDummy);
                ConfigureZone(
                    torso,
                    target.transform,
                    new Vector3(0.90f, 0.39f, 0f),
                    HumanoidHitRegion.Torso);
                ConfigureZone(
                    arm,
                    target.transform,
                    new Vector3(1.10f, 0.39f, 0f),
                    HumanoidHitRegion.Limb);
                int damageEvents = 0;
                targetHealth.Damaged += _ => damageEvents++;
                Physics.SyncTransforms();

                Assert.That(weapon.BeginSwing(), Is.True);
                weapon.OpenBladeDamageWindow();
                InvokeLateUpdate(weapon);
                blade.transform.position = new Vector3(2f, 0f, 0f);
                Physics.SyncTransforms();
                InvokeLateUpdate(weapon);

                Assert.That(
                    targetHealth.Current,
                    Is.EqualTo(40f).Within(0.001f));
                Assert.That(damageEvents, Is.EqualTo(1));
                Assert.That(
                    target.GetComponent<FloatingDamageNumberPresenter>()
                        .ActiveNumberCount,
                    Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void LegacyPrototypeSwordDamageMigratesToSixty()
        {
            Assert.That(
                MeleeWeapon.ResolveConfiguredDamage(
                    "prototype-sword",
                    20f),
                Is.EqualTo(60f).Within(0.001f));
            Assert.That(
                MeleeWeapon.ResolveConfiguredDamage(
                    "custom-sword",
                    20f),
                Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void HeavyOpeningAttackScalesDamageAndLungeWithHoldDuration()
        {
            float threshold =
                ShortSwordAttackPresenter.HeavyChargeThreshold;
            float maximum =
                ShortSwordAttackPresenter.HeavyMaximumChargeDuration;
            float minimumCharge =
                ShortSwordAttackPresenter.CalculateHeavyChargeNormalized(
                    threshold,
                    threshold,
                    maximum);
            float fullCharge =
                ShortSwordAttackPresenter.CalculateHeavyChargeNormalized(
                    maximum,
                    threshold,
                    maximum);

            Assert.That(minimumCharge, Is.EqualTo(0f).Within(0.001f));
            Assert.That(fullCharge, Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                ShortSwordAttackPresenter.HeavyMinimumLungeDistance,
                Is.EqualTo(0f).Within(0.0001f));
            Assert.That(
                ShortSwordAttackPresenter.HeavyMaximumLungeDistance,
                Is.EqualTo(1.80f).Within(0.001f));
            Assert.That(
                ShortSwordAttackPresenter.CalculateHeavyChargeAnimationTime(
                    0f,
                    ShortSwordAttackPresenter.HeavyChargeStartNormalizedTime,
                    ShortSwordAttackPresenter.HeavyChargeHoldNormalizedTime,
                    1.4f),
                Is.EqualTo(ShortSwordAttackPresenter.
                    HeavyChargeStartNormalizedTime).Within(0.001f));
            Assert.That(
                ShortSwordAttackPresenter.CalculateHeavyChargeAnimationTime(
                    10f,
                    ShortSwordAttackPresenter.HeavyChargeStartNormalizedTime,
                    ShortSwordAttackPresenter.HeavyChargeHoldNormalizedTime,
                    1.4f),
                Is.EqualTo(ShortSwordAttackPresenter.
                    HeavyChargeHoldNormalizedTime).Within(0.001f));
            Assert.That(
                ShortSwordAttackPresenter.CalculateHeavyDamageMultiplier(
                    fullCharge,
                    ShortSwordAttackPresenter.
                        HeavyMinimumDamageMultiplier,
                    ShortSwordAttackPresenter.
                        HeavyMaximumDamageMultiplier),
                Is.EqualTo(ShortSwordAttackPresenter.
                    HeavyMaximumDamageMultiplier).Within(0.001f));
            Assert.That(
                ShortSwordAttackPresenter.CalculateHeavyLungeDistance(
                    0f,
                    ShortSwordAttackPresenter.HeavyMinimumLungeDistance,
                    ShortSwordAttackPresenter.HeavyMaximumLungeDistance),
                Is.EqualTo(ShortSwordAttackPresenter.
                    HeavyMinimumLungeDistance).Within(0.001f));
            Assert.That(
                ShortSwordAttackPresenter.CalculateHeavyLungeDistance(
                    0.1f,
                    ShortSwordAttackPresenter.HeavyMinimumLungeDistance,
                    ShortSwordAttackPresenter.HeavyMaximumLungeDistance),
                Is.EqualTo(
                    ShortSwordAttackPresenter.HeavyMaximumLungeDistance *
                    0.1f).Within(0.001f));
            Assert.That(
                ShortSwordAttackPresenter.CalculateHeavyLungeDistance(
                    0.5f,
                    ShortSwordAttackPresenter.HeavyMinimumLungeDistance,
                    ShortSwordAttackPresenter.HeavyMaximumLungeDistance),
                Is.EqualTo(
                    ShortSwordAttackPresenter.HeavyMaximumLungeDistance *
                    0.5f).Within(0.001f));
            Assert.That(
                ShortSwordAttackPresenter.CalculateHeavyLungeDistance(
                    fullCharge,
                    ShortSwordAttackPresenter.HeavyMinimumLungeDistance,
                    ShortSwordAttackPresenter.HeavyMaximumLungeDistance),
                Is.EqualTo(ShortSwordAttackPresenter.
                    HeavyMaximumLungeDistance).Within(0.001f));
        }

        [Test]
        public void HeldGraceStartsAHeavyOnlyWhenInputRemainsHeld()
        {
            Assert.That(
                ShortSwordAttackPresenter.ShouldBeginQueuedHeavyCharge(
                    true,
                    true,
                    true),
                Is.True);
            Assert.That(
                ShortSwordAttackPresenter.ShouldBeginQueuedHeavyCharge(
                    true,
                    false,
                    true),
                Is.False);
            Assert.That(
                ShortSwordAttackPresenter.ShouldBeginQueuedHeavyCharge(
                    false,
                    true,
                    true),
                Is.False);
        }

        private static void ConfigureZone(
            GameObject zoneObject,
            Transform parent,
            Vector3 position,
            HumanoidHitRegion region)
        {
            zoneObject.transform.SetParent(parent, false);
            zoneObject.transform.position = position;
            zoneObject.transform.localScale = Vector3.one * 0.16f;
            zoneObject.AddComponent<HumanoidDamageZone>()
                .Configure(region);
        }

        private static void InvokeLateUpdate(MeleeWeapon weapon)
        {
            MethodInfo lateUpdate = typeof(MeleeWeapon).GetMethod(
                "LateUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(lateUpdate, Is.Not.Null);
            lateUpdate.Invoke(weapon, null);
        }
    }
}
