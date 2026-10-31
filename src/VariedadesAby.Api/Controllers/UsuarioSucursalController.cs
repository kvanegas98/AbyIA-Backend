using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VariedadesAby.Core.DTOs.Usuarios;
using VariedadesAby.Core.Interfaces;

namespace VariedadesAby.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UsuarioSucursalController : ControllerBase
{
    private readonly IUsuarioSucursalService _service;

    public UsuarioSucursalController(IUsuarioSucursalService service)
    {
        _service = service;
    }

    // GET: api/UsuarioSucursal
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var resultado = await _service.ListarAsync();
        return Ok(resultado);
    }

    // GET: api/UsuarioSucursal/Sucursales
    [HttpGet("Sucursales")]
    public async Task<IActionResult> ListarSucursales()
    {
        var resultado = await _service.ListarSucursalesAsync();
        return Ok(resultado);
    }

    // GET: api/UsuarioSucursal/5/Sucursales
    [HttpGet("{idUsuario:int}/Sucursales")]
    public async Task<IActionResult> ListarSucursalesUsuario(int idUsuario)
    {
        var resultado = await _service.ListarSucursalesUsuarioAsync(idUsuario);
        return Ok(resultado);
    }

    // PUT: api/UsuarioSucursal/5/Reasignar
    [HttpPut("{idUsuario:int}/Reasignar")]
    public async Task<IActionResult> Reasignar(
        [FromRoute] int idUsuario,
        [FromBody] ReasignarSucursalRequestDto dto)
    {
        if (idUsuario <= 0 || dto.IdSucursalVieja <= 0 || dto.IdSucursalNueva <= 0)
            return BadRequest(new { mensaje = "Parámetros inválidos." });

        if (dto.IdSucursalVieja == dto.IdSucursalNueva)
            return BadRequest(new { mensaje = "La sucursal nueva debe ser diferente a la actual." });

        try
        {
            await _service.ReasignarAsync(idUsuario, dto.IdSucursalVieja, dto.IdSucursalNueva);
            return Ok(new { mensaje = "Sucursal actualizada correctamente." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    // PUT: api/UsuarioSucursal/5
    [HttpPut("{idUsuario:int}")]
    public async Task<IActionResult> Asignar(
        [FromRoute] int idUsuario,
        [FromBody] AsignarSucursalRequestDto dto)
    {
        if (idUsuario <= 0 || dto.IdSucursal <= 0)
            return BadRequest(new { mensaje = "Parámetros inválidos." });

        try
        {
            await _service.AsignarAsync(idUsuario, dto.IdSucursal);
            return Ok(new { mensaje = "Sucursal asignada correctamente." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }
}
