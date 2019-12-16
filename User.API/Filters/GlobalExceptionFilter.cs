using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace User.API.Filters
{
    /// <summary>
    /// 异常在全局的异常Filter中处理
    /// </summary>
    public class GlobalExceptionFilter : IExceptionFilter
    {
        private readonly IHostingEnvironment _env;
        private ILogger<GlobalExceptionFilter> _logger;

        public GlobalExceptionFilter(IHostingEnvironment env, ILogger<GlobalExceptionFilter> logger)
        {
            _env = env;
            _logger = logger;
        }

        public void OnException(ExceptionContext context)
        {
            var json = new JsonErrorResponse();

            if (context.Exception.GetType()==typeof(UserOperationException))
            {
               json.Message = context.Exception.Message;
               context.Result = new BadRequestObjectResult(json);
            }
            else
            {
                json.Message = "发生了未知的内部错误";

                if (_env.IsDevelopment())
                {
                    json.DevelopMessage = context.Exception.StackTrace;
                }
                context.Result = new InternalServerErrorObjectResult(json);
            }

            _logger.LogError(context.Exception,context.Exception.Message);
            //context.ExceptionHandled 代表异常是否处理，不是true时，异常记录到日志文件中后，
            //系统对异常的处理并未结束，如果这时系统使用了开发人员异常页面（The developer exception page）, 
            //系统在页面上详细展示系统异常信息。
            //若果context.ExceptionHandled为true，异常通过Log4net把日志记录到本地文件后，系统对异常的处理就结束了。
            context.ExceptionHandled = true;
        }

        public class InternalServerErrorObjectResult:ObjectResult
        {
            public InternalServerErrorObjectResult(object error):base(error)
            {
                StatusCode = StatusCodes.Status500InternalServerError;//捕获未预测到的异常
            }
        }
    }
}
