namespace Bee.CTOS.PreShipmentRestacking.Abstractions
{
    /// <summary>
    /// 翻箱动作
    /// </summary>
    [GenerateSerializer]
    public class MoveOperation
    {
        /// <summary>
        /// 初始化
        /// </summary>
        public MoveOperation(string id, string ownerId, int operationIndex, string containerNo, int readyRow, int underRow, int fixingOrdinal, bool isFixed, int currentRow, int targetRow, string remark)
        {
            _id = id;
            _ownerId = ownerId;
            _operationIndex = operationIndex;
            _containerNo = containerNo;
            _readyRow = readyRow;
            _underRow = underRow;
            _fixingOrdinal = fixingOrdinal;
            _isFixed = isFixed;
            _currentRow = currentRow;
            _targetRow = targetRow;
            _remark = remark;
        }

        #region 属性

        [Id(0)]
        private readonly string _id;

        /// <summary>
        /// ID
        /// </summary>
        public string Id => _id;

        [Id(1)]
        private readonly string _ownerId;

        /// <summary>
        /// 所属翻箱ID
        /// </summary>
        public string OwnerId => _ownerId;

        [Id(2)]
        private readonly int _operationIndex;

        /// <summary>
        /// 动作序号
        /// </summary>
        public int OperationIndex => _operationIndex;

        [Id(3)]
        private readonly string _containerNo;

        /// <summary>
        /// 箱号
        /// </summary>
        public string ContainerNo => _containerNo;

        [Id(4)]
        private int _readyRow;

        /// <summary>
        /// 翻出排
        /// </summary>
        public int ReadyRow => _readyRow;

        [Id(5)]
        private int _underRow;

        /// <summary>
        /// 翻入排
        /// </summary>
        public int UnderRow => _underRow;

        [Id(6)]
        private readonly int _fixingOrdinal;

        /// <summary>
        /// 固定序号
        /// </summary>
        public int FixingOrdinal => _fixingOrdinal;

        [Id(7)]
        private readonly bool _isFixed;

        /// <summary>
        /// 是否固定
        /// </summary>
        public bool IsFixed => _isFixed;

        [Id(8)]
        private readonly int _currentRow;

        /// <summary>
        /// 当前排
        /// </summary>
        public int CurrentRow => _currentRow;

        [Id(9)]
        private readonly int _targetRow;

        /// <summary>
        /// 目的排
        /// </summary>
        public int TargetRow => _targetRow;

        [Id(10)]
        private string _remark;

        /// <summary>
        /// 备注
        /// </summary>
        public string Remark => _remark;

        #endregion

        #region 方法

        /// <summary>
        /// 更改暂落排
        /// </summary>
        public void ChangeMomentRow(MoveOperation nextOperation, int newMomentRow)
        {
            if (_underRow == newMomentRow)
                return;

            int oldMomentRow = _underRow;
            _underRow = newMomentRow;
            nextOperation._readyRow = newMomentRow;

            _remark = _remark + "(" + oldMomentRow + " -> " + newMomentRow + ")";
        }

        /// <summary>
        /// 合并下一个动作
        /// </summary>
        public void MergeNext(MoveOperation nextOperation)
        {
            _underRow = nextOperation._underRow;

            _remark = _remark + " -> (" + nextOperation._readyRow + ") -> " + nextOperation._remark;
        }

        #endregion
    }
}