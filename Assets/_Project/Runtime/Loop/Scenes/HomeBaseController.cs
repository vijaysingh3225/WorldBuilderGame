using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DisallowMultipleComponent]
    public sealed class HomeBaseController : MonoBehaviour
    {
        [SerializeField] private PlayerInputSource playerInput;

        private GameplayLoopBootstrap bootstrap;
        private GameSession session;
        private string lastError = string.Empty;

        public GameSession Session => session;
        public PlayerProfile Profile =>
            session != null ? session.ActiveProfile : null;
        public string LastError => lastError;

        public void Configure(PlayerInputSource input)
        {
            playerInput = input;
        }

        private void Start()
        {
            HomeBlockGridInteractor interactor =
                GetComponent<HomeBlockGridInteractor>() ??
                gameObject.AddComponent<HomeBlockGridInteractor>();
            interactor.Configure(
                FindFirstObjectByType<HomePlacementGrid>(),
                playerInput != null ? playerInput.transform : null);
            InitializeSession();
        }

        public bool TryLaunchRaid()
        {
            if (session == null)
            {
                InitializeSession();
            }

            if (session == null)
            {
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(
                    GameplaySceneNames.RaidPrototype))
            {
                lastError =
                    $"Scene '{GameplaySceneNames.RaidPrototype}' is not " +
                    "registered in Build Settings.";
                return false;
            }

            try
            {
                if (!session.HasActiveRaid)
                {
                    WeaponGridProfileBinding weaponBinding =
                        FindFirstObjectByType<WeaponGridProfileBinding>();
                    weaponBinding?.SyncNow();
                    session.BeginRaid(
                        carriedStorageEntryIds:
                            session.ActiveProfile.InventoryEntryIds);
                }
            }
            catch (Exception exception)
            {
                lastError =
                    $"Could not begin raid: {exception.Message}";
                return false;
            }

            return GameplaySceneRuntime.TryLoadScene(
                GameplaySceneNames.RaidPrototype,
                out lastError);
        }

        public void SaveProfile()
        {
            if (session == null)
            {
                return;
            }

            session.SaveProfile();
        }

        private void InitializeSession()
        {
            bootstrap = GameplaySceneRuntime.ResolveBootstrap();
            session = bootstrap.Session;
            if (session == null ||
                session.LaunchContext.Mode ==
                    GameLaunchMode.CombatLab)
            {
                if (!bootstrap.StartHomeSandbox("direct-home"))
                {
                    lastError =
                        bootstrap.LastInitializationError;
                    session = null;
                    return;
                }

                session = bootstrap.Session;
            }

            if (session.HasActiveRaid)
            {
                try
                {
                    session.CompleteActiveRaid(
                        RaidCompletionReason.Abandoned,
                        out _);
                }
                catch (InvalidOperationException exception)
                {
                    lastError = exception.Message;
                }
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class HomeBlockGridInteractor : MonoBehaviour
    {
        private const float DefaultBuildReach = 7.5f;
        private const float SurfaceOffset = 0.012f;
        private readonly List<BoxCollider> blockSurfaces =
            new List<BoxCollider>(4);
        private readonly RaycastHit[] focusHits = new RaycastHit[32];

        [SerializeField] private HomePlacementGrid grid;
        [SerializeField] private Transform player;
        [SerializeField, Min(1f)] private float buildReach =
            DefaultBuildReach;

        private Material lineMaterial;
        private Texture2D reticleTexture;
        private HomeInventoryController inventory;
        private HomeAnvil anvil;
        private BoxCollider basePlatform;
        private BoxCollider fallCatch;
        private Vector3 safePlayerPosition;
        private bool hasSelection;
        private Vector3 selectionCenter;
        private Vector3 selectionNormal;
        private Vector3 selectionAxisU;
        private Vector3 selectionAxisV;
        private float selectionWidth;
        private float selectionHeight;

        public bool HasSelection => hasSelection;
        public Vector3 SelectionCenter => selectionCenter;
        public float BuildReach => buildReach;

        public void Configure(
            HomePlacementGrid placementGrid,
            Transform playerTransform)
        {
            grid = placementGrid;
            player = playerTransform;
            SimplifyToPlatformOnly();
            EnsureSolidBasePlatform();
            CacheBlockSurfaces();
            KeepSingleCubeChest();
            EnsureHomeAnvil();
        }

        private void OnEnable()
        {
            if (grid != null)
            {
                EnsureSolidBasePlatform();
                CacheBlockSurfaces();
            }
        }

        private void Awake()
        {
            EnsureDrawingResources();
        }

        private void OnDestroy()
        {
            if (lineMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(lineMaterial);
                }
                else
                {
                    DestroyImmediate(lineMaterial);
                }
            }
            if (reticleTexture != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(reticleTexture);
                }
                else
                {
                    DestroyImmediate(reticleTexture);
                }
            }
        }

        private void Update()
        {
            inventory ??= FindFirstObjectByType<HomeInventoryController>();
            anvil ??= FindFirstObjectByType<HomeAnvil>();
            if (player == null)
            {
                GameObject playerObject =
                    GameObject.FindGameObjectWithTag("Player");
                player = playerObject != null
                    ? playerObject.transform
                    : null;
            }
            if (blockSurfaces.Count == 0)
            {
                CacheBlockSurfaces();
            }

            RecoverPlayerBelowPlatform();

            ResolveSelection();
        }

        private void EnsureSolidBasePlatform()
        {
            Transform[] transforms =
                FindObjectsByType<Transform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Transform floor = null;
            for (int index = 0; index < transforms.Length; index++)
            {
                if (string.Equals(
                        transforms[index].gameObject.name,
                        "Base Floor",
                        StringComparison.Ordinal))
                {
                    floor = transforms[index];
                    break;
                }
            }
            if (floor == null)
            {
                return;
            }

            floor.gameObject.SetActive(true);
            floor.localScale = Vector3.one;
            basePlatform = floor.GetComponent<BoxCollider>() ??
                floor.gameObject.AddComponent<BoxCollider>();
            HomeBlockPlatform blockPlatform =
                floor.GetComponent<HomeBlockPlatform>() ??
                floor.gameObject.AddComponent<HomeBlockPlatform>();
            Material floorMaterial =
                floor.GetComponent<Renderer>()?.sharedMaterial;
            HomeGridOccupant foundationOccupant =
                floor.GetComponent<HomeGridOccupant>() ??
                floor.gameObject.AddComponent<HomeGridOccupant>();
            foundationOccupant.Configure(
                grid,
                new Vector3Int(-6, -1, -5),
                new Vector3Int(12, 1, 10));
            blockPlatform.Configure(grid, 12, 10, floorMaterial);
            basePlatform = floor.GetComponent<BoxCollider>();
            EnsureFallCatch(floor);

            if (player == null)
            {
                return;
            }

            safePlayerPosition = player.position;
            CharacterController controller =
                player.GetComponent<CharacterController>();
            if (controller != null)
            {
                Physics.IgnoreCollision(
                    controller,
                    basePlatform,
                    false);
                if (fallCatch != null)
                {
                    Physics.IgnoreCollision(
                        controller,
                        fallCatch,
                        false);
                }
            }
        }

        private void EnsureFallCatch(Transform floor)
        {
            const string catchName = "Home Fall Catch";
            Transform catchTransform = floor.parent != null
                ? floor.parent.Find(catchName)
                : null;
            if (catchTransform == null)
            {
                GameObject catchObject = new GameObject(catchName);
                catchTransform = catchObject.transform;
                catchTransform.SetParent(floor.parent, false);
            }

            Bounds floorBounds = basePlatform.bounds;
            catchTransform.position = new Vector3(
                floorBounds.center.x,
                floorBounds.min.y - 1f,
                floorBounds.center.z);
            catchTransform.rotation = Quaternion.identity;
            catchTransform.localScale = Vector3.one;
            fallCatch = catchTransform.GetComponent<BoxCollider>() ??
                catchTransform.gameObject.AddComponent<BoxCollider>();
            fallCatch.enabled = true;
            fallCatch.isTrigger = false;
            fallCatch.center = Vector3.zero;
            fallCatch.size = new Vector3(
                floorBounds.size.x,
                2f,
                floorBounds.size.z);
        }

        private void RecoverPlayerBelowPlatform()
        {
            if (player == null || fallCatch == null ||
                player.position.y >= fallCatch.bounds.min.y - 1f)
            {
                return;
            }

            CharacterController controller =
                player.GetComponent<CharacterController>();
            ThirdPersonMotor motor =
                player.GetComponent<ThirdPersonMotor>();
            Vector3 recoveryPosition = safePlayerPosition;
            recoveryPosition.y = basePlatform.bounds.max.y + 1f;
            if (motor != null)
            {
                motor.ResetForDiagnostics(
                    recoveryPosition,
                    player.rotation);
                return;
            }

            bool controllerWasEnabled =
                controller != null && controller.enabled;
            if (controller != null)
            {
                controller.enabled = false;
            }
            player.position = recoveryPosition;
            if (controller != null)
            {
                controller.enabled = controllerWasEnabled;
            }
        }

        private void OnGUI()
        {
            if (!ShouldShowReticle())
            {
                return;
            }

            EnsureDrawingResources();
            Camera camera = Camera.main;
            Vector3 aimPoint =
                LootInteractionPresentation.CalculateAimPoint(
                    camera,
                    player,
                    Screen.width,
                    Screen.height);
            float guiAimY = Screen.height - aimPoint.y;
            const float size = 16f;
            Color previous = GUI.color;
            GUI.color = hasSelection
                ? new Color(0.82f, 1f, 0.72f, 0.95f)
                : new Color(1f, 1f, 1f, 0.72f);
            GUI.DrawTexture(
                new Rect(
                    aimPoint.x - size * 0.5f,
                    guiAimY - size * 0.5f,
                    size,
                    size),
                reticleTexture);
            GUI.color = previous;
        }

        private void OnRenderObject()
        {
            if (lineMaterial == null)
            {
                EnsureDrawingResources();
            }
            if (lineMaterial == null || !lineMaterial.SetPass(0))
            {
                return;
            }

            if (hasSelection)
            {
                DrawSelection();
            }
        }

        private bool ShouldShowReticle()
        {
            return player != null &&
                Time.timeScale > 0f &&
                !SceneNavigationMenu.IsAnyOpen &&
                (inventory == null || !inventory.IsOpen) &&
                (anvil == null || !anvil.IsOpen);
        }

        private void ResolveSelection()
        {
            hasSelection = false;
            Camera camera = Camera.main;
            if (!ShouldShowReticle() || camera == null)
            {
                return;
            }

            Ray ray = camera.ScreenPointToRay(
                LootInteractionPresentation.CalculateAimPoint(
                    camera,
                    player,
                    Screen.width,
                    Screen.height));
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                focusHits,
                camera.farClipPlane,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            Array.Sort(
                focusHits,
                0,
                hitCount,
                RaycastHitDistanceComparer.Instance);
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = focusHits[index];
                if (hit.collider == null ||
                    hit.collider.transform.IsChildOf(player))
                {
                    continue;
                }

                if (!blockSurfaces.Contains(hit.collider as BoxCollider))
                {
                    return;
                }
                if (Vector3.Distance(player.position, hit.point) >
                    buildReach)
                {
                    return;
                }

                ResolveSelectedFace(hit);
                return;
            }
        }

        private void ResolveSelectedFace(RaycastHit hit)
        {
            Bounds bounds = hit.collider.bounds;
            Vector3 normal = DominantAxis(hit.normal);
            float cellSize = grid != null ? grid.CellSize : 2.5f;
            selectionNormal = normal;

            if (Mathf.Abs(normal.y) > 0.5f)
            {
                selectionAxisU = Vector3.right;
                selectionAxisV = Vector3.forward;
                selectionWidth = Mathf.Min(cellSize, bounds.size.x);
                selectionHeight = Mathf.Min(cellSize, bounds.size.z);
                selectionCenter = new Vector3(
                    SnapWithinBounds(
                        hit.point.x,
                        bounds.min.x,
                        bounds.max.x,
                        cellSize),
                    normal.y > 0f ? bounds.max.y : bounds.min.y,
                    SnapWithinBounds(
                        hit.point.z,
                        bounds.min.z,
                        bounds.max.z,
                        cellSize));
            }
            else if (Mathf.Abs(normal.x) > 0.5f)
            {
                selectionAxisU = Vector3.forward;
                selectionAxisV = Vector3.up;
                selectionWidth = Mathf.Min(cellSize, bounds.size.z);
                selectionHeight = Mathf.Min(cellSize, bounds.size.y);
                selectionCenter = new Vector3(
                    normal.x > 0f ? bounds.max.x : bounds.min.x,
                    SnapWithinBounds(
                        hit.point.y,
                        bounds.min.y,
                        bounds.max.y,
                        cellSize),
                    SnapWithinBounds(
                        hit.point.z,
                        bounds.min.z,
                        bounds.max.z,
                        cellSize));
            }
            else
            {
                selectionAxisU = Vector3.right;
                selectionAxisV = Vector3.up;
                selectionWidth = Mathf.Min(cellSize, bounds.size.x);
                selectionHeight = Mathf.Min(cellSize, bounds.size.y);
                selectionCenter = new Vector3(
                    SnapWithinBounds(
                        hit.point.x,
                        bounds.min.x,
                        bounds.max.x,
                        cellSize),
                    SnapWithinBounds(
                        hit.point.y,
                        bounds.min.y,
                        bounds.max.y,
                        cellSize),
                    normal.z > 0f ? bounds.max.z : bounds.min.z);
            }
            selectionCenter += selectionNormal * SurfaceOffset;
            hasSelection = true;
        }

        private void CacheBlockSurfaces()
        {
            blockSurfaces.Clear();
            BoxCollider[] colliders =
                FindObjectsByType<BoxCollider>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            for (int index = 0; index < colliders.Length; index++)
            {
                string objectName = colliders[index].gameObject.name;
                if (string.Equals(
                        objectName,
                        "Base Floor",
                        StringComparison.Ordinal) ||
                    objectName.EndsWith(
                        " Wall",
                        StringComparison.Ordinal))
                {
                    blockSurfaces.Add(colliders[index]);
                }
            }
        }

        private static void SimplifyToPlatformOnly()
        {
            Transform[] transforms =
                FindObjectsByType<Transform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int index = 0; index < transforms.Length; index++)
            {
                string objectName = transforms[index].gameObject.name;
                bool wall = objectName.EndsWith(
                    " Wall",
                    StringComparison.Ordinal);
                bool gateStructure =
                    string.Equals(
                        objectName,
                        "Raid Gate Left",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        objectName,
                        "Raid Gate Right",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        objectName,
                        "Raid Gate Header",
                        StringComparison.Ordinal);
                if (wall || gateStructure)
                {
                    transforms[index].gameObject.SetActive(false);
                }
            }
        }

        private void KeepSingleCubeChest()
        {
            HomeStorageChest[] chests =
                FindObjectsByType<HomeStorageChest>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (chests.Length == 0)
            {
                return;
            }

            HomeStorageChest keptChest = null;
            for (int index = 0; index < chests.Length; index++)
            {
                if (string.Equals(
                    chests[index].ChestId,
                    PlayerProfile.DefaultChestId,
                    StringComparison.Ordinal))
                {
                    keptChest = chests[index];
                    break;
                }
            }
            keptChest ??= chests[0];

            for (int index = 0; index < chests.Length; index++)
            {
                if (ReferenceEquals(chests[index], keptChest))
                {
                    continue;
                }
                Transform root = chests[index].transform.parent != null
                    ? chests[index].transform.parent
                    : chests[index].transform;
                root.gameObject.SetActive(false);
            }

            ShapeChestAsCube(keptChest);
        }

        private static void ShapeChestAsCube(HomeStorageChest chest)
        {
            Transform root = chest.transform.parent != null
                ? chest.transform.parent
                : chest.transform;
            Transform model = FindDescendant(root, "Chest Model");
            if (model == null)
            {
                return;
            }
            Transform scaleFrame = root.Find("Chest Scale Frame");
            if (scaleFrame == null)
            {
                GameObject scaleObject =
                    new GameObject("Chest Scale Frame");
                scaleFrame = scaleObject.transform;
                scaleFrame.SetParent(root, false);
                scaleFrame.position = root.position;
                scaleFrame.rotation = Quaternion.identity;
                model.SetParent(scaleFrame, true);
            }
            Renderer[] renderers = model != null
                ? model.GetComponentsInChildren<Renderer>(true)
                : Array.Empty<Renderer>();
            if (!TryGetBounds(renderers, out Bounds bounds))
            {
                return;
            }

            const float cubeSize = 1.5f;
            Vector3 scale = scaleFrame.localScale;
            scale.x *= cubeSize /
                Mathf.Max(0.001f, bounds.size.x);
            scale.y *= cubeSize /
                Mathf.Max(0.001f, bounds.size.y);
            scale.z *= cubeSize /
                Mathf.Max(0.001f, bounds.size.z);
            scaleFrame.localScale = scale;
            TryGetBounds(renderers, out bounds);
            model.position += new Vector3(
                root.position.x - bounds.center.x,
                root.position.y - bounds.min.y,
                root.position.z - bounds.center.z);
            TryGetBounds(renderers, out bounds);

            BoxCollider solid = root.GetComponent<BoxCollider>();
            if (solid != null)
            {
                solid.center = root.InverseTransformPoint(bounds.center);
                solid.size = bounds.size;
            }
            chest.transform.localPosition =
                new Vector3(0f, cubeSize * 0.5f, 0f);
            BoxCollider interaction = chest.GetComponent<BoxCollider>();
            if (interaction != null)
            {
                interaction.center = Vector3.zero;
                interaction.size = Vector3.one * (cubeSize + 0.2f);
            }
        }

        private static Transform FindDescendant(
            Transform root,
            string objectName)
        {
            Transform[] transforms =
                root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (string.Equals(
                        transforms[index].gameObject.name,
                        objectName,
                        StringComparison.Ordinal))
                {
                    return transforms[index];
                }
            }
            return null;
        }

        private void EnsureHomeAnvil()
        {
            GameObject root = null;
            Transform[] transforms =
                FindObjectsByType<Transform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (string.Equals(
                        transforms[index].gameObject.name,
                        "Home Anvil",
                        StringComparison.Ordinal))
                {
                    root = transforms[index].gameObject;
                    break;
                }
            }

            if (root != null)
            {
                EnsureHomeAnvilInteraction(root);
                return;
            }

            GameObject source =
                Resources.Load<GameObject>("HomeBase/Anvil/anvil");
            if (source == null || grid == null)
            {
                return;
            }

            root = new GameObject("Home Anvil");
            Transform environment = grid.transform.parent;
            root.transform.SetParent(environment, false);
            HomeGridOccupant occupant =
                root.AddComponent<HomeGridOccupant>();
            occupant.Configure(
                grid,
                new Vector3Int(-3, 0, 3),
                Vector3Int.one,
                1);

            GameObject model = Instantiate(source, root.transform);
            model.name = "Anvil Model";
            Renderer[] renderers =
                model.GetComponentsInChildren<Renderer>(true);
            Texture2D baseColor =
                Resources.Load<Texture2D>(
                    "HomeBase/Anvil/basecolor");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard");
            if (shader != null)
            {
                var material = new Material(shader)
                {
                    name = "Home Anvil Material"
                };
                material.mainTexture = baseColor;
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", baseColor);
                }
                if (material.HasProperty("_Smoothness"))
                {
                    material.SetFloat("_Smoothness", 0.58f);
                }
                if (material.HasProperty("_Metallic"))
                {
                    material.SetFloat("_Metallic", 0.42f);
                }
                for (int index = 0; index < renderers.Length; index++)
                {
                    renderers[index].sharedMaterial = material;
                }
            }

            if (TryGetBounds(
                    renderers,
                    out Bounds initialBounds))
            {
                const float maximumSize = 1.25f;
                float scale = maximumSize / Mathf.Max(
                    0.001f,
                    Mathf.Max(
                        initialBounds.size.x,
                        Mathf.Max(
                            initialBounds.size.y,
                            initialBounds.size.z)));
                model.transform.localScale *= scale;
            }
            if (TryGetBounds(renderers, out Bounds bounds))
            {
                model.transform.position += new Vector3(
                    root.transform.position.x - bounds.center.x,
                    root.transform.position.y - bounds.min.y,
                    root.transform.position.z - bounds.center.z);
            }
            EnsureHomeAnvilInteraction(root);
        }

        private static void EnsureHomeAnvilInteraction(GameObject root)
        {
            Transform existing = root.transform.Find("Anvil Interaction");
            GameObject interaction = existing != null
                ? existing.gameObject
                : new GameObject("Anvil Interaction");
            interaction.transform.SetParent(root.transform, false);
            interaction.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            BoxCollider trigger = interaction.GetComponent<BoxCollider>();
            if (trigger == null)
            {
                trigger = interaction.AddComponent<BoxCollider>();
            }
            trigger.isTrigger = true;
            trigger.size = new Vector3(1.65f, 1.45f, 1.65f);
            if (interaction.GetComponent<HomeAnvil>() == null)
            {
                interaction.AddComponent<HomeAnvil>();
            }
        }

        private void DrawSelection()
        {
            float halfWidth = selectionWidth * 0.49f;
            float halfHeight = selectionHeight * 0.49f;
            Vector3 a = selectionCenter - selectionAxisU * halfWidth -
                selectionAxisV * halfHeight;
            Vector3 b = selectionCenter + selectionAxisU * halfWidth -
                selectionAxisV * halfHeight;
            Vector3 c = selectionCenter + selectionAxisU * halfWidth +
                selectionAxisV * halfHeight;
            Vector3 d = selectionCenter - selectionAxisU * halfWidth +
                selectionAxisV * halfHeight;

            GL.Begin(GL.QUADS);
            GL.Color(new Color(0.48f, 0.85f, 0.35f, 0.20f));
            GL.Vertex(a);
            GL.Vertex(b);
            GL.Vertex(c);
            GL.Vertex(d);
            GL.End();
            GL.Begin(GL.LINES);
            GL.Color(new Color(0.78f, 1f, 0.64f, 0.95f));
            GL.Vertex(a); GL.Vertex(b);
            GL.Vertex(b); GL.Vertex(c);
            GL.Vertex(c); GL.Vertex(d);
            GL.Vertex(d); GL.Vertex(a);
            GL.End();
        }

        private void EnsureDrawingResources()
        {
            if (lineMaterial == null)
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                if (shader != null)
                {
                    lineMaterial = new Material(shader)
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    lineMaterial.SetInt(
                        "_SrcBlend",
                        (int)BlendMode.SrcAlpha);
                    lineMaterial.SetInt(
                        "_DstBlend",
                        (int)BlendMode.OneMinusSrcAlpha);
                    lineMaterial.SetInt("_Cull", (int)CullMode.Off);
                    lineMaterial.SetInt("_ZWrite", 0);
                    lineMaterial.SetInt(
                        "_ZTest",
                        (int)CompareFunction.LessEqual);
                }
            }
            reticleTexture ??= CreateReticleTexture();
        }

        private static Texture2D CreateReticleTexture()
        {
            const int size = 16;
            var texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false)
            {
                name = "Home Block Reticle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            Vector2 center = Vector2.one * (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(
                        new Vector2(x, y),
                        center);
                    byte alpha =
                        (distance >= 5f && distance <= 6.25f) ||
                        distance <= 1.5f
                            ? (byte)255
                            : (byte)0;
                    pixels[y * size + x] =
                        new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static float SnapWithinBounds(
            float value,
            float minimum,
            float maximum,
            float cellSize)
        {
            float extent = Mathf.Max(0f, maximum - minimum);
            float size = Mathf.Min(cellSize, extent);
            float firstCenter = minimum + size * 0.5f;
            float lastCenter = maximum - size * 0.5f;
            float snapped = firstCenter +
                Mathf.Round((value - firstCenter) / cellSize) * cellSize;
            return Mathf.Clamp(snapped, firstCenter, lastCenter);
        }

        private static Vector3 DominantAxis(Vector3 normal)
        {
            Vector3 absolute = new Vector3(
                Mathf.Abs(normal.x),
                Mathf.Abs(normal.y),
                Mathf.Abs(normal.z));
            if (absolute.y >= absolute.x && absolute.y >= absolute.z)
            {
                return normal.y >= 0f ? Vector3.up : Vector3.down;
            }
            if (absolute.x >= absolute.z)
            {
                return normal.x >= 0f ? Vector3.right : Vector3.left;
            }
            return normal.z >= 0f ? Vector3.forward : Vector3.back;
        }

        private static bool TryGetBounds(
            Renderer[] renderers,
            out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return found;
        }

        private static bool TryGetLocalBounds(
            Transform root,
            Renderer[] renderers,
            out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                Bounds local = renderer.localBounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = local.center + new Vector3(
                        (corner & 1) == 0
                            ? -local.extents.x
                            : local.extents.x,
                        (corner & 2) == 0
                            ? -local.extents.y
                            : local.extents.y,
                        (corner & 4) == 0
                            ? -local.extents.z
                            : local.extents.z);
                    point = root.InverseTransformPoint(
                        renderer.transform.TransformPoint(point));
                    if (!found)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        found = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }
            return found;
        }

        private sealed class RaycastHitDistanceComparer :
            IComparer<RaycastHit>
        {
            public static readonly RaycastHitDistanceComparer Instance =
                new RaycastHitDistanceComparer();

            public int Compare(RaycastHit left, RaycastHit right)
            {
                return left.distance.CompareTo(right.distance);
            }
        }
    }
}
