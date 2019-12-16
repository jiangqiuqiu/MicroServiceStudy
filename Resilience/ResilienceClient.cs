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
    public class ResilienceClient : IHttpClient
    {
        private readonly HttpClient _httpClient;
        //根据url origin 去创建policy
        private readonly Func<string, IEnumerable<Policy>> _policyCreator;
        //把policy打包成组合policy wrapper，进行本地缓存
        private readonly ConcurrentDictionary<string, PolicyWrap> _policyWrappers;
        
        private ILogger<ResilienceClient> _logger;
        private IHttpContextAccessor _httpContextAccessor;


        public ResilienceClient(Func<string,IEnumerable<Policy>> policyCreator, ILogger<ResilienceClient> logger, IHttpContextAccessor httpContextAccessor)
        {
            _httpClient = new HttpClient();
            _policyCreator = policyCreator;
            _policyWrappers = new ConcurrentDictionary<string,PolicyWrap>();
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }
        #region 接口实现

        public async Task<HttpResponseMessage> PostAsync<T>(string url, T item, string authorizationToken, string requestId = null, string authorizationMethod = "Bearer")
        {
            return await DoPostAsync(HttpMethod.Post,url,item,authorizationToken,requestId,authorizationMethod);
        }
        #endregion


        #region 私有方法
        private Task<HttpResponseMessage> DoPostAsync<T>(HttpMethod method, string url, T item, string authorizationToken, string requestId = null, string authorizationMethod = "Bearer")
        {
            if (method != HttpMethod.Post && method != HttpMethod.Put)
            {
                throw new ArgumentException("Value must be either post or put", nameof(method));
            }

            var origin = GetOriginFromUri(url);
            return HttpInvoker(origin, async () =>
            {
                var requestMessage = new HttpRequestMessage(method, url);
                SetAuthorizationHeader(requestMessage);
                requestMessage.Content = new StringContent(JsonConvert.SerializeObject(item), System.Text.Encoding.UTF8, "application/json");

                if (authorizationToken != null)
                {
                    requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(authorizationMethod, authorizationToken);
                }
                if (requestId != null)
                {
                    requestMessage.Headers.Add("x-requestid", requestId);
                }
                var response = await _httpClient.SendAsync(requestMessage);

                if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                {
                    throw new HttpRequestException();
                }

                return response;
            });
        }
        private async Task<T> HttpInvoker<T>(string origin,Func<Task<T>> action)
        {
            var normalizeOrigin = NormalizeOrigin(origin);
            if (!_policyWrappers.TryGetValue(normalizeOrigin,out PolicyWrap policyWrap))
            {
                policyWrap = Policy.WrapAsync(_policyCreator(normalizeOrigin).ToArray());
                _policyWrappers.TryAdd(normalizeOrigin,policyWrap);
            }

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
            var authorizationHeader = _httpContextAccessor.HttpContext.Request.Headers["Authorization"];
            if (!string.IsNullOrEmpty(authorizationHeader))
            {
                requestMessage.Headers.Add("Authorization",new List<string>() { authorizationHeader});
            }
        }


        #endregion
    }
}
