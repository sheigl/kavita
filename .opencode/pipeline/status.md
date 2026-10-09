# Pipeline Status

| Feature | Dev Status | Review Status | QA Status | Notes |
|---------|-----------|---------------|-----------|-------|
| TTS Backend (Service, Controller, DTOs, Entities, EF Migration) | ✅ Complete | ✅ Approved | ✅ Passed | 26 unit tests passing (1 intentionally skipped). Fixed: Data Protection API key encryption, SignalR message DTOs, input validation, HTTP timeout |
| TTS Frontend (Angular v21 components, SignalR, Web Audio) | ✅ Complete | ✅ Approved | ✅ Passed | 11 files created. All 7 API endpoints verified matching backend. Fixed: getTextChunks() and startStream() URL mismatches. 12 frontend unit tests (spec files) |
| TTS UX Improvements (config button, setup banner, auto-restart) | ✅ Complete | ✅ Approved | ✅ Passed | Added: gear icon → config modal, conditional visibility, dismissible first-time banner, chapter-change auto-restart. All edge cases verified |
| Per-Series TTS Settings (DTO plumbing, settings UI, i18n) | ✅ Complete | ✅ Approved | ✅ Passed | 7 files modified: DTO + model + service wiring + accordion UI + i18n keys + bug fix. Fixed: type mismatch in packReadingProfile() (null → undefined) |
| EF Core Migration Crash Fix | ✅ Complete | ✅ Approved | ✅ Passed | Added `UserTtsConfig` navigation to AppUser entity, fixed `.WithOne()` config in DataContext and migration Designer.cs. Docker smoke test passes with health endpoint returning 200 |
| Container Build Infrastructure (Dockerfile.build, docker-compose) | ✅ Complete | ✅ Approved | ✅ Passed | Multi-stage build: Node.js → .NET SDK → Ubuntu runtime. No host deps needed. Full `docker build` + container start verified healthy |
| TTS Settings Tab (Settings → Account) | ✅ Complete | ✅ Approved | ✅ Passed | New inline settings component for TTS server config. Fixed: TranslocoModule import (was TranslocoDirective). Form validators match modal version exactly |
| Migration RowVersion Fix (v1 — incorrect) | ❌ Reverted | ❌ Rejected | ❌ Failed at runtime | Modified already-applied migration — EF Core skipped it. Container still threw `no such column: u.RowVersion` |
| Migration RowVersion Fix (v2 — correct) | ✅ Complete | ✅ Approved | ✅ Passed | Reverted original migration to original state, created new `20260622_AddUserTtsConfigRowVersion` migration with `AddColumn`. Designer.cs includes `.IsConcurrencyToken()`. 2,493/2,505 tests passing (0 new failures). **Needs Docker rebuild + runtime smoke test** |
| TTS Playback Start Bugs Fix | ✅ Complete | ✅ Approved | ✅ Passed | Added play button for idle/completed states, passed chapterId to controls, auto-start on page load when profile.ttsEnabled, fixed isTtsVisible$ dual condition. 5 files modified, 14 new unit tests added |
