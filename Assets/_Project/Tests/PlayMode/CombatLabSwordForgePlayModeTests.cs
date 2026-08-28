using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Loop.Scenes;
using WorldBuilder.Gameplay.Presentation;
using WorldBuilder.Gameplay.Weapons;

namespace WorldBuilder.Tests.PlayMode
{
    public sealed class CombatLabSwordForgePlayModeTests
    {
        [UnityTest]
        public IEnumerator CombatLabEntryGeneratesThePlayersFirstSword()
        {
            SceneManager.LoadScene("CombatLab", LoadSceneMode.Single);
            yield return null;
            yield return null;

            CombatLabSwordForge forge =
                Object.FindFirstObjectByType<CombatLabSwordForge>(
                    FindObjectsInactive.Include);
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Assert.That(forge, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(forge.HasGeneratedSword, Is.True);
            Assert.That(forge.GenerationCount, Is.EqualTo(1));

            TwoSlotWeaponPresenter slots =
                player.GetComponentInChildren<TwoSlotWeaponPresenter>(true);
            MeleeWeapon weapon = player.GetComponent<MeleeWeapon>();
            RaidShortSwordPresentation presentation =
                slots.PrimaryWeaponRoot.GetComponent<
                    RaidShortSwordPresentation>();
            Assert.That(presentation, Is.Not.Null);
            Assert.That(presentation.Seed, Is.EqualTo(forge.CurrentSeed));
            Assert.That(
                JsonUtility.ToJson(weapon.CombatProfile),
                Is.EqualTo(JsonUtility.ToJson(
                    presentation.Generator.CurrentDefinition.
                        CombatProfile)));
        }
    }
}
