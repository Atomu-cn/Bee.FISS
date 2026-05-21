using Bee.CTOS.PreShipmentRestacking.Abstractions;
using Phenix.Core.DependencyInjection;

namespace Bee.CTOS.PreShipmentRestacking.Domain
{
    /// <summary>
    /// 单贝整理
    /// </summary>
    [Service(typeof(IBayRestackingService))]
    public class BayRestackingService : IBayRestackingService
    {
        #region 方法

        Task<IDictionary<string, int>> IBayRestackingService.SortDeliveryOrderAsync(int limitRow, int limitTier, IList<ContainerLocation> initialLocations, IDictionary<string, int> deliveryOrderDict)
        {
            IList<BayContainer> deliveryOrders = BayContainer.Fetch(limitRow, limitTier, initialLocations, deliveryOrderDict);
            IDictionary<string, int> result = new Dictionary<string, int>(deliveryOrders.Count);
            foreach (BayContainer item in deliveryOrders)
                result.Add(item.InitialLocation.ContainerNo, item.DeliveryOrdinal);
            return Task.FromResult(result);
        }

        private static void GroupRow(int row, int limitRow, int limitTier, int[] topTiers, BayContainer?[,] bayMatrix,
            ref List<int> peakRowList, ref List<int> emptyRowList, ref List<int> safeRowList, ref List<int> troughRowList)
        {
            peakRowList.Remove(row);
            emptyRowList.Remove(row);
            safeRowList.Remove(row);
            troughRowList.Remove(row);

            int totalAsc = BayContainer.TotalAsc(row, topTiers[row], bayMatrix);
            int leftGap = row > 1 ? totalAsc - BayContainer.TotalAsc(row - 1, topTiers[row - 1], bayMatrix) : 0;
            int rightGap = row < limitRow ? totalAsc - BayContainer.TotalAsc(row + 1, topTiers[row + 1], bayMatrix) : 0;

            if (leftGap > 2 || rightGap > 2 ||
                row == 0 && topTiers[row] > 0 ||
                row == 1 && topTiers[row] > 3)
                peakRowList.Add(row);

            if (row == 0)
                return;

            if (topTiers[row] == 0)
                emptyRowList.Add(row);

            if (topTiers[row] < (row == limitRow ? limitTier + 1 : limitTier))
                if (row > 1 && leftGap < 2 && rightGap < 2 ||
                    row == 1 && topTiers[row] < 3)
                    safeRowList.Add(row);

            if (leftGap < -2 || rightGap < -2)
                troughRowList.Add(row);
        }

        private static void GroupRow(int row, int[] fixedTiers, int[] topTiers, BayContainer?[,] bayMatrix,
            ref List<int> ascRowList, ref List<int> descRowList, ref List<int> singleRowList)
        {
            if (row == 0)
                return;

            ascRowList.Remove(row);
            descRowList.Remove(row);
            singleRowList.Remove(row);

            bool equally = true;
            for (int t = topTiers[row]; t >= 2; t--)
            {
                int diff = bayMatrix[row, t]!.DeliveryOrdinal - bayMatrix[row, t - 1]!.DeliveryOrdinal;
                if (diff > 0)
                {
                    if (equally)
                        descRowList.Add(row);

                    if (topTiers[row] == fixedTiers[row] + 1)
                        singleRowList.Add(row);

                    return;
                }

                if (diff < 0)
                    equally = false;
            }

            if (topTiers[row] > 0)
            {
                if (row == 1 && topTiers[row] >= 3)
                {
                    descRowList.Add(row);
                    return;
                }

                ascRowList.Add(row);

                if (topTiers[row] == 1 && fixedTiers[row] == 0)
                    singleRowList.Add(row);
            }
        }

        private static void GroupRow(MoveOperation operation, int limitRow, int limitTier, int[] fixedTiers, int[] topTiers, BayContainer?[,] bayMatrix,
            ref List<int> peakRowList, ref List<int> emptyRowList, ref List<int> safeRowList, ref List<int> troughRowList,
            ref List<int> ascRowList, ref List<int> descRowList, ref List<int> singleRowList)
        {
            for (int r = 0; r <= limitRow; r++)
                if (r >= operation.ReadyRow - 1 && r <= operation.ReadyRow + 1 ||
                    r >= operation.UnderRow - 1 && r <= operation.UnderRow + 1)
                    GroupRow(r, limitRow, limitTier, topTiers, bayMatrix, ref peakRowList, ref emptyRowList, ref safeRowList, ref troughRowList);
            GroupRow(operation.ReadyRow, fixedTiers, topTiers, bayMatrix, ref ascRowList, ref descRowList, ref singleRowList);
            GroupRow(operation.UnderRow, fixedTiers, topTiers, bayMatrix, ref ascRowList, ref descRowList, ref singleRowList);

            peakRowList.Sort();
            emptyRowList.Sort();
            safeRowList.Sort();
            troughRowList.Sort();
            ascRowList.Sort();
            descRowList.Sort();
            singleRowList.Sort();
        }

        private static bool HaveNonAscRow(int limitRow, IList<int> ascRowList)
        {
            for (int r = 2; r <= limitRow; r++)
                if (!ascRowList.Contains(r))
                    return true;
            return false;
        }

        Task<MoveOperation[]> IBayRestackingService.ExecuteBayRestackingAsync(string ownerId, int limitRow, int limitTier, IList<ContainerLocation> initialLocations, IDictionary<string, int> deliveryOrderDict)
        {
            List<MoveOperation> result = new List<MoveOperation>();
            //获取贝内所有集装箱的箱位记录及装船发箱顺序，按照发箱顺序递增标记发箱序号，起始号1
            IList<BayContainer> deliveryOrders = BayContainer.Fetch(limitRow, limitTier, initialLocations, deliveryOrderDict, out int[] initialTopTiers, out BayContainer?[,] bayMatrix);
            if (deliveryOrders.Count <= 1)
                return Task.FromResult(result.ToArray());

            MoveOperation? operation = null;
            int operationIndex = 1;
            int[] topTiers = (int[])initialTopTiers.Clone();
            int[] fixedTiers = new int[limitRow + 1]; //各排固定层（弃用0下标）

            List<int> peakRowList = new List<int>(limitRow); //超高峰
            List<int> emptyRowList = new List<int>(limitRow); //无箱排
            List<int> safeRowList = new List<int>(limitRow); //安全排
            List<int> troughRowList = new List<int>(limitRow); //超低谷
            List<int> ascRowList = new List<int>(limitRow + 1); //顺堆排
            List<int> descRowList = new List<int>(limitRow + 1) { 0 }; //逆堆排
            List<int> singleRowList = new List<int>(limitRow); //单箱排

            //标记顺堆排、逆堆排、超高峰、安全排、超低谷、无箱排、单箱排
            for (int r = 1; r <= limitRow; r++)
            {
                GroupRow(r, limitRow, limitTier, topTiers, bayMatrix, ref peakRowList, ref emptyRowList, ref safeRowList, ref troughRowList);
                GroupRow(r, fixedTiers, topTiers, bayMatrix, ref ascRowList, ref descRowList, ref singleRowList);
            }

            //遍历发箱序号从大到小排列的集装箱逆序清单，逐一作为当前箱处理为固定箱，直到所有都标记为固定箱
            int i = 0;
            int targetRow = 0; //目标排
            bool precludePrevTarget = false; //排除之前的目标排
            while (i < deliveryOrders.Count)
            {
                BayContainer currentContainer = deliveryOrders[i];

                int limit = i;
                while (limit + 1 < deliveryOrders.Count)
                    if (deliveryOrders[limit + 1].DeliveryOrdinal == currentContainer.DeliveryOrdinal)
                        limit = limit + 1;
                    else
                        break;

                //如果当前箱可以标记为固定箱，则标记为固定箱，遍历到下一个集装箱继续处理
                do
                {
                    bool succeed = false;
                    for (int j = i; j <= limit; j++)
                        if (deliveryOrders[j].TryFix(currentContainer.DeliveryOrdinal, limitRow, ref fixedTiers, topTiers, bayMatrix))
                        {
                            succeed = true;
                            if (j == i)
                            {
                                i = i + 1; //遍历到下一个
                                break;
                            }
                        }

                    if (!succeed)
                        break;
                } while (true);

                //如果当前箱无法标记为固定箱，而同号箱中有可以标记为固定箱的，则交换为当前箱并标记为固定箱，然后继续处理剩余的同号箱，直到没有可以标记为固定箱为止
                int index = limit;
                while (i <= index)
                    if (deliveryOrders[index].IsFixed(fixedTiers))
                    {
                        (deliveryOrders[index], deliveryOrders[i]) = (deliveryOrders[i], deliveryOrders[index]);
                        i = i + 1; //遍历到下一个
                    }
                    else
                        index = index - 1;

                //尝试将压箱数最少、所在排顶层发箱序号最小、靠外的同号箱交换为当前箱
                index = i;
                int minImpededCount = Int32.MaxValue;
                BayContainer? minTopContainer = null;
                for (int j = i; j <= limit; j++)
                {
                    BayContainer container = deliveryOrders[j];
                    BayContainer topContainer = bayMatrix[container.Row, topTiers[container.Row]]!;
                    int impededCount = topContainer.Tier - container.Tier;
                    if (minImpededCount > impededCount ||
                        minImpededCount == impededCount && minTopContainer != null && minTopContainer.DeliveryOrdinal > topContainer.DeliveryOrdinal ||
                        minImpededCount == impededCount && minTopContainer != null && minTopContainer.DeliveryOrdinal == topContainer.DeliveryOrdinal && minTopContainer.Row >= topContainer.Row)
                    {
                        index = j;
                        minImpededCount = impededCount;
                        minTopContainer = topContainer;
                    }
                }

                if (index > i)
                    (deliveryOrders[index], deliveryOrders[i]) = (deliveryOrders[i], deliveryOrders[index]);

                int fixingCount = limit - i + 1;
                if (fixingCount < 1)
                    continue;

                currentContainer = deliveryOrders[i];

                //为当前箱选择目标排
                targetRow = currentContainer.SelectTargetRow(fixingCount, limitRow, limitTier, fixedTiers, topTiers, bayMatrix, troughRowList, ascRowList,
                    row => row == targetRow && precludePrevTarget || ascRowList.Contains(row) && peakRowList.Contains(row));
                precludePrevTarget = false;

                List<int> operationRowList = currentContainer.Row < targetRow
                    ? new List<int> { currentContainer.Row, targetRow }
                    : currentContainer.Row > targetRow
                        ? new List<int> { targetRow, currentContainer.Row }
                        : new List<int> { currentContainer.Row };

                //在满足翻箱约束条件下，翻出当前箱和目标排固定箱上全部压箱，并尽可能把其他排翻成顺堆排，直到当前箱上和目标排固定箱上没有压箱为止
                bool breakMove = false;
                while (targetRow != currentContainer.Row
                           ? topTiers[targetRow] > fixedTiers[targetRow] || topTiers[currentContainer.Row] > currentContainer.Tier
                           : topTiers[targetRow] > fixedTiers[targetRow])
                {
                    BayContainer? readyContainer = null;
                    int underRow = 0;
                    string remark = String.Empty;

                    //除了目标排之外，如果还有其他固定排，可尝试将集装箱逆序清单中紧随当前箱之后、处于顶层的集装箱翻入
                    //统计其他固定排上可堆固定箱的空位数量
                    //从紧随当前箱之后的集装箱起向后遍历集装箱逆序清单，找到位于非顺堆排顶层的集装箱为止，或者遍历次数达到空位数量为止
                    //将遍历到的集装箱翻出，选择其他固定排就低就近靠内的排翻入
                    int emptyCount = 0;
                    List<int> underRowList = new List<int>(limitRow);
                    for (int r = 1; r <= limitRow; r++)
                    {
                        if (r == targetRow)
                            continue;

                        if (topTiers[r] == 0 || topTiers[r] == fixedTiers[r])
                        {
                            int count = (r == 1 ? 3 : r == limitRow ? limitTier + 1 : limitTier) - topTiers[r];
                            if (count > 0)
                            {
                                emptyCount = emptyCount + count;
                                underRowList.Add(r);
                            }
                        }
                    }

                    for (int j = i + 1; j <= (i + emptyCount < deliveryOrders.Count ? i + emptyCount : deliveryOrders.Count - 1); j++)
                    {
                        BayContainer container = deliveryOrders[j];
                        if (container.Tier < topTiers[container.Row])
                            continue;
                        if (ascRowList.Contains(container.Row))
                            continue;

                        int? nearRow = container.SelectUnderNearRow(operation, container.DeliveryOrdinal, limitRow, limitTier, fixedTiers, topTiers, bayMatrix, underRowList,
                            row => operationRowList.Contains(row) || ascRowList.Contains(row) && peakRowList.Contains(row));
                        if (nearRow.HasValue)
                        {
                            readyContainer = container;
                            underRow = nearRow.Value;
                            remark = "整理固定排-除了目标排之外，如果还有其他固定排，可尝试将集装箱逆序清单中紧随当前箱之后、处于顶层的集装箱翻入";
                            break;
                        }
                    }

                    //如果以上未能翻动，选择一个当前排、目标排、非顺堆排的顶层压箱翻入顺堆排
                    //首先，待翻箱发箱序号应该等于翻入排顶层箱，选靠外的排翻出，当前排和目标排顶层压箱优先
                    if (readyContainer == null)
                        for (int r = 0; r <= limitRow; r++)
                        {
                            if (topTiers[r] == fixedTiers[r])
                                continue;
                            if (!operationRowList.Contains(r) && ascRowList.Contains(r))
                                continue;
                            BayContainer topContainer = bayMatrix[r, topTiers[r]]!;
                            if (targetRow != currentContainer.Row && topContainer == currentContainer)
                                continue;
                            if (readyContainer != null)
                            {
                                if (readyContainer.DeliveryOrdinal > topContainer.DeliveryOrdinal)
                                    continue;
                                if (readyContainer.DeliveryOrdinal == topContainer.DeliveryOrdinal && !operationRowList.Contains(r))
                                    continue;
                            }

                            int? ascRow = topContainer.SelectUnderAscRow(operation, currentContainer.DeliveryOrdinal, limitRow, limitTier, fixedTiers, topTiers, bayMatrix, ascRowList,
                                row => operationRowList.Contains(row) || ascRowList.Contains(row) && peakRowList.Contains(row) || bayMatrix[row, topTiers[row]]!.DeliveryOrdinal != topContainer.DeliveryOrdinal);
                            if (ascRow.HasValue)
                            {
                                readyContainer = topContainer;
                                underRow = ascRow.Value;
                                remark = "翻入顺堆排-待翻箱发箱序号应该等于翻入排顶层箱，选靠外的排翻出，当前排和目标排顶层压箱优先";
                            }
                        }

                    //要么，待翻箱发箱序号应该大于等于单箱排和非顺堆排上的顶层压箱且大于等于当前箱和目标排固定箱上全部压箱，选靠外的排翻出，当前排和目标排顶层压箱优先
                    if (readyContainer == null)
                        for (int r = 0; r <= limitRow; r++)
                        {
                            if (topTiers[r] == fixedTiers[r])
                                continue;
                            if (!operationRowList.Contains(r) && ascRowList.Contains(r))
                                continue;
                            BayContainer topContainer = bayMatrix[r, topTiers[r]]!;
                            if (targetRow != currentContainer.Row && topContainer == currentContainer)
                                continue;
                            if (!topContainer.IsMaxOrdinal(limitRow, topTiers, bayMatrix, false, row => row == 0 || !singleRowList.Contains(row) && ascRowList.Contains(row)))
                                continue;
                            if (readyContainer != null)
                            {
                                if (readyContainer.DeliveryOrdinal > topContainer.DeliveryOrdinal)
                                    continue;
                                if (readyContainer.DeliveryOrdinal == topContainer.DeliveryOrdinal && !operationRowList.Contains(r))
                                    continue;
                            }

                            bool find = false;
                            if (targetRow != currentContainer.Row)
                                for (int t = topTiers[currentContainer.Row]; t > currentContainer.Tier; t--)
                                    if (topContainer.DeliveryOrdinal < bayMatrix[currentContainer.Row, t]!.DeliveryOrdinal)
                                    {
                                        find = true;
                                        break;
                                    }

                            if (find)
                                continue;

                            for (int t = topTiers[targetRow]; t > fixedTiers[targetRow]; t--)
                                if (topContainer.DeliveryOrdinal < bayMatrix[targetRow, t]!.DeliveryOrdinal)
                                {
                                    find = true;
                                    break;
                                }

                            if (find)
                                continue;

                            int? ascRow = topContainer.SelectUnderAscRow(operation, currentContainer.DeliveryOrdinal, limitRow, limitTier, fixedTiers, topTiers, bayMatrix, ascRowList,
                                row => operationRowList.Contains(row) || ascRowList.Contains(row) && peakRowList.Contains(row));
                            if (ascRow.HasValue)
                            {
                                readyContainer = topContainer;
                                underRow = ascRow.Value;
                                remark = "翻入顺堆排-待翻箱发箱序号应该大于等于单箱排和非顺堆排上的顶层压箱且大于等于当前箱和目标排固定箱上全部压箱，选靠外的排翻出，当前排和目标排顶层压箱优先";
                            }
                        }

                    //要么，翻动后的逆堆排可成为顺堆排，其次层箱发箱序号应该大于等于逆堆排次层箱且大于等于当前排和目标排的顶层压箱，选靠外的排翻出
                    if (readyContainer == null)
                    {
                        BayContainer? readyEmergeContainer = null;
                        BayContainer currentRowTopContainer = bayMatrix[currentContainer.Row, topTiers[currentContainer.Row]]!;
                        BayContainer? targetRowTopContainer = bayMatrix[targetRow, topTiers[targetRow]];
                        int maxDeliveryOrdinal = targetRowTopContainer != null
                            ? Int32.Max(currentRowTopContainer.DeliveryOrdinal, targetRowTopContainer.DeliveryOrdinal)
                            : currentRowTopContainer.DeliveryOrdinal;
                        foreach (int r in descRowList)
                        {
                            if (r == 0 && topTiers[r] <= 1)
                                continue;
                            BayContainer topContainer = bayMatrix[r, topTiers[r]]!;
                            if (targetRow != currentContainer.Row && topContainer == currentContainer)
                                continue;
                            if (readyContainer != null && readyContainer.DeliveryOrdinal >= topContainer.DeliveryOrdinal)
                                continue;

                            BayContainer emergeContainer = bayMatrix[topContainer.Row, topContainer.Tier - 1]!;
                            if (readyEmergeContainer == null || readyEmergeContainer.DeliveryOrdinal < emergeContainer.DeliveryOrdinal)
                            {
                                bool ascending = true;
                                for (int t = emergeContainer.Tier; t >= 2; t--)
                                    if (bayMatrix[emergeContainer.Row, t]!.DeliveryOrdinal > bayMatrix[emergeContainer.Row, t - 1]!.DeliveryOrdinal)
                                    {
                                        ascending = false;
                                        break;
                                    }

                                if (!ascending)
                                    continue;

                                readyEmergeContainer = emergeContainer;
                            }

                            if (readyEmergeContainer.DeliveryOrdinal < maxDeliveryOrdinal)
                                continue;

                            int? ascRow = topContainer.SelectUnderAscRow(operation, currentContainer.DeliveryOrdinal, limitRow, limitTier, fixedTiers, topTiers, bayMatrix, ascRowList,
                                row => operationRowList.Contains(row) || ascRowList.Contains(row) && peakRowList.Contains(row));
                            if (ascRow.HasValue)
                            {
                                readyContainer = topContainer;
                                underRow = ascRow.Value;
                                remark = "翻入顺堆排-翻动后的逆堆排可成为顺堆排，其次层箱发箱序号应该大于等于逆堆排次层箱且大于等于当前排和目标排的顶层压箱，选靠外的排翻出";
                            }
                        }
                    }

                    //要么，选择一个当前排或目标排顶层压箱翻入合适的其他排
                    //待翻箱是当前排和目标排顶层压箱中发箱序号最小的，选靠外的排翻出
                    if (readyContainer == null)
                        foreach (int r in operationRowList)
                        {
                            if (topTiers[r] == fixedTiers[r])
                                continue;
                            BayContainer topContainer = bayMatrix[r, topTiers[r]]!;
                            if (targetRow != currentContainer.Row && topContainer == currentContainer)
                                continue;
                            if (readyContainer != null && readyContainer.DeliveryOrdinal <= topContainer.DeliveryOrdinal)
                                continue;

                            (int?, string?) selected = topContainer.SelectUnderRightRow(operation, currentContainer.DeliveryOrdinal, limitRow, limitTier, fixedTiers, topTiers, bayMatrix, descRowList,
                                row => operationRowList.Contains(row));
                            if (selected.Item1.HasValue)
                            {
                                readyContainer = topContainer;
                                underRow = selected.Item1.Value;
                                remark = "翻入合适排-" + selected.Item2;
                            }
                        }

                    if (readyContainer != null)
                    {
                        //将待翻箱翻入选择的排
                        //翻动后，将当前翻箱动作登记到翻箱操作步骤中
                        //翻动后，标记顺堆排、逆堆排、超高峰、安全排、超低谷、无箱排、单箱排
                        operation = readyContainer.MoveTo(ownerId, operationIndex, underRow, currentContainer.DeliveryOrdinal, currentContainer.Row, targetRow, limitRow, ref fixedTiers, ref topTiers, ref bayMatrix, remark);
                        result.Add(operation);
                        operationIndex = operationIndex + 1;
                        GroupRow(operation, limitRow, limitTier, fixedTiers, topTiers, bayMatrix, ref peakRowList, ref emptyRowList, ref safeRowList, ref troughRowList, ref ascRowList, ref descRowList, ref singleRowList);

                        //如果贝内除了最外排，其他全都是顺堆排或无箱排，则不再继续翻动
                        if (!HaveNonAscRow(limitRow, ascRowList))
                        {
                            breakMove = true;
                            break;
                        }

                        //如果翻出或翻入的排变成固定排且不是目标排时，则重新为当前箱选择目标排后再继续翻动，选择时不需要排除旧的目标排
                        if (topTiers[operation.ReadyRow] == fixedTiers[operation.ReadyRow] && operation.ReadyRow != targetRow ||
                            topTiers[operation.UnderRow] == fixedTiers[operation.UnderRow] && operation.UnderRow != targetRow)
                        {
                            precludePrevTarget = false;
                            breakMove = true;
                            break;
                        }
                    }
                    else //未能翻动，则重新为当前箱选择目标排后再继续翻动，选择时需要排除旧的目标排
                    {
                        precludePrevTarget = true;
                        breakMove = true;
                        break;
                    }
                }

                if (breakMove)
                    continue;

                //将当前箱翻入目标排，并标记为固定箱
                //翻动后，将当前翻箱动作登记到翻箱操作步骤中
                //翻动后，标记顺堆排、逆堆排、超高峰、安全排、超低谷、无箱排、单箱排
                operation = currentContainer.MoveTo(ownerId, operationIndex, targetRow, currentContainer.DeliveryOrdinal, currentContainer.Row, targetRow, limitRow, ref fixedTiers, ref topTiers, ref bayMatrix, "将当前箱翻入目标排，并标记为固定箱");
                result.Add(operation);
                operationIndex = operationIndex + 1;
                GroupRow(operation, limitRow, limitTier, fixedTiers, topTiers, bayMatrix, ref peakRowList, ref emptyRowList, ref safeRowList, ref troughRowList, ref ascRowList, ref descRowList, ref singleRowList);
                
                //继续遍历，
                i = i + 1;
            }

            //梳理翻箱操作步骤，优化暂落性质的翻箱动作，尝试缩短搬运距离、减少翻箱次数
            if (result.Count > 1)
            {
                topTiers = (int[])initialTopTiers.Clone();

                //从第一个开始遍历翻箱动作
                i = 0;
                while (i < result.Count)
                {
                    MoveOperation operation0 = result[i];
                    MoveOperation? operation1 = null;
                    int limit = i + 1;
                    while (limit < result.Count)
                    {
                        if (operation0.ContainerNo == result[limit].ContainerNo)
                        {
                            operation1 = result[limit];
                            break;
                        }

                        limit = limit + 1;
                    }

                    //如果一箱的前后两个翻箱动作中间的暂落排，期间仅是该箱在翻动，则尝试将暂落排替换为期间没有过翻箱动作、未达层高限值、与终落排最近的排
                    if (operation1 != null)
                    {
                        bool find = false;
                        List<int> ignoreRowList = new List<int>();
                        for (int j = i + 1; j < limit; j++)
                            if (result[j].ReadyRow == operation1.ReadyRow || result[j].UnderRow == operation1.ReadyRow)
                            {
                                find = true;
                                break;
                            }
                            else
                            {
                                ignoreRowList.Add(result[j].ReadyRow);
                                ignoreRowList.Add(result[j].UnderRow);
                            }

                        if (!find)
                        {
                            int? minNearRow = null;
                            int minNearCount = Int32.MaxValue;
                            for (int r = 0; r <= limitRow; r++)
                            {
                                if (ignoreRowList.Contains(r))
                                    continue;
                                if (topTiers[r] >= (r == 0 || r == limitRow ? limitTier + 1 : limitTier))
                                    continue;

                                int nearCount = Math.Abs(operation1.UnderRow - r);
                                if (minNearCount > nearCount)
                                {
                                    minNearRow = r;
                                    minNearCount = nearCount;
                                }
                            }

                            if (minNearRow.HasValue)
                                operation0.ChangeMomentRow(operation1, minNearRow.Value);
                        }
                    }

                    //如果一箱有在多个排上翻入翻出，期间这些排上仅是该箱在翻动，则合并这些翻箱动作，将第一个的翻入排号替换为最后一个的翻入排号，然后舍弃除第一个之外的多余动作
                    if (operation1 != null)
                    {
                        bool find = false;
                        for (int j = i + 1; j < limit; j++)
                            if (result[j].ReadyRow == operation1.ReadyRow || result[j].UnderRow == operation1.ReadyRow ||
                                result[j].ReadyRow == operation1.UnderRow || result[j].UnderRow == operation1.UnderRow)
                            {
                                find = true;
                                break;
                            }

                        if (!find)
                        {
                            operation0.MergeNext(operation1);
                            result.Remove(operation1);
                            continue;
                        }
                    }

                    //舍弃翻入排号和翻出排号相同的翻箱动作
                    if (operation0.ReadyRow == operation0.UnderRow)
                    {
                        operation0.MergeNext(operation0);
                        result.Remove(operation0);
                        continue;
                    }

                    topTiers[operation0.ReadyRow] = topTiers[operation0.ReadyRow] - 1;
                    topTiers[operation0.UnderRow] = topTiers[operation0.UnderRow] + 1;

                    //继续遍历，直到遍历完所有翻箱动作为止
                    i = i + 1;
                }
            }

            //返回梳理完成的翻箱操作步骤，依此可制定出一套翻箱作业计划
            return Task.FromResult(result.ToArray());
        }

        #endregion
    }
}