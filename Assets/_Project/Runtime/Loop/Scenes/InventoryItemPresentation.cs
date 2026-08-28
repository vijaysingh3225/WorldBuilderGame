using UnityEngine;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    public static class InventoryItemPresentation
    {
        public const float IconInset = 3f;

        public static bool DrawSingleCellIcon(
            Rect cellRect,
            string definitionId)
        {
            Texture2D icon = ItemDefinitionCatalog.LoadIcon(definitionId);
            if (icon == null)
            {
                return false;
            }

            GUI.DrawTexture(
                new Rect(
                    cellRect.x + IconInset,
                    cellRect.y + IconInset,
                    Mathf.Max(0f, cellRect.width - IconInset * 2f),
                    Mathf.Max(0f, cellRect.height - IconInset * 2f)),
                icon,
                ScaleMode.ScaleToFit,
                true);
            return true;
        }

        public static Rect CalculateSingleCellCursorRect(
            Vector2 mousePosition,
            float cellSize)
        {
            float size = Mathf.Max(1f, cellSize);
            return new Rect(
                mousePosition.x - size * 0.5f,
                mousePosition.y - size * 0.5f,
                size,
                size);
        }
    }
}
