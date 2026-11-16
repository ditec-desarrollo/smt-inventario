using InventarioSmt.Data;
using Microsoft.EntityFrameworkCore;

namespace InventarioSmt.Servicios;

/// <summary>Estados con significado propio. Se buscan por texto porque el código depende de cada base.</summary>
public static class EstadoHelper
{
    public const string SolicitudBaja = "SOLICITUD DE BAJA";

    /// <summary>Código del estado "Solicitud de Baja" (la comparación de la base no distingue mayúsculas).</summary>
    public static Task<int?> CodigoSolicitudBajaAsync(AppDbContext db, CancellationToken ct = default) =>
        db.Estados.AsNoTracking()
            .Where(e => e.Detalle == SolicitudBaja)
            .Select(e => (int?)e.Codigo)
            .FirstOrDefaultAsync(ct);
}
