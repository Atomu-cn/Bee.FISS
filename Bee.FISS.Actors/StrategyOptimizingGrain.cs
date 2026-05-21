using Bee.CTOS.PreShipmentRestacking.Abstractions;
using Microsoft.Extensions.Logging;

namespace Bee.CTOS.PreShipmentRestacking.Actors
{
    /// <summary>
    /// 表示用于策略优化的 Orleans Grain 实现。
    /// 该类接收外部请求并将具体的优化计算委托给注入的 <see cref="IStrategyOptimizingService"/> 实例处理。
    /// 使用 Guid.Empty 实现 Grain 单例。
    /// </summary>
    public class StrategyOptimizingGrain : Grain, IStrategyOptimizingGrain
    {
        /// <summary>
        /// 使用依赖注入构造 <see cref="StrategyOptimizingGrain"/> 的实例。
        /// </summary>
        /// <param name="logger">用于记录信息、调试和错误的 <see cref="ILogger{StrategyOptimizingGrain}"/> 实例。</param>
        /// <param name="strategyOptimizingService">执行策略优化逻辑的服务实现，不能为空。</param>
        public StrategyOptimizingGrain(ILogger<StrategyOptimizingGrain> logger, IStrategyOptimizingService strategyOptimizingService)
        {
            _logger = logger;
            _strategyOptimizingService = strategyOptimizingService;

            string primaryKey = this.GetPrimaryKeyString();
            if (!String.IsNullOrEmpty(primaryKey))
                _aiMode = primaryKey;
        }

        #region 属性

        /// <summary>
        /// 日志记录器，供类内部记录运行信息与异常使用。
        /// </summary>
        private readonly ILogger<StrategyOptimizingGrain> _logger;

        /// <summary>
        /// 负责执行具体策略优化计算的服务接口。
        /// </summary>
        private readonly IStrategyOptimizingService _strategyOptimizingService;

        /// <summary>
        /// AI 模型
        /// </summary>
        private readonly string _aiMode = "glm-4-flash";

        #endregion

        #region 方法

        async Task<string> IStrategyOptimizingGrain.ExecuteStrategyOptimizingAsync(string constraints, string originalStrategy, string pendingPart, string aiKey)
        {
            try
            {
                return await _strategyOptimizingService.ExecuteStrategyOptimizingAsync(constraints, originalStrategy, pendingPart, aiKey, _aiMode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Execute failed: {originalStrategy}");
                throw;
            }
        }
    }

    #endregion
}