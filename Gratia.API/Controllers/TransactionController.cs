using Gratia.Application.DTOs.Transaction;
using Gratia.Application.Features.Transactions.Commands;
using Gratia.Application.Features.Transactions.Queries;
using Gratia.Application.Interfaces;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gratia.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TransactionController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ICurrentUserService _currentUserService;

        public TransactionController(ISender sender, ICurrentUserService currentUserService)
        {
            _sender = sender;
            _currentUserService = currentUserService;
        }

        [HttpPost]
        public async Task<ActionResult<ReadTransactionDto>> CreateTransaction([FromBody] CreateTransactionDto createTransactionDto, CancellationToken cancellationToken)
        {
            var transaction = await _sender.Send(new CreateTransactionCommand(_currentUserService.UserId, createTransactionDto), cancellationToken);
            return Ok(transaction);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ReadTransactionDto>> GetTransaction(Guid id, CancellationToken cancellationToken)
        {
            var transaction = await _sender.Send(new GetTransactionByIdQuery(id, _currentUserService.UserId, _currentUserService.CompanyId), cancellationToken);
            return Ok(transaction);
        }

        [HttpGet("my-history")]
        public async Task<ActionResult<IEnumerable<ReadTransactionDto>>> GetMyTransactionHistory(CancellationToken cancellationToken)
        {
            var history = await _sender.Send(new GetUserTransactionHistoryQuery(_currentUserService.UserId), cancellationToken);
            return Ok(history);
        }

        [HttpGet("sent")]
        public async Task<ActionResult<IEnumerable<ReadTransactionDto>>> GetSentTransaction(CancellationToken cancellationToken)
        {
            var transactions = await _sender.Send(new GetSentTransactionsQuery(_currentUserService.UserId), cancellationToken);
            return Ok(transactions);
        }

        [HttpGet("received")]
        public async Task<ActionResult<IEnumerable<ReadTransactionDto>>> GetReceivedTransaction(CancellationToken cancellationToken)
        {
            var transactions = await _sender.Send(new GetReceivedTransactionsQuery(_currentUserService.UserId), cancellationToken);
            return Ok(transactions);
        }

        [HttpGet("company-history")]
        public async Task<ActionResult<ReadTransactionHistoryDto>> GetCompanyHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
        {
            var history = await _sender.Send(new GetCompanyTransactionHistoryQuery(_currentUserService.CompanyId, page, pageSize), cancellationToken);
            return Ok(history);
        }
    }
}
