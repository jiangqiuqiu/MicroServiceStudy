using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using DnsClient;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Resilience;
using User.Identity.Authentication;
using User.Identity.DTOs;
using User.Identity.Infrastructure;
using User.Identity.Services;

namespace User.Identity
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddIdentityServer()
                .AddExtensionGrantValidator<SmsAuthCodeValidator>()//把自定义的验证加进来
                .AddDeveloperSigningCredential()
                .AddInMemoryClients(Config.GetClients())
                .AddInMemoryIdentityResources(Config.GetIdentityResources())
                .AddInMemoryApiResources(Config.GetApiResources());

            services.AddOptions();
            services.Configure<ServiceDiscoveryOptions>(Configuration.GetSection("ServiceDiscovery"));//注册配置节
            services.AddSingleton<IDnsQuery>(p =>
            {
                var serviceConfiguration = p.GetRequiredService<IOptions<ServiceDiscoveryOptions>>().Value;

                return new LookupClient(serviceConfiguration.Consul.DnsEndpoint.ToIPEndPoint());//这里的地址是从配置文件中来的
            });

            //注册全局单例的ResilienceClientFactory
            services.AddSingleton(typeof(ResilienceClientFactory),sp => {
                var logger = sp.GetRequiredService<ILogger<ResilienceHttpClient>>();
                var httpcontextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
                int retryCount = 5;
                int exceptionCountAllowedBeforBreaking = 5;
                var factory = new ResilienceClientFactory(logger,httpcontextAccessor,retryCount,exceptionCountAllowedBeforBreaking);
                return factory;
            });


            //services.AddSingleton(new HttpClient());//通过这种方式可以获取到单例的实例
            //获取全局单例HttpClient
            services.AddSingleton<IHttpClient>(sp=> {
                return sp.GetRequiredService<ResilienceClientFactory>().GetResilienceHttpClient();
            });

            services.AddScoped<IUserService, UserService>()
                    .AddScoped<IAuthCodeService, TestAuthCodeService>();

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }


            app.UseIdentityServer();
            app.UseMvc();
        }
    }
}
