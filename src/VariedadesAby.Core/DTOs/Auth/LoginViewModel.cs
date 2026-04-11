using System.ComponentModel.DataAnnotations;

namespace VariedadesAby.Core.DTOs.Auth;

public class LoginViewModel
{
    [Required]
    [EmailAddress]
    public string email { get; set; } = string.Empty;

    [Required]
    public string password { get; set; } = string.Empty;
}
