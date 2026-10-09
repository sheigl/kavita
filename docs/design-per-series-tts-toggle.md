# Design: Per-Series TTS Toggle Feature

## Overview
Adds a "Text-to-Speech" accordion section to the EPUB reader settings panel, letting users enable/disable TTS playback and override voice/speed on a per-series basis. Settings persist via the existing `AppUserReadingProfile` → `UserReadingProfileDto` → Angular `ReadingProfile` pipeline.

## Architecture Decisions

1. **Decision**: Add TTS fields to existing `UserReadingProfileDto` rather than creating new endpoints.
   - **Why**: The reading profile API already handles per-series CRUD with debounced auto-save. Adding 3 primitive fields to the DTO is zero-cost plumbing — AutoMapper maps them automatically, and the frontend form pipeline already exists.

2. **Decision**: Place TTS controls in the reader settings panel (accordion item) rather than a separate modal.
   - **Why**: Consistent with Kavita's established pattern for per-series settings. Users expect all series-specific reading options in one place.

3. **Decision**: Gate `isTtsVisible$` on both `serverUrl` AND `profile.ttsEnabled`.
   - **Why**: Prevents showing TTS playback controls when the user has explicitly disabled TTS for this series, even if they have a server configured globally.

4. **Trade-offs considered**: 
   - **Separate API endpoint for just TTS fields**: Rejected — would require new controller methods, new DTO, and duplicate debounced-save logic. The existing reading profile pipeline handles it with zero backend changes.
   - **Inline near TTS controls (gear button)**: Rejected — the reader settings panel is already the canonical place for per-series configuration; a separate modal would fragment the UX.

## Files to Create/Modify

### Modified Backend Files
| File | Changes | Reason |
|------|---------|--------|
| `Kavita.Models/DTOs/UserReadingProfileDto.cs` | Add `TtsEnabled`, `TtsVoiceOverride`, `TtsSpeedOverride` properties in new `#region TtsReader` block | Expose entity columns over the API. AutoMapper maps them automatically (same-named primitive/nullable fields). |

### Modified Frontend Files
| File | Changes | Reason |
|------|---------|--------|
| `UI/Web/src/app/_models/preferences/reading-profiles.ts` | Add `ttsEnabled`, `ttsVoiceOverride?`, `ttsSpeedOverride?` to `ReadingProfile` interface | TypeScript model must match DTO shape for API round-trip |
| `UI/Web/src/app/_services/epub-reader-settings.service.ts` | Add TTS signal, form controls, load/save logic in `setupDefaultsFromProfile()`, `setupSettingsForm()`, `packReadingProfile()` | Wire TTS fields into existing debounced auto-save pipeline |
| `UI/Web/src/app/book-reader/_components/reader-settings/reader-settings.component.html` | Add new `<div ngbAccordionItem id="tts-panel">` section after color panel, before "Update Parent" buttons | Render toggle + voice dropdown + speed slider in settings panel |
| `UI/Web/src/app/book-reader/_components/reader-settings/reader-settings.component.ts` | Inject `TtsService`, expose `ttsVoices$` observable for template | Provide voice list data to the TTS accordion section |
| `UI/Web/src/app/book-reader/_components/book-reader/book-reader.component.ts` | Update `isTtsVisible$` to combine `config?.serverUrl` AND `profile?.ttsEnabled` | Hide/show playback controls based on per-series toggle state |
| `UI/Web/src/assets/langs/en.json` | Add 7 new i18n keys under `"reader-settings"` section | Labels for TTS accordion UI elements |

### No New Files Required
All changes extend existing components and services.

## Data Flow

```
┌─────────────────────────────────────────────────────────────┐
│ LOAD (book open)                                            │
│                                                             │
│  GET /api/reading-profile/{libraryId}/{seriesId}            │
│    → UserReadingProfileDto (includes TTS fields via DTO)    │
│    → Angular ReadingProfile model                           │
│    → EpubReaderSettingsService.initialize()                 │
│      → setupDefaultsFromProfile() reads profile.ttsEnabled  │
│      → setupSettingsForm() creates form controls            │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ SAVE (user changes settings)                                │
│                                                             │
│  Form control change                                        │
│    → debounceTime(500ms)                                    │
│    → updateImplicitProfile()                                │
│      → packReadingProfile() includes TTS fields             │
│      → POST /api/reading-profile/series                     │
│        (full UserReadingProfileDto with TTS fields)         │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ PLAYBACK GATE                                               │
│                                                             │
│  isTtsVisible$ = combineLatest([                            │
│    ttsService.getConfig(),                                  │
│    readerSettingsService.currentReadingProfile,             │
│  ]).pipe(                                                   │
│    map(([config, profile]) =>                               │
│      !!(config?.serverUrl && profile?.ttsEnabled))          │
│  )                                                          │
│                                                             │
│  → true: shows <app-tts-controls> in bottom bar             │
│  → false: hides controls (banner shown if no server URL)    │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ STREAMING (backend already handles this)                    │
│                                                             │
│  TtsService.StreamAsync()                                   │
│    → reads profile.TtsEnabled                               │
│    → reads profile.TtsVoiceOverride                         │
│    → reads profile.TtsSpeedOverride                         │
│    → fallback chain: request → profile → user default       │
└─────────────────────────────────────────────────────────────┘
```

## Detailed Implementation Plan

### Task 1: Backend — Add TTS fields to UserReadingProfileDto

**File**: `Kavita.Models/DTOs/UserReadingProfileDto.cs`

Add after the `#region PdfReader` block (before closing brace):

```csharp
#region TtsReader

/// <inheritdoc cref="AppUserReadingProfile.TtsEnabled"/>
public bool TtsEnabled { get; set; } = false;

/// <inheritdoc cref="AppUserReadingProfile.TtsVoiceOverride"/>
public string? TtsVoiceOverride { get; set; }

/// <inheritdoc cref="AppUserReadingProfile.TtsSpeedOverride"/>
public float? TtsSpeedOverride { get; set; }

#endregion
```

**Why no AutoMapper change needed**: The existing `CreateMap<AppUserReadingProfile, UserReadingProfileDto>()` only has one custom mapping (`BookReaderThemeName`). All other fields map by convention (same property names). Since the entity and DTO properties share identical names and compatible types (`bool`, `string?`, `float?`), AutoMapper handles them automatically.

**No controller changes needed**: The existing endpoints already serialize/deserialize the full `UserReadingProfileDto`. Adding new properties is backward-compatible — older clients simply won't send those fields, and they'll default to their entity defaults.

### Task 2: Frontend Model — Add TTS fields to ReadingProfile interface

**File**: `UI/Web/src/app/_models/preferences/reading-profiles.ts`

Add after the PDF Reader section in the `ReadingProfile` interface:

```typescript
  // TTS Reader
  ttsEnabled: boolean;
  ttsVoiceOverride?: string;
  ttsSpeedOverride?: number;
```

### Task 3: Frontend Service — Wire TTS into EpubReaderSettingsService

**File**: `UI/Web/src/app/_services/epub-reader-settings.service.ts`

Three changes needed:

**A. Add to form group type** (`BookReadingProfileFormGroup`):
```typescript
export type BookReadingProfileFormGroup = FormGroup<{
  // ... existing fields ...
  bookReaderDisableBookmarkIcon: FormControl<boolean>;
  // New TTS controls:
  ttsEnabled: FormControl<boolean>;
  ttsVoiceOverride: FormControl<string | null>;
  ttsSpeedOverride: FormControl<number | null>;
}>
```

**B. In `setupDefaultsFromProfile()`**: Add after existing signal setup:
```typescript
this._ttsEnabled.set(profile.ttsEnabled ?? false);
```

**C. In `setupSettingsForm()`**: Add to form group creation:
```typescript
bookReaderDisableBookmarkIcon: this.fb.control(profile.bookReaderDisableBookmarkIcon),
// New TTS controls:
ttsEnabled: this.fb.control(profile.ttsEnabled ?? false),
ttsVoiceOverride: this.fb.control<string | null>(profile.ttsVoiceOverride ?? null),
ttsSpeedOverride: this.fb.control<number | null>(profile.ttsSpeedOverride ?? null),
```

**D. In `packReadingProfile()`**: Add before return:
```typescript
data.ttsEnabled = this._ttsEnabled();
data.ttsVoiceOverride = this.settingsForm.get('ttsVoiceOverride')?.value ?? null;
data.ttsSpeedOverride = this.settingsForm.get('ttsSpeedOverride')?.value ?? null;
```

**Note**: No separate signal subscription needed for TTS fields — the existing `settingsForm.valueChanges` pipeline (debounced 500ms → `updateImplicitProfile()`) already handles persistence. The voice dropdown and speed slider are form controls, not signals, so they flow through the same path as other settings.

### Task 4: Frontend UI — Add TTS accordion section to reader settings panel

**File**: `UI/Web/src/app/book-reader/_components/reader-settings/reader-settings.component.html`

Insert new accordion item after the color-panel (`id="color-panel"`) and before the "Update Parent" buttons row:

```html
<div ngbAccordionItem id="tts-panel" [title]="t('tts-settings-title')" [collapsed]="true">
  <h2 class="accordion-header" ngbAccordionHeader>
    <button class="accordion-button" ngbAccordionButton type="button"
            [attr.aria-expanded]="acc.isExpanded('tts-panel')">
      {{t('tts-settings-title')}}
      <i class="fas fa-chevron-up" aria-hidden="true"></i>
    </button>
  </h2>
  <div ngbAccordionCollapse>
    <div ngbAccordionBody>
      <ng-template>
        <!-- Enable/Disable Toggle -->
        <div class="controls d-flex justify-content-between align-items-center">
          <label for="tts-enabled" class="form-label">{{t('tts-enable-label')}}</label>
          <div class="form-check form-switch">
            <input type="checkbox" id="tts-enabled" formControlName="ttsEnabled"
                   class="form-check-input" switch>
            <label>{{settingsForm.get('ttsEnabled')?.value ? t('on') : t('off')}}</label>
          </div>
        </div>

        <!-- Voice Override (conditional) -->
        @if (settingsForm.get('ttsEnabled')?.value) {
          <div class="controls mt-3">
            <label for="tts-voice" class="form-label">{{t('tts-voice-label')}}</label>
            <select id="tts-voice" formControlName="ttsVoiceOverride" class="form-select">
              <option [ngValue]="null">{{t('tts-voice-default')}}</option>
              @for(voice of (ttsVoices$ | async); track voice.id) {
                <option [value]="voice.id">{{voice.name}}</option>
              }
            </select>
          </div>

          <!-- Speed Override -->
          <div class="row g-0 controls">
            <label for="tts-speed" class="form-label col-6">{{t('tts-speed-label')}}</label>
            <span class="col-6 float-end" style="display: inline-flex;">
              {{t('tts-speed-min')}}
              <input type="range" class="form-range ms-2 me-2" id="tts-speed"
                     min="0.5" max="4.0" step="0.1" formControlName="ttsSpeedOverride"
                     [ngbTooltip]="settingsForm.get('ttsSpeedOverride')?.value + 'x'">
              {{t('tts-speed-max')}}
            </span>
          </div>
        }
      </ng-template>
    </div>
  </div>
</div>
```

**File**: `UI/Web/src/app/book-reader/_components/reader-settings/reader-settings.component.ts`

Add imports and inject TtsService:
```typescript
import {AsyncPipe} from '@angular/common';
// ... existing imports ...
import {TtsService} from '../../_services/tts-service';
import {Observable} from 'rxjs';
```

In the component class, add:
```typescript
private readonly ttsService = inject(TtsService);
ttsVoices$: Observable<TtsVoiceDto[]> = this.ttsService.getVoices();
```

Add `AsyncPipe` and `TtsVoiceDto` to imports/protected fields as needed.

### Task 5: Frontend — Update isTtsVisible$ in BookReaderComponent

**File**: `UI/Web/src/app/book-reader/_components/book-reader/book-reader.component.ts`

Replace current `isTtsVisible$`:
```typescript
// Before:
isTtsVisible$: Observable<boolean> = this.ttsService.getConfig().pipe(
  tap(config => { ... }),
  map(config => !!config?.serverUrl),
  takeUntilDestroyed(this.destroyRef),
);

// After:
isTtsVisible$: Observable<boolean> = combineLatest([
  this.ttsService.getConfig(),
  this.readerSettingsService.currentReadingProfile,
]).pipe(
  tap(([config]) => {
    if (config?.serverUrl) {
      this.ttsBannerDismissed.set(true);
    }
  }),
  map(([config, profile]) => !!(config?.serverUrl && profile?.ttsEnabled)),
  takeUntilDestroyed(this.destroyRef),
);
```

Add `combineLatest` to imports from 'rxjs'.

### Task 6: i18n — Add TTS settings labels

**File**: `UI/Web/src/assets/langs/en.json`

Add under `"reader-settings"` section (before the closing brace):

```json
"tts-settings-title": "Text-to-Speech",
"tts-enable-label": "Enable TTS for this series",
"tts-voice-label": "Voice Override",
"tts-voice-default": "Use default",
"tts-speed-label": "Playback Speed",
"tts-speed-min": "0.5x",
"tts-speed-max": "4x"
```

## Data Models / Interfaces

### Backend DTO Addition
```csharp
// UserReadingProfileDto.cs — new region
#region TtsReader
public bool TtsEnabled { get; set; } = false;
public string? TtsVoiceOverride { get; set; }
public float? TtsSpeedOverride { get; set; }
#endregion
```

### Frontend Interface Addition
```typescript
// reading-profiles.ts — additions to ReadingProfile interface
ttsEnabled: boolean;
ttsVoiceOverride?: string;
ttsSpeedOverride?: number;
```

## Testing Strategy

### Backend Unit Tests (xUnit + NSubstitute)
**File**: `Kavita.Services.Tests/ReadingProfileServiceTests.cs` (or new test file)

- **Test**: `UserReadingProfileDto_ContainsTtsFields` — Verify DTO has the three TTS properties with correct defaults (`false`, `null`, `null`)
- **Test**: `AutoMapper_MapsTtsFieldsFromEntity` — Create entity with TTS values, map to DTO, verify all three fields transfer correctly

### Frontend Unit Tests (Jasmine/Karma or Angular Testing)
**File**: New test spec for `EpubReaderSettingsService`

- **Test**: `packReadingProfile includes ttsEnabled from signal` — Set `_ttsEnabled`, call pack, verify DTO field
- **Test**: `packReadingProfile includes ttsVoiceOverride from form control` — Set form value, call pack, verify DTO field
- **Test**: `setupSettingsForm creates TTS controls with correct defaults` — Initialize service, verify form has `ttsEnabled`, `ttsVoiceOverride`, `ttsSpeedOverride` controls

### Frontend Component Tests
**File**: New test spec for `ReaderSettingsComponent`

- **Test**: `TTS accordion renders when reading profile is present` — Verify `#tts-panel` exists in DOM
- **Test**: `Voice dropdown and speed slider hidden when ttsEnabled=false` — Toggle off, verify conditional sections are absent
- **Test**: `Voice dropdown and speed slider visible when ttsEnabled=true` — Toggle on, verify they appear

### Integration / E2E Tests
**File**: New test in integration tests or Cypress spec

- **Flow**: Open book → open settings panel → toggle TTS on → select voice → set speed → close panel → reopen → verify values persist (debounced save round-trip)
- **Flow**: Toggle TTS off → verify playback controls disappear from bottom bar (`isTtsVisible$` becomes false)

## Potential Risks

1. **Risk**: Voice dropdown shows stale data if TTS server voices change after page load.
   - **Mitigation**: The `ttsVoices$` observable calls the API each time it's subscribed to (via `getVoices()`). If needed, add a refresh button or re-fetch on accordion expand.

2. **Risk**: Speed slider value displayed as "NaNx" if form control is null.
   - **Mitigation**: Use `[ngbTooltip]="(settingsForm.get('ttsSpeedOverride')?.value || 1.0) + 'x'"` with a fallback default.

3. **Risk**: Existing reading profiles without TTS columns may have NULL values causing issues.
   - **Mitigation**: Entity defaults (`TtsEnabled = false`, others `null`) handle this. The DTO also has the same defaults. Frontend uses `?? false` / `?? null` guards.

4. **Risk**: Auto-save debounce (500ms) may cause race conditions if user rapidly toggles TTS on/off.
   - **Mitigation**: Existing `distinctUntilChanged()` in the valueChanges pipeline prevents redundant saves for identical values. This is already in place for all settings.

## Handoff to Developer

**Design Document**: Above
**Estimated Complexity**: Low — 6 focused file modifications, no new files, no backend logic changes
**Key Files**: 
1. `Kavita.Models/DTOs/UserReadingProfileDto.cs` (backend plumbing)
2. `UI/Web/src/app/_services/epub-reader-settings.service.ts` (form wiring)
3. `UI/Web/src/app/book-reader/_components/reader-settings/reader-settings.component.html` (UI)
4. `UI/Web/src/app/book-reader/_components/book-reader/book-reader.component.ts` (visibility gate)

**Start With**: Task 1 — Add TTS fields to `UserReadingProfileDto.cs`. This is a 5-line change that unblocks all frontend work since the API will immediately start returning those fields.
