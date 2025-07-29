using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Interfaces;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;

        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        async Task<ReadUserDto> IUserService.AddUserAsync(RegisterUserDto registerUserDto)
        {
            //Input DTO comes in → Convert to Entity → Work with Entity → Convert back to Output DTO
            // Step 1: Create User entity from RegisterUserDto
            var user = new User
            (
                registerUserDto.FullName,
                registerUserDto.Email,
                registerUserDto.HashedPassword,
                registerUserDto.JobTitle,
                registerUserDto.Role,
                registerUserDto.NumberOfPointsAcquired,
                registerUserDto.NumberOfPointsAvailable,
                registerUserDto.CompanyId
            );

            // Step 2: Save to database (returns User entity)
            var savedUser = await _userRepository.AddAsync(user);

            // Step 3: Convert saved User to ReadUserDto
            var readUserDto = new ReadUserDto
            {
                FullName = savedUser.FullName,
                Email = savedUser.Email,
                JobTitle = savedUser.JobTitle,
                Role = savedUser.Role,
                NumberOfPointsAcquired = savedUser.NumberOfPointsAcquired,
                NumberOfPointsAvailable = savedUser.NumberOfPointsAvailable,
                CompanyId = savedUser.CompanyId
            };

            return readUserDto;
        }

        async Task<IEnumerable<ReadUserDto>> IUserService.GetAllUsersAsync()
        {
            var users = await _userRepository.GetAllAsync();

            return users.Select(user => new ReadUserDto
            {
                FullName = user.FullName,
                Email = user.Email,
                JobTitle = user.JobTitle,
                Role = user.Role,
                NumberOfPointsAcquired = user.NumberOfPointsAcquired,
                NumberOfPointsAvailable = user.NumberOfPointsAvailable,
                CompanyId = user.CompanyId
            });
        }

        async Task<ReadUserDto> IUserService.UpdateUserAsync(UpdateUserDto updateUserDto)
        {
            var user = await _userRepository.GetByIdAsync(updateUserDto.Id);
            if(user == null)
            {
                throw new KeyNotFoundException($"User with this email {updateUserDto.Email} does not exist.");
            }
            user.Update
                (
                    updateUserDto.FullName,
                    updateUserDto.Email,
                    updateUserDto.HashedPassword,
                    updateUserDto.JobTitle,
                    updateUserDto.Role
                );
            var userUpdated = await _userRepository.UpdateAsync( user );
            var readUserDto = new ReadUserDto
            {
                FullName = updateUserDto.FullName,
                Email = updateUserDto.Email,
                JobTitle = updateUserDto.JobTitle,
                Role = updateUserDto.Role,
            };
            return readUserDto;
        }

        public async Task DeleteUserAsync(Guid id)
        {
             await _userRepository.DeleteAsync(id);
        }
    }
}
