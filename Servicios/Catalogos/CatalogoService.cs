using System.Data;
using System.Data.Common;
using System.Globalization;
using InventarioSmt.Data;
using Microsoft.EntityFrameworkCore;

namespace InventarioSmt.Servicios.Catalogos;

/// <summary>
/// ABM genérico de los catálogos. Los nombres de tabla y columna salen siempre de
/// <see cref="Catalogo"/> (nunca del pedido del usuario) y los valores viajan como parámetros.
/// </summary>
public class CatalogoService
{
    private readonly AppDbContext _db;

    public CatalogoService(AppDbContext db) => _db = db;

    public record Resultado(List<Dictionary<string, object?>> Filas, int Total);

    /// <summary>Un nivel de la jerarquía con sus opciones ya acotadas por el nivel superior.</summary>
    public record Nivel(string Lookup, string Etiqueta, List<(string Valor, string Texto)> Opciones,
        string? Seleccionado);

    public async Task<Resultado> ListarAsync(CatalogoDefinicion def, string? buscar,
        int pagina, int tamano, bool todas = false, IReadOnlyList<string?>? jerarquia = null,
        CancellationToken ct = default)
    {
        var cn = await AbrirAsync(ct);

        var parametros = new List<object?>();
        var where = Filtro(def, buscar, jerarquia, parametros);
        var orden = string.Join(", ", def.Orden.Select(o => $"`{o}`"));

        var total = Convert.ToInt32(
            await EscalarAsync(cn, $"SELECT COUNT(*) FROM `{def.Tabla}`{where}", parametros, ct),
            CultureInfo.InvariantCulture);

        var columnas = string.Join(", ", def.Campos.Select(c => $"`{c.Columna}`"));
        var limite = todas ? string.Empty : $" LIMIT {tamano} OFFSET {(pagina - 1) * tamano}";

        var filas = await LeerAsync(cn,
            $"SELECT {columnas} FROM `{def.Tabla}`{where} ORDER BY {orden}{limite}", parametros, ct);

        return new Resultado(filas, total);
    }

    public async Task<Dictionary<string, object?>?> ObtenerAsync(CatalogoDefinicion def, string clave,
        CancellationToken ct = default)
    {
        var pk = def.Claves.First();
        var cn = await AbrirAsync(ct);

        var columnas = string.Join(", ", def.Campos.Select(c => $"`{c.Columna}`"));
        var filas = await LeerAsync(cn,
            $"SELECT {columnas} FROM `{def.Tabla}` WHERE `{pk.Columna}` = @p0 LIMIT 1",
            new List<object?> { Convertir(pk, clave) }, ct);

        return filas.FirstOrDefault();
    }

    /// <summary>Inserta o actualiza; devuelve el mensaje de error o null si salió bien.</summary>
    public async Task<string?> GuardarAsync(CatalogoDefinicion def, IDictionary<string, string?> valores,
        bool esAlta, string? claveOriginal, CancellationToken ct = default)
    {
        var pk = def.Claves.First();
        var campos = def.Editables.ToList();
        var parametros = new List<object?>();

        string sql;
        if (esAlta)
        {
            var columnas = string.Join(", ", campos.Select(c => $"`{c.Columna}`"));
            var marcas = string.Join(", ", campos.Select((_, i) => $"@p{i}"));
            foreach (var c in campos) parametros.Add(Convertir(c, Valor(valores, c)));
            sql = $"INSERT INTO `{def.Tabla}` ({columnas}) VALUES ({marcas})";
        }
        else
        {
            var asignaciones = string.Join(", ", campos.Select((c, i) => $"`{c.Columna}` = @p{i}"));
            foreach (var c in campos) parametros.Add(Convertir(c, Valor(valores, c)));
            parametros.Add(Convertir(pk, claveOriginal));
            sql = $"UPDATE `{def.Tabla}` SET {asignaciones} WHERE `{pk.Columna}` = @p{campos.Count}";
        }

        try
        {
            var cn = await AbrirAsync(ct);
            await EjecutarAsync(cn, sql, parametros, ct);
            return null;
        }
        catch (DbException ex)
        {
            return MensajeAmigable(ex, def);
        }
    }

    /// <summary>Baja lógica si la tabla tiene estado; si no, borrado físico.</summary>
    public async Task<string?> BajaAsync(CatalogoDefinicion def, string clave, bool reactivar,
        CancellationToken ct = default)
    {
        var pk = def.Claves.First();
        var estado = def.CampoEstado;

        var sql = estado is null
            ? $"DELETE FROM `{def.Tabla}` WHERE `{pk.Columna}` = @p0"
            : $"UPDATE `{def.Tabla}` SET `{estado.Columna}` = {(reactivar ? 0 : 1)} WHERE `{pk.Columna}` = @p0";

        try
        {
            var cn = await AbrirAsync(ct);
            await EjecutarAsync(cn, sql, new List<object?> { Convertir(pk, clave) }, ct);
            return null;
        }
        catch (DbException ex)
        {
            return MensajeAmigable(ex, def);
        }
    }

    public Task<List<(string Valor, string Texto)>> OpcionesAsync(string lookup,
        CancellationToken ct = default) => OpcionesAsync(lookup, null, ct);

    /// <summary>
    /// Opciones de una lista desplegable que apunta a otro catálogo, acotadas al nivel
    /// superior cuando se indica (los departamentos de una repartición, por ejemplo).
    /// </summary>
    public async Task<List<(string Valor, string Texto)>> OpcionesAsync(string lookup,
        string? padre, CancellationToken ct = default)
    {
        var def = Catalogo.Buscar(lookup);
        if (def is null) return new List<(string, string)>();

        // La consulta sale de Catalogo, nunca del pedido del usuario.
        var sql = Catalogo.Lookup(def);
        var parametros = new List<object?>();
        var where = string.Empty;

        if (sql.Padre is not null && !string.IsNullOrWhiteSpace(padre))
        {
            where = $" WHERE {sql.Padre} = @p0";
            parametros.Add(Numero(padre));
        }

        var cn = await AbrirAsync(ct);
        var filas = await LeerAsync(cn,
            $"SELECT {sql.Valor} AS v, {sql.Texto} AS t FROM {sql.Desde}{where} ORDER BY t",
            parametros, ct);

        return filas
            .Select(f => (
                Valor: Convert.ToString(f["v"], CultureInfo.InvariantCulture) ?? string.Empty,
                Texto: Convert.ToString(f["t"], CultureInfo.InvariantCulture) ?? string.Empty))
            .Where(o => o.Valor.Length > 0)
            .ToList();
    }

    /// <summary>Opción de una lista desplegable junto con el nivel del que depende.</summary>
    public record Opcion(string Valor, string Texto, string Padre);

    /// <summary>
    /// Todas las opciones de un nivel con su padre, para armar listas en cascada en el
    /// formulario sin volver al servidor en cada cambio.
    /// </summary>
    public async Task<List<Opcion>> OpcionesArbolAsync(string lookup, CancellationToken ct = default)
    {
        var def = Catalogo.Buscar(lookup);
        if (def is null) return new List<Opcion>();

        var sql = Catalogo.Lookup(def);
        var padre = sql.Padre ?? "''";

        var cn = await AbrirAsync(ct);
        var filas = await LeerAsync(cn,
            $"SELECT {sql.Valor} AS v, {sql.Texto} AS t, {padre} AS p FROM {sql.Desde} ORDER BY t",
            new List<object?>(), ct);

        return filas
            .Select(f => new Opcion(ValorCrudo(f["v"]), ValorCrudo(f["t"]), ValorCrudo(f["p"])))
            .Where(o => o.Valor.Length > 0)
            .ToList();
    }

    /// <summary>
    /// Cadena de listas desplegables de un lookup jerárquico: cada nivel se carga con el valor
    /// elegido en el anterior, y una selección que ya no pertenece a ese nivel se descarta.
    /// </summary>
    public async Task<List<Nivel>> NivelesAsync(string lookupHoja, IReadOnlyList<string?> seleccion,
        CancellationToken ct = default)
    {
        var niveles = new List<Nivel>();
        var cadena = Catalogo.Cadena(lookupHoja);
        string? padre = null;

        for (var i = 0; i < cadena.Count; i++)
        {
            var opciones = i == 0 || padre is not null
                ? await OpcionesAsync(cadena[i], padre, ct)
                : new List<(string Valor, string Texto)>();

            var elegido = i < seleccion.Count ? seleccion[i]?.Trim() : null;
            if (string.IsNullOrEmpty(elegido) || !opciones.Any(o => o.Valor == elegido)) elegido = null;

            niveles.Add(new Nivel(cadena[i], Catalogo.EtiquetaNivel(cadena[i]), opciones, elegido));
            padre = elegido;
        }

        return niveles;
    }

    /// <summary>
    /// Valores de cada nivel que llevan a un registro, para abrir la edición con los
    /// desplegables de la jerarquía ya posicionados.
    /// </summary>
    public async Task<List<string?>> CaminoAsync(string lookupHoja, string? valorHoja,
        CancellationToken ct = default)
    {
        var cadena = Catalogo.Cadena(lookupHoja);
        var camino = Enumerable.Repeat((string?)null, cadena.Count).ToList();
        if (string.IsNullOrWhiteSpace(valorHoja)) return camino;

        var valor = valorHoja.Trim();
        for (var i = cadena.Count - 1; i >= 0 && valor is not null; i--)
        {
            camino[i] = valor;
            valor = i == 0 ? null : await PadreDeAsync(cadena[i], valor, ct);
        }

        return camino;
    }

    private async Task<string?> PadreDeAsync(string lookup, string valor, CancellationToken ct)
    {
        var def = Catalogo.Buscar(lookup);
        if (def is null) return null;

        var sql = Catalogo.Lookup(def);
        if (sql.Padre is null) return null;

        var cn = await AbrirAsync(ct);
        var dato = await EscalarAsync(cn,
            $"SELECT {sql.Padre} FROM {sql.Base} WHERE {sql.Valor} = @p0 LIMIT 1",
            new List<object?> { Numero(valor) }, ct);

        var texto = ValorCrudo(dato);
        return texto.Length == 0 ? null : texto;
    }

    /// <summary>Códigos de cada nivel que llevan a una oficina, y la ruta para mostrarla.</summary>
    public record RutaOficina(int Reparticion, int Departamento, int Seccion, int Subseccion,
        int Oficina, string Texto, uint IdDepartamento);

    public async Task<RutaOficina?> RutaOficinaAsync(uint idOficina, CancellationToken ct = default)
    {
        var cn = await AbrirAsync(ct);
        var filas = await LeerAsync(cn,
            "SELECT d.reparticion AS r, d.coddep AS d, s.seccion AS s, b.subseccion AS b, o.oficina AS o, " +
            "d.id_departamento AS id, " +
            "CONCAT_WS(' / ', CONCAT(d.coddep, ' - ', COALESCE(d.detalle, '')), " +
            "CONCAT(s.seccion, ' - ', s.nombre), CONCAT(b.subseccion, ' - ', b.nombre), " +
            "CONCAT(o.oficina, ' - ', o.nombre)) AS t " +
            "FROM `oficinas` o " +
            "JOIN `subsecciones` b ON b.id_subseccion = o.id_subseccion " +
            "JOIN `secciones` s ON s.id_seccion = b.id_seccion " +
            "JOIN `departamentos` d ON d.id_departamento = s.id_departamento " +
            "WHERE o.id_oficina = @p0",
            new List<object?> { idOficina }, ct);

        if (filas.Count == 0) return null;
        var f = filas[0];
        int N(string c) => Convert.ToInt32(f[c], CultureInfo.InvariantCulture);
        return new RutaOficina(N("r"), N("d"), N("s"), N("b"), N("o"), ValorCrudo(f["t"]),
            Convert.ToUInt32(f["id"], CultureInfo.InvariantCulture));
    }

    private static readonly Dictionary<string, (string Tabla, string Id, string Codigo)> CodigosNivel = new()
    {
        ["secciones"] = ("secciones", "id_seccion", "seccion"),
        ["subsecciones"] = ("subsecciones", "id_subseccion", "subseccion"),
        ["oficinas"] = ("oficinas", "id_oficina", "oficina")
    };

    /// <summary>Código funcional (no el id) de una sección, subsección u oficina.</summary>
    public async Task<int?> CodigoAsync(string nivel, uint? id, CancellationToken ct = default)
    {
        if (id is null || !CodigosNivel.TryGetValue(nivel, out var c)) return null;

        var cn = await AbrirAsync(ct);
        var dato = await EscalarAsync(cn,
            $"SELECT `{c.Codigo}` FROM `{c.Tabla}` WHERE `{c.Id}` = @p0",
            new List<object?> { id.Value }, ct);
        return dato is null or DBNull ? null : Convert.ToInt32(dato, CultureInfo.InvariantCulture);
    }

    /// <summary>Ruta "depto / sección / subsección / oficina" del bien, hasta el nivel que tenga.</summary>
    public async Task<string?> RutaTextoAsync(InventarioSmt.Models.Inventario bien, CancellationToken ct = default)
    {
        if (bien.IdDepartamento is null) return null;

        var cn = await AbrirAsync(ct);
        var filas = await LeerAsync(cn,
            "SELECT CONCAT(d.coddep, ' - ', COALESCE(d.detalle, '')) AS d, " +
            "(SELECT CONCAT(s.seccion, ' - ', s.nombre) FROM `secciones` s WHERE s.id_seccion = @p1) AS s, " +
            "(SELECT CONCAT(b.subseccion, ' - ', b.nombre) FROM `subsecciones` b WHERE b.id_subseccion = @p2) AS b, " +
            "(SELECT CONCAT(o.oficina, ' - ', o.nombre) FROM `oficinas` o WHERE o.id_oficina = @p3) AS o " +
            "FROM `departamentos` d WHERE d.id_departamento = @p0",
            new List<object?> { bien.IdDepartamento, bien.IdSeccion, bien.IdSubseccion, bien.IdOficina }, ct);

        if (filas.Count == 0) return null;
        return string.Join(" / ", new[] { "d", "s", "b", "o" }
            .Select(k => ValorCrudo(filas[0][k])).Where(t => t.Length > 0));
    }

    /// <summary>Repartición y texto de un departamento, para bienes con depto y sin oficina.</summary>
    public record DatosDepartamento(int Reparticion, string Texto);

    public async Task<DatosDepartamento?> DepartamentoAsync(uint idDepartamento, CancellationToken ct = default)
    {
        var cn = await AbrirAsync(ct);
        var filas = await LeerAsync(cn,
            "SELECT d.reparticion AS r, CONCAT(d.coddep, ' - ', COALESCE(d.detalle, '')) AS t " +
            "FROM `departamentos` d WHERE d.id_departamento = @p0",
            new List<object?> { idDepartamento }, ct);

        if (filas.Count == 0) return null;
        return new DatosDepartamento(Convert.ToInt32(filas[0]["r"], CultureInfo.InvariantCulture),
            ValorCrudo(filas[0]["t"]));
    }

    /// <summary>Diccionario código → texto para mostrar los lookups en listas y reportes.</summary>
    public async Task<Dictionary<string, string>> DiccionarioAsync(string lookup,
        CancellationToken ct = default)
    {
        var opciones = await OpcionesAsync(lookup, ct);
        var dic = new Dictionary<string, string>();
        foreach (var (valor, texto) in opciones) dic[valor] = texto;
        return dic;
    }

    public static string Mostrar(CampoCatalogo campo, object? valor)
    {
        if (valor is null || valor is DBNull) return string.Empty;

        if (campo.Tipo == TipoCampo.Estado)
        {
            return Convert.ToInt32(valor, CultureInfo.InvariantCulture) == 0 ? "Activo" : "Inactivo";
        }

        return valor switch
        {
            DateTime f => f.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            IFormattable n => n.ToString(null, CultureInfo.InvariantCulture),
            _ => valor.ToString() ?? string.Empty
        };
    }

    public static string ValorCrudo(object? valor) => valor switch
    {
        null or DBNull => string.Empty,
        IFormattable n => n.ToString(null, CultureInfo.InvariantCulture),
        _ => valor.ToString() ?? string.Empty
    };

    private static string? Valor(IDictionary<string, string?> valores, CampoCatalogo campo) =>
        valores.TryGetValue(campo.Columna, out var v) ? v : null;

    private static string Filtro(CatalogoDefinicion def, string? buscar,
        IReadOnlyList<string?>? jerarquia, List<object?> parametros)
    {
        var partes = new List<string>();

        var jerarquico = Jerarquico(def, jerarquia, parametros);
        if (jerarquico.Length > 0) partes.Add(jerarquico);

        var busqueda = Busqueda(def, buscar, parametros);
        if (busqueda.Length > 0) partes.Add(busqueda);

        return partes.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", partes);
    }

    /// <summary>
    /// Filtra por el nivel más bajo que el usuario eligió: si es el padre directo compara la
    /// columna, y si es un nivel más alto resuelve la dependencia con subconsultas anidadas.
    /// </summary>
    private static string Jerarquico(CatalogoDefinicion def, IReadOnlyList<string?>? jerarquia,
        List<object?> parametros)
    {
        var filtro = def.CampoFiltro;
        if (filtro is null || jerarquia is null) return string.Empty;

        var cadena = Catalogo.Cadena(filtro.Lookup!);
        var nivel = -1;
        for (var i = 0; i < cadena.Count && i < jerarquia.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(jerarquia[i])) nivel = i;
        }

        if (nivel < 0) return string.Empty;

        var marca = $"@p{parametros.Count}";
        parametros.Add(Numero(jerarquia[nivel]));

        if (nivel == cadena.Count - 1) return $"`{filtro.Columna}` = {marca}";

        return $"`{filtro.Columna}` IN ({Subconsulta(cadena, nivel, cadena.Count - 1, marca)})";
    }

    /// <summary>
    /// Encadena los niveles intermedios: los ids del nivel <paramref name="hasta"/> que
    /// dependen del valor elegido en el nivel <paramref name="desde"/>.
    /// </summary>
    private static string Subconsulta(IReadOnlyList<string> cadena, int desde, int hasta,
        string marca)
    {
        var def = Catalogo.Buscar(cadena[hasta]);
        var sql = Catalogo.Lookup(def!);

        var condicion = hasta - 1 == desde
            ? $"{sql.Padre} = {marca}"
            : $"{sql.Padre} IN ({Subconsulta(cadena, desde, hasta - 1, marca)})";

        return $"SELECT {sql.Valor} FROM {sql.Base} WHERE {condicion}";
    }

    private static long? Numero(string? valor) =>
        long.TryParse((valor ?? string.Empty).Trim(), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var n)
            ? n
            : null;

    /// <summary>Busca el texto en las columnas de texto y, si es numérico, en las numéricas.</summary>
    private static string Busqueda(CatalogoDefinicion def, string? buscar, List<object?> parametros)
    {
        var texto = (buscar ?? string.Empty).Trim();
        if (texto.Length == 0) return string.Empty;

        var condiciones = new List<string>();

        foreach (var campo in def.Campos.Where(c => c.Tipo == TipoCampo.Texto))
        {
            condiciones.Add($"`{campo.Columna}` LIKE @p{parametros.Count}");
            parametros.Add($"%{texto}%");
        }

        if (long.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero))
        {
            foreach (var campo in def.Campos.Where(c => c.Tipo == TipoCampo.Entero))
            {
                condiciones.Add($"`{campo.Columna}` = @p{parametros.Count}");
                parametros.Add(numero);
            }
        }

        return condiciones.Count == 0 ? string.Empty : "(" + string.Join(" OR ", condiciones) + ")";
    }

    private static object? Convertir(CampoCatalogo campo, string? valor)
    {
        var texto = (valor ?? string.Empty).Trim();

        if (campo.Tipo is TipoCampo.Entero or TipoCampo.Estado)
        {
            if (texto.Length == 0) return DBNull.Value;
            return long.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                ? n
                : DBNull.Value;
        }

        if (campo.Largo > 0 && texto.Length > campo.Largo) texto = texto[..campo.Largo];
        return texto;
    }

    private static string MensajeAmigable(DbException ex, CatalogoDefinicion def) => ex.Message switch
    {
        var m when m.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) =>
            $"Ya existe un registro de {def.Titulo.ToLowerInvariant()} con ese código.",
        var m when m.Contains("foreign key", StringComparison.OrdinalIgnoreCase) =>
            "No se puede completar la operación porque el registro está en uso por otras tablas.",
        _ => "No se pudo guardar: " + ex.Message
    };

    /// <summary>Usa la conexión del DbContext; la cierra Entity Framework, no estos métodos.</summary>
    private async Task<DbConnection> AbrirAsync(CancellationToken ct)
    {
        var cn = _db.Database.GetDbConnection();
        if (cn.State != ConnectionState.Open) await cn.OpenAsync(ct);
        return cn;
    }

    private static DbCommand Comando(DbConnection cn, string sql, IReadOnlyList<object?> parametros)
    {
        var cmd = cn.CreateCommand();
        cmd.CommandText = sql;

        for (var i = 0; i < parametros.Count; i++)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = $"@p{i}";
            p.Value = parametros[i] ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        return cmd;
    }

    private static async Task<List<Dictionary<string, object?>>> LeerAsync(DbConnection cn, string sql,
        IReadOnlyList<object?> parametros, CancellationToken ct)
    {
        await using var cmd = Comando(cn, sql, parametros);
        await using var lector = await cmd.ExecuteReaderAsync(ct);

        var filas = new List<Dictionary<string, object?>>();
        while (await lector.ReadAsync(ct))
        {
            var fila = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < lector.FieldCount; i++)
            {
                fila[lector.GetName(i)] = await lector.IsDBNullAsync(i, ct) ? null : lector.GetValue(i);
            }

            filas.Add(fila);
        }

        return filas;
    }

    private static async Task<object?> EscalarAsync(DbConnection cn, string sql,
        IReadOnlyList<object?> parametros, CancellationToken ct)
    {
        await using var cmd = Comando(cn, sql, parametros);
        return await cmd.ExecuteScalarAsync(ct);
    }

    private static async Task EjecutarAsync(DbConnection cn, string sql,
        IReadOnlyList<object?> parametros, CancellationToken ct)
    {
        await using var cmd = Comando(cn, sql, parametros);
        await cmd.ExecuteNonQueryAsync(ct);
    }

}
