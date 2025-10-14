using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using LfsProxy.Models;
using Microsoft.Extensions.Options;

namespace LfsProxy.Middleware;

public class LfsBasicAuthMiddleware
{
    private readonly ILogger<LfsBasicAuthMiddleware> _logger;
    private readonly RequestDelegate _next;
    private readonly List<LfsUser> _users;

    public LfsBasicAuthMiddleware(RequestDelegate next, ILogger<LfsBasicAuthMiddleware> logger,
        IOptions<LfsConfig> options)
    {
        _next = next;
        _logger = logger;
        _users = options.Value.Users;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_users.Count == 0)
        {
            _logger.LogError("LFS 用户未配置，认证已禁用。拒绝所有请求以确保安全。");
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await context.Response.WriteAsync("Authentication is not configured on the server.");
            return;
        }

        if (context.Request.Headers.TryGetValue("Authorization", out var value))
            try
            {
                var authHeader = AuthenticationHeaderValue.Parse(value);
                if (authHeader.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase) &&
                    authHeader.Parameter != null)
                {
                    var credentialBytes = Convert.FromBase64String(authHeader.Parameter);
                    var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':', 2);
                    var username = credentials[0];
                    var password = credentials[1];

                    var user = _users.FirstOrDefault(u => u.Username == username && u.Password == password);

                    if (user != null)
                    {
                        if (string.IsNullOrEmpty(user.Uid))
                        {
                            _logger.LogError("用户 '{Username}' 认证成功，但其配置中缺少 'Uid'。请检查 appsettings.json。", user.Username);
                            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            await context.Response.WriteAsync("User configuration error on server.");
                            return;
                        }

                        _logger.LogInformation("用户 '{Username}' 认证成功。", user.Username);

                        var claims = new[]
                        {
                            new Claim(ClaimTypes.Name, user.Username),
                            new Claim(ClaimTypes.NameIdentifier, user.Uid)
                        };

                        var identity = new ClaimsIdentity(claims, "Basic");

                        context.User = new ClaimsPrincipal(identity);

                        await _next(context);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理认证时发生意外错误。");
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                await context.Response.WriteAsync("An error occurred during authentication.");
                return;
            }

        _logger.LogWarning("认证失败或未提供凭证。请求 {Method} {Path} 被拒绝。", context.Request.Method, context.Request.Path);

        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
        context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Git LFS Server\"";
        await context.Response.WriteAsync("Unauthorized");
    }
}