using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Cors;
using System.Text.Json;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = null; // 禁用驼峰命名法
        });

        // 添加CORS策略（允许所有来源、方法、头部）
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", builder =>
            {
                builder
                    .AllowAnyOrigin()  // 允许所有来源
                    .AllowAnyMethod()  // 允许所有HTTP方法
                    .AllowAnyHeader(); // 允许所有请求头
            });
        });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseRouting();

        // 启用CORS中间件
        app.UseCors("AllowAll"); // 应用名为"AllowAll"的策略

        app.UseEndpoints(endpoints => endpoints.MapControllers());
    }
}