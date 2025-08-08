using Gratia.Application.DTOs.UserDTO;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Gratia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
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
            await _gratiaDbContext.Companies.AddAsync(company);   
            await _gratiaDbContext.SaveChangesAsync();
            return company;
        }

        public async Task DeleteAsync(Guid Id)
        {
            if(Id == Guid.Empty) throw new ArgumentNullException("Invalid company id",nameof(Id));
            var company = await _gratiaDbContext.Companies.FirstOrDefaultAsync(c => c.Id == Id);
            if(company == null)
            {
                throw new InvalidOperationException("Company does not exist");
            }
            try
            {
                _gratiaDbContext.Companies.Remove(company);
                await _gratiaDbContext.SaveChangesAsync();
            }catch(DbUpdateException ex)
            {
                throw new InvalidOperationException("Failed to execute the operation");
            }
        }

        public async Task<Company> UpdateAsync(Company company)
        {
            if(company == null) throw new ArgumentNullException(nameof(company));
            var companz = await _gratiaDbContext.Companies.AnyAsync(c => c.Id == company.Id);
            if(company == null)
            {
                throw new InvalidOperationException("Company does not exist");
            }
            try
            {
                _gratiaDbContext.Companies.Update(company);
                await _gratiaDbContext.SaveChangesAsync();
                return company;
            }
            catch(DbUpdateException ex)
            {
                throw new InvalidOperationException("Failed to execute the operation");
            }

        }
    }
}
