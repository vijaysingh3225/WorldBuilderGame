using System;
using UnityEngine;
using WorldBuilder.Gameplay.Core;

namespace WorldBuilder.Gameplay.Loop
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class GameplayLoopBootstrap : MonoBehaviour
    {
        [SerializeField] private bool initializeOnAwake = true;
        [SerializeField] private GameLaunchMode directSceneLaunchMode =
            GameLaunchMode.CombatLab;
        [SerializeField] private string defaultProfileSlotId =
            GameLaunchContext.DefaultProfileSlot;
        [SerializeField] private string directScenePresetId = "direct-scene";

        private static GameplayLoopBootstrap current;

        public static GameplayLoopBootstrap Current => current;
        public GameSession Session { get; private set; }
        public string LastInitializationError { get; private set; }
        public bool HasSession => Session != null;

        public event Action<GameSession> SessionChanged;

        private void Awake()
        {
            if (current != null && current != this)
            {
                Destroy(gameObject);
                return;
            }

            current = this;
            DontDestroyOnLoad(gameObject);

            if (initializeOnAwake && Session == null)
            {
                TryStart(BuildDirectSceneContext());
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                SavePersistentProfile();
            }
        }

        private void OnApplicationQuit()
        {
            SavePersistentProfile();
        }

        public bool StartFreshGame(
            string profileSlotId = GameLaunchContext.DefaultProfileSlot,
            bool allowOverwriteExisting = false)
        {
            return TryStart(
                GameLaunchContext.CreateFreshGame(profileSlotId),
                allowProfileOverwrite: allowOverwriteExisting);
        }

        public bool ContinueGame(
            string profileSlotId = GameLaunchContext.DefaultProfileSlot)
        {
            return TryStart(GameLaunchContext.CreateContinue(profileSlotId));
        }

        public bool StartHomeSandbox(
            string presetId = "home-default",
            PlayerProfile seedProfile = null)
        {
            return TryStart(
                GameLaunchContext.CreateHomeSandbox(presetId),
                seedProfile);
        }

        public bool StartRaidSandbox(
            string presetId = "raid-default",
            int? fixedSeed = null,
            PlayerProfile seedProfile = null)
        {
            return TryStart(
                GameLaunchContext.CreateRaidSandbox(presetId, fixedSeed),
                seedProfile);
        }

        public bool StartCombatLab(PlayerProfile seedProfile = null)
        {
            return TryStart(GameLaunchContext.CreateCombatLab(), seedProfile);
        }

        public bool TryStart(
            GameLaunchContext launchContext,
            PlayerProfile sandboxSeedProfile = null,
            IPlayerProfileStore profileStoreOverride = null,
            bool allowProfileOverwrite = false)
        {
            if (launchContext == null)
            {
                LastInitializationError = "A launch context is required.";
                return false;
            }

            try
            {
                GameLaunchContext context = launchContext.Clone();
                context.Normalize();
                IPlayerProfileStore store = profileStoreOverride ??
                    CreateStore(context);
                Session = new GameSession(
                    context,
                    store,
                    sandboxSeedProfile,
                    allowProfileOverwrite: allowProfileOverwrite);
                LastInitializationError = string.Empty;
                GameplayEventLog.Publish(
                    "session_started",
                    gameObject,
                    context.Mode.ToString());
                SessionChanged?.Invoke(Session);
                return true;
            }
            catch (Exception exception)
            {
                Session = null;
                LastInitializationError = exception.Message;
                Debug.LogError(
                    $"Gameplay loop initialization failed: {exception}",
                    this);
                return false;
            }
        }

        public bool TryGetPersistentProfileExists(
            string profileSlotId,
            out bool exists)
        {
            try
            {
                IPlayerProfileStore store = new JsonPlayerProfileStore();
                exists = store.Exists(profileSlotId);
                LastInitializationError = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                exists = false;
                LastInitializationError =
                    $"Could not inspect profile slot '{profileSlotId}': " +
                    exception.Message;
                Debug.LogError(LastInitializationError, this);
                return false;
            }
        }

        public void SavePersistentProfile()
        {
            if (Session == null || !Session.ProfileStore.IsPersistent)
            {
                return;
            }

            try
            {
                Session.SaveProfile();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not save the active profile: {exception}", this);
            }
        }

        private GameLaunchContext BuildDirectSceneContext()
        {
            switch (directSceneLaunchMode)
            {
                case GameLaunchMode.FreshGame:
                    return GameLaunchContext.CreateFreshGame(defaultProfileSlotId);
                case GameLaunchMode.Continue:
                    return GameLaunchContext.CreateContinue(defaultProfileSlotId);
                case GameLaunchMode.HomeSandbox:
                    return GameLaunchContext.CreateHomeSandbox(directScenePresetId);
                case GameLaunchMode.RaidSandbox:
                    return GameLaunchContext.CreateRaidSandbox(directScenePresetId);
                case GameLaunchMode.CombatLab:
                default:
                    return GameLaunchContext.CreateCombatLab();
            }
        }

        private static IPlayerProfileStore CreateStore(GameLaunchContext context)
        {
            return context.PersistenceEnabled
                ? new JsonPlayerProfileStore()
                : new MemoryPlayerProfileStore();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            current = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrapExists()
        {
            if (current != null ||
                FindFirstObjectByType<GameplayLoopBootstrap>() != null)
            {
                return;
            }

            GameObject bootstrapObject = new GameObject("[Gameplay Loop]");
            bootstrapObject.AddComponent<GameplayLoopBootstrap>();
        }
    }
}
