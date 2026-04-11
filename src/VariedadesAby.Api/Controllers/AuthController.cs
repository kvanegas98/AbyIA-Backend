using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using VariedadesAby.Core.DTOs.Auth;

namespace VariedadesAby.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IDbConnection _db;
    private readonly IConfiguration _config;

    public AuthController(IDbConnection db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpPost("[action]")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var email = model.email.ToLower().Trim();

        var usuario = await _db.QueryFirstOrDefaultAsync<UsuarioLoginDto>(
            @"SELECT u.idusuario, u.nombre, u.email, u.password_hash, u.password_salt,
                     u.idsucursal, u.condicion, r.nombre AS rolNombre
                FROM usuario u WITH (NOLOCK)
               INNER JOIN rol r WITH (NOLOCK) ON r.idrol = u.idrol
               WHERE u.email = @email AND u.condicion = 1",
            new { email });

        if (usuario is null)
            return Unauthorized(new { mensaje = "Credenciales incorrectas." });

        if (!VerificarPasswordHash(model.password, usuario.password_hash, usuario.password_salt))
            return Unauthorized(new { mensaje = "Credenciales incorrectas." });

        string[] rolesPermitidos = ["Administrador", "Gestor de Operaciones"];
        if (!rolesPermitidos.Contains(usuario.rolNombre))
            return Unauthorized(new { mensaje = "No tienes permiso para acceder a este sistema." });

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.idusuario.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, usuario.rolNombre),
            new("idusuario",  usuario.idusuario.ToString()),
            new("rol",        usuario.rolNombre),
            new("nombre",     usuario.nombre),
            new("idsucursal", usuario.idsucursal.ToString())
        };

        return Ok(new
        {
            token      = GenerarToken(claims),
            idusuario = usuario.idusuario,
            nombre    = usuario.nombre,
            rol       = usuario.rolNombre,
            idsucursal = usuario.idsucursal
        });
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static bool VerificarPasswordHash(string password, byte[] hash, byte[] salt)
    {
        using var hmac = new HMACSHA512(salt);
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        return computedHash.SequenceEqual(hash);
    }

    private string GenerarToken(List<Claim> claims)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:             _config["Jwt:Issuer"],
            audience:           _config["Jwt:Issuer"],
            expires:            DateTime.Now.AddDays(1),
            signingCredentials: creds,
            claims:             claims);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ─── DTO interno ─────────────────────────────────────────────────────────

    private sealed class UsuarioLoginDto
    {
        public int idusuario { get; set; }
        public string nombre { get; set; } = string.Empty;
        public string email { get; set; } = string.Empty;
        public byte[] password_hash { get; set; } = [];
        public byte[] password_salt { get; set; } = [];
        public int idsucursal { get; set; }
        public bool condicion { get; set; }
        public string rolNombre { get; set; } = string.Empty;
    }
}
