using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Tests
{
    public sealed class ArrowImpactFeedbackTests
    {
        [Test]
        public void BowLoadsPlayerFeedbackAndUsesTimedPullback()
        {
            GameObject bowObject =
                new GameObject("Feedback Test Bow");
            try
            {
                BowWeapon bow =
                    bowObject.AddComponent<BowWeapon>();
                AudioClip pullback =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(
                        "Assets/_Project/Audio/SFX/" +
                        "Bow Pullback.wav");
                AudioClip impact =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(
                        "Assets/_Project/Audio/SFX/" +
                        "Arrow Impact.wav");
                AudioClip enemyHit =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(
                        "Assets/_Project/Audio/SFX/" +
                        "ArrowHit.mp3");
                AudioClip headshot =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(
                        "Assets/_Project/Audio/SFX/" +
                        "HeadShot.mp3");
                bow.Configure(
                    null,
                    bowObject.transform,
                    bowObject.transform,
                    bowObject.transform,
                    pullback,
                    impact,
                    enemyHit,
                    headshot);

                Assert.That(bow.AudioConfigured, Is.True);
                Assert.That(
                    bow.EnemyHitFeedbackClip.name,
                    Is.EqualTo("ArrowHit"));
                Assert.That(
                    bow.HeadshotFeedbackClip.name,
                    Is.EqualTo("HeadShot"));
                AudioImporter hitImporter =
                    AssetImporter.GetAtPath(
                        "Assets/_Project/Audio/SFX/" +
                        "ArrowHit.mp3") as AudioImporter;
                AudioImporter headshotImporter =
                    AssetImporter.GetAtPath(
                        "Assets/_Project/Audio/SFX/" +
                        "HeadShot.mp3") as AudioImporter;
                Assert.That(hitImporter, Is.Not.Null);
                Assert.That(headshotImporter, Is.Not.Null);
                Assert.That(
                    hitImporter.defaultSampleSettings.
                        preloadAudioData,
                    Is.True);
                Assert.That(
                    headshotImporter.defaultSampleSettings.
                        preloadAudioData,
                    Is.True);
                Assert.That(
                    bow.EnemyHitFeedbackClip.loadState,
                    Is.Not.EqualTo(
                        AudioDataLoadState.Unloaded));
                Assert.That(
                    bow.HeadshotFeedbackClip.loadState,
                    Is.Not.EqualTo(
                        AudioDataLoadState.Unloaded));
                AssertAudibleSamples(
                    bow.EnemyHitFeedbackClip);
                AssertAudibleSamples(
                    bow.HeadshotFeedbackClip);
                Assert.That(
                    bow.PullbackPitch,
                    Is.InRange(0.58f, 0.68f));
                Assert.That(
                    pullback.length / bow.PullbackPitch,
                    Is.GreaterThan(0.72f),
                    "The rising pullback sound should remain present through most of the visible draw.");
            }
            finally
            {
                Object.DestroyImmediate(bowObject);
            }
        }

        private static void AssertAudibleSamples(
            AudioClip clip)
        {
            int sampleCount = Mathf.Min(
                clip.samples * clip.channels,
                clip.frequency * clip.channels * 3);
            float[] samples = new float[sampleCount];
            Assert.That(
                clip.GetData(samples, 0),
                Is.True,
                $"{clip.name} sample data must be readable.");
            float peak = 0f;
            for (int index = 0;
                 index < samples.Length;
                 index++)
            {
                peak = Mathf.Max(
                    peak,
                    Mathf.Abs(samples[index]));
            }
            Assert.That(
                peak,
                Is.GreaterThan(0.01f),
                $"{clip.name} must contain audible waveform data.");
        }

        [Test]
        public void PlayerHitFeedbackAudioIsNonSpatial()
        {
            GameObject arrowObject =
                new GameObject("Feedback Test Arrow");
            try
            {
                BowArrowProjectile arrow =
                    arrowObject.AddComponent<
                        BowArrowProjectile>();
                AudioClip enemyHit =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(
                        "Assets/_Project/Audio/SFX/" +
                        "ArrowHit.mp3");
                AudioClip headshot =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(
                        "Assets/_Project/Audio/SFX/" +
                        "HeadShot.mp3");
                AudioSource persistentFeedback =
                    arrowObject.AddComponent<AudioSource>();
                arrow.Launch(
                    null,
                    Vector3.forward,
                    10f,
                    null,
                    enemyHit,
                    headshot,
                    persistentFeedback);
                MethodInfo playFeedback =
                    typeof(BowArrowProjectile).GetMethod(
                        "PlayPlayerHitFeedback",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);

                Assert.That(playFeedback, Is.Not.Null);
                playFeedback.Invoke(
                    arrow,
                    new object[] { true });

                AudioSource feedbackSource =
                    arrowObject.GetComponent<AudioSource>();
                Assert.That(feedbackSource, Is.Not.Null);
                Assert.That(feedbackSource.spatialBlend, Is.Zero);
                Assert.That(
                    feedbackSource,
                    Is.SameAs(persistentFeedback));
                Assert.That(
                    arrow.EnemyHitFeedbackClip,
                    Is.SameAs(enemyHit));
                Assert.That(
                    arrow.HeadshotFeedbackClip,
                    Is.SameAs(headshot));
            }
            finally
            {
                Object.DestroyImmediate(arrowObject);
            }
        }

        [Test]
        public void PlayerAndEnemyPullbacksUseDifferentSpatialMixes()
        {
            GameObject player = new GameObject("Player");
            GameObject enemy = new GameObject("Enemy");
            GameObject playerBowObject =
                new GameObject("Player Bow");
            GameObject enemyBowObject =
                new GameObject("Enemy Bow");
            GameObject playerArrow =
                new GameObject("Player Arrow");
            GameObject enemyArrow =
                new GameObject("Enemy Arrow");
            try
            {
                player.tag = "Player";
                playerBowObject.transform.SetParent(
                    player.transform,
                    false);
                playerArrow.transform.SetParent(
                    player.transform,
                    false);
                enemyBowObject.transform.SetParent(
                    enemy.transform,
                    false);
                enemyArrow.transform.SetParent(
                    enemy.transform,
                    false);
                BowWeapon playerBow =
                    playerBowObject.AddComponent<BowWeapon>();
                BowWeapon enemyBow =
                    enemyBowObject.AddComponent<BowWeapon>();
                PlayerInputSource playerInput =
                    player.AddComponent<PlayerInputSource>();
                PlayerInputSource enemyInput =
                    enemy.AddComponent<PlayerInputSource>();
                AudioClip pullback =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(
                        "Assets/_Project/Audio/SFX/" +
                        "Bow Pullback.wav");
                playerBow.Configure(
                    playerInput,
                    player.transform,
                    playerBowObject.transform,
                    playerArrow.transform,
                    pullback);
                enemyBow.Configure(
                    enemyInput,
                    enemy.transform,
                    enemyBowObject.transform,
                    enemyArrow.transform,
                    pullback);

                Assert.That(
                    playerBow.PullbackVolume,
                    Is.EqualTo(0.09f).Within(0.001f));
                Assert.That(
                    playerBow.PullbackSpatialBlend,
                    Is.Zero);
                Assert.That(
                    playerBow.HitFeedbackSpatialBlend,
                    Is.Zero);
                Assert.That(
                    playerBow.HitFeedbackAudioHost,
                    Is.SameAs(player),
                    "Hit feedback must live on the player root, not on the distant target or fired arrow.");
                Assert.That(
                    playerBow.MaximumDamage,
                    Is.EqualTo(100f));
                Assert.That(
                    playerBow.MinimumDamage,
                    Is.EqualTo(12f));
                Assert.That(
                    enemyBow.PullbackVolume,
                    Is.EqualTo(0.14f).Within(0.001f));
                Assert.That(
                    enemyBow.IsPlayerOwned,
                    Is.False,
                    "An AI intent component must not opt the enemy into player crosshair aiming.");
                Assert.That(
                    enemyBow.PullbackSpatialBlend,
                    Is.EqualTo(1f));
                Assert.That(
                    enemyBow.PullbackMaxDistance,
                    Is.EqualTo(20f).Within(0.001f));
                Assert.That(
                    enemyBow.MaximumDamage,
                    Is.EqualTo(100f),
                    "A fully drawn Raid arrow should retain its one-shot threat while using NPC aiming rules.");
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void DamageZoneResolvesTheMovingHitBone()
        {
            GameObject enemy = new GameObject("Enemy");
            GameObject upperLeg = new GameObject("Upper Leg");
            GameObject lowerLeg = new GameObject("Lower Leg");
            GameObject hitbox = new GameObject("Thigh Hitbox");
            try
            {
                enemy.AddComponent<Health>();
                enemy.AddComponent<EnemyDamageProfile>();
                upperLeg.transform.SetParent(
                    enemy.transform,
                    false);
                lowerLeg.transform.SetParent(
                    enemy.transform,
                    false);
                lowerLeg.transform.localPosition =
                    Vector3.down;
                hitbox.transform.SetParent(
                    enemy.transform,
                    false);
                HumanoidDamageZone zone =
                    hitbox.AddComponent<
                        HumanoidDamageZone>();
                zone.Configure(
                    HumanoidHitRegion.Limb,
                    upperLeg.transform,
                    lowerLeg.transform);

                Transform attachment =
                    zone.ResolveAttachmentTransform(
                        lowerLeg.transform.position +
                        Vector3.up * 0.05f);

                Assert.That(
                    attachment,
                    Is.SameAs(upperLeg.transform),
                    "A thigh impact must follow the upper-leg body segment through ragdoll motion.");
            }
            finally
            {
                Object.DestroyImmediate(enemy);
            }
        }

        [TestCase(HumanoidHitRegion.Torso, false)]
        [TestCase(HumanoidHitRegion.Head, true)]
        public void PlayerEnemyImpactReachesConfirmationAudio(
            HumanoidHitRegion region,
            bool expectedHeadshot)
        {
            GameObject player = new GameObject("Player");
            GameObject enemy = new GameObject("Enemy");
            GameObject hitbox = new GameObject("Hitbox");
            GameObject arrowObject = new GameObject("Arrow");
            try
            {
                player.tag = "Player";
                Health health = enemy.AddComponent<Health>();
                EnemyDamageProfile profile =
                    enemy.AddComponent<EnemyDamageProfile>();
                profile.Configure(
                    EnemyCombatVariant.CombatLabDummy);
                hitbox.transform.SetParent(
                    enemy.transform,
                    false);
                BoxCollider collider =
                    hitbox.AddComponent<BoxCollider>();
                HumanoidDamageZone zone =
                    hitbox.AddComponent<HumanoidDamageZone>();
                zone.Configure(
                    region,
                    hitbox.transform);
                AudioClip enemyHit =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(
                        "Assets/_Project/Audio/SFX/" +
                        "ArrowHit.mp3");
                AudioClip headshot =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(
                        "Assets/_Project/Audio/SFX/" +
                        "HeadShot.mp3");
                GameObject bowObject =
                    new GameObject("Player Bow");
                bowObject.transform.SetParent(
                    player.transform,
                    false);
                GameObject nockedArrow =
                    new GameObject("Nocked Arrow");
                nockedArrow.transform.SetParent(
                    bowObject.transform,
                    false);
                BowWeapon bow =
                    bowObject.AddComponent<BowWeapon>();
                bow.Configure(
                    null,
                    player.transform,
                    bowObject.transform,
                    nockedArrow.transform,
                    null,
                    null,
                    enemyHit,
                    headshot);
                BowArrowProjectile arrow =
                    arrowObject.AddComponent<BowArrowProjectile>();
                arrow.Launch(
                    player,
                    Vector3.forward * 20f,
                    10f,
                    null,
                    enemyHit,
                    headshot,
                    null,
                    true);
                MethodInfo resolveImpact =
                    typeof(BowArrowProjectile).GetMethod(
                        "ResolveImpact",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);

                Assert.That(resolveImpact, Is.Not.Null);
                resolveImpact.Invoke(
                    arrow,
                    new object[]
                    {
                        collider,
                        hitbox.transform.position
                    });

                Assert.That(
                    arrow.LastImpactDamagedEnemy,
                    Is.True);
                Assert.That(
                    arrow.LastImpactWasHeadshot,
                    Is.EqualTo(expectedHeadshot));
                Assert.That(
                    health.Current,
                    Is.LessThan(health.Maximum));
                Assert.That(
                    bow.HitFeedbackSpatialBlend,
                    Is.Zero);
                PlayerHitFeedbackEmitter emitter =
                    player.GetComponent<
                        PlayerHitFeedbackEmitter>();
                Assert.That(emitter, Is.Not.Null);
                Assert.That(
                    emitter.SpatialBlend,
                    Is.Zero);
                Assert.That(
                    emitter.PlaybackCount,
                    Is.EqualTo(1),
                    "Accepted player bow damage must immediately play feedback through the player-root source.");
                Assert.That(
                    PlayerHitFeedbackEmitter.
                        FeedbackVolume,
                    Is.EqualTo(0.12f).Within(0.001f));
                Assert.That(
                    emitter.LastSourceClip,
                    Is.SameAs(
                        expectedHeadshot
                            ? headshot
                            : enemyHit),
                    "A hit must play exactly one region-appropriate confirmation clip.");
            }
            finally
            {
                Object.DestroyImmediate(arrowObject);
                Object.DestroyImmediate(enemy);
                Object.DestroyImmediate(player);
            }
        }
    }
}
