using InventarioSmt.Data;
using InventarioSmt.Models;
using InventarioSmt.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace InventarioSmt.Pages.Administracion.Usuarios;

/// <summary>Grupos de usuarios. Vive bajo /Administracion/Usuarios para quedar protegido por ese módulo.</summary>
public class GruposModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly MenuService _menu;
    private readonly SesionUsuario _sesion;

    public GruposModel(AppDbContext db, MenuService menu, SesionUsuario sesion)
    {
        _db = db;
        _menu = menu;
        _sesion = sesion;
    }

    public List<Grupo> Lista { get; private set; } = new();
    public Dictionary<int, int> Miembros { get; private set; } = new();
    public string? Mensaje { get; private set; }

    public async Task OnGetAsync()
    {
        Mensaje = TempData["Mensaje"] as string;

        Lista = await _db.Grupos.AsNoTracking()
            .Include(g => g.Accesos).ThenInclude(a => a.Card)
            .OrderBy(g => g.Nombre)
            .ToListAsync();

        Miembros = await _db.Usuarios.AsNoTracking()
            .Where(u => u.IdGrupo != null)
            .GroupBy(u => u.IdGrupo!.Value)
            .Select(g => new { g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Cantidad);
    }

    public async Task<IActionResult> OnPostBajaAsync(int id, bool reactivar)
    {
        var grupo = await _db.Grupos.Include(g => g.Accesos).FirstOrDefaultAsync(g => g.IdGrupo == id);
        if (grupo is null) return NotFound();

        var estado = reactivar ? (byte)0 : (byte)1;
        var error = await GrupoEditarModel.ValidarGrupoPropioAsync(_db, _menu, _sesion, id, estado,
            grupo.Accesos.Select(a => a.IdCard).ToList());

        if (error is not null)
        {
            TempData["Mensaje"] = error;
            return RedirectToPage();
        }

        grupo.Estado = estado;
        await _db.SaveChangesAsync();
        await _menu.SincronizarCsvAsync(
            await _db.Usuarios.Where(u => u.IdGrupo == id).Select(u => u.IdUsuario).ToListAsync());

        TempData["Mensaje"] = reactivar
            ? $"Se reactivó el grupo {grupo.Nombre}."
            : $"Se dio de baja el grupo {grupo.Nombre}: sus usuarios conservan solo los accesos individuales.";
        return RedirectToPage();
    }
}
