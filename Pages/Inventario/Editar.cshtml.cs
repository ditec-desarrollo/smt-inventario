using System.Text.Json;
using InventarioSmt.Data;
using InventarioSmt.Servicios;
using InventarioSmt.Servicios.Catalogos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InventarioSmt.Pages.Inventario;

public class EditarModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly FotoService _fotos;
    private readonly SesionUsuario _sesion;
    private readonly CatalogoService _catalogos;

    public EditarModel(AppDbContext db, FotoService fotos, SesionUsuario sesion,
        CatalogoService catalogos)
    {
        _db = db;
        _fotos = fotos;
        _sesion = sesion;
        _catalogos = catalogos;
    }

    [BindProperty]
    public Models.Inventario Bien { get; set; } = new();

    [BindProperty]
    public List<IFormFile> Fotos { get; set; } = new();

    public bool EsAlta => Bien.Id == 0;
    public SelectList Estados { get; private set; } = default!;
    public SelectList TiposInstrumento { get; private set; } = default!;
    public SelectList TiposComprobante { get; private set; } = default!;
    public SelectList TiposAutorizacion { get; private set; } = default!;

    /// <summary>Códigos de repartición para los combos; el valor es el código como texto.</summary>
    public List<SelectListItem> Reparticiones { get; private set; } = new();

    /// <summary>Todas las reparticiones, para origen y destino (el bien puede venir o ir a cualquiera).</summary>
    public List<SelectListItem> ReparticionesTodas { get; private set; } = new();

    public List<SelectListItem> TiposAdquisicion { get; private set; } = new();

    /// <summary>Opciones del nomenclador para el combo con búsqueda: [{ c, s, t }].</summary>
    public string NomencladorJson { get; private set; } = "[]";
    public string TextoNomenclador { get; private set; } = string.Empty;

    /// <summary>Niveles debajo de la repartición del bien, en el orden de la cascada.</summary>
    public static readonly string[] NivelesUbicacion = { "departamentos", "secciones", "subsecciones", "oficinas" };

    /// <summary>Opciones de cada nivel con su padre, para filtrar la cascada en el navegador.</summary>
    public Dictionary<string, List<CatalogoService.Opcion>> Arbol { get; } = new();

    /// <summary>Valores de repartición, depto, sección, subsección y oficina del bien.</summary>
    public List<string?> Camino { get; private set; } = new();

    public List<SelectListItem> Rendidos { get; } = new()
    {
        new SelectListItem("0 - No rendido", "0"),
        new SelectListItem("1 - Rendido", "1")
    };

    /// <summary>Nombre del empleado del legajo cargado; se muestra pero no se guarda.</summary>
    public string NombreLegajo { get; private set; } = string.Empty;

    /// <summary>Nombre del proveedor cargado; se muestra pero no se guarda.</summary>
    public string NombreProveedor { get; private set; } = string.Empty;

    public string? Aviso { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id.HasValue)
        {
            var bien = await _db.Inventario.FirstOrDefaultAsync(i => i.Id == id);
            if (bien is null) return NotFound();
            Bien = bien;
            Bien.Reparticion = FormatoHelper.SinDecimales(Bien.Reparticion);
        }
        else
        {
            Bien.FechaAlta = DateTime.Today;
            Bien.Cantidad = 1;
        }

        await CargarCatalogosAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Bien.Legajo != 0 && !await _db.Legajos.AnyAsync(l => l.Numero == Bien.Legajo))
        {
            ModelState.AddModelError(
                "Bien.Legajo",
                $"El legajo {Bien.Legajo} no existe en la tabla de legajos.");
        }

        if (Bien.Proveedor != 0 && !await _db.Proveedores.AnyAsync(p => p.Numero == Bien.Proveedor))
        {
            ModelState.AddModelError(
                "Bien.Proveedor",
                $"El proveedor {Bien.Proveedor} no existe en la tabla de proveedores.");
        }

        if (!await _db.Nomenclador.AnyAsync(n => n.Cuenta == Bien.Cuenta && n.Subcuenta == Bien.Subcuenta))
        {
            ModelState.AddModelError("Bien.Cuenta", "La cuenta y subcuenta elegidas no existen en el nomenclador.");
        }

        if (!_sesion.PuedeVer(Bien.Reparticion))
        {
            ModelState.AddModelError("Bien.Reparticion",
                "Elegí una de las reparticiones que tenés habilitadas.");
        }

        await ValidarUbicacionAsync();

        if (!ModelState.IsValid)
        {
            await CargarCatalogosAsync();
            return Page();
        }

        if (Bien.FechaBaja is null && Bien.Estado.HasValue &&
            Bien.Estado == await EstadoHelper.CodigoSolicitudBajaAsync(_db))
        {
            Bien.FechaBaja = DateTime.Today;
        }

        if (EsAlta)
        {
            _db.Inventario.Add(Bien);
        }
        else
        {
            var existe = await _db.Inventario.AnyAsync(i => i.Id == Bien.Id);
            if (!existe) return NotFound();
            _db.Inventario.Update(Bien);
        }

        await _db.SaveChangesAsync();

        var errores = new List<string>();
        foreach (var archivo in Fotos.Where(f => f.Length > 0))
        {
            var r = await _fotos.GuardarAsync(Bien.Id, archivo, _sesion.Cuil);
            if (!r.Ok && r.Error is not null) errores.Add($"{archivo.FileName}: {r.Error}");
        }

        TempData["Mensaje"] = errores.Count == 0
            ? "El bien se guardó correctamente."
            : "El bien se guardó, pero algunas fotos no pudieron cargarse: " + string.Join(" ", errores);

        return RedirectToPage("Detalle", new { id = Bien.Id });
    }

    /// <summary>Devuelve el nombre del empleado al escribir el número de legajo.</summary>
    public async Task<IActionResult> OnGetLegajoAsync(int legajo)
    {
        var encontrado = await _db.Legajos
            .Where(l => l.Numero == legajo)
            .Select(l => new { legajo = l.Numero, nombre = l.Nombre })
            .FirstOrDefaultAsync();

        return new JsonResult(encontrado);
    }

    /// <summary>Búsqueda de legajos por nombre para el popup.</summary>
    public async Task<IActionResult> OnGetBuscarLegajosAsync(string? nombre)
    {
        var texto = (nombre ?? string.Empty).Trim();
        if (texto.Length < 2) return new JsonResult(Array.Empty<object>());

        var resultados = await _db.Legajos
            .Where(l => l.Nombre.Contains(texto))
            .OrderBy(l => l.Nombre)
            .Take(50)
            .Select(l => new { legajo = l.Numero, nombre = l.Nombre, dni = l.Dni })
            .ToListAsync();

        return new JsonResult(resultados);
    }

    /// <summary>Devuelve el nombre del proveedor al escribir su número.</summary>
    public async Task<IActionResult> OnGetProveedorAsync(long proveedor)
    {
        var encontrado = await _db.Proveedores
            .Where(p => p.Numero == proveedor)
            .Select(p => new { proveedor = p.Numero, nombre = p.NombreComercial })
            .FirstOrDefaultAsync();

        return new JsonResult(encontrado);
    }

    /// <summary>Búsqueda de proveedores por nombre, razón social o CUIT para el popup.</summary>
    public async Task<IActionResult> OnGetBuscarProveedoresAsync(string? texto)
    {
        var busqueda = (texto ?? string.Empty).Trim();
        if (busqueda.Length < 2) return new JsonResult(Array.Empty<object>());

        var resultados = await _db.Proveedores
            .Where(p => p.NombreComercial.Contains(busqueda)
                     || p.RazonSocial.Contains(busqueda)
                     || p.Cuit.ToString().Contains(busqueda))
            .OrderBy(p => p.NombreComercial)
            .Take(50)
            .Select(p => new
            {
                proveedor = p.Numero,
                nombre = p.NombreComercial,
                razonsocial = p.RazonSocial,
                cuit = p.Cuit
            })
            .ToListAsync();

        return new JsonResult(resultados);
    }

    private async Task CargarCatalogosAsync()
    {
        Estados = new SelectList(await _db.Estados.OrderBy(e => e.Detalle).ToListAsync(), "Codigo", "Detalle");
        TiposInstrumento = new SelectList(await _db.TiposInstrumento.OrderBy(e => e.Detalle).ToListAsync(), "Codigo", "Detalle");
        TiposComprobante = new SelectList(await _db.TiposComprobante.OrderBy(e => e.Detalle).ToListAsync(), "Codigo", "Detalle");
        TiposAutorizacion = new SelectList(await _db.TiposAutorizacion.OrderBy(e => e.Detalle).ToListAsync(), "Codigo", "Detalle");

        var reparticiones = await _db.Reparticiones.AsNoTracking().OrderBy(r => r.Nombre).ToListAsync();
        ReparticionesTodas = reparticiones
            .Select(r => new SelectListItem($"{r.Codigo} - {r.Nombre}", r.Codigo.ToString()))
            .ToList();
        Reparticiones = reparticiones
            .Where(r => _sesion.Reparticiones is null || _sesion.Reparticiones.Contains(r.Codigo))
            .Select(r => new SelectListItem($"{r.Codigo} - {r.Nombre}", r.Codigo.ToString()))
            .ToList();

        TiposAdquisicion = await _db.TiposAdquisicion
            .OrderBy(t => t.Detalle)
            .Select(t => new SelectListItem
            {
                Value = t.Codigo.ToString(),
                Text = t.Codigo + " - " + t.Detalle
            })
            .ToListAsync();

        if (Bien.TipoAdquis != 0 && !TiposAdquisicion.Any(t => t.Value == Bien.TipoAdquis.ToString()))
        {
            TiposAdquisicion.Insert(0, new SelectListItem(
                $"{Bien.TipoAdquis} - (valor actual, fuera del catálogo)",
                Bien.TipoAdquis.ToString()));
        }

        // Un bien heredado puede tener códigos que no están en el catálogo:
        // se agregan como opción para no perder el valor al guardar.
        if (_sesion.Reparticiones is null) AgregarSiFalta(Reparticiones, FormatoHelper.SinDecimales(Bien.Reparticion));
        AgregarSiFalta(ReparticionesTodas, Bien.RepOrigen == 0 ? null : Bien.RepOrigen.ToString());
        AgregarSiFalta(ReparticionesTodas, Bien.RepDestino == 0 ? null : Bien.RepDestino.ToString());

        // Se ofrecen las activas y, si el bien tiene una dada de baja, también esa.
        var nomenclador = (await _db.Nomenclador.AsNoTracking()
                .Where(n => n.Estado == 0 || (n.Cuenta == Bien.Cuenta && n.Subcuenta == Bien.Subcuenta))
                .OrderBy(n => n.Cuenta).ThenBy(n => n.Subcuenta)
                .Select(n => new { n.Cuenta, n.Subcuenta, n.Descripcion })
                .ToListAsync())
            .Select(n => new { c = n.Cuenta, s = n.Subcuenta, t = Models.Nomenclador.Formatear(n.Cuenta, n.Subcuenta, n.Descripcion) })
            .ToList();
        NomencladorJson = JsonSerializer.Serialize(nomenclador);
        TextoNomenclador = nomenclador.FirstOrDefault(n => n.c == Bien.Cuenta && n.s == Bien.Subcuenta)?.t ?? string.Empty;

        foreach (var nivel in NivelesUbicacion)
            Arbol[nivel] = await _catalogos.OpcionesArbolAsync(nivel);
        // Con oficina se posiciona toda la ruta; sin oficina, al menos el departamento.
        var (nivelHoja, valorHoja) = HojaUbicacion();
        Camino = (await _catalogos.CaminoAsync(nivelHoja, valorHoja))
            .Concat(Enumerable.Repeat((string?)null, 5)).Take(5).ToList();

        if (Bien.Legajo != 0)
        {
            NombreLegajo = await _db.Legajos
                .Where(l => l.Numero == Bien.Legajo)
                .Select(l => l.Nombre)
                .FirstOrDefaultAsync() ?? string.Empty;
        }

        if (Bien.Proveedor != 0)
        {
            NombreProveedor = await _db.Proveedores
                .Where(p => p.Numero == Bien.Proveedor)
                .Select(p => p.NombreComercial)
                .FirstOrDefaultAsync() ?? string.Empty;
        }
    }

    /// <summary>Nivel más bajo de la ubicación que tiene el bien, para reconstruir la cascada.</summary>
    private (string Nivel, string? Valor) HojaUbicacion() =>
        Bien.IdOficina.HasValue ? ("oficinas", Bien.IdOficina.ToString())
        : Bien.IdSubseccion.HasValue ? ("subsecciones", Bien.IdSubseccion.ToString())
        : Bien.IdSeccion.HasValue ? ("secciones", Bien.IdSeccion.ToString())
        : Bien.IdDepartamento.HasValue ? ("departamentos", Bien.IdDepartamento.ToString())
        : ("oficinas", null);

    /// <summary>
    /// Controla la cadena depto → sección → subsección → oficina: cada nivel exige el anterior
    /// y tiene que ser hijo suyo, y el depto tiene que ser de la repartición del bien.
    /// Si hay sección, copia los códigos a las columnas heredadas (QR y planillas viejas);
    /// si no, las deja como vinieron de la migración.
    /// </summary>
    private async Task ValidarUbicacionAsync()
    {
        if (!Bien.IdDepartamento.HasValue) return;

        if (Bien.IdSubseccion.HasValue && !Bien.IdSeccion.HasValue)
        {
            ModelState.AddModelError("Bien.IdSubseccion", "Para elegir la subsección primero elegí la sección.");
            return;
        }

        if (Bien.IdOficina.HasValue && !Bien.IdSubseccion.HasValue)
        {
            ModelState.AddModelError("Bien.IdOficina", "Para elegir la oficina primero elegí la subsección.");
            return;
        }

        var (nivel, valor) = HojaUbicacion();
        var camino = await _catalogos.CaminoAsync(nivel, valor);
        var elegidos = new[]
        {
            FormatoHelper.SinDecimales(Bien.Reparticion), Bien.IdDepartamento?.ToString(),
            Bien.IdSeccion?.ToString(), Bien.IdSubseccion?.ToString(), Bien.IdOficina?.ToString()
        };

        for (var i = 0; i < camino.Count; i++)
        {
            if (camino[i] == elegidos[i]) continue;
            ModelState.AddModelError(string.Empty,
                "La ubicación no es coherente: el departamento tiene que ser de la repartición del bien " +
                "y cada nivel tiene que pertenecer al anterior.");
            return;
        }

        if (!Bien.IdSeccion.HasValue) return;

        Bien.Seccion = await _catalogos.CodigoAsync("secciones", Bien.IdSeccion) ?? 0;
        Bien.Subseccion = await _catalogos.CodigoAsync("subsecciones", Bien.IdSubseccion) ?? 0;
        Bien.Oficina = await _catalogos.CodigoAsync("oficinas", Bien.IdOficina);
    }

    private static void AgregarSiFalta(List<SelectListItem> lista, string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return;
        if (lista.Any(r => r.Value == codigo)) return;

        lista.Insert(0, new SelectListItem(
            $"{codigo} - (valor actual, fuera del catálogo)", codigo));
    }
}
