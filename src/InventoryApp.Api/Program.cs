using System.Text;
using InventoryApp.Api.Infrastructure;
using InventoryApp.Api.Services;
using InventoryApp.Application;
using InventoryApp.Application.Abstractions;
using InventoryApp.Contracts.Security;
using InventoryApp.Infrastructure;
using InventoryApp.Infrastructure.Security;
using InventoryApp.Infrastructure.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Kestrel serves HTTP/2 for native gRPC and HTTP/1.1 for gRPC-Web (used by mobile clients).
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(endpoint =>
        endpoint.Protocols = HttpProtocols.Http1AndHttp2);
});

builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.Interceptors.Add<ExceptionInterceptor>();
    options.MaxReceiveMessageSize = 8 * 1024 * 1024;
});

builder.Services.AddGrpcReflection();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserAccessor>();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationServices();

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

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
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

// One policy per permission, from the contract shared with the client.
builder.Services.AddAuthorization(options => options.AddInventoryPolicies());

// CORS is only needed for gRPC-Web callers; the MAUI app is not bound by browser CORS,
// but a Blazor WebAssembly front end or the dev browser tooling would be.
builder.Services.AddCors(options =>
{
    options.AddPolicy("GrpcWeb", policy => policy
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader()
        .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding"));
});

var app = builder.Build();

// Create/migrate the database and load sample data on start-up.
await using (var scope = app.Services.CreateAsyncScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.InitialiseAsync();
}

app.UseSerilogRequestLogging();
app.UseRouting();
app.UseCors("GrpcWeb");
app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });
app.UseAuthentication();
app.UseAuthorization();

app.MapGrpcService<AuthenticationGrpcService>().EnableGrpcWeb().RequireCors("GrpcWeb");
app.MapGrpcService<CategoryGrpcService>().EnableGrpcWeb().RequireCors("GrpcWeb");
app.MapGrpcService<SupplierGrpcService>().EnableGrpcWeb().RequireCors("GrpcWeb");
app.MapGrpcService<ProductGrpcService>().EnableGrpcWeb().RequireCors("GrpcWeb");
app.MapGrpcService<InventoryGrpcService>().EnableGrpcWeb().RequireCors("GrpcWeb");
app.MapGrpcService<PurchaseGrpcService>().EnableGrpcWeb().RequireCors("GrpcWeb");
app.MapGrpcService<SalesGrpcService>().EnableGrpcWeb().RequireCors("GrpcWeb");
app.MapGrpcService<ReportGrpcService>().EnableGrpcWeb().RequireCors("GrpcWeb");

if (app.Environment.IsDevelopment())
{
    app.MapGrpcReflectionService();   // lets grpcurl / Postman discover the services
}

app.MapGet("/", () => Results.Ok(new
{
    service = "InventoryApp gRPC API",
    status = "running",
    utc = DateTime.UtcNow
}));

app.MapGet("/health", () => Results.Ok("Healthy"));

app.Run();
