using System.Globalization;
using System.Text;

namespace InventarioSmt.Servicios;

/// <summary>
/// Genera los reportes en PDF sin dependencias externas: escribe un PDF 1.4 con
/// fuentes estándar (Helvetica) y una tabla paginada.
/// </summary>
public class PdfService
{
    private const float Margen = 28f;
    private const float AltoFila = 15f;
    private const float Fuente = 8.5f;
    private const float FuenteTitulo = 14f;

    /// <summary>Tamaño de hoja A4 horizontal en puntos.</summary>
    private const float Ancho = 842f;
    private const float Alto = 595f;

    /// <summary>Tamaño de hoja A4 vertical en puntos.</summary>
    private const float AnchoVertical = 595f;
    private const float AltoVertical = 842f;

    public byte[] Tabla(string titulo, string? subtitulo, IReadOnlyList<string> encabezados,
        IReadOnlyList<IReadOnlyList<string>> filas)
    {
        var anchos = AnchosColumna(encabezados, filas);
        var paginas = new List<string>();
        var fecha = DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("es-AR"));

        var porPagina = (int)Math.Floor((Alto - Margen * 2 - 70f) / AltoFila);
        porPagina = Math.Max(1, porPagina);
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(filas.Count / (double)porPagina));

        for (var p = 0; p < totalPaginas; p++)
        {
            var desde = p * porPagina;
            var lote = filas.Skip(desde).Take(porPagina).ToList();
            paginas.Add(Contenido(titulo, subtitulo, fecha, encabezados, anchos, lote,
                p + 1, totalPaginas, filas.Count));
        }

        return Armar(paginas);
    }

    /// <summary>Grupo del índice: arranca siempre en página nueva.</summary>
    public record GrupoIndice(string Reparticion, string Departamento, IReadOnlyList<IReadOnlyList<string>> Filas);

    /// <summary>
    /// Índice de control en A4 vertical: una página nueva por grupo (repartición y departamento),
    /// con el total de la página al pie y el total general en la última.
    /// </summary>
    public byte[] IndicePorGrupo(string titulo, IReadOnlyList<string> encabezados, IReadOnlyList<GrupoIndice> grupos)
    {
        var fecha = DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("es-AR"));
        var porPagina = Math.Max(1, (int)Math.Floor((AltoVertical - Margen * 2 - 120f) / AltoFila));
        var anchos = AnchosColumna(encabezados, grupos.SelectMany(g => g.Filas).ToList(), AnchoVertical);
        var totalGeneral = grupos.Sum(g => g.Filas.Count);

        var lotes = new List<(GrupoIndice Grupo, List<IReadOnlyList<string>> Filas)>();
        foreach (var g in grupos)
        {
            var paginasGrupo = Math.Max(1, (int)Math.Ceiling(g.Filas.Count / (double)porPagina));
            for (var p = 0; p < paginasGrupo; p++)
                lotes.Add((g, g.Filas.Skip(p * porPagina).Take(porPagina).ToList()));
        }

        if (lotes.Count == 0)
            lotes.Add((new GrupoIndice("-", "-", Array.Empty<IReadOnlyList<string>>()), new()));

        var paginas = lotes
            .Select((l, i) => ContenidoIndice(titulo, fecha, encabezados, anchos, l.Grupo, l.Filas,
                i + 1, lotes.Count, i == lotes.Count - 1 ? totalGeneral : null))
            .ToList();

        return Armar(paginas, AnchoVertical, AltoVertical);
    }

    private static string ContenidoIndice(string titulo, string fecha, IReadOnlyList<string> encabezados,
        float[] anchos, GrupoIndice grupo, IReadOnlyList<IReadOnlyList<string>> filas,
        int pagina, int totalPaginas, int? totalGeneral)
    {
        var sb = new StringBuilder();
        var y = AltoVertical - Margen;

        sb.Append("0.098 0.463 0.824 rg\n");
        var maximoTitulo = Math.Max(1, (int)((AnchoVertical - Margen * 2) / (FuenteTitulo * 0.55f)));
        sb.Append(Texto(Margen, y - 12f, titulo.Length <= maximoTitulo ? titulo : titulo[..maximoTitulo], FuenteTitulo, true));
        sb.Append("0 0 0 rg\n");

        // Fecha y número de página debajo del título, a la derecha, para que no se pisen.
        y -= 32f;
        sb.Append(Texto(AnchoVertical - Margen - 120f, y, $"{fecha}   Pág. {pagina} de {totalPaginas}", Fuente, false));
        sb.Append(Texto(Margen, y, Recortar($"Repartición: {grupo.Reparticion}", AnchoVertical - Margen * 2 - 130f), 10f, true));
        y -= 14f;
        sb.Append(Texto(Margen, y, $"Departamento: {grupo.Departamento}", 10f, true));
        y -= 14f;

        var anchoTabla = AnchoVertical - Margen * 2;
        sb.Append("0.098 0.463 0.824 rg\n");
        sb.Append(CultureInfo.InvariantCulture, $"{Margen:0.##} {y - AltoFila + 4f:0.##} {anchoTabla:0.##} {AltoFila:0.##} re f\n");
        sb.Append("1 1 1 rg\n");

        var x = Margen;
        for (var c = 0; c < encabezados.Count; c++)
        {
            sb.Append(Texto(x + 3f, y - AltoFila + 8f, Recortar(encabezados[c], anchos[c]), Fuente, true));
            x += anchos[c];
        }

        sb.Append("0 0 0 rg\n");
        y -= AltoFila;

        foreach (var fila in filas)
        {
            x = Margen;
            for (var c = 0; c < encabezados.Count; c++)
            {
                var valor = c < fila.Count ? fila[c] : string.Empty;
                sb.Append(Texto(x + 3f, y - AltoFila + 8f, Recortar(valor, anchos[c]), Fuente, false));
                x += anchos[c];
            }

            sb.Append("0.85 0.85 0.85 RG\n0.4 w\n");
            sb.Append(CultureInfo.InvariantCulture,
                $"{Margen:0.##} {y - AltoFila + 3f:0.##} m {AnchoVertical - Margen:0.##} {y - AltoFila + 3f:0.##} l S\n");
            y -= AltoFila;
        }

        // Pie con los totales.
        var pie = Margen + 30f;
        sb.Append("0 0 0 RG\n0.8 w\n");
        sb.Append(CultureInfo.InvariantCulture, $"{Margen:0.##} {pie + 12f:0.##} m {AnchoVertical - Margen:0.##} {pie + 12f:0.##} l S\n");
        sb.Append(Texto(Margen, pie, $"Bienes en esta página: {filas.Count}", Fuente, true));
        if (totalGeneral.HasValue)
            sb.Append(Texto(Margen + 200f, pie, $"Total general: {totalGeneral}", Fuente, true));

        return sb.ToString();
    }

    private static float[] AnchosColumna(IReadOnlyList<string> encabezados,
        IReadOnlyList<IReadOnlyList<string>> filas, float ancho = Ancho)
    {
        var pesos = new float[encabezados.Count];

        for (var c = 0; c < encabezados.Count; c++)
        {
            var largo = (float)encabezados[c].Length;
            foreach (var fila in filas)
            {
                if (c < fila.Count) largo = Math.Max(largo, Math.Min(fila[c].Length, 60));
            }

            pesos[c] = Math.Max(4f, largo);
        }

        var disponible = ancho - Margen * 2;
        var total = pesos.Sum();
        return pesos.Select(p => p / total * disponible).ToArray();
    }

    private static string Contenido(string titulo, string? subtitulo, string fecha,
        IReadOnlyList<string> encabezados, float[] anchos, IReadOnlyList<IReadOnlyList<string>> filas,
        int pagina, int totalPaginas, int totalFilas)
    {
        var sb = new StringBuilder();
        var y = Alto - Margen;

        sb.Append("0.098 0.463 0.824 rg\n");
        sb.Append(Texto(Margen, y - 12f, titulo, FuenteTitulo, true));
        sb.Append("0 0 0 rg\n");

        y -= 30f;
        var encabezadoIzq = subtitulo ?? "Sistema de Inventario — Municipalidad de San Miguel de Tucumán";
        sb.Append(Texto(Margen, y, encabezadoIzq, Fuente, false));
        sb.Append(Texto(Ancho - Margen - 190f, y, $"{fecha}   Pág. {pagina} de {totalPaginas}   {totalFilas} registro(s)", Fuente, false));

        y -= 16f;

        // Encabezado de la tabla.
        sb.Append("0.098 0.463 0.824 rg\n");
        sb.Append(CultureInfo.InvariantCulture, $"{Margen:0.##} {y - AltoFila + 4f:0.##} {Ancho - Margen * 2:0.##} {AltoFila:0.##} re f\n");
        sb.Append("1 1 1 rg\n");

        var x = Margen;
        for (var c = 0; c < encabezados.Count; c++)
        {
            sb.Append(Texto(x + 3f, y - AltoFila + 8f, Recortar(encabezados[c], anchos[c]), Fuente, true));
            x += anchos[c];
        }

        sb.Append("0 0 0 rg\n");
        y -= AltoFila;

        foreach (var fila in filas)
        {
            x = Margen;
            for (var c = 0; c < encabezados.Count; c++)
            {
                var valor = c < fila.Count ? fila[c] : string.Empty;
                sb.Append(Texto(x + 3f, y - AltoFila + 8f, Recortar(valor, anchos[c]), Fuente, false));
                x += anchos[c];
            }

            sb.Append("0.85 0.85 0.85 RG\n0.4 w\n");
            sb.Append(CultureInfo.InvariantCulture,
                $"{Margen:0.##} {y - AltoFila + 3f:0.##} m {Ancho - Margen:0.##} {y - AltoFila + 3f:0.##} l S\n");

            y -= AltoFila;
        }

        return sb.ToString();
    }

    /// <summary>Recorta el texto al ancho de la columna, estimando el ancho medio de Helvetica.</summary>
    private static string Recortar(string valor, float anchoColumna)
    {
        var maximo = Math.Max(1, (int)((anchoColumna - 6f) / (Fuente * 0.5f)));
        return valor.Length <= maximo ? valor : valor[..maximo];
    }

    private static string Texto(float x, float y, string valor, float tamano, bool negrita)
    {
        var fuente = negrita ? "/F2" : "/F1";
        return string.Create(CultureInfo.InvariantCulture,
            $"BT {fuente} {tamano:0.##} Tf {x:0.##} {y:0.##} Td ({Escapar(valor)}) Tj ET\n");
    }

    /// <summary>Escapa paréntesis y barras; el PDF usa WinAnsiEncoding (Latin-1).</summary>
    private static string Escapar(string valor)
    {
        var sb = new StringBuilder(valor.Length + 8);
        foreach (var c in valor)
        {
            switch (c)
            {
                case '(': sb.Append("\\("); break;
                case ')': sb.Append("\\)"); break;
                case '\\': sb.Append("\\\\"); break;
                case '\r': case '\n': case '\t': sb.Append(' '); break;
                default:
                    sb.Append(c <= 'ÿ' ? c : '?');
                    break;
            }
        }

        return sb.ToString();
    }

    private static byte[] Armar(IReadOnlyList<string> paginas, float ancho = Ancho, float alto = Alto)
    {
        // Objetos: 1 catálogo, 2 árbol de páginas, 3 y 4 fuentes, y por página la
        // página y su contenido.
        var objetos = new List<byte[]>();
        var primeraPagina = 5;
        var ids = Enumerable.Range(0, paginas.Count)
            .Select(i => primeraPagina + i * 2)
            .ToList();

        objetos.Add(Latin1("<< /Type /Catalog /Pages 2 0 R >>"));
        objetos.Add(Latin1(
            $"<< /Type /Pages /Count {paginas.Count} /Kids [{string.Join(' ', ids.Select(i => $"{i} 0 R"))}] >>"));
        objetos.Add(Latin1("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"));
        objetos.Add(Latin1("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"));

        for (var i = 0; i < paginas.Count; i++)
        {
            var idPagina = ids[i];
            objetos.Add(Latin1(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {ancho.ToString("0.##", CultureInfo.InvariantCulture)} " +
                $"{alto.ToString("0.##", CultureInfo.InvariantCulture)}] " +
                $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {idPagina + 1} 0 R >>"));

            var flujo = Latin1Bytes(paginas[i]);
            var encabezado = Latin1($"<< /Length {flujo.Length} >>\nstream\n");
            objetos.Add(encabezado.Concat(flujo).Concat(Latin1Bytes("\nendstream")).ToArray());
        }

        var salida = new MemoryStream();
        Escribir(salida, "%PDF-1.4\n%\u00e2\u00e3\u00cf\u00d3\n");

        var offsets = new List<long>();
        for (var i = 0; i < objetos.Count; i++)
        {
            offsets.Add(salida.Length);
            Escribir(salida, $"{i + 1} 0 obj\n");
            salida.Write(objetos[i]);
            Escribir(salida, "\nendobj\n");
        }

        var inicioXref = salida.Length;
        Escribir(salida, $"xref\n0 {objetos.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) Escribir(salida, $"{offset:0000000000} 00000 n \n");
        Escribir(salida, $"trailer\n<< /Size {objetos.Count + 1} /Root 1 0 R >>\nstartxref\n{inicioXref}\n%%EOF");

        return salida.ToArray();
    }

    private static byte[] Latin1(string texto) => Latin1Bytes(texto);

    private static byte[] Latin1Bytes(string texto) =>
        Encoding.Latin1.GetBytes(texto);

    private static void Escribir(Stream destino, string texto)
    {
        var bytes = Latin1Bytes(texto);
        destino.Write(bytes, 0, bytes.Length);
    }
}
