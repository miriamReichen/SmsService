using Microsoft.AspNetCore.Mvc;
using SmsService.Application;
using SmsService.DTO;

namespace SmsService.API.Controllers;

[ApiController]
[Route("employees")]
public class EmployeesController : ControllerBase
{
    private readonly EmployeeService _employees;

    public EmployeesController(EmployeeService employees) => _employees = employees;

    /// <summary>Read-only list of employees with their phones (no CRUD).</summary>
    [HttpGet]
    public Task<IReadOnlyList<EmployeeDto>> List(CancellationToken ct) => _employees.ListAsync(ct);
}
