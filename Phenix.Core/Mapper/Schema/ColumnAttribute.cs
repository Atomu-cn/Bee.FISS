using System;

namespace Phenix.Core.Mapper.Schema
{
    /// <summary>
    /// 字段映射标签
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ColumnAttribute : Attribute
    {
        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="name">字段名</param>
        public ColumnAttribute(string name)
            : base()
        {
            _name = name ?? String.Empty;
        }

        #region 属性
        
        private readonly string _name;

        /// <summary>
        /// 字段名
        /// </summary>
        public string Name
        {
            get { return _name; }
        }

        #endregion
    }
}