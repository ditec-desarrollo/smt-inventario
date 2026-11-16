using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventarioSmt.Models;

[Table("inventario_fotos")]
public class InventarioFoto
{
    [Column("id_foto")]
    public int IdFoto { get; set; }

    [Column("id")]
    public int InventarioId { get; set; }

    [Column("archivo"), StringLength(260)]
    public string Archivo { get; set; } = string.Empty;

    [Column("archivo_thumb"), StringLength(260)]
    public string? ArchivoThumb { get; set; }

    [Column("nombre_original"), StringLength(150)]
    public string? NombreOriginal { get; set; }

    [Column("content_type"), StringLength(60)]
    public string ContentType { get; set; } = "image/jpeg";

    [Column("tamano_bytes")]
    public int TamanoBytes { get; set; }

    [Column("ancho")]
    public short? Ancho { get; set; }

    [Column("alto")]
    public short? Alto { get; set; }

    [Column("hash_sha256"), StringLength(64)]
    public string? HashSha256 { get; set; }

    [Column("descripcion"), StringLength(150)]
    public string? Descripcion { get; set; }

    [Column("principal")]
    public bool Principal { get; set; }

    [Column("orden")]
    public short Orden { get; set; }

    [Column("estado")]
    public byte Estado { get; set; }

    [Column("usuario_cuil"), StringLength(11)]
    public string? UsuarioCuil { get; set; }

    [Column("fecha_alta")]
    public DateTime FechaAlta { get; set; }

    public Inventario? Inventario { get; set; }
}
