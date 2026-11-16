using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventarioSmt.Models;

[Table("inventario")]
public class Inventario
{
    [Column("id")]
    public int Id { get; set; }

    [Column("identificacion"), StringLength(100), Display(Name = "Identificación")]
    public string? Identificacion { get; set; }

    [Column("expediente"), StringLength(100)]
    public string? Expediente { get; set; }

    [Column("libramiento"), StringLength(100)]
    public string? Libramiento { get; set; }

    [Column("equipamiento"), StringLength(255)]
    public string? Equipamiento { get; set; }

    [Column("marca"), StringLength(100)]
    public string? Marca { get; set; }

    [Column("modelo"), StringLength(100)]
    public string? Modelo { get; set; }

    [Column("nroserie"), StringLength(100), Display(Name = "N° de serie")]
    public string? NroSerie { get; set; }

    [Column("caracteristicas"), Display(Name = "Características")]
    public string? Caracteristicas { get; set; }

    [Column("reparticion"), StringLength(150), Display(Name = "Repartición")]
    public string? Reparticion { get; set; }

    [Column("oficina")]
    public int? Oficina { get; set; }

    /// <summary>Oficina de la estructura organizativa; de ella salen depto, sección y subsección.</summary>
    [Column("id_oficina"), Display(Name = "Oficina")]
    public uint? IdOficina { get; set; }

    /// <summary>
    /// Ubicación organizativa: el departamento es obligatorio; sección, subsección y oficina
    /// son opcionales, pero cada una exige la anterior y tiene que ser hija suya.
    /// </summary>
    [Column("id_departamento"), Display(Name = "Departamento")]
    [Required(ErrorMessage = "Elegí el departamento del bien.")]
    public uint? IdDepartamento { get; set; }

    [Column("id_seccion"), Display(Name = "Sección")]
    public uint? IdSeccion { get; set; }

    [Column("id_subseccion"), Display(Name = "Subsección")]
    public uint? IdSubseccion { get; set; }

    [Column("seccion"), Display(Name = "Sección")]
    public int Seccion { get; set; }

    [Column("subseccion"), Display(Name = "Subsección")]
    public int Subseccion { get; set; }

    [Column("estado")]
    public int? Estado { get; set; }

    [Column("tipoinst"), Display(Name = "Tipo de instrumento")]
    public int TipoInst { get; set; }

    [Column("instrlegal"), StringLength(50), Display(Name = "Instrumento legal")]
    public string InstrLegal { get; set; } = string.Empty;

    [Column("reporigen"), Display(Name = "Repartición de origen")]
    public int RepOrigen { get; set; }

    [Column("fechaorigen"), DataType(DataType.Date), Display(Name = "Fecha de origen")]
    public DateTime? FechaOrigen { get; set; }

    [Column("repdestino"), Display(Name = "Repartición de destino")]
    public int RepDestino { get; set; }

    [Column("fechadestino"), DataType(DataType.Date), Display(Name = "Fecha de destino")]
    public DateTime? FechaDestino { get; set; }

    [Column("tipoadquis"), Display(Name = "Tipo de adquisición")]
    public int TipoAdquis { get; set; }

    /// <summary>Cuenta del nomenclador; por defecto 1 / 0 ("Otra").</summary>
    [Column("cuenta"), Display(Name = "Nomenclador")]
    public int Cuenta { get; set; } = 1;

    [Column("subcuenta"), Display(Name = "Subcuenta")]
    public int Subcuenta { get; set; }

    [Column("fechaalta"), DataType(DataType.Date), Display(Name = "Fecha de alta")]
    public DateTime? FechaAlta { get; set; }

    [Column("fechabaja"), DataType(DataType.Date), Display(Name = "Fecha de baja")]
    public DateTime? FechaBaja { get; set; }

    [Column("cantidad")]
    public int Cantidad { get; set; }

    [Column("rendido")]
    public int Rendido { get; set; }

    [Column("chasis"), StringLength(50)]
    public string Chasis { get; set; } = string.Empty;

    [Column("marcaauto"), StringLength(200), Display(Name = "Marca (automotor)")]
    public string MarcaAuto { get; set; } = string.Empty;

    [Column("modeloauto"), StringLength(200), Display(Name = "Modelo (automotor)")]
    public string ModeloAuto { get; set; } = string.Empty;

    [Column("motor"), StringLength(30)]
    public string Motor { get; set; } = string.Empty;

    [Column("dominio"), StringLength(10)]
    public string Dominio { get; set; } = string.Empty;

    [Column("valorinicial"), Display(Name = "Valor inicial")]
    public decimal ValorInicial { get; set; }

    [Column("color"), StringLength(100)]
    public string Color { get; set; } = string.Empty;

    [Column("legajo")]
    public int Legajo { get; set; }

    [Column("ubicacionfisica"), StringLength(100), Display(Name = "Ubicación física")]
    public string UbicacionFisica { get; set; } = string.Empty;

    [Column("proveedor")]
    public int Proveedor { get; set; }

    [Column("comprobante"), StringLength(20)]
    public string Comprobante { get; set; } = string.Empty;

    [Column("tipocomp"), Display(Name = "Tipo de comprobante")]
    public int TipoComp { get; set; }

    [Column("tipoautoriza"), Display(Name = "Tipo de autorización")]
    public int TipoAutoriza { get; set; }

    public Estado? EstadoNav { get; set; }
    public TipoInstrumento? TipoInstNav { get; set; }
    public TipoComprobante? TipoCompNav { get; set; }
    public TipoAutorizacion? TipoAutorizaNav { get; set; }
    public ICollection<InventarioFoto> Fotos { get; set; } = new List<InventarioFoto>();
}
