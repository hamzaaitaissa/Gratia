using Gratia.Application;
using Gratia.Application.Command;
using Gratia.Application.Interfaces;
using Gratia.Application.Services;
using Gratia.Domain.Repositories;
using Gratia.Infrastructure.Data;
using Gratia.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

//adding dbcontext
builder.Services.AddDbContext<GratiaDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

//addin JWt Config
// The signing key is a secret: user-secrets in development, the AppSettings__Token environment variable elsewhere
var jwtKey = builder.Configuration.GetValue<string>("AppSettings:Token");
if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "AppSettings:Token is not configured. Run 'dotnet user-secrets set AppSettings:Token <key> --project Gratia.API' " +
        "or set the AppSettings__Token environment variable (at least 64 characters, HS512).");
}
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});


// Add services to the container.
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();
//builder.Services.AddScoped<ICompanyCommandService, CompanyCommandService>();
builder.Services.AddApplicationService();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ITransactionRepository,TransactionRepository>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IPasswordHashingService, PasswordHashingService>();
builder.Services.AddScoped<IInvitationRepository, InvitationRepository>();
builder.Services.AddTransient<IEmailService, EmailService>();
builder.Services.AddScoped<IInvitationService,  InvitationService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddLogging();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
