using Gratia.Application.DTOs.UserDTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.Interfaces
{
    internal interface IUserService
    {
        Task<RegisterUserDto> AddUserAsync(RegisterUserDto registerUserDto);
        Task<UpdateUserDto> UpdateUserAsync(UpdateUserDto updateUserDto);
        Task<IEnumerable<ReadUserDto>> GetAllUsersAsync();
    }
}
