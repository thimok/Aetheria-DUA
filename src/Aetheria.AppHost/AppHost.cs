using System.Net.Http.Json;

var builder = DistributedApplication.CreateBuilder(args);

var sqlPasswordParameter = builder.AddParameter("sql-password", secret: true);

var sql = builder
    .AddSqlServer("sql", password: sqlPasswordParameter)
    .WithDataVolume("aetheria-sql-data")
    .WithLifetime(ContainerLifetime.Persistent);

var umbracoDatabase = sql.AddDatabase("umbracoDbDSN", "AetheriaUmbraco");

var operationsCache = builder
    .AddRedis("operations-cache")
    .WithDataVolume("aetheria-operations-redis-data")
    .WithPersistence(interval: TimeSpan.FromSeconds(5), keysChangedThreshold: 1)
    .WithRedisInsight();

var operationsApi = builder
    .AddProject<Projects.Aetheria_Operations_Api>("operations-api")
    .WithReference(operationsCache).WaitFor(operationsCache)
    .WithHttpHealthCheck("/health")
    .WithHttpCommand(
        path: "/api/demo/reset",
        displayName: "Reset park operations",
        commandOptions: new HttpCommandOptions
        {
            Description = "Restore attraction wait times, statuses and demo settings to their defaults.",
            ConfirmationMessage = "Reset all live park operational data?",
            IconName = "ArrowReset",
            ResultMode = HttpCommandResultMode.Auto
        })
    .WithHttpCommand(
        path: "/api/demo/wait-time",
        displayName: "Set attraction wait time",
        commandName: "set-wait-time",
        commandOptions: new HttpCommandOptions
        {
            Method = HttpMethod.Put,
            Description = "Manually override an attraction wait time.",
            IconName = "Clock",
            ResultMode = HttpCommandResultMode.Auto,
            Arguments =
            [
                new InteractionInput
                {
                    Name = "attraction",
                    Label = "Attraction",
                    InputType = InputType.Choice,
                    Required = true,
                    Value = "clockwork-citadel",

                    Options =
                    [
                        new KeyValuePair<string, string>("clockwork-citadel", "Clockwork Citadel"),

                        new KeyValuePair<string, string>("stormwing", "Stormwing"),

                        new KeyValuePair<string, string>("deepwood-expedition", "Deepwood Expedition"),

                        new KeyValuePair<string, string>("orbitfall", "Orbitfall")
                    ]
                },
                new InteractionInput
                {
                    Name = "waitTime",
                    Label = "Wait time (minutes)",
                    InputType = InputType.Number,
                    Required = true,
                    Value = "30"
                }
            ],
            PrepareRequest = context =>
            {
                var attractionId = context.Arguments.GetString("attraction")!;

                var waitTime = context.Arguments.GetInt32("waitTime");

                context.Request.Content = JsonContent.Create(new
                    {
                        AttractionId = attractionId,

                        WaitTimeMinutes = waitTime
                    });

                return Task.CompletedTask;
            }
        });

var simulator = builder
    .AddProject<Projects.Aetheria_WaitTimeSimulator>("wait-time-simulator")
    .WithReference(operationsApi).WaitFor(operationsApi)
    .WithHttpHealthCheck("/health")
    .WithHttpCommand(
        path: "/api/simulation/resume",
        displayName: "Resume simulation",
        commandName: "resume-simulation",
        commandOptions: new HttpCommandOptions
        {
            Description = "Automatically advance the park by 15 simulated minutes every 5 seconds.",
            IconName = "Play",
            ResultMode = HttpCommandResultMode.Auto
        })
    .WithHttpCommand(
        path: "/api/simulation/pause",
        displayName: "Pause simulation",
        commandName: "pause-simulation",
        commandOptions: new HttpCommandOptions
        {
            Description = "Pause automatic park-time progression.",
            IconName = "Pause",
            ResultMode = HttpCommandResultMode.Auto
        })
    .WithHttpCommand(
        path: "/api/simulation/advance",
        displayName: "Advance 15 park minutes",
        commandName: "advance-simulation",
        commandOptions: new HttpCommandOptions
        {
            Description = "Advance exactly one simulated 15-minute interval.",
            IconName = "FastForward",
            ResultMode = HttpCommandResultMode.Auto
        })
    .WithHttpCommand(
        path: "/api/simulation/reset",
        displayName: "Reset simulation",
        commandName: "reset-simulation",
        commandOptions: new HttpCommandOptions
        {
            Description = "Reset simulated time to 09:00, scenario to Normal, and pause.",
            ConfirmationMessage = "Reset the park simulator?",
            IconName = "ArrowReset",
            ResultMode = HttpCommandResultMode.Auto
        })
    .WithHttpCommand(
        path: "/api/simulation/scenario",
        displayName: "Change park scenario",
        commandName: "change-scenario",
        commandOptions: new HttpCommandOptions
        {
            Method = HttpMethod.Put,
            Description = "Change how simulated guest demand behaves.",
            IconName = "WeatherSunny",
            ResultMode = HttpCommandResultMode.Auto,
            Arguments =
            [
                new InteractionInput
                {
                    Name = "scenario",
                    Label = "Park scenario",
                    InputType = InputType.Choice,
                    Required = true,
                    Value = "Normal",

                    Options =
                    [
                        new KeyValuePair<string, string>("Quiet", "Quiet morning"),

                        new KeyValuePair<string, string>("Normal", "Normal operations"),

                        new KeyValuePair<string, string>("Peak", "Peak crowds"),

                        new KeyValuePair<string, string>("Rain", "Rain"),

                        new KeyValuePair<string, string>("Closing", "Closing time")
                    ]
                }
            ],

            PrepareRequest = context =>
            {
                var scenario = context.Arguments.GetString("scenario")!;

                context.Request.Content = JsonContent.Create(new
                {
                    Scenario = scenario
                });

                return Task.CompletedTask;
            }
        });

builder
    .AddProject<Projects.Aetheria_Web>("web")
    .WithReference(umbracoDatabase).WaitFor(umbracoDatabase)
    .WithReference(operationsApi).WaitFor(operationsApi)
    .WithEnvironment("ConnectionStrings__umbracoDbDSN_ProviderName", "Microsoft.Data.SqlClient")
    .WithHttpHealthCheck("/health");

builder.Build().Run();