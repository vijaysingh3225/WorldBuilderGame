using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldBuilder.Gameplay.WeaponGrid
{
    public enum ArtifactStat
    {
        Damage = 0,
        MaxHealth = 1,
        MoveSpeed = 2
    }

    [Serializable]
    public struct ArtifactStatModifier
    {
        [SerializeField] private ArtifactStat stat;
        [SerializeField] private float amount;

        public ArtifactStatModifier(ArtifactStat stat, float amount)
        {
            this.stat = stat;
            this.amount = amount;
        }

        public ArtifactStat Stat => stat;
        public float Amount => amount;
    }

    /// <summary>
    /// Serializable definition data. Artifact instances refer to this by DefinitionId,
    /// keeping profile/save data independent from Unity object references.
    /// </summary>
    [Serializable]
    public sealed class ArtifactDefinitionData
    {
        [SerializeField] private string definitionId;
        [SerializeField] private string displayName;
        [SerializeField] private Color displayColor = new Color(0.82f, 0.56f, 0.2f);
        [SerializeField] private List<GridCoordinate> shape = new List<GridCoordinate>
        {
            GridCoordinate.Root
        };
        [SerializeField] private List<ArtifactStatModifier> modifiers =
            new List<ArtifactStatModifier>();

        public ArtifactDefinitionData()
        {
        }

        public ArtifactDefinitionData(
            string definitionId,
            string displayName,
            Color displayColor,
            IEnumerable<GridCoordinate> shape,
            IEnumerable<ArtifactStatModifier> modifiers)
        {
            this.definitionId = definitionId;
            this.displayName = displayName;
            this.displayColor = displayColor;
            this.shape = shape != null
                ? new List<GridCoordinate>(shape)
                : new List<GridCoordinate>();
            this.modifiers = modifiers != null
                ? new List<ArtifactStatModifier>(modifiers)
                : new List<ArtifactStatModifier>();
            EnsureValid();
        }

        public string DefinitionId => definitionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? definitionId
            : displayName;
        public Color DisplayColor => displayColor;
        public IReadOnlyList<GridCoordinate> Shape => shape;
        public IReadOnlyList<ArtifactStatModifier> Modifiers => modifiers;

        public void EnsureValid()
        {
            definitionId = string.IsNullOrWhiteSpace(definitionId)
                ? Guid.NewGuid().ToString("N")
                : definitionId.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName)
                ? definitionId
                : displayName.Trim();
            shape ??= new List<GridCoordinate>();
            modifiers ??= new List<ArtifactStatModifier>();
            if (shape.Count == 0)
            {
                shape.Add(GridCoordinate.Root);
            }

            var unique = new HashSet<GridCoordinate>();
            for (int index = shape.Count - 1; index >= 0; index--)
            {
                if (!unique.Add(shape[index]))
                {
                    shape.RemoveAt(index);
                }
            }
        }

        public IEnumerable<GridCoordinate> GetRotatedShape(int quarterTurns)
        {
            int rotation = GridCoordinate.NormalizeRotation(quarterTurns);
            for (int index = 0; index < shape.Count; index++)
            {
                yield return shape[index].RotateClockwise(rotation);
            }
        }
    }

    [CreateAssetMenu(
        fileName = "ArtifactDefinition",
        menuName = "World Builder/Weapon Grid/Artifact Definition")]
    public sealed class ArtifactDefinitionAsset : ScriptableObject
    {
        [SerializeField] private ArtifactDefinitionData definition =
            new ArtifactDefinitionData();

        public ArtifactDefinitionData Definition => definition;

#if UNITY_EDITOR
        private void OnValidate()
        {
            definition ??= new ArtifactDefinitionData();
            definition.EnsureValid();
        }
#endif
    }

    [Serializable]
    public sealed class ArtifactInstance
    {
        [SerializeField] private string instanceId;
        [SerializeField] private string definitionId;

        public ArtifactInstance()
        {
        }

        public ArtifactInstance(string instanceId, string definitionId)
        {
            this.instanceId = instanceId;
            this.definitionId = definitionId;
            EnsureValid();
        }

        public string InstanceId => instanceId;
        public string DefinitionId => definitionId;

        public static ArtifactInstance Create(string definitionId)
        {
            return new ArtifactInstance(Guid.NewGuid().ToString("N"), definitionId);
        }

        public void EnsureValid()
        {
            instanceId = string.IsNullOrWhiteSpace(instanceId)
                ? Guid.NewGuid().ToString("N")
                : instanceId.Trim();
            definitionId = definitionId?.Trim() ?? string.Empty;
        }
    }

    [Serializable]
    public sealed class ArtifactPlacement
    {
        [SerializeField] private ArtifactInstance artifact;
        [SerializeField] private GridCoordinate anchor;
        [SerializeField, Range(0, 3)] private int rotation;

        public ArtifactPlacement()
        {
        }

        public ArtifactPlacement(
            ArtifactInstance artifact,
            GridCoordinate anchor,
            int rotation)
        {
            this.artifact = artifact;
            this.anchor = anchor;
            this.rotation = GridCoordinate.NormalizeRotation(rotation);
        }

        public ArtifactInstance Artifact => artifact;
        public GridCoordinate Anchor => anchor;
        public int Rotation => GridCoordinate.NormalizeRotation(rotation);

        public IEnumerable<GridCoordinate> OccupiedCells(
            ArtifactDefinitionData definition)
        {
            if (definition == null)
            {
                yield break;
            }

            foreach (GridCoordinate offset in definition.GetRotatedShape(Rotation))
            {
                yield return anchor + offset;
            }
        }
    }

    [Serializable]
    public struct WeaponGridModifiers
    {
        [SerializeField] private float damage;
        [SerializeField] private float maxHealth;
        [SerializeField] private float moveSpeed;

        public float Damage => damage;
        public float MaxHealth => maxHealth;
        public float MoveSpeed => moveSpeed;

        public void Add(ArtifactStat stat, float amount)
        {
            switch (stat)
            {
                case ArtifactStat.Damage:
                    damage += amount;
                    break;
                case ArtifactStat.MaxHealth:
                    maxHealth += amount;
                    break;
                case ArtifactStat.MoveSpeed:
                    moveSpeed += amount;
                    break;
            }
        }

        public void Add(WeaponGridModifiers other)
        {
            damage += other.damage;
            maxHealth += other.maxHealth;
            moveSpeed += other.moveSpeed;
        }

        public static WeaponGridModifiers Create(
            float damage,
            float maxHealth,
            float moveSpeed)
        {
            return new WeaponGridModifiers
            {
                damage = damage,
                maxHealth = maxHealth,
                moveSpeed = moveSpeed
            };
        }
    }

    public readonly struct WeaponGridModifierSummary
    {
        public WeaponGridModifierSummary(
            int activeWeaponIndex,
            WeaponGridModifiers primary,
            WeaponGridModifiers secondary,
            WeaponGridModifiers effective)
        {
            ActiveWeaponIndex = activeWeaponIndex;
            Primary = primary;
            Secondary = secondary;
            Effective = effective;
        }

        public int ActiveWeaponIndex { get; }
        public WeaponGridModifiers Primary { get; }
        public WeaponGridModifiers Secondary { get; }
        public WeaponGridModifiers Effective { get; }
    }
}
