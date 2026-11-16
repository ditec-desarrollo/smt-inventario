// Envía las etiquetas en ZPL a la Zebra conectada por USB usando Zebra Browser Print,
// que expone un servicio local en el puerto 9101 (HTTPS) o 9100 (HTTP).
// Si el utilitario no está instalado se ofrece descargar el archivo .zpl.
(function () {
    var bases = ['https://localhost:9101', 'http://localhost:9100'];

    function json(url, opciones) {
        return fetch(url, opciones).then(function (r) {
            if (!r.ok) throw new Error('respuesta ' + r.status);
            return r.json();
        });
    }

    function buscarServicio() {
        var intentos = bases.map(function (base) {
            return json(base + '/default?type=printer').then(function (impresora) {
                if (!impresora || !impresora.uid) throw new Error('sin impresora predeterminada');
                return { base: base, impresora: impresora };
            });
        });

        return Promise.any(intentos);
    }

    function mostrar(estado, texto) {
        if (estado) estado.textContent = texto;
    }

    // opciones: las del fetch del ZPL (p. ej. POST con la lista de bienes).
    // descargar: alternativa a location.href cuando el ZPL se pide por POST.
    window.zebraImprimir = function (urlZpl, estado, opciones, descargar) {
        mostrar(estado, 'Enviando a la impresora...');

        return Promise.all([fetch(urlZpl, opciones).then(function (r) {
            if (!r.ok) throw new Error('zpl ' + r.status);
            return r.text();
        }), buscarServicio()])
            .then(function (resultados) {
                var zpl = resultados[0];
                var servicio = resultados[1];

                return fetch(servicio.base + '/write', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ device: servicio.impresora, data: zpl })
                });
            })
            .then(function () {
                mostrar(estado, 'Etiquetas enviadas.');
            })
            .catch(function () {
                mostrar(estado, '');

                if (confirm('No se encontró Zebra Browser Print en esta PC.\n\n' +
                    'Instalalo una vez desde la página de soporte de Zebra para imprimir ' +
                    'con un clic, o descargá el archivo ZPL ahora.\n\n' +
                    '¿Descargar el archivo ZPL?')) {
                    if (descargar) descargar(); else location.href = urlZpl;
                }
            });
    };
})();
