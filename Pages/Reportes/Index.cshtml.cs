using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventarioSmt.Pages.Reportes;

/// <summary>Menú de reportes. Cada reporte vive bajo /Reportes, así que lo cubre el mismo permiso.</summary>
public class IndexModel : PageModel
{
    public record Reporte(string Pagina, string Titulo, string Icono, string Descripcion);

    public static readonly IReadOnlyList<Reporte> Reportes = new[]
    {
        new Reporte("Bienes", "Reporte de bienes", "summarize",
            "Por repartición, departamento y nomenclador. Imprimir, Excel y PDF."),
        new Reporte("Etiquetas", "Etiquetas QR", "qr_code_2",
            "Imprimir códigos QR en la Zebra e índice de control por departamento."),
        new Reporte("Bajas", "Solicitudes de baja", "delete_sweep",
            "Bienes en Solicitud de Baja por repartición y fecha de baja. PDF y Excel.")
    };

    public void OnGet()
    {
    }
}
