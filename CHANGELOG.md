# Changelog

All notable changes to this project are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-11-23

### Changed

- The first stable release. The rules are unchanged from 0.1.0-preview.1.

## [0.1.0-preview.1] - 2026-11-20

### Added

- TRA0001 rejects every null-forgiving operator; `#pragma`, `<NoWarn>` and `.editorconfig` cannot switch it off.
- TRA0002 rejects a `[SuppressMessage]` or `[UnconditionalSuppressMessage]` naming TRA0001, TRA0003, TRA0004 or
  TRA0005.
- TRA0003 rejects switching the compiler's null checks off in source: `#nullable disable`, a `#pragma warning
  disable` of a nullable warning, `nullable` or everything, or an `.editorconfig` severity lowering one.
- TRA0004 rejects switching them off for the project: `Nullable` other than `enable`, warning level 0, `NoWarn`,
  `WarningsNotAsErrors` or a global config lowering a nullable warning.
- TRA0005 rejects `[AllowNull]` or `[NotNull]` on a field or auto-property.
