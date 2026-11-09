using Gratia.API.Controllers;
using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Features.Users.Commands;
using Gratia.Application.Interfaces;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GratiaTests
{
    // Controllers only build messages and send them; this checks the one request-shape rule UserController has
    public class UserControllerTests
    {
        private readonly Mock<ISender> _mockSender = new();

        [Fact]
        public async Task Update_with_different_ids_in_route_and_body_returns_400_without_sending()
        {
            var userController = new UserController(_mockSender.Object, Mock.Of<ICurrentUserService>());

            var result = await userController.Update(Guid.NewGuid(), new UpdateUserDto { Id = Guid.NewGuid() }, CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(400, problem.StatusCode);
            _mockSender.Verify(s => s.Send(It.IsAny<UpdateUserCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
