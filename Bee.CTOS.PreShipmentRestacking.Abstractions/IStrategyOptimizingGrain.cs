namespace Bee.CTOS.PreShipmentRestacking.Abstractions
{
    /// <summary>
    /// 策略优化
    /// </summary>
    public interface IStrategyOptimizingGrain : IGrainWithStringKey
    {
        #region 方法

        /// <summary>
        /// 执行策略优化
        /// </summary>
        /// <param name="constraints">约束条件</param>
        /// <param name="originalStrategy">完整策略</param>
        /// <param name="pendingPart">优化部分</param>
        /// <param name="aiKey">AI密钥</param>
        /// <returns>已优化策略</returns>
        Task<string> ExecuteStrategyOptimizingAsync(string constraints, string originalStrategy, string pendingPart, string aiKey);

        #endregion
    }
}