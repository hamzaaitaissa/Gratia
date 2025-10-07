using Gratia.API.Controllers;
using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Interfaces;
using Gratia.Domain.Enum;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace GratiaTests
{
    public class UserControllerTests
    {
        private readonly Mock<IUserService> _mockUserService;
        private readonly Mock<ILogger<UserController>> _logger;
        public UserControllerTests()
        {
            _mockUserService = new Mock<IUserService>();
            _logger = new Mock<ILogger<UserController>>();

        }

        [Fact]
        public async Task Create_ReturnIAction_WithUser()
        {
            //arrange
            var registerUserDto = new RegisterUserDto
            {
                CompanyId = Guid.NewGuid(),
                Email = "test@test.com",
                FullName = "Test User",
                JobTitle = "Developer",
                HashedPassword = "Password123!",
                NumberOfPointsAcquired = 25,
                NumberOfPointsAvailable = 0,
                Role = UserRole.Employee,
            };
            var readUserDto = new ReadUserDto
            {
                FullName = registerUserDto.FullName,
                Email = registerUserDto.Email,
            };

            //setuping the mock of userService
            _mockUserService.Setup(s => s.AddUserAsync(It.IsAny<RegisterUserDto>())).
                ReturnsAsync(readUserDto);
            var _userController = new UserController(_mockUserService.Object, _logger.Object);
            //act
            var result = await _userController.Create(registerUserDto);

            //assert
            Assert.NotNull(result);
            Assert.IsType<CreatedAtActionResult>(result);
        }
    }
}
