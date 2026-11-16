using InventarioSmt.Models;
using InventarioSmt.Servicios;
using Microsoft.EntityFrameworkCore;

namespace InventarioSmt.Data;

public class AppDbContext : DbContext
{
    private readonly SesionUsuario? _sesion;

    public AppDbContext(DbContextOptions<AppDbContext> options, SesionUsuario? sesion = null) : base(options)
    {
        _sesion = sesion;
    }

    /// <summary>
    /// Reparticiones (como texto) cuyos bienes puede ver el usuario del request; null = todas.
    /// Lo usa el filtro global de inventario, así ninguna consulta muestra bienes ajenos.
    /// </summary>
    public IReadOnlyList<string>? ReparticionesFiltro => _sesion?.ReparticionesTexto;

    public DbSet<Inventario> Inventario => Set<Inventario>();
    public DbSet<InventarioFoto> InventarioFotos => Set<InventarioFoto>();
    public DbSet<Estado> Estados => Set<Estado>();
    public DbSet<TipoInstrumento> TiposInstrumento => Set<TipoInstrumento>();
    public DbSet<TipoComprobante> TiposComprobante => Set<TipoComprobante>();
    public DbSet<TipoAutorizacion> TiposAutorizacion => Set<TipoAutorizacion>();
    public DbSet<TipoAdquisicion> TiposAdquisicion => Set<TipoAdquisicion>();
    public DbSet<Nomenclador> Nomenclador => Set<Nomenclador>();
    public DbSet<Departamento> Departamentos => Set<Departamento>();
    public DbSet<Reparticion> Reparticiones => Set<Reparticion>();
    public DbSet<Legajo> Legajos => Set<Legajo>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Grupo> Grupos => Set<Grupo>();
    public DbSet<CardMenu> CardMenu => Set<CardMenu>();
    public DbSet<UsuarioAcceso> UsuariosAccesos => Set<UsuarioAcceso>();
    public DbSet<GrupoAcceso> GruposAccesos => Set<GrupoAcceso>();
    public DbSet<UsuarioReparticion> UsuariosReparticiones => Set<UsuarioReparticion>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Estado>().HasKey(e => e.Codigo);
        b.Entity<TipoInstrumento>().HasKey(e => e.Codigo);
        b.Entity<TipoComprobante>().HasKey(e => e.Codigo);
        b.Entity<TipoAutorizacion>().HasKey(e => e.Codigo);
        b.Entity<TipoAdquisicion>().HasKey(e => e.Codigo);
        b.Entity<Departamento>().HasKey(x => x.Id);
        b.Entity<Nomenclador>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Cuenta, x.Subcuenta }).IsUnique();
        });

        b.Entity<Reparticion>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Codigo).IsUnique();
        });

        b.Entity<Proveedor>(e =>
        {
            e.HasKey(x => x.Numero);
            e.Property(x => x.Numero).ValueGeneratedNever();
        });

        b.Entity<Legajo>(e =>
        {
            e.HasKey(x => x.Numero);
            e.Property(x => x.Numero).ValueGeneratedNever();
            e.Property(x => x.NroReg).ValueGeneratedOnAdd();
        });

        b.Entity<Inventario>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ValorInicial).HasPrecision(12, 2);
            e.HasOne(x => x.EstadoNav).WithMany().HasForeignKey(x => x.Estado);
            e.HasOne(x => x.TipoInstNav).WithMany().HasForeignKey(x => x.TipoInst);
            e.HasOne(x => x.TipoCompNav).WithMany().HasForeignKey(x => x.TipoComp);
            e.HasOne(x => x.TipoAutorizaNav).WithMany().HasForeignKey(x => x.TipoAutoriza);
            e.HasMany(x => x.Fotos).WithOne(f => f.Inventario).HasForeignKey(f => f.InventarioId);
            e.HasQueryFilter(x => ReparticionesFiltro == null || ReparticionesFiltro.Contains(x.Reparticion!));
        });

        b.Entity<InventarioFoto>(e =>
        {
            e.HasKey(x => x.IdFoto);
            e.HasQueryFilter(f => ReparticionesFiltro == null || ReparticionesFiltro.Contains(f.Inventario!.Reparticion!));
            e.Property(x => x.FechaAlta).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        b.Entity<Usuario>(e =>
        {
            e.HasKey(x => x.IdUsuario);
            e.HasIndex(x => x.Cuil).IsUnique();
            e.HasOne(x => x.Grupo).WithMany().HasForeignKey(x => x.IdGrupo);
        });

        b.Entity<Grupo>().HasKey(x => x.IdGrupo);
        b.Entity<CardMenu>().HasKey(x => x.Id);

        b.Entity<UsuarioAcceso>(e =>
        {
            e.HasKey(x => new { x.IdUsuario, x.IdCard });
            e.HasOne(x => x.Usuario).WithMany(u => u.AccesosIndividuales).HasForeignKey(x => x.IdUsuario);
            e.HasOne(x => x.Card).WithMany().HasForeignKey(x => x.IdCard);
        });

        b.Entity<UsuarioReparticion>(e =>
        {
            e.HasKey(x => new { x.IdUsuario, x.Reparticion });
            e.HasOne(x => x.Usuario).WithMany(u => u.Reparticiones).HasForeignKey(x => x.IdUsuario);
        });

        b.Entity<GrupoAcceso>(e =>
        {
            e.HasKey(x => new { x.IdGrupo, x.IdCard });
            e.HasOne(x => x.Grupo).WithMany(g => g.Accesos).HasForeignKey(x => x.IdGrupo);
            e.HasOne(x => x.Card).WithMany().HasForeignKey(x => x.IdCard);
        });
    }
}
