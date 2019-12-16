using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Polly;
using Polly.Wrap;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System.Linq;
using Newtonsoft.Json;

namespace Resilience
{
    public class ResilienceHttpClient : IHttpClient
    {
        private readonly HttpClient _httpClient;//提供用于发送 HTTP 请求并从 URI 标识的资源接收 HTTP 响应的基类
        //根据url origin 去创建policy
        private readonly Func<string, IEnumerable<Policy>> _policyCreator;
        //把policy打包成组合policy wrapper，进行本地缓存
        private readonly ConcurrentDictionary<string, PolicyWrap> _policyWrappers;
        
        private ILogger<ResilienceHttpClient> _logger;
        private IHttpContextAccessor _httpContextAccessor;


        public ResilienceHttpClient(Func<string,IEnumerable<Policy>> policyCreator, ILogger<ResilienceHttpClient> logger, IHttpContextAccessor httpContextAccessor)
        {
            _httpClient = new HttpClient();
            _policyCreator = policyCreator;
            _policyWrappers = new ConcurrentDictionary<string,PolicyWrap>();
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }
        #region 接口实现

        public async Task<HttpResponseMessage> PostAsync<T>(string url, T item, string authorizationToken=null, string requestId = null, string authorizationMethod = "Bearer")
        {        
            //通过委托进行传值
            Func<HttpRequestMessage> func = () =>GetHttpRequestMessage<T>(HttpMethod.Post, url, item);
            return await DoPostPutAsync(HttpMethod.Post,url, func, authorizationToken,requestId,authorizationMethod);
        }

        public async Task<HttpResponseMessage> PostAsync(string url, Dictionary<string, string> form, string authorizationToken, string requestId = null, string authorizationMethod = "Bearer")
        {
            Func<HttpRequestMessage> func = ()=> GetHttpRequestMessage(HttpMethod.Post, url, form);
            return await DoPostPutAsync(HttpMethod.Post, url, func, requestId, authorizationMethod);
        }
        #endregion


        #region 私有方法
       
        private Task<HttpResponseMessage> DoPostPutAsync(HttpMethod method, string url,Func<HttpRequestMessage> requesMessageFunc, string authorizationToken=null, string requestId = null, string authorizationMethod = "Bearer")
        {
            if (method != HttpMethod.Post && method != HttpMethod.Put)
            {
                throw new ArgumentException("Value must be either post or put", nameof(method));
            }

            var origin = GetOriginFromUri(url);
            return HttpInvoker(origin, async () =>
            {
                HttpRequestMessage requestMessage = requesMessageFunc();
               
                SetAuthorizationHeader(requestMessage);
                
                if (authorizationToken != null)
                {
                    requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(authorizationMethod, authorizationToken);//表示 Authorization、ProxyAuthorization、WWW-Authneticate 和 Proxy-Authenticate 标头值中的验证信息。
                }
                if (requestId != null)
                {
                    requestMessage.Headers.Add("x-requestid", requestId);
                }
                var response = await _httpClient.SendAsync(requestMessage);//以异步操作发送 HTTP 请求获取响应

                if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                {
                    throw new HttpRequestException();
                }

                return response;
            });
        }

        private HttpRequestMessage GetHttpRequestMessage<T>(HttpMethod method,string url,T item)
        {
            var requestMessage = new HttpRequestMessage(method, url);
            //设置 HTTP请求消息的内容
            //StringContent -》ByteArrayContent-》HttpContent
            requestMessage.Content= new StringContent(JsonConvert.SerializeObject(item), System.Text.Encoding.UTF8, "application/json"); ;
            return requestMessage;
        }

        private HttpRequestMessage GetHttpRequestMessage(HttpMethod method, string url,Dictionary<string,string> form)
        {
            var requestMessage = new HttpRequestMessage(method, url);
            requestMessage.Content = new FormUrlEncodedContent(form);
            return requestMessage;
        }

        /// <summary>
        /// 弹性策略
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="origin"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        private async Task<T> HttpInvoker<T>(string origin,Func<Task<T>> action)
        {
            var normalizeOrigin = NormalizeOrigin(origin);
            if (!_policyWrappers.TryGetValue(normalizeOrigin,out PolicyWrap policyWrap))
            {
                policyWrap = Policy.WrapAsync(_policyCreator(normalizeOrigin).ToArray());
                _policyWrappers.TryAdd(normalizeOrigin,policyWrap);
            }

            // 执行应用全部的操作
            // 包装器中定义的策略
            return await policyWrap.ExecuteAsync(action,new Context(normalizeOrigin));
        }

        private static string NormalizeOrigin(string origin)
        {
            return origin?.Trim().ToLower();
        }

        private string GetOriginFromUri(string uri)
        {
            var url = new Uri(uri);
            var origin = $"{url.Scheme}://{url.DnsSafeHost}:{url.Port}";
            return origin;
        }

        private void SetAuthorizationHeader(HttpRequestMessage requestMessage)
        {
            //通过IHttpContextAccessor获取HttpContext（上下文），进而获取头部信息
            var authorizationHeader = _httpContextAccessor.HttpContext.Request.Headers["Authorization"];
            if (!string.IsNullOrEmpty(authorizationHeader))
            {
                //添加指定的标头及其值到 HttpHeaders 集合中
                requestMessage.Headers.Add("Authorization",new List<string>() { authorizationHeader});
            }
        }

        


        #endregion
    }
}
