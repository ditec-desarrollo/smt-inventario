using System.ComponentModel.DataAnnotations;
using InventarioSmt.Data;
using InventarioSmt.Models;
using InventarioSmt.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace InventarioSmt.Pages.Administracion.Usuarios;

public class GrupoEditarModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly MenuService _menu;
    private readonly SesionUsuario _sesion;

    public GrupoEditarModel(AppDbContext db, MenuService menu, SesionUsuario sesion)
    {
        _db = db;
        _menu = menu;
        _sesion = sesion;
    }

    public class Entrada
    {
        public int IdGrupo { get; set; }

        [Required(ErrorMessage = "Ingresá el nombre del grupo."), StringLength(60), Display(Name = "Grupo")]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(150), Display(Name = "Detalle")]
        public string? Detalle { get; set; }

        [Display(Name = "Estado")]
        public byte Estado { get; set; }
    }

    [BindProperty]
    public Entrada Datos { get; set; } = new();

    [BindProperty]
    public List<int> Modulos { get; set; } = new();

    public bool EsAlta => Datos.IdGrupo == 0;
    public List<CardMenu> Cards { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id.HasValue)
        {
            var g = await _db.Grupos.AsNoTracking().Include(x => x.Accesos)
                .FirstOrDefaultAsync(x => x.IdGrupo == id);
            if (g is null) return NotFound();

            Datos = new Entrada { IdGrupo = g.IdGrupo, Nombre = g.Nombre, Detalle = g.Detalle, Estado = g.Estado };
            Modulos = g.Accesos.Select(a => a.IdCard).ToList();
        }

        await CargarAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Datos.Nombre = Datos.Nombre.Trim().ToUpperInvariant();
        Datos.Detalle = string.IsNullOrWhiteSpace(Datos.Detalle) ? null : Datos.Detalle.Trim();

        var existente = EsAlta ? null : await _db.Grupos.Include(x => x.Accesos)
            .FirstOrDefaultAsync(x => x.IdGrupo == Datos.IdGrupo);
        if (!EsAlta && existente is null) return NotFound();

        if (Datos.Nombre.Length > 0 &&
            await _db.Grupos.AnyAsync(g => g.Nombre == Datos.Nombre && g.IdGrupo != Datos.IdGrupo))
            ModelState.AddModelError("Datos.Nombre", "Ya existe un grupo con ese nombre.");

        var validos = await _db.CardMenu.Select(c => c.Id).ToListAsync();
        Modulos = Modulos.Distinct().Where(validos.Contains).ToList();

        if (!EsAlta)
        {
            var error = await ValidarGrupoPropioAsync(_db, _menu, _sesion, Datos.IdGrupo, Datos.Estado, Modulos);
            if (error is not null) ModelState.AddModelError(string.Empty, error);
        }

        if (!ModelState.IsValid)
        {
            await CargarAsync();
            return Page();
        }

        var grupo = existente ?? new Grupo();
        if (existente is null) _db.Grupos.Add(grupo);

        grupo.Nombre = Datos.Nombre;
        grupo.Detalle = Datos.Detalle;
        grupo.Estado = Datos.Estado;

        foreach (var acceso in grupo.Accesos.Where(a => !Modulos.Contains(a.IdCard)).ToList())
            _db.GruposAccesos.Remove(acceso);
        foreach (var idCard in Modulos.Where(m => grupo.Accesos.All(a => a.IdCard != m)).ToList())
            grupo.Accesos.Add(new GrupoAcceso { IdCard = idCard });

        await _db.SaveChangesAsync();
        await _menu.SincronizarCsvAsync(
            await _db.Usuarios.Where(u => u.IdGrupo == grupo.IdGrupo).Select(u => u.IdUsuario).ToListAsync());

        TempData["Mensaje"] = existente is null
            ? $"Se creó el grupo {grupo.Nombre}."
            : $"Se actualizó el grupo {grupo.Nombre}.";
        return RedirectToPage("Grupos");
    }

    /// <summary>
    /// Si el grupo es el del usuario que está operando, controla que con los cambios siga
    /// entrando a Usuarios. Devuelve el mensaje de error o null.
    /// </summary>
    public static async Task<string?> ValidarGrupoPropioAsync(AppDbContext db, MenuService menu,
        SesionUsuario sesion, int idGrupo, byte estadoGrupo, IReadOnlyCollection<int> cardsGrupo)
    {
        var yo = sesion.Usuario;
        if (yo is null || yo.IdGrupo != idGrupo) return null;

        var individuales = await db.UsuariosAccesos
            .Where(a => a.IdUsuario == yo.IdUsuario).Select(a => a.IdCard).ToListAsync();

        return await menu.ConservaGestionAsync(yo.Estado, individuales, yo.IdGrupo, estadoGrupo, cardsGrupo)
            ? null
            : "Es tu propio grupo: con este cambio te quedás sin acceso a Usuarios. " +
              "Habilitate primero Usuarios como acceso individual o pedíselo a otro administrador.";
    }

    private async Task CargarAsync() =>
        Cards = await _db.CardMenu.AsNoTracking().OrderBy(c => c.Orden).ThenBy(c => c.Id).ToListAsync();
}
