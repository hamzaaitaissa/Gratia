using Gratia.Application.Interfaces;
using Gratia.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.Services
{
    public class PointResetService : IPointResetService
    {

        private readonly IUserRepository _userRepository;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PointResetService> _logger;

        public PointResetService(IUserRepository userRepository, ILogger<PointResetService> logger, IConfiguration configuration)
        {
            _userRepository = userRepository;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task ResetPointMonthly()
        {
            try
            {
                _logger.LogInformation("Starting the monthly reset process");
                int monthlyAllowance = _configuration.GetValue<int>("AppSettings:MonthlyAllowance");
                if(monthlyAllowance <= 0)
                {
                    _logger.LogError("Invalid Monthly Allowance");
                    throw new InvalidOperationException("Monthly Allowance must be greater than 0");
                }

                var users = await _userRepository.GetAllAsync();
                if (!users.Any())
                {
                    _logger.LogError("No Users found");
                    return;
                }
                var updatedCount = 0;
                foreach (var user in users)
                {
                    try
                    {
                        user.NumberOfPointsAvailable = monthlyAllowance;
                        await _userRepository.UpdateAsync(user);
                        updatedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to update points for user {UserId}", user.Id);
                    }
                }
                _logger.LogInformation($"Completed monthly point reset. affected users count: {updatedCount}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete monthly point reset");
                throw;
            }
            
        }
    }
}
