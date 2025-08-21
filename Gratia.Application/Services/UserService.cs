using Gratia.Application.DTOs.Token;
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
        private readonly ITokenService _tokenService;

        public UserService(IUserRepository userRepository, ITokenService tokenService)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
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
            var readUserDto = MapToReadDto(savedUser);

            return readUserDto;
        }

        async Task<IEnumerable<ReadUserDto>> IUserService.GetAllUsersAsync()
        {
            var users = await _userRepository.GetAllAsync();

            return users.Select(user => MapToReadDto(user));
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
            var readUserDto = MapToReadDto(user);
            return readUserDto;
        }

        public async Task DeleteUserAsync(Guid id)
        {
            await _userRepository.DeleteAsync(id);
        }

        public async Task<ReadUserDto> GetUserById(Guid Id)
        {
            var user = await _userRepository.GetByIdAsync(Id);
            var userReturned = MapToReadDto(user);
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
            var userReturned = MapToReadDto(user);
            return userReturned;

        }

        public async Task<TokenResponseDto> RefreshTokenAsync(string refreshToken)
        {
            var user = await _userRepository.GetUserByRefreshTokenAsync(refreshToken);
            if(user == null || user.RefreshToken != refreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            {
                return null;
            }
            var userDto = MapToReadDto(user);
            var tokenResponse = _tokenService.GenerateTokens(userDto);
            user.RefreshToken = tokenResponse.RefreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

            await _userRepository.UpdateAsync(user);

            return tokenResponse;
        }

        public async Task RevokeRefreshTokenAsync(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null) return;

            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;

            await _userRepository.UpdateAsync(user);
        }

        public async Task SaveRefreshTokenAsync(string email, string refreshToken)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null) throw new InvalidOperationException("User not found");
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            await _userRepository.UpdateAsync(user);
        }

        private static ReadUserDto MapToReadDto(User user)
        {
            return new ReadUserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                JobTitle = user.JobTitle,
                Role = user.Role.ToString(),
                NumberOfPointsAcquired = user.NumberOfPointsAcquired,
                NumberOfPointsAvailable = user.NumberOfPointsAvailable,
                CompanyId = user.CompanyId
            };
        }
    }
}
