using DnsClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Resilience;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using User.Identity.DTOs;

namespace User.Identity.Services
{
    public class UserService : IUserService
    {
        private IHttpClient _httpClient;
        //private readonly string _userServiceUrl = "http://localhost:5000";
        private string _userServiceUrl;
        private ILogger<UserService> _logger;

        public UserService(IHttpClient httpClient, IOptions<ServiceDiscoveryOptions> serviceDiscoveryOptions,IDnsQuery dnsQuery,ILogger<UserService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            //服务发现通过IDnsQuery完成
            //通过consul服务发现获取UserAPI地址
            var address = dnsQuery.ResolveService("service.consul", serviceDiscoveryOptions.Value.UserServiceName);
            var addressList = address.First().AddressList;
            var host= addressList.Any()?addressList.First().ToString():address.First().HostName;
            var port = address.First().Port;

            _userServiceUrl = $"http://{host}:{port}";
        }
        public async Task<int> CheckOrCreate(string phone)
        {
            var form = new Dictionary<string, string> { { "phone", phone } };

             
            try
            {
                //var content = new FormUrlEncodedContent(form);
                HttpResponseMessage response = await _httpClient.PostAsync(_userServiceUrl + "/api/users/check-or-create", form);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var userId = await response.Content.ReadAsStringAsync();
                    int.TryParse(userId, out int intUserId);

                    return intUserId;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("CheckOrCreate重试后失败"+ex.Message+ex.StackTrace);
                throw ex;
            }


            

            return 0;
        }
    }
}
