using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace InventarioSmt.Servicios;

/// <summary>
/// Mantiene la cadena vacía en lugar de convertirla a null, porque las columnas de
/// texto NOT NULL del inventario admiten "" pero no NULL.
/// </summary>
public class CadenaVaciaMetadataProvider : IDisplayMetadataProvider
{
    public void CreateDisplayMetadata(DisplayMetadataProviderContext context)
    {
        context.DisplayMetadata.ConvertEmptyStringToNull = false;
    }
}
