using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using User.API.Data;
using User.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.JsonPatch;


namespace User.API.Controllers
{
    [Route("api/users")]
    [ApiController]
    public class UserController : BaseController
    {
        private UserContext _userContext;
        private ILogger<UserController> _logger;
        public UserController(UserContext userContext, ILogger<UserController> logger)
        {
            _userContext = userContext;
            _logger = logger;
        }

        
        /// <summary>
        /// 获取当前用户的信息
        /// </summary>
        /// <returns></returns>
        [Route("")]
        [HttpGet]
        public async Task<JsonResult> Get()
        {
            //AsNoTracking干什么的呢？
            //无跟踪查询而已，也就是说查询出来的对象不能直接做修改。
            //所以，我们在做数据集合查询显示，而又不需要对集合修改并更新到数据库的时候，一定不要忘记加上AsNoTracking
            var user = await _userContext.Users
                .AsNoTracking()
                .Include(u=>u.Properties)//导航
                .SingleOrDefaultAsync(u => u.Id == UserIdentity.UserId);
            if (user == null)
            {
                //return NotFound();
                throw new UserOperationException($"错误的用户上下文Id {UserIdentity.UserId}");
            }
            return Json(user);
        }


        [Route("")]
        [HttpPatch]
        public async Task<IActionResult> Patch([FromBody]JsonPatchDocument<AppUser> patch)
        {
            //return new string[] { "value1", "value2" };
            AppUser appUser = await _userContext.Users.                
                SingleOrDefaultAsync(u => u.Id==UserIdentity.UserId);

            patch.ApplyTo(appUser);

            //Apply后，会当做是新增的，但其实并非如此，因此下面再Save的时候，会产生冲突，因此需要先把AppLyTo的这些Properties Detach掉！
            //最终Save的时候，只认下面两个foreach里的添加和删除操作
            //解决问题
            // MySql.Data.MySqlClient.MySqlException: Duplicate entry 'fin_stage-2-A+轮' for key 'PRIMARY' 
            //

            foreach (var property in appUser?.Properties)
            {
                _userContext.Entry(property).State = EntityState.Detached;
            }


            //查询到原有的UserProperties
            var originalProperties = await _userContext.UserProperties.AsNoTracking().Where(u => u.AppUserId == UserIdentity.UserId).ToListAsync();

            //原有的和现在applyto后的Union再去除重复的
            var allProperties = originalProperties.Union(appUser.Properties).Distinct();

            //在原有的里面但是不在现在的里面的UserProperties就需要去除了
            var removeProperties = originalProperties.Except(appUser.Properties);

            //新增的UserProperties是指在所有的里面，但是不在原有的UserProperties中的
            var newProperties = allProperties.Except(originalProperties);

            foreach (var property in removeProperties)
            {
                //_userContext.Entry(property).State = EntityState.Deleted;
                _userContext.Remove(property);
            }

            foreach (var property in newProperties)
            {
                //_userContext.Entry(property).State = EntityState.Added;
                _userContext.Add(property);
            }

            _userContext.Users.Update(appUser);
            await _userContext.SaveChangesAsync();
            return Json(appUser);
        }


        /// <summary>
        /// 检查或创建用户（当用户手机号不存在的时候创建用户）
        /// </summary>
        /// <param name="phone"></param>
        /// <returns></returns>
        [Route("check-or-create")]
        [HttpPost]
        public async Task<IActionResult> CheckOrCreate(string phone)
        {
           var user = _userContext.Users.SingleOrDefault(u=>u.Phone==phone);

            //TODO:需要做手机号码的格式验证

            if(user==null)//如果不存在该手机号的用户则添加
            {
                user = new AppUser { Phone = phone };
                _userContext.Users.Add(user);
                await _userContext.SaveChangesAsync();
            }
           

            return Ok(user.Id);
        }
    }
}
