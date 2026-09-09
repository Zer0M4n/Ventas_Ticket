# Despliegue en AWS (2 servidores: app + base de datos)

Esta guia asume:

- Dos instancias **EC2** con **Ubuntu Server 22.04 LTS**.
  - **Servidor A - "app"**: corre la aplicacion Blazor (.NET 8) + Nginx como proxy inverso.
  - **Servidor B - "db"**: corre PostgreSQL, solo accesible desde el Servidor A.
- Ambas instancias estan en la **misma VPC**, para poder comunicarse por IP privada.

```
Internet ---> [Servidor A: Nginx :80/:443 -> Kestrel :5000] ---> (red privada) ---> [Servidor B: PostgreSQL :5432]
```

---


## 0. Antes de empezar

- Crea un par de llaves (key pair) en EC2 para poder conectarte por SSH a ambas instancias.
- Crea (o usa) una VPC con una subred privada para "db" y una subred publica para "app"
  (si no quieres complicarte, ambas pueden ir en la misma subred publica; lo importante
  es el **Security Group** de "db", que nunca debe abrir el puerto 5432 a Internet).

### Security Groups a crear

**SG-app** (para el Servidor A):
- Entrada: TCP 22 (SSH) desde tu IP.
- Entrada: TCP 80 (HTTP) desde 0.0.0.0/0.
- Entrada: TCP 443 (HTTPS) desde 0.0.0.0/0 (si vas a usar dominio + certificado).

**SG-db** (para el Servidor B):
- Entrada: TCP 22 (SSH) desde tu IP (opcional, solo para administrar).
- Entrada: TCP 5432 (PostgreSQL) **solo desde SG-app** (selecciona el security group,
  no un rango de IPs) o desde la IP privada del Servidor A.
- Sin entrada 80/443 (este servidor no expone nada a Internet).

---

## 1. Servidor B: PostgreSQL

Conectate por SSH y actualiza el sistema:

```bash
sudo apt update && sudo apt upgrade -y
```

### 1.1 Instalar PostgreSQL 16

```bash
sudo apt install -y postgresql postgresql-contrib
sudo systemctl enable postgresql
sudo systemctl status postgresql
```

### 1.2 Crear la base de datos y el usuario de la app

```bash
sudo -u postgres psql
```

Dentro de `psql`:

```sql
CREATE DATABASE ventasticket;
CREATE USER ventasticket_user WITH PASSWORD 'PON_AQUI_UNA_CONTRASENA_FUERTE';
GRANT ALL PRIVILEGES ON DATABASE ventasticket TO ventasticket_user;
ALTER DATABASE ventasticket OWNER TO ventasticket_user;
\q
```

### 1.3 Permitir conexiones remotas (desde el Servidor A)

Edita `postgresql.conf` (la ruta tipica es `/etc/postgresql/16/main/postgresql.conf`):

```bash
sudo nano /etc/postgresql/16/main/postgresql.conf
```

Busca la linea `#listen_addresses = 'localhost'` y cambiala por:

```
listen_addresses = '*'
```

Edita `pg_hba.conf` (misma carpeta) para permitir la conexion desde la IP privada del
Servidor A (reemplaza `10.0.1.10` por la IP privada real del Servidor A):

```bash
sudo nano /etc/postgresql/16/main/pg_hba.conf
```

Agrega al final:

```
host    ventasticket    ventasticket_user    10.0.1.10/32    scram-sha-256
```

Reinicia PostgreSQL:

```bash
sudo systemctl restart postgresql
```

### 1.4 Firewall del sistema operativo (opcional, ademas del Security Group)

```bash
sudo ufw allow from 10.0.1.10 to any port 5432
sudo ufw enable
```

> El Security Group de AWS ya deberia bastar; `ufw` es una capa extra de seguridad.

En este punto el Servidor B esta listo. **Nunca** abras el puerto 5432 al 0.0.0.0/0.

---

## 2. Servidor A: aplicacion Blazor

Conectate por SSH y actualiza el sistema:

```bash
sudo apt update && sudo apt upgrade -y
```

### 2.1 Instalar el runtime de ASP.NET Core 8

```bash
sudo apt install -y wget
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

sudo apt update
sudo apt install -y aspnetcore-runtime-8.0
```

Verifica:

```bash
dotnet --list-runtimes
```

Deberias ver `Microsoft.AspNetCore.App 8.0.x` y `Microsoft.NETCore.App 8.0.x`.

> Nota: solo se necesita el **runtime** en este servidor. El **SDK** completo (para
> compilar y generar migraciones con `dotnet ef`) se usa en tu maquina de desarrollo
> o en un pipeline de CI, no en el servidor de produccion.

### 2.2 Crear carpeta de la app y usuario de servicio

```bash
sudo mkdir -p /var/www/ventasticket
sudo useradd -r -s /usr/sbin/nologin ventasticket || true
sudo chown -R ventasticket:ventasticket /var/www/ventasticket
```

### 2.3 Publicar y subir la app

En tu maquina de desarrollo (con el SDK de .NET 8 instalado):

```bash
cd Ventas_Ticket
dotnet publish -c Release -o ./publish
```

Sube el contenido de `./publish` al servidor, por ejemplo con `scp`:

```bash
scp -i tu-llave.pem -r ./publish/* ubuntu@IP_SERVIDOR_A:/tmp/ventasticket-publish
ssh -i tu-llave.pem ubuntu@IP_SERVIDOR_A
sudo mv /tmp/ventasticket-publish/* /var/www/ventasticket/
sudo chown -R ventasticket:ventasticket /var/www/ventasticket
```

### 2.4 Configurar la cadena de conexion de produccion

**No** dejes contrasenas reales en `appsettings.json` dentro del repositorio. En el
servidor, crea (o edita) `/var/www/ventasticket/appsettings.Production.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=IP_PRIVADA_SERVIDOR_B;Port=5432;Database=ventasticket;Username=ventasticket_user;Password=PON_AQUI_LA_MISMA_CONTRASENA"
  }
}
```

```bash
sudo chown ventasticket:ventasticket /var/www/ventasticket/appsettings.Production.json
sudo chmod 600 /var/www/ventasticket/appsettings.Production.json
```

Alternativa (mas segura, sin guardar la contrasena en un archivo): definir la cadena
de conexion como variable de entorno en el servicio systemd (ver siguiente paso),
usando la variable `ConnectionStrings__DefaultConnection`.

### 2.5 Generar la migracion inicial (una sola vez, antes del primer arranque)

Como el servidor de produccion solo tiene el runtime (no el SDK), genera la migracion
en tu maquina de desarrollo, contra una base local o de prueba, y **incluye la carpeta
`Migrations/` en el publish** (se genera automaticamente si ya existe en el proyecto
al momento de correr `dotnet publish`):

```bash
# en tu maquina de desarrollo, dentro de Ventas_Ticket/
dotnet ef migrations add InitialCreate
dotnet publish -c Release -o ./publish
```

La app aplica las migraciones automaticamente al iniciar (`db.Database.Migrate()` en
`Program.cs`), asi que no necesitas ejecutar `dotnet ef database update` en el
servidor de produccion.

### 2.6 Crear el servicio systemd

```bash
sudo nano /etc/systemd/system/ventasticket.service
```

Contenido:

```ini
[Unit]
Description=Ventas Ticket - App Blazor
After=network.target

[Service]
WorkingDirectory=/var/www/ventasticket
ExecStart=/usr/bin/dotnet /var/www/ventasticket/Ventas_Ticket.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=ventasticket
User=ventasticket
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5000
# Opcional: cadena de conexion por variable de entorno en vez de appsettings.Production.json
# Environment=ConnectionStrings__DefaultConnection=Host=IP_PRIVADA_SERVIDOR_B;Port=5432;Database=ventasticket;Username=ventasticket_user;Password=PON_AQUI_LA_CONTRASENA

[Install]
WantedBy=multi-user.target
```

Activa e inicia el servicio:

```bash
sudo systemctl daemon-reload
sudo systemctl enable ventasticket
sudo systemctl start ventasticket
sudo systemctl status ventasticket
```

Revisar logs si algo falla:

```bash
sudo journalctl -u ventasticket -f
```

La app queda escuchando solo en `127.0.0.1:5000` (no expuesta directamente a
Internet); Nginx sera quien la exponga en el puerto 80/443.

### 2.7 Instalar y configurar Nginx como proxy inverso

```bash
sudo apt install -y nginx
```

```bash
sudo nano /etc/nginx/sites-available/ventasticket
```

Contenido:

```nginx
server {
    listen 80;
    server_name TU_DOMINIO_O_IP;

    location / {
        proxy_pass         http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header    Upgrade $http_upgrade;
        proxy_set_header    Connection keep-alive;
        proxy_set_header    Host $host;
        proxy_cache_bypass  $http_upgrade;
        proxy_set_header    X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header    X-Forwarded-Proto $scheme;
    }
}
```

Activa el sitio:

```bash
sudo ln -s /etc/nginx/sites-available/ventasticket /etc/nginx/sites-enabled/
sudo rm -f /etc/nginx/sites-enabled/default
sudo nginx -t
sudo systemctl restart nginx
```

En este punto ya deberias poder entrar a `http://IP_O_DOMINIO_SERVIDOR_A` y ver la app.

### 2.8 HTTPS con Let's Encrypt (opcional, requiere un dominio apuntando al Servidor A)

```bash
sudo apt install -y certbot python3-certbot-nginx
sudo certbot --nginx -d tu-dominio.com
```

Certbot configura el certificado y ajusta Nginx automaticamente; renueva solo
(`certbot.timer` ya viene habilitado en Ubuntu).

---

## 3. Verificacion final

1. Abre la URL publica del Servidor A: deberias ver la pantalla "Funcion de Prueba".
2. Registra un usuario nuevo y compra un boleto.
3. Revisa en el Servidor B que se haya guardado el registro:

   ```bash
   sudo -u postgres psql -d ventasticket -c "SELECT * FROM \"Compras\";"
   ```

---

## 4. Notas de seguridad

- El puerto 5432 (PostgreSQL) **nunca** debe estar abierto a 0.0.0.0/0; solo al
  Security Group / IP privada del servidor de la app.
- No subas contrasenas reales a un repositorio Git. Usa
  `appsettings.Production.json` fuera del control de versiones, o variables de
  entorno, o (para un entorno mas avanzado) AWS Secrets Manager / Systems Manager
  Parameter Store.
- Mantén ambos servidores actualizados: `sudo apt update && sudo apt upgrade -y`.
- Considera activar backups automaticos de PostgreSQL (snapshots de EBS o `pg_dump`
  programado) antes de usar esto con datos reales.
