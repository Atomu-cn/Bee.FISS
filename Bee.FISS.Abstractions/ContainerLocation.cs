namespace Bee.CTOS.PreShipmentRestacking.Abstractions
{
    /// <summary>
    /// 箱位
    /// </summary>
    [GenerateSerializer]
    public class ContainerLocation
    {
        /// <summary>
        /// 初始化
        /// </summary>
        public ContainerLocation(string containerNo, int row, int tier)
        {
            _containerNo = containerNo;
            _row = row;
            _tier = tier;
        }

        #region 属性

        [Id(0)]
        private string _containerNo;

        /// <summary>
        /// 箱号
        /// </summary>
        public string ContainerNo
        {
            get { return _containerNo; }
        }

        [Id(1)]
        private int _row;

        /// <summary>
        /// 排
        /// </summary>
        public int Row
        {
            get { return _row; }
        }

        [Id(2)]
        private int _tier;

        /// <summary>
        /// 层
        /// </summary>
        public int Tier
        {
            get { return _tier; }
        }

        #endregion

        #region 方法

        /// <summary>
        /// 交换位置
        /// </summary>
        public void Exchange(ContainerLocation location)
        {
            int row = location._row;
            int tier = location._tier;
            location._row = _row;
            location._tier = _tier;
            _row = row;
            _tier = tier;
        }

        #endregion
    }
}