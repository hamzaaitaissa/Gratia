using Gratia.Application.Interfaces;
using Gratia.Application.Services;
using Gratia.Domain.Repositories;
using Gratia.Infrastructure.Repositories;
using Gratia.Worker;
using Microsoft.EntityFrameworkCore;
using Gratia.Infrastructure.Data;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddScoped<IPointResetService, PointResetService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddHostedService<PointResetWorker>();
builder.Services.AddDbContext<GratiaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var host = builder.Build();
host.Run();
