using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace User.API.Data
{
    public class UserContextSeed
    {
        private ILogger<UserContextSeed> _logger;
        public UserContextSeed(ILogger<UserContextSeed> logger)
        {
            _logger = logger;
        }
        public static async Task SeedAsync(IApplicationBuilder applicationBuilder,ILoggerFactory loggerFactory,int? retry=0)
        {
            var retryForAvaliblity = retry.Value;

            try
            {
                using (var scope=applicationBuilder.ApplicationServices.CreateScope())
                {
                    var userContext = (UserContext)scope.ServiceProvider.GetService(typeof(UserContext));
                    var logger = (ILogger<UserContextSeed>)scope.ServiceProvider.GetService(typeof(ILogger<UserContextSeed>));

                    logger.LogDebug("Begin UserContextSeed SeedAsync");
                    userContext.Database.Migrate();

                    if (!userContext.Users.Any())
                    {
                        userContext.Users.Add(new Models.AppUser { Name = "jiang" });
                        userContext.SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                if (retryForAvaliblity<10)
                {
                    retryForAvaliblity++;

                    var logger = loggerFactory.CreateLogger(typeof(UserContextSeed));
                    logger.LogError(ex.Message);

                    await SeedAsync(applicationBuilder,loggerFactory,retryForAvaliblity);
                }
            }
        }
    }
}
