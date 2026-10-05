using AssetManagement.Api.Errors;
using AssetManagement.Api.Models;
using AssetManagement.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize]
public sealed class EmployeesController(EmployeeRepository employees) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<EmployeeDto>>> Search([FromQuery] EmployeeQuery query, CancellationToken ct) =>
        Ok(await employees.SearchAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmployeeDto>> GetById(int id, CancellationToken ct) =>
        Ok(await employees.GetByIdAsync(id, ct) ?? throw new BusinessException(404, "EMPLOYEE_NOT_FOUND", "El colaborador no existe."));

    [HttpPost]
    [Authorize(Roles = AssetsController.AdminRole)]
    public async Task<ActionResult<EmployeeDto>> Create(CreateEmployeeRequest request, CancellationToken ct)
    {
        var created = await employees.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
