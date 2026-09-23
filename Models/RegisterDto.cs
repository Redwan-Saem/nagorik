using System.ComponentModel.DataAnnotations;

namespace Nagorik.Api.Models
{
    public class RegisterDto
    {
        [Required] public string Name { get; set; } = "";
        [Required, EmailAddress] public string Email { get; set; } = "";
        [Required] public string PhoneNumber { get; set; } = "";
        [Required, MinLength(8)] public string Password { get; set; } = "";
    }
}