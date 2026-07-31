# Changelog

All notable changes to **OpenDefinery for Revit** are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.3.0] - 2026-08-01

### Added
- **Family Editor add-in** (`OpenDefinery-FamEditor`) — a dedicated Revit
  command on the Triple Zero Labs tab for editing the open family:
  - Family types in a left sidebar; parameters in a grid grouped by
    parameter group (collapsible), with search and data-type / group /
    binding filters and a "Show Groups" toggle.
  - Inline value editing shown and parsed in the project's units — enter a
    value in any unit (e.g. `1.5 m`) and Revit converts it.
  - Yes/No parameters as tri-state checkboxes.
  - Formula editor: the ƒ(x) button turns green when a formula is set, the
    value shows a `<CALCULATED>` marker, and clicking the value cell opens
    the editor.
  - Convert a family parameter to an OpenDefinery **shared** parameter, and
    **swap** a shared parameter for another of the same data type, both
    driven by a Collection picker.
  - Toggle a parameter between **Instance** and **Type** binding.
  - Add a family's shared parameters to a Collection, with an undo toast.

### Changed
- Product version bumped to **0.3.0** across the installer and both add-ins.
- All dialogs in the Family Editor use OS2-styled modal windows instead of
  the stock Windows `MessageBox`.
- The Instance/Type toggle is **blocked when a formula would be lost** (the
  parameter has a formula, or another parameter's formula references it),
  and the dropdown reverts to its real value instead of leaving a partial
  change. Toggling proceeds only when no formula is at stake.
- Formula-rejection errors are now readable, stripping Revit's internal
  argument text and adding hints (name spelling, unit compatibility,
  instance-vs-type references).
- The Family Editor command was removed from the Parameter Manager add-in
  (it is now its own add-in).

### Fixed
- **Empty Collection parameters after sign-in.** Signing in from the Family
  Editor only set the auth tokens, leaving `DataTypes` and
  `PublishedCollections` unpopulated. `Collection.GetParameters` then threw
  and the exception was swallowed, so Make Shared and Swap showed nothing.
  Sign-in now hydrates the session; `Collection.GetFromString` also guards
  against a null published-collections lookup so it can never wipe the list.
- **Swap / Add-to-Collection type mismatch.** Data types are now resolved
  across every representation (OpenDefinery id, display name, and Revit
  ParameterType name), so the family's shared-parameter code (e.g. `TEXT`)
  matches the Collection's stored type.
- **Family Editor crash on open.** The "Show Groups" checkbox fired its
  handler during window construction, before the grid existed, causing a
  null-reference (`ParamGrid`); the view rebuild now no-ops until the window
  is ready.

### Security
- No credentials, signing metadata, or keys are committed; `signing-metadata.json`,
  `*.pfx`, and build output remain ignored.

## [0.1.0]

### Added
- Initial OpenDefinery for Revit release: the Parameter Manager add-in
  (migrated from the desktop app), multi-version support for Revit
  2022–2027, the shared OS2 WPF theme, Azure Trusted Signing of the build
  output, and the Inno Setup installer / deploy pipeline.

> Note: 0.2.0 was assigned internally but never released; 0.1.0 goes
> straight to 0.3.0.

[0.3.0]: https://github.com/TripleZeroLabs/OpenDefinery-RevitAddins/releases/tag/v0.3.0
[0.1.0]: https://github.com/TripleZeroLabs/OpenDefinery-RevitAddins/releases/tag/v0.1.0
