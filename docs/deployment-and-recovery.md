# Deployment, shell mode, and recovery

## Current deployment level

The current Cerebrum build is a compatibility-mode desktop application. It can be started and stopped like any other Windows application and makes no persistent shell changes.

This is the only supported mode until the recovery and notification-area work described here is implemented and tested.

## Deployment stages

### Stage 1: developer build

Purpose:

- compile and debug Cerebrum plus the independently runnable component repositories;
- run Cerebrum alongside Explorer;
- validate monitors, component discovery, supervision, and desktop actions.

Properties:

- framework-dependent;
- unsigned;
- manually started;
- Explorer remains visible;
- no installer or registry changes.

### Stage 2: compatibility installer

Purpose:

- install signed releases in stable per-machine or per-user locations;
- register ordinary logon startup;
- keep Explorer as the registered shell.

Required behavior:

- install versioned Cerebrum, Wallpaperbank, Parietal, Medulla, Thalamus, Cortex, and—once runnable—Snip artifacts;
- write no development repository paths;
- verify signatures before activation;
- preserve settings across upgrades;
- provide repair and uninstall;
- start Cerebrum after Explorer;
- never restart Explorer during routine installation.

### Stage 3: opt-in shell test mode

Purpose:

- allow a tester to enter a Cerebrum-owned session without relying on the Explorer taskbar or desktop surface.

This stage requires a separate signed deployment/shell supervisor. Cerebrum.Host.exe itself must not silently edit shell settings.

Preflight requirements:

- all required binaries exist and have accepted signatures;
- Wallpaperbank can create its wallpaper surface;
- Parietal can create its top status/global-menu surface;
- Medulla can create its dock surfaces;
- Cerebrum can create a desktop on every active monitor;
- the private broker passes health and capability checks;
- the known-good Windows Explorer path exists;
- the user has acknowledged the recovery procedure;
- crash-loop state is clear;
- a timed first-boot confirmation is armed.

Recovery requirements:

- a documented keyboard route launches the recovery tool;
- a timeout returns the next sign-in to Explorer unless the user confirms success;
- repeated early Cerebrum exits restore Explorer for the next sign-in;
- Safe Mode remains untouched;
- an administrator can repair another affected profile;
- disabling shell mode does not remove user data;
- every state transition is logged without personal content.

Explorer should be retained on disk and available as a recovery shell. Cerebrum is not an executable replacement for the Windows explorer.exe file.

## Private test image

It is technically possible to produce a bootable Cerebrum test ISO for virtual machines and later spare hardware.

The safe image is not a fork of Windows. It consists of:

1. an official Windows Evaluation image or a Windows ISO supplied under the tester's license;
2. an unattended Windows setup answer file;
3. signed Cerebrum component packages;
4. a post-setup provisioning package or script;
5. recovery tooling and validation;
6. optional shell-mode enablement after compatibility-mode preflight.

Typical Microsoft deployment tooling includes Windows ADK, DISM, Windows PE where needed, and ISO-generation tooling. Exact commands and image versions should be documented only when the deployment repository is created, because they depend on the licensed source image and current Windows servicing requirements.

## Image pipeline policy

Inputs must be explicit and reproducible:

- source ISO path and digest;
- Windows edition and architecture;
- ADK/tooling version;
- component versions and artifact digests;
- signing-certificate identity;
- answer-file version;
- provisioning version;
- output-image digest.

The pipeline must never commit or publish Microsoft installation files in the Cerebrum source repositories.

Outputs should be separated:

- private VM test ISO;
- component bundle;
- checksums and software bill of materials;
- build log without product keys or personal paths.

Do not embed product keys, developer certificates, access tokens, or user settings.

## First image boot

Recommended order:

1. Windows setup completes normally.
2. Windows reaches a standard Explorer desktop.
3. Provisioning verifies Windows servicing health.
4. Signed Cerebrum components install.
5. Compatibility mode launches and runs automated health checks.
6. The tester validates display, input, taskbar, file manager, and recovery controls.
7. Shell test mode becomes available as an explicit option.
8. The next sign-in enters shell mode with a confirmation timer.
9. Failure or lack of confirmation returns the next sign-in to Explorer.

An image should never enter shell mode on its first boot without proving that the recovery path works.

## Crash-loop policy

Persist only non-sensitive boot health:

- attempted session version;
- startup timestamp;
- time until healthy;
- clean-shutdown marker;
- early-exit count;
- rollback reason code.

Suggested state transitions:

    Explorer compatibility
          │ successful preflight + explicit consent
          ▼
    Cerebrum pending confirmation
          │ healthy confirmation
          ▼
    Cerebrum confirmed
          │ repeated early failure
          ▼
    Explorer recovery

The deployment supervisor—not Wallpaperbank, Parietal, Medulla, Cortex, or Snip—owns this state.

## Hardware test progression

1. Hyper-V or another disposable virtual machine.
2. VM snapshots across install, upgrade, rollback, and uninstall.
3. Multiple virtual displays and resolution changes.
4. Windows update and component upgrade cycles.
5. RDP, lock/unlock, suspend/resume where supported.
6. Spare physical machine with a separate administrator recovery account.
7. Heterogeneous-DPI and GPU-vendor hardware.
8. Only after those gates, an everyday workstation opt-in.

Never use an only workstation as the first shell-mode test machine.

## Release requirements

Before distributing a test image or shell-mode installer:

- code-sign every executable and installer;
- produce hashes and a software bill of materials;
- scan release artifacts;
- publish exact uninstall and recovery steps;
- test standard-user operation;
- test UAC and secure desktop transitions;
- test Windows Update before and after installation;
- verify that uninstall restores ordinary Explorer startup;
- perform repeated install, upgrade, rollback, and uninstall cycles;
- review Microsoft licensing for the intended distribution model.

A user-supplied-ISO builder or provisioning bundle is generally a cleaner distribution shape than redistributing a modified Windows ISO. Licensing decisions should be reviewed for the actual audience and edition before release.

## Prohibited deployment behavior

- Replacing, patching, or renaming explorer.exe.
- Removing Explorer from the Windows image.
- Modifying WindowsApps directly.
- Disabling UAC, Defender, Secure Boot, or Windows Update as a convenience.
- Storing a plaintext product key.
- Entering shell mode without consent and recovery.
- Restarting all shell processes in response to a single component failure.
- Treating a successful package install as proof that the user-facing shell cache is already refreshed.
