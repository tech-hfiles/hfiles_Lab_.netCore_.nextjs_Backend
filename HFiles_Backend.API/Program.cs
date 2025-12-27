using System.Text;
using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.MySql;
using HFiles_Backend.API.Extensions;
using HFiles_Backend.API.Interfaces;
using HFiles_Backend.API.Middleware;
using HFiles_Backend.API.Services;
using HFiles_Backend.API.Settings;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Entities.Labs;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Domain.Interfaces.Clinics;
using HFiles_Backend.Infrastructure.Data;
using HFiles_Backend.Infrastructure.Repositories;
using HFiles_Main.API.Middlewares;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MySqlConnector;
using Serilog;

// Configure Serilog at the top
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/api.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting up application");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog
    builder.Host.UseSerilog();

    // Load .env file explicitly
    DotNetEnv.Env.Load();

    // Environment variables into configuration
    builder.Configuration["ConnectionStrings:DefaultConnection"] = $"Server={Environment.GetEnvironmentVariable("DB_HOST")};" +
                                                                 $"Port={Environment.GetEnvironmentVariable("DB_PORT")};" +
                                                                 $"Database={Environment.GetEnvironmentVariable("DB_NAME")};" +
                                                                 $"User={Environment.GetEnvironmentVariable("DB_USER")};" +
                                                                 $"Password={Environment.GetEnvironmentVariable("DB_PASSWORD")};";

    builder.Configuration["InternalAPI:ApiKey"] = Environment.GetEnvironmentVariable("INTERNAL_API_KEY");
    builder.Configuration["UserBackend:BaseUrl"] = Environment.GetEnvironmentVariable("USER_BACKEND_URL");

    builder.Configuration["Smtp:Host"] = Environment.GetEnvironmentVariable("SMTP_HOST");
    builder.Configuration["Smtp:Port"] = Environment.GetEnvironmentVariable("SMTP_PORT");
    builder.Configuration["Smtp:Username"] = Environment.GetEnvironmentVariable("SMTP_USER");
    builder.Configuration["Smtp:Password"] = Environment.GetEnvironmentVariable("SMTP_PASS");
    builder.Configuration["Smtp:From"] = Environment.GetEnvironmentVariable("SMTP_FROM");

    // Google OAuth Configuration
    builder.Configuration["GoogleOAuth:ClientId"] = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
        ?? throw new Exception("GOOGLE_CLIENT_ID missing");
    builder.Configuration["GoogleOAuth:ClientSecret"] = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET")
        ?? throw new Exception("GOOGLE_CLIENT_SECRET missing");
    builder.Configuration["Security:EncryptionKey"] = Environment.GetEnvironmentVariable("SECURITY_ENCRYPTION_KEY")
        ?? throw new Exception("SECURITY_ENCRYPTION_KEY missing");
    builder.Configuration["AppSettings:BaseUrl"] = Environment.GetEnvironmentVariable("APP_BASE_URL")
        ?? throw new Exception("APP_BASE_URL missing");
    builder.Configuration["AppSettings:FrontendUrl"] = Environment.GetEnvironmentVariable("APP_FRONTEND_URL")
        ?? throw new Exception("APP_FRONTEND_URL missing");

    // Log configuration status
    Log.Information("Google OAuth Configuration Loaded: ClientId Present={ClientIdPresent}",
        !string.IsNullOrEmpty(builder.Configuration["GoogleOAuth:ClientId"]));
    Log.Information("Security Encryption Key Present: {KeyPresent}",
        !string.IsNullOrEmpty(builder.Configuration["Security:EncryptionKey"]));
    Log.Information("App Base URL: {BaseUrl}", builder.Configuration["AppSettings:BaseUrl"]);

    builder.Configuration["Interakt:ApiUrl"] = Environment.GetEnvironmentVariable("INTERAKT_API_URL");
    builder.Configuration["Interakt:ApiKey"] = Environment.GetEnvironmentVariable("INTERAKT_API_KEY");

    builder.Configuration["JwtSettings:Key"] = Environment.GetEnvironmentVariable("JWT_KEY") ?? throw new Exception("JWT_KEY missing");
    builder.Configuration["JwtSettings:Issuer"] = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? throw new Exception("JWT_ISSUER missing");
    builder.Configuration["JwtSettings:Audience"] = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? throw new Exception("JWT_AUDIENCE missing");
    builder.Configuration["JwtSettings:DurationInMinutes"] = Environment.GetEnvironmentVariable("JWT_DURATION") ?? "180";

    Log.Information("DB Connection String: {ConnectionString}", builder.Configuration.GetConnectionString("DefaultConnection"));
    Log.Information("JWT Key Present: {KeyPresent}", !string.IsNullOrEmpty(builder.Configuration["JwtSettings:Key"]));

    try
    {
        using var connection = new MySqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"));
        connection.Open();
        Log.Information("Database connection successful!");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Connection failed");
    }

    // JWT Config binding
    builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

    // Session
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(180);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;
    });

    // CORS
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy.WithOrigins(allowedOrigins!)
            //policy.WithOrigins("http://localhost:3000") 
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });

    // Services
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
    builder.Services.AddLogging();
    builder.Services.AddAuthorizationBuilder()
      .AddPolicy("SuperAdminPolicy", policy => policy.RequireRole("Super Admin"))
      .AddPolicy("AdminPolicy", policy => policy.RequireRole("Admin"))
      .AddPolicy("MemberPolicy", policy => policy.RequireRole("Member"))
      .AddPolicy("SuperAdminOrAdminPolicy", policy => policy.RequireRole("Super Admin", "Admin"));
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<OtpVerificationStore>();
    builder.Services.Configure<FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = 1024 * 1024 * 500;
    });

    // Swagger + JWT
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "HFiles API", Version = "v1" });

        // Fix for schema ID conflicts (e.g., Labs.Signup vs Clinics.Signup)
        options.CustomSchemaIds(type => type.FullName);

        // JWT Bearer token support
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT token like this: Bearer {your token}"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

	// Scoped services
	builder.Services.AddScoped<IClinicHigh5AppointmentService, High5AppointmentRepository>();
	builder.Services.AddScoped<IClinicEnquiryRepository, ClinicEnquiryRepository>();
	builder.Services.AddScoped<ClinicEnquiryRepository, ClinicEnquiryRepository>();
	builder.Services.AddScoped<IPasswordHasher<LabSignup>, PasswordHasher<LabSignup>>();
    builder.Services.AddScoped<IPasswordHasher<LabSuperAdmin>, PasswordHasher<LabSuperAdmin>>();
    builder.Services.AddScoped<IPasswordHasher<LabMember>, PasswordHasher<LabMember>>();
    builder.Services.AddScoped<EmailService>();
    builder.Services.AddScoped<JwtTokenService>();
    builder.Services.Configure<WhatsappSettings>(builder.Configuration.GetSection("Interakt"));
    builder.Services.AddHttpClient<IWhatsappService, WhatsappService>();
    builder.Services.AddScoped<LabAuthorizationService>();
    builder.Services.AddScoped<LocationService>();
    builder.Services.AddScoped<S3StorageService>();
    builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, RoleBasedAuthorization>();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<AuditLogFilter>();
    builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>();
    builder.Services.AddScoped<AppointmentStatusService>();

    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<AuditLogFilter>();
    });

    // Clinic Services
    builder.Services.AddScoped<IClinicRepository, ClinicRepository>();
    builder.Services.AddScoped<ClinicRepository, ClinicRepository>();
    builder.Services.AddScoped<IPasswordHasher<ClinicSignup>, PasswordHasher<ClinicSignup>>();
    builder.Services.AddScoped<IClinicSuperAdminRepository, ClinicSuperAdminRepository>();
    builder.Services.AddScoped<ClinicSuperAdminRepository, ClinicSuperAdminRepository>();
    builder.Services.AddScoped<IPasswordHasher<ClinicSuperAdmin>, PasswordHasher<ClinicSuperAdmin>>();
    builder.Services.AddScoped<IClinicMemberRepository, ClinicMemberRepository>();
    builder.Services.AddScoped<ClinicMemberRepository, ClinicMemberRepository>();
    builder.Services.AddScoped<IClinicAuthorizationService, ClinicAuthorizationService>();
    builder.Services.AddScoped<IPasswordHasher<ClinicMember>, PasswordHasher<ClinicMember>>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<UserRepository, UserRepository>();
    builder.Services.AddScoped<IClinicBranchRepository, ClinicBranchRepository>();
    builder.Services.AddScoped<ClinicBranchRepository, ClinicBranchRepository>();
    builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
    builder.Services.AddScoped<AppointmentRepository, AppointmentRepository>();
    builder.Services.AddScoped<IClinicVisitRepository, ClinicVisitRepository>();
    builder.Services.AddScoped<ClinicVisitRepository, ClinicVisitRepository>();
    builder.Services.AddScoped<IClinicPrescriptionRepository, ClinicPrescriptionRepository>();
    builder.Services.AddScoped<ClinicPrescriptionRepository, ClinicPrescriptionRepository>();
    builder.Services.AddScoped<IClinicTreatmentRepository, ClinicTreatmentRepository>();
    builder.Services.AddScoped<ClinicTreatmentRepository, ClinicTreatmentRepository>();
    builder.Services.AddScoped<IClinicPatientRecordRepository, ClinicPatientRecordRepository>();
    builder.Services.AddScoped<ClinicPatientRecordRepository, ClinicPatientRecordRepository>();
    builder.Services.AddScoped<ClinicMedicalHistoryRepository, ClinicMedicalHistoryRepository>();
    builder.Services.AddScoped<ITokenBlacklistService, TokenBlacklistService>();
    builder.Services.AddHostedService<TokenCleanupBackgroundService>();
    builder.Services.AddScoped<IClinicPatientMedicalHistoryRepository, ClinicPatientMedicalHistoryRepository>();
    builder.Services.AddScoped<ClinicPatientMedicalHistoryRepository, ClinicPatientMedicalHistoryRepository>();
    builder.Services.AddScoped<IUniqueIdGeneratorService, UniqueIdGeneratorService>();
    builder.Services.AddScoped<IClinicStatisticsRepository, ClinicStatisticsRepository>();
    builder.Services.AddScoped<ClinicStatisticsRepository, ClinicStatisticsRepository>();
    builder.Services.AddScoped<IClinicStatisticsCacheService, ClinicStatisticsCacheService>();
    builder.Services.AddScoped<IHifi5PricingPackageRepository, Hifi5PricingPackageRepository>();
    builder.Services.AddScoped<IClinicMemberRecordRepository, ClinicMemberRecordRepository>();
    builder.Services.AddScoped<IClinicMemberRecordService, ClinicMemberRecordService>();
    // for calling the APis
	builder.Services.AddHttpContextAccessor(); // ← ADD THIS LINE
    // Add this line in your service registration
    builder.Services.AddScoped<IHigh5ChocheFormRepository, High5ChocheFormRepository>();

	// Register Repository
	builder.Services.AddScoped<IClinicGoogleTokenRepository, ClinicGoogleTokenRepository>();

    builder.Services.AddScoped<ICountryService, CountryService>();
    builder.Services.AddScoped<IHfidService, HfidService>();

    // Register Services
    builder.Services.AddScoped<IGoogleAuthService, GoogleAuthService>();
    builder.Services.AddScoped<IGoogleCalendarService, GoogleCalendarService>();

    // Register Background Service for automatic token refresh
    builder.Services.AddHostedService<GoogleTokenRefreshService>();


    // DbContext
    builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection")),
        mysqlOptions =>
        {
            mysqlOptions.MigrationsAssembly("HFiles_Backend.Infrastructure");
            mysqlOptions.CommandTimeout(300);
        }
    )
);


    // Hangfire setup
    builder.Services.AddHangfire(config =>
     config.UseStorage(
         new MySqlStorage(
             builder.Configuration.GetConnectionString("DefaultConnection"),
             new MySqlStorageOptions
             {
                 TransactionIsolationLevel = System.Transactions.IsolationLevel.ReadCommitted,
                 QueuePollInterval = TimeSpan.FromSeconds(15),
                 JobExpirationCheckInterval = TimeSpan.FromHours(6),
                 CountersAggregateInterval = TimeSpan.FromMinutes(15),
                 PrepareSchemaIfNecessary = true,
                 DashboardJobListLimit = 50000,
                 TransactionTimeout = TimeSpan.FromMinutes(5)
             }
         )
     )
 );



    builder.Services.AddHangfireServer();

    builder.Services.AddScoped<IGoogleCalendarService, GoogleCalendarService>();


    builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });


    // JWT Setup
    var tempJwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()
                          ?? throw new Exception("Failed to load JwtSettings");

    if (string.IsNullOrEmpty(tempJwtSettings.Key))
        throw new Exception("JWT secret key is missing");

    var key = Encoding.ASCII.GetBytes(tempJwtSettings.Key);

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = tempJwtSettings.Issuer,
            ValidAudience = tempJwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

    var app = builder.Build();



    app.UseHangfireDashboard();

    // Register recurring jobs after app startup
    //app.Lifetime.ApplicationStarted.Register(() =>
    //{
    //    using var scope = app.Services.CreateScope();
    //    var recurringJobs = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

    //    recurringJobs.AddOrUpdate<AppointmentStatusService>(
    //        "sweep-absent-appointments",
    //        service => service.SweepAbsentAppointmentsAsync(),
    //        "*/1 * * * *"
    //    );
    //});


    // Middleware
    if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseMiddleware<GlobalExceptionMiddleware>();

    app.UseRouting();
    app.UseSession();
    app.UseCors("AllowFrontend");
    app.UseAuthentication();
    app.UseJwtBlacklistMiddleware();
    app.UseAuthorization();

    app.MapControllers();
    app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
}
finally
{
    Log.CloseAndFlush();
}
