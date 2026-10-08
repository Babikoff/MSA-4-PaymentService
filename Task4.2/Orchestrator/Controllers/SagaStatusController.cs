using Microsoft.AspNetCore.Mvc;
using Orchestrator.Models;
using Orchestrator.Services;

namespace Orchestrator.Controllers;

[ApiController]
[Route("sagas")]
public class SagaStatusController : ControllerBase
{
    private readonly ISagaRepository _repo;

    public SagaStatusController(ISagaRepository repo) => _repo = repo;

    [HttpGet("{paymentId:guid}/status")]
    public async Task<ActionResult<SagaState>> GetStatus(Guid paymentId)
    {
        var state = await _repo.GetAsync(paymentId);
        return state is null ? NotFound() : Ok(state);
    }
}
