namespace InventarioSmt.Servicios;

/// <summary>Normaliza, valida (dígito verificador) y formatea CUIL/CUIT.</summary>
public static class CuilHelper
{
    private static readonly int[] Pesos = { 5, 4, 3, 2, 7, 6, 5, 4, 3, 2 };

    public static string Normalizar(string? texto) =>
        new((texto ?? string.Empty).Where(c => c is >= '0' and <= '9').ToArray());

    public static bool EsValido(string cuil)
    {
        if (cuil.Length != 11 || cuil.Any(c => c is < '0' or > '9')) return false;

        var suma = 0;
        for (var i = 0; i < 10; i++) suma += (cuil[i] - '0') * Pesos[i];

        var verificador = 11 - suma % 11;
        if (verificador == 11) verificador = 0;
        return verificador < 10 && verificador == cuil[10] - '0';
    }

    public static string Formatear(string? cuil) =>
        cuil is { Length: 11 } ? $"{cuil[..2]}-{cuil[2..10]}-{cuil[10]}" : cuil ?? string.Empty;
}
