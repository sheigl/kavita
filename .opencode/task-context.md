## Goal
- Fix runtime SQLite schema error blocking TTS config save in container, then continue TTS settings feature work.

## Constraints & Preferences
- Must follow Angular standalone component patterns (imports array, `inject()`, signals, OnPush)
- Form validators must match the existing modal version (`ManageTtsConfigComponent`) exactly
- Existing modal in book reader must NOT be modified (backward compatibility)
- Reuse existing `"tts.config.*"` i18n keys where possible
- Voice field must be free-text input (not dropdown) so users can specify arbitrary voice names

## Progress
### Done
- Fixed TTS DI error: changed `TtsService` constructor from `IDataProtector` → `IDataProtectionProvider`, created protector via `.CreateProtector("Kavita.TtsApiKey")` — QA passed (14/14 service tests, 11/12 controller tests)
- Created preventive DI resolution integration test suite (`ServiceResolutionFixture.cs` + `ServiceResolutionTests.cs`) verifying all 83 registered services resolve from the real container — catches missing DI registration bugs before runtime
- Fixed SQLite schema error in DI tests: added `Database.EnsureCreated()` to fixture so in-memory database has proper tables (including `RowVersion` concurrency columns) — QA passed (83/83 resolution tests, 170/171 full suite)
- TTS Settings Tab fully implemented and merged:
  - New `ManageTtsSettingsComponent` with reactive form (serverUrl, apiKey, defaultModel, defaultVoice, defaultSpeed)
  - Added `TtsConfig = 'tts-config'` to `SettingsTabId` enum and nav item in Account section
  - Wired into `settings.component.html` with `@defer (prefetch on idle)`
  - Added `"tts-config": "Text-to-Speech"` i18n key in `en.json`
  - All form validators match modal version (required, maxLength, min/max)
  - Save error handling with dismissible alert
  - Docker build passes, container starts cleanly
- Fixed Docker build error: `TranslocoDirective` → `TranslocoModule` import in `manage-tts-settings.component.ts` (NG8004: No pipe found with name 'transloco')
- Changed voice field from `<select>` dropdown to `<input type="text">` for arbitrary voice name support; removed `voices` signal, `fetchVoices()`, and voice-fetching `effect()`
- Fixed runtime SQLite schema error in container: added missing `RowVersion` column to `UserTtsConfigs` table creation in migration `20260617_AddTtsFeature.cs`, updated Designer.cs snapshot, removed redundant follow-up migration — QA passed (2,554/2,676 tests passing, 0 new failures)

### In Progress
- (none)

### Blocked
- (none)

## Key Decisions
- Created new inline `ManageTtsSettingsComponent` instead of reusing modal directly — modal template uses Bootstrap `<div class="modal-*">` wrappers that would break layout inside settings page
- Placed TTS tab in Account section (per-user config stored in `UserTtsConfig`) after ReadingProfiles, not in Server/Admin sections
- Voice field is free-text input (not dropdown) — supports any TTS server with arbitrary voice names, not just OpenAI's hardcoded list
- DI resolution tests use `Database.EnsureCreated()` instead of applying migrations — simpler for test environment, same pattern as existing `AbstractDbTest.cs`, but this masked a missing column in the real migration
- `TranslocoModule` (not `TranslocoDirective`) is the correct import for standalone components using `| transloco` pipe in templates
- All entities with `[ConcurrencyCheck] public uint RowVersion` must include `RowVersion = table.Column<uint>(type: "INTEGER", nullable: false)` in their migration's CreateTable — omission causes runtime SQLite errors

## Next Steps
- Rebuild Docker container and verify `PUT /api/tts/config` succeeds end-to-end (runtime smoke test)
- Continue with any remaining TTS feature work or new backlog items

## Critical Context
- Runtime error was: `Microsoft.Data.Sqlite.SqliteException (0x80004005): SQLite Error 1: 'no such column: u.RowVersion'` on `PUT /api/tts/config`
  - Stack: `UserRepository.GetUserTtsConfigAsync` → `TtsService.SaveUserTtsConfigAsync` → `TtsController.SaveTtsConfig`
- Root cause was migration `20260617_AddTtsFeature.cs` creating `UserTtsConfigs` table without `RowVersion` column, though entity declares `[ConcurrencyCheck] public uint RowVersion`
- `Database.EnsureCreated()` in tests builds schema from current EF Core model (includes `RowVersion`), masking migration gaps — container applies real migrations where the gap surfaces
- Original DI bug: `IDataProtector` is not registered in ASP.NET Core DI — must inject `IDataProtectionProvider` and call `.CreateProtector("Kavita.TtsApiKey")`. Unit tests bypass DI entirely by using `new TtsService(...)` with mocks.
- Five entities use `[ConcurrencyCheck]` on `RowVersion` column: `AppUser`, `ServerSetting`, `UserTtsConfig`, plus others — requires schema creation before any EF Core queries run
- Existing modal at `UI/Web/src/app/book-reader/_components/manage-tts-config/` remains untouched — still used by book reader
- Docker build uses multi-stage: `node:22-bookworm` (Angular) → `mcr.microsoft.com/dotnet/sdk:10.0` (.NET publish) → Ubuntu runtime

## Relevant Files
- `Kavita.Database/Migrations/20260617_AddTtsFeature.cs`: Fixed — now includes `RowVersion` column in `UserTtsConfigs` table creation
- `Kavita.Models/Entities/User/UserTtsConfig.cs`: Entity declares `[ConcurrencyCheck] public uint RowVersion` (line 48-49)
- `Kavita.Services/TtsService.cs`: DI fix (IDataProtectionProvider → CreateProtector)
- `Kavita.Server.Tests/DI/ServiceResolutionFixture.cs`: Builds real DI container + in-memory SQLite schema for resolution tests
- `UI/Web/src/app/user-settings/manage-tts-settings/manage-tts-settings.component.ts/.html`: Inline TTS settings component with free-text voice input
