using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Ventas_Ticket.Models;

namespace Ventas_Ticket.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Evento> Eventos => Set<Evento>();
    public DbSet<Compra> Compras => Set<Compra>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Evento>(e =>
        {
            e.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
            e.Property(x => x.Precio).HasColumnType("decimal(10,2)");
        });

        builder.Entity<Compra>(c =>
        {
            c.Property(x => x.PrecioTotal).HasColumnType("decimal(10,2)");

            c.HasOne(x => x.Evento)
                .WithMany()
                .HasForeignKey(x => x.EventoId)
                .OnDelete(DeleteBehavior.Cascade);

            c.HasOne(x => x.Usuario)
                .WithMany()
                .HasForeignKey(x => x.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
