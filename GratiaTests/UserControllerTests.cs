using Gratia.API.Controllers;
using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Features.Users.Commands;
using Gratia.Domain.Enum;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GratiaTests
{
    public class UserControllerTests
    {
        private readonly Mock<ISender> _mockSender = new();

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

            // the controller only sends a command; the handler is tested separately
            _mockSender
                .Setup(s => s.Send(It.Is<CreateUserCommand>(c => c.User == registerUserDto), It.IsAny<CancellationToken>()))
                .ReturnsAsync(readUserDto);

            var userController = new UserController(_mockSender.Object);

            //act
            var result = await userController.Create(registerUserDto, CancellationToken.None);

            //assert
            var created = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Same(readUserDto, created.Value);
            _mockSender.Verify(s => s.Send(It.IsAny<CreateUserCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
