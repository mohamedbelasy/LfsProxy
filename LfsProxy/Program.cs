using LfsProxy.Middleware;
using LfsProxy.Models;
using LfsProxy.Services;
using Microsoft.Extensions.Options;
using Minio;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<LfsConfig>(builder.Configuration.GetSection("Lfs"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IMinioClient>(sp =>
{
    var config = sp.GetRequiredService<IOptions<LfsConfig>>().Value.S3;
    var endpoint = config.Endpoint;
    var useSsl = config.UseSsl;

    if (endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
        useSsl = true;
        endpoint = endpoint[8..];
    }
    else if (endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
    {
        useSsl = false;
        endpoint = endpoint[7..];
    }

    var client = new MinioClient()
        .WithEndpoint(endpoint)
        .WithCredentials(config.AccessKey, config.SecretKey);

    if (useSsl)
        client.WithSSL();

    return client.Build();
});

builder.Services.AddScoped<MinioService>();

builder.Services.AddSingleton<JsonLockService>();

/*builder.Services.AddDbContext<LfsDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));*/

var app = builder.Build();

var basePath = app.Configuration.GetValue<string>("BasePath");

if (!string.IsNullOrEmpty(basePath))
{
    app.UsePathBase($"/{basePath}");
    app.Use(async (context, next) =>
    {
        if (!context.Request.PathBase.HasValue && context.Request.Path != "/")
        {
            context.Response.StatusCode = 404;
            Console.WriteLine($"--> [DEBUG] 拒绝不带BasePath的请求: {context.Request.Path}");
            return;
        }

        await next.Invoke();
    });
}

app.UseRouting();

/*app.Use(async (context, next) =>
{
    Console.WriteLine($"--> [DEBUG] 传入请求: {context.Request.Method} {context.Request.Path}");
    Console.WriteLine($"[DEBUG] 请求头: {context.Request.Headers}");
    await next.Invoke();
});*/


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<LfsBasicAuthMiddleware>();

app.MapControllers();

app.Run();