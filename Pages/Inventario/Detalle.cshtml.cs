using InventarioSmt.Data;
using InventarioSmt.Servicios;
using InventarioSmt.Servicios.Catalogos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace InventarioSmt.Pages.Inventario;

public class DetalleModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly CatalogoService _catalogos;

    public DetalleModel(AppDbContext db, CatalogoService catalogos)
    {
        _db = db;
        _catalogos = catalogos;
    }

    /// <summary>Depto / sección / subsección / oficina del bien, si está vinculado.</summary>
    /// <summary>Depto / sección / subsección / oficina del bien, hasta el nivel que tenga.</summary>
    public string? Ubicacion { get; private set; }

    public string? Nomenclador { get; private set; }


    public Models.Inventario Bien { get; private set; } = default!;
    public List<Models.InventarioFoto> Fotos { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var bien = await _db.Inventario
            .Include(i => i.EstadoNav)
            .Include(i => i.TipoInstNav)
            .Include(i => i.TipoCompNav)
            .Include(i => i.TipoAutorizaNav)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id);
        if (bien is null) return NotFound();

        Bien = bien;
        var n = await _db.Nomenclador.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Cuenta == bien.Cuenta && x.Subcuenta == bien.Subcuenta);
        Nomenclador = Models.Nomenclador.Formatear(bien.Cuenta, bien.Subcuenta, n?.Descripcion);
        Ubicacion = await _catalogos.RutaTextoAsync(bien);
        Fotos = await _db.InventarioFotos.AsNoTracking()
            .Where(f => f.InventarioId == id && f.Estado == 0)
            .OrderByDescending(f => f.Principal).ThenBy(f => f.Orden)
            .ToListAsync();

        return Page();
    }

    /// <summary>Baja lógica: registra la fecha de baja y pasa el estado a "Solicitud de Baja"; no borra el registro.</summary>
    public async Task<IActionResult> OnPostBajaAsync(int id)
    {
        var bien = await _db.Inventario.FirstOrDefaultAsync(i => i.Id == id);
        if (bien is null) return NotFound();

        bien.FechaBaja = DateTime.Today;
        bien.Estado = await EstadoHelper.CodigoSolicitudBajaAsync(_db) ?? bien.Estado;
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = "Se registró la baja del bien.";
        return RedirectToPage("Detalle", new { id });
    }
}
