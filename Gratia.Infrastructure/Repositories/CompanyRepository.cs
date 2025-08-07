using Gratia.Application.DTOs.UserDTO;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Gratia.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Infrastructure.Repositories
{
    public class CompanyRepository : ICompanyRepository
    {
        private readonly GratiaDbContext _gratiaDbContext;

        public CompanyRepository(GratiaDbContext gratiaDbContext)
        {
            _gratiaDbContext = gratiaDbContext;
        }

        public async Task<Company> AddAsync(Company company)
        {
            if(company == null) throw new ArgumentNullException(nameof(company));
            await _gratiaDbContext.AddAsync(company);   
            await _gratiaDbContext.SaveChangesAsync();
            return company;
        }

        public Task DeleteAsync()
        {
            throw new NotImplementedException();
        }

        public Task<Company> UpdateAsync(Company company)
        {
            throw new NotImplementedException();
        }
    }
}
