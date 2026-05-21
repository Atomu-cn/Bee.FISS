using System;
using System.Security.Authentication;

namespace Phenix.Core.Security.Auth
{
    /// <summary>
    /// 用户找不到异常
    /// </summary>
    [Serializable]
    public class UserNotFoundException : AuthenticationException
    {
        /// <summary>
        /// 用户找不到异常
        /// </summary>
        public UserNotFoundException(Exception innerException = null)
            : this(AppSettings.GetValue("您不是注册用户!"), innerException)
        {
        }

        /// <summary>
        /// 用户找不到异常
        /// </summary>
        public UserNotFoundException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }
}
