using Microsoft.AspNetCore.Identity;

namespace Ventas_Ticket.Models;

public class ApplicationUser : IdentityUser
{
    public string? NombreCompleto { get; set; }
}
