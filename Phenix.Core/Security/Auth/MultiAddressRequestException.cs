using System;
using System.Security.Authentication;

namespace Phenix.Core.Security.Auth
{
    /// <summary>
    /// 多终端请求禁止异常
    /// </summary>
    [Serializable]
    public class MultiAddressRequestException : AuthenticationException
    {
        /// <summary>
        /// 多终端请求禁止异常
        /// </summary>
        public MultiAddressRequestException(Exception innerException = null)
            : this(AppSettings.GetValue("禁止同一用户在多处终端上同时登录系统!"), innerException)
        {
        }

        /// <summary>
        /// 多终端请求禁止异常
        /// </summary>
        public MultiAddressRequestException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }
}