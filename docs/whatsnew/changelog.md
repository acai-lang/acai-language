# Acai Changelog

## Version 0.1
- Added `show`, `set`, and friendly condition syntax.
- Added `if/then/else/end` and `else if` support.
- Added `repeat while/until`, `for from ... to ...`, and `continue`/`stop`.
- Added imports via `use` and classes via `make class`.
- Added built-in `call input(...)` and string interpolation.

## Version 0.2.0-beta.1 (beta)
- Introduced a secure updater and fresh-install workflow for channel-based installs (`stable` / `beta`).
- Added staged patch application and binary assembly to safely update running executables.
- Added `upgrade-matrix.txt` support: plain `key=value` manifests for chained patches and channel tags.
- Platform-aware installers/extractors: Windows `.exe`, macOS `.pkg`, Linux `.tar.gz` are supported for fresh installs.
- Improvements: `metadata/VERSION` is now used as the single source of truth for local versioning; docs updated with upgrade instructions.

### Notes (beta)
- This is a beta release. Installer flows may require elevation on some platforms; test in a safe environment first.
- If you publish patch deltas (bsdiff), ensure a compatible patch tool/package is available for the updater to apply them.

## Notes
The changelog is kept short and focused on the latest feature set.
