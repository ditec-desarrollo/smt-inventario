# Deploy de Inventario SMT en Linux (Debian 11)

Guía para instalar y actualizar la aplicación en el servidor. Todos los comandos se ejecutan como `root`.

La aplicación es ASP.NET Core 8 publicada como **self-contained linux-x64**: trae su propio runtime, así que **no hay que instalar .NET** en el servidor. Corre como servicio `systemd` en `127.0.0.1:5000`, y Apache la publica hacia afuera como proxy inverso.

| Qué | Dónde |
|---|---|
| Aplicación | `/opt/inventario/` (ejecutable `InventarioSmt`) |
| Fotos de los bienes | `/opt/inventario/wwwroot/uploads/` (**no se borra nunca**) |
| Configuración con claves | `/etc/inventario/inventario.env` |
| Servicio | `/etc/systemd/system/inventario.service` |
| Sitio Apache | `/etc/apache2/sites-available/inventariomuni.conf` |
| Logs de la app | `journalctl -u inventario` |

Los archivos de ejemplo están en esta carpeta `deploy/` del repositorio.

## 0. Requisitos previos

- Debian 11 x64 con Apache 2. Para confirmar la versión: `cat /etc/debian_version`.
- Acceso desde el servidor a la base MariaDB 10.4 (host, puerto, base, usuario y clave).
- Acceso HTTPS saliente a `https://estadisticas.smt.gob.ar:5000`, que es la validación del login de Ciudad Digital.
- DNS `inventariomuni.smt.gob.ar` apuntando al servidor, y su certificado TLS (sirve un comodín `*.smt.gob.ar`).
- La cookie `smt_auth` de Ciudad Digital tiene que estar emitida para el dominio `.smt.gob.ar`. Si no, la app no la recibe y el usuario vuelve al login en cada entrada.

## 1. Paquetes del sistema

```bash
apt-get update
apt-get install -y libicu67 ca-certificates curl rsync
```

Si `apt-get update` da **404** en `bullseye-security`, comentá esa línea en `/etc/apt/sources.list` y repetí. Es una consecuencia de que Debian 11 ya no tiene soporte.

## 2. Obtener el paquete

Descargá el archivo `inventario-linux-x64-<versión>.tar.gz` desde **Releases** del repositorio (https://github.com/ditec-desarrollo/smt-inventario/releases) y copialo al servidor, por ejemplo a `/root/`.

## 3. Instalación inicial (solo la primera vez)

```bash
# Aplicación
mkdir -p /opt/inventario
tar -xzf /root/inventario-linux-x64-<versión>.tar.gz -C /opt/inventario
chmod +x /opt/inventario/InventarioSmt
mkdir -p /opt/inventario/wwwroot/uploads
chown -R root:root /opt/inventario
chown -R www-data:www-data /opt/inventario/wwwroot/uploads

# Configuración con claves (completar usuario y clave reales de MariaDB)
mkdir -p /etc/inventario
cp inventario.env.ejemplo /etc/inventario/inventario.env
nano /etc/inventario/inventario.env
chown root:www-data /etc/inventario/inventario.env
chmod 640 /etc/inventario/inventario.env

# Servicio
cp inventario.service /etc/systemd/system/inventario.service
systemctl daemon-reload
systemctl enable --now inventario
systemctl status inventario
```

La app solo escribe en `wwwroot/uploads`. Todo lo demás queda de `root` y `www-data` lo puede leer pero no modificar.

## 4. Apache

```bash
cp inventariomuni.conf /etc/apache2/sites-available/inventariomuni.conf
nano /etc/apache2/sites-available/inventariomuni.conf   # rutas del certificado
a2enmod ssl proxy proxy_http headers
a2ensite inventariomuni
apachectl configtest
systemctl reload apache2
```

## 5. Verificación

```bash
systemctl status inventario               # tiene que decir "active (running)"
journalctl -u inventario -n 50            # sin errores al arrancar
curl -I http://127.0.0.1:5000/            # 200 o redirección (302) al login
curl -sI https://estadisticas.smt.gob.ar:5000/ | head -1   # el servidor llega a Ciudad Digital
```

Después, desde un navegador: iniciar sesión en Ciudad Digital, entrar a `https://inventariomuni.smt.gob.ar`, abrir un bien y subirle una foto de prueba.

## 6. Actualización a una versión nueva

```bash
systemctl stop inventario

# Copia de la versión actual, sin las fotos, para poder volver atrás
rm -rf /opt/inventario.anterior
rsync -a --exclude 'wwwroot/uploads/' /opt/inventario/ /opt/inventario.anterior/

# Versión nueva: reemplaza todo menos las fotos
rm -rf /tmp/inventario-nuevo && mkdir /tmp/inventario-nuevo
tar -xzf /root/inventario-linux-x64-<versión>.tar.gz -C /tmp/inventario-nuevo
rsync -a --delete --exclude 'wwwroot/uploads/' /tmp/inventario-nuevo/ /opt/inventario/
chmod +x /opt/inventario/InventarioSmt
chown -R www-data:www-data /opt/inventario/wwwroot/uploads

systemctl start inventario
systemctl status inventario
```

**Nunca** borrar ni sobrescribir `/opt/inventario/wwwroot/uploads`. Ahí están las fotos y no hay otra copia.

### Volver a la versión anterior

```bash
systemctl stop inventario
rsync -a --delete --exclude 'wwwroot/uploads/' /opt/inventario.anterior/ /opt/inventario/
systemctl start inventario
```

Si la versión nueva traía cambios en la base de datos (scripts `.sql` indicados en el Release), volver atrás puede requerir también revertirlos.

## 7. Respaldo

Además del backup de la base MariaDB, hay que respaldar las fotos:

```bash
tar -czf /ruta/backup/uploads-$(date +%F).tar.gz -C /opt/inventario/wwwroot uploads
```

## 8. Problemas comunes

| Síntoma | Causa probable |
|---|---|
| El servicio no arranca: "Falta la cadena de conexión 'MySql'" | `/etc/inventario/inventario.env` no existe, está vacío o `www-data` no lo puede leer |
| El servicio no arranca: "Couldn't find a valid ICU package" | Falta `libicu67` (paso 1) |
| "Permission denied" al arrancar | Falta `chmod +x /opt/inventario/InventarioSmt` |
| Vuelve al login de Ciudad Digital una y otra vez | La cookie `smt_auth` no llega a `inventariomuni.smt.gob.ar` (alcance del dominio), o el servidor no llega a `estadisticas.smt.gob.ar:5000` |
| Error 502/503 en el navegador | El servicio está caído: `systemctl status inventario` |
| Error 413 al subir fotos | Falta o es chico el `LimitRequestBody` en Apache |
| Error al guardar fotos | `www-data` no puede escribir en `wwwroot/uploads` |
| En el log: "Failed to determine the https port for redirect" | Es esperable: la app escucha en HTTP detrás de Apache. No es un error |
