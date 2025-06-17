using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CalculatorBackend
{
    public class Program
    {
        public static void Main(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole(); // 输出日志到控制台
                logging.SetMinimumLevel(LogLevel.Debug); // 显示所有级别的日志
            })
                .ConfigureWebHostDefaults(web => web.UseStartup<Startup>().UseUrls("http://*:5000"))
                .Build()
                .Run();
    }
}
