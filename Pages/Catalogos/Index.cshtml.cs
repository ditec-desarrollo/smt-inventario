using InventarioSmt.Servicios.Catalogos;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventarioSmt.Pages.Catalogos;

public class IndexModel : PageModel
{
    public IReadOnlyList<CatalogoDefinicion> Catalogos => Catalogo.Todos;
}
