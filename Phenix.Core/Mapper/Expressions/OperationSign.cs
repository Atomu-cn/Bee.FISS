using System;
using Phenix.Core.Data;

namespace Phenix.Core.Mapper.Expressions
{
    /// <summary>
    /// 运算符号
    /// </summary>
    [Serializable]
    public enum OperationSign
    {
        /// <summary>
        /// 无 
        /// </summary>
        None,

        /// <summary>
        /// +
        /// </summary>
        [EnumCaption("加上", Key = "+")]
        Add,

        /// <summary>
        /// -
        /// </summary>
        [EnumCaption("减去", Key = "-")]
        Subtract,

        /// <summary>
        /// *
        /// </summary>
        [EnumCaption("乘以", Key = "*")]
        Multiply,

        /// <summary>
        /// /
        /// </summary>
        [EnumCaption("除以", Key = "/")]
        Divide,

        /// <summary>
        /// 字符数
        /// </summary>
        [EnumCaption("字符数", Key = " Length")]
        Length,

        /// <summary>
        /// 转换为小写形式
        /// </summary>
        [EnumCaption("转换为小写形式", Key = " Lower")]
        ToLower,

        /// <summary>
        /// 转换为大写形式
        /// </summary>
        [EnumCaption("转换为大写形式", Key = " Upper")]
        ToUpper,

        /// <summary>
        /// 去除字符串左边的空格
        /// </summary>
        [EnumCaption("去除字符串左边的空格", Key = " LTrim")]
        TrimStart,

        /// <summary>
        /// 去除字符串右边的空格
        /// </summary>
        [EnumCaption("去除字符串右边的空格", Key = " RTrim")]
        TrimEnd,

        /// <summary>
        /// 去除字符串左右两边的空格
        /// </summary>
        [EnumCaption("去除字符串左右两边的空格", Key = " Trim")]
        Trim,

        /// <summary>
        /// 截取字符串
        /// </summary>
        [EnumCaption("截取字符串", Key = " Substr")]
        Substring
    }
}