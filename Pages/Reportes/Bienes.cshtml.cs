using ClosedXML.Excel;
using InventarioSmt.Data;
using InventarioSmt.Models;
using InventarioSmt.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace InventarioSmt.Pages.Reportes;

/// <summary>
/// Reporte de bienes por repartición, departamento y nomenclador. El filtro global de
/// AppDbContext ya limita los bienes a las reparticiones habilitadas del usuario.
/// </summary>
public class BienesModel : PageModel
{
    private const int TamanoPagina = 50;
    private static readonly string[] Encabezados =
        { "Identificación", "Equipamiento", "Repartición", "Departamento", "Fecha de alta", "Estado" };

    private readonly AppDbContext _db;
    private readonly SesionUsuario _sesion;
    private readonly PdfService _pdf;

    public BienesModel(AppDbContext db, SesionUsuario sesion, PdfService pdf)
    {
        _db = db;
        _sesion = sesion;
        _pdf = pdf;
    }

    [BindProperty(SupportsGet = true)]
    public int? Rep { get; set; }

    [BindProperty(SupportsGet = true)]
    public uint? Depto { get; set; }

    /// <summary>Nomenclador elegido, como "cuenta-subcuenta".</summary>
    [BindProperty(SupportsGet = true)]
    public List<string> N { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int Pagina { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public bool Imprimir { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Buscar { get; set; }

    public record Fila(string? Identificacion, string? Equipamiento, string Reparticion,
        string? Departamento, DateTime? FechaAlta, string? Estado);

    public record Opcion(string Valor, string Texto, string Padre = "");

    public List<Opcion> Reparticiones { get; private set; } = new();
    public List<Opcion> Departamentos { get; private set; } = new();
    public List<Opcion> Nomenclador { get; private set; } = new();
    public List<Fila> Filas { get; private set; } = new();
    public int Total { get; private set; }
    public int TotalPaginas { get; private set; } = 1;

    public async Task OnGetAsync()
    {
        await CargarFiltrosAsync();
        if (!Buscar && !Imprimir) return;

        var consulta = Consulta();
        Total = await consulta.CountAsync();
        TotalPaginas = Math.Max(1, (int)Math.Ceiling(Total / (double)TamanoPagina));
        Pagina = Math.Clamp(Pagina, 1, TotalPaginas);

        var ordenada = Ordenar(consulta);
        Filas = Imprimir
            ? await LeerAsync(ordenada)
            : await LeerAsync(ordenada.Skip((Pagina - 1) * TamanoPagina).Take(TamanoPagina));
    }

    public async Task<IActionResult> OnGetExcelAsync()
    {
        await CargarFiltrosAsync();
        var filas = await LeerAsync(Ordenar(Consulta()));

        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Bienes");
        hoja.Cell(1, 1).Value = "Reporte de bienes";
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
            hoja.Cell(r, 1).Value = FormatoHelper.Identificacion(f.Identificacion);
            hoja.Cell(r, 2).Value = f.Equipamiento;
            hoja.Cell(r, 3).Value = f.Reparticion;
            hoja.Cell(r, 4).Value = f.Departamento;
            if (f.FechaAlta.HasValue)
            {
                hoja.Cell(r, 5).Value = f.FechaAlta.Value;
                hoja.Cell(r, 5).Style.DateFormat.Format = "dd/mm/yyyy";
            }
            hoja.Cell(r, 6).Value = f.Estado;
            r++;
        }

        hoja.SheetView.FreezeRows(4);
        hoja.Range(4, 1, Math.Max(4, r - 1), Encabezados.Length).SetAutoFilter();
        hoja.Columns().AdjustToContents(4, Math.Max(4, r - 1));

        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"reporte_bienes_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
    }

    public async Task<IActionResult> OnGetPdfAsync()
    {
        await CargarFiltrosAsync();
        var filas = (await LeerAsync(Ordenar(Consulta())))
            .Select(f => (IReadOnlyList<string>)new[]
            {
                FormatoHelper.Identificacion(f.Identificacion) ?? string.Empty, f.Equipamiento ?? string.Empty,
                f.Reparticion, f.Departamento ?? string.Empty,
                f.FechaAlta?.ToString("dd/MM/yyyy") ?? string.Empty, f.Estado ?? string.Empty
            })
            .ToList();

        var pdf = _pdf.Tabla("Reporte de bienes", Subtitulo(), Encabezados, filas);
        return File(pdf, "application/pdf", $"reporte_bienes_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
    }

    /// <summary>Texto con los filtros aplicados, para el encabezado de Excel, PDF e impresión.</summary>
    public string Subtitulo()
    {
        var rep = Rep.HasValue ? Reparticiones.FirstOrDefault(r => r.Valor == Rep.ToString())?.Texto : null;
        var depto = Depto.HasValue ? Departamentos.FirstOrDefault(d => d.Valor == Depto.ToString())?.Texto : null;
        var nom = N.Count == 0 ? "todo" : $"{N.Count} cuenta(s)";
        return $"Repartición: {rep ?? "todas las habilitadas"} · Departamento: {depto ?? "todos"} · Nomenclador: {nom}";
    }

    private IQueryable<Models.Inventario> Consulta()
    {
        var q = _db.Inventario.AsNoTracking().AsQueryable();

        if (Rep.HasValue)
        {
            // inventario.reparticion es texto y los registros heredados traen decimales.
            var textos = new[] { $"{Rep}", $"{Rep}.0", $"{Rep}.00" };
            q = q.Where(i => textos.Contains(i.Reparticion!));
        }

        if (Depto.HasValue) q = q.Where(i => i.IdDepartamento == Depto);

        var claves = N.Select(ClaveNomenclador).Where(c => c.HasValue).Select(c => c!.Value).Distinct().ToList();
        if (claves.Count > 0) q = q.Where(i => claves.Contains(i.Cuenta * 1000 + i.Subcuenta));

        return q;
    }

    /// <summary>
    /// Orden por repartición y departamento. La repartición es texto: ordenar primero por largo
    /// hace que "20" quede antes que "100" (orden numérico) sin convertir en la base.
    /// </summary>
    private static IQueryable<Models.Inventario> Ordenar(IQueryable<Models.Inventario> q) =>
        q.OrderBy(i => i.Reparticion!.Length).ThenBy(i => i.Reparticion)
         .ThenBy(i => i.IdDepartamento).ThenBy(i => i.Id);

    private async Task<List<Fila>> LeerAsync(IQueryable<Models.Inventario> consulta)
    {
        var datos = await (
                from i in consulta
                join d in _db.Departamentos on i.IdDepartamento equals (uint?)d.Id into dj
                from d in dj.DefaultIfEmpty()
                select new
                {
                    i.Identificacion, i.Equipamiento, i.Reparticion,
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
                return new Fila(x.Identificacion, x.Equipamiento,
                    nombres.TryGetValue(codigo, out var texto) ? texto : codigo, x.Departamento, x.FechaAlta, x.Estado);
            })
            .ToList();
    }

    private static int? ClaveNomenclador(string valor)
    {
        var partes = valor.Split('-');
        return partes.Length == 2 && int.TryParse(partes[0], out var c) && int.TryParse(partes[1], out var s)
            ? c * 1000 + s
            : null;
    }

    private async Task CargarFiltrosAsync()
    {
        var reps = await _db.Reparticiones.AsNoTracking()
            .Where(r => _sesion.Reparticiones == null || _sesion.Reparticiones.Contains(r.Codigo))
            .OrderBy(r => r.Codigo).ToListAsync();
        Reparticiones = reps.Select(r => new Opcion(r.Codigo.ToString(), $"{r.Codigo} - {r.Nombre}")).ToList();

        var codigos = reps.Select(r => r.Codigo).ToList();
        Departamentos = (await _db.Departamentos.AsNoTracking()
                .Where(d => codigos.Contains(d.Reparticion))
                .OrderBy(d => d.Reparticion).ThenBy(d => d.Codigo).ToListAsync())
            .Select(d => new Opcion(d.Id.ToString(), $"{d.Codigo} - {d.Detalle}", d.Reparticion.ToString()))
            .ToList();

        Nomenclador = (await _db.Nomenclador.AsNoTracking()
                .Where(n => n.Estado == 0)
                .OrderBy(n => n.Cuenta).ThenBy(n => n.Subcuenta).ToListAsync())
            .Select(n => new Opcion($"{n.Cuenta}-{n.Subcuenta}", Models.Nomenclador.Formatear(n.Cuenta, n.Subcuenta, n.Descripcion)))
            .ToList();
    }
}
