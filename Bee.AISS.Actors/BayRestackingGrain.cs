using Bee.CTOS.PreShipmentRestacking.Abstractions;
using Microsoft.Extensions.Logging;

namespace Bee.CTOS.PreShipmentRestacking.Actors
{
    /// <summary>
    /// 单贝整理
    /// </summary>
    public class BayRestackingGrain : Grain, IBayRestackingGrain
    {
        /// <summary>
        /// 初始化 <see cref="BayRestackingGrain"/> 实例。
        /// 从依赖注入获取日志记录器和贝位整理服务，并根据 Actor 主键解析出该 Actor 负责的行与层限制值，供后续整理计算使用。
        /// </summary>
        /// <param name="logger">用于记录本 Actor 操作、诊断信息和异常的 <see cref="ILogger{BayRestackingActor}"/> 实例。</param>
        /// <param name="bayRestackingService">执行具体贝位整理算法与业务逻辑的 <see cref="IBayRestackingService"/> 实例。</param>
        /// <remarks>
        /// 构造函数内部通过调用 <c>this.GetPrimaryKeyLong()</c> 获取 Actor 主键，然后使用 <c>IBayRestackingActor.SplitKey(long)</c> 将主键拆分为 <c>_limitRow</c> 和 <c>_limitTier</c> 两个只读限制值，
        /// 以便在后续的 ExecuteAsync 中将范围限定到该 Actor 管辖的行和层上。
        /// </remarks>
        public BayRestackingGrain(ILogger<BayRestackingGrain> logger, IBayRestackingService bayRestackingService)
        {
            _logger = logger;
            _bayRestackingService = bayRestackingService;

            long key = this.GetPrimaryKeyLong(out string? _bay);
            (_limitRow, _limitTier) = IBayRestackingGrain.SplitKey(key);
        }

        #region 属性

        /// <summary>
        /// 日志记录器，用于记录 Actor 内的操作、诊断信息和异常。
        /// 由依赖注入提供并在构造函数中初始化；为只读字段，线程安全使用。
        /// </summary>
        private readonly ILogger<BayRestackingGrain> _logger;

        /// <summary>
        /// 贝位整理服务接口的实例，封装具体的整理算法与业务逻辑。
        /// 由依赖注入提供并在构造函数中初始化；用于在 ExecuteAsync 中委托实际计算。
        /// </summary>
        private readonly IBayRestackingService _bayRestackingService;

        /// <summary>
        /// 当前 Actor 负责的贝位（bay）标识。
        /// </summary>
        private readonly string? _bay;

        /// <summary>
        /// 当前 Actor 负责的行（row）限制（由 Actor 主键解析得到的行数或行索引上限）。
        /// 在构造函数中通过调用 <c>IBayRestackingActor.SplitKey</c> 从 Actor 主键计算并作为只读值保存。
        /// </summary>
        private readonly int _limitRow;

        /// <summary>
        /// 当前 Actor 负责的层（tier）限制（由 Actor 主键解析得到的层数或层索引上限）。
        /// 在构造函数中通过调用 <c>IBayRestackingActor.SplitKey</c> 从 Actor 主键计算并作为只读值保存。
        /// </summary>
        private readonly int _limitTier;

        #endregion

        #region 方法

        async Task<IDictionary<string, int>> IBayRestackingGrain.SortDeliveryOrderAsync(IList<ContainerLocation> initialLocations, IDictionary<string, int> deliveryOrderDict)
        {
            try
            {
                return await _bayRestackingService.SortDeliveryOrderAsync(_limitRow, _limitTier, initialLocations, deliveryOrderDict);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Sort failed for (limitRow = {_limitRow} & limitTier = {_limitTier})");
                throw;
            }
        }

        async Task<MoveOperation[]> IBayRestackingGrain.ExecuteBayRestackingAsync(string ownerId, IList<ContainerLocation> initialLocations, IDictionary<string, int> deliveryOrderDict)
        {
            try
            {
                return await _bayRestackingService.ExecuteBayRestackingAsync(ownerId, _limitRow, _limitTier, initialLocations, deliveryOrderDict);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Execute failed for (ownerId = {ownerId}，limitRow = {_limitRow} & limitTier = {_limitTier})");
                throw;
            }
        }

        #endregion
    }
}