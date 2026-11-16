using System.Security.Cryptography;
using InventarioSmt.Data;
using InventarioSmt.Models;
using Microsoft.EntityFrameworkCore;
using ImageMagick;

namespace InventarioSmt.Servicios;

public class FotoService
{
    public const long TamanoMaximoBytes = 12 * 1024 * 1024;
    private const int LadoMaximo = 1600;
    private const int LadoThumb = 320;

    private static readonly string[] TiposPermitidos = { "image/jpeg", "image/png", "image/webp", "image/heic" };

    static FotoService()
    {
        // Evita imágenes con dimensiones absurdas que agotarían la memoria al decodificarlas.
        ResourceLimits.Width = 20000;
        ResourceLimits.Height = 20000;
    }

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _entorno;

    public FotoService(AppDbContext db, IWebHostEnvironment entorno)
    {
        _db = db;
        _entorno = entorno;
    }

    public record Resultado(bool Ok, string? Error, InventarioFoto? Foto);

    public async Task<Resultado> GuardarAsync(int idInventario, IFormFile archivo, string? cuil,
        CancellationToken ct = default)
    {
        if (archivo.Length == 0) return new Resultado(false, "El archivo está vacío.", null);
        if (archivo.Length > TamanoMaximoBytes)
            return new Resultado(false, "La imagen supera el tamaño máximo permitido (12 MB).", null);
        if (!TiposPermitidos.Contains(archivo.ContentType, StringComparer.OrdinalIgnoreCase))
            return new Resultado(false, "Formato no permitido. Sólo se aceptan imágenes.", null);

        using var origen = new MemoryStream();
        await archivo.CopyToAsync(origen, ct);
        origen.Position = 0;

        var hash = Convert.ToHexString(await SHA256.HashDataAsync(origen, ct)).ToLowerInvariant();
        origen.Position = 0;

        var yaExiste = await _db.InventarioFotos
            .AnyAsync(f => f.InventarioId == idInventario && f.HashSha256 == hash, ct);
        if (yaExiste) return new Resultado(false, "Esa foto ya fue cargada para este bien.", null);

        // El formato se decide por la firma del archivo, no por lo que declara el navegador,
        // para que ImageMagick no interprete otros tipos (SVG, PDF, etc.).
        var formato = DetectarFormato(origen.GetBuffer().AsSpan(0, (int)origen.Length));
        if (formato is null) return new Resultado(false, "El archivo no es una imagen válida.", null);

        MagickImage imagen;
        try
        {
            imagen = new MagickImage(origen, new MagickReadSettings { Format = formato.Value });
        }
        catch (MagickException)
        {
            return new Resultado(false, "El archivo no es una imagen válida.", null);
        }

        using (imagen)
        {
            imagen.AutoOrient();
            if (imagen.Width > LadoMaximo || imagen.Height > LadoMaximo)
                imagen.Resize(new MagickGeometry(LadoMaximo, LadoMaximo));

            // JPEG no tiene transparencia: lo transparente de un PNG queda blanco.
            imagen.BackgroundColor = MagickColors.White;
            imagen.Alpha(AlphaOption.Remove);
            imagen.Format = MagickFormat.Jpeg;
            imagen.Quality = 82;

            var carpetaRelativa = Path.Combine("uploads", "inventario", idInventario.ToString());
            var carpetaFisica = Path.Combine(_entorno.WebRootPath, carpetaRelativa);
            Directory.CreateDirectory(carpetaFisica);

            var nombre = $"{Guid.NewGuid():N}.jpg";
            var nombreThumb = $"thumb_{nombre}";

            await imagen.WriteAsync(Path.Combine(carpetaFisica, nombre), ct);

            using (var thumb = imagen.Clone())
            {
                if (thumb.Width > LadoThumb || thumb.Height > LadoThumb)
                    thumb.Resize(new MagickGeometry(LadoThumb, LadoThumb));
                await thumb.WriteAsync(Path.Combine(carpetaFisica, nombreThumb), ct);
            }

            var esPrimera = !await _db.InventarioFotos
                .AnyAsync(f => f.InventarioId == idInventario && f.Estado == 0, ct);
            var maxOrden = await _db.InventarioFotos
                .Where(f => f.InventarioId == idInventario)
                .Select(f => (short?)f.Orden).MaxAsync(ct) ?? 0;

            var foto = new InventarioFoto
            {
                InventarioId = idInventario,
                Archivo = ToUrl(Path.Combine(carpetaRelativa, nombre)),
                ArchivoThumb = ToUrl(Path.Combine(carpetaRelativa, nombreThumb)),
                NombreOriginal = Path.GetFileName(archivo.FileName),
                ContentType = "image/jpeg",
                TamanoBytes = (int)new FileInfo(Path.Combine(carpetaFisica, nombre)).Length,
                Ancho = (short)imagen.Width,
                Alto = (short)imagen.Height,
                HashSha256 = hash,
                Principal = esPrimera,
                Orden = (short)(maxOrden + 1),
                Estado = 0,
                UsuarioCuil = cuil,
                FechaAlta = DateTime.Now
            };

            _db.InventarioFotos.Add(foto);
            await _db.SaveChangesAsync(ct);
            return new Resultado(true, null, foto);
        }
    }

    public async Task MarcarPrincipalAsync(int idInventario, int idFoto, CancellationToken ct = default)
    {
        var fotos = await _db.InventarioFotos
            .Where(f => f.InventarioId == idInventario).ToListAsync(ct);
        foreach (var f in fotos) f.Principal = f.IdFoto == idFoto;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Baja lógica de la foto; el archivo se conserva en disco.</summary>
    public async Task EliminarAsync(int idFoto, CancellationToken ct = default)
    {
        var foto = await _db.InventarioFotos.FirstOrDefaultAsync(f => f.IdFoto == idFoto, ct);
        if (foto is null) return;

        foto.Estado = 9;
        foto.Principal = false;
        await _db.SaveChangesAsync(ct);

        var quedaPrincipal = await _db.InventarioFotos
            .AnyAsync(f => f.InventarioId == foto.InventarioId && f.Estado == 0 && f.Principal, ct);
        if (!quedaPrincipal)
        {
            var siguiente = await _db.InventarioFotos
                .Where(f => f.InventarioId == foto.InventarioId && f.Estado == 0)
                .OrderBy(f => f.Orden).FirstOrDefaultAsync(ct);
            if (siguiente is not null)
            {
                siguiente.Principal = true;
                await _db.SaveChangesAsync(ct);
            }
        }
    }

    private static MagickFormat? DetectarFormato(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return MagickFormat.Jpeg;
        if (b.Length >= 8 && b[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return MagickFormat.Png;
        if (b.Length >= 12 && b[..4].SequenceEqual("RIFF"u8) && b.Slice(8, 4).SequenceEqual("WEBP"u8))
            return MagickFormat.WebP;
        if (b.Length >= 12 && b.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            var marca = b.Slice(8, 4);
            if (marca.SequenceEqual("heic"u8) || marca.SequenceEqual("heix"u8) ||
                marca.SequenceEqual("mif1"u8) || marca.SequenceEqual("msf1"u8))
                return MagickFormat.Heic;
        }
        return null;
    }

    private static string ToUrl(string ruta) => ruta.Replace(Path.DirectorySeparatorChar, '/');
}
