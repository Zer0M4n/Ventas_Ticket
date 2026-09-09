# Ventas Ticket

Proyecto minimo de venta de boletos para eventos, hecho con **Blazor Server (.NET 8)**,
**Entity Framework Core** y **PostgreSQL**.

## Que incluye

- Registro e inicio de sesion de usuarios (ASP.NET Core Identity).
- Una sola pantalla principal ("Funcion de Prueba") donde el usuario logueado puede
  comprar boletos (sin seleccion de butacas, solo cantidad).
- Pantalla "Mis Boletos" con el historial de compras del usuario.
- Sin panel de administracion: solo existe el flujo de usuarios comprando boletos.
- Base de datos minima en PostgreSQL: `AspNetUsers` (Identity), `Eventos` y `Compras`.

## Estructura relevante

```
Ventas_Ticket/
  Program.cs                     -> arranque, Identity, EF Core, migraciones automaticas
  Models/                        -> ApplicationUser, Evento, Compra
  Data/AppDbContext.cs           -> DbContext (Identity + Eventos + Compras)
  Data/DbInitializer.cs          -> siembra el evento "Funcion de Prueba"
  Components/Account/            -> Login, Register, Logout (Identity minimo, sin panel admin)
  Components/Pages/Home.razor    -> pantalla de compra de boletos
  Components/Pages/MisBoletos.razor -> historial de compras del usuario
```

## Requisitos para desarrollo local

- .NET 8 SDK
- PostgreSQL 14+ (local o en Docker)
- Herramienta EF Core: `dotnet tool install --global dotnet-ef` (si no la tienes)

## Configurar la base de datos local

1. Crea una base de datos y un usuario en PostgreSQL, por ejemplo:

   ```sql
   CREATE DATABASE ventasticket;
   CREATE USER ventasticket_user WITH PASSWORD 'TU_PASSWORD';
   GRANT ALL PRIVILEGES ON DATABASE ventasticket TO ventasticket_user;
   ```

2. Actualiza `appsettings.Development.json` (o usa `dotnet user-secrets`) con tu cadena
   de conexion real, por ejemplo:

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Port=5432;Database=ventasticket;Username=ventasticket_user;Password=TU_PASSWORD"
     }
   }
   ```

## Crear la migracion inicial

Este repo no incluye migraciones generadas (se crean con el SDK de .NET, que no esta
disponible en este entorno). Antes de correr el proyecto por primera vez, desde la
carpeta del proyecto (`Ventas_Ticket/`) ejecuta:

```bash
dotnet restore
dotnet ef migrations add InitialCreate
```

Esto genera la carpeta `Migrations/` con el esquema (Identity + Eventos + Compras).
El propio `Program.cs` ya llama a `db.Database.Migrate()` al iniciar, asi que no hace
falta correr `dotnet ef database update` a mano (aunque tambien puedes hacerlo si
prefieres aplicarlas manualmente).

## Ejecutar en local

```bash
dotnet run
```

La app crea las tablas (si no existen) y siembra automaticamente el evento
"Funcion de Prueba" la primera vez que arranca.

## Publicar para produccion

```bash
dotnet publish -c Release -o ./publish
```

Ver `DEPLOY-AWS.md` para la guia completa de despliegue en AWS con dos servidores
separados (servidor de aplicacion y servidor de base de datos).
