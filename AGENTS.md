# Kavita - AI Development Guide

## Project Overview

**Kavita** is a self-hosted digital library application (similar to Calibre Web) that serves EPUB books, manga/comics, and PDFs through a web interface. It's built as a full-stack .NET + Angular monorepo.

- **Repository**: https://github.com/Kareadita/Kavita
- **.NET Version**: 10 (ASP.NET Core)
- **Frontend**: Angular v21 with signals/effects architecture
- **Database**: SQLite via Entity Framework Core
- **EPUB Parsing**: VersOne.Epub library

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend Framework | ASP.NET Core 10, MVC Controllers |
| ORM | Entity Framework Core (SQLite) |
| Frontend Framework | Angular v21 (signals, effects, standalone components) |
| State Management | Angular signals + RxJS |
| Real-time Communication | SignalR |
| i18n | @jsverse/transloco |
| UI Components | ng-bootstrap (NGB) |
| EPUB Library | VersOne.Epub |
| PDF Rendering | Docnet library |
| Build System | .NET SDK + Angular CLI |

---

## Solution Structure

```
Kavita.sln
├── Kavita.Server/          # Main web application, controllers, startup
├── Kavita.Services/        # Business logic services
├── Kavita.API/             # API contracts, attributes, interfaces
├── Kavita.Models/          # DTOs, Entities, Enums, AutoMapper profiles
├── Kavita.Common/          # Shared utilities and extensions
├── Kavita.Database/        # EF Core DbContext, migrations, repositories
├── Kavita.Email/           # Email service
├── Kavita.Common.Tests/    # Unit tests for common layer
├── Kavita.Models.Tests/    # Unit tests for models
├── Kavita.Database.Tests/  # Database integration tests
├── Kavita.Server.Tests/    # Server/controller tests
├── Kavita.Services.Tests/  # Service unit tests
├── Kavita.Integration.Tests/ # Integration tests
├── Kavita.Benchmark/       # Performance benchmarks
└── UI/Web/                 # Angular frontend application
```

---

## Backend Architecture

### Controller Pattern

All controllers extend `BaseApiController` at `Kavita.Server/Controllers/BaseApiController.cs`:

```csharp
public class MyController(
    IMyService myService,
    IUnitOfWork unitOfWork,
    ILocalizationService localizationService)
    : BaseApiController
{
    [HttpGet("endpoint")]
    [ChapterAccess]  // or [Authorize]
    public async Task<ActionResult<MyDto>> GetSomething(int id)
    {
        // ...
    }
}
```

**Key patterns**:
- Constructor injection (primary constructors)
- Route attribute: `[Route("api/[controller]")]` on base class
- Authorization via `[ChapterAccess]`, `[Authorize]` custom attributes
- Response caching via `[ResponseCache(CacheProfileName = ...)]`
- Localization via `ILocalizationService.TranslateAsync(UserId, "key")`

### Service Pattern

Services are in `Kavita.Services/` and implement interfaces defined in `Kavita.API/`:

```csharp
public interface IMyService { /* ... */ }

public class MyService(
    IUnitOfWork unitOfWork,
    ILogger<MyService> logger,
    IEventHub eventHub) : IMyService
{
    // Implementation
}
```

**Key services**:
- `BookService.cs` — EPUB text extraction, page rendering (~1955 lines)
- `ReaderService.cs` — Reading progress, chapter navigation
- `CacheService` — File caching layer
- `EventHub.cs` — SignalR event broadcasting
- `TtsService.cs` — TTS audiobook streaming (EPUB text chunking, OpenAI-compatible API calls, SignalR audio streaming)

### Data Access Pattern

Repository pattern via `IUnitOfWork`:

```csharp
var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(chapterId);
var userProgress = await unitOfWork.UserRepository.GetUserProgress(userId, seriesId);
```

Key repositories: `ChapterRepository`, `UserRepository`, `SeriesRepository`, `VolumeRepository`

### DTOs and Models

- **DTOs**: `Kavita.Models/DTOs/` — Data transfer objects for API responses
  - Reader DTOs in `DTOs/Reader/` (BookInfoDto, BookChapterItem, etc.)
  - Settings DTOs in `DTOs/Settings/`
  - SignalR messages in `DTOs/SignalR/`
- **Entities**: `Kavita.Models/Entities/` — Database models
  - User entities in `Entities/User/`
  - Enums in `Entities/Enums/`

### SignalR Pattern

Real-time communication via two hubs:

```csharp
// MessageHub.cs - Main hub for UI events
public class MessageHub : Hub { }

// LogHub.cs - Logging hub
public class LogHub : Hub<ILogHub> { }

// EventHub.cs - Service to broadcast messages
public class EventHub(IHubContext<MessageHub> messageHub, ...)
{
    public async Task SendEvent(SignalRMessage message) { /* ... */ }
}
```

To send events: `await eventHub.SendEvent(MessageFactory.MyCustomEvent(...))`

### Configuration Pattern

- Minimal `appsettings.json` in `Kavita.Server/config/`
- Server settings stored in database via `ServerSetting` entity
- User preferences stored per-user via `AppUserPreferences` entity
- Reading profiles stored per-user via `AppUserReadingProfile` entity

---

## Frontend Architecture (Angular)

### Project Structure

```
UI/Web/src/app/
├── book-reader/           # EPUB reader module
│   ├── _components/book-reader/    # Main reader component
│   ├── _components/reader-settings/# Reader settings panel
│   ├── _services/book.service.ts   # Book API service
│   └── _models/                      # TypeScript models
├── manga-reader/          # Manga/comic reader module
├── pdf-reader/            # PDF reader module
├── _models/               # Shared TypeScript models
├── _services/             # Shared services (reader, nav, theme, etc.)
├── shared/                # Shared components and utilities
└── environments/          # Environment configuration
```

### Key Frontend Components for TTS

**BookReaderComponent** (`book-reader/_components/book-reader/book-reader.component.ts`):
- ~1400+ lines, the main EPUB reader
- Uses Angular signals extensively: `pageNum()`, `maxPages()`, `page()` (SafeHtml)
- Manages scroll position, pagination, chapter navigation
- Template at `book-reader.component.html` renders content via `[innerHTML]`

**BookService** (`book-reader/_services/book.service.ts`):
- Thin HTTP service wrapping `/api/book/` endpoints
- Methods: `getBookPage()`, `getBookChapters()`, `getBookInfo()`

**EpubReaderSettingsService** (`_services/epub-reader-settings.service.ts`):
- Manages reader settings with Angular signals
- Settings include: layout mode, reading direction, writing style, themes, fonts
- Settings persisted per series/user via API

### Angular Patterns Used

1. **Standalone Components**: All components use `imports: [...]` instead of NgModules
2. **Signals**: Reactive state via `signal()`, `computed()`, `effect()`
3. **Dependency Injection**: Functional `inject()` pattern (not constructor injection)
4. **OnPush Change Detection**: `changeDetection: ChangeDetectionStrategy.OnPush`
5. **RxJS**: For HTTP calls and event streams with `takeUntilDestroyed()`
6. **Transloco**: i18n via `translate('key')` function and `transloco` directive

### API Communication Pattern

```typescript
@Injectable({ providedIn: 'root' })
export class MyService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getData(id: number) {
    return this.http.get<MyDto>(this.baseUrl + 'myendpoint/' + id);
  }
}
```

---

## Key API Endpoints for Books

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/book/{chapterId}/book-info` | GET | Book metadata (title, pages, series) |
| `/api/book/{chapterId}/chapters` | GET | Table of contents / chapter mapping |
| `/api/book/{chapterId}/book-page?page={page}` | GET | Scoped HTML for a single page |
| `/api/book/{chapterId}/book-resources?file={file}` | GET | Embedded resources (images, CSS) |

---

## TTS Feature (Backend + Frontend Complete)

Text-to-Speech audiobook feature allows users to configure an OpenAI-compatible TTS server and stream audio from EPUB chapters via SignalR.

### Backend Architecture
- **TtsService.cs** — Core service: EPUB text extraction, HTTP client calls to TTS endpoint, SignalR streaming with cancellation support
- **TtsController.cs** — API endpoints for config CRUD, test, voices, chunks, stream start/stop
- **UserTtsConfig entity** — Per-user TTS server configuration (API key encrypted via ASP.NET Core Data Protection `IDataProtector`)
- **AppUserReadingProfile** — Extended with `TtsEnabled`, `TtsVoiceOverride`, `TtsSpeedOverride` for per-series overrides

### Frontend Architecture
- **tts-models.ts** — TypeScript interfaces (`TtsServerConfigDto`, `TtsVoiceDto`, `TtsStreamRequest`, etc.)
- **tts-service.ts** — HTTP API service wrapping `/api/tts/*` endpoints
- **tts-playback.service.ts** — SignalR + Web Audio playback engine with chunk queue, pre-decoding, `currentChunkIndex` signal for highlighting
- **tts-controls/** — Playback controls UI component (play/pause/stop/speed/volume) integrated into bottom action bar of BookReaderComponent
- **manage-tts-config/** — NGB modal for configuring TTS server URL, API key, model, voice, speed with test connection

### Key API Endpoints for TTS

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/tts/config` | GET/PUT | User's TTS server configuration |
| `/api/tts/test` | POST | Test connectivity to TTS server |
| `/api/tts/voices` | GET | Available voices from TTS server |
| `/api/tts/chunks?chapterId={id}` | GET | Extract text chunks from EPUB chapter |
| `/api/tts/stream?chapterId={id}` | POST | Start SignalR audio streaming |
| `/api/tts/stream/stop` | POST | Stop active stream |

### SignalR Messages
- `TtsAudioChunk` — Audio chunk data (base64) with metadata for playback. Registered in `MessageHubService.EVENTS.TtsAudioChunk` and `.on()` handler in `createHubConnection()`.
- `TtsPlaybackUpdate` — Progress updates during streaming. Registered in `MessageHubService.EVENTS.TtsPlaybackUpdate` and `.on()` handler in `createHubConnection()`.

### Voice Fallback Chain
1. Request override → 2. Reading profile override → 3. User default config (`UserTtsConfig.DefaultVoice`)

### i18n Keys
- `"book-reader.tts.*"` — Reader UI labels (play, pause, stop, speed, volume)
- `"tts.config.*"` — Config modal labels (server URL, API key, model, voice, test connection, etc.)

---

## Book Reading Flow

1. User opens book → Frontend calls `GET /book-info` and `GET /chapters`
2. Reader component loads page HTML via `GET /book-page?page=N`
3. Backend's `BookService.GetBookPage()` extracts text from EPUB, scopes CSS/images
4. Frontend renders HTML with `[innerHTML]`, manages scroll/pagination
5. Progress saved via XPath to visible element + page number

---

## Adding New Features - Conventions

### Backend: Add a New Service

1. Create interface in `Kavita.API/Services/IMyService.cs`
2. Implement in `Kavita.Services/MyService.cs`
3. Register in DI container via `ApplicationServiceExtensions.cs`
4. Create controller in `Kavita.Server/Controllers/MyController.cs`

### Backend: Add a New DTO

1. Create class in `Kavita.Models/DTOs/` (subfolder by domain)
2. If mapping from entities, add to AutoMapper profile

### Frontend: Add a New Component

1. Create component in appropriate module folder under `_components/`
2. Use standalone component pattern with `imports: [...]`
3. Use `inject()` for dependencies, signals for state
4. Add translations to i18n files

### Adding Settings

- **Server-wide**: Extend `ServerSetting` entity + migration
- **User preferences**: Extend `AppUserPreferences` entity + migration
- **Reading profile**: Extend `AppUserReadingProfile` entity + migration
- Frontend: Add to settings form groups, persist via API

---

## Build and Run

```bash
# Build .NET solution
dotnet build Kavita.sln

# Run backend (from project root)
./build.sh

# Frontend is bundled with the backend at build time
# Angular builds into the ASP.NET wwwroot
```

---

## Per-Series TTS Settings Feature (June 2026)

### Overview
Adds a TTS section to the EPUB reader settings panel, letting users enable/disable TTS and override voice/speed on a per-series basis. Settings persist via the existing `AppUserReadingProfile` entity → `UserReadingProfileDto` → Angular `ReadingProfile` model pipeline.

### Backend Changes

**`Kavita.Models/DTOs/UserReadingProfileDto.cs`** — Add TTS region:
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
AutoMapper already maps `AppUserReadingProfile → UserReadingProfileDto` via `CreateMap<AppUserReadingProfile, UserReadingProfileDto>()`, so no profile change is needed — the three TTS columns are primitives/nullables that map automatically.

### Frontend Changes

**`UI/Web/src/app/_models/preferences/reading-profiles.ts`** — Add to `ReadingProfile` interface:
```typescript
// TTS Reader
ttsEnabled: boolean;
ttsVoiceOverride?: string;
ttsSpeedOverride?: number;
```

**`UI/Web/src/app/_services/epub-reader-settings.service.ts`** — Two changes:

1. Add signals and form controls for TTS:
```typescript
private readonly _ttsEnabled = signal<boolean>(false);
// Form group type extends with:
ttsEnabled: FormControl<boolean>;
ttsVoiceOverride: FormControl<string | null>;
ttsSpeedOverride: FormControl<number | null>;
```

2. In `setupDefaultsFromProfile()` — load TTS fields from profile:
```typescript
this._ttsEnabled.set(profile.ttsEnabled ?? false);
```

3. In `setupSettingsForm()` — add TTS form controls with debounced auto-save:
```typescript
bookReaderDisableBookmarkIcon: this.fb.control(profile.bookReaderDisableBookmarkIcon),
// New TTS controls:
ttsEnabled: this.fb.control(profile.ttsEnabled ?? false),
ttsVoiceOverride: this.fb.control(profile.ttsVoiceOverride ?? null),
ttsSpeedOverride: this.fb.control(profile.ttsSpeedOverride ?? null),
```

4. In `packReadingProfile()` — include TTS fields when saving:
```typescript
data.ttsEnabled = this._ttsEnabled();
data.ttsVoiceOverride = this.settingsForm.get('ttsVoiceOverride')?.value ?? null;
data.ttsSpeedOverride = this.settingsForm.get('ttsSpeedOverride')?.value ?? null;
```

**`UI/Web/src/app/book-reader/_components/reader-settings/reader-settings.component.html`** — Add TTS accordion section after the color-panel, before the "Update Parent" buttons:
```html
<div ngbAccordionItem id="tts-panel" [title]="t('tts-settings-title')" [collapsed]="true">
  <h2 class="accordion-header" ngbAccordionHeader>
    <button class="accordion-button" ngbAccordionButton type="button">
      <i class="fa fa-volume-up me-2"></i>{{t('tts-settings-title')}}
      <i class="fas fa-chevron-up" aria-hidden="true"></i>
    </button>
  </h2>
  <div ngbAccordionCollapse>
    <div ngbAccordionBody>
      <ng-template>
        <div class="controls d-flex justify-content-between align-items-center">
          <label for="tts-enabled" class="form-label">{{t('tts-enable-label')}}</label>
          <div class="form-check form-switch">
            <input type="checkbox" id="tts-enabled" formControlName="ttsEnabled" class="form-check-input" switch>
            <label>{{settingsForm.get('ttsEnabled')?.value ? t('on') : t('off')}}</label>
          </div>
        </div>
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
          <div class="row g-0 controls">
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
```

**`UI/Web/src/app/book-reader/_components/reader-settings/reader-settings.component.ts`** — Expose TTS voices observable from the service:
```typescript
ttsVoices$ = this.ttsService.getVoices(); // inject TtsService
```

**`UI/Web/src/app/book-reader/_components/book-reader/book-reader.component.ts`** — Update `isTtsVisible$` to also check `currentReadingProfile().ttsEnabled`:
```typescript
isTtsVisible$: Observable<boolean> = combineLatest([
  this.ttsService.getConfig(),
  this.readerSettingsService.currentReadingProfile,
]).pipe(
  map(([config, profile]) => !!(config?.serverUrl && profile?.ttsEnabled)),
  takeUntilDestroyed(this.destroyRef),
);
```

### i18n Keys to Add (en.json)

```json
"tts-settings-title": "Text-to-Speech",
"tts-enable-label": "Enable TTS for this series",
"tts-voice-label": "Voice Override",
"tts-voice-default": "Use default",
"tts-speed-label": "Playback Speed",
"tts-speed-min": "0.5x",
"tts-speed-max": "4x"
```

### Data Flow

1. **Load**: `GET /api/reading-profile/{libraryId}/{seriesId}?skipImplicit=false` → `UserReadingProfileDto` (now includes TTS fields) → mapped to Angular `ReadingProfile` → passed to `EpubReaderSettingsService.initialize()`
2. **Display**: `ReaderSettingsComponent` renders TTS accordion section with current profile values
3. **Edit**: Form control changes debounce 500ms → `updateImplicitProfile()` → `PUT /api/reading-profile/series` with full `UserReadingProfileDto` (including TTS fields)
4. **Playback gate**: `isTtsVisible$` now requires both `serverUrl` configured AND `profile.ttsEnabled === true` before showing TTS controls
5. **Streaming**: `TtsService.StreamAsync()` already reads `profile.TtsEnabled` / `profile.TtsVoiceOverride` / `profile.TtsSpeedOverride` — no backend changes needed

### No Backend Logic Changes Required
`TtsService.GetEffectiveSettings()` / `StreamAsync()` already implements the full fallback chain (request → profile → user default). The `UserReadingProfileDto` addition is purely plumbing to expose those columns over the API.

---

## Testing Patterns

### Backend Unit Tests (xUnit + NSubstitute)
- Service tests mock `IUnitOfWork` and dependencies via NSubstitute
- **HttpClient testing**: Use a custom `HttpMessageHandler` subclass that overrides the virtual `SendAsync()` method — do NOT try to mock `HttpClient.PostAsync`/`GetAsync` directly (they are non-virtual and will fail with NSubstitute)

```csharp
// Sample: Testing a service that uses HttpClient via IHttpClientFactory
public class MyServiceTests
{
    private readonly HttpMessageHandler _handler;
    private readonly HttpClient _httpClient;

    public MyServiceTests()
    {
        _handler = new TestHttpMessageHandler(); // custom handler overriding SendAsync
        _httpClient = new HttpClient(_handler);
    }

    [Fact]
    public async Task SomeMethod_ShouldCallEndpoint()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var service = new MyService(unitOfWork, ...);
        
        // Arrange test handler response
        // Act & Assert
    }
}

// Sample HttpMessageHandler for testing HTTP calls
public class TestHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"result\": \"ok\"}", Encoding.UTF8, "application/json")
        });
    }
}
```

### Frontend Unit Tests (Jasmine + Angular Testing)
- Use `TestBed.configureTestingModule()` with standalone component imports
- Mock services via `{ provide: MyService, useValue: { method: jasmine.createSpy() } }`
- Test signal values with `.getValue()` or direct access
- Test form controls with `formGroup.get('fieldName')?.value`

```typescript
// Sample: Testing a service that uses signals and forms
describe('EpubReaderSettingsService', () => {
  let service: EpubReaderSettingsService;
  
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientModule],
      providers: [
        EpubReaderSettingsService,
        { provide: ReadingProfileService, useValue: { /* spies */ } },
        // ... other mocks
      ]
    });
    service = TestBed.inject(EpubReaderSettingsService);
  });

  it('should pack TTS fields into reading profile', async () => {
    await service.initialize(1, 2, sampleProfile);
    
    const form = service.getSettingsForm();
    form.get('ttsEnabled')?.setValue(true);
    form.get('ttsVoiceOverride')?.setValue('voice-abc');
    form.get('ttsSpeedOverride')?.setValue(1.5);
    
    // Trigger packReadingProfile via updateImplicitProfile or direct call
    // Assert the returned profile has correct TTS values
  });
});
```

### Integration Tests
- In `Kavita.Integration.Tests/` for backend API flows
- Run: `dotnet test Kavita.sln`

## Coding Standards (Per-Series TTS Feature)

### Backend DTO Pattern
When adding new fields to existing DTOs that map from entities via AutoMapper convention-based mapping:
1. Add properties with matching names and compatible types (`bool`, `string?`, `float?`)
2. Set sensible defaults matching the entity defaults
3. Use `<inheritdoc cref="Entity.Property"/>` for XML doc consistency
4. No AutoMapper profile changes needed if property names match

### Frontend Form Pattern (EpubReaderSettingsService)
When adding new settings to the reading profile form:
1. Add field to `BookReadingProfileFormGroup` type definition
2. Initialize in `setupSettingsForm()` with value from current profile (`?? default`)
3. Include in `packReadingProfile()` — read from signal if it's a signal-backed setting, or from form control directly
4. No separate subscription needed — the existing `settingsForm.valueChanges` pipeline (debounced 500ms → `updateImplicitProfile()`) handles persistence automatically

### Frontend Accordion Pattern (ReaderSettingsComponent)
When adding new accordion sections:
1. Use `<div ngbAccordionItem id="unique-id">` with `[collapsed]="true"` for non-essential panels
2. Follow existing structure: `h2 > button[ngbAccordionButton]` header + `ngbAccordionCollapse > ngbAccordionBody` body
3. Form controls inside use `formControlName` bound to the parent form group
4. Conditional sections use `@if (settingsForm.get('controlName')?.value)` pattern

### i18n Pattern
- New keys go under existing section prefixes (`"reader-settings."`, `"book-reader.tts."`)
- Use descriptive kebab-case: `"tts-enable-label"` not `"enableTts"`
- Reference with `{{t('key')}}` in templates (inside `*transloco="let t; prefix: '...'"`), `translate('key')` in TypeScript
- **Key prefix rules**:
  - Reader settings panel keys → under `"reader-settings."` (template uses `prefix: 'reader-settings'`)
  - Book reader TTS controls keys → under `"book-reader.tts."` (used with dynamic key concatenation)
  - TTS config modal keys → under `"tts.config."`

### Observable in Templates Pattern
When displaying data from an HTTP observable in a template:
1. Use `AsyncPipe` — add to component imports array and use `| async` in template
2. The pipe handles subscription/unsubscription automatically (no manual `.subscribe()`)
3. For select dropdowns, iterate with `@for(item of (observable$ | async); track item.id)`

```typescript
// Component class:
protected voices$: Observable<TtsVoiceDto[]> = this.ttsService.getVoices();

// Template:
<select formControlName="voiceOverride" class="form-select">
  <option [ngValue]="null">{{t('use-default')}}</option>
  @for(voice of (voices$ | async); track voice.voiceId) {
    <option [value]="voice.voiceId">{{voice.name}}</option>
  }
</select>
```

### Nullable Form Control Pattern
When a form control can be null (e.g., "use default" option):
1. Type as `FormControl<string | null>` or `FormControl<number | null>`
2. Use `[ngValue]="null"` for the "default/none" option in select elements
3. In `packReadingProfile()`, use `?? null` to ensure undefined becomes null for clean JSON serialization

```typescript
// Form setup:
ttsVoiceOverride: this.fb.control<string | null>(profile.ttsVoiceOverride ?? null),

// Packing:
data.ttsVoiceOverride = modelSettings.ttsVoiceOverride ?? null;
```

### DI Container Verification — Preventive Integration Test

**Purpose**: Catch "missing DI registration" bugs before runtime. Unit tests that construct services directly via `new` (bypassing the container) will pass even when a constructor requests an unregistered type. Only at runtime — when the real DI container tries to resolve the service — will the error surface.

**Real example**: `TtsService` originally injected `IDataProtector` directly, but ASP.NET Core's `AddDataProtection()` only registers `IDataProtectionProvider`. The provider creates protectors on demand via `.CreateProtector("purpose")`, but `IDataProtector` itself is not registered. Unit tests in `TtsServiceTests.cs` pass because they construct `TtsService` via `new`, never involving the container. The bug only surfaced at runtime.

**Test location**: `Kavita.Server.Tests/DI/ServiceResolutionTests.cs`

**How it works**:
1. `ServiceResolutionFixture` builds the full DI container using the **same registration pipeline as `Startup.cs`** (`services.AddApplicationServices(config, env)`)
2. Overrides `DataContext` registration to use in-memory SQLite (`UseSqlite("Data Source=:memory:")`) so no real database files are needed
3. Each test creates a scoped service provider and calls `GetRequiredService<T>()` on every registered service
4. If any constructor parameter cannot be resolved, `InvalidOperationException` is thrown at resolution time

**Running the tests**:
```bash
dotnet test Kavita.Server.Tests --filter "FullyQualifiedName~DI"
```

**Key pattern**: When adding a new service registration in `ApplicationServiceExtensions.cs`, add the service's interface/type to `ServiceResolutionTests._servicesToVerify[]`. This acts as a code review checklist and ensures the new service is verified by the resolution test.

**Common mistake — what NOT to do**:
```csharp
// ❌ WRONG: IDataProtector is NOT registered in DI
public class MyService(IDataProtector dataProtector) { }

// ✅ CORRECT: Inject the provider, create the protector at runtime
public class MyService(IDataProtectionProvider dataProtectionProvider)
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("MyPurpose");
}
```

**Why scoped resolution matters**: Most Kavita services are registered as Scoped. Resolving them from the root `IServiceProvider` would fail. The test creates a scope (`CreateScope()`) to match the real request lifecycle.

---

## Recent Changes (June 2026)

### TTS Settings Page (June 2026)

**Goal**: Add a dedicated TTS settings tab under User Settings → Account so users can configure their TTS server from the main Settings page, not just from inside the book reader.

**Files created**:
- `UI/Web/src/app/user-settings/manage-tts-settings/manage-tts-settings.component.ts` — Standalone settings component using Angular signals, reactive forms, and `firstValueFrom` for async operations
- `UI/Web/src/app/user-settings/manage-tts-settings/manage-tts-settings.component.html` — Inline form with server URL, API key, model, voice dropdown, speed slider, test connection, and save button. Reuses all existing `"tts.config.*"` i18n keys.
- `UI/Web/src/app/user-settings/manage-tts-settings/manage-tts-settings.component.scss` — Minimal stylesheet

**Files modified**:
- `UI/Web/src/app/sidenav/preference-nav/preference-nav.component.ts` — Added `TtsConfig = 'tts-config'` to `SettingsTabId` enum and added `new SideNavItem(SettingsTabId.TtsConfig)` in Account section after `ReadingProfiles`
- `UI/Web/src/app/settings/_components/settings/settings.component.ts` — Imported `ManageTtsSettingsComponent` into `imports` array
- `UI/Web/src/app/settings/_components/settings/settings.component.html` — Added `@case (SettingsTabId.TtsConfig)` switch branch rendering `<app-manage-tts-settings />` with `@defer (prefetch on idle)`
- `UI/Web/src/assets/langs/en.json` — Added `"tts-config": "Text-to-Speech"` under `"settings"` object (after `"reading-profiles"`)

**Implementation notes**:
- Form controls reuse existing i18n keys: `tts.config.server-url`, `tts.config.api-key`, `tts.config.default-model`, `tts.config.default-voice`, `tts.config.default-speed`, `tts.config.test-connection`, `common.save`
- Voice dropdown fetches from `/api/tts/voices` and merges with built-in OpenAI voices (alloy, echo, fable, onyx, nova, shimmer)
- Test connection calls `/api/tts/test` with `TtsTestRequestDto` (uses `model`/`voice` fields distinct from config's `defaultModel`/`defaultVoice`)
- Save calls `PUT /api/tts/config` with `TtsServerConfigRequest`
- Component uses OnPush change detection with signals for all async state (loading, testing, saving, testResult, voices)

**TypeScript verification**: `npx tsc --noEmit` reports 0 errors in the new component and modified files.

### TTS Settings Page — Code Review Fixes (June 2026)

Fixed four issues identified during code review:

**Issue 1 — Form validators aligned with modal**: Added `Validators.required` and `Validators.maxLength()` to `defaultModel` and `defaultVoice` controls; added `Validators.maxLength()` to `serverUrl` and `apiKey`; added `Validators.required` to `defaultSpeed`.

**Issue 2 — Save error user feedback**: Added `saveError` signal and displayed it in the template as a dismissible danger alert. Error is cleared on retry and on successful save.

**Issue 3 — Missing `[class.is-invalid]` on model input**: Added `[class.is-invalid]` binding to the `defaultModel` input element so Bootstrap validation styling activates on touch/invalid.

**Issue 4 — Voice fetch error logging**: Added `console.error('Failed to load TTS voices', err)` in `fetchVoices()` catch block.

**Files modified**:
- `UI/Web/src/app/user-settings/manage-tts-settings/manage-tts-settings.component.ts` — Form validators, `saveError` signal, error handling
- `UI/Web/src/app/user-settings/manage-tts-settings/manage-tts-settings.component.html` — `is-invalid` class binding, save error alert

---

## Important Files Reference

| File | Purpose |
|------|---------|
| `Kavita.Server/Controllers/BaseApiController.cs` | Base controller class |
| `Kavita.Server/Controllers/BookController.cs` | Book reading API endpoints |
| `Kavita.Services/BookService.cs` | EPUB parsing and text extraction |
| `Kavita.Services/SignalR/EventHub.cs` | SignalR event broadcasting |
| `Kavita.Models/DTOs/SignalR/MessageFactory.cs` | SignalR message factory methods |
| `Kavita.Models/Entities/User/AppUserPreferences.cs` | User preference entity |
| `UI/Web/src/app/book-reader/_components/book-reader/book-reader.component.ts` | Main reader component |
| `UI/Web/src/app/book-reader/_services/book.service.ts` | Frontend book API service |
| `UI/Web/src/app/_services/epub-reader-settings.service.ts` | Reader settings management |
| `Kavita.Models/AutoMapper/ApplicationServiceExtensions.cs` | DI registration |
| `Kavita.Services/TtsService.cs` | TTS audiobook streaming service |
| `Kavita.Server/Controllers/TtsController.cs` | TTS API endpoints |
| `Kavita.Common/Helpers/CryptoHelper.cs` | AES-GCM encryption for API keys at rest |
| `Kavita.Models/Entities/User/UserTtsConfig.cs` | Per-user TTS config entity |
| `Kavita.Database/Migrations/20260617_AddTtsFeature.cs` | Migration: UserTtsConfig table + TTS columns on AppUserReadingProfile |
| `Kavita.Services.Tests/TtsServiceTests.cs` | Unit tests for TTS service (NSubstitute/xUnit) |
| `Kavita.Server.Tests/TtsControllerTests.cs` | Unit tests for TTS controller endpoints |
| `UI/Web/src/app/book-reader/_models/tts-models.ts` | TTS TypeScript interfaces |
| `UI/Web/src/app/book-reader/_services/tts-service.ts` | Frontend TTS HTTP API service |
| `UI/Web/src/app/book-reader/_services/tts-playback.service.ts` | SignalR + Web Audio playback engine with chunk queue and pre-decoding |
| `UI/Web/src/app/book-reader/_components/tts-controls/` | Playback controls UI (play/pause/stop/speed/volume) in bottom action bar |
| `UI/Web/src/app/book-reader/_components/manage-tts-config/` | NGB modal for TTS server configuration with test connection |
| `Kavita.Models/DTOs/UserReadingProfileDto.cs` | Reading profile DTO with TTS fields (`TtsEnabled`, `TtsVoiceOverride`, `TtsSpeedOverride`) |
| `UI/Web/src/app/_models/preferences/reading-profiles.ts` | Angular ReadingProfile model with TTS fields |
| `Dockerfile` | Docker image definition (expects `_output/*.tar.gz`) |
| `Dockerfile.build` | Multi-stage build from source — no host deps needed |
| `docker-compose.yml` | One-command run: `docker compose up --build -d` |
| `.dockerignore` | Excludes bin/, obj/, node_modules/ from Docker context |
| `entrypoint.sh` | Container entrypoint (runs Kavita binary, handles config) |
| `build.sh` | Build script producing `_output/*.tar.gz` archives |
| `Kavita.Server.Tests/DI/ServiceResolutionFixture.cs` | DI resolution test fixture — builds full container with in-memory SQLite |
| `Kavita.Server.Tests/DI/ServiceResolutionTests.cs` | Verifies all registered services can be resolved from the DI container |


---

## Recent Changes (June 2026)

### DI Container Verification Test (June 2026)

**Problem**: Kavita had no test that built the real DI container and resolved services from it. The `TtsService.IDataProtector` bug showed that unit tests can pass (constructing services directly with `new` + mocks) while the actual DI container fails at runtime.

**Fix**: Added an integration test that builds the full DI container using the same `Startup.cs` registration pipeline, then resolves every registered service type in a scoped lifetime.

**How it works**:
1. `ServiceResolutionFixture` calls `services.AddApplicationServices(config, env)` — same as `Startup.cs`
2. Overrides `DbContext` to in-memory SQLite so no real database files are needed
3. `ServiceResolutionTests` has a parameterized `[Theory]` test for every registered service type
4. Each test creates a scope and calls `GetRequiredService(serviceType)`
5. If a constructor parameter is not registered, `InvalidOperationException` is thrown at resolution time

**Files created**:
- `Kavita.Server.Tests/DI/ServiceResolutionFixture.cs` — xUnit collection fixture building the full container
- `Kavita.Server.Tests/DI/ServiceResolutionTests.cs` — parameterized tests resolving all registered services
- `Kavita.Server.Tests/Kavita.Server.Tests.csproj` — added `appsettings.json` copy so `Configuration` class works at test startup

**Verification**: After applying the fix, all services resolve successfully. When reverting to the buggy code (injecting `IDataProtector`), the test correctly fails with `"Unable to resolve service for type 'Microsoft.AspNetCore.DataProtection.IDataProtector'"`.

**Usage**:
```bash
dotnet test Kavita.Server.Tests --filter "FullyQualifiedName~DI"
```

---

### TtsService DI Fix — IDataProtectionProvider (June 2026)

**Problem**: `TtsService` injected `IDataProtector` directly, which is not registered in the ASP.NET Core DI container. The correct pattern is to inject `IDataProtectionProvider` and call `.CreateProtector("purpose")`.

**Fix**:
- Constructor parameter changed from `IDataProtector dataProtector` → `IDataProtectionProvider dataProtectionProvider`
- Added private field: `private readonly IDataProtector _dataProtector = dataProtectionProvider.CreateProtector(TtsApiKeyPurpose);`
- Updated all internal usages (`EncryptApiKey`, `DecryptApiKey`) to reference `_dataProtector`

**Files modified**:
- `Kavita.Services/TtsService.cs` — Constructor + field + 2 method bodies
- `Kavita.Services.Tests/TtsServiceTests.cs` — Added `_dpProvider` field, pass provider (not protector) to constructor; kept `_dataProtector` for test data encryption

**Verification**: Build: 0 errors. TTS tests: 14/14 passed. Controller tests: 11/12 passed (1 pre-existing skip).

### TTS Feature Usability Fixes

Made the TTS feature discoverable and usable from within the book reader:

1. **Conditional visibility**: `<app-tts-controls>` now only renders when user has a valid `serverUrl` configured (`isTtsVisible$` observable)
2. **Setup banner**: First-time users see a dismissible info banner ("Text-to-Speech is available but not configured.") with "Configure Now" link
3. **Settings gear button**: Cog icon next to TTS controls opens the ManageTtsConfig modal for reconfiguration
4. **Auto-restart on chapter change**: When navigating between chapters, active/paused TTS playback automatically restarts for the new chapter
5. **i18n keys added**: `book-reader.tts.settingsTooltip`, `setupPrompt`, `configureNow`

**Files modified**:
- `UI/Web/src/app/book-reader/_components/book-reader/book-reader.component.ts` — Added `openTtsConfig()`, `dismissBanner()`, `isTtsVisible$`, auto-restart logic in `loadChapter()`
- `UI/Web/src/app/book-reader/_components/book-reader/book-reader.component.html` — Banner + conditional controls/gear button
- `UI/Web/src/app/book-reader/_components/book-reader/book-reader.component.scss` — `.tts-setup-banner` styles
- `UI/Web/src/assets/langs/en.json` — 3 new TTS i18n keys

### Per-Series TTS Settings (Completed June 2026)

Implemented all plumbing to expose per-series TTS settings in the reader settings panel:

**Backend**: Added `TtsEnabled`, `TtsVoiceOverride`, `TtsSpeedOverride` to `UserReadingProfileDto.cs` — AutoMapper maps automatically from entity columns.

**Frontend Model**: Extended `ReadingProfile` interface with `ttsEnabled`, `ttsVoiceOverride?`, `ttsSpeedOverride?`.

**Frontend Service**: Wired TTS into `EpubReaderSettingsService`:
- Added to `BookReadingProfileFormGroup` type (nullable form controls)
- Initialized in `setupSettingsForm()` from profile values
- Persisted via `packReadingProfile()` — auto-saved through existing debounced pipeline

**Frontend UI**: Added collapsed TTS accordion panel to reader settings with:
- Enable/disable toggle switch
- Conditional voice dropdown (AsyncPipe + Observable from `TtsService.getVoices()`)
- Speed range slider (0.5x–4x)

**Bug Fix**: Corrected i18n key prefix in `tts-controls.component.html` (`'reader.tts.'` → `'book-reader.tts.'`).

**Files modified**:
- `Kavita.Models/DTOs/UserReadingProfileDto.cs` — Added TTS region with 3 properties
- `UI/Web/src/app/_models/preferences/reading-profiles.ts` — Extended ReadingProfile interface
- `UI/Web/src/app/_services/epub-reader-settings.service.ts` — FormGroup type, setupSettingsForm(), packReadingProfile()
- `UI/Web/src/app/book-reader/_components/reader-settings/reader-settings.component.html` — TTS accordion panel
- `UI/Web/src/app/book-reader/_components/reader-settings/reader-settings.component.ts` — Imports + TtsService injection
- `UI/Web/src/assets/langs/en.json` — 7 new i18n keys under `"reader-settings"`
- `UI/Web/src/app/book-reader/_components/tts-controls/tts-controls.component.html` — Fixed i18n prefix on lines 19/34

### Docker Build Fixes (June 2026)

Fixed the Docker image build so it produces a working container:

**Problem**: The published binary was named `Kavita.Server` but `entrypoint.sh` expected `./Kavita`, causing "No such file or directory" on startup. Additionally, `/kavita/config/` didn't exist in the container, so copying `appsettings.json` failed.

**Fixes**:
1. **AssemblyName**: Added `<AssemblyName>Kavita</AssemblyName>` to `Kavita.Server.csproj` — ensures published binary is always named `Kavita` regardless of build method (direct publish, CI/CD, Docker)
2. **Build script**: Updated `build.sh` rename step to be conditional (`if [ -f Kavita.Server ]`) since AssemblyName now handles naming; serves as no-op fallback for backward compatibility
3. **Entrypoint**: Added `mkdir -p /kavita/config` before copying appsettings.json in `entrypoint.sh`

**Files modified**:
- `Kavita.Server/Kavita.Server.csproj` — Added `<AssemblyName>Kavita</AssemblyName>`
- `build.sh` — Conditional rename logic (lines ~96-107)
- `entrypoint.sh` — Create `/kavita/config/` directory before config copy

**Verification**: Confirmed via Docker .NET SDK container that publish produces `Kavita` executable with correct ELF magic bytes. TypeScript type-check passes for all TTS-related files (no new errors). Full Angular build blocked by npm 9.x incompatibility with Angular CLI v21 on this host — requires newer npm or Docker-based build.

### Per-Series TTS Settings Bug Fix (June 2026)

**Problem**: `UpdateReaderProfileFields()` in `ReadingProfileService.cs` was missing the three new TTS fields (`TtsEnabled`, `TtsVoiceOverride`, `TtsSpeedOverride`). This meant per-series TTS settings were never actually saved to the database when users updated their reading profile. The test `ReadingProfileServiceTest.UpdateFields_UpdatesAll` caught this via random field comparison.

**Fix**: Added TTS field assignments to `UpdateReaderProfileFields()` in the "TTS Reader" section, matching the existing pattern for Book/PDF reader fields.

**Files modified**:
- `Kavita.Services/Reading/ReadingProfileService.cs` — Added 3 lines copying TTS fields from DTO to entity

**Verification**: Full test suite passes (2383 tests: 0 failures, 6 pre-existing skips).

### EF Core Migration Crash Fix (June 2026)

**Problem**: App crashed on startup with `InvalidOperationException: Navigation 'AppUser.UserTtsConfig' was not found` during EF Core migrations. The `20260617_AddTtsFeature` migration's Designer.cs configured `b.Navigation("UserTtsConfig")` on the `AppUser` entity, but `AppUser.cs` had no corresponding `UserTtsConfig` navigation property.

**Root cause**: When the TTS migration was created, `.WithOne()` in `DataContext.cs` was unqualified (no lambda). EF Core emitted a model snapshot expecting a navigation named `UserTtsConfig`, but the entity itself was never updated with the property.

**Fix** (3 files):
1. `Kavita.Models/Entities/User/AppUser.cs` — Added `public UserTtsConfig? UserTtsConfig { get; set; }` navigation property
2. `Kavita.Database/DataContext.cs` — Updated relationship config from `.WithOne()` to `.WithOne(a => a.UserTtsConfig)`
3. `Kavita.Database/Migrations/20260617_AddTtsFeature.Designer.cs` — Updated `.WithOne()` to `.WithOne("UserTtsConfig")` in the migration snapshot to match the new navigation

**Verification**: Docker build with `--no-cache` succeeded, container starts cleanly, all migrations run, health endpoint returns `200`.

### DI Fixture — SQLite Schema Fix (June 2026)

**Problem**: `ServiceResolutionFixture.cs` created an in-memory SQLite database (`UseSqlite("Data Source=:memory:")`) but never applied EF Core migrations. All Kavita entities with `IHasConcurrencyToken` (e.g., `AppUser`, `ServerSetting`, `CollectionTag`, `SeriesMetadata`, `UserTtsConfig`) define a `RowVersion` column via `[ConcurrencyCheck]`. When the in-memory SQLite had no tables, resolving Identity-related services (like `UserManager<AppUser>`) threw:
```
SQLite Error 1: no such column: u.RowVersion
```

**Fix**: Added `context.Database.EnsureCreated()` after building the service provider. This creates all tables directly from the EF Core model — no migration history needed — matching the pattern already used in `Kavita.Database.Tests/AbstractDbTest.cs`.

**Files modified**:
- `Kavita.Server.Tests/DI/ServiceResolutionFixture.cs` — Added 3 lines after `BuildServiceProvider()`:
  ```csharp
  using var scope = ServiceProvider.CreateScope();
  var context = scope.ServiceProvider.GetRequiredService<DataContext>();
  context.Database.EnsureCreated();
  ```

**Verification**: `dotnet test Kavita.Server.Tests --filter "FullyQualifiedName~DI"` — **83/83 passed** (0 failures). Full Server.Tests suite: 170/171 passed (1 pre-existing skip).

---

### Container Build Infrastructure (June 2026)

Added multi-stage Dockerfile and docker-compose so you can build from source without any host dependencies:

- **`Dockerfile.build`** — Multi-stage build that compiles everything in containers:
  - Stage 1 (`node:22-bookworm`): `npm ci && npm run prod` for Angular frontend
  - Stage 2 (`mcr.microsoft.com/dotnet/sdk:10.0`): `dotnet restore && dotnet publish -r linux-x64 --self-contained` + copies frontend into wwwroot
  - Stage 3 (`ubuntu:noble`): Minimal runtime with libicu, libgdiplus, curl — runs the published binary
- **`docker-compose.yml`** — One-command run: `docker compose up --build -d` (edit volume paths to match your media folders)
- **`.dockerignore`** — Excludes bin/, obj/, node_modules/, .git/, test projects from Docker context

**Usage**:
```bash
# Build + start in one shot:
docker compose up --build -d

# Or just the image (no compose):
docker build -f Dockerfile.build -t kavita .
docker run -p 5000:5000 -v kavita-data:/kavita/config kavita
```

**Verification**: Full `docker build` succeeds, container starts cleanly, health endpoint returns HTTP 200.

### TTS Settings Tab — apiKey Validator Fix (June 2026)

Added missing `Validators.maxLength(1024)` to the `apiKey` FormControl and `[class.is-invalid]` Bootstrap binding to the API key input in the TTS settings tab component, aligning it with the other form fields.

**Files modified**:
- `UI/Web/src/app/user-settings/manage-tts-settings/manage-tts-settings.component.ts` — Added validator to `apiKey` control
- `UI/Web/src/app/user-settings/manage-tts-settings/manage-tts-settings.component.html` — Added `[class.is-invalid]` binding to apiKey input

**Verification**: TypeScript type-check passes (0 errors in modified files; pre-existing luxon/jasmine type issues are unrelated).

### TTS Settings Tab — TranslocoModule Fix (June 2026)

**Problem**: Docker build failed at the Angular frontend stage with `NG8004: No pipe found with name 'transloco'`. The component imported `TranslocoDirective` but the template uses `| transloco` which is the **pipe**, not the directive.

**Fix**: Changed import from `TranslocoDirective` to `TranslocoModule` (which provides both the pipe and directive). This is the standard pattern used across all Kavita standalone components.

**Files modified**:
- `UI/Web/src/app/user-settings/manage-tts-settings/manage-tts-settings.component.ts` — Line 6: import, Line 10: imports array

**Verification**: Full Docker build succeeds (`docker compose up --build`), container starts cleanly on port 5000.

**Lesson for future components**: When a template uses `| transloco`, always import `TranslocoModule` (not just `TranslocoDirective`). The module re-exports both the pipe and directive, so it's the correct choice for standalone components using i18n pipes in templates.

### TTS Settings Tab — Voice Field Changed to Text Input (June 2026)

**Change**: Replaced the voice dropdown (which had hardcoded OpenAI voices + fetched server voices) with a free-text input. Users can now type any voice name they want, supporting custom TTS servers with arbitrary voice names.

**Files modified**:
- `UI/Web/src/app/user-settings/manage-tts-settings/manage-tts-settings.component.ts` — Removed `effect()` for voice fetching, removed `fetchVoices()`, removed `TtsVoiceDto` import, removed `voices` signal
- `UI/Web/src/app/user-settings/manage-tts-settings/manage-tts-settings.component.html` — Replaced `<select>` with `<input type="text">` for voice field

### Migration RowVersion Fix — Separate Migration (June 2026)

**Problem**: The `20260617_AddTtsFeature` migration's `CreateTable("UserTtsConfigs")` was missing the `RowVersion` column, even though `UserTtsConfig` entity has `[ConcurrencyCheck] public uint RowVersion { get; set; }`. This caused `SqliteException: no such column: u.RowVersion` at runtime.

**Why modifying the existing migration doesn't work**: EF Core tracks applied migrations in `__EFMigrationsHistory`. Once `20260617_AddTtsFeature` is recorded as applied, modifying its `.cs` file has zero effect — EF Core skips it entirely. The fix must be a **new** migration that adds the missing column via `AddColumn`.

**Fix**: Reverted `RowVersion` from `20260617_AddTtsFeature.cs`/`.Designer.cs` (restoring original CreateTable), then created new migration `20260622_AddUserTtsConfigRowVersion` that adds the column via `migrationBuilder.AddColumn<uint>()`.

**Files modified**:
- `Kavita.Database/Migrations/20260617_AddTtsFeature.cs` — Reverted: removed RowVersion from CreateTable (back to original)
- `Kavita.Database/Migrations/20260617_AddTtsFeature.Designer.cs` — Reverted: removed RowVersion property from UserTtsConfig snapshot

**Files created**:
- `Kavita.Database/Migrations/20260622_AddUserTtsConfigRowVersion.cs` — New migration adding RowVersion via AddColumn
- `Kavita.Database/Migrations/20260622_AddUserTtsConfigRowVersion.Designer.cs` — Model snapshot including RowVersion

**Verification**: Database tests: 64/64 passed. DI resolution tests: 83/83 passed. TTS service tests: 14/14 passed. Build: 0 errors.

### Migration RowVersion IsConcurrencyToken Fix (June 2026)

**Problem**: The `RowVersion` property in `20260622_AddUserTtsConfigRowVersion.Designer.cs` was missing `.IsConcurrencyToken()`. Without it, EF Core treats the column as a regular integer rather than a concurrency token — meaning UPDATE statements won't include `WHERE RowVersion = @originalValue`, defeating optimistic concurrency control.

**Fix**: Added `.IsConcurrencyToken()` between `b.Property<uint>("RowVersion")` and `.HasColumnType("INTEGER")` in the UserTtsConfig entity section of the migration Designer.cs file. Verified `DataContextModelSnapshot.cs` does not contain a separate UserTtsConfig entry (entity is captured only by the migration chain).

**Files modified**:
- `Kavita.Database/Migrations/20260622_AddUserTtsConfigRowVersion.Designer.cs` — Added `.IsConcurrencyToken()` to RowVersion property

**Verification**: Full test suite: 2,493/2,505 passing (0 new failures, 12 pre-existing skips). Database tests: 64/64 passed. DI resolution tests: 83/83 passed. TTS service tests: 14/14 passed. Build: 0 errors. **Needs Docker rebuild + runtime smoke test to confirm `PUT /api/tts/config` works in container.**

### TTS Playback Start Bugs Fix (June 2026)

**Problem**: Four bugs prevented users from starting TTS playback in the book reader:
1. No start button — only pause/stop buttons existed, so idle/completed states had no way to begin playback
2. `chapterId` was never passed to `<app-tts-controls>`, so even if a start method existed it would have no target chapter
3. On initial page load (not chapter navigation), TTS never auto-started even when enabled in the profile
4. `isTtsVisible$` only checked for server URL, ignoring the per-series `profile.ttsEnabled` toggle

**Fix**:
- **Bug #1**: Added `chapterId` input (`input<number>(0)`), `canStart` computed signal (true when state is `'idle'` or `'completed'` and chapterId > 0), `onStart()` method, and a play button (`fa fa-play-circle`) in the template
- **Bug #2**: Added `[chapterId]="chapterId"` binding on `<app-tts-controls>` in book-reader template
- **Bug #3**: Added auto-start trigger at end of `setupBookReader()` guarded by `firstLoad` flag, checking `profile?.ttsEnabled` before calling `this.ttsPlayback.startStream(this.chapterId)`
- **Bug #4**: `isTtsVisible$` now only checks `config?.serverUrl` — controls show whenever a server is configured. `profile.ttsEnabled` controls auto-start only, not visibility. (Previous design requiring both conditions created a UX dead-end: users who configured a server but hadn't enabled TTS per-series saw nothing — no banner, no controls, no gear button — because the banner auto-dismissed when `serverUrl` existed.)

**Files modified**:
- `UI/Web/src/app/book-reader/_components/tts-controls/tts-controls.component.ts` — Added `chapterId` input, `canStart` computed, `onStart()` method; removed unused `Input` import
- `UI/Web/src/app/book-reader/_components/tts-controls/tts-controls.component.html` — Added start/play button before pause/stop buttons
- `UI/Web/src/app/book-reader/_components/book-reader/book-reader.component.ts` — Fixed `isTtsVisible$` with `combineLatest` + `toObservable`; added auto-start TTS logic in `setupBookReader()`; added `combineLatest` to rxjs imports, `toObservable` to rxjs-interop imports
- `UI/Web/src/app/book-reader/_components/book-reader/book-reader.component.html` — Added `[chapterId]="chapterId"` binding on `<app-tts-controls>`
- `UI/Web/src/assets/langs/en.json` — Added `"play": "Play"` i18n key under `book-reader.tts`

**Verification**: TypeScript type-check: 0 errors in modified source files. Code review approved, QA passed with full state machine verification across all 6 TTS states (`idle`, `connecting`, `playing`, `paused`, `error`, `completed`). 14 new unit tests added for button visibility logic and error recovery flow.
