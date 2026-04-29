# Microsoft Store publishing plan

Scope and ordered checklist to ship WPUService through the Microsoft Store.

The current build is a self-contained single-file Win32 exe with a
custom `%LOCALAPPDATA%` installer, `HKCU\Run` autostart, AUMID + Start
Menu shortcut written at first launch, Outlook COM interop, Win32
low-level input hooks, SQLite (native libs), and DPAPI-protected
secrets. All of that is compatible with the Store via the
**Desktop Bridge / `runFullTrust`** route, but several pieces of the
install-time bootstrapping have to move from runtime code into the
MSIX manifest, and a packaging project has to be added.

Estimated effort: **1–2 focused days of dev work**, plus 1–3 days of
Store certification wait time.

---

## Phase 1 — Accounts & external setup (before any code work)

- [ ] Pick **Individual ($19)** vs **Company ($99)** Partner Center account.
      Individual is fine to ship; company is required if listing under a
      legal entity name.
- [ ] Create the account at <https://partner.microsoft.com/dashboard>
      and complete tax / payout setup if planning to charge.
- [ ] **Reserve the app name** in Partner Center. Suggested:
      `Workstation Presence Utility`. The name **must not contain
      "Teams"** or imply Microsoft affiliation — that is the single most
      common rejection reason for apps in this category.
- [ ] **Host a privacy policy** at a public URL (GitHub Pages off the
      WPUService repo is the cheapest path). Required because the app
      reads notification content. Must mention: what data is read
      (notification text), where it's stored (locally, in
      `%LOCALAPPDATA%\Packages\<family>`), what is transmitted (alerts
      to the user-configured destination only), no telemetry.
- [ ] Decide pricing model: free / paid one-time / freemium. Pricing can
      be changed post-launch.

## Phase 2 — Repo changes (the actual work)

### 2a. Add the packaging project

- [ ] Add a **Windows Application Packaging Project** (`.wapproj`)
      to the solution, referencing `WPUService.csproj` as the entry app.
      (`dotnet new wapproj` is not provided by the SDK — easiest path is
      to create it in Visual Studio with the "Windows Application
      Packaging Project" template, or hand-author the .wapproj XML.)
- [ ] Set `<EntryPoint>` to `WPUService.exe` and target
      `<TargetPlatformVersion>10.0.19041.0</TargetPlatformVersion>` to
      match the existing csproj.

### 2b. `Package.appxmanifest`

- [ ] **Identity** — Name, Publisher, Version. The Publisher value
      must match the CN issued by Partner Center after name reservation.
- [ ] **Properties** — DisplayName, PublisherDisplayName, Logo (50x50).
- [ ] **Dependencies** — `MinVersion` 10.0.17763.0 to mirror the
      `<SupportedOSPlatformVersion>` already in the csproj.
- [ ] **Capabilities** — declare:
      - `runFullTrust` (under `rescap:`) — required for WinForms,
        Outlook COM, low-level input hooks, SQLite native libs.
      - `userNotificationListener` (under `uap:`) — required for
        reading Teams notifications.
- [ ] **Application / VisualElements** — DisplayName, Description,
      Logo (Square150x150), SmallLogo (Square44x44), Wide310x150Logo,
      SplashScreen, BackgroundColor.
- [ ] **StartupTask extension** — replaces the current `HKCU\Run`
      autostart. Inside `<Application><Extensions>`:
      ```xml
      <desktop:Extension Category="windows.startupTask"
                         Executable="WPUService.exe"
                         EntryPoint="Windows.FullTrustApplication">
        <desktop:StartupTask TaskId="WPUStartupTask"
                             Enabled="false"
                             DisplayName="Workstation Presence Utility" />
      </desktop:Extension>
      ```
      The user controls it via the Settings UI; we toggle it through
      the `StartupTask` WinRT API.

### 2c. Visual assets

- [ ] Produce a **1240×1240 master logo** (transparent PNG). The
      existing `icons/active.ico` can be the source if reworked at
      higher resolution.
- [ ] Generate the full Store asset set (Square44x44 in all scales,
      Square71x71, Square150x150, Square310x310, Wide310x150,
      SplashScreen 620x300, Store logo 50x50, plus 100/125/150/200/400
      scale variants). Easiest tool: **PWABuilder Image Generator** or
      Visual Studio's manifest "Generate" button on the Visual Assets
      tab.
- [ ] Generate **3–5 store screenshots** of the redesigned settings
      window. Required dimensions: 1366×768 minimum, 1920×1080 ideal.

### 2d. Code refactors (conditionally skip install-time bootstrapping when packaged)

The app currently runs install-time code on every cold start
(`Installer.IsRunningFromInstallLocation`, `EnsureShortcut`,
`EnsureAumidRegistration`, the install prompt in `Program.Main`). When
running packaged, MSIX provides install/uninstall, AUMID, and Start
Menu shortcut — that code must short-circuit.

- [ ] Add a helper:
      ```csharp
      internal static class Packaging
      {
          public static bool IsPackaged()
          {
              try { _ = Windows.ApplicationModel.Package.Current; return true; }
              catch { return false; }
          }
      }
      ```
      Requires adding the WindowsAppSDK / `Microsoft.Windows.SDK.NET`
      package or referencing `Windows.winmd` so the `Windows.ApplicationModel`
      types are available. Already partly present via the WinForms target.
- [ ] In `Program.Main`, gate the install prompt and the
      `IsRunningFromInstallLocation` check on `!Packaging.IsPackaged()`.
- [ ] In `TrayContext` constructor, gate `Installer.EnsureShortcut()`
      and `Installer.EnsureAumidRegistration()` on
      `!Packaging.IsPackaged()`.
- [ ] **Rewrite `Autostart.cs`** with a packaged code path that uses
      the WinRT `StartupTask` API:
      ```csharp
      // packaged:
      var task = await StartupTask.GetAsync("WPUStartupTask");
      bool enabled = task.State == StartupTaskState.Enabled;
      // toggle:
      if (enable) await task.RequestEnableAsync();
      else task.Disable();
      ```
      Keep the existing `HKCU\Run` path for the unpackaged build so the
      same source still produces a sideloadable exe.
- [ ] Confirm the `Installer.OnUninstallClicked` flow in
      `TrayContext` is hidden in the Settings form when packaged
      (Store handles uninstall via Apps & Features). Either hide the
      "Danger zone" section or replace the button with one that opens
      `ms-settings:appsfeatures-app`.
- [ ] Confirm `Config.ConfigDir` (`%APPDATA%\WPUService`) under MSIX
      gets virtualized to `%LOCALAPPDATA%\Packages\<family>\RoamingState\WPUService\`
      automatically — verify after first sideload, no code change needed.

### 2e. Build flags

- [ ] In the WPUService csproj when consumed by .wapproj:
      `<PublishSingleFile>false</PublishSingleFile>` and
      `<SelfContained>true</SelfContained>`. Single-file is the wrong
      shape for MSIX (the package wants individual files so MSIX's own
      compression and incremental update logic can work). Keep
      self-contained so users without the .NET runtime still launch.
- [ ] Confirm `Microsoft.Data.Sqlite`'s native `e_sqlite3.dll` ends up
      inside the package. Self-contained publish handles this; MSIX
      will pick it up from the publish output.

## Phase 3 — Sideload test (before submitting)

- [ ] Generate a self-signed dev certificate matching the manifest
      Publisher CN; install it into `LocalMachine\TrustedPeople`.
- [ ] Build the `.wapproj` in `Release|x64` to produce an `.msix`.
- [ ] Install on a **clean Windows 11 VM** (or a second user account):
      `Add-AppxPackage .\WPUService.msix`
- [ ] Smoke-test:
      - [ ] App launches, tray icon appears.
      - [ ] Notification access granted (or correctly prompts to grant).
      - [ ] Real Teams notification triggers detection.
      - [ ] StartupTask toggle from Settings actually persists across reboot.
      - [ ] Outlook send works (full-trust COM).
      - [ ] SMTP send works.
      - [ ] Pushover send works.
      - [ ] Config (`SmtpPasswordEncrypted`, `PushoverApiTokenEncrypted`)
            survives app restart (DPAPI under packaged identity).
      - [ ] Uninstall via Apps & Features removes everything cleanly.
- [ ] Specifically verify low-level keyboard/mouse hooks
      (`RealInputWatcher`) fire under MSIX — they should under
      `runFullTrust`, but this is the highest-risk piece if anything
      surprises us.

## Phase 4 — Store submission

- [ ] In Partner Center → Submissions, create the first submission.
- [ ] Upload the signed MSIX. Partner Center signs the final package
      with the Store cert; do not pre-sign with a production cert.
- [ ] Fill **Pricing & availability**: markets, price, free trial.
- [ ] Fill **Properties**: category (Productivity), supported
      languages, system requirements.
- [ ] Fill **Age ratings** questionnaire (this app: no objectionable
      content → expected G-equivalent).
- [ ] **Description** must:
      - Avoid "Teams" in the title.
      - Use "Microsoft Teams" only descriptively in the long description.
      - Include a clear disclaimer: *"Not affiliated with, endorsed by,
        or sponsored by Microsoft Corporation. Microsoft Teams is a
        trademark of Microsoft Corporation."*
      - Justify each restricted capability (`runFullTrust`,
        `userNotificationListener`) in the **Notes for certification**
        field. Cite the specific features that need them (Outlook COM
        for the Outlook send mode; notification listener for the core
        product functionality).
- [ ] Upload screenshots and store logo.
- [ ] **Submit**. Cert review usually returns in 24–72 hours.

## Phase 5 — Post-submission

- [ ] Address any certification failures (most likely category:
      capability justification, trademark phrasing, or visual asset
      sizing).
- [ ] Once live, decide on update cadence. Versioning rule: every
      submission must have a strictly greater `Version` in
      `Package.appxmanifest`.
- [ ] Set up CI (optional) to build the .wapproj and upload via the
      **Microsoft Store Submission API** so future releases don't
      require manual Partner Center clicking.

---

## Known risks / open questions

1. **`runFullTrust` review scrutiny.** Apps that need full trust get a
   slower, stricter cert pass. Justification is straightforward here
   (Outlook COM, low-level input hooks for accurate idle detection,
   native SQLite), but expect at least one round-trip with the cert team.
2. **Trademark.** "Teams" cannot be in the product name. Description
   wording must be careful. This is a real and common rejection reason.
3. **Notification listener consent.** The Store version may behave
   differently from the sideloaded version with respect to first-run
   consent dialogs — verify in Phase 3 on a clean machine.
4. **Single-instance mutex.** The current `Global\WPUService.SingleInstance`
   mutex name is fine under MSIX; just confirm during sideload that
   double-launch is suppressed.
5. **Update path for existing sideloaded users.** Anyone running the
   current `%LOCALAPPDATA%\WPUService\WPUService.exe` build won't
   auto-migrate. Document a one-time uninstall-old + install-new
   step, or have the Store-packaged build detect and clean up the old
   install location on first run.

## What's done vs. what's pending

- **Done**: app code itself is functionally complete; capabilities map
  cleanly onto Store-allowed APIs; DPAPI / SQLite / Outlook / SMTP /
  Pushover paths are all Store-compatible under `runFullTrust`.
- **Pending**: everything in Phases 1–4 above.
