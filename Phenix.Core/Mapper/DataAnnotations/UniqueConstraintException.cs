using System;
using System.Collections.Generic;

#if PgSQL
using Npgsql;
#endif
#if MsSQL
using Microsoft.Data.SqlClient;
#endif
#if MySQL
using MySql.Data.MySqlClient;
#endif
#if ORA
using Oracle.ManagedDataAccess.Client;
#endif

using Phenix.Core.Data.Validation;
using Phenix.Core.Mapper.Schema;

namespace Phenix.Core.Mapper.DataAnnotations
{
    /// <summary>
    /// 唯一约束异常
    /// </summary>
    [Serializable]
    public class UniqueConstraintException : ValidationException
    {
        /// <summary>
        /// 唯一约束异常
        /// </summary>
        public UniqueConstraintException(Exception innerException = null)
            : this(null, null, innerException)
        {
        }

        /// <summary>
        /// 唯一约束异常
        /// </summary>
        public UniqueConstraintException(string propertyName, string description, object value)
            : base(propertyName, 1, String.Format(AppSettings.GetValue("提交的数据与以往记录的数据有重复{0}{1}"), description != null ? ": " : "!", description), null, value)
        {
        }

        #region 工厂

        private static bool IsRepeated(Exception exception)
        {
#if PgSQL
            return exception is NpgsqlException && exception.Message.IndexOf("23505:", StringComparison.Ordinal) == 0;
#endif
#if MsSQL
            return exception is SqlException sqlException && (sqlException.Number == 2601 || sqlException.Number == 2627);
#endif
#if MySQL
            return exception is MySqlException && exception.Message.IndexOf("Duplicate entry", StringComparison.Ordinal) == 0;
#endif
#if ORA
            return exception is OracleException && exception.Message.IndexOf("ORA-00001", StringComparison.Ordinal) == 0;
#endif
        }

        private static Exception Convert(Exception exception)
        {
#if PgSQL
            return exception is NpgsqlException ? new ValidationException(null, 0, exception.Message, exception) : exception;
#endif
#if MsSQL
            return exception is SqlException sqlException ? new ValidationException(null, sqlException.Number, exception.Message, exception) : exception;
#endif
#if MySQL
            return exception is MySqlException ? new ValidationException(null, 0, exception.Message, exception) : exception;
#endif
#if ORA
            return exception is OracleException ? new ValidationException(null, Int32.Parse(exception.Message.Remove(exception.Message.IndexOf(":")).Substring(4)), exception.Message, exception) : exception;
#endif
        }

        internal static Exception Refine<T>(Exception exception, Sheet sheet, T entity)
            where T : class
        {
            if (IsRepeated(exception))
                foreach (KeyValuePair<string, Property> kvp in sheet.GetProperties(entity.GetType()))
                    if (kvp.Value.Column.UniqueIndexes.Count > 0)
                        foreach (Schema.Index index in kvp.Value.Column.UniqueIndexes)
                            if (exception.Message.IndexOf(index.Name, StringComparison.OrdinalIgnoreCase) >= 0)
                                return new UniqueConstraintException(kvp.Value.PropertyInfo.Name, kvp.Value.Description, kvp.Value.GetValue(entity));

            return Convert(exception);
        }

        internal static Exception Refine(Exception exception, Sheet sheet, Type ownerType, IDictionary<string, object> propertyValues)
        {
            if (IsRepeated(exception))
                foreach (KeyValuePair<string, object> kvp in propertyValues)
                {
                    Column column = sheet.GetProperty(ownerType, kvp.Key).Column;
                    if (column.UniqueIndexes.Count > 0)
                        foreach (Schema.Index index in column.UniqueIndexes)
                            if (exception.Message.IndexOf(index.Name, StringComparison.OrdinalIgnoreCase) >= 0)
                                return new UniqueConstraintException(kvp.Key, !String.IsNullOrEmpty(column.Description) ? column.Description : kvp.Key, kvp.Value);
                }

            return Convert(exception);
        }

        #endregion
    }
}