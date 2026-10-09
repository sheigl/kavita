using System;
using System.IO.Abstractions;
using System.Linq;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Helpers;
using Kavita.API.Services.Metadata;
using Kavita.API.Services.Plus;
using Kavita.API.Services.Reading;
using Kavita.API.Services.ReadingLists;
using Kavita.API.Services.Scanner;
using Kavita.API.Services.SignalR;
using Kavita.API.Store;
using Kavita.Database;
using Kavita.Server.Extensions;
using Kavita.Server.Logging;
using Kavita.Server.Middleware;
using Kavita.Server.Store;
using Kavita.Services.Helpers;
using Kavita.Services.Metadata;
using Kavita.Services.Plus;
using Kavita.Services.Plus.ScrobbleService;
using Kavita.Services.Reading;
using Kavita.Services.ReadingLists;
using Kavita.Services.Scanner;
using Kavita.Services.SignalR;
using Kavita.Models.DTOs.Internal;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace Kavita.Server.Tests.DI;

/// <summary>
/// Builds the real DI container (same pipeline as Startup.cs) with in-memory SQLite,
/// so we can verify all services resolve correctly without needing a real database.
/// </summary>
public class ServiceResolutionFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// All registered service interfaces that should be resolvable from the container.
    /// Add new service interfaces here when registering services in ApplicationServiceExtensions.cs.
    /// </summary>
    public static readonly Type[] ServicesToVerify =
    {
        typeof(ITokenService),
        typeof(IFileService),
        typeof(ICacheHelper),
        typeof(IStatsService),
        typeof(ITaskScheduler),
        typeof(ICacheService),
        typeof(IArchiveService),
        typeof(IBackupService),
        typeof(ICleanupService),
        typeof(IBookService),
        typeof(IVersionUpdaterService),
        typeof(IDownloadService),
        typeof(IReaderService),
        typeof(IReadingItemService),
        typeof(IAccountService),
        typeof(IEmailService),
        typeof(IBookmarkService),
        typeof(IThemeService),
        typeof(ISeriesService),
        typeof(IReadingListService),
        typeof(IDeviceService),
        typeof(IStatisticService),
        typeof(IMediaErrorService),
        typeof(IMediaConversionService),
        typeof(IStreamService),
        typeof(IRatingService),
        typeof(IPersonService),
        typeof(IReadingProfileService),
        typeof(IKoreaderService),
        typeof(IFontService),
        typeof(IAnnotationService),
        typeof(IOpdsService),
        typeof(IUrlValidationService),
        typeof(ICblExportService),
        typeof(ICblGithubService),
        typeof(ICblImportService),
        typeof(IScannerService),
        typeof(IProcessSeries),
        typeof(IMetadataService),
        typeof(IWordCountAnalyzerService),
        typeof(ILibraryWatcher),
        typeof(ITachiyomiService),
        typeof(ICollectionTagService),
        typeof(IFileSystem),
        typeof(IDirectoryService),
        typeof(IEventHub),
        typeof(IPresenceTracker),
        typeof(IImageService),
        typeof(ICoverDbService),
        typeof(ILocalizationService),
        typeof(ISettingsService),
        typeof(IAuthKeyService),
        typeof(IKavitaPlusApiService),
        typeof(IScrobbleRuleService),
        typeof(IScrobblingService),
        typeof(ILicenseService),
        typeof(IExternalMetadataService),
        typeof(ISmartCollectionSyncService),
        typeof(IWantToReadSyncService),
        typeof(IKavitaPlusAuditService),
        typeof(IKavitaPlusProviderHealthService),
        typeof(IOidcService),
        typeof(IReadingHistoryService),
        typeof(IClientDeviceService),
        typeof(IDeviceTrackingService),
        // TTS Service - the one that had the bug!
        typeof(ITtsService),
        // File cache & session services
        typeof(IFileCacheService),
        typeof(IReadingSessionService),
        typeof(IEntityNamingService),
        typeof(IActiveUserTrackerService),
        // Server-level services
        typeof(ILoggingService),
        typeof(IClientInfoAccessor),
        typeof(IUserContext),
        // Database
        typeof(IUnitOfWork),
        typeof(IDataContext),
    };

    public ServiceResolutionFixture()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var services = new ServiceCollection();

        // Register configuration and bind AppSettingsDto (needed by TokenService)
        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<AppSettingsDto>(configuration);

        // Set up in-memory SQLite for DataContext (avoids file-based config/cache.db)
        services.AddDbContext<DataContext>(options =>
            options.UseSqlite("Data Source=:memory:"));

        // Add Data Protection (registers IDataProtectionProvider, needed by TtsService)
        services.AddDataProtection()
            .SetApplicationName("Kavita");

        // Add mappings (AutoMapper)
        services.AddAutoMapper(typeof(Startup).Assembly);

        // Register application services (same as Startup.cs -> AddApplicationServices)
        services.AddScoped<ILoggingService, LoggingService>();
        services.AddSingleton<IClientInfoAccessor, ClientInfoAccessor>();
        services.AddScoped<UserContext>();
        services.AddScoped<IUserContext>(sp => sp.GetRequiredService<UserContext>());

        // Add all Kavita services
        Kavita.Services.Extensions.ApplicationServiceExtensions.AddKavitaServices(services);

        // Add database unit of work (normally done by AddKavitaDatabases)
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDataContext, DataContext>();

        // Add in-memory caching
        services.AddMemoryCache();

        // Add EasyCaching in-memory providers
        services.AddEasyCaching(options =>
        {
            options.UseInMemory("Favicon");
            options.UseInMemory("Publisher");
            options.UseInMemory("Library");
            options.UseInMemory("RevokedJwt");
            options.UseInMemory("LocaleOptions");
            options.UseInMemory("KavitaPlusExternalSeries");
            options.UseInMemory("License");
            options.UseInMemory("LicenseInfo");
            options.UseInMemory("KavitaPlusMatchSeries");
            options.UseInMemory("ProviderHealth");
        });

        // Identity services (needed for UserManager, SignInManager etc)
        services.AddIdentityCore<Models.Entities.User.AppUser>(opt =>
            {
                opt.Password.RequireNonAlphanumeric = false;
                opt.Password.RequireDigit = false;
                opt.Password.RequireLowercase = false;
                opt.Password.RequireUppercase = false;
                opt.Password.RequiredLength = 6;
                opt.SignIn.RequireConfirmedEmail = false;
                opt.Lockout.AllowedForNewUsers = true;
            })
            .AddTokenProvider<DataProtectorTokenProvider<Models.Entities.User.AppUser>>(TokenOptions.DefaultProvider)
            .AddRoles<Models.Entities.User.AppRole>()
            .AddRoleManager<RoleManager<Models.Entities.User.AppRole>>()
            .AddSignInManager<SignInManager<Models.Entities.User.AppUser>>()
            .AddRoleValidator<RoleValidator<Models.Entities.User.AppRole>>()
            .AddEntityFrameworkStores<DataContext>();

        services.AddSingleton<TicketSerializer>();
        services.AddSingleton<ITicketStore, CustomTicketStore>();

        // SignalR (registers IHubContext<MessageHub>, IHubContext<LogHub>, needed by EventHub)
        services.AddSignalR();

        // Hosting environment (needed by LocalizationService for content root path resolution)
        var hostingEnvironment = Substitute.For<IHostEnvironment>();
        hostingEnvironment.ContentRootPath.Returns(AppContext.BaseDirectory);
        services.AddSingleton(hostingEnvironment);

        // HybridCache (needed by ReadingSessionService — registered in Startup via AddHybridCache)
        services.AddHybridCache();

        // Build the service provider
        ServiceProvider = services.BuildServiceProvider();

        // Create database schema from EF Core model.
        // Without this, the in-memory SQLite has no tables and any query (e.g., Identity
        // UserManager resolving AppUser with RowVersion column) throws "no such column".
        using var scope = ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        if (ServiceProvider is IDisposable disposable)
            disposable.Dispose();
    }
}
