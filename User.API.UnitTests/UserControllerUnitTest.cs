using System;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using FluentAssertions;
using Microsoft.AspNetCore.JsonPatch;
using User.API.Controllers;
using User.API.Data;
using System.Collections.Generic;
using System.Linq;

namespace User.API.UnitTests
{
    public class UserControllerUnitTest
    {
        private Data.UserContext GetUserContext()
        {
            var options = new DbContextOptionsBuilder<Data.UserContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())//ʹ���ڴ����ݿ�
                .Options;

            var userContext = new Data.UserContext(options);
            userContext.Users.Add(new Models.AppUser
            {
                Id = 2,
                Name="jiang"
            }) ;
            userContext.SaveChanges();

            return userContext;
        }

        private (UserController controller,UserContext userContext) GetUserController()
        {
            var context = GetUserContext();
            var loggerMoq = new Mock<ILogger<Controllers.UserController>>();
            var logger = loggerMoq.Object;

            //ͨ��Ԫ�鷵�ض��ֵ��������ѧϰѧϰ
            return (controller:new UserController(context, logger),userContext:context);
        }

        [Fact]
        public async Task Get_ReturnRightUser_WithExpectedParameter()
        {

            //var controller = GetUserController();
            //var  response = await controller.Get();
            (UserController controller, UserContext userContext) = GetUserController();
            var response =await controller.Get();

            //Assert.IsType<JsonResult>(response);

            //����FluentAssetions
            var result = response.Should().BeOfType<JsonResult>().Subject;

            var appUser = result.Value.Should().BeAssignableTo<Models.AppUser>().Subject;
            appUser.Id.Should().Be(2);
            appUser.Name.Should().Be("jiang");
        }

        [Fact]
        public async Task Patch_ReturnNewName_WithExpectedNewNameParameter()
        {
            (UserController controller, UserContext userContext) = GetUserController();//����Ԫ��

            var document = new JsonPatchDocument<Models.AppUser>();
            document.Replace(u=>u.Name,"yu");

            var response=await controller.Patch(document);

            var result = response.Should().BeOfType<JsonResult>().Subject;

            //assert  response
            var appUser = result.Value.Should().BeAssignableTo<Models.AppUser>().Subject;
            appUser.Name.Should().Be("yu");

            //assert name value in context
            var userModel =await userContext.Users.SingleOrDefaultAsync(u=>u.Id==2);
            userModel.Should().NotBeNull();
            userModel.Name.Should().Be("yu");
        }

        [Fact]
        public async Task Patch_ReturnProperties_WithAddNewProperties()
        {
            (UserController controller, UserContext userContext) = GetUserController();//����Ԫ��

            var document = new JsonPatchDocument<Models.AppUser>();
            document.Replace(u => u.Properties,new List<Models.UserProperty> { 
                new Models.UserProperty{Key="fin_industry",Value="������",Text="������"}
            });

            var response = await controller.Patch(document);

            var result = response.Should().BeOfType<JsonResult>().Subject;

            //assert  response
            var appUser = result.Value.Should().BeAssignableTo<Models.AppUser>().Subject;
            appUser.Properties.Count.Should().Be(1);
            appUser.Properties.First().Value.Should().Be("������");
            appUser.Properties.First().Key.Should().Be("fin_industry");

            //assert name value in context
            var userModel = await userContext.Users.SingleOrDefaultAsync(u => u.Id == 2);
            userModel.Properties.Count.Should().Be(1);
            userModel.Properties.First().Value.Should().Be("������");
            userModel.Properties.First().Key.Should().Be("fin_industry");
        }

        [Fact]
        public async Task Patch_ReturnProperties_WithRemoveNewProperties()
        {
            (UserController controller, UserContext userContext) = GetUserController();//����Ԫ��

            var document = new JsonPatchDocument<Models.AppUser>();
            document.Replace(u => u.Properties, new List<Models.UserProperty> {
                //new Models.UserProperty{Key="fin_industry",Value="������",Text="������"}//ɾ����Properties
            });

            var response = await controller.Patch(document);

            var result = response.Should().BeOfType<JsonResult>().Subject;

            //assert  response
            var appUser = result.Value.Should().BeAssignableTo<Models.AppUser>().Subject;
            appUser.Properties.Should().BeEmpty();
           

            //assert name value in context
            var userModel = await userContext.Users.SingleOrDefaultAsync(u => u.Id == 2);
            userModel.Properties.Should().BeEmpty();
        }
    }
}
