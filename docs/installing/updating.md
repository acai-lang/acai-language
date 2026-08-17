# Updating Acai

This page explains the `update` command and the automatic upgrader behavior in simple steps.

**Quick CLI usage**

- Fresh install (channel):

  - Stable channel:

    dotnet run update --channel stable

  - Beta channel:

    dotnet run update --channel beta

The command downloads and runs the platform-specific installer for the selected channel.

**What the upgrader does**

- Reads your local version from the `metadata/VERSION` file (searches parent folders).
- Downloads a small plain-text upgrade matrix hosted in the repository (`upgrade-matrix.txt`).
- If a fresh install is requested, it downloads the OS-specific release asset for the channel and runs the platform installer/extractor.
- If patch-based updates are used, the upgrader applies chained staged patches, assembling a final binary into the user update folder, then swaps it into place.

**Matrix file format (simple)**

The project expects a plain text file with `key=value` pairs, one per line. Lines starting with `#` are ignored. Example:

```
# latest stable and beta tags
latest=1.3.0
latest-beta=1.4.0-beta.2

# chained patches
patch-1.0.0-to-1.1.0=https://raw.githubusercontent.com/acai-lang/acai-language/main/patches/patch-1.0.0-to-1.1.0.bsdiff
patch-1.1.0-to-1.3.0=https://raw.githubusercontent.com/acai-lang/acai-language/main/patches/patch-1.1.0-to-1.3.0.bsdiff
```

- The `latest` key is used by default; `latest-beta` is used when installing the `beta` channel.
- Patches must be named with the `patch-{from}-to-{to}` pattern so the upgrader can chain them.

**Where files are stored during upgrade**

- Windows: `%LocalAppData%\Acai\update`
- macOS: `~/Library/Application Support/Acai/update`
- Linux: `~/.local/share/Acai/update`

The upgrader stages intermediate files here and removes them after successful installation.

**Permissions and elevation**

- Some installer steps (macOS `installer`, Windows system installs) may require elevated privileges. The updater does not escalate automatically — if an install fails with a permission error, run the suggested command with elevated privileges or use the OS installer manually.

**Safety notes**

- The upgrader avoids overwriting the running executable directly; it assembles a staged binary and performs a safe swap.
- The tool does not upload or expose any private data.

**Troubleshooting**

- If the updater cannot find `metadata/VERSION`, it falls back to a default version and will attempt upgrades. Create `metadata/VERSION` containing the current version string to ensure correct behavior.
- If a patch fails to apply, the upgrader will stop and leave the downloaded files in the update folder for inspection.

**Example matrix snippet**

```
latest=1.3.0
latest-beta=1.4.0-beta.2
patch-1.2.0-to-1.3.0=https://github.com/acai-lang/acai-language/releases/download/v1.3.0/patch-1.2.0-to-1.3.0.bsdiff
```

That's it — simple, safe upgrade behavior for all supported platforms.
