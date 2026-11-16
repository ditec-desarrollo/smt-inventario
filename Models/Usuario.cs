using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventarioSmt.Models;

[Table("usuarios")]
public class Usuario
{
    [Column("id_usuario")]
    public int IdUsuario { get; set; }

    [Column("usuario_apynom"), StringLength(120), Display(Name = "Apellido y nombre")]
    public string ApellidoNombre { get; set; } = string.Empty;

    [Column("usuario_cuil"), StringLength(11)]
    public string Cuil { get; set; } = string.Empty;

    [Column("usuario_nafiliado"), StringLength(20), Display(Name = "N° de afiliado")]
    public string? NroAfiliado { get; set; }

    [Column("id_grupo")]
    public int? IdGrupo { get; set; }

    [Column("estado")]
    public byte Estado { get; set; }

    /// <summary>Ve los bienes de todas las reparticiones, incluidas las que se creen después.</summary>
    [Column("todas_reparticiones"), Display(Name = "Todas las reparticiones")]
    public bool TodasReparticiones { get; set; }

    [Column("accesos"), StringLength(255)]
    public string? Accesos { get; set; }

    [Column("fecha_alta")]
    public DateTime FechaAlta { get; set; }

    [Column("ultimo_acceso")]
    public DateTime? UltimoAcceso { get; set; }

    public Grupo? Grupo { get; set; }
    public ICollection<UsuarioAcceso> AccesosIndividuales { get; set; } = new List<UsuarioAcceso>();
    public ICollection<UsuarioReparticion> Reparticiones { get; set; } = new List<UsuarioReparticion>();
}

/// <summary>Repartición cuyos bienes puede ver el usuario.</summary>
[Table("usuarios_reparticiones")]
public class UsuarioReparticion
{
    [Column("id_usuario")]
    public int IdUsuario { get; set; }

    [Column("reparticion")]
    public int Reparticion { get; set; }

    public Usuario? Usuario { get; set; }
}

[Table("grupos")]
public class Grupo
{
    [Column("id_grupo")]
    public int IdGrupo { get; set; }

    [Column("grupo"), StringLength(60)]
    public string Nombre { get; set; } = string.Empty;

    [Column("detalle"), StringLength(150)]
    public string? Detalle { get; set; }

    [Column("estado")]
    public byte Estado { get; set; }

    public ICollection<GrupoAcceso> Accesos { get; set; } = new List<GrupoAcceso>();
}

[Table("card_menu")]
public class CardMenu
{
    [Column("id")]
    public int Id { get; set; }

    [Column("title"), StringLength(60)]
    public string Title { get; set; } = string.Empty;

    [Column("subtitle"), StringLength(120)]
    public string? Subtitle { get; set; }

    [Column("icon"), StringLength(60)]
    public string? Icon { get; set; }

    [Column("ruta"), StringLength(120)]
    public string? Ruta { get; set; }

    [Column("orden")]
    public int Orden { get; set; }

    [Column("estado")]
    public byte Estado { get; set; }
}

[Table("usuarios_accesos")]
public class UsuarioAcceso
{
    [Column("id_usuario")]
    public int IdUsuario { get; set; }

    [Column("id_card")]
    public int IdCard { get; set; }

    public Usuario? Usuario { get; set; }
    public CardMenu? Card { get; set; }
}

[Table("grupos_accesos")]
public class GrupoAcceso
{
    [Column("id_grupo")]
    public int IdGrupo { get; set; }

    [Column("id_card")]
    public int IdCard { get; set; }

    public Grupo? Grupo { get; set; }
    public CardMenu? Card { get; set; }
}
