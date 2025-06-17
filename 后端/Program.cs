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
                    logging.AddConsole(); // �����־������̨
                logging.SetMinimumLevel(LogLevel.Debug); // ��ʾ���м������־
            })
                .ConfigureWebHostDefaults(web => web.UseStartup<Startup>().UseUrls("https://10.244.200.237:44310"))
                .Build()
                .Run();
    }
}
