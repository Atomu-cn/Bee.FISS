namespace Bee.FISS.Abstractions
{
    /// <summary>
    /// 单贝整理
    /// </summary>
    /// <remarks>
    /// <para>
    /// 实现此接口的 actor 用于在给定行数与层数限制下，基于初始箱位与期望的出箱顺序，计算一组翻箱/移动操作（<see cref="MoveOperation"/>）。
    /// 这些操作应保证在执行完毕后，贝内的箱子能够按照 <paramref name="deliveryOrderDict"/> 指定的顺序出箱（较小的序号表示更高优先级）。
    /// </para>
    /// <para>
    /// 本接口包含用于编码/解码由行数与层数组合而成的 long 类型 key 的静态工具方法，便于在 Grain 分区或标识中传递限制值。
    /// </para>
    /// </remarks>
    public interface IBayRestackingGrain : IGrainWithIntegerCompoundKey
    {
        #region 方法

        /// <summary>
        /// 将给定的行数与层数编码为单一的 <see cref="long"/> 类型 key，便于在 Grain 分区、标识或字典中传递限制值。
        /// 编码规则：将 <c>limitRow</c> 放在高 32 位，将 <c>limitTier</c> 放在低 32 位。
        /// </summary>
        /// <param name="limitRow">要编码的最大行数（应为非负值），将存储于返回值的高 32 位。</param>
        /// <param name="limitTier">要编码的最大层数（应为非负值），将存储于返回值的低 32 位。</param>
        /// <returns>
        /// 返回一个 <see cref="long"/> 值：高 32 位表示 <c>limitRow</c>，低 32 位表示 <c>limitTier</c>。
        /// 可使用 <see cref="SplitKey(long)"/> 将该 key 解析回原始的行数与层数。
        /// </returns>
        public static long CombineKey(int limitRow, int limitTier)
        {
            // 防护：确保输入值在合理范围内，避免位移或截断导致的负值或数据丢失。
            if (limitRow < 0) throw new ArgumentOutOfRangeException(nameof(limitRow), "limitRow must be non-negative.");
            if (limitTier < 0) throw new ArgumentOutOfRangeException(nameof(limitTier), "limitTier must be non-negative.");

            return ((long)limitRow << 32) | (uint)limitTier;
        }

        /// <summary>
        /// 将由 <see cref="CombineKey(int,int)"/> 生成的合并 key 拆解为行数和层数。
        /// 高 32 位表示 <c>limitRow</c>，低 32 位表示 <c>limitTier</c>。
        /// </summary>
        /// <param name="key">通过 <see cref="CombineKey(int,int)"/> 合并得到的 long 类型 key。</param>
        /// <returns>
        /// 返回一个元组 (<c>limitRow</c>, <c>limitTier</c>)：
        /// <list type="bullet">
        /// <item><description><c>limitRow</c> — 从高 32 位解析得到的最大行数。</description></item>
        /// <item><description><c>limitTier</c> — 从低 32 位解析得到的最大层数。</description></item>
        /// </list>
        /// </returns>
        /// <example>
        /// <code language="csharp">
        /// // 将 5 行、3 层 编码为 key，然后再解码回 (5,3)
        /// long key = IBayRestackingActor.CombineKey(5, 3);
        /// var (rows, tiers) = IBayRestackingActor.SplitKey(key); // rows == 5, tiers == 3
        /// </code>
        /// </example>
        public static (int limitRow, int limitTier) SplitKey(long key)
        {
            int limitRow = (int)(key >> 32);
            int limitTier = (int)(key & 0xFFFFFFFF);

            // 防护：如果解码后出现负值，通常意味着传入的 key 并非由本接口的 CombineKey 生成或输入越界。
            if (limitRow < 0 || limitTier < 0)
                throw new ArgumentException("Decoded key contains negative values. Ensure the key was created by CombineKey with non-negative inputs.", nameof(key));

            return (limitRow, limitTier);
        }

        /// <summary>
        /// 逆序发箱顺序映射
        /// </summary>
        /// <param name="initialLocations">
        /// 初始贝图：贝内所有集装箱的箱位集合。集合中的每一项应实现 <see cref="ContainerLocation"/>，表示箱号、所在排和所在层。
        /// </param>
        /// <param name="deliveryOrderDict">
        /// 发箱顺序映射。键为箱号（string），值为该箱的发箱序号（int，序号越小表示越早装船）。该映射用于确定优先出箱的顺序，未在字典中的箱子视为最低优先级。
        /// </param>
        //// <returns>逆序后的发箱序号映射表（键：箱号，值：发箱序号）</returns>
        Task<IDictionary<string, int>> SortDeliveryOrderAsync(IList<ContainerLocation> initialLocations, IDictionary<string, int> deliveryOrderDict);

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
        Task<MoveOperation[]> ExecuteBayRestackingAsync(string ownerId, IList<ContainerLocation> initialLocations, IDictionary<string, int> deliveryOrderDict);

        #endregion
    }
}