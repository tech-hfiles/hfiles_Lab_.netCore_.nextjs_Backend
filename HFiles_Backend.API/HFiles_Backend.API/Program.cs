using System.Text;
using HFiles_Backend.API.Middleware;
using HFiles_Backend.API.Services;
using HFiles_Backend.API.Settings;
using HFiles_Backend.Domain.Entities.Labs;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MySqlConnector;
using Serilog;
using Microsoft.AspNetCore.Http.Features;

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

    builder.Configuration["Smtp:Host"] = Environment.GetEnvironmentVariable("SMTP_HOST");
    builder.Configuration["Smtp:Port"] = Environment.GetEnvironmentVariable("SMTP_PORT");
    builder.Configuration["Smtp:Username"] = Environment.GetEnvironmentVariable("SMTP_USER");
    builder.Configuration["Smtp:Password"] = Environment.GetEnvironmentVariable("SMTP_PASS");
    builder.Configuration["Smtp:From"] = Environment.GetEnvironmentVariable("SMTP_FROM");

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
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy.WithOrigins("http://localhost:3000")
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
      .AddPolicy("SuperAdminOrAdminPolicy", policy => policy.RequireRole("Super Admin","Admin"));
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

    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<AuditLogFilter>(); 
    });


    // DbContext
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseMySql(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection")),
            mysqlOptions => mysqlOptions.MigrationsAssembly("HFiles_Backend.Infrastructure")
        )
    );

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

    // Migrations
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Log.Information("MIGRATION DB_HOST: {DB_HOST}", Environment.GetEnvironmentVariable("DB_HOST"));
        Log.Information("MIGRATION DB_USER: {DB_USER}", Environment.GetEnvironmentVariable("DB_USER"));

        try
        {
            using var connection = new MySqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"));
            connection.Open();
            Log.Information("Migration connection successful!");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Migration connection failed");
        }

        db.Database.Migrate();
    }

    // Middleware
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    //app.UseStaticFiles(new StaticFileOptions
    //{
    //    FileProvider = new PhysicalFileProvider(
    //        Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "uploads")),
    //    RequestPath = "/uploads"
    //});

    app.UseRouting();

    app.UseSession();
    app.UseCors("AllowFrontend");
    app.UseAuthentication();
    app.UseAuthorization();

    //app.UseMiddleware<ExceptionLoggingMiddleware>();
    //app.UseMiddleware<ApiLoggingMiddleware>();

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
