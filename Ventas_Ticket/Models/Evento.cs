namespace Ventas_Ticket.Models;

public class Evento
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public DateTime FechaEvento { get; set; }
    public string Lugar { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public int CupoDisponible { get; set; }
}
