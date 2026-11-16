namespace InventarioSmt.Servicios;

public class CiudadDigitalOptions
{
    public const string SectionName = "CiudadDigital";

    /// <summary>Endpoint que valida el token y devuelve los datos de la persona.</summary>
    public string AuthStatusUrl { get; set; } = "https://estadisticas.smt.gob.ar:5000/usuarios/authStatus";

    /// <summary>Portal al que se redirige cuando el token es inválido o no existe.</summary>
    public string LoginUrl { get; set; } = "https://ciudaddigital.smt.gob.ar/#/login";

    /// <summary>Nombre de la cookie donde se guarda el token recibido por querystring.</summary>
    public string CookieName { get; set; } = "smt_auth";

    /// <summary>Minutos que se cachea la validación del token para no llamar al portal en cada request.</summary>
    public int MinutosCacheToken { get; set; } = 5;

    /// <summary>
    /// Sólo para desarrollo local sin acceso al portal: omite la validación del token
    /// y usa <see cref="CuilDesarrollo"/> como identidad.
    /// </summary>
    public bool ModoDesarrollo { get; set; }

    public string CuilDesarrollo { get; set; } = string.Empty;

    public string NombreDesarrollo { get; set; } = "Usuario de desarrollo";
}
