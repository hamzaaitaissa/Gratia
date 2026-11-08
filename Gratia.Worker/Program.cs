using Gratia.Application;
using Gratia.Application.Settings;
using Gratia.Infrastructure;
using Gratia.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

// The worker exists to reset points, so it refuses to start without a valid monthly allowance
builder.Services.AddOptions<PointsOptions>().ValidateOnStart();
builder.Services.AddHostedService<PointResetWorker>();

var host = builder.Build();
host.Run();
