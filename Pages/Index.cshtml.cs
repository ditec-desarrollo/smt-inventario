using InventarioSmt.Servicios;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventarioSmt.Pages;

public class IndexModel : PageModel
{
    public SesionUsuario Sesion { get; }

    public IndexModel(SesionUsuario sesion) => Sesion = sesion;

    public void OnGet() { }
}
