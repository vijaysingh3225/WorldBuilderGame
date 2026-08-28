using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Core;
using WorldBuilder.Gameplay.Presentation;
using WorldBuilder.Gameplay.WeaponGrid;
using WorldBuilder.Gameplay.Weapons;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DisallowMultipleComponent]
    public sealed class RaidPrototypeController : MonoBehaviour
    {
        [SerializeField] private Health playerHealth;
        [SerializeField] private PlayerStamina playerStamina;
        [SerializeField, Min(0f)] private float extractionReturnDelay =
            0.85f;
        [SerializeField, Min(0f)] private float deathReturnDelay =
            3.25f;

        private readonly List<EnemyBrain> enemies =
            new List<EnemyBrain>();
        private readonly List<Health> enemyHealth =
            new List<Health>();
        private readonly List<RaidObelisk> obelisks =
            new List<RaidObelisk>();
        private GameplayLoopBootstrap bootstrap;
        private GameSession session;
        private WeaponGridSandboxToolkit gridToolkit;
        private BowWeapon bowWeapon;
        private ShortSwordAttackPresenter shortSwordAttack;
        private Transform playerRoot;
        private bool initialized;
        private bool playerDeathSubscribed;
        private bool completionPending;
        private bool showCompletionOverlay;
        private float returnAt;
        private float actorRefreshAt;
        private float localLightSafetyRefreshAt;
        private int lootCollected;
        private int obelisksActivated;
        private string statusMessage =
            "Find and activate the four obelisks.";
        private string completionMessage = string.Empty;
        private GUIStyle compactBarStyle;

        public GameSession Session => session;
        public bool RaidActive =>
            session != null &&
            session.HasActiveRaid &&
            !completionPending;
        public int LootCollected => lootCollected;
        public int ObelisksActivated => obelisksActivated;
        public int ObeliskCount => obelisks.Count;
        public int ArrowCount =>
            session != null &&
            session.ActiveRaid != null
                ? session.ActiveRaid.GetItemQuantity(
                    ItemDefinitionIds.Arrow,
                    session.ActiveProfile)
                : 0;

        public event Action AllObelisksActivated;

        public static bool ShouldShowCompletionOverlay(
            RaidCompletionReason reason)
        {
            return reason != RaidCompletionReason.PlayerDied;
        }

        public void Configure(Health player)
        {
            if (playerHealth != null &&
                playerDeathSubscribed)
            {
                playerHealth.Died -= HandlePlayerDied;
            }

            playerHealth = player;
            playerDeathSubscribed = false;
            playerRoot =
                player != null ? player.transform : null;
            ResolvePlayerStamina();
            if (Application.isPlaying &&
                isActiveAndEnabled)
            {
                SubscribeToPlayerDeath();
            }
        }

        private void Start()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            SubscribeToPlayerDeath();
            SubscribeToEnemyDeaths();
        }

        private void Update()
        {
            if (Time.unscaledTime >= localLightSafetyRefreshAt)
            {
                DisableUnexpectedLocalLights();
                localLightSafetyRefreshAt = Time.unscaledTime + 0.25f;
            }

            if (!initialized)
            {
                Initialize();
            }

            if (Time.unscaledTime >= actorRefreshAt)
            {
                ResolveActors();
                actorRefreshAt = Time.unscaledTime + 1f;
            }

            if (completionPending)
            {
                if (Time.unscaledTime >= returnAt)
                {
                    ReturnHome();
                }

                return;
            }

            ActivateEnemiesForPatrol();

            gridToolkit ??=
                FindFirstObjectByType<WeaponGridSandboxToolkit>();
            if (gridToolkit != null && gridToolkit.IsOpen)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (!BrowserRaidDemoController.IsEnabled &&
                keyboard != null &&
                keyboard.hKey.wasPressedThisFrame)
            {
                CompleteRaid(
                    RaidCompletionReason.Abandoned,
                    0.1f);
            }
        }

        // The raid is lit solely by its directional sun. Local lights have
        // repeatedly arrived through generated props and pooled actors, and
        // their HDR/bloom interaction can make the game unreadable. Keep the
        // invariant in the live scene as a final safety net.
        private static void DisableUnexpectedLocalLights()
        {
            Light[] lights = FindObjectsByType<Light>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int index = 0; index < lights.Length; index++)
            {
                Light light = lights[index];
                if (light == null || light.type == LightType.Directional)
                {
                    continue;
                }

                light.intensity = 0f;
                light.range = 0f;
                light.enabled = false;
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.Died -= HandlePlayerDied;
            }
            playerDeathSubscribed = false;

            for (int index = 0;
                 index < enemyHealth.Count;
                 index++)
            {
                if (enemyHealth[index] != null)
                {
                    enemyHealth[index].Died -=
                        HandleEnemyDied;
                }
            }
        }

        private void SubscribeToEnemyDeaths()
        {
            for (int index = 0;
                 index < enemyHealth.Count;
                 index++)
            {
                Health health = enemyHealth[index];
                if (health == null)
                {
                    continue;
                }

                // Make re-enabling the controller idempotent while restoring
                // subscriptions that OnDisable deliberately removes.
                health.Died -= HandleEnemyDied;
                health.Died += HandleEnemyDied;
            }
        }

        private void OnGUI()
        {
            CombatLabHud.CalculatePlayerBarRects(
                Screen.height,
                300f,
                out Rect healthRect,
                out Rect staminaRect,
                out Rect chargeRect);
            DrawHealthBar(healthRect);
            DrawResourceBar(
                staminaRect,
                playerStamina != null
                    ? playerStamina.Normalized
                    : 1f,
                new Color(0.12f, 0.10f, 0.045f, 0.95f),
                new Color(0.78f, 0.70f, 0.30f, 0.96f),
                "STAMINA");
            GUI.Label(
                new Rect(
                    healthRect.xMax + 10f,
                    healthRect.y - 1f,
                    150f,
                    healthRect.height + 2f),
                $"ARROWS  {ArrowCount}",
                LoopSceneGui.Muted);
            bowWeapon ??= FindFirstObjectByType<BowWeapon>();
            if (shortSwordAttack == null && playerRoot != null)
            {
                shortSwordAttack =
                    playerRoot.GetComponentInChildren<
                        ShortSwordAttackPresenter>(true);
            }
            if (bowWeapon != null &&
                (bowWeapon.IsDrawing ||
                 bowWeapon.DrawNormalized > 0f))
            {
                DrawBowCharge(chargeRect);
            }
            else if (shortSwordAttack != null &&
                     shortSwordAttack.IsHeavyCharging)
            {
                DrawHeavyCharge(chargeRect);
            }

            if (completionPending && showCompletionOverlay)
            {
                LoopSceneGui.DrawDimmer(0.42f);
                Rect messageRect = new Rect(
                    Screen.width * 0.5f - 240f,
                    Screen.height * 0.42f - 38f,
                    480f,
                    76f);
                LoopSceneGui.DrawPanel(
                    messageRect,
                    new Color(0.82f, 0.62f, 0.24f));
                GUI.Label(
                    messageRect,
                    completionMessage,
                    LoopSceneGui.Centered);
            }
        }

        public void RegisterEnemy(EnemyBrain enemy)
        {
            if (enemy == null || enemies.Contains(enemy))
            {
                return;
            }

            enemy.ConfigureForArenaDormancy();
            Health health = enemy.GetComponent<Health>();
            enemies.Add(enemy);
            enemyHealth.Add(health);
            RaidLootContainer loot =
                enemy.GetComponent<RaidLootContainer>() ??
                enemy.gameObject.AddComponent<RaidLootContainer>();
            int raidSeed = session != null &&
                session.ActiveRaid != null &&
                session.ActiveRaid.LaunchRequest != null
                    ? session.ActiveRaid.LaunchRequest.Seed
                    : 0;
            loot.ConfigureCorpse(
                enemy,
                raidSeed ^ StableHash(enemy.name));
            if (health != null)
            {
                health.Died += HandleEnemyDied;
            }

            if (!enemy.IsActivated)
            {
                enemy.enabled = false;
            }
        }

        public bool TryCollect(RaidPickup pickup)
        {
            if (pickup == null ||
                pickup.IsCollected ||
                !RaidActive)
            {
                return false;
            }

            try
            {
                StorageEntry entry = StorageEntry.Create(
                    pickup.DefinitionId,
                    pickup.Quantity);
                session.ActiveRaid.RecordLoot(
                    entry,
                    session.ActiveProfile);
                lootCollected++;
                statusMessage =
                    BrowserRaidDemoController.IsEnabled
                        ? $"Collected {pickup.DisplayName}."
                        : $"Collected {pickup.DisplayName}. " +
                          "Reach the extraction marker.";
                pickup.MarkCollected();
                return true;
            }
            catch (Exception exception)
            {
                statusMessage =
                    $"Could not collect pickup: {exception.Message}";
                return false;
            }
        }

        public bool TryTransferLoot(
            RaidLootContainer source,
            StorageEntry entry,
            out string message)
        {
            message = string.Empty;
            if (source == null ||
                entry == null ||
                !source.Contains(entry.EntryId) ||
                !RaidActive)
            {
                message = "That loot is no longer available.";
                return false;
            }

            if (!source.TryTake(
                    entry.EntryId,
                    entry.Quantity,
                    out StorageEntry taken))
            {
                message = "That loot is no longer available.";
                return false;
            }

            int moved = TryPlaceInInventory(taken, -1, true);
            if (moved < taken.Quantity)
            {
                StorageEntry remainder = taken.CreateSplitCopy(
                    taken.Quantity - moved);
                source.TryAdd(remainder, entry.SlotIndex, false);
                message = "The 4 x 6 backpack is full.";
                return false;
            }

            lootCollected++;
            message =
                $"{ItemDefinitionCatalog.DisplayName(entry.DefinitionId)} " +
                "moved to backpack.";
            return true;
        }

        public bool TryTakeInventoryEntry(
            StorageEntry entry,
            int quantity,
            out StorageEntry taken)
        {
            taken = null;
            return entry != null &&
                RaidActive &&
                session.ActiveRaid.TryTakeCarried(
                    entry.EntryId,
                    quantity,
                    session.ActiveProfile,
                    out taken);
        }

        public int TryPlaceInInventory(
            StorageEntry entry,
            int targetSlot,
            bool autoStack)
        {
            return entry != null && RaidActive
                ? session.ActiveRaid.TryAddCarried(
                    entry,
                    targetSlot,
                    autoStack,
                    session.ActiveProfile)
                : 0;
        }

        public bool TryTakeLootEntry(
            RaidLootContainer source,
            StorageEntry entry,
            int quantity,
            out StorageEntry taken)
        {
            taken = null;
            return source != null &&
                entry != null &&
                RaidActive &&
                source.TryTake(entry.EntryId, quantity, out taken);
        }

        public int TryPlaceInLoot(
            RaidLootContainer source,
            StorageEntry entry,
            int targetSlot,
            bool autoStack)
        {
            return source != null &&
                entry != null &&
                RaidActive
                    ? source.TryAdd(entry, targetSlot, autoStack)
                    : 0;
        }

        public bool TryConsumePlayerArrow()
        {
            return session != null &&
                session.ActiveRaid != null &&
                session.ActiveRaid.TryConsumeItem(
                    ItemDefinitionIds.Arrow,
                    1,
                    session.ActiveProfile);
        }

        public void RegisterObelisk(RaidObelisk obelisk)
        {
            if (obelisk == null || obelisks.Contains(obelisk))
            {
                return;
            }

            obelisks.Add(obelisk);
            if (obelisk.IsActivated)
            {
                obelisksActivated++;
            }
        }

        public bool TryActivateObelisk(RaidObelisk obelisk)
        {
            if (obelisk == null ||
                obelisk.IsActivated ||
                !RaidActive)
            {
                return false;
            }

            RegisterObelisk(obelisk);
            obelisk.MarkActivated();
            obelisksActivated++;
            statusMessage =
                $"Activated {obelisk.DisplayName}. " +
                $"{obelisksActivated}/{Mathf.Max(4, obelisks.Count)} " +
                "obelisks awakened.";
            GameplayEventLog.Publish(
                "raid-obelisk-activated",
                obelisk.gameObject,
                $"quadrant={obelisk.QuadrantIndex}; " +
                $"count={obelisksActivated}");

            if (obelisks.Count >= 4 &&
                obelisksActivated >= obelisks.Count)
            {
                statusMessage =
                    "All four obelisks are awake. Their purpose is still unknown.";
                AllObelisksActivated?.Invoke();
                GameplayEventLog.Publish(
                    "raid-all-obelisks-activated",
                    gameObject,
                    "future-objective-hook");
            }
            return true;
        }

        public bool TryExtract()
        {
            if (!RaidActive || BrowserRaidDemoController.IsEnabled)
            {
                return false;
            }

            CompleteRaid(
                RaidCompletionReason.Extracted,
                extractionReturnDelay);
            return completionPending;
        }

        private int DefeatedCount =>
            session != null &&
            session.ActiveRaid != null
                ? session.ActiveRaid.EnemiesDefeated
                : 0;

        private void Initialize()
        {
            bootstrap =
                GameplaySceneRuntime.ResolveBootstrap();
            session = bootstrap.Session;
            if (session == null ||
                session.LaunchContext.Mode ==
                    GameLaunchMode.CombatLab)
            {
                if (!bootstrap.StartRaidSandbox(
                        "direct-raid"))
                {
                    statusMessage =
                        bootstrap.LastInitializationError;
                    return;
                }

                session = bootstrap.Session;
                statusMessage =
                    "Direct scene play: using a disposable raid session.";
            }

            try
            {
                if (!session.HasActiveRaid)
                {
                    session.BeginRaid();
                }

                lootCollected =
                    session.ActiveRaid.CollectedStorageEntries.Count;
            }
            catch (Exception exception)
            {
                statusMessage =
                    $"Could not initialize raid: {exception.Message}";
                return;
            }

            ResolveActors();
            initialized = true;
        }

        private void ResolveActors()
        {
            if (playerHealth == null)
            {
                GameObject player =
                    GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerRoot = player.transform;
                    playerHealth =
                        player.GetComponent<Health>();
                    if (playerHealth != null)
                    {
                        SubscribeToPlayerDeath();
                    }
                }
            }
            else
            {
                playerRoot = playerHealth.transform;
                SubscribeToPlayerDeath();
            }
            ResolvePlayerStamina();

            EnemyBrain[] discoveredEnemies =
                FindObjectsByType<EnemyBrain>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int index = 0;
                 index < discoveredEnemies.Length;
                 index++)
            {
                RegisterEnemy(discoveredEnemies[index]);
            }

            RaidObelisk[] discoveredObelisks =
                FindObjectsByType<RaidObelisk>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int index = 0;
                 index < discoveredObelisks.Length;
                 index++)
            {
                RegisterObelisk(discoveredObelisks[index]);
            }

            EnsureProceduralRaidSwords(discoveredEnemies);

            if (playerRoot == null)
            {
                statusMessage =
                    "Raid session is active, but no Player was found.";
            }
        }

        private void EnsureProceduralRaidSwords(
            EnemyBrain[] discoveredEnemies)
        {
            EnsureProceduralSword(playerRoot);
            for (int index = 0; index < discoveredEnemies.Length; index++)
            {
                EnsureProceduralSword(discoveredEnemies[index] != null
                    ? discoveredEnemies[index].transform
                    : null);
            }
        }

        private void EnsureProceduralSword(Transform actor)
        {
            if (actor == null)
            {
                return;
            }
            TwoSlotWeaponPresenter weapons =
                actor.GetComponentInChildren<TwoSlotWeaponPresenter>(true);
            Transform sword = weapons != null
                ? weapons.PrimaryWeaponRoot
                : null;
            if (sword != null)
            {
                int raidSeed = session != null &&
                    session.ActiveRaid != null &&
                    session.ActiveRaid.LaunchRequest != null
                        ? session.ActiveRaid.LaunchRequest.Seed
                        : 0;
                int visualSeed = ResolveActorSwordSeed(
                    raidSeed,
                    actor == playerRoot ? "player" : actor.name);
                RaidLootContainer loot =
                    actor.GetComponent<RaidLootContainer>();
                if (loot != null &&
                    string.Equals(
                        loot.SpawnedWeaponDefinitionId,
                        ItemDefinitionIds.LootShortSword,
                        StringComparison.Ordinal))
                {
                    visualSeed = loot.SpawnedWeaponVisualSeed;
                }
                RaidShortSwordPresentation presentation =
                    RaidShortSwordPresentation.Replace(
                        sword,
                        visualSeed);
                presentation?.ConfigureMeleeWeapon(
                    actor.GetComponent<MeleeWeapon>());
            }
        }

        public static int ResolveActorSwordSeed(
            int raidSeed,
            string actorIdentity)
        {
            return unchecked(
                raidSeed * 486187739 ^
                StableHash(string.IsNullOrWhiteSpace(actorIdentity)
                    ? "raid-sword"
                    : actorIdentity));
        }

        private void ActivateEnemiesForPatrol()
        {
            if (playerRoot == null)
            {
                return;
            }

            for (int index = 0;
                 index < enemies.Count;
                 index++)
            {
                EnemyBrain enemy = enemies[index];
                Health health = enemyHealth[index];
                if (enemy != null &&
                    health != null &&
                    health.IsAlive &&
                    !enemy.IsActivated)
                {
                    enemy.enabled = true;
                    enemy.Configure(playerRoot);
                }
            }
        }

        private void HandlePlayerDied(DamageRequest request)
        {
            CompleteRaid(
                RaidCompletionReason.PlayerDied,
                deathReturnDelay);
        }

        private void SubscribeToPlayerDeath()
        {
            if (playerHealth == null ||
                playerDeathSubscribed)
            {
                return;
            }

            playerHealth.Died += HandlePlayerDied;
            playerDeathSubscribed = true;
        }

        private void HandleEnemyDied(DamageRequest request)
        {
            if (!RaidActive)
            {
                return;
            }

            try
            {
                session.ActiveRaid.RecordEnemyDefeated();
                int weaponSlot =
                    string.Equals(
                        request.SourceId,
                        "prototype-bow",
                        StringComparison.Ordinal)
                        ? 2
                        : 1;
                session.ActiveRaid.AddWeaponExperience(
                    weaponSlot,
                    5);
                statusMessage =
                    BrowserRaidDemoController.IsEnabled
                        ? "Enemy defeated. Keep exploring the raid."
                        : "Enemy defeated. Keep moving toward extraction.";
            }
            catch (InvalidOperationException)
            {
                // Completion and a death can land in the same frame.
            }
        }

        private void CompleteRaid(
            RaidCompletionReason reason,
            float returnDelay)
        {
            if (completionPending ||
                session == null ||
                !session.HasActiveRaid)
            {
                return;
            }

            try
            {
                RaidResult result =
                    session.CompleteActiveRaid(
                        reason,
                        out RaidOutcomeReceipt receipt);
                completionPending = true;
                if (BrowserRaidDemoController.IsEnabled)
                {
                    showCompletionOverlay = false;
                    returnAt = float.PositiveInfinity;
                    completionMessage = string.Empty;
                    BrowserRaidDemoController.NotifyRaidCompleted(
                        result.CompletionReason,
                        DefeatedCount,
                        lootCollected);
                    return;
                }
                showCompletionOverlay =
                    ShouldShowCompletionOverlay(
                        result.CompletionReason);
                returnAt =
                    Time.unscaledTime +
                    Mathf.Max(0f, returnDelay);
                switch (result.CompletionReason)
                {
                    case RaidCompletionReason.Extracted:
                        completionMessage =
                            $"EXTRACTED  /  {receipt.ItemsAdded} " +
                            "item(s) returned";
                        break;
                    case RaidCompletionReason.PlayerDied:
                        completionMessage = string.Empty;
                        break;
                    default:
                        completionMessage =
                            "RAID ABANDONED  /  returning home";
                        break;
                }
            }
            catch (Exception exception)
            {
                statusMessage =
                    $"Could not finish raid: {exception.Message}";
            }
        }

        private void ReturnHome()
        {
            if (!GameplaySceneRuntime.TryLoadScene(
                    GameplaySceneNames.HomeBase,
                    out string error))
            {
                statusMessage = error;
                showCompletionOverlay = true;
                completionMessage =
                    "RAID COMPLETE  /  Home Base scene unavailable";
                returnAt = float.PositiveInfinity;
            }
        }

        private int CountLivingEnemies()
        {
            int count = 0;
            for (int index = 0;
                 index < enemyHealth.Count;
                 index++)
            {
                if (enemyHealth[index] != null &&
                    enemyHealth[index].IsAlive)
                {
                    count++;
                }
            }

            return count;
        }

        private void DrawHealthBar(Rect rect)
        {
            float normalized =
                playerHealth != null
                    ? playerHealth.Normalized
                    : 0f;
            Color previous = GUI.color;
            GUI.color =
                new Color(0.30f, 0.075f, 0.065f, 0.96f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color =
                new Color(0.35f, 0.72f, 0.46f, 0.95f);
            GUI.DrawTexture(
                new Rect(
                    rect.x + 2f,
                    rect.y + 2f,
                    (rect.width - 4f) * normalized,
                    rect.height - 4f),
                Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(
                new Rect(
                    rect.x + 8f,
                    rect.y - 1f,
                    rect.width - 16f,
                    rect.height + 2f),
                playerHealth != null
                    ? $"HEALTH  {Mathf.CeilToInt(playerHealth.Current)}" +
                      $" / {Mathf.CeilToInt(playerHealth.Maximum)}"
                    : "HEALTH  —",
                LoopSceneGui.Muted);
        }

        private void ResolvePlayerStamina()
        {
            if (playerStamina != null || playerRoot == null)
            {
                return;
            }

            playerStamina = playerRoot.GetComponent<PlayerStamina>();
            if (playerStamina == null)
            {
                playerStamina = playerRoot.gameObject
                    .AddComponent<PlayerStamina>();
            }
        }

        private void DrawResourceBar(
            Rect rect,
            float normalized,
            Color missing,
            Color fill,
            string label)
        {
            Color previous = GUI.color;
            GUI.color = missing;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = fill;
            GUI.DrawTexture(
                new Rect(
                    rect.x + 1f,
                    rect.y + 1f,
                    (rect.width - 2f) * Mathf.Clamp01(normalized),
                    rect.height - 2f),
                Texture2D.whiteTexture);
            GUI.color = previous;
            compactBarStyle ??= new GUIStyle(LoopSceneGui.Muted)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
                normal =
                {
                    textColor = new Color(0.94f, 0.94f, 0.90f)
                }
            };
            GUI.Label(
                new Rect(
                    rect.x + 5f,
                    rect.y - 1f,
                    rect.width - 10f,
                    rect.height + 2f),
                label,
                compactBarStyle);
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                string safe = value ?? string.Empty;
                for (int index = 0; index < safe.Length; index++)
                {
                    hash = hash * 31 + safe[index];
                }
                return hash;
            }
        }

        private void DrawBowCharge(Rect rect)
        {
            DrawResourceBar(
                rect,
                bowWeapon.DrawNormalized,
                new Color(0.075f, 0.065f, 0.045f, 0.96f),
                bowWeapon.CanFire
                    ? new Color(0.90f, 0.64f, 0.20f)
                    : new Color(0.42f, 0.43f, 0.45f),
                bowWeapon.CanFire ? "BOW  READY" : "BOW  DRAW");
        }

        private void DrawHeavyCharge(Rect rect)
        {
            DrawResourceBar(
                rect,
                shortSwordAttack.HeavyChargeNormalized,
                new Color(0.11f, 0.045f, 0.035f, 0.96f),
                new Color(0.78f, 0.28f, 0.16f),
                "HEAVY STRIKE");
        }
    }
}
