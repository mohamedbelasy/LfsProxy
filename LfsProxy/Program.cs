using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using LfsProxy.Middleware;
using LfsProxy.Models;
using LfsProxy.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddOptions<LfsConfig>()
    .Bind(builder.Configuration.GetSection("Lfs"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var config = sp.GetRequiredService<IOptions<LfsConfig>>().Value.S3;

    var credentials = new BasicAWSCredentials(config.AccessKey, config.SecretKey);

    var s3Config = new AmazonS3Config
    {
        ForcePathStyle = config.S3ForcePathStyle,
        UseHttp = !config.UseSsl
    };

    if (!string.IsNullOrEmpty(config.Region)) s3Config.RegionEndpoint = RegionEndpoint.GetBySystemName(config.Region);

    if (!string.IsNullOrEmpty(config.Endpoint)) s3Config.ServiceURL = config.Endpoint;

    return new AmazonS3Client(credentials, s3Config);
});


builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Clear();
    options.KnownNetworks.Clear();
});

builder.Services.AddScoped<S3Service>();

builder.Services.AddSingleton<JsonLockService>();

var app = builder.Build();

app.UseForwardedHeaders();

var basePath = app.Configuration.GetValue<string>("BasePath");

if (!string.IsNullOrEmpty(basePath))
{
    app.UsePathBase($"/{basePath}");
    app.Use(async (context, next) =>
    {
        if (!context.Request.PathBase.HasValue && context.Request.Path != "/")
        {
            context.Response.StatusCode = 404;
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

try
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("正在执行启动时S3连接检查...");
    var s3Client = app.Services.GetRequiredService<IAmazonS3>();
    var s3Config = app.Services.GetRequiredService<IOptions<LfsConfig>>().Value.S3;
    var request = new GetBucketLocationRequest
    {
        BucketName = s3Config.BucketName
    };
    var response = await s3Client.GetBucketLocationAsync(request);
    logger.LogInformation("S3 连接成功！存储桶 '{BucketName}' 可访问，位置: {Location}",
        s3Config.BucketName, response.Location);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogCritical(ex, "S3 连接检查失败！请检查 S3 配置和网络连接。应用程序将关闭。");
    await Task.Delay(1000);
    Environment.Exit(1);
}

app.Run();