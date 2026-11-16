using System.Security.Claims;
using InventarioSmt.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InventarioSmt.Servicios;

/// <summary>
/// Toma el token de Ciudad Digital (querystring ?auth= o cookie), lo valida contra el
/// portal, resuelve el usuario local por CUIL y arma la sesión con sus accesos.
/// </summary>
public class AutenticacionMiddleware
{
    private static readonly string[] RutasPublicas = { "/SinAcceso", "/Error" };

    private readonly RequestDelegate _siguiente;
    private readonly CiudadDigitalOptions _opciones;

    public AutenticacionMiddleware(RequestDelegate siguiente, IOptions<CiudadDigitalOptions> opciones)
    {
        _siguiente = siguiente;
        _opciones = opciones.Value;
    }

    public async Task InvokeAsync(HttpContext ctx, CiudadDigitalService portal,
        AppDbContext db, MenuService menu, SesionUsuario sesion)
    {
        var ruta = ctx.Request.Path.Value ?? "/";
        if (RutasPublicas.Any(r => ruta.StartsWith(r, StringComparison.OrdinalIgnoreCase)))
        {
            await _siguiente(ctx);
            return;
        }

        // El portal entrega el token por querystring; se guarda en cookie y se limpia la URL.
        if (ctx.Request.Query.TryGetValue("auth", out var tokenUrl) && !string.IsNullOrWhiteSpace(tokenUrl))
        {
            ctx.Response.Cookies.Append(_opciones.CookieName, tokenUrl.ToString()!, new CookieOptions
            {
                HttpOnly = true,
                Secure = ctx.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                IsEssential = true
            });

            var limpia = ctx.Request.Query
                .Where(q => q.Key != "auth")
                .Select(q => $"{Uri.EscapeDataString(q.Key)}={Uri.EscapeDataString(q.Value.ToString())}");
            var qs = string.Join("&", limpia);
            ctx.Response.Redirect(qs.Length > 0 ? $"{ruta}?{qs}" : ruta);
            return;
        }

        PersonaCiudadDigital? persona;
        if (_opciones.ModoDesarrollo && !string.IsNullOrWhiteSpace(_opciones.CuilDesarrollo))
        {
            persona = new PersonaCiudadDigital(0, _opciones.CuilDesarrollo, _opciones.NombreDesarrollo);
        }
        else
        {
            var token = ctx.Request.Cookies[_opciones.CookieName];
            persona = string.IsNullOrWhiteSpace(token)
                ? null
                : await portal.ValidarTokenAsync(token, ctx.RequestAborted);
        }

        if (persona is null)
        {
            ctx.Response.Cookies.Delete(_opciones.CookieName);
            ctx.Response.Redirect(_opciones.LoginUrl);
            return;
        }

        sesion.Autenticado = true;
        sesion.Cuil = persona.DocumentoPersona;
        sesion.Nombre = persona.NombrePersona;
        sesion.IdPersona = persona.IdPersona;

        var usuario = await db.Usuarios
            .Include(u => u.Grupo)
            .FirstOrDefaultAsync(u => u.Cuil == persona.DocumentoPersona, ctx.RequestAborted);

        if (usuario is null || usuario.Estado != 0)
        {
            ctx.Response.Redirect("/SinAcceso");
            return;
        }

        sesion.Usuario = usuario;
        sesion.AsignarReparticiones(usuario.TodasReparticiones, await db.UsuariosReparticiones
            .Where(r => r.IdUsuario == usuario.IdUsuario)
            .Select(r => r.Reparticion)
            .ToListAsync(ctx.RequestAborted));
        sesion.Accesos = await menu.AccesosDeAsync(usuario, ctx.RequestAborted);

        var identidad = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.IdUsuario.ToString()),
            new Claim(ClaimTypes.Name, usuario.ApellidoNombre),
            new Claim("cuil", usuario.Cuil)
        }, "CiudadDigital");
        identidad.AddClaims(sesion.Accesos.Select(a => new Claim("acceso", a.Id.ToString())));
        ctx.User = new ClaimsPrincipal(identidad);

        var rutasModulos = await menu.RutasDeModulosAsync(ctx.RequestAborted);
        var esModulo = rutasModulos.Any(r => ruta.StartsWith(r, StringComparison.OrdinalIgnoreCase));
        if (esModulo && !sesion.TieneAcceso(ruta))
        {
            ctx.Response.Redirect("/SinAcceso");
            return;
        }

        await _siguiente(ctx);
    }
}
