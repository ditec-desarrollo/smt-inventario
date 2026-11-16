using System.Globalization;
using System.Text;
using QRCoder;

namespace InventarioSmt.Servicios;

/// <summary>Genera los códigos QR de los bienes como PNG, sin depender de servicios externos.</summary>
public class QrService
{
    public byte[] GenerarPng(string contenido, int pixelesPorModulo = 8,
        QRCodeGenerator.ECCLevel correccion = QRCodeGenerator.ECCLevel.Q)
    {
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(contenido, correccion);
        var png = new PngByteQRCode(datos);
        return png.GetGraphic(pixelesPorModulo);
    }

    /// <summary>
    /// QR para etiquetas chicas: corrección baja, para que el módulo quede lo más grande
    /// posible dentro del troquel.
    /// </summary>
    public byte[] GenerarPngEtiqueta(string contenido) =>
        GenerarPng(contenido, 8, QRCodeGenerator.ECCLevel.L);

    /// <summary>Largo máximo del texto para que el QR no pase de 33 módulos (0,7 mm por módulo en 23 mm).</summary>
    public const int LargoMaximoEtiqueta = 110;

    /// <summary>
    /// Texto legible que se codifica en la etiqueta de un bien. Va en mayúsculas, sin acentos y
    /// sin campos vacíos: así el QR entra en modo alfanumérico y necesita menos módulos, que es
    /// lo que permite leerlo impreso a 23 mm.
    /// </summary>
    public static string ContenidoBien(
        string id,
        string? equipamiento,
        string? marca,
        string? modelo,
        string? nroSerie,
        string? caracteristicas,
        string? reparticion,
        string? oficina)
    {
        var partes = new List<string> { $"ID:{id}" };

        Agregar("EQUIP", equipamiento, 25);
        Agregar("MARCA", marca, 15);
        Agregar("MOD", modelo, 15);
        Agregar("SERIE", nroSerie, 15);
        Agregar("REP", reparticion, 6);
        Agregar("OFIC", oficina, 6);
        Agregar("CARACT", caracteristicas, 25);

        var contenido = Normalizar(string.Join(' ', partes));

        return contenido.Length > LargoMaximoEtiqueta
            ? contenido[..LargoMaximoEtiqueta].TrimEnd()
            : contenido;

        void Agregar(string etiqueta, string? valor, int largoMaximo)
        {
            var texto = (valor ?? string.Empty).Trim();
            if (texto.Length == 0) return;
            if (texto.Length > largoMaximo) texto = texto[..largoMaximo].TrimEnd();

            partes.Add($"{etiqueta}:{texto}");
        }
    }

    /// <summary>Deja solo los caracteres del modo alfanumérico del estándar QR.</summary>
    private static string Normalizar(string texto)
    {
        const string permitidos = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";

        var sinAcentos = texto
            .Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray();

        var salida = new StringBuilder(sinAcentos.Length);

        foreach (var c in new string(sinAcentos).ToUpperInvariant())
            salida.Append(permitidos.Contains(c) ? c : ' ');

        return salida.ToString().Trim();
    }
}
