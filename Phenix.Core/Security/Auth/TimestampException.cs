using System;
using System.Security.Authentication;

namespace Phenix.Core.Security.Auth
{
    /// <summary>
    /// 时间戳异常
    /// </summary>
    [Serializable]
    public class TimestampException : AuthenticationException
    {
        /// <summary>
        /// 时间戳异常
        /// </summary>
        public TimestampException(Exception innerException = null)
            : this(String.Format(AppSettings.GetValue("时间戳异常, 请注意盗链风险!"), innerException))
        {
        }

        /// <summary>
        /// 多会话请求禁止异常
        /// </summary>
        public TimestampException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }
}