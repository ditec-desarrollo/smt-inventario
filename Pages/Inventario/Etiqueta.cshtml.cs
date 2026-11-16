using System.Text;
using InventarioSmt.Data;
using InventarioSmt.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace InventarioSmt.Pages.Inventario;

public class EtiquetaModel : PageModel
{
    /// <summary>Cada avance del medio imprime una fila de 2 etiquetas troqueladas.</summary>
    private const int BienesPorEtiqueta = 2;

    private const int Columnas = 2;

    /// <summary>Ancho de la hoja en puntos: 104 mm a 203 dpi.</summary>
    private const int AnchoHoja = 832;

    /// <summary>Alto del bloque en puntos: el paso real del medio en la ZD421 (31,25 mm a 203 dpi).</summary>
    private const int AltoHoja = 250;

    private const int AnchoColumna = 416;
    private const int AltoFila = 203;

    private readonly AppDbContext _db;
    private readonly QrService _qr;

    public EtiquetaModel(AppDbContext db, QrService qr)
    {
        _db = db;
        _qr = qr;
    }

    public record BienEtiqueta(int Id, string Numero);

    /// <summary>Bienes agrupados según el troquel: cada grupo es una hoja de etiquetas.</summary>
    public List<List<BienEtiqueta>> Etiquetas { get; private set; } = new();

    /// <param name="id">Bien único (ruta), usado desde la pantalla de edición.</param>
    /// <param name="ids">Lista de bienes separados por coma, usada desde el listado.</param>
    public async Task<IActionResult> OnGetAsync(int? id, string? ids)
    {
        var numeros = LeerNumeros(id, ids);
        if (numeros.Count == 0) return NotFound();

        var bienes = await _db.Inventario
            .Where(i => numeros.Contains(i.Id))
            .Select(i => new { i.Id, i.Identificacion })
            .ToListAsync();

        if (bienes.Count == 0) return NotFound();

        // Se respeta el orden en que llegaron los identificadores.
        var ordenados = numeros
            .Select(n => bienes.FirstOrDefault(b => b.Id == n))
            .Where(b => b is not null)
            .Select(b => new BienEtiqueta(b!.Id, NumeroEtiqueta(b.Id, b.Identificacion)))
            .ToList();

        Etiquetas = ordenados
            .Select((bien, indice) => new { bien, indice })
            .GroupBy(x => x.indice / BienesPorEtiqueta)
            .Select(g => g.Select(x => x.bien).ToList())
            .ToList();

        return Page();
    }

    /// <summary>Lote grande enviado por formulario (reporte de etiquetas), para no exceder el largo de la URL.</summary>
    public Task<IActionResult> OnPostAsync(string? ids) => OnGetAsync(null, ids);

    public Task<IActionResult> OnPostZplAsync(string? ids) => OnGetZplAsync(ids);

    private static List<int> LeerNumeros(int? id, string? ids)
    {
        var numeros = new List<int>();

        if (id.HasValue) numeros.Add(id.Value);

        if (!string.IsNullOrWhiteSpace(ids))
        {
            numeros.AddRange(ids
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => int.TryParse(t, out var n) ? n : 0)
                .Where(n => n > 0));
        }

        return numeros.Distinct().ToList();
    }

    /// <summary>Número impreso al lado del QR: 8 dígitos con ceros a la izquierda.</summary>
    private static string NumeroEtiqueta(int id, string? identificacion)
    {
        var texto = FormatoHelper.SinDecimales(identificacion);
        var digitos = new string(texto.Where(char.IsDigit).ToArray());

        if (digitos.Length == 0) digitos = id.ToString();

        return digitos.PadLeft(8, '0');
    }

    /// <summary>Devuelve el PNG del código QR de un bien.</summary>
    public async Task<IActionResult> OnGetQrAsync(int id)
    {
        var contenido = await ContenidoQrAsync(id);
        if (contenido is null) return NotFound();

        return File(_qr.GenerarPngEtiqueta(contenido), "image/png");
    }

    /// <summary>
    /// Etiquetas en ZPL para mandar crudo a la Zebra, sin pasar por el driver gráfico
    /// de Windows. Cada bloque ^XA es una hoja de 104 x 76,2 mm con 6 etiquetas, que es
    /// el paso que avanza la impresora entre espacios de troquel.
    /// </summary>
    public async Task<IActionResult> OnGetZplAsync(string? ids)
    {
        var numeros = LeerNumeros(null, ids);
        if (numeros.Count == 0) return NotFound();

        var bienes = await _db.Inventario
            .Where(i => numeros.Contains(i.Id))
            .Select(i => new { i.Id, i.Identificacion })
            .ToListAsync();

        var ordenados = numeros
            .Select(n => bienes.FirstOrDefault(b => b.Id == n))
            .Where(b => b is not null)
            .Select(b => new BienEtiqueta(b!.Id, NumeroEtiqueta(b.Id, b.Identificacion)))
            .ToList();

        if (ordenados.Count == 0) return NotFound();

        var zpl = new StringBuilder();

        for (var i = 0; i < ordenados.Count; i += BienesPorEtiqueta)
        {
            zpl.Append($"^XA^CI28^PW{AnchoHoja}^LL{AltoHoja}^LH0,0\n");

            for (var posicion = 0; posicion < BienesPorEtiqueta && i + posicion < ordenados.Count; posicion++)
            {
                var bien = ordenados[i + posicion];
                var contenido = await ContenidoQrAsync(bien.Id) ?? string.Empty;
                var x = posicion % Columnas * AnchoColumna;
                var y = posicion / Columnas * AltoFila;

                // QR nativo de la impresora: magnificación 4 => módulo de 4 puntos (0,5 mm).
                zpl.Append($"^FO{x + 16},{y + 12}^BQN,2,4,L^FDLA,{contenido}^FS\n");
                zpl.Append($"^FO{x + 190},{y + 80}^A0N,36,36^FD{bien.Numero}^FS\n");
            }

            zpl.Append("^XZ\n");
        }

        return File(Encoding.UTF8.GetBytes(zpl.ToString()), "application/octet-stream", "etiquetas.zpl");
    }

    private async Task<string?> ContenidoQrAsync(int id)
    {
        var bien = await _db.Inventario
            .Where(i => i.Id == id)
            .Select(i => new
            {
                i.Id,
                i.Identificacion,
                i.Equipamiento,
                i.Marca,
                i.Modelo,
                i.NroSerie,
                i.Caracteristicas,
                i.Reparticion,
                i.Oficina
            })
            .FirstOrDefaultAsync();

        if (bien is null) return null;

        var numero = FormatoHelper.SinDecimales(bien.Identificacion);
        if (string.IsNullOrWhiteSpace(numero)) numero = bien.Id.ToString();

        return QrService.ContenidoBien(
            numero,
            bien.Equipamiento,
            bien.Marca,
            bien.Modelo,
            bien.NroSerie,
            bien.Caracteristicas,
            FormatoHelper.SinDecimales(bien.Reparticion),
            bien.Oficina?.ToString());
    }
}
