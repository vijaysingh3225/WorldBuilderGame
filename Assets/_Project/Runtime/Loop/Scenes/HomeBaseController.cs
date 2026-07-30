using System;
using UnityEngine;
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
}
