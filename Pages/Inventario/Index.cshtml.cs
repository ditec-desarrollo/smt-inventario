using InventarioSmt.Data;
using InventarioSmt.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InventarioSmt.Pages.Inventario;

public class IndexModel : PageModel
{
    private const int TamanoPagina = 20;
    private readonly AppDbContext _db;

    private readonly SesionUsuario _sesion;

    public IndexModel(AppDbContext db, SesionUsuario sesion)
    {
        _db = db;
        _sesion = sesion;
    }

    [BindProperty(SupportsGet = true)]
    public string? Buscar { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Estado { get; set; }

    /// <summary>Código de repartición elegido en el filtro; la lista se muestra por nombre.</summary>
    [BindProperty(SupportsGet = true)]
    public int? Reparticion { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Marca { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Pagina { get; set; } = 1;

    public int TotalPaginas { get; private set; }
    public int TotalRegistros { get; private set; }
    public List<Fila> Bienes { get; private set; } = new();
    public List<Models.Estado> Estados { get; private set; } = new();
    public List<SelectListItem> Reparticiones { get; private set; } = new();
    public List<string> Marcas { get; private set; } = new();

    public record Fila(int Id, string? Identificacion, string? Equipamiento, string? Marca, string? Modelo,
        string? NroSerie, string? Reparticion, string? Ubicacion, decimal ValorInicial, string? Estado,
        int? EstadoCodigo, string? Miniatura, int CantidadFotos);

    public async Task OnGetAsync()
    {
        Estados = await _db.Estados.OrderBy(e => e.Detalle).ToListAsync();

        var reparticiones = await _db.Reparticiones
            .AsNoTracking()
            .OrderBy(r => r.Nombre)
            .Select(r => new { r.Codigo, r.Nombre })
            .ToListAsync();

        Reparticiones = reparticiones
            .Where(r => _sesion.Reparticiones is null || _sesion.Reparticiones.Contains(r.Codigo))
            .Select(r => new SelectListItem($"{r.Nombre} ({r.Codigo})", r.Codigo.ToString()))
            .ToList();

        var nombrePorCodigo = reparticiones
            .GroupBy(r => r.Codigo)
            .ToDictionary(g => g.Key, g => g.First().Nombre);

        Marcas = await _db.Inventario
            .AsNoTracking()
            .Where(i => i.Marca != null && i.Marca != "")
            .Select(i => i.Marca!)
            .Distinct()
            .OrderBy(m => m)
            .Take(500)
            .ToListAsync();

        var consulta = _db.Inventario.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(Buscar))
        {
            var t = Buscar.Trim();
            consulta = consulta.Where(i =>
                (i.Identificacion != null && i.Identificacion.Contains(t)) ||
                (i.Equipamiento != null && i.Equipamiento.Contains(t)) ||
                (i.Marca != null && i.Marca.Contains(t)) ||
                (i.Modelo != null && i.Modelo.Contains(t)) ||
                (i.NroSerie != null && i.NroSerie.Contains(t)) ||
                i.UbicacionFisica.Contains(t));
        }

        if (Estado.HasValue) consulta = consulta.Where(i => i.Estado == Estado);

        if (Reparticion.HasValue)
        {
            // La repartición del inventario es texto y los registros heredados traen
            // decimales ("313.0"), así que se aceptan las dos formas del código.
            var codigo = Reparticion.Value.ToString();
            var conPunto = codigo + ".";
            consulta = consulta.Where(i =>
                i.Reparticion != null && (i.Reparticion == codigo || i.Reparticion.StartsWith(conPunto)));
        }

        if (!string.IsNullOrWhiteSpace(Marca))
        {
            var marca = Marca.Trim();
            consulta = consulta.Where(i => i.Marca != null && i.Marca.Contains(marca));
        }

        TotalRegistros = await consulta.CountAsync();
        TotalPaginas = Math.Max(1, (int)Math.Ceiling(TotalRegistros / (double)TamanoPagina));
        Pagina = Math.Clamp(Pagina, 1, TotalPaginas);

        Bienes = await consulta
            .OrderByDescending(i => i.Id)
            .Skip((Pagina - 1) * TamanoPagina)
            .Take(TamanoPagina)
            .Select(i => new Fila(
                i.Id, i.Identificacion, i.Equipamiento, i.Marca, i.Modelo, i.NroSerie,
                i.Reparticion, i.UbicacionFisica, i.ValorInicial,
                i.EstadoNav != null ? i.EstadoNav.Detalle : null,
                i.Estado,
                i.Fotos.Where(f => f.Estado == 0).OrderByDescending(f => f.Principal)
                    .Select(f => f.ArchivoThumb ?? f.Archivo).FirstOrDefault(),
                i.Fotos.Count(f => f.Estado == 0)))
            .ToListAsync();

        Bienes = Bienes
            .Select(b => b with { Reparticion = NombreReparticion(b.Reparticion, nombrePorCodigo) })
            .ToList();
    }

    /// <summary>Muestra el nombre de la repartición; si el código no está en el catálogo deja el código.</summary>
    private static string NombreReparticion(string? valor, IReadOnlyDictionary<int, string> nombres)
    {
        var codigo = FormatoHelper.SinDecimales(valor);
        if (codigo.Length == 0) return string.Empty;

        return int.TryParse(codigo, out var numero) && nombres.TryGetValue(numero, out var nombre)
            ? nombre
            : codigo;
    }
}
