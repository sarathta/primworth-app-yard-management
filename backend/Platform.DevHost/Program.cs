using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Platform.DevHost;
using Primworth_API.IRepository;
using Primworth_API.Repository;
using Primworth_API.Services.Permissions;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// Platform.DevHost — a stand-in for the platform so an application can be developed without it.
// Same auth shape, same tenant header, same provisioning hook, same response envelope.
// ─────────────────────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);
var dev = builder.Configuration.GetSection("Dev").Get<DevOptions>() ?? new DevOptions();
builder.Services.AddSingleton(dev);

using var startupLoggerFactory = LoggerFactory.Create(b => b.AddConsole());
var startupLogger = startupLoggerFactory.CreateLogger("DevHost");

// ── what the platform provides to applications ──────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<DapperContext>();
builder.Services.AddScoped<ISchemaResolverService, DevSchemaResolverService>();
builder.Services.AddScoped<IProjectAccessGuard, DevProjectAccessGuard>();
builder.Services.AddSingleton<IRolePermissionReader, DevRolePermissionReader>();
builder.Services.AddScoped<DevProvisioningService>();

// ── the applications: discovered from the referenced Apps.* projects ────────────────────────
var appAssemblies = AppDiscovery.LoadAppAssemblies();
foreach (var assembly in appAssemblies)
    AppDiscovery.RegisterServices(builder.Services, assembly, startupLogger);

var mvc = builder.Services.AddControllers(options =>
{
    options.Filters.Add<DevPermissionFilter>();
    options.Filters.Add<DevProjectAccessDeniedFilter>();
});
foreach (var assembly in appAssemblies)
    mvc.AddApplicationPart(assembly);

// ── auth: JWT bearer, and every endpoint requires a user unless it opts out ─────────────────
var key = Encoding.ASCII.GetBytes(dev.JwtKey);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
    };
});
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(dev.CorsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseDeveloperExceptionPage();
app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
DevEndpoints.Map(app);

if (dev.ProvisionOnStartup)
{
    using var scope = app.Services.CreateScope();
    var results = await scope.ServiceProvider.GetRequiredService<DevProvisioningService>().RunAllAsync(app.Logger);
    app.Logger.LogInformation("[DevHost] startup provisioning: {Count} run(s)", results.Count);
}

app.Logger.LogInformation("[DevHost] ready — Swagger at {Url}/swagger, sign in as {Email} / {Password}",
    builder.Configuration["Urls"], dev.User.Email, dev.User.Password);
app.Run();
