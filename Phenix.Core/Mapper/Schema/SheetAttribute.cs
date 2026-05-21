using System;

namespace Phenix.Core.Mapper.Schema
{
    /// <summary>
    /// 表/视图映射标签
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class SheetAttribute : Attribute
    {
        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="name">表名/视图名</param>
        public SheetAttribute(string name)
            : base()
        {
            _name = name ?? String.Empty;
        }

        #region 属性
        
        private readonly string _name;

        /// <summary>
        /// 表名/视图名
        /// </summary>
        public string Name
        {
            get { return _name; }
        }

        /// <summary>
        /// 主键名
        /// </summary>
        public string PrimaryKeyName { get; set; }

        #endregion
    }
}
