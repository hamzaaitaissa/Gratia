using Gratia.Application.DTOs.Transaction;
using Gratia.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Gratia.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TransactionController : ControllerBase
    {
        private readonly ITransactionService _transactionService;
        private readonly ICurrentUserService _currentUserService;

        public TransactionController(ITransactionService transactionService, ICurrentUserService currentUserService)
        {
            _transactionService = transactionService;
            _currentUserService = currentUserService;
        }

        [HttpPost]
        public async Task<ActionResult<ReadTransactionDto>> CreateTransaction([FromBody] CreateTransactionDto createTransactionDto)
        {
            try
            {
                var userId = _currentUserService.UserId;
                var transactionCreated = await _transactionService.CreateTransactionAsync(userId, createTransactionDto);
                return Ok(transactionCreated);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            { 
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{id}")]
        public Task<ActionResult<ReadTransactionDto>> GetTransaction(int id)
        {
            try
            {

            }catch (ArgumentException ex)
            {

            }
        }
    }
}
