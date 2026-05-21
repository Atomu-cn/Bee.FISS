using Bee.CTOS.PreShipmentRestacking.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration;
using Serilog;

namespace Bee.CTOS.PreShipmentRestacking.Client
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            try
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
                    .UseOrleansClient((context, clientBuilder) =>
                    {
                        clientBuilder
                            .UseLocalhostClustering()
                            .Configure<ClusterOptions>(options =>
                            {
                                options.ClusterId = "bee.ctos";
                                options.ServiceId = "PreShipmentRestacking";
                            });
                    })
                    .UseConsoleLifetime()
                    .Build();

                await host.StartAsync();
                Console.WriteLine("=== Bee.CTOS.PreShipmentRestacking 开发环境 Orleans Client 启动成功 ===");
                Console.WriteLine("");

                IClusterClient client = host.Services.GetRequiredService<IClusterClient>();
                await ExecuteAsync(client);

                await host.StopAsync();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"客户端启动失败: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                return 1;
            }
        }

        static async Task ExecuteAsync(IClusterClient client)
        {
            int limitRow = 6;
            int limitTier = 5;

            IBayRestackingGrain grain = client.GetGrain<IBayRestackingGrain>(IBayRestackingGrain.CombineKey(limitRow, limitTier), "B01");

            Console.WriteLine("**** 演示 PreShipmentRestacking 功能 ****");
            Console.WriteLine();
            Console.WriteLine("**** 本方法在保持贝内箱数不变的前提下，最小化从初始堆存状态到最终堆存状态所需要翻箱操作的总次数。");
            Console.WriteLine("**** 堆存贝位如果有k层箱位，则贝位内至少留k-1个空箱位作为翻箱操作使用。");
            Console.WriteLine("**** 靠近车道的排最多三层，排与排尽量层高一致，或梯形落差至多两层，以防安全风险。");
            Console.WriteLine();

            Random random = new Random((int)DateTime.Now.Ticks);

            bool needInitial = true;
            List<ContainerLocation> originalInitialLocations = new List<ContainerLocation>(limitRow * limitTier);
            IDictionary<string, int> originalDeliveryOrderDict = new Dictionary<string, int>(limitRow * limitTier);
            do
            {
                //构造初始贝图和发箱顺序
                if (needInitial)
                {
                    originalInitialLocations.Clear();
                    originalDeliveryOrderDict.Clear();
                    int deliveryOrder = limitRow * limitTier;
                    int topTier = random.Next(3 + 1);
                    for (int r = 1; r <= limitRow; r++)
                    {
                        for (int t = 1; t <= topTier; t++)
                        {
                            string containerNo = $"{r}.{t}";
                            originalInitialLocations.Add(new ContainerLocation(containerNo, r, t));
                            originalDeliveryOrderDict[containerNo] = deliveryOrder;
                            if (random.Next(2) == 1)
                                deliveryOrder = deliveryOrder - 1;
                        }

                        int tt = (r == limitRow - 1 ? limitTier + 1 : limitTier) - random.Next(2 + 1);
                        if (tt > topTier + 2)
                            tt = topTier + 2;
                        topTier = r == limitRow - 1 && limitTier - 1 > limitRow * limitTier - (originalInitialLocations.Count + tt)
                            ? (limitTier - 1) - (limitRow * limitTier - originalInitialLocations.Count)
                            : tt;
                    }

                    //打乱发箱顺序
                    foreach (ContainerLocation item in originalInitialLocations)
                        if (random.Next(2) == 1)
                            item.Exchange(((ContainerLocation)originalInitialLocations[random.Next(originalInitialLocations.Count)]));

                    //整理发箱顺序
                    originalDeliveryOrderDict = await grain.SortDeliveryOrderAsync(originalInitialLocations, originalDeliveryOrderDict);
                }

                List<ContainerLocation> initialLocations = new List<ContainerLocation>(originalInitialLocations);
                IDictionary<string, int> deliveryOrderDict = new Dictionary<string, int>(originalDeliveryOrderDict);

                //构造贝位矩阵
                int[,] bayMatrix = new int[limitRow + 1, limitTier + 2];
                int[] topTiers = new int[limitRow + 1];
                foreach (ContainerLocation item in initialLocations)
                {
                    bayMatrix[item.Row, item.Tier] = deliveryOrderDict[item.ContainerNo];
                    if (topTiers[item.Row] < item.Tier)
                        topTiers[item.Row] = item.Tier;
                }

                //统计压箱数
                int impededCount = 0;
                for (int r = 1; r <= limitRow; r++)
                for (int t = 2; t <= topTiers[r]; t++)
                    if (bayMatrix[r, t] > bayMatrix[r, t - 1])
                        impededCount = impededCount + topTiers[r] - (t - 1);

                Console.WriteLine("初始贝图：                                            ");
                for (int t = limitTier + 1; t > 0; t--)
                {
                    Console.Write(t == limitTier + 1
                        ? $"        "
                        : $"Tier {t}  ");
                    for (int r = 0; r <= limitRow; r++)
                    {
                        Console.Write(bayMatrix[r, t] > 0
                            ? $"[{bayMatrix[r, t]:D3}] "
                            : $"      ");
                        if (r == 0)
                            Console.Write($" ");
                    }

                    Console.WriteLine();
                }

                Console.Write($"   ROW ");
                for (int r = 0; r <= limitRow; r++)
                    Console.Write(r == 0
                        ? $" 车道   "
                        : $"  {r}   ");
                Console.WriteLine();

                Console.WriteLine();
                Console.Write("请按回车键开始验证：");
                Console.ReadKey();
                try
                {
                    DateTime startTime = DateTime.Now;
                    string ownerId = Guid.NewGuid().ToString("N");
                    IList<MoveOperation> operations = await grain.ExecuteBayRestackingAsync(ownerId, initialLocations, deliveryOrderDict);
                    Console.WriteLine($"{ownerId}耗时{DateTime.Now.Subtract(startTime).TotalMilliseconds}毫秒 步数：{operations.Count}，压箱数：{impededCount}，超 {operations.Count - impededCount} 步。 {(operations.Count - impededCount <= 0 ? "(￣▽￣)\"" : "")}");
                    Console.WriteLine("步骤如下：");
                    Console.WriteLine();

                    int moveTime = 0;
                    foreach (MoveOperation item in operations)
                    {
                        topTiers[item.UnderRow] = topTiers[item.UnderRow] + 1;
                        bayMatrix[item.UnderRow, topTiers[item.UnderRow]] = bayMatrix[item.ReadyRow, topTiers[item.ReadyRow]];
                        bayMatrix[item.ReadyRow, topTiers[item.ReadyRow]] = 0;
                        topTiers[item.ReadyRow] = topTiers[item.ReadyRow] - 1;

                        moveTime = moveTime + 1;
                        Console.WriteLine($"STEP {moveTime:D2}："
#if DEBUG
                                          + $"{item.Remark}"
#endif
                                          + "            "
                        );
                        for (int t = limitTier + 1; t > 0; t--)
                        {
                            Console.Write(t == limitTier + 1
                                ? $"        "
                                : $"TIER {t}  ");
                            for (int r = 0; r <= limitRow; r++)
                            {
                                if (r == item.ReadyRow && t == topTiers[r] + 1)
                                {
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.Write($"[   ] ");
                                }
                                else if (bayMatrix[r, t] > 0)
                                {
                                    if (r == item.UnderRow && t == topTiers[r])
                                        Console.ForegroundColor = ConsoleColor.Red;
                                    else if (bayMatrix[r, t] > item.FixingOrdinal)
                                        Console.ForegroundColor = ConsoleColor.DarkCyan;
                                    else if (bayMatrix[r, t] == item.FixingOrdinal)
                                        Console.ForegroundColor = ConsoleColor.Cyan;
                                    else
                                        Console.ResetColor();
                                    Console.Write($"[");

                                    if (bayMatrix[r, t] > item.FixingOrdinal)
                                        Console.ForegroundColor = ConsoleColor.DarkCyan;
                                    else if (bayMatrix[r, t] == item.FixingOrdinal)
                                        Console.ForegroundColor = ConsoleColor.Cyan;
                                    else
                                        Console.ResetColor();
                                    Console.Write($"{bayMatrix[r, t]:D3}");

                                    if (r == item.UnderRow && t == topTiers[r])
                                        Console.ForegroundColor = ConsoleColor.Red;
                                    else if (bayMatrix[r, t] > item.FixingOrdinal)
                                        Console.ForegroundColor = ConsoleColor.DarkCyan;
                                    else if (bayMatrix[r, t] == item.FixingOrdinal)
                                        Console.ForegroundColor = ConsoleColor.Cyan;
                                    else
                                        Console.ResetColor();
                                    Console.Write($"] ");
                                }
                                else
                                    Console.Write($"      ");

                                if (r == 0)
                                    Console.Write($" ");

                                Console.ResetColor();
                            }

                            Console.WriteLine();
                        }

                        Console.Write($"   ROW ");
                        for (int r = 0; r <= limitRow; r++)
                        {
                            bool descOrdinal = true;
                            for (int t = 1; t <= topTiers[r]; t++)
                                if (t > 1 && bayMatrix[r, t - 1] < bayMatrix[r, t])
                                    descOrdinal = false;
                            if (descOrdinal)
                                Console.ForegroundColor = ConsoleColor.Green;
                            Console.Write(r == 0
                                ? r == item.CurrentRow
                                    ? $" 车道↑ "
                                    : $" 车道   "
                                : r == item.CurrentRow
                                    ? item.CurrentRow == item.TargetRow
                                        ? $"→{r}← "
                                        : $"↑{r}↑ "
                                    : r == item.TargetRow
                                        ? $"↓{r}↓ "
                                        : $"  {r}   ");

                            Console.ResetColor();
                        }

                        Console.WriteLine();
                        Console.WriteLine();
                        Console.Write("请按回车键继续：");
                        Console.ReadKey();
                    }

                    Console.WriteLine($"步数：{moveTime}，压箱数：{impededCount}，超 {moveTime - impededCount} 步。 {(moveTime - impededCount <= 0 ? "(￣▽￣)\"" : "")}");
                }
                catch (Exception e)
                {
                    Console.WriteLine("无解: {0}", $"Message --->{e.Message}, StackTrace --->{e.StackTrace}");
                }

                Console.WriteLine();
                Console.Write("请按：q键退出程序 / r键回放历史 / 回车键新启验证");
                char key = Console.ReadKey().KeyChar;
                if (key == 'q')
                    return;
                needInitial = key != 'r';
            } while (true);
        }
    }
}