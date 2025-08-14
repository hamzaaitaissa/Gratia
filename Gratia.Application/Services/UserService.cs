using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Interfaces;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using System.Text;

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
            //step 2 check if user already exists
            var userExist = await _userRepository.GetByEmailAsync(registerUserDto.Email);
            if(userExist != null)
            {
                throw new KeyNotFoundException($"User with this email {registerUserDto.Email} already exists");
            }
            // Step 3: hash psd and save user
            var hashedPassword = new PasswordHasher<User>().HashPassword(user, registerUserDto.HashedPassword);
            user.HashedPassword = hashedPassword;
            var savedUser = await _userRepository.AddAsync(user);

            // Step 3: Convert saved User to ReadUserDto
            var readUserDto = new ReadUserDto
            {
                Id = savedUser.Id,
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
                Id = user.Id,
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
            if (user == null)
            {
                throw new KeyNotFoundException($"User with this email {updateUserDto.Email} does not exist.");
            }
            var hashedPassword = new PasswordHasher<User>().HashPassword(user, updateUserDto.HashedPassword);

            user.Update
                (
                    updateUserDto.FullName,
                    updateUserDto.Email,
                    hashedPassword,
                    updateUserDto.JobTitle,
                    updateUserDto.Role
                );
            var userUpdated = await _userRepository.UpdateAsync(user);
            var readUserDto = new ReadUserDto
            {
                Id = user.Id,
                FullName = updateUserDto.FullName,
                Email = updateUserDto.Email,
                JobTitle = updateUserDto.JobTitle,
                Role = updateUserDto.Role,
                NumberOfPointsAcquired = user.NumberOfPointsAcquired,
                NumberOfPointsAvailable = user.NumberOfPointsAvailable,
                CompanyId = user.CompanyId
            };
            return readUserDto;
        }

        public async Task DeleteUserAsync(Guid id)
        {
            await _userRepository.DeleteAsync(id);
        }

        public async Task<ReadUserDto> GetUserById(Guid Id)
        {
            var user = await _userRepository.GetByIdAsync(Id);
            var userReturned = new ReadUserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                JobTitle = user.JobTitle,
                Role = user.Role,
                NumberOfPointsAcquired = user.NumberOfPointsAcquired,
                NumberOfPointsAvailable = user.NumberOfPointsAvailable,
                CompanyId = user.CompanyId
            };
            return userReturned;
        }

        public async Task<bool> LoginUser(LoginDto loginDto)
        {
            var userExistWithEmail = await _userRepository.GetByEmailAsync(loginDto.Email);
            if (userExistWithEmail == null)
            {
                return false;
            }
            if(new PasswordHasher<User>().VerifyHashedPassword(userExistWithEmail,userExistWithEmail.HashedPassword, loginDto.HashedPassword) == PasswordVerificationResult.Failed)
            {
                return false;
            }
            return true;

        }

        public async Task<ReadUserDto> GetUserByEmail(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null)
            {
                throw new KeyNotFoundException("User with this Email not found");
            }
            var userReturned = new ReadUserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                JobTitle = user.JobTitle,
                Role = user.Role,
                NumberOfPointsAcquired = user.NumberOfPointsAcquired,
                NumberOfPointsAvailable = user.NumberOfPointsAvailable,
                CompanyId = user.CompanyId
            };
            return userReturned;

        }
    }
}
