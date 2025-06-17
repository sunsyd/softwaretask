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
            options.JsonSerializerOptions.PropertyNamingPolicy = null; // �����շ�������
        });

        // ���CORS���ԣ�����������Դ��������ͷ����
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", builder =>
            {
                builder
                    .AllowAnyOrigin()  // ����������Դ
                    .AllowAnyMethod()  // ��������HTTP����
                    .AllowAnyHeader(); // ������������ͷ
            });
        });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseRouting();

        // ����CORS�м��
        app.UseCors("AllowAll"); // Ӧ����Ϊ"AllowAll"�Ĳ���

        app.UseEndpoints(endpoints => endpoints.MapControllers());
    }
}