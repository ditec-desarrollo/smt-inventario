using InventarioSmt.Data;
using InventarioSmt.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace InventarioSmt.Pages.Reportes;

/// <summary>
/// Selección de bienes para imprimir etiquetas QR (con /Inventario/Etiqueta) y el índice de control.
/// Modo 1: repartición, departamentos y texto; modo 2: rango de ID. El filtro global de
/// AppDbContext ya limita los bienes a las reparticiones habilitadas del usuario.
/// </summary>
public class EtiquetasModel : PageModel
{
    public const int MaximoFilas = 3000;
    private static readonly string[] EncabezadosIndice = { "Identificación", "Equipamiento", "Fecha de alta" };

    private readonly AppDbContext _db;
    private readonly SesionUsuario _sesion;
    private readonly PdfService _pdf;

    public EtiquetasModel(AppDbContext db, SesionUsuario sesion, PdfService pdf)
    {
        _db = db;
        _sesion = sesion;
        _pdf = pdf;
    }

    /// <summary>1 = por repartición y departamentos; 2 = por rango de ID.</summary>
    [BindProperty(SupportsGet = true)]
    public int Modo { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int? Rep { get; set; }

    /// <summary>Departamentos elegidos. Vacío = todos los de la repartición.</summary>
    [BindProperty(SupportsGet = true)]
    public List<uint> D { get; set; } = new();

    /// <summary>Texto a buscar en equipamiento o características.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Texto { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Desde { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Hasta { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Titulo { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Buscar { get; set; }

    public record Fila(int Id, string? Identificacion, string? Equipamiento, int RepCodigo, string Reparticion,
        int DeptoCodigo, string Departamento, DateTime? FechaAlta, string? Estado);

    public record Item(string Valor, string Texto, string Padre = "", string Nombre = "");

    public List<Item> Reparticiones { get; private set; } = new();
    public List<Item> Departamentos { get; private set; } = new();
    public List<Fila> Filas { get; private set; } = new();
    public bool Recortado { get; private set; }
    public string? Error { get; private set; }

    public async Task OnGetAsync()
    {
        await CargarFiltrosAsync();
        if (Modo != 2) Modo = 1;
        if (string.IsNullOrWhiteSpace(Titulo)) Titulo = TituloPorDefecto();
        if (!Buscar) return;

        Error = Validar();
        if (Error is not null) return;

        var consulta = Consulta().OrderBy(i => i.Id);
        Filas = await LeerAsync(consulta.Take(MaximoFilas + 1));
        Recortado = Filas.Count > MaximoFilas;
        if (Recortado) Filas = Filas.Take(MaximoFilas).ToList();
        Filas = Modo == 2 ? Filas.OrderBy(f => f.Id).ToList() : PorUbicacion(Filas);
    }

    /// <summary>Índice de control de los bienes marcados, en PDF, con una página por departamento.</summary>
    public async Task<IActionResult> OnPostIndiceAsync(string? ids)
    {
        await CargarFiltrosAsync();
        var numeros = (ids ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => int.TryParse(t, out var n) ? n : 0)
            .Where(n => n > 0).Distinct().ToList();
        if (numeros.Count == 0) return BadRequest("No se marcó ningún bien.");

        var filas = PorUbicacion(await LeerAsync(_db.Inventario.AsNoTracking().Where(i => numeros.Contains(i.Id))));
        var grupos = filas
            .GroupBy(f => new { f.RepCodigo, f.DeptoCodigo, f.Reparticion, f.Departamento })
            .Select(g => new PdfService.GrupoIndice(g.Key.Reparticion, g.Key.Departamento,
                g.Select(f => (IReadOnlyList<string>)new[]
                {
                    FormatoHelper.Identificacion(f.Identificacion) ?? string.Empty,
                    f.Equipamiento ?? string.Empty,
                    f.FechaAlta?.ToString("dd/MM/yyyy") ?? string.Empty
                }).ToList()))
            .ToList();

        var titulo = string.IsNullOrWhiteSpace(Titulo) ? "Índice de bienes" : Titulo.Trim();
        var pdf = _pdf.IndicePorGrupo(titulo, EncabezadosIndice, grupos);
        return File(pdf, "application/pdf", $"indice_bienes_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
    }

    public string TituloPorDefecto()
    {
        if (Modo == 2)
            return Desde.HasValue || Hasta.HasValue
                ? $"Reporte Bienes ID {Desde?.ToString() ?? "..."} a {Hasta?.ToString() ?? "..."}"
                : "Reporte Bienes";

        var nombre = Reparticiones.FirstOrDefault(r => r.Valor == Rep?.ToString())?.Nombre;
        return nombre is null ? "Reporte Bienes Reparticion" : $"Reporte Bienes Reparticion: {nombre}";
    }

    private string? Validar()
    {
        if (Modo == 1 && !Rep.HasValue) return "Elegí la repartición.";
        if (Modo == 2 && !Desde.HasValue && !Hasta.HasValue) return "Indicá el ID desde, el ID hasta o ambos.";
        if (Modo == 2 && Desde > Hasta) return "El ID desde no puede ser mayor que el ID hasta.";
        return null;
    }

    private IQueryable<Models.Inventario> Consulta()
    {
        var q = _db.Inventario.AsNoTracking().AsQueryable();

        if (Modo == 2)
        {
            if (Desde.HasValue) q = q.Where(i => i.Id >= Desde);
            if (Hasta.HasValue) q = q.Where(i => i.Id <= Hasta);
            return q;
        }

        // inventario.reparticion es texto y los registros heredados traen decimales.
        var textos = new[] { $"{Rep}", $"{Rep}.0", $"{Rep}.00" };
        q = q.Where(i => textos.Contains(i.Reparticion!));

        var deptos = D.Select(d => (uint?)d).Distinct().ToList();
        if (deptos.Count > 0) q = q.Where(i => deptos.Contains(i.IdDepartamento));

        foreach (var palabra in (Texto ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            q = q.Where(i => i.Equipamiento!.Contains(palabra) || i.Caracteristicas!.Contains(palabra));

        return q;
    }

    private static List<Fila> PorUbicacion(IEnumerable<Fila> filas) =>
        filas.OrderBy(f => f.RepCodigo).ThenBy(f => f.DeptoCodigo).ThenBy(f => f.Departamento).ThenBy(f => f.Id).ToList();

    private async Task<List<Fila>> LeerAsync(IQueryable<Models.Inventario> consulta)
    {
        var datos = await (
                from i in consulta
                join d in _db.Departamentos on i.IdDepartamento equals (uint?)d.Id into dj
                from d in dj.DefaultIfEmpty()
                select new
                {
                    i.Id, i.Identificacion, i.Equipamiento, i.Reparticion,
                    DeptoCodigo = d == null ? (int?)null : d.Codigo,
                    Departamento = d == null ? null : d.Detalle,
                    i.FechaAlta,
                    Estado = i.EstadoNav == null ? null : i.EstadoNav.Detalle
                })
            .ToListAsync();

        var nombres = Reparticiones.ToDictionary(r => r.Valor, r => r.Texto);
        return datos
            .Select(x =>
            {
                var codigo = FormatoHelper.SinDecimales(x.Reparticion) ?? string.Empty;
                return new Fila(x.Id, x.Identificacion, x.Equipamiento,
                    int.TryParse(codigo, out var n) ? n : int.MaxValue,
                    nombres.TryGetValue(codigo, out var texto) ? texto : codigo,
                    x.DeptoCodigo ?? int.MaxValue,
                    x.DeptoCodigo.HasValue ? $"{x.DeptoCodigo} - {x.Departamento}" : "Sin departamento",
                    x.FechaAlta, x.Estado);
            })
            .ToList();
    }

    private async Task CargarFiltrosAsync()
    {
        var reps = await _db.Reparticiones.AsNoTracking()
            .Where(r => _sesion.Reparticiones == null || _sesion.Reparticiones.Contains(r.Codigo))
            .OrderBy(r => r.Codigo).ToListAsync();
        Reparticiones = reps.Select(r => new Item(r.Codigo.ToString(), $"{r.Codigo} - {r.Nombre}", Nombre: r.Nombre)).ToList();

        var codigos = reps.Select(r => r.Codigo).ToList();
        Departamentos = (await _db.Departamentos.AsNoTracking()
                .Where(d => codigos.Contains(d.Reparticion))
                .OrderBy(d => d.Reparticion).ThenBy(d => d.Codigo).ToListAsync())
            .Select(d => new Item(d.Id.ToString(), $"{d.Codigo} - {d.Detalle}", d.Reparticion.ToString()))
            .ToList();
    }
}
