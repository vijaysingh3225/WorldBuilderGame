using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WorldBuilder.Gameplay.Combat;

namespace WorldBuilder.Gameplay.Presentation
{
    /// <summary>
    /// Draws only the surface swept by a blade during its active cut window.
    /// Each ribbon segment is bounded by two successive base-to-tip blade
    /// positions, so it starts flush with the real blade and never projects in
    /// front of its direction of travel.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShortSwordSwingTrail : MonoBehaviour
    {
        private const float TrailLifetime = 0.065f;
        private const float MaximumOpacity = 0.28f;
        private const float MinimumMovementSqr = 0.000004f;

        private struct BladeSample
        {
            public BladeSample(Vector3 bladeBase, Vector3 bladeTip, float time)
            {
                Base = bladeBase;
                Tip = bladeTip;
                Time = time;
            }

            public Vector3 Base { get; }
            public Vector3 Tip { get; }
            public float Time { get; }
        }

        private static Material sharedTrailMaterial;

        [SerializeField] private MeleeWeapon weapon;
        [SerializeField] private MeshFilter sweepMeshFilter;
        [SerializeField] private MeshRenderer sweepRenderer;

        private readonly List<BladeSample> samples = new List<BladeSample>();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();
        private readonly List<Color> colors = new List<Color>();

        private Mesh sweepMesh;
        private bool emitting;

        public Mesh SweepMesh => sweepMesh;

        public void Configure(MeleeWeapon meleeWeapon)
        {
            weapon = meleeWeapon;
            EnsureTrailMesh();
        }

        public void BeginSlice()
        {
            if (weapon == null)
            {
                return;
            }

            EnsureTrailMesh();
            samples.Clear();
            CaptureBladeSample();
            sweepMesh.Clear();
            sweepRenderer.enabled = true;
            emitting = true;
        }

        public void EndSwing()
        {
            emitting = false;
            samples.Clear();
            if (sweepMesh != null)
            {
                sweepMesh.Clear();
            }
            if (sweepRenderer != null)
            {
                sweepRenderer.enabled = false;
            }
        }

        private void Awake()
        {
            weapon ??= GetComponentInParent<MeleeWeapon>();
            EnsureTrailMesh();
        }

        private void LateUpdate()
        {
            if (!emitting || weapon == null || sweepMesh == null)
            {
                return;
            }

            CaptureBladeSample();
            RemoveExpiredSamples();
            RebuildSweepMesh();
        }

        private void OnDisable()
        {
            EndSwing();
        }

        private void OnDestroy()
        {
            if (sweepMesh != null)
            {
                Destroy(sweepMesh);
            }
        }

        private void EnsureTrailMesh()
        {
            if (sweepMeshFilter != null && sweepRenderer != null)
            {
                return;
            }

            GameObject trailObject = new GameObject("Sword Blade Sweep Trail");
            trailObject.layer = gameObject.layer;
            trailObject.transform.SetParent(transform, false);
            sweepMeshFilter = trailObject.AddComponent<MeshFilter>();
            sweepRenderer = trailObject.AddComponent<MeshRenderer>();
            sweepRenderer.sharedMaterial = GetTrailMaterial();
            sweepRenderer.shadowCastingMode = ShadowCastingMode.Off;
            sweepRenderer.receiveShadows = false;
            sweepRenderer.enabled = false;
            sweepMesh = new Mesh
            {
                name = "Runtime Sword Blade Sweep Mesh",
                hideFlags = HideFlags.HideAndDontSave
            };
            sweepMesh.MarkDynamic();
            sweepMeshFilter.sharedMesh = sweepMesh;
        }

        private void CaptureBladeSample()
        {
            weapon.GetBladeSegment(
                out Vector3 bladeBase,
                out Vector3 bladeTip);
            BladeSample sample = new BladeSample(
                bladeBase,
                bladeTip,
                Time.time);
            if (samples.Count > 0)
            {
                BladeSample previous = samples[samples.Count - 1];
                Vector3 previousCenter = (previous.Base + previous.Tip) * 0.5f;
                Vector3 currentCenter = (bladeBase + bladeTip) * 0.5f;
                if ((currentCenter - previousCenter).sqrMagnitude <
                    MinimumMovementSqr)
                {
                    return;
                }
            }
            samples.Add(sample);
        }

        private void RemoveExpiredSamples()
        {
            float minimumTime = Time.time - TrailLifetime;
            while (samples.Count > 2 && samples[0].Time < minimumTime)
            {
                samples.RemoveAt(0);
            }
        }

        private void RebuildSweepMesh()
        {
            sweepMesh.Clear();
            if (samples.Count < 2)
            {
                return;
            }

            vertices.Clear();
            triangles.Clear();
            colors.Clear();
            float oldestTime = samples[0].Time;
            float newestTime = samples[samples.Count - 1].Time;
            float duration = Mathf.Max(0.0001f, newestTime - oldestTime);
            for (int index = 0; index < samples.Count - 1; index++)
            {
                BladeSample previous = samples[index];
                BladeSample current = samples[index + 1];
                int vertexIndex = vertices.Count;
                vertices.Add(transform.InverseTransformPoint(previous.Base));
                vertices.Add(transform.InverseTransformPoint(previous.Tip));
                vertices.Add(transform.InverseTransformPoint(current.Tip));
                vertices.Add(transform.InverseTransformPoint(current.Base));
                triangles.Add(vertexIndex);
                triangles.Add(vertexIndex + 1);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex + 3);
                AddSampleColor(previous.Time, oldestTime, duration);
                AddSampleColor(previous.Time, oldestTime, duration);
                AddSampleColor(current.Time, oldestTime, duration);
                AddSampleColor(current.Time, oldestTime, duration);
            }

            sweepMesh.SetVertices(vertices);
            sweepMesh.SetTriangles(triangles, 0, true);
            sweepMesh.SetColors(colors);
            sweepMesh.RecalculateBounds();
        }

        private void AddSampleColor(
            float sampleTime,
            float oldestTime,
            float duration)
        {
            float ageWeight = Mathf.InverseLerp(
                oldestTime,
                oldestTime + duration,
                sampleTime);
            colors.Add(new Color(
                0.72f,
                0.86f,
                1f,
                MaximumOpacity * ageWeight));
        }

        private static Material GetTrailMaterial()
        {
            if (sharedTrailMaterial != null)
            {
                return sharedTrailMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default") ??
                Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                return null;
            }

            sharedTrailMaterial = new Material(shader)
            {
                name = "Runtime Sword Blade Sweep Trail",
                hideFlags = HideFlags.HideAndDontSave
            };
            return sharedTrailMaterial;
        }
    }
}
