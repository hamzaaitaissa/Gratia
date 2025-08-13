using Gratia.Application.DTOs.UserDTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.Interfaces
{
    public interface IUserService
    {
        Task<ReadUserDto> AddUserAsync(RegisterUserDto registerUserDto);
        Task<ReadUserDto> GetUserById(Guid Id);
        Task<ReadUserDto> UpdateUserAsync(UpdateUserDto updateUserDto);
        Task<IEnumerable<ReadUserDto>> GetAllUsersAsync();
        Task DeleteUserAsync(Guid id);
        Task<bool> LoginUser(LoginDto loginDto);
        Task<ReadUserDto> GetUserByEmail(string email);
    }
}
