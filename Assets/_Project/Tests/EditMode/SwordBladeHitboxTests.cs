using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Input;
using WorldBuilder.Gameplay.Presentation;
using WorldBuilder.Gameplay.Weapons;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class SwordBladeHitboxTests
    {
        [Test]
        public void SwingTrail_RecreatesRuntimeMesh_WhenSceneChildrenRemain()
        {
            GameObject owner = new GameObject("Sword Trail Owner");
            try
            {
                MeleeWeapon weapon = owner.AddComponent<MeleeWeapon>();
                GameObject blade = new GameObject("Visible Blade");
                blade.transform.SetParent(owner.transform, false);
                const float bladeLength = 1.20f;
                blade.transform.localPosition = Vector3.down *
                    (bladeLength * 0.5f);
                weapon.ConfigureBlade(blade.transform, bladeLength, 0.06f);
                ShortSwordSwingTrail trail =
                    owner.AddComponent<ShortSwordSwingTrail>();
                trail.Configure(weapon);
                Mesh originalMesh = trail.SweepMesh;
                Assert.That(originalMesh, Is.Not.Null);

                FieldInfo sweepMeshField = typeof(ShortSwordSwingTrail)
                    .GetField(
                        "sweepMesh",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(sweepMeshField, Is.Not.Null);
                sweepMeshField.SetValue(trail, null);
                Object.DestroyImmediate(originalMesh);

                trail.Configure(weapon);

                Assert.That(trail.SweepMesh, Is.Not.Null);
                MeshFilter filter = owner
                    .GetComponentInChildren<MeshFilter>(true);
                MeshRenderer renderer = owner
                    .GetComponentInChildren<MeshRenderer>(true);
                Assert.That(filter, Is.Not.Null);
                Assert.That(renderer, Is.Not.Null);
                Assert.That(filter.sharedMesh, Is.SameAs(trail.SweepMesh));
                filter.transform.localPosition = new Vector3(0.31f, 0.12f, 0.2f);
                filter.transform.localRotation = Quaternion.Euler(0f, 17f, 0f);
                Assert.That(weapon.BeginSwing(), Is.True);
                weapon.OpenBladeDamageWindow();
                Assert.DoesNotThrow(trail.BeginSlice);
                Assert.That(renderer.enabled, Is.False);

                // Rotate around a stationary midpoint. Center-only sampling misses
                // this common sword motion even though both blade endpoints move.
                blade.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
                blade.transform.localPosition = Vector3.left *
                    (bladeLength * 0.5f);
                MethodInfo lateUpdate = typeof(ShortSwordSwingTrail)
                    .GetMethod(
                        "LateUpdate",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(lateUpdate, Is.Not.Null);
                Assert.DoesNotThrow(() => lateUpdate.Invoke(trail, null));
                Assert.That(
                    trail.SweepMesh.vertexCount,
                    Is.GreaterThanOrEqualTo(4),
                    "A moving blade should rebuild the visible swept ribbon.");
                Assert.That(renderer.enabled, Is.True);

                Vector3[] meshVertices = trail.SweepMesh.vertices;
                Vector3 renderedTip = filter.transform.TransformPoint(
                    meshVertices[meshVertices.Length - 2]);
                Vector3 renderedBase = filter.transform.TransformPoint(
                    meshVertices[meshVertices.Length - 1]);
                weapon.GetBladeSegment(
                    out Vector3 authoritativeBase,
                    out Vector3 authoritativeTip);
                Assert.That(
                    Vector3.Distance(renderedBase, authoritativeBase),
                    Is.LessThan(0.0001f),
                    "The newest ribbon edge must begin on the live blade base.");
                Assert.That(
                    Vector3.Distance(renderedTip, authoritativeTip),
                    Is.LessThan(0.0001f),
                    "The newest ribbon edge must end on the live blade tip.");
                Assert.That(
                    Vector3.Distance(renderedBase, renderedTip),
                    Is.EqualTo(bladeLength).Within(0.0001f),
                    "The ribbon span must adapt to the configured blade length.");

                weapon.CloseBladeDamageWindow();
                Assert.DoesNotThrow(() => lateUpdate.Invoke(trail, null));
                Assert.That(renderer.enabled, Is.False);
                Assert.That(trail.SweepMesh.vertexCount, Is.Zero);
                Assert.That(trail.SampleCount, Is.Zero);
                Assert.That(trail.IsEmitting, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ExplicitBladeSegmentUsesVisibleBladeLocalEndpoints()
        {
            GameObject owner = new GameObject("Visible Blade Source Owner");
            GameObject blade = new GameObject("Visible Generated Blade");
            try
            {
                MeleeWeapon weapon = owner.AddComponent<MeleeWeapon>();
                blade.transform.SetParent(owner.transform, false);
                blade.transform.localPosition = new Vector3(0.2f, 0.4f, -0.1f);
                blade.transform.localRotation = Quaternion.Euler(12f, 25f, -18f);
                blade.transform.localScale = Vector3.one * 1.35f;
                Vector3 localBase = Vector3.up * -0.03f;
                Vector3 localTip = Vector3.up * 0.82f;

                weapon.ConfigureBladeSegment(
                    blade.transform,
                    localBase,
                    localTip,
                    0.08f);
                weapon.GetBladeSegment(
                    out Vector3 actualBase,
                    out Vector3 actualTip);

                Assert.That(
                    Vector3.Distance(
                        actualBase,
                        blade.transform.TransformPoint(localBase)),
                    Is.LessThan(0.00001f));
                Assert.That(
                    Vector3.Distance(
                        actualTip,
                        blade.transform.TransformPoint(localTip)),
                    Is.LessThan(0.00001f));
                Assert.That(
                    weapon.Reach,
                    Is.EqualTo(Vector3.Distance(actualBase, actualTip))
                        .Within(0.00001f));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

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
                ShortSwordCombatProfile profile =
                    ShortSwordCombatProfile.Default;
                profile.HitPauseDuration = 0.061f;
                profile.StaggerDuration = 0.31f;
                profile.ImpactShakeMultiplier = 1.27f;
                weapon.ConfigureGeneratedCombatProfile(profile);

                target.name = "Sweep Target";
                target.transform.position = new Vector3(1f, 0.39f, 0f);
                target.transform.localScale = Vector3.one * 0.1f;
                Health targetHealth = target.AddComponent<Health>();
                int damageEvents = 0;
                DamageRequest appliedRequest = default;
                targetHealth.Damaged += request =>
                {
                    damageEvents++;
                    appliedRequest = request;
                };
                Physics.SyncTransforms();

                Assert.That(weapon.BeginSwing(), Is.True);
                weapon.OpenBladeDamageWindow();
                InvokeLateUpdate(weapon);
                Assert.That(targetHealth.Current, Is.EqualTo(100f));

                blade.transform.position = new Vector3(2f, 0f, 0f);
                Physics.SyncTransforms();
                InvokeLateUpdate(weapon);

                Assert.That(targetHealth.Current, Is.EqualTo(60f));
                Assert.That(damageEvents, Is.EqualTo(1));
                Assert.That(
                    appliedRequest.HitPauseDuration,
                    Is.EqualTo(0.061f).Within(0.001f));
                Assert.That(
                    appliedRequest.StaggerDuration,
                    Is.EqualTo(0.31f).Within(0.001f));
                Assert.That(
                    appliedRequest.ImpactStrength,
                    Is.EqualTo(1.27f).Within(0.001f));

                InvokeLateUpdate(weapon);
                Assert.That(targetHealth.Current, Is.EqualTo(60f));
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
                    Is.EqualTo(60f).Within(0.001f));
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
        public void SerializedPrototypeSwordDamageMigratesToForty()
        {
            Assert.That(
                MeleeWeapon.ResolveConfiguredDamage(
                    "prototype-sword",
                    20f),
                Is.EqualTo(40f).Within(0.001f));
            Assert.That(
                MeleeWeapon.ResolveConfiguredDamage(
                    "prototype-sword",
                    60f),
                Is.EqualTo(40f).Within(0.001f));
            Assert.That(
                MeleeWeapon.ResolveConfiguredDamage(
                    "custom-sword",
                    20f),
                Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void GeneratedCombatProfileChangesDamageCooldownAndFeel()
        {
            GameObject owner = new GameObject("Generated Sword Stats");
            try
            {
                MeleeWeapon weapon = owner.AddComponent<MeleeWeapon>();
                ShortSwordCombatProfile profile =
                    ShortSwordCombatProfile.Default;
                profile.DamageMultiplier = 1.12f;
                profile.AttackSpeedMultiplier = 1.10f;
                profile.Heft = 0.82f;
                profile.Handling = 0.34f;
                profile.HitPauseDuration = 0.061f;
                profile.StaggerDuration = 0.31f;
                profile.ImpactShakeMultiplier = 1.27f;
                profile.SwingPitchMultiplier = 0.93f;
                profile.SwingVolumeMultiplier = 1.31f;
                profile.TrailPersistenceMultiplier = 0.74f;
                profile.TrailOpacityMultiplier = 0.86f;

                weapon.ConfigureGeneratedCombatProfile(profile);

                Assert.That(weapon.Damage, Is.EqualTo(44.8f).Within(0.001f));
                Assert.That(
                    weapon.Cooldown,
                    Is.EqualTo(0.15f / 1.10f).Within(0.001f));
                Assert.That(weapon.Heft, Is.EqualTo(0.82f));
                Assert.That(weapon.Handling, Is.EqualTo(0.34f));
                Assert.That(weapon.HitPauseDuration, Is.EqualTo(0.061f));
                Assert.That(weapon.StaggerDuration, Is.EqualTo(0.31f));
                Assert.That(
                    weapon.ImpactShakeMultiplier,
                    Is.EqualTo(1.27f));
                Assert.That(weapon.SwingPitchMultiplier, Is.EqualTo(0.93f));
                Assert.That(weapon.SwingVolumeMultiplier, Is.EqualTo(1.31f));
                Assert.That(
                    weapon.TrailPersistenceMultiplier,
                    Is.EqualTo(0.74f));
                Assert.That(weapon.TrailOpacityMultiplier, Is.EqualTo(0.86f));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void SwingTrailUsesGeneratedSliceCharacter()
        {
            GameObject owner = new GameObject("Generated Sword Trail Feel");
            try
            {
                ShortSwordSwingTrail trail =
                    owner.AddComponent<ShortSwordSwingTrail>();
                ShortSwordCombatProfile profile =
                    ShortSwordCombatProfile.Default;
                profile.TrailPersistenceMultiplier = 1.75f;
                profile.TrailOpacityMultiplier = 1.40f;

                trail.ConfigureGeneratedCombatProfile(profile);

                Assert.That(
                    trail.EffectiveTrailLifetime,
                    Is.EqualTo(0.085f).Within(0.0001f),
                    "Even the longest slicing profile must stay visually tight.");
                Assert.That(
                    trail.EffectiveMaximumOpacity,
                    Is.EqualTo(0.28f * 1.40f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void RaidPresentationAppliesSeededGeometryAndCombatProfileTogether()
        {
            GameObject owner = new GameObject("Raid Sword Owner");
            GameObject swordRoot = new GameObject("Sword Socket");
            try
            {
                swordRoot.transform.SetParent(owner.transform, false);
                MeleeWeapon weapon = owner.AddComponent<MeleeWeapon>();
                RaidShortSwordPresentation presentation =
                    RaidShortSwordPresentation.Replace(
                        swordRoot.transform,
                        18231);

                presentation.ConfigureMeleeWeapon(weapon);

                Assert.That(presentation.BladeHitbox, Is.Not.Null);
                Assert.That(
                    weapon.Reach,
                    Is.EqualTo(presentation.BladeLength).Within(0.0001f));
                Assert.That(
                    JsonUtility.ToJson(weapon.CombatProfile),
                    Is.EqualTo(JsonUtility.ToJson(
                        presentation.Generator.CurrentDefinition.
                            CombatProfile)));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void BasicSwordRequiresFullThreeHitComboAgainstStandardHealth()
        {
            GameObject target = new GameObject("Standard Sword Target");
            try
            {
                Health health = target.AddComponent<Health>();
                var hit = new DamageRequest(
                    null,
                    MeleeWeapon.DefaultSwordDamage,
                    target.transform.position,
                    Vector3.forward,
                    "prototype-sword");

                health.ReceiveDamage(hit);
                health.ReceiveDamage(hit);
                Assert.That(health.IsAlive, Is.True);
                Assert.That(health.Current, Is.EqualTo(20f).Within(0.001f));

                health.ReceiveDamage(hit);
                Assert.That(health.IsAlive, Is.False);
                Assert.That(health.Current, Is.Zero.Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
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
                MeleeWeapon.DefaultSwordDamage,
                Is.EqualTo(40f).Within(0.001f));
            Assert.That(
                MeleeWeapon.DefaultSwordDamage *
                ShortSwordAttackPresenter.CalculateHeavyDamageMultiplier(
                    minimumCharge,
                    ShortSwordAttackPresenter.
                        HeavyMinimumDamageMultiplier,
                    ShortSwordAttackPresenter.
                        HeavyMaximumDamageMultiplier),
                Is.EqualTo(40f).Within(0.001f));
            Assert.That(
                MeleeWeapon.DefaultSwordDamage *
                ShortSwordAttackPresenter.CalculateHeavyDamageMultiplier(
                    fullCharge,
                    ShortSwordAttackPresenter.
                        HeavyMinimumDamageMultiplier,
                    ShortSwordAttackPresenter.
                        HeavyMaximumDamageMultiplier),
                Is.EqualTo(60f).Within(0.001f));
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
        public void HeldHeavyAttackCanBeginWhenSwordFinishesEquipping()
        {
            Assert.That(
                ShortSwordAttackPresenter.
                    ShouldBeginHeldHeavyChargeOnEquip(
                        false,
                        true,
                        true),
                Is.True,
                "A click held through the draw animation should begin charging when the sword becomes ready.");
            Assert.That(
                ShortSwordAttackPresenter.
                    ShouldBeginHeldHeavyChargeOnEquip(
                        false,
                        true,
                        false),
                Is.False,
                "Releasing before the sword becomes ready must cancel the pending heavy charge.");
            Assert.That(
                ShortSwordAttackPresenter.
                    ShouldBeginHeldHeavyChargeOnEquip(
                        true,
                        true,
                        true),
                Is.False,
                "An already-equipped sword must continue to use the normal input edge instead of retriggering every frame.");
            Assert.That(
                ShortSwordAttackPresenter.
                    ShouldBeginHeldHeavyChargeOnEquip(
                        false,
                        false,
                        true),
                Is.False,
                "Holding attack while the sword remains unavailable must not start a charge early.");
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
