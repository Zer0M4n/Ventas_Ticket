namespace Ventas_Ticket.Models;

public class Compra
{
    public int Id { get; set; }

    public int EventoId { get; set; }
    public Evento? Evento { get; set; }

    public string UsuarioId { get; set; } = string.Empty;
    public ApplicationUser? Usuario { get; set; }

    public int Cantidad { get; set; }
    public decimal PrecioTotal { get; set; }
    public DateTime FechaCompra { get; set; }
}
