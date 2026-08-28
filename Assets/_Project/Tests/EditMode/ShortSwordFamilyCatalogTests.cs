using System.Linq;
using NUnit.Framework;
using WorldBuilder.Gameplay.Weapons;

namespace WorldBuilder.Tests.EditMode
{
    [Category("ShortSwordCatalog")]
    public sealed class ShortSwordFamilyCatalogTests
    {
        private static readonly ShortSwordFamily[] ActiveFamilies =
        {
            ShortSwordFamily.Cruciform,
            ShortSwordFamily.Leafblade,
            ShortSwordFamily.Legionary,
            ShortSwordFamily.Piercer
        };

        private static readonly ShortSwordFamily[] RetiredFamilies =
        {
            ShortSwordFamily.Seax,
            ShortSwordFamily.Falchion,
            ShortSwordFamily.Kopis,
            ShortSwordFamily.Hanger
        };

        private static readonly ShortSwordBladeProfile[] RetiredProfiles =
        {
            ShortSwordBladeProfile.Seax,
            ShortSwordBladeProfile.Falchion,
            ShortSwordBladeProfile.Kopis,
            ShortSwordBladeProfile.Hanger
        };

        [Test]
        public void CatalogExposesOnlyTheFourRemainingFamilies()
        {
            Assert.That(
                ShortSwordGenerationBranchCatalog.Families,
                Is.EqualTo(ActiveFamilies));
            Assert.That(
                ShortSwordGenerationBranchCatalog.TryGetGroup(
                    ShortSwordGenerationDecision.Family,
                    out ShortSwordGenerationBranchGroup familyGroup),
                Is.True);
            Assert.That(
                familyGroup.Options.Select(option => option.Value),
                Is.EqualTo(ActiveFamilies.Select(family => (int)family)));
            Assert.That(
                ShortSwordGenerationBranchCatalog.TryGetGroup(
                    ShortSwordGenerationDecision.BladeProfile,
                    out ShortSwordGenerationBranchGroup bladeGroup),
                Is.True);
            Assert.That(
                bladeGroup.Options
                    .Select(option => option.Value)
                    .Intersect(RetiredProfiles.Select(profile => (int)profile)),
                Is.Empty);
        }

        [Test]
        public void RetiredFamiliesAndProfilesCannotBeLocked()
        {
            var constraints =
                new ProceduralShortSwordGenerationConstraints();
            foreach (ShortSwordFamily family in RetiredFamilies)
            {
                Assert.That(
                    constraints.Toggle(
                        ShortSwordGenerationDecision.Family,
                        (int)family),
                    Is.False,
                    family.ToString());
                Assert.That(
                    ShortSwordGenerationBranchCatalog.IsActiveFamily(family),
                    Is.False,
                    family.ToString());
            }
            foreach (ShortSwordBladeProfile profile in RetiredProfiles)
            {
                Assert.That(
                    constraints.Toggle(
                        ShortSwordGenerationDecision.BladeProfile,
                        (int)profile),
                    Is.False,
                    profile.ToString());
            }
            Assert.That(constraints.ActiveLockCount, Is.Zero);
        }

        [Test]
        public void UnrestrictedGenerationNeverEmitsRetiredSwordTypes()
        {
            for (int seed = 0; seed < 2048; seed++)
            {
                ProceduralShortSwordDefinition sword =
                    ProceduralShortSwordGenerator.CreateDefinition(seed);
                Assert.That(
                    ActiveFamilies.Contains(sword.Family),
                    Is.True,
                    $"Seed {seed}");
                Assert.That(
                    RetiredProfiles.Contains(sword.BladeProfile),
                    Is.False,
                    $"Seed {seed}");
            }
        }
    }
}
