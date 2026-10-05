using AssetManagement.Api.Errors;
using AssetManagement.Api.Models;
using AssetManagement.Api.Repositories;
using AssetManagement.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/assets")]
[Authorize]
public sealed class AssetsController(AssetRepository assets) : ControllerBase
{
    public const string AdminRole = "Administrador";
    public const string AdminOrOperator = "Administrador,Operador";

    [HttpGet]
    public async Task<ActionResult<PagedResult<AssetDto>>> Search([FromQuery] AssetQuery query, CancellationToken ct) =>
        Ok(await assets.SearchAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AssetDto>> GetById(int id, CancellationToken ct) =>
        Ok(await assets.GetByIdAsync(id, ct) ?? throw new BusinessException(404, "ASSET_NOT_FOUND", "El activo no existe."));

    [HttpPost]
    [Authorize(Roles = AdminRole)]
    public async Task<ActionResult<AssetDto>> Create(CreateAssetRequest request, CancellationToken ct)
    {
        var created = await assets.CreateAsync(request, User.GetUserId(), ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = AdminRole)]
    public async Task<ActionResult<AssetDto>> Update(int id, UpdateAssetRequest request, CancellationToken ct) =>
        Ok(await assets.UpdateAsync(id, request, User.GetUserId(), ct));

    [HttpGet("{id:int}/history")]
    public async Task<ActionResult<List<AssetMovementDto>>> History(int id, CancellationToken ct) =>
        Ok(await assets.GetHistoryAsync(id, ct));

    [HttpPost("{id:int}/assign")]
    [Authorize(Roles = AdminOrOperator)]
    public async Task<ActionResult<AssignmentResultDto>> Assign(int id, AssignRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await assets.AssignAsync(id, request, User.GetUserId(), ct));

    [HttpPost("{id:int}/return")]
    [Authorize(Roles = AdminOrOperator)]
    public async Task<ActionResult<AssignmentResultDto>> Return(int id, ReturnRequest request, CancellationToken ct) =>
        Ok(await assets.ReturnAsync(id, request, User.GetUserId(), ct));
}
