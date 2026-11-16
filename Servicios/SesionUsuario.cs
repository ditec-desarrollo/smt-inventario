using InventarioSmt.Models;

namespace InventarioSmt.Servicios;

/// <summary>Datos del usuario autenticado para el request en curso.</summary>
public class SesionUsuario
{
    public bool Autenticado { get; set; }
    public string Cuil { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int IdPersona { get; set; }
    public Usuario? Usuario { get; set; }
    public IReadOnlyList<CardMenu> Accesos { get; set; } = Array.Empty<CardMenu>();

    /// <summary>Códigos de repartición cuyos bienes puede ver; null = todas. Sin asignar no ve ninguna.</summary>
    public IReadOnlyCollection<int>? Reparticiones { get; private set; } = Array.Empty<int>();

    /// <summary>
    /// Los mismos códigos en las formas en que aparecen en inventario.reparticion,
    /// que en los registros heredados trae decimales ("313", "313.0", "313.00").
    /// </summary>
    public IReadOnlyList<string>? ReparticionesTexto { get; private set; } = Array.Empty<string>();

    public void AsignarReparticiones(bool todas, IEnumerable<int> codigos)
    {
        if (todas)
        {
            Reparticiones = null;
            ReparticionesTexto = null;
            return;
        }

        var lista = codigos.Distinct().ToList();
        Reparticiones = lista;
        ReparticionesTexto = lista.SelectMany(c => new[] { $"{c}", $"{c}.0", $"{c}.00" }).ToList();
    }

    public bool PuedeVer(string? reparticion) =>
        Reparticiones is null ||
        (int.TryParse(FormatoHelper.SinDecimales(reparticion), out var codigo) && Reparticiones.Contains(codigo));

    public bool TieneAcceso(int idCard) => Accesos.Any(c => c.Id == idCard);
    public bool TieneAcceso(string ruta) =>
        Accesos.Any(c => !string.IsNullOrEmpty(c.Ruta) &&
                         ruta.StartsWith(c.Ruta, StringComparison.OrdinalIgnoreCase));
}
