using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Confluent.SchemaRegistry;
using Grpc.Net.Client;
using Maichess.Database.V1;
using Maichess.Engine.V1;
using Maichess.User.V1;
using MaichessAnticheatService.Analysis;
using MaichessAnticheatService.Cases;
using MaichessAnticheatService.Data;
using MaichessAnticheatService.Detection;
using MaichessAnticheatService.Kafka;
using MaichessAnticheatService.Rest;
using MaichessAnticheatService.Stream;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

DotNetEnv.Env.Load();
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string anticheatDbUrl = builder.Configuration["Services:AnticheatDatabase"]
    ?? throw new InvalidOperationException("Services:AnticheatDatabase is not configured");
string matchDbUrl = builder.Configuration["Services:MatchDatabase"]
    ?? throw new InvalidOperationException("Services:MatchDatabase is not configured");
string engineUrl = builder.Configuration["Services:EngineService"]
    ?? throw new InvalidOperationException("Services:EngineService is not configured");
string userServiceUrl = builder.Configuration["Services:UserService"]
    ?? throw new InvalidOperationException("Services:UserService is not configured");
string jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured");

DetectionOptions detection = builder.Configuration.GetSection("Detection").Get<DetectionOptions>()
    ?? new DetectionOptions();
AnalysisOptions analysis = builder.Configuration.GetSection("Analysis").Get<AnalysisOptions>()
    ?? new AnalysisOptions();

builder.Services.AddSingleton(detection);
builder.Services.AddSingleton(analysis);
builder.Services.AddSingleton<IAnticheatStore>(
    new DatabaseAnticheatStore(new Database.DatabaseClient(GrpcChannel.ForAddress(anticheatDbUrl))));
builder.Services.AddSingleton<IMatchReader>(
    new DatabaseMatchReader(new Database.DatabaseClient(GrpcChannel.ForAddress(matchDbUrl))));
builder.Services.AddSingleton<IEngineAnalyzer>(sp => new GrpcEngineAnalyzer(
    new Bots.BotsClient(GrpcChannel.ForAddress(engineUrl)),
    sp.GetRequiredService<AnalysisOptions>(),
    sp.GetRequiredService<DetectionOptions>()));
builder.Services.AddSingleton<IDevGate>(
    new UserServiceDevGate(new Users.UsersClient(GrpcChannel.ForAddress(userServiceUrl))));
builder.Services.AddSingleton(sp => new GameAnalyzer(
    sp.GetRequiredService<IEngineAnalyzer>(),
    sp.GetRequiredService<DetectionOptions>(),
    () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
builder.Services.AddSingleton(sp => new AnticheatStreamProcessor(sp.GetRequiredService<DetectionOptions>()));

// Bounded so a flood of finished games applies backpressure-by-dropping (the
// consumer logs drops) instead of growing without limit; analysis is rebuildable
// by replaying the topic with a fresh consumer group.
var analysisQueue = Channel.CreateBounded<FinishedMatch>(
    new BoundedChannelOptions(1000) { SingleReader = true });
builder.Services.AddSingleton(analysisQueue.Reader);
builder.Services.AddSingleton(analysisQueue.Writer);

if (builder.Configuration.GetValue("Kafka:Enabled", false))
{
    // The platform injects KAFKA_BOOTSTRAP / SCHEMA_REGISTRY_URL into every pod when
    // kafka.enabled (see helm maichess.kafkaEnv), exactly as the match-manager
    // consumers read them; honour the same env so wiring stays uniform.
    string bootstrap = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP") ?? "kafka:9092";
    string registryUrl = Environment.GetEnvironmentVariable("SCHEMA_REGISTRY_URL")
        ?? "http://schema-registry:8081";

    builder.Services.AddSingleton<ISchemaRegistryClient>(
        _ => new CachedSchemaRegistryClient(new SchemaRegistryConfig { Url = registryUrl }));
    builder.Services.AddSingleton<ICheatEventProducer>(sp =>
        new KafkaCheatEventProducer(bootstrap, sp.GetRequiredService<ISchemaRegistryClient>()));
    builder.Services.AddHostedService(sp => new MatchEventConsumer(
        bootstrap,
        sp.GetRequiredService<AnticheatStreamProcessor>(),
        sp.GetRequiredService<CaseService>(),
        sp.GetRequiredService<ChannelWriter<FinishedMatch>>(),
        sp.GetRequiredService<ILogger<MatchEventConsumer>>()));
    builder.Services.AddHostedService(sp => new AnalysisWorker(
        sp.GetRequiredService<ChannelReader<FinishedMatch>>(),
        sp.GetRequiredService<IMatchReader>(),
        sp.GetRequiredService<GameAnalyzer>(),
        sp.GetRequiredService<CaseService>(),
        sp.GetRequiredService<ILogger<AnalysisWorker>>()));
}
else
{
    builder.Services.AddSingleton<ICheatEventProducer>(new NoopCheatEventProducer());
}

builder.Services.AddSingleton(sp => new CaseService(
    sp.GetRequiredService<IAnticheatStore>(),
    sp.GetRequiredService<ICheatEventProducer>(),
    sp.GetRequiredService<DetectionOptions>(),
    () => Guid.NewGuid().ToString(),
    () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue("access_token", out string? token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

string otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
    ?? "http://otel-collector:4317";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("anticheat-service"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddGrpcClientInstrumentation()
        .AddOtlpExporter(exporter => exporter.Endpoint = new Uri(otlpEndpoint)));

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok());
app.MapAnticheatEndpoints();

await app.RunAsync();
