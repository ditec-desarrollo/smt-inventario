using System.ComponentModel.DataAnnotations;
using InventarioSmt.Data;
using InventarioSmt.Models;
using InventarioSmt.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InventarioSmt.Pages.Administracion.Usuarios;

public class EditarModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly MenuService _menu;
    private readonly SesionUsuario _sesion;

    public EditarModel(AppDbContext db, MenuService menu, SesionUsuario sesion)
    {
        _db = db;
        _menu = menu;
        _sesion = sesion;
    }

    public class Entrada
    {
        public int IdUsuario { get; set; }

        [Required(ErrorMessage = "Ingresá el apellido y nombre."), StringLength(120), Display(Name = "Apellido y nombre")]
        public string ApellidoNombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresá el CUIL."), Display(Name = "CUIL")]
        public string Cuil { get; set; } = string.Empty;

        [StringLength(20), Display(Name = "N° de afiliado")]
        public string? NroAfiliado { get; set; }

        [Display(Name = "Grupo")]
        public int? IdGrupo { get; set; }

        [Display(Name = "Estado")]
        public byte Estado { get; set; }

        [Display(Name = "Todas las reparticiones")]
        public bool TodasReparticiones { get; set; }
    }

    [BindProperty]
    public Entrada Datos { get; set; } = new();

    /// <summary>Módulos habilitados en forma individual (se suman a los del grupo).</summary>
    [BindProperty]
    public List<int> Modulos { get; set; } = new();

    /// <summary>Códigos de las reparticiones cuyos bienes puede ver.</summary>
    [BindProperty]
    public List<int> Reparticiones { get; set; } = new();

    public List<Reparticion> ListaReparticiones { get; private set; } = new();

    public bool EsAlta => Datos.IdUsuario == 0;
    public bool EsPropio => !EsAlta && Datos.IdUsuario == _sesion.Usuario?.IdUsuario;
    public DateTime? FechaAlta { get; private set; }
    public List<CardMenu> Cards { get; private set; } = new();
    public List<SelectListItem> Grupos { get; private set; } = new();

    /// <summary>Módulos de cada grupo activo, para marcar en pantalla lo que ya da el grupo.</summary>
    public Dictionary<int, List<int>> AccesosPorGrupo { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id.HasValue)
        {
            var u = await _db.Usuarios.AsNoTracking().Include(x => x.AccesosIndividuales)
                .Include(x => x.Reparticiones)
                .FirstOrDefaultAsync(x => x.IdUsuario == id);
            if (u is null) return NotFound();

            Datos = new Entrada
            {
                IdUsuario = u.IdUsuario,
                ApellidoNombre = u.ApellidoNombre,
                Cuil = CuilHelper.Formatear(u.Cuil),
                NroAfiliado = u.NroAfiliado,
                IdGrupo = u.IdGrupo,
                Estado = u.Estado,
                TodasReparticiones = u.TodasReparticiones
            };
            Reparticiones = u.Reparticiones.Select(r => r.Reparticion).ToList();
            Modulos = u.AccesosIndividuales.Select(a => a.IdCard).ToList();
            FechaAlta = u.FechaAlta;
        }

        await CargarAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Datos.ApellidoNombre = Datos.ApellidoNombre.Trim().ToUpperInvariant();
        Datos.NroAfiliado = string.IsNullOrWhiteSpace(Datos.NroAfiliado) ? null : Datos.NroAfiliado.Trim();
        var cuil = CuilHelper.Normalizar(Datos.Cuil);

        var existente = EsAlta ? null : await _db.Usuarios.Include(x => x.AccesosIndividuales)
            .Include(x => x.Reparticiones)
            .FirstOrDefaultAsync(x => x.IdUsuario == Datos.IdUsuario);
        if (!EsAlta && existente is null) return NotFound();

        // El dígito verificador se controla solo en CUIL nuevos o modificados, para no
        // trabar la edición de usuarios migrados con datos viejos.
        if (cuil.Length > 0 && cuil != existente?.Cuil && !CuilHelper.EsValido(cuil))
            ModelState.AddModelError("Datos.Cuil", "El CUIL no es válido: tienen que ser 11 dígitos y el verificador tiene que coincidir.");
        else if (cuil.Length > 0 && await _db.Usuarios.AnyAsync(u => u.Cuil == cuil && u.IdUsuario != Datos.IdUsuario))
            ModelState.AddModelError("Datos.Cuil", "Ya hay otro usuario con ese CUIL.");

        if (Datos.NroAfiliado is not null &&
            await _db.Usuarios.AnyAsync(u => u.NroAfiliado == Datos.NroAfiliado && u.IdUsuario != Datos.IdUsuario))
            ModelState.AddModelError("Datos.NroAfiliado", "Ya hay otro usuario con ese número de afiliado.");

        if (Datos.IdGrupo.HasValue && !await _db.Grupos.AnyAsync(g => g.IdGrupo == Datos.IdGrupo))
            ModelState.AddModelError("Datos.IdGrupo", "El grupo elegido no existe.");

        var validos = await _db.CardMenu.Select(c => c.Id).ToListAsync();
        Modulos = Modulos.Distinct().Where(validos.Contains).ToList();

        var codigos = await _db.Reparticiones.Select(r => r.Codigo).ToListAsync();
        Reparticiones = Reparticiones.Distinct().Where(codigos.Contains).ToList();

        if (EsPropio && !await _menu.ConservaGestionAsync(Datos.Estado, Modulos, Datos.IdGrupo))
            ModelState.AddModelError(string.Empty,
                "No podés quitarte a vos mismo el acceso a Usuarios ni darte de baja: tiene que hacerlo otro administrador.");

        if (!ModelState.IsValid)
        {
            FechaAlta = existente?.FechaAlta;
            await CargarAsync();
            return Page();
        }

        var usuario = existente ?? new Usuario { FechaAlta = DateTime.Now };
        if (existente is null) _db.Usuarios.Add(usuario);

        usuario.ApellidoNombre = Datos.ApellidoNombre;
        usuario.Cuil = cuil;
        usuario.NroAfiliado = Datos.NroAfiliado;
        usuario.IdGrupo = Datos.IdGrupo;
        usuario.Estado = Datos.Estado;
        usuario.TodasReparticiones = Datos.TodasReparticiones;

        foreach (var acceso in usuario.AccesosIndividuales.Where(a => !Modulos.Contains(a.IdCard)).ToList())
            _db.UsuariosAccesos.Remove(acceso);
        foreach (var idCard in Modulos.Where(m => usuario.AccesosIndividuales.All(a => a.IdCard != m)).ToList())
            usuario.AccesosIndividuales.Add(new UsuarioAcceso { IdCard = idCard });

        foreach (var r in usuario.Reparticiones.Where(r => !Reparticiones.Contains(r.Reparticion)).ToList())
            _db.UsuariosReparticiones.Remove(r);
        foreach (var codigo in Reparticiones.Where(c => usuario.Reparticiones.All(r => r.Reparticion != c)).ToList())
            usuario.Reparticiones.Add(new UsuarioReparticion { Reparticion = codigo });

        await _db.SaveChangesAsync();
        await _menu.SincronizarCsvAsync(new[] { usuario.IdUsuario });

        TempData["Mensaje"] = existente is null
            ? $"Se dio de alta a {usuario.ApellidoNombre}."
            : $"Se actualizaron los datos de {usuario.ApellidoNombre}.";
        return RedirectToPage("Index");
    }

    private async Task CargarAsync()
    {
        Cards = await _db.CardMenu.AsNoTracking().OrderBy(c => c.Orden).ThenBy(c => c.Id).ToListAsync();
        ListaReparticiones = await _db.Reparticiones.AsNoTracking().OrderBy(r => r.Codigo).ToListAsync();

        var grupos = await _db.Grupos.AsNoTracking().OrderBy(g => g.Nombre).ToListAsync();
        Grupos = grupos
            .Where(g => g.Estado == 0 || g.IdGrupo == Datos.IdGrupo)
            .Select(g => new SelectListItem(g.Estado == 0 ? g.Nombre : g.Nombre + " (inactivo)", g.IdGrupo.ToString()))
            .ToList();

        var activos = grupos.Where(g => g.Estado == 0).Select(g => g.IdGrupo).ToList();
        AccesosPorGrupo = (await _db.GruposAccesos.AsNoTracking()
                .Where(ga => activos.Contains(ga.IdGrupo)).ToListAsync())
            .GroupBy(ga => ga.IdGrupo)
            .ToDictionary(g => g.Key, g => g.Select(x => x.IdCard).ToList());
    }
}
