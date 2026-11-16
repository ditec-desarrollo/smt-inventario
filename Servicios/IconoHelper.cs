using System.Text.RegularExpressions;

namespace InventarioSmt.Servicios;

/// <summary>
/// Traduce los nombres de íconos guardados en card_menu, escritos en el formato
/// PascalCase de Material UI (Inventory2, ManageAccounts), al formato de
/// ligadura que usa Material Symbols (inventory_2, manage_accounts).
/// </summary>
public static class IconoHelper
{
    private const string PorDefecto = "widgets";

    public static string Normalizar(string? icono)
    {
        if (string.IsNullOrWhiteSpace(icono)) return PorDefecto;

        var nombre = icono.Trim();

        if (nombre.Contains(' ')) nombre = nombre.Replace(' ', '_');

        // Ya viene en formato de ligadura.
        if (nombre.All(c => char.IsLower(c) || char.IsDigit(c) || c == '_'))
        {
            return nombre;
        }

        // Los sufijos "Icon", "Outlined", "Rounded" y "Sharp" son de Material UI.
        nombre = Regex.Replace(nombre, "(Icon|Outlined|Rounded|Sharp|TwoTone)$", "");

        nombre = Regex.Replace(nombre, "([a-z])([A-Z])", "$1_$2");
        nombre = Regex.Replace(nombre, "([A-Za-z])([0-9])", "$1_$2");
        nombre = Regex.Replace(nombre, "([A-Z]+)([A-Z][a-z])", "$1_$2");

        nombre = nombre.ToLowerInvariant().Trim('_');

        return nombre.Length == 0 ? PorDefecto : nombre;
    }
}
