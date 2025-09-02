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
                return StatusCode(500, "An error occurred while creating the transaction");
            }
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ReadTransactionDto>> GetTransaction(Guid id)
        {
            try
            {
                var transaction = await _transactionService.GetTransactionByIdAsync(id);
                var userId = _currentUserService.UserId;
                if(userId != transaction.SenderId && userId != transaction.ReceiverId)
                {
                    return Forbid("You are not allowed to retrieve this transaction");
                }
                return Ok(transaction);
            }catch (KeyNotFoundException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "An error occurred while retrieving the transaction");
            }
        }

        [HttpGet("my-history")]
        public async Task<ActionResult<IEnumerable<ReadTransactionDto>>> GetMyTransactionHistory()
        {
            try
            {
               var userId = _currentUserService.UserId;
               var History = await _transactionService.GetUserTransactionHistoryAsync(userId);
               return Ok(History);
            }
            catch(Exception ex)
            {
                return StatusCode(500, "An error occurred while retrieving your history");
            }
        }

        [HttpGet("sent")]
        public async Task<ActionResult<IEnumerable<ReadTransactionDto>>> GetSentTransaction()
        {
            try
            {
                var UserId = _currentUserService.UserId;
                var transactions = await _transactionService.GetSentTransactionsAsync(UserId);
                return Ok(transactions);
            }
            catch(Exception ex)
            {
                return StatusCode(500, "An error occurred while creating the transaction");
            }
        }

        [HttpGet("received")]
        public async Task<ActionResult<IEnumerable<ReadTransactionDto>>> GetReceivedTransaction()
        {
            try
            {
                var UserId = _currentUserService.UserId;
                var transactions = await _transactionService.GetReceivedTransactionsAsync(UserId);
                return Ok(transactions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "An error occurred while creating the transaction");
            }
        }
    }
}
