using Orleans.Services;

namespace Bee.ISS.Abstractions
{
    /// <summary>
    /// 单贝整理
    /// </summary>
    public interface IBayRestackingService : IGrainService
    {
        #region 方法

        /// <summary>
        /// 逆序发箱顺序映射
        /// </summary>
        /// <param name="limitRow">
        /// 排数上限。表示允许的最大排号（方法内部一般按 0..limitRow 的范围处理，0 为最外排的车道）。
        /// </param>
        /// <param name="limitTier">
        /// 层高上限。用于判断每排的最大可用层数（在不同排上可能会有细微差异，调用方应以该参数为约定上限）。
        /// </param>
        /// <param name="initialLocations">
        /// 初始贝图：贝内所有集装箱的箱位集合。集合中的每一项应实现 <see cref="ContainerLocation"/>，表示箱号、所在排和所在层。
        /// </param>
        /// <param name="deliveryOrderDict">
        /// 发箱顺序映射。键为箱号（string），值为该箱的发箱序号（int，序号越小表示越早装船）。该映射用于确定优先出箱的顺序，未在字典中的箱子视为最低优先级。
        /// </param>
        //// <returns>逆序后的发箱序号映射表（键：箱号，值：发箱序号）</returns>
        Task<IDictionary<string, int>> SortDeliveryOrderAsync(int limitRow, int limitTier, IList<ContainerLocation> initialLocations, IDictionary<string, int> deliveryOrderDict);

        /// <summary>
        /// 计算并返回对单个贝进行重排（翻箱）的动作序列，以满足给定的发箱顺序约束。
        /// </summary>
        /// <param name="ownerId">
        /// 所属翻箱ID。
        /// </param>
        /// <param name="limitRow">
        /// 排数上限。表示允许的最大排号（方法内部一般按 0..limitRow 的范围处理，0 为最外排的车道）。
        /// </param>
        /// <param name="limitTier">
        /// 层高上限。用于判断每排的最大可用层数（在不同排上可能会有细微差异，调用方应以该参数为约定上限）。
        /// </param>
        /// <param name="initialLocations">
        /// 初始贝图：贝内所有集装箱的箱位集合。集合中的每一项应实现 <see cref="ContainerLocation"/>，表示箱号、所在排和所在层。
        /// </param>
        /// <param name="deliveryOrderDict">
        /// 发箱顺序映射。键为箱号（string），值为该箱的发箱序号（int，序号越小表示越早装船）。该映射用于确定优先出箱的顺序，未在字典中的箱子视为最低优先级。
        /// </param>
        /// <returns>
        /// 按执行顺序排列的翻箱动作列表，元素实现 <see cref="MoveOperation"/>。
        /// </returns>
        Task<MoveOperation[]> ExecuteBayRestackingAsync(string ownerId, int limitRow, int limitTier, IList<ContainerLocation> initialLocations, IDictionary<string, int> deliveryOrderDict);

        #endregion
    }
}