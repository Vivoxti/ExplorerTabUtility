# Settings and tray regression checks

Run the persistence tests on Windows with the .NET 9 SDK:

```powershell
dotnet test tests/ExplorerTabUtility.Tests/ExplorerTabUtility.Tests.csproj
```

The tests use isolated temporary files and do not read or change your application settings. They cover hidden-icon/hotkey persistence, recovery, interrupted/failed writes, serialization failures, and concurrent saves.

The `Validate settings and tray fix` workflow also builds both supported application frameworks and uploads portable x64 builds.

## Manual tray checks

Use a separate Windows test account or back up your settings before testing an application build.

1. Configure and save an enabled `ToggleVisibility` hotkey, then enable **Hide tray icon**. Confirm the hotkey still opens and closes settings.
2. Restart the application. Confirm the icon remains hidden and the hotkey/preferences are retained.
3. In a test session, restart Windows Explorer using Task Manager. Confirm the hidden icon does not return, including after waiting more than seven seconds. Repeat with **Window Hook** disabled.
4. Disable **Hide tray icon**. Confirm the icon, context menu, and double-click action work. Repeat hiding/showing several times.
5. Restart Windows Explorer while the icon is visible. Confirm it returns and its menu still works.
6. Disable/remove the `ToggleVisibility` profile and save. Confirm the icon becomes accessible again, as required by the existing safety check.
7. Exit the application. Confirm it removes the tray icon and exits cleanly.
