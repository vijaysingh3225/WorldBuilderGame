# Unity and AI setup

## One-time editor setup

1. Install Unity 6.3 LTS through Unity Hub.
2. Sign into Hub and activate a Unity Personal license if Unity asks for one.
3. Add this repository as a project from disk and open it with Unity 6.3.
4. Allow the Universal Render Pipeline and Input System packages to import.
5. The Combat Lab builder should generate and open Assets/_Project/Scenes/CombatLab.unity. If it does not, use WorldBuilder > Build Combat Lab.
6. Set Visual Studio Code as the external script editor in Edit > Preferences > External Tools.

## Unity AI and scene-aware Codex

Unity AI is intentionally installed after the editor upgrade because the current Assistant package does not support the older editor originally installed on this computer.

1. Link the Unity project to a Unity Cloud organization from the editor.
2. Choose the AI button in Unity's toolbar, accept the beta terms, and choose Agree and install Unity AI.
3. Open Edit > Project Settings > AI > Unity MCP Server.
4. Enable the bridge and select Codex as the client. The first connection requires approval inside Unity.
5. Target this exact project when more than one Unity editor is open.

The MCP connection supplements the repository context in AGENTS.md. It lets an AI coding client inspect the live scene hierarchy and console and invoke approved editor actions; normal source control and review still apply.

Official references:

- https://unity.com/blog/unity-ai-how-to-get-started
- https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.9/manual/install/install-chat.html
- https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.9/manual/integration/unity-mcp-get-started.html
- https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.9/manual/integration/ai-gateway-get-started.html

## Iteration rule

Ask the agent for one observable gameplay change at a time. After each change:

1. Wait for Unity to compile.
2. Check the Console for errors.
3. Run the affected edit-mode tests.
4. Play the Combat Lab.
5. State what feels better or worse in concrete terms.
6. Commit only a coherent, working increment.
