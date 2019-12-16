using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Polly;
using Resilience;

namespace User.Identity.Infrastructure
{
    public class ResilienceClientFactory
    {               
        private readonly ILogger<ResilienceHttpClient> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private int _retryCount;//重试次数
        private int _exceptionCountAllowedBeforBreaking;//熔断之前允许的异常次数

        public ResilienceClientFactory(ILogger<ResilienceHttpClient> logger, 
            IHttpContextAccessor httpContextAccessor,
            int retryCount,
            int exceptionCountAllowedBeforBreaking)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _retryCount = retryCount;
            _exceptionCountAllowedBeforBreaking = exceptionCountAllowedBeforBreaking;
        }

        public ResilienceHttpClient GetResilienceHttpClient() =>
            new ResilienceHttpClient(origin=> CreatePolicy(origin), _logger, _httpContextAccessor);


        private Policy[] CreatePolicy(string origin)
        {
            //这里正式开始使用Polly
            return new Policy[] {
                Policy.Handle<HttpRequestException>() // 定义条件
                 // 定义处理方式
                .WaitAndRetryAsync(
                    //重试次数
                    _retryCount,
                    retryAttempt=>TimeSpan.FromSeconds(Math.Pow(2,retryAttempt)),
                    (exception,timeSpan,retryCount,context)=>
                    {
                        var msg=$"Retry {retryCount} implemented with Polly's RetryPolicy"+
                            $"of {context.PolicyKey}"+
                            $"at {context.ExecutionKey},"+
                            $"due to:{exception}";
                        _logger.LogWarning(msg);
                        _logger.LogDebug(msg);
                    }),
                Policy.Handle<HttpRequestException>()
                .CircuitBreakerAsync(
                    _exceptionCountAllowedBeforBreaking,
                    TimeSpan.FromMinutes(1),
                    (exception, duration) =>
                    {
                        _logger.LogTrace("熔断器打开");
                    },
                    ()=>
                    {
                        _logger.LogTrace("熔断器关闭");
                    })
                    
            };
        }
    }
}
