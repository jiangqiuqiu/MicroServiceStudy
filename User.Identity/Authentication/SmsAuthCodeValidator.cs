using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using User.Identity.Services;

//添加了ID4后，User.Identity将会成为一个登陆中心
namespace User.Identity.Authentication
{
    /// <summary>
    /// 实现自定义授权模式：sms_auth_code 用手机号和验证码来进行验证
    /// </summary>
    public class SmsAuthCodeValidator : IExtensionGrantValidator
    {
        //通过定义接口，进行对外部依赖的注入
        private readonly IUserService _userService;
        private readonly IAuthCodeService _authCodeService;

        public SmsAuthCodeValidator(IUserService userService,IAuthCodeService authCodeService)
        {
            _userService = userService;
            _authCodeService = authCodeService;
        }


        public string GrantType => "sms_auth_code";//只读属性

        public async Task ValidateAsync(ExtensionGrantValidationContext context)
        {
            var phone = context.Request.Raw["phone"];
            var code = context.Request.Raw["auth_code"];
            var errorValidationResult = new GrantValidationResult(TokenRequestErrors.InvalidGrant);

            if (string.IsNullOrWhiteSpace(phone)||string.IsNullOrWhiteSpace(code))
            {
                context.Result=errorValidationResult;
                return;
            }

            //检查验证码
            if (!_authCodeService.Validate(phone,code))
            {
                context.Result = errorValidationResult;
                return;
            }

            //用户验证和注册
            var userId =await _userService.CheckOrCreate(phone);
            if (userId<=0)
            {
                context.Result = errorValidationResult;
                return;
            }

            context.Result = new GrantValidationResult(userId.ToString(), GrantType); 
        }
    }
}
