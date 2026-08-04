using NUnit.Framework;
using UnityEngine;
using WorldBuilder.Gameplay.Loop.Scenes;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class InventoryPreviewRendererTests
    {
        [Test]
        public void CharacterPreview_StartsAtFrontThreeQuarterView()
        {
            GameObject previewObject = new GameObject("inventory-preview-test");
            try
            {
                InventoryPreviewRenderer preview =
                    previewObject.AddComponent<InventoryPreviewRenderer>();

                Assert.That(
                    preview.CharacterYaw,
                    Is.EqualTo(InventoryPreviewRenderer.DefaultCharacterYaw));
                Assert.That(preview.CharacterYaw, Is.InRange(0f, 45f));
            }
            finally
            {
                Object.DestroyImmediate(previewObject);
            }
        }

        [Test]
        public void ResetCharacterView_RestoresDefaultAfterDragging()
        {
            GameObject previewObject = new GameObject("inventory-preview-test");
            try
            {
                InventoryPreviewRenderer preview =
                    previewObject.AddComponent<InventoryPreviewRenderer>();
                preview.RotateCharacter(137f);

                preview.ResetCharacterView();

                Assert.That(
                    preview.CharacterYaw,
                    Is.EqualTo(InventoryPreviewRenderer.DefaultCharacterYaw));
            }
            finally
            {
                Object.DestroyImmediate(previewObject);
            }
        }
    }
}
