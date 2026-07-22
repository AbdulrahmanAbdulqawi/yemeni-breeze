using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Features.Auth;
using YemeniBreeze.Api.Features.Contact;
using YemeniBreeze.Api.Features.Email;
using YemeniBreeze.Api.Features.Events;
using YemeniBreeze.Api.Features.Gallery;
using YemeniBreeze.Api.Features.Registrations;
using YemeniBreeze.Api.Features.Settings;
using YemeniBreeze.Api.Features.Storage;
using YemeniBreeze.Api.Features.Uploads;

var builder = WebApplication.CreateBuilder(args);

const long maxUploadBytes = 210L * 1024 * 1024; // 210 MB — headroom over the 200 MB file limit
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maxUploadBytes);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = maxUploadBytes;
    o.ValueLengthLimit = int.MaxValue;
});

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")
        ?? "Data Source=yemenibreeze.db"));

builder.Services
    .AddIdentityCore<IdentityUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddSingleton<StorageService>();
builder.Services.AddSingleton<ImageService>();
builder.Services.AddSingleton<EmailService>();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
            ?? ["http://localhost:4200"])
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

app.UseCors();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapEventsEndpoints();
app.MapRegistrationsEndpoints();
app.MapGalleryEndpoints();
app.MapContactEndpoints();
app.MapUploadsEndpoints();
app.MapSettingsEndpoints();

using (var scope = app.Services.CreateScope())
{
    await SeedData.RunAsync(scope.ServiceProvider);
}

app.Run();

public partial class Program;
