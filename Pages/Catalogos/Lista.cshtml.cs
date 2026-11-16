using InventarioSmt.Servicios;
using InventarioSmt.Servicios.Catalogos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventarioSmt.Pages.Catalogos;

public class ListaModel : PageModel
{
    private const int TamanoPagina = 25;

    private readonly CatalogoService _catalogos;
    private readonly PdfService _pdf;

    public ListaModel(CatalogoService catalogos, PdfService pdf)
    {
        _catalogos = catalogos;
        _pdf = pdf;
    }

    [BindProperty(SupportsGet = true)]
    public string Catalogo { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? Buscar { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Pagina { get; set; } = 1;

    /// <summary>
    /// Valor elegido en cada nivel de la jerarquía, del más alto al más bajo
    /// (repartición, departamento, sección, subsección).
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "n")]
    public List<string?> Niveles { get; set; } = new();

    public CatalogoDefinicion Definicion { get; private set; } = default!;
    public List<Dictionary<string, object?>> Filas { get; private set; } = new();
    public List<CatalogoService.Nivel> Jerarquia { get; private set; } = new();
    public Dictionary<string, Dictionary<string, string>> Lookups { get; private set; } = new();
    public int Total { get; private set; }
    public int TotalPaginas { get; private set; }
    public string? Mensaje { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var def = Servicios.Catalogos.Catalogo.Buscar(Catalogo);
        if (def is null) return NotFound();

        Definicion = def;
        Mensaje = TempData["Mensaje"] as string;

        await CargarJerarquiaAsync(def);

        var resultado = await _catalogos.ListarAsync(def, Buscar, Math.Max(1, Pagina), TamanoPagina,
            jerarquia: Seleccion, ct: HttpContext.RequestAborted);

        Total = resultado.Total;
        TotalPaginas = Math.Max(1, (int)Math.Ceiling(Total / (double)TamanoPagina));
        Pagina = Math.Clamp(Pagina, 1, TotalPaginas);
        Filas = resultado.Filas;
        Lookups = await CargarLookupsAsync(def);

        return Page();
    }

    public async Task<IActionResult> OnGetPdfAsync()
    {
        var def = Servicios.Catalogos.Catalogo.Buscar(Catalogo);
        if (def is null) return NotFound();

        Definicion = def;
        await CargarJerarquiaAsync(def);

        var resultado = await _catalogos.ListarAsync(def, Buscar, 1, int.MaxValue, todas: true,
            jerarquia: Seleccion, ct: HttpContext.RequestAborted);
        var lookups = await CargarLookupsAsync(def);

        var columnas = def.Columnas.ToList();
        var encabezados = columnas.Select(c => c.Etiqueta).ToList();
        var filas = resultado.Filas
            .Select(f => (IReadOnlyList<string>)columnas.Select(c => Texto(c, f, lookups)).ToList())
            .ToList();

        var pdf = _pdf.Tabla($"Catálogo de {def.Titulo}", Subtitulo(), encabezados, filas);
        return File(pdf, "application/pdf", $"catalogo_{def.Clave}.pdf");
    }

    public async Task<IActionResult> OnPostBajaAsync(string clave, bool reactivar)
    {
        var def = Servicios.Catalogos.Catalogo.Buscar(Catalogo);
        if (def is null) return NotFound();

        Definicion = def;
        await CargarJerarquiaAsync(def);

        var error = await _catalogos.BajaAsync(def, clave, reactivar, HttpContext.RequestAborted);

        TempData["Mensaje"] = error ?? (def.CampoEstado is null
            ? "El registro se eliminó."
            : reactivar ? "El registro se reactivó." : "El registro se dio de baja.");

        return RedirectToPage(Ruta(Pagina));
    }

    /// <summary>Valores válidos de cada nivel, ya descartados los que no corresponden al padre.</summary>
    public List<string?> Seleccion => Jerarquia.Select(n => n.Seleccionado).ToList();

    /// <summary>Valor del nivel más bajo elegido, con el que se precarga un alta.</summary>
    public string? Padre => Jerarquia.LastOrDefault(n => n.Seleccionado is not null)?.Seleccionado;

    /// <summary>
    /// Parámetros de ruta que conservan catálogo, búsqueda y toda la ruta jerárquica al navegar.
    /// </summary>
    public Dictionary<string, string> Ruta(int? pagina = null)
    {
        var ruta = new Dictionary<string, string> { ["catalogo"] = Catalogo };

        if (!string.IsNullOrWhiteSpace(Buscar)) ruta["Buscar"] = Buscar.Trim();

        if (pagina is not null) ruta["Pagina"] = pagina.Value.ToString();

        var seleccion = Seleccion;
        for (var i = 0; i < seleccion.Count; i++)
        {
            if (seleccion[i] is { } valor) ruta[$"n[{i}]"] = valor;
        }

        return ruta;
    }

    /// <summary>Deja constancia en el PDF de los filtros con los que se emitió.</summary>
    private string Subtitulo()
    {
        var filtros = Jerarquia
            .Where(n => n.Seleccionado is not null)
            .Select(n =>
            {
                var opcion = n.Opciones.FirstOrDefault(o => o.Valor == n.Seleccionado);
                var texto = string.IsNullOrEmpty(opcion.Texto) ? n.Seleccionado : opcion.Texto;
                return $"{n.Etiqueta}: {texto}";
            })
            .ToList();

        if (!string.IsNullOrWhiteSpace(Buscar)) filtros.Add($"Búsqueda: {Buscar.Trim()}");

        return filtros.Count == 0 ? "Listado completo" : string.Join(" | ", filtros);
    }

    private async Task CargarJerarquiaAsync(CatalogoDefinicion def)
    {
        if (def.CampoFiltro is not { } filtro) return;

        Jerarquia = await _catalogos.NivelesAsync(filtro.Lookup!, Niveles,
            HttpContext.RequestAborted);
    }

    public string Texto(CampoCatalogo campo, IDictionary<string, object?> fila) =>
        Texto(campo, fila, Lookups);

    private static string Texto(CampoCatalogo campo, IDictionary<string, object?> fila,
        IReadOnlyDictionary<string, Dictionary<string, string>> lookups)
    {
        fila.TryGetValue(campo.Columna, out var valor);

        if (campo.Lookup is not null && lookups.TryGetValue(campo.Lookup, out var dic))
        {
            var crudo = CatalogoService.ValorCrudo(valor);
            if (crudo.Length > 0 && dic.TryGetValue(crudo, out var nombre)) return nombre;
        }

        return CatalogoService.Mostrar(campo, valor);
    }

    public bool EstaInactivo(IDictionary<string, object?> fila)
    {
        var estado = Definicion.CampoEstado;
        if (estado is null) return false;

        fila.TryGetValue(estado.Columna, out var valor);
        return CatalogoService.ValorCrudo(valor) is var texto && texto.Length > 0 && texto != "0";
    }

    public string Clave(IDictionary<string, object?> fila)
    {
        var pk = Definicion.Claves.First();
        fila.TryGetValue(pk.Columna, out var valor);
        return CatalogoService.ValorCrudo(valor);
    }

    private async Task<Dictionary<string, Dictionary<string, string>>> CargarLookupsAsync(
        CatalogoDefinicion def)
    {
        var lookups = new Dictionary<string, Dictionary<string, string>>();

        foreach (var nombre in def.Campos.Where(c => c.Lookup is not null)
                     .Select(c => c.Lookup!).Distinct())
        {
            lookups[nombre] = await _catalogos.DiccionarioAsync(nombre, HttpContext.RequestAborted);
        }

        return lookups;
    }
}
