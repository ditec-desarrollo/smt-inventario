namespace InventarioSmt.Servicios.Catalogos;

public enum TipoCampo
{
    Entero,
    Texto,
    /// <summary>Estado de compatibilidad con las tablas heredadas: 0 activo, 1 inactivo.</summary>
    Estado
}

/// <summary>Una columna del catálogo: cómo se muestra, se valida y se guarda.</summary>
/// <param name="Columna">Nombre de la columna en la base.</param>
/// <param name="Etiqueta">Texto que ve el usuario.</param>
/// <param name="Clave">Forma parte de la clave primaria.</param>
/// <param name="Automatico">La base lo genera (AUTO_INCREMENT): no se edita ni se inserta.</param>
/// <param name="Lookup">Clave de otro catálogo para mostrarlo como lista desplegable.</param>
public record CampoCatalogo(
    string Columna,
    string Etiqueta,
    TipoCampo Tipo = TipoCampo.Texto,
    bool Clave = false,
    bool Requerido = false,
    int Largo = 0,
    string? Lookup = null,
    bool Automatico = false,
    bool EnLista = true);

/// <param name="Clave">Nombre corto usado en la URL.</param>
/// <param name="Orden">Columnas del ORDER BY.</param>
/// <param name="Filtro">
/// Columna por la que el listado filtra con una lista desplegable, antes de buscar por texto
/// (la dependencia jerárquica: la localidad se busca dentro de una provincia, la sección
/// dentro de un departamento, y así).
/// </param>
public record CatalogoDefinicion(
    string Clave,
    string Titulo,
    string Tabla,
    string Icono,
    string Descripcion,
    IReadOnlyList<CampoCatalogo> Campos,
    IReadOnlyList<string> Orden,
    string? Filtro = null)
{
    public IEnumerable<CampoCatalogo> Claves => Campos.Where(c => c.Clave);

    /// <summary>Campo del filtro jerárquico, validado contra las columnas de la definición.</summary>
    public CampoCatalogo? CampoFiltro =>
        Filtro is null ? null : Campos.FirstOrDefault(c => c.Columna == Filtro && c.Lookup is not null);
    public IEnumerable<CampoCatalogo> Editables => Campos.Where(c => !c.Automatico);
    public IEnumerable<CampoCatalogo> Columnas => Campos.Where(c => c.EnLista);

    /// <summary>Las tablas con columna de estado usan baja lógica en lugar de borrado físico.</summary>
    public CampoCatalogo? CampoEstado => Campos.FirstOrDefault(c => c.Tipo == TipoCampo.Estado);
}

/// <summary>Catálogos administrables; la definición es la única fuente de nombres de tabla y columnas.</summary>
public static class Catalogo
{
    public static readonly IReadOnlyList<CatalogoDefinicion> Todos = new List<CatalogoDefinicion>
    {
        new("legajos", "Legajos", "legajos", "badge", "Empleados responsables de los bienes",
            new CampoCatalogo[]
            {
                new("legajo", "Legajo", TipoCampo.Entero, Clave: true, Requerido: true),
                new("nombre", "Nombre y apellido", Requerido: true, Largo: 100),
                new("dni", "DNI", TipoCampo.Entero),
                new("reparticion", "Repartición", TipoCampo.Entero, Requerido: true, Lookup: "reparticiones")
            },
            new[] { "nombre" },
            Filtro: "reparticion"),

        new("provincias", "Provincias", "provincias", "map", "Provincias usadas en localidades y proveedores",
            new CampoCatalogo[]
            {
                new("pcia", "Código", TipoCampo.Entero, Clave: true, Requerido: true),
                new("detalle", "Detalle", Requerido: true, Largo: 40),
                new("estado", "Estado", TipoCampo.Estado)
            },
            new[] { "detalle" }),

        new("localidades", "Localidades", "localidades", "location_city", "Localidades por provincia, con código postal",
            new CampoCatalogo[]
            {
                new("id", "Id", TipoCampo.Entero, Clave: true, Automatico: true, EnLista: false),
                new("pcia", "Provincia", TipoCampo.Entero, Requerido: true, Lookup: "provincias"),
                new("localidad", "Código", TipoCampo.Entero, Requerido: true),
                new("detalle", "Detalle", Requerido: true, Largo: 100),
                new("codpos", "Código postal", Largo: 6)
            },
            new[] { "detalle" },
            Filtro: "pcia"),

        new("reparticiones", "Reparticiones", "reparticiones", "account_balance", "Dependencias municipales",
            new CampoCatalogo[]
            {
                new("id", "Id", TipoCampo.Entero, Clave: true, Automatico: true, EnLista: false),
                new("reparticion", "Código", TipoCampo.Entero, Requerido: true),
                new("nombre", "Nombre", Requerido: true, Largo: 255)
            },
            new[] { "nombre" }),

        new("departamentos", "Departamentos", "departamentos", "corporate_fare",
            "Departamentos dentro de cada repartición",
            new CampoCatalogo[]
            {
                new("id_departamento", "Id", TipoCampo.Entero, Clave: true, Automatico: true, EnLista: false),
                new("reparticion", "Repartición", TipoCampo.Entero, Requerido: true, Lookup: "reparticiones"),
                new("coddep", "Código", TipoCampo.Entero, Requerido: true),
                new("detalle", "Detalle", Largo: 200)
            },
            new[] { "reparticion", "coddep" },
            Filtro: "reparticion"),

        new("secciones", "Secciones", "secciones", "segment", "Secciones dentro de un departamento",
            new CampoCatalogo[]
            {
                new("id_seccion", "Id", TipoCampo.Entero, Clave: true, Automatico: true, EnLista: false),
                new("id_departamento", "Departamento", TipoCampo.Entero, Requerido: true, Lookup: "departamentos"),
                new("seccion", "Código", TipoCampo.Entero, Requerido: true),
                new("nombre", "Nombre", Requerido: true, Largo: 100)
            },
            new[] { "id_departamento", "seccion" },
            Filtro: "id_departamento"),

        new("subsecciones", "Subsecciones", "subsecciones", "list", "Subsecciones dentro de una sección",
            new CampoCatalogo[]
            {
                new("id_subseccion", "Id", TipoCampo.Entero, Clave: true, Automatico: true, EnLista: false),
                new("id_seccion", "Sección", TipoCampo.Entero, Requerido: true, Lookup: "secciones"),
                new("subseccion", "Código", TipoCampo.Entero, Requerido: true),
                new("nombre", "Nombre", Requerido: true, Largo: 100)
            },
            new[] { "id_seccion", "subseccion" },
            Filtro: "id_seccion"),

        new("oficinas", "Oficinas", "oficinas", "meeting_room", "Oficinas donde se ubican los bienes",
            new CampoCatalogo[]
            {
                new("id_oficina", "Id", TipoCampo.Entero, Clave: true, Automatico: true, EnLista: false),
                new("id_subseccion", "Subsección", TipoCampo.Entero, Requerido: true, Lookup: "subsecciones"),
                new("oficina", "Código", TipoCampo.Entero, Requerido: true),
                new("nombre", "Nombre", Requerido: true, Largo: 100)
            },
            new[] { "id_subseccion", "oficina" },
            Filtro: "id_subseccion"),

        new("tipoadquisicion", "Tipos de adquisición", "tipoadquisicion", "shopping_cart", "Formas de adquisición del bien",
            new CampoCatalogo[]
            {
                new("tipoadquis", "Código", TipoCampo.Entero, Clave: true, Requerido: true),
                new("detalle", "Detalle", Requerido: true, Largo: 50)
            },
            new[] { "detalle" }),

        new("nomenclador", "Nomenclador", "nomenclador", "account_tree",
            "Cuentas y subcuentas patrimoniales de los bienes",
            new CampoCatalogo[]
            {
                new("id_nomenclador", "Id", TipoCampo.Entero, Clave: true, Automatico: true, EnLista: false),
                new("cuenta", "Cuenta", TipoCampo.Entero, Requerido: true),
                new("subcuenta", "Subcuenta", TipoCampo.Entero, Requerido: true),
                new("descripcion", "Descripción", Requerido: true, Largo: 200),
                new("estado", "Estado", TipoCampo.Estado)
            },
            new[] { "cuenta", "subcuenta" }),

        new("tipoautorizaciones", "Tipos de autorización", "tipoautorizaciones", "gavel", "Autorizaciones que respaldan el alta",
            new CampoCatalogo[]
            {
                new("tipoautoriza", "Código", TipoCampo.Entero, Clave: true, Requerido: true),
                new("detalle", "Detalle", Requerido: true, Largo: 80)
            },
            new[] { "detalle" }),

        new("tipocomprobantes", "Tipos de comprobante", "tipocomprobantes", "receipt_long", "Comprobantes de compra o ingreso",
            new CampoCatalogo[]
            {
                new("tipocomp", "Código", TipoCampo.Entero, Clave: true, Requerido: true),
                new("detalle", "Detalle", Requerido: true, Largo: 10)
            },
            new[] { "detalle" }),

        new("tipoinstrumento", "Tipos de instrumento", "tipoinstrumento", "description", "Instrumentos legales (decreto, resolución, etc.)",
            new CampoCatalogo[]
            {
                new("tipoinst", "Código", TipoCampo.Entero, Clave: true, Requerido: true),
                new("detalle", "Detalle", Requerido: true, Largo: 50)
            },
            new[] { "detalle" })
    };

    public static CatalogoDefinicion? Buscar(string? clave) =>
        Todos.FirstOrDefault(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Consulta que alimenta una lista desplegable. El texto incluye el camino jerárquico
    /// completo, porque en las tablas con id autoincremental el valor guardado no le dice
    /// nada a quien carga los datos.
    /// </summary>
    public static LookupSql Lookup(CatalogoDefinicion def) => def.Clave switch
    {
        "reparticiones" => new("r.reparticion",
            "CONCAT(r.reparticion, ' - ', r.nombre)",
            "`reparticiones` r"),

        "provincias" => new("p.pcia",
            "CONCAT(p.pcia, ' - ', COALESCE(p.detalle, ''))",
            "`provincias` p"),

        "departamentos" => new("d.id_departamento",
            "CONCAT(d.coddep, ' - ', COALESCE(d.detalle, ''))",
            "`departamentos` d",
            Padre: "d.reparticion", LookupPadre: "reparticiones",
            DesdeBase: "`departamentos` d"),

        "secciones" => new("s.id_seccion",
            "CONCAT(s.seccion, ' - ', s.nombre)",
            "`secciones` s",
            Padre: "s.id_departamento", LookupPadre: "departamentos",
            DesdeBase: "`secciones` s"),

        "subsecciones" => new("b.id_subseccion",
            "CONCAT(b.subseccion, ' - ', b.nombre)",
            "`subsecciones` b",
            Padre: "b.id_seccion", LookupPadre: "secciones",
            DesdeBase: "`subsecciones` b"),

        "oficinas" => new("o.id_oficina",
            "CONCAT(o.oficina, ' - ', o.nombre)",
            "`oficinas` o",
            Padre: "o.id_subseccion", LookupPadre: "subsecciones",
            DesdeBase: "`oficinas` o"),

        _ => new($"c.`{def.Claves.First().Columna}`", "c.`detalle`", $"`{def.Tabla}` c")
    };

    /// <summary>
    /// Niveles desde el más alto hasta el lookup indicado, para que el usuario recorra la
    /// jerarquía completa (repartición → departamento → sección → subsección → oficina).
    /// </summary>
    public static IReadOnlyList<string> Cadena(string lookup)
    {
        var cadena = new List<string>();
        var actual = lookup;

        while (actual is not null && !cadena.Contains(actual))
        {
            cadena.Insert(0, actual);
            var def = Buscar(actual);
            actual = def is null ? null : Lookup(def).LookupPadre;
        }

        return cadena;
    }

    public static string EtiquetaNivel(string lookup) => lookup switch
    {
        "reparticiones" => "Repartición",
        "departamentos" => "Departamento",
        "secciones" => "Sección",
        "subsecciones" => "Subsección",
        "oficinas" => "Oficina",
        "provincias" => "Provincia",
        "localidades" => "Localidad",
        "legajos" => "Legajo",
        _ => Buscar(lookup)?.Titulo ?? lookup
    };
}

/// <param name="Valor">Columna que se guarda.</param>
/// <param name="Texto">Expresión que ve el usuario.</param>
/// <param name="Desde">Tabla y joins necesarios para armar el texto.</param>
/// <param name="Padre">Columna que apunta al nivel superior de la jerarquía.</param>
/// <param name="LookupPadre">Catálogo de ese nivel superior.</param>
/// <param name="DesdeBase">Tabla sola, para usar el lookup dentro de una subconsulta.</param>
public record LookupSql(string Valor, string Texto, string Desde,
    string? Padre = null, string? LookupPadre = null, string? DesdeBase = null)
{
    public string Base => DesdeBase ?? Desde;
}
