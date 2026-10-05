using AssetManagement.Api.Errors;
using AssetManagement.Api.Models;
using AssetManagement.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/suppliers")]
[Authorize]
public sealed class SuppliersController(SupplierRepository suppliers) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<SupplierDto>>> Search([FromQuery] SupplierQuery query, CancellationToken ct) =>
        Ok(await suppliers.SearchAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SupplierDto>> GetById(int id, CancellationToken ct) =>
        Ok(await suppliers.GetByIdAsync(id, ct) ?? throw new BusinessException(404, "SUPPLIER_NOT_FOUND", "El proveedor no existe."));

    [HttpPost]
    [Authorize(Roles = AssetsController.AdminRole)]
    public async Task<ActionResult<SupplierDto>> Create(CreateSupplierRequest request, CancellationToken ct)
    {
        var created = await suppliers.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
