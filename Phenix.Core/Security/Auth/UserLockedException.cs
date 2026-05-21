using System;
using System.Security.Authentication;

namespace Phenix.Core.Security.Auth
{
    /// <summary>
    /// 用户账号锁定异常
    /// </summary>
    [Serializable]
    public class UserLockedException : AuthenticationException
    {
        /// <summary>
        /// 用户账号锁定异常
        /// </summary>
        public UserLockedException(int lockedMinutes)
            : base(String.Format(AppSettings.GetValue("您的账号被锁定, {0}分钟之后请再尝试登录!"), lockedMinutes))
        {
        }
    }
}