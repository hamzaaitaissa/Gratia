using Gratia.Application.Interfaces;
using Gratia.Application.Services;
using Gratia.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddScoped<IPointResetService, PointResetService>();
builder.Services.AddHostedService<PointResetWorker>();

var host = builder.Build();
host.Run();
