using InventarioSmt.Servicios;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace InventarioSmt.Pages;

public class SinAccesoModel : PageModel
{
    public string LoginUrl { get; }

    public SinAccesoModel(IOptions<CiudadDigitalOptions> opciones) => LoginUrl = opciones.Value.LoginUrl;

    public void OnGet() { }
}
