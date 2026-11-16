using InventarioSmt.Data;
using InventarioSmt.Models;
using Microsoft.EntityFrameworkCore;

namespace InventarioSmt.Servicios;

/// <summary>
/// Resuelve los accesos efectivos de un usuario: los que le da su grupo (si el grupo está
/// activo) más los individuales. Reemplaza al FIND_IN_SET sobre la columna CSV
/// `accesos` que usaba la aplicación PHP.
/// </summary>
public class MenuService
{
    /// <summary>Ruta del módulo de administración de usuarios en card_menu.</summary>
    public const string RutaUsuarios = "/Administracion/Usuarios";

    private readonly AppDbContext _db;

    public MenuService(AppDbContext db) => _db = db;

    public async Task<List<CardMenu>> AccesosDeAsync(Usuario usuario, CancellationToken ct = default)
    {
        var deGrupo = _db.GruposAccesos
            .Where(ga => ga.IdGrupo == usuario.IdGrupo && ga.Grupo!.Estado == 0)
            .Select(ga => ga.IdCard);

        var individuales = _db.UsuariosAccesos
            .Where(ua => ua.IdUsuario == usuario.IdUsuario)
            .Select(ua => ua.IdCard);

        var permitidos = await deGrupo.Union(individuales).ToListAsync(ct);

        return await _db.CardMenu
            .Where(c => c.Estado == 0 && permitidos.Contains(c.Id))
            .OrderBy(c => c.Orden).ThenBy(c => c.Id)
            .ToListAsync(ct);
    }

    /// <summary>Accesos efectivos de varios usuarios a la vez, para listados.</summary>
    public async Task<Dictionary<int, List<CardMenu>>> AccesosDeVariosAsync(
        IReadOnlyCollection<int> idsUsuarios, CancellationToken ct = default)
    {
        var usuarios = await _db.Usuarios.AsNoTracking()
            .Where(u => idsUsuarios.Contains(u.IdUsuario))
            .Select(u => new { u.IdUsuario, u.IdGrupo })
            .ToListAsync(ct);

        var grupos = usuarios.Where(u => u.IdGrupo.HasValue).Select(u => u.IdGrupo!.Value).Distinct().ToList();

        var deGrupo = await _db.GruposAccesos.AsNoTracking()
            .Where(ga => grupos.Contains(ga.IdGrupo) && ga.Grupo!.Estado == 0)
            .Select(ga => new { ga.IdGrupo, ga.IdCard })
            .ToListAsync(ct);

        var individuales = await _db.UsuariosAccesos.AsNoTracking()
            .Where(ua => idsUsuarios.Contains(ua.IdUsuario))
            .Select(ua => new { ua.IdUsuario, ua.IdCard })
            .ToListAsync(ct);

        var cards = await _db.CardMenu.AsNoTracking()
            .Where(c => c.Estado == 0)
            .OrderBy(c => c.Orden).ThenBy(c => c.Id)
            .ToListAsync(ct);

        return usuarios.ToDictionary(u => u.IdUsuario, u =>
        {
            var ids = deGrupo.Where(g => g.IdGrupo == u.IdGrupo).Select(g => g.IdCard)
                .Concat(individuales.Where(i => i.IdUsuario == u.IdUsuario).Select(i => i.IdCard))
                .ToHashSet();
            return cards.Where(c => ids.Contains(c.Id)).ToList();
        });
    }

    /// <summary>
    /// Reescribe usuarios.accesos (CSV que leía la aplicación PHP) con los accesos
    /// efectivos, para que ambas aplicaciones vean lo mismo mientras convivan.
    /// </summary>
    public async Task SincronizarCsvAsync(IReadOnlyCollection<int> idsUsuarios, CancellationToken ct = default)
    {
        if (idsUsuarios.Count == 0) return;

        var accesos = await AccesosDeVariosAsync(idsUsuarios, ct);
        var usuarios = await _db.Usuarios.Where(u => idsUsuarios.Contains(u.IdUsuario)).ToListAsync(ct);

        foreach (var u in usuarios)
        {
            var ids = accesos.TryGetValue(u.IdUsuario, out var cards)
                ? cards.Select(c => c.Id).OrderBy(i => i)
                : Enumerable.Empty<int>();
            var csv = string.Join(",", ids);
            u.Accesos = csv.Length == 0 ? null : csv;
        }

        await _db.SaveChangesAsync(ct);
    }

    public Task<int?> IdCardUsuariosAsync(CancellationToken ct = default) =>
        _db.CardMenu.Where(c => c.Ruta == RutaUsuarios).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);

    /// <summary>
    /// Indica si un usuario con estos datos seguiría entrando a Usuarios. Sirve para que
    /// nadie se quite a sí mismo el acceso y deje el sistema sin administrador.
    /// Si se pasan <paramref name="estadoGrupo"/> y <paramref name="cardsGrupo"/>, se evalúa
    /// el grupo con esos valores (los que se están por guardar) en lugar de los de la base.
    /// </summary>
    public async Task<bool> ConservaGestionAsync(byte estado, IEnumerable<int> individuales, int? idGrupo,
        byte? estadoGrupo = null, IEnumerable<int>? cardsGrupo = null, CancellationToken ct = default)
    {
        if (estado != 0) return false;

        var card = await IdCardUsuariosAsync(ct);
        if (card is null) return true;
        if (individuales.Contains(card.Value)) return true;
        if (idGrupo is null) return false;

        estadoGrupo ??= await _db.Grupos.Where(g => g.IdGrupo == idGrupo)
            .Select(g => (byte?)g.Estado).FirstOrDefaultAsync(ct);
        if (estadoGrupo != 0) return false;

        return cardsGrupo is not null
            ? cardsGrupo.Contains(card.Value)
            : await _db.GruposAccesos.AnyAsync(ga => ga.IdGrupo == idGrupo && ga.IdCard == card, ct);
    }

    /// <summary>Rutas de los módulos activos, para exigir acceso al entrar a cada uno.</summary>
    public async Task<List<string>> RutasDeModulosAsync(CancellationToken ct = default) =>
        await _db.CardMenu
            .Where(c => c.Estado == 0 && c.Ruta != null && c.Ruta != "")
            .Select(c => c.Ruta!)
            .ToListAsync(ct);
}
