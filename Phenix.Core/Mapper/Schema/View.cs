using System;
using System.Collections.Generic;
using Phenix.Core.Data;
using Phenix.Core.Data.Common;

namespace Phenix.Core.Mapper.Schema
{
    /// <summary>
    /// 视图
    /// </summary>
    [Serializable]
    public sealed class View : Sheet
    {
        [Newtonsoft.Json.JsonConstructor]
        private View(string name, string description, IDictionary<string, Column> columns, string viewText)
            : base(name, description, columns)
        {
            _viewText = viewText;
        }

        internal View(MetaData owner, string name, string description, string viewText)
            : base(owner, name, description)
        {
            _viewText = SqlHelper.ClearComment(viewText);
            int i = _viewText.LastIndexOf(" with read only", StringComparison.OrdinalIgnoreCase);
            if (i == _viewText.Length - 16)
                _viewText = _viewText.Remove(i);
        }

        #region 属性

        private readonly string _viewText;

        /// <summary>
        /// ViewText
        /// </summary>
        public string ViewText
        {
            get { return _viewText; }
        }

        [NonSerialized]
        private IDictionary<string, Table> _tables;

        /// <summary>
        /// 数据源
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public IDictionary<string, Table> Tables
        {
            get { return _tables ??= Owner.ExtractViewTables(ViewText); }
        }

        [NonSerialized]
        private string _className;

        /// <summary>
        /// 类名
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public override string ClassName
        {
            get { return _className ??= Standards.GetPascalCasingByViewName(Name, String.Compare(Prefix, "PH7", StringComparison.OrdinalIgnoreCase) == 0); }
            set { _className = value; }
        }

        [NonSerialized]
        private string _prefix;

        /// <summary>
        /// 前缀
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public override string Prefix
        {
            get { return _prefix ??= Standards.GetPrefixBySheetName(Name); }
        }

        #endregion

        #region 方法

        /// <summary>
        /// 检索表
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <returns>表</returns>
        public Table FindTable(string tableName)
        {
            return tableName != null && Tables.TryGetValue(tableName, out Table result) ? result : null;
        }

        /// <summary>
        /// 检索表字段
        /// </summary>
        /// <param name="columnAlias">字段别名</param>
        /// <returns>表字段</returns>
        public Column FindTableColumn(string columnAlias)
        {
            string columnExpression = SqlHelper.FindColumnExpression(SqlHelper.GetColumnBody(ViewText), columnAlias);
            string columnSource = SqlHelper.ExtractColumnSource(columnExpression);
            string shortColumnName = SqlHelper.ExtractShortColumnName(columnExpression);
            if (!String.IsNullOrEmpty(shortColumnName))
                foreach (KeyValuePair<string, List<string>> kvp in SqlHelper.GetSourceBody(ViewText))
                foreach (string tableAlias in kvp.Value)
                    if (String.IsNullOrEmpty(columnSource) || String.Compare(tableAlias, columnSource, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        Table table = Owner.FindTable(kvp.Key);
                        if (table != null)
                        {
                            Column result = table.FindColumn(shortColumnName);
                            if (result != null)
                                return result;
                            if (!String.IsNullOrEmpty(columnSource))
                                return null;
                        }

                        View view = Owner.FindView(kvp.Key);
                        if (view != null)
                            return view.FindTableColumn(shortColumnName);
                        if (!String.IsNullOrEmpty(columnSource))
                            return null;
                    }

            return null;
        }

        /// <summary>
        /// 检索表字段
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <param name="columnAlias">字段别名</param>
        /// <returns>表字段</returns>
        public Column FindTableColumn(string tableName, string columnAlias)
        {
            Table table = FindTable(tableName);
            if (table != null)
            {
                Column result = table.FindColumn(columnAlias);
                if (result != null)
                    return result;
            }

            return FindTableColumn(columnAlias);
        }

        #endregion
    }
}