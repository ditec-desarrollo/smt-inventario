using InventarioSmt.Servicios.Catalogos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventarioSmt.Pages.Catalogos;

public class EditarModel : PageModel
{
    private readonly CatalogoService _catalogos;

    public EditarModel(CatalogoService catalogos) => _catalogos = catalogos;

    [BindProperty(SupportsGet = true)]
    public string Catalogo { get; set; } = string.Empty;

    /// <summary>Valor de la clave primaria cuando se edita; vacío en el alta.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Clave { get; set; }

    /// <summary>
    /// Ruta jerárquica con la que se venía navegando el listado: el alta la toma como
    /// dependencia inicial y el botón de volver la conserva.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "n")]
    public List<string?> Niveles { get; set; } = new();

    [BindProperty]
    public Dictionary<string, string?> Valores { get; set; } = new();

    public CatalogoDefinicion Definicion { get; private set; } = default!;
    public Dictionary<string, List<(string Valor, string Texto)>> Opciones { get; private set; } = new();

    /// <summary>Niveles que llevan al campo de dependencia, del más alto al propio campo.</summary>
    public IReadOnlyList<string> Cadena { get; private set; } = Array.Empty<string>();

    /// <summary>Opciones de cada nivel con su padre, para la cascada del formulario.</summary>
    public Dictionary<string, List<CatalogoService.Opcion>> Arbol { get; private set; } = new();

    /// <summary>Valor elegido en cada nivel de la cadena.</summary>
    public List<string?> Camino { get; private set; } = new();

    public string? Error { get; private set; }

    public bool EsAlta => string.IsNullOrWhiteSpace(Clave);

    public async Task<IActionResult> OnGetAsync()
    {
        var def = Servicios.Catalogos.Catalogo.Buscar(Catalogo);
        if (def is null) return NotFound();
        Definicion = def;

        if (!EsAlta)
        {
            var fila = await _catalogos.ObtenerAsync(def, Clave!, HttpContext.RequestAborted);
            if (fila is null) return NotFound();

            foreach (var campo in def.Campos)
            {
                fila.TryGetValue(campo.Columna, out var valor);
                Valores[campo.Columna] = CatalogoService.ValorCrudo(valor);
            }
        }
        else
        {
            foreach (var campo in def.Campos)
            {
                Valores[campo.Columna] = campo.Tipo == TipoCampo.Estado ? "0" : string.Empty;
            }

            // Solo el último nivel de la ruta corresponde al campo de dependencia.
            if (def.CampoFiltro is { } filtro)
            {
                var ultimo = Servicios.Catalogos.Catalogo.Cadena(filtro.Lookup!).Count - 1;
                if (ultimo < Niveles.Count && !string.IsNullOrWhiteSpace(Niveles[ultimo]))
                {
                    Valores[filtro.Columna] = Niveles[ultimo]!.Trim();
                }
            }
        }

        await CargarOpcionesAsync(def);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var def = Servicios.Catalogos.Catalogo.Buscar(Catalogo);
        if (def is null) return NotFound();
        Definicion = def;

        foreach (var campo in def.Editables)
        {
            var valor = (Valores.TryGetValue(campo.Columna, out var v) ? v : null)?.Trim();

            if (campo.Requerido && string.IsNullOrWhiteSpace(valor))
            {
                ModelState.AddModelError($"Valores[{campo.Columna}]", $"{campo.Etiqueta} es obligatorio.");
            }

            if (campo.Tipo is TipoCampo.Entero or TipoCampo.Estado
                && !string.IsNullOrWhiteSpace(valor) && !long.TryParse(valor, out _))
            {
                ModelState.AddModelError($"Valores[{campo.Columna}]", $"{campo.Etiqueta} debe ser un número.");
            }
        }

        if (!ModelState.IsValid)
        {
            await CargarOpcionesAsync(def);
            return Page();
        }

        var error = await _catalogos.GuardarAsync(def, Valores, EsAlta, Clave, HttpContext.RequestAborted);
        if (error is not null)
        {
            Error = error;
            await CargarOpcionesAsync(def);
            return Page();
        }

        TempData["Mensaje"] = EsAlta ? "El registro se agregó." : "El registro se actualizó.";
        return RedirectToPage("Lista", Ruta());
    }

    /// <summary>Parámetros que devuelven al listado con la misma ruta jerárquica.</summary>
    public Dictionary<string, string> Ruta()
    {
        var ruta = new Dictionary<string, string> { ["catalogo"] = Catalogo };

        for (var i = 0; i < Niveles.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(Niveles[i])) ruta[$"n[{i}]"] = Niveles[i]!.Trim();
        }

        return ruta;
    }

    private async Task CargarOpcionesAsync(CatalogoDefinicion def)
    {
        foreach (var nombre in def.Campos.Where(c => c.Lookup is not null)
                     .Select(c => c.Lookup!).Distinct())
        {
            Opciones[nombre] = await _catalogos.OpcionesAsync(nombre, HttpContext.RequestAborted);
        }

        // El campo de dependencia se muestra como cascada de niveles (repartición → ... → padre).
        if (def.CampoFiltro is not { } filtro) return;

        Cadena = Servicios.Catalogos.Catalogo.Cadena(filtro.Lookup!);
        if (Cadena.Count < 2) return;

        foreach (var nivel in Cadena)
        {
            Arbol[nivel] = await _catalogos.OpcionesArbolAsync(nivel, HttpContext.RequestAborted);
        }

        var actual = Valores.TryGetValue(filtro.Columna, out var v) ? v : null;
        Camino = await _catalogos.CaminoAsync(filtro.Lookup!, actual, HttpContext.RequestAborted);
    }
}
