using System.Globalization;

namespace InventarioSmt.Servicios;

public static class FormatoHelper
{
    /// <summary>
    /// La identificación se guarda como texto y muchos registros heredados traen
    /// decimales ("12788.0"); se muestran sin la parte decimal cuando es cero.
    /// </summary>
    public static string Identificacion(string? valor) => SinDecimales(valor);

    /// <summary>
    /// Quita la parte decimal de un valor numérico guardado como texto cuando es cero.
    /// Si el texto no es numérico se devuelve tal cual.
    /// </summary>
    public static string SinDecimales(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;

        var texto = valor.Trim();

        if (decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var numero)
            && numero == decimal.Truncate(numero))
        {
            return decimal.Truncate(numero).ToString("0", CultureInfo.InvariantCulture);
        }

        return texto;
    }
}
