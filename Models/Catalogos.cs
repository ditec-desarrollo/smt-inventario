using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventarioSmt.Models;

[Table("estados")]
public class Estado
{
    [Column("estado")]
    public int Codigo { get; set; }

    [Column("detalle"), StringLength(100)]
    public string? Detalle { get; set; }
}

[Table("tipoinstrumento")]
public class TipoInstrumento
{
    [Column("tipoinst")]
    public int Codigo { get; set; }

    [Column("detalle"), StringLength(50)]
    public string? Detalle { get; set; }
}

[Table("tipocomprobantes")]
public class TipoComprobante
{
    [Column("tipocomp")]
    public int Codigo { get; set; }

    [Column("detalle"), StringLength(10)]
    public string Detalle { get; set; } = string.Empty;
}

[Table("tipoautorizaciones")]
public class TipoAutorizacion
{
    [Column("tipoautoriza")]
    public int Codigo { get; set; }

    [Column("detalle"), StringLength(80)]
    public string? Detalle { get; set; }
}

[Table("tipoadquisicion")]
public class TipoAdquisicion
{
    [Column("tipoadquis")]
    public int Codigo { get; set; }

    [Column("detalle"), StringLength(50)]
    public string? Detalle { get; set; }
}

/// <summary>Cuentas y subcuentas patrimoniales; inventario guarda el par (cuenta, subcuenta).</summary>
[Table("nomenclador")]
public class Nomenclador
{
    [Column("id_nomenclador")]
    public uint Id { get; set; }

    [Column("cuenta")]
    public int Cuenta { get; set; }

    [Column("subcuenta")]
    public int Subcuenta { get; set; }

    [Column("descripcion"), StringLength(200)]
    public string Descripcion { get; set; } = string.Empty;

    [Column("estado")]
    public byte Estado { get; set; }

    /// <summary>Mismo formato que la columna rdo del sistema anterior: 002001 - Acoplado.</summary>
    public static string Formatear(int cuenta, int subcuenta, string? descripcion) =>
        string.IsNullOrWhiteSpace(descripcion)
            ? $"{cuenta:000}{subcuenta:000}"
            : $"{cuenta:000}{subcuenta:000} - {descripcion.Trim()}";
}

[Table("departamentos")]
public class Departamento
{
    [Column("id_departamento")]
    public uint Id { get; set; }

    [Column("reparticion")]
    public int Reparticion { get; set; }

    [Column("coddep")]
    public int Codigo { get; set; }

    [Column("detalle"), StringLength(200)]
    public string? Detalle { get; set; }
}

[Table("reparticiones")]
public class Reparticion
{
    [Column("id")]
    public int Id { get; set; }

    /// <summary>Código de repartición, es la clave que guardan las tablas de inventario.</summary>
    [Column("reparticion")]
    public int Codigo { get; set; }

    [Column("nombre"), StringLength(255)]
    public string Nombre { get; set; } = string.Empty;
}

[Table("proveedores")]
public class Proveedor
{
    [Column("proveedor")]
    public long Numero { get; set; }

    [Column("nombrecomercial"), StringLength(100)]
    public string NombreComercial { get; set; } = string.Empty;

    [Column("razonsocial"), StringLength(100)]
    public string RazonSocial { get; set; } = string.Empty;

    [Column("cuit")]
    public long Cuit { get; set; }

    [Column("estado")]
    public int? Estado { get; set; }
}

[Table("legajos")]
public class Legajo
{
    [Column("legajo")]
    public int Numero { get; set; }

    [Column("nombre"), StringLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Column("dni")]
    public int? Dni { get; set; }

    [Column("reparticion")]
    public int Reparticion { get; set; }

    [Column("nroreg")]
    public long NroReg { get; set; }
}
