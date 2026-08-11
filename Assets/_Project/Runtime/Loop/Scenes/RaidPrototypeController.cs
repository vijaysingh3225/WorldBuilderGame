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
        private int lootCollected;
        private int obelisksActivated;
        private string statusMessage =
            "Find and activate the four obelisks.";
        private string completionMessage = string.Empty;

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

        private void Update()
        {
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
            if (keyboard != null &&
                keyboard.hKey.wasPressedThisFrame)
            {
                CompleteRaid(
                    RaidCompletionReason.Abandoned,
                    0.1f);
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

        private void OnGUI()
        {
            DrawHealthBar(new Rect(24f, 40f, 300f, 18f));
            GUI.Label(
                new Rect(24f, 63f, 180f, 20f),
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
                DrawBowCharge(new Rect(24f, 88f, 300f, 12f));
            }
            else if (shortSwordAttack != null &&
                     shortSwordAttack.IsHeavyCharging)
            {
                DrawHeavyCharge(new Rect(24f, 88f, 300f, 12f));
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
                    $"Collected {pickup.DisplayName}. " +
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
            if (!RaidActive)
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

        private static void EnsureProceduralSword(Transform actor)
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
                RaidShortSwordPresentation presentation =
                    RaidShortSwordPresentation.Replace(
                    sword,
                    UnityEngine.Random.Range(int.MinValue, int.MaxValue));
                presentation?.ConfigureMeleeWeapon(
                    actor.GetComponent<MeleeWeapon>());
            }
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
                    "Enemy defeated. Keep moving toward extraction.";
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
                new Color(0.02f, 0.025f, 0.03f, 0.9f);
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
            Color previous = GUI.color;
            GUI.color =
                new Color(0.02f, 0.025f, 0.03f, 0.9f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = bowWeapon.CanFire
                ? new Color(0.90f, 0.64f, 0.20f)
                : new Color(0.42f, 0.43f, 0.45f);
            GUI.DrawTexture(
                new Rect(
                    rect.x + 2f,
                    rect.y + 2f,
                    (rect.width - 4f) *
                        bowWeapon.DrawNormalized,
                    rect.height - 4f),
                Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void DrawHeavyCharge(Rect rect)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0.02f, 0.025f, 0.03f, 0.9f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(0.78f, 0.28f, 0.16f);
            GUI.DrawTexture(
                new Rect(
                    rect.x + 2f,
                    rect.y + 2f,
                    (rect.width - 4f) *
                    shortSwordAttack.HeavyChargeNormalized,
                    rect.height - 4f),
                Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(
                new Rect(rect.x, rect.y + 12f, rect.width, 20f),
                "HEAVY STRIKE",
                LoopSceneGui.Muted);
        }
    }
}
