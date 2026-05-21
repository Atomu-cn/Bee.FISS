using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration;
using Phenix.Core.DependencyInjection;
using Serilog;

namespace Bee.ISS
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            IHost host = Host.CreateDefaultBuilder(args)
                .UseContentRoot(Phenix.Core.AppRun.BaseDirectory)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.SetBasePath(Phenix.Core.AppRun.BaseDirectory).AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                })
                .UseSerilog((context, loggerConfig) =>
                {
                    loggerConfig.ReadFrom.Configuration(context.Configuration).Enrich.FromLogContext();
                })
                .UseOrleans(siloBuilder => siloBuilder
                    .UseLocalhostClustering()
                    .Configure<ClusterOptions>(options =>
                    {
                        options.ClusterId = "bee.ctos";
                        options.ServiceId = "PreShipmentRestacking";
                    })
                    .AddMemoryGrainStorageAsDefault()
                    .ConfigureServices(services => services
                        .AddServices(
                            Path.Combine(Phenix.Core.AppRun.BaseDirectory, "Bee.CTOS.PreShipmentRestacking.Domain.dll"),
                            allow: type =>
                            {
                                Console.WriteLine($"装配 domain 服务：{type.Name}");
                                return true;
                            })
                        .AddHttpClient("StrategyOptimizer")
                            .ConfigureHttpClient((provider, client) =>
                            {
                                IConfiguration config = provider.GetRequiredService<IConfiguration>();
                                client.BaseAddress = new Uri(config["AI:BaseUrl"] ?? "https://open.bigmodel.cn/api/paas/v4/chat/completions");
                            })
                    )
                )
                .UseConsoleLifetime()
                .Build();

            await host.StartAsync();
            Console.WriteLine("=== Bee.CTOS.PreShipmentRestacking 开发环境 Orleans Silo 启动成功 ===");
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();

            await host.StopAsync();
            return 0;
        }
    }
}