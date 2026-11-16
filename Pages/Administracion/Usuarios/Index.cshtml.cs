using InventarioSmt.Data;
using InventarioSmt.Models;
using InventarioSmt.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InventarioSmt.Pages.Administracion.Usuarios;

public class IndexModel : PageModel
{
    private const int TamanoPagina = 25;
    private readonly AppDbContext _db;
    private readonly MenuService _menu;
    private readonly SesionUsuario _sesion;

    public IndexModel(AppDbContext db, MenuService menu, SesionUsuario sesion)
    {
        _db = db;
        _menu = menu;
        _sesion = sesion;
    }

    [BindProperty(SupportsGet = true)]
    public string? Buscar { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Grupo { get; set; }

    /// <summary>"0" activos, "1" inactivos, vacío todos.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Estado { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Pagina { get; set; } = 1;

    public List<Usuario> Usuarios { get; private set; } = new();
    public Dictionary<int, List<CardMenu>> Accesos { get; private set; } = new();
    public Dictionary<int, List<int>> ReparticionesPorUsuario { get; private set; } = new();
    public SelectList Grupos { get; private set; } = default!;
    public int Total { get; private set; }
    public int TotalPaginas { get; private set; }
    public string? Mensaje { get; private set; }
    public int IdPropio => _sesion.Usuario?.IdUsuario ?? 0;

    public async Task OnGetAsync()
    {
        Mensaje = TempData["Mensaje"] as string;

        var q = _db.Usuarios.AsNoTracking().Include(u => u.Grupo).AsQueryable();

        if (!string.IsNullOrWhiteSpace(Buscar))
        {
            var texto = Buscar.Trim();
            var digitos = CuilHelper.Normalizar(texto);
            q = q.Where(u => u.ApellidoNombre.Contains(texto)
                          || (digitos.Length > 0 && u.Cuil.Contains(digitos)));
        }

        if (Grupo.HasValue) q = q.Where(u => u.IdGrupo == Grupo);
        if (Estado is "0" or "1")
        {
            var estado = byte.Parse(Estado);
            q = q.Where(u => u.Estado == estado);
        }

        Total = await q.CountAsync();
        TotalPaginas = Math.Max(1, (int)Math.Ceiling(Total / (double)TamanoPagina));
        Pagina = Math.Clamp(Pagina, 1, TotalPaginas);

        Usuarios = await q.OrderBy(u => u.ApellidoNombre)
            .Skip((Pagina - 1) * TamanoPagina).Take(TamanoPagina)
            .ToListAsync();

        var ids = Usuarios.Select(u => u.IdUsuario).ToList();
        Accesos = await _menu.AccesosDeVariosAsync(ids);
        ReparticionesPorUsuario = (await _db.UsuariosReparticiones.AsNoTracking()
                .Where(r => ids.Contains(r.IdUsuario)).ToListAsync())
            .GroupBy(r => r.IdUsuario)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Reparticion).OrderBy(c => c).ToList());
        Grupos = new SelectList(await _db.Grupos.AsNoTracking().OrderBy(g => g.Nombre).ToListAsync(),
            nameof(Models.Grupo.IdGrupo), nameof(Models.Grupo.Nombre), Grupo);
    }

    public async Task<IActionResult> OnPostBajaAsync(int id, bool reactivar)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == id);
        if (usuario is null) return NotFound();

        if (!reactivar && id == IdPropio)
        {
            TempData["Mensaje"] = "No podés darte de baja a vos mismo: tiene que hacerlo otro administrador.";
        }
        else
        {
            usuario.Estado = reactivar ? (byte)0 : (byte)1;
            await _db.SaveChangesAsync();
            TempData["Mensaje"] = reactivar
                ? $"Se reactivó a {usuario.ApellidoNombre}."
                : $"Se dio de baja a {usuario.ApellidoNombre}: ya no puede entrar al sistema.";
        }

        return RedirectToPage(Ruta(Pagina));
    }

    public Dictionary<string, string> Ruta(int? pagina = null)
    {
        var ruta = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(Buscar)) ruta["Buscar"] = Buscar;
        if (Grupo.HasValue) ruta["Grupo"] = Grupo.Value.ToString();
        if (!string.IsNullOrEmpty(Estado)) ruta["Estado"] = Estado;
        if (pagina is > 1) ruta["Pagina"] = pagina.Value.ToString();
        return ruta;
    }
}
