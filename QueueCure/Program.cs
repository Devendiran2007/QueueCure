using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QueueCure.Data;
using QueueCure.Hubs;
using QueueCure.Repositories;
using QueueCure.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Add SQL Server DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<QueueCureDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. Add Repositories and Services
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IQueueRepository, QueueRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IHistoricalDataRepository, HistoricalDataRepository>();
builder.Services.AddSingleton<IPredictionModel, StatisticalPredictionModel>();
builder.Services.AddScoped<IMLPredictionService, MLPredictionService>();
builder.Services.AddScoped<IQueueService, QueueService>();
builder.Services.AddScoped<IWhatsAppService, WhatsAppService>();
builder.Services.AddScoped<IQueueReliabilityService, QueueReliabilityService>();
builder.Services.AddScoped<IQueueImpactService, QueueImpactService>();
builder.Services.AddScoped<IPredictionExplanationService, PredictionExplanationService>();
builder.Services.AddScoped<IDelayDetectionService, DelayDetectionService>();
builder.Services.AddHostedService<DelayMonitoringBackgroundService>();
builder.Services.AddScoped<ISimulationEngine, SimulationEngine>();

// 3. Add controllers and OpenAPI support
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();

// 4. Add SignalR
builder.Services.AddSignalR();

// 5. Add Authentication with JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? "QueueCureSuperSecretSecurityKeyThatNeedsToBeLongEnough123!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "QueueCureApi";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "QueueCureClient";

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
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };

    // Support receiving token via Query String for SignalR connection if needed
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hub"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ReceptionistOnly", policy => policy.RequireRole("Receptionist"));
    options.AddPolicy("DoctorOnly", policy => policy.RequireRole("Doctor"));
    options.AddPolicy("StaffOnly", policy => policy.RequireRole("Receptionist", "Doctor"));
});

var app = builder.Build();

// Seed database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<QueueCureDbContext>();
        QueueCure.Data.DbInitializer.Initialize(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred seeding the DB.");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Serve files inside wwwroot (like index.html, css, js)
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Map controllers and SignalR hubs
app.MapControllers();
app.MapHub<QueueHub>("/hub/queue");

app.Run();
