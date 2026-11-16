using InventarioSmt.Data;
using InventarioSmt.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace InventarioSmt.Pages.Inventario;

public class FotosModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly FotoService _fotos;
    private readonly SesionUsuario _sesion;

    public FotosModel(AppDbContext db, FotoService fotos, SesionUsuario sesion)
    {
        _db = db;
        _fotos = fotos;
        _sesion = sesion;
    }

    public Models.Inventario Bien { get; private set; } = default!;
    public List<Models.InventarioFoto> Galeria { get; private set; } = new();

    [BindProperty]
    public List<IFormFile> Archivos { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await CargarAsync(id)) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostSubirAsync(int id)
    {
        if (!await _db.Inventario.AnyAsync(i => i.Id == id)) return NotFound();

        if (!await CargarAsync(id)) return NotFound();

        var errores = new List<string>();
        var cargadas = 0;
        foreach (var archivo in Archivos.Where(a => a.Length > 0))
        {
            var r = await _fotos.GuardarAsync(id, archivo, _sesion.Cuil);
            if (r.Ok) cargadas++;
            else if (r.Error is not null) errores.Add($"{archivo.FileName}: {r.Error}");
        }

        TempData["Mensaje"] = errores.Count == 0
            ? $"Se cargaron {cargadas} foto(s)."
            : $"Se cargaron {cargadas} foto(s). {string.Join(" ", errores)}";

        return RedirectToPage("Fotos", new { id });
    }

    public async Task<IActionResult> OnPostPrincipalAsync(int id, int idFoto)
    {
        if (!await _db.Inventario.AnyAsync(i => i.Id == id)) return NotFound();

        await _fotos.MarcarPrincipalAsync(id, idFoto);
        return RedirectToPage("Fotos", new { id });
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id, int idFoto)
    {
        if (!await _db.Inventario.AnyAsync(i => i.Id == id)) return NotFound();

        await _fotos.EliminarAsync(idFoto);
        TempData["Mensaje"] = "La foto se dio de baja.";
        return RedirectToPage("Fotos", new { id });
    }

    private async Task<bool> CargarAsync(int id)
    {
        var bien = await _db.Inventario.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
        if (bien is null) return false;

        Bien = bien;
        Galeria = await _db.InventarioFotos.AsNoTracking()
            .Where(f => f.InventarioId == id && f.Estado == 0)
            .OrderByDescending(f => f.Principal).ThenBy(f => f.Orden)
            .ToListAsync();
        return true;
    }
}
