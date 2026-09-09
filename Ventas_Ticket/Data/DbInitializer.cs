using Ventas_Ticket.Models;

namespace Ventas_Ticket.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (!db.Eventos.Any())
        {
            db.Eventos.Add(new Evento
            {
                Nombre = "Funcion de Prueba",
                Descripcion = "Evento de prueba para validar el flujo de compra de boletos.",
                FechaEvento = DateTime.UtcNow.AddDays(30),
                Lugar = "Recinto de Prueba",
                Precio = 100.00m,
                CupoDisponible = 100
            });

            await db.SaveChangesAsync();
        }
    }
}
