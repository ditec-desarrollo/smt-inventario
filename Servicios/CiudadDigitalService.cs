using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace InventarioSmt.Servicios;

public record PersonaCiudadDigital(int IdPersona, string DocumentoPersona, string NombrePersona);

/// <summary>
/// Valida el token de Ciudad Digital contra el endpoint authStatus del portal.
/// Es el mismo mecanismo que usa la aplicación React existente.
/// </summary>
public class CiudadDigitalService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly CiudadDigitalOptions _opciones;
    private readonly ILogger<CiudadDigitalService> _log;

    public CiudadDigitalService(HttpClient http, IMemoryCache cache,
        IOptions<CiudadDigitalOptions> opciones, ILogger<CiudadDigitalService> log)
    {
        _http = http;
        _cache = cache;
        _opciones = opciones.Value;
        _log = log;
    }

    public async Task<PersonaCiudadDigital?> ValidarTokenAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var clave = $"authStatus:{token.GetHashCode()}";
        if (_cache.TryGetValue<PersonaCiudadDigital>(clave, out var enCache)) return enCache;

        try
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Get, _opciones.AuthStatusUrl);
            pedido.Headers.TryAddWithoutValidation("Authorization", token);
            pedido.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var respuesta = await _http.SendAsync(pedido, ct);
            if (!respuesta.IsSuccessStatusCode) return null;

            var cuerpo = await respuesta.Content.ReadFromJsonAsync<AuthStatusResponse>(cancellationToken: ct);
            var usuario = cuerpo?.Usuario;
            if (cuerpo?.Message == "Invalid token" || usuario is null ||
                string.IsNullOrWhiteSpace(usuario.DocumentoPersona))
            {
                return null;
            }

            var persona = new PersonaCiudadDigital(
                usuario.IdPersona,
                usuario.DocumentoPersona.Trim(),
                usuario.NombrePersona?.Trim() ?? string.Empty);

            _cache.Set(clave, persona, TimeSpan.FromMinutes(_opciones.MinutosCacheToken));
            return persona;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _log.LogWarning(ex, "No se pudo validar el token contra Ciudad Digital");
            return null;
        }
    }

    private class AuthStatusResponse
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        // El portal devuelve la propiedad con eñe y tilde: "usuarioSinContraseña".
        [JsonPropertyName("usuarioSinContraseña")]
        public UsuarioPortal? Usuario { get; set; }
    }

    private class UsuarioPortal
    {
        [JsonPropertyName("id_persona")]
        public int IdPersona { get; set; }

        [JsonPropertyName("documento_persona")]
        public string? DocumentoPersona { get; set; }

        [JsonPropertyName("nombre_persona")]
        public string? NombrePersona { get; set; }
    }
}
