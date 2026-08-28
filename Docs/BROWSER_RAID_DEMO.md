# Browser Raid Demo

The browser build is an isolated, disposable raid session. It contains only
the Bootstrap and Raid Prototype scenes, uses fixed seed `30817`, disables
extraction and persistence, and offers replay or return-to-menu after death.

## First-time Unity setup

1. Close Unity.
2. Open Unity Hub and select **Installs**.
3. Open the settings menu for Unity `6000.3.20f1`.
4. Select **Add modules**.
5. Install **Web Build Support**.
6. Reopen `WorldBuilderGame`.

## Test and create the upload

1. Use **WorldBuilder > Play > Browser Raid Demo** to test the flow in the
   Editor.
2. Use **WorldBuilder > Build > Browser Raid Demo and Run** to compile a Web
   build and open it using Unity's local server.
3. For the uploadable release, use
   **WorldBuilder > Build > Browser Raid Demo**.
4. Find the completed build in `Artifacts/RaidBrowserDemo`.
5. In PowerShell, from the project root, create a ZIP whose root contains
   `index.html`:

   ```powershell
   Compress-Archive `
     -Path .\Artifacts\RaidBrowserDemo\* `
     -DestinationPath .\Artifacts\WorldBuilder-RaidDemo-001.zip
   ```

Do not ZIP the `RaidBrowserDemo` directory itself. Opening `index.html`
directly from disk is not a valid test; use Unity's **Build and Run** command
or an HTTP host.

## Upload to itch.io

1. Sign in to itch.io and choose **Dashboard > Create new project**.
2. Enter a title and project URL.
3. Set **Kind of project** to **HTML**.
4. Upload `WorldBuilder-RaidDemo-001.zip` and mark it as playable in the
   browser if itch.io presents that option.
5. Choose **Click to launch in fullscreen**. Leave mobile-friendly disabled
   for this keyboard-and-mouse prototype.
6. Keep the page in **Draft** or **Restricted** while testing.
7. Save the page, open its private view, and test loading, mouse capture,
   movement, combat, Escape, restart, and a second complete load.
8. Change the page visibility to **Public** when the private build passes.

The build uses Brotli compression and browser data caching. itch.io recognizes
Unity Brotli files and supplies the required response headers. Its current
HTML upload limits are 500 MB extracted, 200 MB for any individual file, and
1,000 files. See <https://itch.io/docs/creators/html5>.
