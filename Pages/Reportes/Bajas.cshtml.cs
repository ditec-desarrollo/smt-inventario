using System.Globalization;
using ClosedXML.Excel;
using InventarioSmt.Data;
using InventarioSmt.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace InventarioSmt.Pages.Reportes;

/// <summary>
/// Bienes en estado "Solicitud de Baja" por repartición y rango de fecha de baja.
/// El PDF y el Excel salen solo con los marcados. El filtro global de AppDbContext
/// ya limita los bienes a las reparticiones habilitadas del usuario.
/// </summary>
public class BajasModel : PageModel
{
    public const int MaximoFilas = 3000;
    private static readonly string[] Encabezados =
        { "ID", "Equipamiento", "Repartición", "Departamento", "Fecha de alta", "Fecha de baja" };

    private readonly AppDbContext _db;
    private readonly SesionUsuario _sesion;
    private readonly PdfService _pdf;

    public BajasModel(AppDbContext db, SesionUsuario sesion, PdfService pdf)
    {
        _db = db;
        _sesion = sesion;
        _pdf = pdf;
    }

    [BindProperty(SupportsGet = true)]
    public int? Rep { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? Desde { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? Hasta { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Titulo { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Buscar { get; set; }

    public record Fila(int Id, string? Equipamiento, int RepCodigo, string Reparticion,
        int DeptoCodigo, string Departamento, DateTime? FechaAlta, DateTime? FechaBaja);

    public record Item(string Valor, string Texto, string Nombre);

    public List<Item> Reparticiones { get; private set; } = new();
    public List<Fila> Filas { get; private set; } = new();
    public bool Recortado { get; private set; }
    public string? Error { get; private set; }

    public async Task OnGetAsync()
    {
        await CargarReparticionesAsync();
        if (!Buscar && Desde is null && Hasta is null)
        {
            Desde = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            Hasta = DateTime.Today;
        }
        if (string.IsNullOrWhiteSpace(Titulo)) Titulo = TituloPorDefecto();
        if (!Buscar) return;

        if (Desde > Hasta)
        {
            Error = "La fecha de baja desde no puede ser mayor que la fecha hasta.";
            return;
        }

        var codigo = await EstadoHelper.CodigoSolicitudBajaAsync(_db);
        if (codigo is null)
        {
            Error = $"No existe el estado \"{EstadoHelper.SolicitudBaja}\" en la tabla estados.";
            return;
        }

        Filas = await LeerAsync(Consulta(codigo.Value).OrderBy(i => i.Id).Take(MaximoFilas + 1));
        Recortado = Filas.Count > MaximoFilas;
        if (Recortado) Filas = Filas.Take(MaximoFilas).ToList();
        Filas = Ordenar(Filas);
    }

    public async Task<IActionResult> OnPostPdfAsync(string? ids)
    {
        var filas = await MarcadosAsync(ids);
        if (filas is null) return BadRequest("No se marcó ningún bien.");

        var datos = filas.Select(f => (IReadOnlyList<string>)new[]
        {
            f.Id.ToString(), f.Equipamiento ?? string.Empty, f.Reparticion, f.Departamento,
            f.FechaAlta?.ToString("dd/MM/yyyy") ?? string.Empty, f.FechaBaja?.ToString("dd/MM/yyyy") ?? string.Empty
        }).ToList();

        var pdf = _pdf.Tabla(TituloFinal(), Subtitulo(), Encabezados, datos);
        return File(pdf, "application/pdf", $"solicitud_baja_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
    }

    public async Task<IActionResult> OnPostExcelAsync(string? ids)
    {
        var filas = await MarcadosAsync(ids);
        if (filas is null) return BadRequest("No se marcó ningún bien.");

        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Solicitud de baja");
        hoja.Cell(1, 1).Value = TituloFinal();
        hoja.Cell(1, 1).Style.Font.Bold = true;
        hoja.Cell(2, 1).Value = Subtitulo();

        for (var c = 0; c < Encabezados.Length; c++)
        {
            hoja.Cell(4, c + 1).Value = Encabezados[c];
            hoja.Cell(4, c + 1).Style.Font.Bold = true;
            hoja.Cell(4, c + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        var r = 5;
        foreach (var f in filas)
        {
            hoja.Cell(r, 1).Value = f.Id;
            hoja.Cell(r, 2).Value = f.Equipamiento;
            hoja.Cell(r, 3).Value = f.Reparticion;
            hoja.Cell(r, 4).Value = f.Departamento;
            if (f.FechaAlta.HasValue)
            {
                hoja.Cell(r, 5).Value = f.FechaAlta.Value;
                hoja.Cell(r, 5).Style.DateFormat.Format = "dd/mm/yyyy";
            }
            if (f.FechaBaja.HasValue)
            {
                hoja.Cell(r, 6).Value = f.FechaBaja.Value;
                hoja.Cell(r, 6).Style.DateFormat.Format = "dd/mm/yyyy";
            }
            r++;
        }

        hoja.SheetView.FreezeRows(4);
        hoja.Range(4, 1, Math.Max(4, r - 1), Encabezados.Length).SetAutoFilter();
        hoja.Columns().AdjustToContents(4, Math.Max(4, r - 1));

        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"solicitud_baja_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
    }

    public string TituloPorDefecto() =>
        $"Solicitud de Baja de Bienes entre {Fecha(Desde)} y {Fecha(Hasta)}";

    /// <summary>Segunda línea del título: el nombre de la repartición elegida.</summary>
    public string Subtitulo() =>
        Reparticiones.FirstOrDefault(r => r.Valor == Rep?.ToString())?.Nombre ?? "Todas las reparticiones habilitadas";

    private string TituloFinal() => string.IsNullOrWhiteSpace(Titulo) ? TituloPorDefecto() : Titulo.Trim();

    private static string Fecha(DateTime? f) =>
        f?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "...";

    private async Task<List<Fila>?> MarcadosAsync(string? ids)
    {
        await CargarReparticionesAsync();
        var numeros = (ids ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => int.TryParse(t, out var n) ? n : 0)
            .Where(n => n > 0).Distinct().ToList();
        if (numeros.Count == 0) return null;

        var codigo = await EstadoHelper.CodigoSolicitudBajaAsync(_db);
        if (codigo is null) return null;

        return Ordenar(await LeerAsync(_db.Inventario.AsNoTracking()
            .Where(i => numeros.Contains(i.Id) && i.Estado == codigo)));
    }

    private IQueryable<Models.Inventario> Consulta(int codigoEstado)
    {
        var q = _db.Inventario.AsNoTracking().Where(i => i.Estado == codigoEstado);

        if (Rep.HasValue)
        {
            // inventario.reparticion es texto y los registros heredados traen decimales.
            var textos = new[] { $"{Rep}", $"{Rep}.0", $"{Rep}.00" };
            q = q.Where(i => textos.Contains(i.Reparticion!));
        }

        if (Desde.HasValue)
        {
            var desde = Desde.Value.Date;
            q = q.Where(i => i.FechaBaja >= desde);
        }

        if (Hasta.HasValue)
        {
            var hasta = Hasta.Value.Date.AddDays(1);
            q = q.Where(i => i.FechaBaja < hasta);
        }

        return q;
    }

    private static List<Fila> Ordenar(IEnumerable<Fila> filas) =>
        filas.OrderBy(f => f.RepCodigo).ThenBy(f => f.DeptoCodigo).ThenBy(f => f.FechaBaja).ThenBy(f => f.Id).ToList();

    private async Task<List<Fila>> LeerAsync(IQueryable<Models.Inventario> consulta)
    {
        var datos = await (
                from i in consulta
                join d in _db.Departamentos on i.IdDepartamento equals (uint?)d.Id into dj
                from d in dj.DefaultIfEmpty()
                select new
                {
                    i.Id, i.Equipamiento, i.Reparticion,
                    DeptoCodigo = d == null ? (int?)null : d.Codigo,
                    Departamento = d == null ? null : d.Detalle,
                    i.FechaAlta, i.FechaBaja
                })
            .ToListAsync();

        var nombres = Reparticiones.ToDictionary(r => r.Valor, r => r.Texto);
        return datos
            .Select(x =>
            {
                var codigo = FormatoHelper.SinDecimales(x.Reparticion) ?? string.Empty;
                return new Fila(x.Id, x.Equipamiento,
                    int.TryParse(codigo, out var n) ? n : int.MaxValue,
                    nombres.TryGetValue(codigo, out var texto) ? texto : codigo,
                    x.DeptoCodigo ?? int.MaxValue,
                    x.Departamento ?? "Sin departamento",
                    x.FechaAlta, x.FechaBaja);
            })
            .ToList();
    }

    private async Task CargarReparticionesAsync()
    {
        var reps = await _db.Reparticiones.AsNoTracking()
            .Where(r => _sesion.Reparticiones == null || _sesion.Reparticiones.Contains(r.Codigo))
            .OrderBy(r => r.Codigo).ToListAsync();
        Reparticiones = reps.Select(r => new Item(r.Codigo.ToString(), $"{r.Codigo} - {r.Nombre}", r.Nombre)).ToList();
    }
}
