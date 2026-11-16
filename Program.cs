using System.Globalization;
using InventarioSmt.Data;
using InventarioSmt.Servicios;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var cadena = builder.Configuration.GetConnectionString("MySql");
if (string.IsNullOrWhiteSpace(cadena))
{
    throw new InvalidOperationException(
        "Falta la cadena de conexión 'MySql'. Configúrela con User Secrets o la variable de entorno " +
        "ConnectionStrings__MySql; no se guardan credenciales en el repositorio.");
}

// La versión se toma de configuración para no requerir el servidor en tiempo de arranque.
var version = ServerVersion.Parse(builder.Configuration["MySql:ServerVersion"] ?? "10.6.0-mariadb");

builder.Services.AddDbContext<AppDbContext>(o => o.UseMySql(cadena, version));

builder.Services.Configure<CiudadDigitalOptions>(
    builder.Configuration.GetSection(CiudadDigitalOptions.SectionName));

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<CiudadDigitalService>(c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddScoped<SesionUsuario>();
builder.Services.AddScoped<MenuService>();
builder.Services.AddScoped<FotoService>();
builder.Services.AddSingleton<QrService>();
builder.Services.AddSingleton<PdfService>();
builder.Services.AddScoped<InventarioSmt.Servicios.Catalogos.CatalogoService>();

// Las columnas NOT NULL del inventario admiten cadena vacía: no se exige valor por el
// solo hecho de que la propiedad sea string no anulable.
builder.Services.AddRazorPages()
    .AddMvcOptions(o =>
    {
        o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        o.ModelMetadataDetailsProviders.Add(new CadenaVaciaMetadataProvider());
    });

var app = builder.Build();

var cultura = new CultureInfo("es-AR");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(cultura),
    SupportedCultures = new[] { cultura },
    SupportedUICultures = new[] { cultura }
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseMiddleware<AutenticacionMiddleware>();

app.MapRazorPages();

app.Run();
