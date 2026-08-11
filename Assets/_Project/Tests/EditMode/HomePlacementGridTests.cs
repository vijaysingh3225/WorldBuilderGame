using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WorldBuilder.Gameplay.Loop.Scenes;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class HomePlacementGridTests
    {
        private GameObject root;
        private HomePlacementGrid grid;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Placement Grid Test Root");
            grid = root.AddComponent<HomePlacementGrid>();
            grid.Configure(2.5f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void CellCoordinatesUseVolumeCentersInsteadOfGridCorners()
        {
            Assert.That(
                grid.CellToWorldCenter(new Vector3Int(-4, 0, 3)),
                Is.EqualTo(new Vector3(-8.75f, 1.25f, 8.75f)));
            Assert.That(
                grid.GetFootprintBaseCenter(
                    new Vector3Int(-4, 0, 3),
                    Vector3Int.one),
                Is.EqualTo(new Vector3(-8.75f, 0f, 8.75f)));
            Assert.That(
                grid.WorldToCell(new Vector3(-8.75f, 1.25f, 8.75f)),
                Is.EqualTo(new Vector3Int(-4, 0, 3)));
        }

        [Test]
        public void OccupantsReserveThreeDimensionalCellsAndRejectOverlap()
        {
            HomeGridOccupant lower = CreateOccupant("Lower");
            HomeGridOccupant upper = CreateOccupant("Upper");
            HomeGridOccupant overlap = CreateOccupant("Overlap");

            Assert.That(
                lower.Configure(
                    grid,
                    Vector3Int.zero,
                    Vector3Int.one),
                Is.True);
            Assert.That(
                upper.Configure(
                    grid,
                    new Vector3Int(0, 1, 0),
                    Vector3Int.one),
                Is.True);
            Assert.That(
                overlap.Configure(
                    grid,
                    Vector3Int.zero,
                    Vector3Int.one),
                Is.False);
            Assert.That(grid.GetOccupant(Vector3Int.zero), Is.SameAs(lower));
            Assert.That(
                grid.GetOccupant(new Vector3Int(0, 1, 0)),
                Is.SameAs(upper));
            Assert.That(upper.transform.position.y, Is.EqualTo(2.5f));
        }

        [Test]
        public void RotationSwapsHorizontalFootprintAndMovementReleasesCells()
        {
            HomeGridOccupant occupant = CreateOccupant("Structure");
            Assert.That(
                occupant.Configure(
                    grid,
                    Vector3Int.zero,
                    new Vector3Int(2, 1, 1),
                    1),
                Is.True);
            Assert.That(
                occupant.OccupiedCells().ToArray(),
                Is.EquivalentTo(new[]
                {
                    Vector3Int.zero,
                    new Vector3Int(0, 0, 1)
                }));

            Assert.That(
                occupant.TryPlace(new Vector3Int(3, 0, 2)),
                Is.True);
            Assert.That(grid.GetOccupant(Vector3Int.zero), Is.Null);
            Assert.That(
                grid.GetOccupant(new Vector3Int(3, 0, 2)),
                Is.SameAs(occupant));
        }

        [Test]
        public void LegacyTwoDimensionalCoordinatesMigrateIntoGroundPlane()
        {
            HomeGridOccupant occupant = CreateOccupant("Legacy Occupant");
            var serialized = new UnityEditor.SerializedObject(occupant);
            serialized.FindProperty("cell").vector3IntValue =
                new Vector3Int(-3, 3, 0);
            serialized.FindProperty("footprint").vector3IntValue =
                new Vector3Int(2, 4, 0);
            serialized.FindProperty("serializationVersion").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            occupant.OnAfterDeserialize();

            Assert.That(
                occupant.Cell,
                Is.EqualTo(new Vector3Int(-3, 0, 3)));
            Assert.That(
                occupant.Footprint,
                Is.EqualTo(new Vector3Int(2, 1, 4)));
        }

        private HomeGridOccupant CreateOccupant(string objectName)
        {
            var gameObject = new GameObject(objectName);
            gameObject.transform.SetParent(root.transform, false);
            return gameObject.AddComponent<HomeGridOccupant>();
        }
    }
}
