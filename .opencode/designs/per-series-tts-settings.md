# Design: Per-Series TTS Settings Completion

## Overview
Complete the "per-series TTS settings" feature by adding plumbing that lets users enable/disable TTS and override voice/speed per series from the reader settings panel. The core TTS infrastructure (backend service, controller, entities, frontend playback) is already built — this design adds the missing UI/DTO/model wiring to expose those settings in the reading profile flow.

## Architecture Decisions

1. **No new backend logic needed**: `TtsService.GetEffectiveSettings()` / `StreamAsync()` already implements the full fallback chain (request → profile → user default). The entity `AppUserReadingProfile` already has `TtsEnabled`, `TtsVoiceOverride`, `TtsSpeedOverride` columns with a migration. We only need to expose these through the DTO and frontend.

2. **AutoMapper convention-based mapping**: Property names match between `AppUserReadingProfile` (entity) and `UserReadingProfileDto` — no explicit AutoMapper profile changes needed for primitives/nullables.

3. **Debounced auto-save via existing pipeline**: The TTS form controls join the existing `settingsForm.valueChanges` → 500ms debounce → `updateImplicitProfile()` → `PUT /api/reading-profile/series` pipeline. No new subscription logic required.

4. **AsyncPipe for voice dropdown**: Use `| async` pipe in template with `TtsService.getVoices()` Observable, consistent with how `isTtsVisible$` is used elsewhere in the book reader.

5. **i18n keys under `"reader-settings"` prefix**: The TTS accordion lives inside the reader settings panel which uses `*transloco="let t; prefix: 'reader-settings'"`, so new keys go there, not under `"book-reader.tts"`.

## Files to Create/Modify

### Modified Files
| File | Changes | Reason |
|------|---------|--------|
| `Kavita.Models/DTOs/UserReadingProfileDto.cs` | Add 3 TTS properties in new `#region TtsReader` block | Expose entity columns over API |
| `UI/Web/src/app/_models/preferences/reading-profiles.ts` | Add 3 fields to `ReadingProfile` interface | TypeScript type safety for profile data |
| `UI/Web/src/app/_services/epub-reader-settings.service.ts` | Extend form group, setup methods, pack method | Wire TTS settings into reactive form pipeline |
| `UI/Web/src/app/book-reader/_components/reader-settings/reader-settings.component.html` | Add TTS accordion section | UI for per-series TTS controls |
| `UI/Web/src/app/book-reader/_components/reader-settings/reader-settings.component.ts` | Inject TtsService, expose voices observable | Feed voice dropdown data to template |
| `UI/Web/src/assets/langs/en.json` | Add 7 i18n keys under `"reader-settings"` | Labels for new UI elements |
| `UI/Web/src/app/book-reader/_components/tts-controls/tts-controls.component.html` | Fix i18n key prefix `'reader.tts.'` → `'book-reader.tts.'` | Bug fix: wrong translation key path |

### No New Files Required

## Task Breakdown (Ordered by Dependency)

---

### Task 1: Backend DTO — Add TTS Properties to `UserReadingProfileDto.cs`

**Files**: `Kavita.Models/DTOs/UserReadingProfileDto.cs`

**Description**: Add three properties matching the entity columns in `AppUserReadingProfile.TtsEnabled`, `.TtsVoiceOverride`, `.TtsSpeedOverride`. Place them in a new `#region TtsReader` block after `#endregion PdfReader`.

**Exact Change**: Insert before the closing brace of the record (after line 138, after `#endregion` for PdfReader):

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

**Acceptance Criteria**:
- Properties match entity names exactly (AutoMapper convention)
- `TtsEnabled` defaults to `false` matching the entity default
- `TtsVoiceOverride` is nullable string (`string?`)
- `TtsSpeedOverride` is nullable float (`float?`)
- XML docs use `<inheritdoc cref="AppUserReadingProfile.PropertyName"/>`

**Edge Cases**: None — AutoMapper handles primitives and nullables by convention.

---

### Task 2: Frontend Model — Add TTS Fields to `ReadingProfile` Interface

**Files**: `UI/Web/src/app/_models/preferences/reading-profiles.ts`

**Description**: Add three fields to the `ReadingProfile` interface after the PDF Reader section (after line 60, before the `// relations` comment).

**Exact Change**: Insert between line 61 (`pdfSpreadMode: PdfSpreadMode;`) and line 63 (`// relations`):

```typescript
  // TTS Reader
  ttsEnabled: boolean;
  ttsVoiceOverride?: string;
  ttsSpeedOverride?: number;
```

**Acceptance Criteria**:
- `ttsEnabled` is non-optional `boolean` (matches entity default)
- `ttsVoiceOverride` is optional string (`string | undefined`)
- `ttsSpeedOverride` is optional number (`number | undefined`) — note: C# `float?` maps to TypeScript `number`

**Edge Cases**: When the backend returns null for nullable fields, AutoMapper serializes as JSON `null`. The frontend should handle `null` → treat same as `undefined`.

---

### Task 3: Frontend Service — Wire TTS into EpubReaderSettingsService

**Files**: `UI/Web/src/app/_services/epub-reader-settings.service.ts`

**Description**: Four changes across the service to integrate TTS fields into the reactive form pipeline.

#### Change 3a: Extend `BookReadingProfileFormGroup` type (line ~28-40)

Add three new controls to the FormGroup type definition:

```typescript
export type BookReadingProfileFormGroup = FormGroup<{
  bookReaderMargin: FormControl<number>;
  bookReaderLineSpacing: FormControl<number>;
  bookReaderFontSize: FormControl<number>;
  bookReaderFontFamily: FormControl<string>;
  bookReaderTapToPaginate: FormControl<boolean>;
  bookReaderReadingDirection: FormControl<ReadingDirection>;
  bookReaderWritingStyle: FormControl<WritingStyle>;
  bookReaderThemeName: FormControl<string>;
  bookReaderLayoutMode: FormControl<BookPageLayoutMode>;
  bookReaderImmersiveMode: FormControl<boolean>;
  bookReaderDisableBookmarkIcon: FormControl<boolean>;
  // TTS Reader settings
  ttsEnabled: FormControl<boolean>;
  ttsVoiceOverride: FormControl<string | null>;
  ttsSpeedOverride: FormControl<number | null>;
}>
```

#### Change 3b: Add TTS fields in `setupSettingsForm()` (line ~460-481)

Add three form controls to the FormGroup constructor, after `bookReaderDisableBookmarkIcon`:

```typescript
this.settingsForm = new FormGroup({
  // ... existing controls ...
  bookReaderDisableBookmarkIcon: this.fb.control(profile.bookReaderDisableBookmarkIcon),
  // TTS Reader settings
  ttsEnabled: this.fb.control(profile.ttsEnabled ?? false),
  ttsVoiceOverride: this.fb.control<string | null>(profile.ttsVoiceOverride ?? null),
  ttsSpeedOverride: this.fb.control<number | null>(profile.ttsSpeedOverride ?? null),
});
```

#### Change 3c: Include TTS fields in `packReadingProfile()` (line ~646-675)

Add three lines to pack the form values into the returned ReadingProfile, after the existing signal-based assignments:

```typescript
private packReadingProfile(): ReadingProfile {
    const currentProfile = this._currentReadingProfile();
    if (!currentProfile) {
      throw new Error('No current reading profile');
    }

    const modelSettings = this.settingsForm.getRawValue();
    const data = { ...currentProfile };

    // Update from form values (existing)
    data.bookReaderFontFamily = modelSettings.bookReaderFontFamily;
    data.bookReaderFontSize = modelSettings.bookReaderFontSize;
    data.bookReaderLineSpacing = modelSettings.bookReaderLineSpacing;
    data.bookReaderMargin = modelSettings.bookReaderMargin;
    data.bookReaderDisableBookmarkIcon = modelSettings.bookReaderDisableBookmarkIcon;

    // Update from signals (existing)
    data.bookReaderTapToPaginate = this._clickToPaginate();
    data.bookReaderLayoutMode = this._layoutMode();
    data.bookReaderImmersiveMode = this._immersiveMode();
    data.bookReaderReadingDirection = this._readingDirection();
    data.bookReaderWritingStyle = this._writingStyle();

    const activeTheme = this._activeTheme();
    if (activeTheme) {
      data.bookReaderThemeName = activeTheme.name;
    }

    // TTS Reader settings — read from form controls directly
    data.ttsEnabled = modelSettings.ttsEnabled;
    data.ttsVoiceOverride = modelSettings.ttsVoiceOverride ?? null;
    data.ttsSpeedOverride = modelSettings.ttsSpeedOverride ?? null;

    return data;
  }
```

**Acceptance Criteria**:
- Form group type compiles with new control types
- `ttsEnabled` defaults to `false` when profile value is undefined/null
- `ttsVoiceOverride` and `ttsSpeedOverride` default to `null` (not `undefined`) for clean JSON serialization
- TTS fields are included in the debounced auto-save pipeline via existing `settingsForm.valueChanges` subscription

**Edge Cases**:
- If profile has no TTS fields yet (old data), `?? false` / `?? null` handles undefined gracefully
- Speed override is nullable — sending `null` to backend means "use user default"

---

### Task 4: Frontend Component HTML — Add TTS Accordion Section

**Files**: `UI/Web/src/app/book-reader/_components/reader-settings/reader-settings.component.html`

**Description**: Insert a new accordion item between the color panel (line ~191) and the "Update Parent" buttons section (line ~193). The TTS section uses the existing form group via `formControlName`.

**Exact Change**: Replace lines 192-193 (the closing of color-panel and the start of the Update Parent section):

```html
        </div>

        <!-- TTS Settings Panel -->
        <div ngbAccordionItem id="tts-panel" [title]="t('tts-settings-title')" [collapsed]="true">
          <h2 class="accordion-header" ngbAccordionHeader>
            <button class="accordion-button" ngbAccordionButton type="button" [attr.aria-expanded]="acc.isExpanded('tts-panel')">
              <i class="fa fa-volume-up me-1"></i>{{t('tts-settings-title')}}
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
                    <input type="checkbox" id="tts-enabled" formControlName="ttsEnabled" class="form-check-input" switch>
                    <label>{{settingsForm.get('ttsEnabled')?.value ? t('on') : t('off')}}</label>
                  </div>
                </div>

                <!-- Voice Override (conditional on ttsEnabled) -->
                @if (settingsForm.get('ttsEnabled')?.value) {
                  <div class="controls mt-3">
                    <label for="tts-voice" class="form-label">{{t('tts-voice-label')}}</label>
                    <select id="tts-voice" formControlName="ttsVoiceOverride" class="form-select">
                      <option [ngValue]="null">{{t('tts-voice-default')}}</option>
                      @for(voice of (ttsVoices$ | async); track voice.voiceId) {
                        <option [value]="voice.voiceId">{{voice.name}}</option>
                      }
                    </select>
                  </div>

                  <!-- Speed Override Slider -->
                  <div class="row g-0 controls mt-3">
                    <label for="tts-speed" class="form-label col-6">{{t('tts-speed-label')}}</label>
                    <span class="col-6 float-end" style="display: inline-flex;">
                      {{t('tts-speed-min')}}
                      <input type="range" class="form-range ms-2 me-2" id="tts-speed"
                             min="0.5" max="4.0" step="0.1" formControlName="ttsSpeedOverride">
                      {{t('tts-speed-max')}}
                    </span>
                  </div>
                }
              </ng-template>
            </div>
          </div>
        </div>

        @let currentRP = currentReadingProfile();
```

**Acceptance Criteria**:
- Accordion item follows existing pattern: `ngbAccordionItem` → `h2 > button[ngbAccordionButton]` → `ngbAccordionCollapse > ngbAccordionBody > ng-template`
- `[collapsed]="true"` — TTS panel starts collapsed (non-essential)
- Toggle switch uses `formControlName="ttsEnabled"` bound to parent form group
- Voice dropdown and speed slider are conditionally shown with `@if (settingsForm.get('ttsEnabled')?.value)`
- Voice dropdown populates from `ttsVoices$ | async` observable
- Speed range: min 0.5, max 4.0, step 0.1

**Edge Cases**:
- If TTS server is not configured, `getVoices()` may return empty array — the "Use default" option still shows
- Voice dropdown uses `[ngValue]="null"` for the default option to properly serialize as JSON null

---

### Task 5: Frontend Component TS — Inject TtsService and Expose Voices

**Files**: `UI/Web/src/app/book-reader/_components/reader-settings/reader-settings.component.ts`

**Description**: Two changes: import/inject TtsService, expose the voices observable for template use.

#### Change 5a: Add imports (line ~1)

Add to existing imports:
```typescript
import {AsyncPipe} from '@angular/common';
import {Observable} from 'rxjs';
import {TtsVoiceDto} from '../../../_models/tts-models';
import {TtsService} from '../../../_services/tts-service';
```

#### Change 5b: Add to component imports array (line ~86-88)

Add `AsyncPipe` to the standalone component imports:
```typescript
imports: [ReactiveFormsModule, NgbAccordionDirective, NgbAccordionItem, NgbAccordionHeader, NgbAccordionButton,
    NgbAccordionCollapse, NgbAccordionBody, NgbTooltip, NgTemplateOutlet, NgClass, NgStyle,
    TitleCasePipe, TranslocoDirective, AsyncPipe]
```

#### Change 5c: Inject TtsService and expose voices observable (after line ~92)

Add private field and public property:
```typescript
export class ReaderSettingsComponent implements OnInit {

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly ttsService = inject(TtsService);

  // Observable for voice dropdown in TTS settings panel
  protected ttsVoices$: Observable<TtsVoiceDto[]> = this.ttsService.getVoices();
```

**Acceptance Criteria**:
- `AsyncPipe` is imported and added to component imports array
- `TtsService` injected via `inject()` (not constructor)
- `ttsVoices$` is a protected Observable accessible from template
- No new subscription needed — the async pipe handles subscription/unsubscription

**Edge Cases**:
- If TTS server is not configured, `getVoices()` returns empty array or error — template gracefully shows only "Use default" option
- The HTTP call fires once on component init; voices list is static for the session

---

### Task 6: i18n Keys — Add TTS Labels to en.json

**Files**: `UI/Web/src/assets/langs/en.json`

**Description**: Add 7 new keys under the `"reader-settings"` section (after line ~1984, before the closing brace of reader-settings).

**Exact Change**: Insert after `"theme-paper": "Paper"` and before the closing `}`:

```json
        ,
        "tts-settings-title": "Text-to-Speech",
        "tts-enable-label": "Enable TTS for this series",
        "tts-voice-label": "Voice Override",
        "tts-voice-default": "Use default",
        "tts-speed-label": "Playback Speed",
        "tts-speed-min": "0.5x",
        "tts-speed-max": "4x"
```

**Acceptance Criteria**:
- Keys are under `"reader-settings"` prefix (matching the `*transloco` prefix in template)
- Key names use kebab-case: `"tts-settings-title"` not `"ttsSettingsTitle"`
- All 7 keys present with English values

**Note on other language files**: Each of the ~28 other language JSON files needs these same keys added. They can initially reference the English fallback using Transloco's fallback mechanism, or be translated later. For now, adding to `en.json` is sufficient — untranslated keys will fall back to English at runtime.

---

### Task 7: Bug Fix — Correct i18n Key Prefix in tts-controls.component.html

**Files**: `UI/Web/src/app/book-reader/_components/tts-controls/tts-controls.component.html`

**Description**: Lines 19 and 34 use `'reader.tts.'` prefix which doesn't exist. The correct prefix is `'book-reader.tts.'` where the TTS keys are defined (see en.json line ~1364).

#### Change 7a: Line 19 — Fix pause/resume tooltip

**Before**:
```html
[ngbTooltip]="'reader.tts.' + (isPaused() ? 'resume' : 'pause') | transloco"
```

**After**:
```html
[ngbTooltip]="'book-reader.tts.' + (isPaused() ? 'resume' : 'pause') | transloco"
```

#### Change 7b: Line 34 — Fix stop tooltip

**Before**:
```html
[ngbTooltip]="'reader.tts.stop' | transloco"
```

**After**:
```html
[ngbTooltip]="'book-reader.tts.stop' | transloco"
```

**Acceptance Criteria**:
- Pause/resume tooltip resolves to `book-reader.tts.pause` / `book-reader.tts.resume` (keys exist at en.json lines 1369-1370)
- Stop tooltip resolves to `book-reader.tts.stop` (key exists at en.json line 1371)

---

## Data Models / Interfaces

### Backend: UserReadingProfileDto (after changes)
```csharp
public sealed record UserReadingProfileDto
{
    // ... existing properties ...

    #region TtsReader
    public bool TtsEnabled { get; set; } = false;
    public string? TtsVoiceOverride { get; set; }
    public float? TtsSpeedOverride { get; set; }
    #endregion
}
```

### Frontend: ReadingProfile (after changes)
```typescript
export interface ReadingProfile {
  // ... existing properties ...

  // PDF Reader
  pdfTheme: PdfTheme;
  pdfScrollMode: PdfScrollMode;
  pdfSpreadMode: PdfSpreadMode;

  // TTS Reader
  ttsEnabled: boolean;
  ttsVoiceOverride?: string;
  ttsSpeedOverride?: number;

  // relations
  seriesIds: number[];
  libraryIds: number[];
}
```

### Frontend: BookReadingProfileFormGroup (after changes)
```typescript
export type BookReadingProfileFormGroup = FormGroup<{
  // ... existing controls ...
  ttsEnabled: FormControl<boolean>;
  ttsVoiceOverride: FormControl<string | null>;
  ttsSpeedOverride: FormControl<number | null>;
}>
```

## Data Flow

### Load (Backend → Frontend)
1. `GET /api/reading-profile/{libraryId}/{seriesId}?skipImplicit=false` returns `UserReadingProfileDto` with TTS fields populated from database
2. AutoMapper maps `AppUserReadingProfile.TtsEnabled/VoiceOverride/SpeedOverride` → DTO properties by convention
3. Frontend receives JSON, maps to TypeScript `ReadingProfile` interface
4. `EpubReaderSettingsService.initialize()` calls `setupDefaultsFromProfile()` and `setupSettingsForm()`, which initialize TTS form controls with profile values

### Display (Frontend)
1. `ReaderSettingsComponent` renders the TTS accordion section inside existing `<form [formGroup]="settingsForm">`
2. Toggle switch reflects `ttsEnabled` value from form control
3. Voice dropdown populates from `ttsVoices$ | async`, selected value bound to `ttsVoiceOverride`
4. Speed slider bound to `ttsSpeedOverride`

### Edit (Frontend → Backend)
1. User changes TTS toggle/voice/speed in settings panel
2. Form control change triggers `settingsForm.valueChanges`
3. Existing pipeline: 500ms debounce → `distinctUntilChanged()` → `updateImplicitProfile()`
4. `packReadingProfile()` reads TTS values from form controls and includes them in the profile object
5. `PUT /api/reading-profile/series` sends full `UserReadingProfileDto` with updated TTS fields
6. Backend saves to database via existing repository

### Playback Gate (Existing — no changes needed)
- `isTtsVisible$` in `book-reader.component.ts` already checks for configured server URL
- The per-series `ttsEnabled` flag is checked by the backend's `TtsService.GetEffectiveSettings()` during streaming

## Testing Strategy

### Backend Unit Tests (xUnit + NSubstitute)

**Test 1: DTO property defaults**
```csharp
[Fact]
public void UserReadingProfileDto_TtsProperties_HaveCorrectDefaults()
{
    var dto = new UserReadingProfileDto();
    Assert.False(dto.TtsEnabled);
    Assert.Null(dto.TtsVoiceOverride);
    Assert.Null(dto.TtsSpeedOverride);
}
```

**Test 2: AutoMapper maps TTS fields correctly**
```csharp
[Fact]
public void AutoMapper_AppUserReadingProfile_To_Dto_MapsTtsFields()
{
    var entity = new AppUserReadingProfile
    {
        TtsEnabled = true,
        TtsVoiceOverride = "nova",
        TtsSpeedOverride = 1.5f
    };

    var dto = mapper.Map<UserReadingProfileDto>(entity);

    Assert.True(dto.TtsEnabled);
    Assert.Equal("nova", dto.TtsVoiceOverride);
    Assert.Equal(1.5f, dto.TtsSpeedOverride);
}
```

**Test 3: AutoMapper handles null TTS overrides**
```csharp
[Fact]
public void AutoMapper_AppUserReadingProfile_To_Dto_HandlesNullTtsOverrides()
{
    var entity = new AppUserReadingProfile { TtsEnabled = false };

    var dto = mapper.Map<UserReadingProfileDto>(entity);

    Assert.False(dto.TtsEnabled);
    Assert.Null(dto.TtsVoiceOverride);
    Assert.Null(dto.TtsSpeedOverride);
}
```

### Frontend Unit Tests (Jasmine + Angular Testing)

**Test 1: EpubReaderSettingsService packs TTS fields correctly**
```typescript
describe('EpubReaderSettingsService - TTS', () => {
  let service: EpubReaderSettingsService;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [HttpClientModule],
      providers: [
        EpubReaderSettingsService,
        { provide: ReadingProfileService, useValue: { /* spies */ } },
        { provide: FontService, useValue: { getFonts: () => of([]) } },
        // ... other mocks
      ]
    });
    service = TestBed.inject(EpubReaderSettingsService);
  });

  it('should include TTS fields in packed profile', async () => {
    const profile: ReadingProfile = { /* minimal profile */ ttsEnabled: false, ttsVoiceOverride: undefined, ttsSpeedOverride: undefined };
    await service.initialize(1, 2, profile);

    const form = service.getSettingsForm();
    form.get('ttsEnabled')?.setValue(true);
    form.get('ttsVoiceOverride')?.setValue('nova');
    form.get('ttsSpeedOverride')?.setValue(1.5);

    // Trigger pack via updateImplicitProfile or spy on it
    // Assert returned profile has ttsEnabled: true, ttsVoiceOverride: 'nova', ttsSpeedOverride: 1.5
  });

  it('should default TTS fields when profile values are undefined', async () => {
    const profile: ReadingProfile = { /* minimal profile without TTS fields */ };
    await service.initialize(1, 2, profile);

    const form = service.getSettingsForm();
    expect(form.get('ttsEnabled')?.value).toBe(false);
    expect(form.get('ttsVoiceOverride')?.value).toBeNull();
    expect(form.get('ttsSpeedOverride')?.value).toBeNull();
  });
});
```

**Test 2: ReaderSettingsComponent exposes ttsVoices$ observable**
```typescript
describe('ReaderSettingsComponent - TTS', () => {
  let component: ReaderSettingsComponent;
  let fixture: ComponentFixture<ReaderSettingsComponent>;
  let mockTtsService: jasmine.SpyObj<TtsService>;

  beforeEach(async () => {
    mockTtsService = jasmine.createSpyObj('TtsService', ['getVoices']);
    const voices$ = of([{ voiceId: 'alloy', name: 'Alloy' }, { voiceId: 'nova', name: 'Nova' }]);
    mockTtsService.getVoices.and.returnValue(voices$);

    await TestBed.configureTestingModule({
      imports: [ReaderSettingsComponent, ReactiveFormsModule],
      providers: [
        { provide: TtsService, useValue: mockTtsService },
        // ... other mocks
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ReaderSettingsComponent);
    component = fixture.componentInstance;
  });

  it('should expose ttsVoices$ from TtsService', () => {
    expect(mockTtsService.getVoices).toHaveBeenCalled();
  });
});
```

### Integration Tests
- **API flow**: `GET /api/reading-profile/{libraryId}/{seriesId}` → verify response JSON includes `ttsEnabled`, `ttsVoiceOverride`, `ttsSpeedOverride` fields
- **Round-trip**: Set TTS values via frontend form → verify database has correct values after debounced save

### E2E Tests (if applicable)
- Open book reader → open settings panel → expand TTS accordion → toggle enable → change voice/speed → close panel → reopen and verify values persist

## Potential Risks

1. **Risk**: Old reading profiles in the database don't have TTS columns set (migration adds them with defaults).
   - **Mitigation**: Entity has `= false` / null defaults, migration sets column defaults. DTO also has matching defaults. Frontend uses `?? false` / `?? null`.

2. **Risk**: Voice dropdown shows stale data if TTS server config changes mid-session.
   - **Mitigation**: Acceptable trade-off — voices list is fetched once on settings panel open. User can reload the page to refresh. This matches how other reference data (fonts, themes) works in the app.

3. **Risk**: `ttsSpeedOverride` form control type mismatch between C# `float?` and TypeScript `number | null`.
   - **Mitigation**: JSON serialization handles float → number transparently. The `[ngValue]="null"` pattern for default option ensures clean null propagation.

4. **Risk**: i18n keys missing in non-English language files cause raw key display.
   - **Mitigation**: Transloco's fallback mechanism will show English text when a key is missing in the active locale. Keys can be translated incrementally.

5. **Risk**: Bug fix (Task 7) may have been already fixed or the keys might exist under both prefixes.
   - **Mitigation**: Verified via grep — `'reader.tts.'` prefix does NOT exist in en.json, only `'book-reader.tts.'`. The fix is necessary.

## Handoff to Developer

**Design Document**: Above (inline)
**Estimated Complexity**: Low — 7 discrete changes across 6 files, no new backend logic, all wiring into existing patterns
**Key Files**:
1. `Kavita.Models/DTOs/UserReadingProfileDto.cs` — Add 3 properties
2. `UI/Web/src/app/_services/epub-reader-settings.service.ts` — Wire form controls and pack method
3. `UI/Web/src/app/book-reader/_components/reader-settings/reader-settings.component.html` — TTS accordion UI
4. `UI/Web/src/assets/langs/en.json` — i18n keys
5. `UI/Web/src/app/book-reader/_components/tts-controls/tts-controls.component.html` — Bug fix

**Start With**: Task 1 (Backend DTO) → Task 2 (Frontend Model) → Task 3 (Service wiring). These are the foundation that Tasks 4-7 depend on. Task 7 (bug fix) is independent and can be done in parallel.
