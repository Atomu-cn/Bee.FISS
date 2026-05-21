using Bee.CTOS.PreShipmentRestacking.Abstractions;

namespace Bee.CTOS.PreShipmentRestacking.Domain
{
    /// <summary>
    /// 贝内箱子
    /// </summary>
    internal class BayContainer
    {
        /// <summary>
        /// 初始化
        /// </summary>
        public BayContainer(ContainerLocation initialLocation, int deliveryOrdinal)
        {
            _initialLocation = initialLocation;
            _row = initialLocation.Row;
            _tier = initialLocation.Tier;
            _deliveryOrdinal = deliveryOrdinal;
        }

        #region 属性

        private readonly ContainerLocation _initialLocation;

        /// <summary>
        /// 初始位置
        /// </summary>
        public ContainerLocation InitialLocation
        {
            get { return _initialLocation; }
        }

        private int _row;

        /// <summary>
        /// 排
        /// </summary>
        public int Row
        {
            get { return _row; }
        }

        private int _tier;

        /// <summary>
        /// 层
        /// </summary>
        public int Tier
        {
            get { return _tier; }
        }

        private int _deliveryOrdinal;

        /// <summary>
        /// 发箱序号
        /// </summary>
        public int DeliveryOrdinal
        {
            get { return _deliveryOrdinal; }
        }

        #endregion

        #region 方法

        /// <summary>
        /// 逆序整理
        /// </summary>
        public static IList<BayContainer> Fetch(int limitRow, int limitTier, IList<ContainerLocation> initialLocations, IDictionary<string, int> deliveryOrderDict)
        {
            return Fetch(limitRow, limitTier, initialLocations, deliveryOrderDict, out _, out _);
        }

        /// <summary>
        /// 逆序整理
        /// </summary>
        public static IList<BayContainer> Fetch(int limitRow, int limitTier, IList<ContainerLocation> initialLocations, IDictionary<string, int> deliveryOrderDict, out int[] topTiers, out BayContainer?[,] bayMatrix)
        {
            if (limitRow <= 1)
                throw new ArgumentOutOfRangeException(nameof(limitRow), limitRow, "limitRow <= 1");
            if (limitTier <= 1)
                throw new ArgumentOutOfRangeException(nameof(limitTier), limitTier, "limitTier <= 1");

            topTiers = new int[limitRow + 1]; //各排顶层（[0]下标代表位于车道上的临时排）
            bayMatrix = new BayContainer[limitRow + 1, limitTier + 2]; //贝位矩阵（[0,X]下标代表位于车道的临时排各箱位，最内排可高出限值一层）

            if (limitTier - 1 > limitRow * limitTier - initialLocations.Count)
                throw new InvalidOperationException(String.Format("堆存贝位如果有{0}层箱位，则贝位内至少留{0}-1个空箱位作为翻箱操作使用, 但实际只有{1}个空箱位!", limitTier, limitRow * limitTier - initialLocations.Count));

            List<BayContainer> result = new List<BayContainer>(initialLocations.Count);
            foreach (ContainerLocation item in initialLocations)
            {
                if (item.Row < 0)
                    throw new ArgumentOutOfRangeException(nameof(item.Row), item.Row, "row < 0");
                if (item.Row > limitRow)
                    throw new ArgumentOutOfRangeException(nameof(item.Row), item.Row, "row > " + limitRow);
                if (item.Tier < 1)
                    throw new ArgumentOutOfRangeException(nameof(item.Tier), item.Tier, "tier < 1");
                if (item.Row < limitRow && item.Tier > limitTier || item.Row == limitRow && item.Tier > limitTier + 1)
                    throw new ArgumentOutOfRangeException(nameof(item.Tier), item.Tier, "tier > " + limitTier);
                if (bayMatrix.GetValue(item.Row, item.Tier) != null)
                    throw new InvalidOperationException(item.Tier + "层已有" + bayMatrix[item.Row, item.Tier]!._initialLocation.ContainerNo + "不能再添加" + item.ContainerNo + "!");

                int deliveryOrder = deliveryOrderDict.TryGetValue(item.ContainerNo, out int deliveryOrdinal) ? deliveryOrdinal : Int32.MaxValue;
                BayContainer container = new BayContainer(item, deliveryOrder);
                bayMatrix[item.Row, item.Tier] = container;
                if (topTiers[item.Row] < item.Tier)
                    topTiers[item.Row] = item.Tier;
                result.Add(container);
            }

            if (topTiers.Aggregate(0, (current, tier) => current + tier) > initialLocations.Count)
                throw new InvalidOperationException("初始贝图不应该出现" + (topTiers.Aggregate(0, (current, tier) => current + tier) - initialLocations.Count) + "个悬空箱位!");

            result.Sort((x, y) => -x._deliveryOrdinal.CompareTo(y._deliveryOrdinal));
            int ordinal = Int32.MinValue;
            int nowOrdinal = 0;
            for (int i = result.Count - 1; i >= 0; i--)
            {
                BayContainer container = result[i];
                if (ordinal != container._deliveryOrdinal)
                {
                    ordinal = container._deliveryOrdinal;
                    nowOrdinal = nowOrdinal + 1;
                }

                container._deliveryOrdinal = nowOrdinal;
            }

            return result;
        }

        /// <summary>
        /// 统计顺堆箱数
        /// </summary>
        public static int TotalAsc(int row, int tier, BayContainer?[,] bayMatrix)
        {
            if (row == 0)
                return 0;

            int result = 0;
            bool findDesc = false;
            for (int t = 1; t < (row == 1 && tier > 3 ? 3 : tier); t++)
                if (bayMatrix[row, t]!._deliveryOrdinal >= bayMatrix[row, t + 1]!._deliveryOrdinal)
                    result = result + 1;
                else
                {
                    findDesc = true;
                    break;
                }

            if (!findDesc)
                result = result + 1;
            return result;
        }

        /// <summary>
        /// 是否固定
        /// </summary>
        public bool IsFixed(int[] fixedTiers)
        {
            return _row > 0 && _tier <= fixedTiers[_row];
        }

        /// <summary>
        /// 尝试标记为固定箱
        /// </summary>
        public bool TryFix(int fixingOrdinal, int limitRow, ref int[] fixedTiers, int[] topTiers, BayContainer?[,] bayMatrix)
        {
            //在临时位上的集装箱，不能被认定
            if (_row == 0 || _row == 1 && _tier > 3)
                return false;

            //所在排达到层高极限，不能被认定
            if (IsUltraLimit(_row, _tier - 1, limitRow, topTiers, bayMatrix))
                return false;

            //仅限于在当前箱和同号箱中被认定
            //当前箱和同号箱要么堆放在最底层，要么压在固定箱上，才能被认定
            if (fixingOrdinal == _deliveryOrdinal && fixedTiers[_row] == _tier - 1)
            {
                fixedTiers[_row] = _tier; //抬高固定层标记线
                return true;
            }

            return false;
        }

        private bool IsUltraLimit(int underRow, int underTier, int limitRow, int[] topTiers, BayContainer?[,] bayMatrix)
        {
            //针对顺堆排可能成为超高峰或超低谷的情况进行判定
            if (underRow == 0 || underRow == 1)
                return false;
            if (TotalAsc(underRow, underTier, bayMatrix) < underTier ||
                underTier > 0 && bayMatrix[underRow, underTier]!._deliveryOrdinal < _deliveryOrdinal)
                return false;

            //首先，统计待处理的非固定箱的数量，即待处理箱数
            //如果当前排成了超高峰，则统计将两侧超低谷填成正常排所需填充箱数
            //如果当前排成了超低谷，则统计将自身超低谷填成正常排所需填充箱数
            int activityCount = topTiers[0];
            int locationCount = 0;
            for (int r = 1; r <= limitRow; r++)
            {
                int totalAsc = r == underRow
                    ? TotalAsc(r, topTiers[r], bayMatrix) + 1
                    : TotalAsc(r, topTiers[r], bayMatrix);
                activityCount = activityCount + topTiers[r] - totalAsc;
                if (r >= underRow - 1 && r <= underRow + 1)
                {
                    int leftGap = r > 1
                        ? totalAsc - (r - 1 == underRow
                            ? TotalAsc(r - 1, topTiers[r - 1], bayMatrix) + 1
                            : TotalAsc(r - 1, topTiers[r - 1], bayMatrix))
                        : 0;
                    int rightGap = r < limitRow
                        ? totalAsc - (r + 1 == underRow
                            ? TotalAsc(r + 1, topTiers[r + 1], bayMatrix) + 1
                            : TotalAsc(r + 1, topTiers[r + 1], bayMatrix))
                        : 0;
                    if (r == underRow && leftGap >= -2 && leftGap <= 2 && rightGap >= -2 && rightGap <= 2)
                        return false;
                    if (r == underRow - 1 && rightGap < -2 ||
                        r == underRow && (leftGap < -2 || rightGap < -2) ||
                        r == underRow + 1 && leftGap < -2)
                        locationCount = locationCount + Math.Abs(leftGap <= rightGap ? leftGap : rightGap) - 2;
                }
            }

            //那么，如果待处理箱数达不到超低谷填充箱数，则判定为突破超高极限
            return activityCount < locationCount;
        }

        /// <summary>
        /// 是否最大序号
        /// </summary>
        public bool IsMaxOrdinal(int limitRow, int[] topTiers, BayContainer?[,] bayMatrix, bool ignoreEqual, Func<int, bool> ignoreFactor)
        {
            for (int r = 0; r <= limitRow; r++)
            {
                if (topTiers[r] == 0)
                    continue;
                if (ignoreFactor(r))
                    continue;
                if (bayMatrix[r, topTiers[r]]!._deliveryOrdinal > _deliveryOrdinal ||
                    ignoreEqual && bayMatrix[r, topTiers[r]]!._deliveryOrdinal == _deliveryOrdinal)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 翻箱约束条件
        /// </summary>
        public bool AllowMoveTo(int underRow, MoveOperation? prevOperation, int fixingOrdinal, int limitRow, int limitTier, int[] fixedTiers, int[] topTiers, BayContainer?[,] bayMatrix)
        {
            int underTier = topTiers[underRow];

            //不允许翻固定箱
            if (IsFixed(fixedTiers))
                return false;

            //不允许翻到自身所在排
            if (underRow == _row)
                return false;

            //不允许翻到压箱中有同号箱的排，除非自身也是同号箱
            if (_deliveryOrdinal != fixingOrdinal)
                for (int t = underTier; t > fixedTiers[underRow]; t--)
                    if (bayMatrix[underRow, t]!._deliveryOrdinal == fixingOrdinal)
                        return false;

            //不允许翻到前一个翻箱动作中正好是自身翻出的排和位
            if (prevOperation != null && underRow == prevOperation.ReadyRow && prevOperation.ContainerNo == _initialLocation.ContainerNo)
                return false;

            //不允许翻到已达层高限值的排，最内排可高出限值一层
            if (underTier >= (underRow == 0 || underRow == limitRow ? limitTier + 1 : limitTier))
                return false;

            //不允许翻到翻入后是顺堆排但会突破该排层高极限的排
            return !IsUltraLimit(underRow, underTier, limitRow, topTiers, bayMatrix);
        }

        /// <summary>
        /// 翻入
        /// </summary>
        public MoveOperation MoveTo(string ownerId, int operationIndex, int underRow, int fixingOrdinal, int currentRow, int targetRow, int limitRow, ref int[] fixedTiers, ref int[] topTiers, ref BayContainer?[,] bayMatrix, string remark)
        {
            if (_tier < topTiers[_row])
                throw new InvalidOperationException("无法翻动" + _row + "排第" + _tier + "层的箱（被" + (topTiers[_row] - _tier) + "只箱压着呢）!");

            int readyRow = _row;
            //清理原箱位
            topTiers[_row] = _tier - 1;
            bayMatrix[_row, _tier] = null;
            //翻入新箱位
            _row = underRow;
            _tier = topTiers[underRow] + 1;
            //整理新箱位
            topTiers[_row] = _tier;
            bayMatrix[_row, _tier] = this;
            //翻动后的同号箱尝试标记为固定箱
            bool isFixed = TryFix(fixingOrdinal, limitRow, ref fixedTiers, topTiers, bayMatrix);
            //返回翻箱动作
            return new MoveOperation(Guid.NewGuid().ToString("N"), ownerId, operationIndex, _initialLocation.ContainerNo, readyRow, underRow, fixingOrdinal, isFixed, currentRow, targetRow, remark);
        }

        /// <summary>
        /// 选择目标排
        /// </summary>
        public int SelectTargetRow(int fixingCount, int limitRow, int limitTier, int[] fixedTiers, int[] topTiers, BayContainer?[,] bayMatrix, IList<int> troughRowList, IList<int> ascRowList, Func<int, bool> ignoreFactor)
        {
            int emptyRow = 0;

            int fixedTroughRow = 0;
            int minFixedTroughTierCount = Int32.MaxValue;
            int minFixedTroughNearCount = Int32.MaxValue;
            int fixedRow = 0;
            int minFixedTierCount = Int32.MaxValue;
            int minFixedNearCount = Int32.MaxValue;
            int emptyCount = 0;
            bool haveSingle = false;

            BayContainer? minTopContainer = null;
            int minSingleTierCount = Int32.MaxValue;
            int minSingleNearCount = Int32.MaxValue;

            BayContainer? minLowContainer = null;
            int minImpededCount = Int32.MaxValue;

            for (int r = 1; r <= limitRow; r++)
            {
                if (ignoreFactor(r))
                    continue;

                //目标排不应该是临时排
                if (r == 1 && fixedTiers[r] == 3)
                    continue;

                //目标排上的固定箱不能堆到层高限值
                if (fixedTiers[r] >= (r == 1 ? 3 : r == limitRow ? limitTier + 1 : limitTier))
                    continue;

                //首先，选择靠内的无箱排翻入当前箱，翻入后不突破该排的层高极限
                if (topTiers[r] == 0 && !IsUltraLimit(r, fixedTiers[r], limitRow, topTiers, bayMatrix))
                {
                    emptyRow = r;
                    continue;
                }

                int impededCount = topTiers[r] - fixedTiers[r];
                int tierCount = topTiers[r];
                int nearCount = Math.Abs(_row - r);

                //如果以上选不到，但是当前箱位于顶层，或者所有固定排剩余空位能够堆的下当前箱和所有同号箱，则选择就低就近靠内的超低谷固定排翻入当前箱，除非仅剩一个空位且存在固定箱上压单箱的排
                //如果以上选不到，但是当前箱位于顶层，或者所有固定排剩余空位能够堆的下当前箱和所有同号箱，则选择就低就近靠内的固定排翻入当前箱，除非仅剩一个空位且存在固定箱上压单箱的排
                if (impededCount == 0)
                {
                    if (troughRowList.Contains(r) && (fixedTroughRow == 0 ||
                                                      minFixedTroughTierCount > tierCount ||
                                                      minFixedTroughTierCount == tierCount && minFixedTroughNearCount >= nearCount))
                    {
                        fixedTroughRow = r;
                        minFixedTroughTierCount = tierCount;
                        minFixedTroughNearCount = nearCount;
                    }

                    if (fixedRow == 0 ||
                        minFixedTierCount > tierCount ||
                        minFixedTierCount == tierCount && minFixedNearCount >= nearCount)
                    {
                        fixedRow = r;
                        minFixedTierCount = tierCount;
                        minFixedNearCount = nearCount;
                    }

                    int count = (r == 1 ? 3 : r == limitRow ? limitTier + 1 : limitTier) - topTiers[r];
                    if (count > 0)
                        emptyCount = emptyCount + count;

                    continue;
                }

                //如果以上选不到，选择固定箱上压单箱的排中发箱序号最小的，就低就近靠内的排翻入当前箱，翻入后不突破该排的层高极限
                if (impededCount == 1 && !IsUltraLimit(r, fixedTiers[r], limitRow, topTiers, bayMatrix))
                {
                    haveSingle = true;
                    BayContainer topContainer = bayMatrix[r, topTiers[r]]!;

                    if (minTopContainer == null ||
                        minTopContainer._deliveryOrdinal > topContainer._deliveryOrdinal ||
                        minTopContainer._deliveryOrdinal == topContainer._deliveryOrdinal && minSingleTierCount > tierCount ||
                        minTopContainer._deliveryOrdinal == topContainer._deliveryOrdinal && minSingleTierCount == tierCount && minSingleNearCount >= nearCount)
                    {
                        minTopContainer = topContainer;
                        minSingleTierCount = tierCount;
                        minSingleNearCount = nearCount;
                    }
                }

                //如果以上选不到，选择固定箱上压箱数最少、最低层压箱发箱序号最小、靠内的排翻入当前箱，非顺堆排优先
                BayContainer lowContainer = bayMatrix[r, fixedTiers[r] + 1]!;
                if (minLowContainer == null ||
                    minImpededCount > impededCount ||
                    minImpededCount == impededCount && minLowContainer._deliveryOrdinal > lowContainer._deliveryOrdinal ||
                    minImpededCount == impededCount && minLowContainer._deliveryOrdinal == lowContainer._deliveryOrdinal && !ascRowList.Contains(r))
                {
                    minLowContainer = lowContainer;
                    minImpededCount = impededCount;
                }
            }

            if (emptyRow > 0)
                return emptyRow;
            if (fixedTroughRow > 0 && (_tier == topTiers[_row] || emptyCount >= fixingCount || emptyCount == 1 && haveSingle))
                return fixedTroughRow;
            if (fixedRow > 0 && (_tier == topTiers[_row] || emptyCount >= fixingCount || emptyCount == 1 && haveSingle))
                return fixedRow;
            if (minTopContainer != null)
                return minTopContainer._row;
            if (minLowContainer != null)
                return minLowContainer._row;

            throw new InvalidOperationException("无处安放" + _row + "排第" + _tier + "层的当前箱!");
        }

        /// <summary>
        /// 选择翻入就近排
        /// </summary>
        public int? SelectUnderNearRow(MoveOperation? prevOperation, int fixingOrdinal, int limitRow, int limitTier, int[] fixedTiers, int[] topTiers, BayContainer?[,] bayMatrix, IList<int> optionRowList, Func<int, bool> ignoreFactor)
        {
            int? result = null;
            int minTierCount = Int32.MaxValue;
            int minNearCount = Int32.MaxValue;
            foreach (int r in optionRowList)
            {
                if (ignoreFactor(r))
                    continue;

                if (AllowMoveTo(r, prevOperation, fixingOrdinal, limitRow, limitTier, fixedTiers, topTiers, bayMatrix)) //翻箱约束条件
                {
                    int tierCount = topTiers[r];
                    int nearCount = Math.Abs(_row - r);
                    if (minTierCount > tierCount ||
                        minTierCount == tierCount && minNearCount >= nearCount)
                    {
                        result = r;
                        minTierCount = tierCount;
                        minNearCount = nearCount;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 选择翻入顺堆排
        /// </summary>
        public int? SelectUnderAscRow(MoveOperation? prevOperation, int fixingOrdinal, int limitRow, int limitTier, int[] fixedTiers, int[] topTiers, BayContainer?[,] bayMatrix, IList<int> ascRowList, Func<int, bool> ignoreFactor)
        {
            BayContainer? result = null;
            int minTierCount = Int32.MaxValue;
            int minNearCount = Int32.MaxValue;
            foreach (int r in ascRowList)
            {
                if (ignoreFactor(r))
                    continue;

                BayContainer topContainer = bayMatrix[r, topTiers[r]]!;
                if (topContainer._deliveryOrdinal < _deliveryOrdinal)
                    continue;

                if (AllowMoveTo(r, prevOperation, fixingOrdinal, limitRow, limitTier, fixedTiers, topTiers, bayMatrix)) //翻箱约束条件
                {
                    int tierCount = topTiers[r];
                    int nearCount = Math.Abs(_row - r);
                    if (result == null ||
                        result._deliveryOrdinal > topContainer._deliveryOrdinal ||
                        result._deliveryOrdinal == topContainer._deliveryOrdinal && minTierCount > tierCount ||
                        result._deliveryOrdinal == topContainer._deliveryOrdinal && minTierCount == tierCount && minNearCount >= nearCount)
                    {
                        result = topContainer;
                        minTierCount = tierCount;
                        minNearCount = nearCount;
                    }
                }
            }

            return result != null ? result._row : null;
        }

        private int? FindMinOrdinalRow(int[] fixedTiers, int[] topTiers, BayContainer?[,] bayMatrix, IList<int> optionRowList)
        {
            int? result = null;
            int minDeliveryOrdinal = Int32.MaxValue;
            int minImpededCount = Int32.MaxValue;
            int minNearCount = Int32.MaxValue;
            foreach (int r in optionRowList)
            {
                int maxDeliveryOrdinal = Int32.MinValue;
                for (int t = topTiers[r]; t > fixedTiers[r]; t--)
                    if (maxDeliveryOrdinal < bayMatrix[r, t]!._deliveryOrdinal)
                        maxDeliveryOrdinal = bayMatrix[r, t]!._deliveryOrdinal;

                int impededCount = topTiers[r] - fixedTiers[r];
                int nearCount = Math.Abs(_row - r);
                if (minDeliveryOrdinal > maxDeliveryOrdinal ||
                    minDeliveryOrdinal == maxDeliveryOrdinal && minImpededCount > impededCount ||
                    minDeliveryOrdinal == maxDeliveryOrdinal && minImpededCount == impededCount && minNearCount > nearCount)
                {
                    result = r;
                    minDeliveryOrdinal = maxDeliveryOrdinal;
                    minImpededCount = impededCount;
                    minNearCount = nearCount;
                }
            }

            return result;
        }

        private int? FindMinImpededRow(int fixingOrdinal, int[] fixedTiers, int[] topTiers, BayContainer?[,] bayMatrix, IList<int> optionRowList)
        {
            int? result = null;
            int minImpededCount = Int32.MaxValue;
            int minNearCount = Int32.MaxValue;
            foreach (int r in optionRowList)
            {
                int impededCount = topTiers[r] - fixedTiers[r];
                for (int t = topTiers[r]; t > fixedTiers[r]; t--)
                    if (bayMatrix[r, t]!._deliveryOrdinal == fixingOrdinal) //同号箱
                    {
                        impededCount = topTiers[r] - t;
                        break;
                    }

                int nearCount = Math.Abs(_row - r);
                if (minImpededCount > impededCount ||
                    minImpededCount == impededCount && minNearCount > nearCount)
                {
                    result = r;
                    minImpededCount = impededCount;
                    minNearCount = nearCount;
                }
            }

            return result;
        }

        /// <summary>
        /// 选择翻入临时排
        /// </summary>
        public int? SelectUnderTempRow(MoveOperation? prevOperation, int fixingOrdinal, int limitRow, int limitTier, int[] fixedTiers, int[] topTiers, BayContainer?[,] bayMatrix, Func<int, bool> ignoreFactor)
        {
            BayContainer? topContainer0 = bayMatrix[0, topTiers[0]];
            BayContainer? topContainer1 = bayMatrix[1, topTiers[1]];

            bool allowRow0 = !ignoreFactor(0) &&
                             AllowMoveTo(0, prevOperation, fixingOrdinal, limitRow, limitTier, fixedTiers, topTiers, bayMatrix);
            bool allowRow1 = !ignoreFactor(1) && fixedTiers[1] == 3 &&
                             AllowMoveTo(1, prevOperation, fixingOrdinal, limitRow, limitTier, fixedTiers, topTiers, bayMatrix);

            if (allowRow0 && allowRow1)
                if (topContainer0 != null && topContainer1 != null)
                    if (topContainer0._deliveryOrdinal <= _deliveryOrdinal && topContainer1._deliveryOrdinal <= _deliveryOrdinal)
                        return topContainer0._deliveryOrdinal <= topContainer1._deliveryOrdinal
                            ? 1
                            : 0;
                    else
                        return topContainer0._deliveryOrdinal <= _deliveryOrdinal
                            ? 0
                            : 1;
                else if (topContainer0 != null)
                    return topContainer0._deliveryOrdinal <= _deliveryOrdinal
                        ? 0
                        : 1;
                else if (topContainer1 != null)
                    return topContainer1._deliveryOrdinal <= _deliveryOrdinal
                        ? 1
                        : 0;
                else
                    return null;
            else if (allowRow0)
                return 0;
            else if (allowRow1)
                return 1;
            else
                return null;
        }

        /// <summary>
        /// 选择翻入合适排
        /// </summary>
        public (int?, string?) SelectUnderRightRow(MoveOperation? prevOperation, int fixingOrdinal, int limitRow, int limitTier, int[] fixedTiers, int[] topTiers, BayContainer?[,] bayMatrix, IList<int> descRowList, Func<int, bool> ignoreFactor)
        {
            //首先，翻入临时位未被占用或顶层箱发箱序号相同的临时排
            if (!ignoreFactor(0) && (topTiers[0] == 0 || bayMatrix[0, topTiers[0]]!._deliveryOrdinal == _deliveryOrdinal) &&
                AllowMoveTo(0, prevOperation, fixingOrdinal, limitRow, limitTier, fixedTiers, topTiers, bayMatrix))
                return (0, "翻入临时位都未被占用的临时排");
            if (!ignoreFactor(1) && topTiers[1] >= 3 && (fixedTiers[1] == 3 || bayMatrix[1, topTiers[1]]!._deliveryOrdinal == _deliveryOrdinal) &&
                AllowMoveTo(1, prevOperation, fixingOrdinal, limitRow, limitTier, fixedTiers, topTiers, bayMatrix))
                return (1, "翻入临时位都未被占用的临时排");

            //整理待翻入排
            List<int> optionRowList = new List<int>();
            foreach (int r in descRowList)
            {
                if (ignoreFactor(r))
                    continue;

                if (topTiers[r] > 0 && bayMatrix[r, topTiers[r]]!._deliveryOrdinal > _deliveryOrdinal)
                    continue;

                if (AllowMoveTo(r, prevOperation, fixingOrdinal, limitRow, limitTier, fixedTiers, topTiers, bayMatrix)) //翻箱约束条件
                    optionRowList.Add(r);
            }

            //要么，翻入后的逆堆排仍是逆堆排，其固定箱上压箱中最大发箱序号应该小于等于其他排压箱的最大发箱序号且压箱数最少，就近靠外的排翻入
            int? result = FindMinOrdinalRow(fixedTiers, topTiers, bayMatrix, optionRowList);
            if (result.HasValue)
                return (result.Value, "翻入后的逆堆排仍是逆堆排，其固定箱上压箱中最大发箱序号应该小于等于其他排压箱的最大发箱序号且压箱数最少，就近靠外的排翻入");

            //整理待翻入排
            optionRowList.Clear();
            for (int r = 0; r <= limitRow; r++)
            {
                if (ignoreFactor(r))
                    continue;

                if (topTiers[r] == fixedTiers[r]) //固定箱
                    continue;

                if (bayMatrix[r, topTiers[r]]!._deliveryOrdinal == fixingOrdinal) //同号箱
                    continue;

                if (AllowMoveTo(r, prevOperation, fixingOrdinal, limitRow, limitTier, fixedTiers, topTiers, bayMatrix)) //翻箱约束条件
                    optionRowList.Add(r);
            }

            //要么，翻入顶层不是固定箱和同号箱的排，其固定箱上压箱中最大发箱序号应该小于等于其他排压箱的最大发箱序号且压箱数最少，就近靠外的排翻入
            result = FindMinOrdinalRow(fixedTiers, topTiers, bayMatrix, optionRowList);
            if (result.HasValue)
                return (result.Value, "翻入顶层不是固定箱和同号箱的排，其固定箱上压箱中最大发箱序号应该小于等于其他排压箱的最大发箱序号且压箱数最少，就近靠外的排翻入");

            //要么，翻入顶层不是固定箱和同号箱的排，其固定箱上或同号箱上压箱数应该最少，就近靠外的排翻入
            result = FindMinImpededRow(fixingOrdinal, fixedTiers, topTiers, bayMatrix, optionRowList);
            if (result.HasValue)
                return (result.Value, "翻入顶层不是固定箱和同号箱的排，其固定箱上或同号箱上压箱数应该最少，就近靠外的排翻入");

            //要么，翻入顶层箱发箱序号与待翻箱最相近、就近的临时排，翻入后要么是单箱，要么尽可能是逆堆的
            int? tempRow = SelectUnderTempRow(prevOperation, fixingOrdinal, limitRow, limitTier, fixedTiers, topTiers, bayMatrix, ignoreFactor);
            if (tempRow.HasValue)
                return (tempRow, "翻入顶层箱发箱序号与待翻箱最相近、就近的临时排，翻入后要么是单箱，要么尽可能是逆堆的");

            //整理待翻入排
            optionRowList.Clear();
            for (int r = 0; r <= limitRow; r++)
            {
                if (ignoreFactor(r))
                    continue;

                if (AllowMoveTo(r, prevOperation, fixingOrdinal, limitRow, limitTier, fixedTiers, topTiers, bayMatrix)) //翻箱约束条件
                    optionRowList.Add(r);
            }

            //要么，翻入固定箱上压箱中最大发箱序号应该小于等于其他排压箱的最大发箱序号且压箱数最少，就近靠外的排翻入
            result = FindMinOrdinalRow(fixedTiers, topTiers, bayMatrix, optionRowList);
            if (result.HasValue)
                return (result.Value, "翻入固定箱上压箱中最大发箱序号应该小于等于其他排压箱的最大发箱序号且压箱数最少，就近靠外的排翻入");

            //要么，翻入固定箱上或同号箱上压箱数应该最少，就近靠外的排翻入
            result = FindMinImpededRow(fixingOrdinal, fixedTiers, topTiers, bayMatrix, optionRowList);
            if (result.HasValue)
                return (result.Value, "翻入固定箱上或同号箱上压箱数应该最少，就近靠外的排翻入");

            //要么，翻入固定箱上压箱数应该最少，就近靠外的排翻入
            result = FindMinImpededRow(0, fixedTiers, topTiers, bayMatrix, optionRowList);
            if (result.HasValue)
                return (result.Value, "翻入固定箱上压箱数应该最少，就近靠外的排翻入");

            return (null, null);
        }

        #endregion
    }
}