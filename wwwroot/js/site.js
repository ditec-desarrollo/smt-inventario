// Confirmación con SweetAlert2 para cualquier formulario con data-confirmar.
document.addEventListener('submit', function (e) {
    const form = e.target;
    const mensaje = form.dataset.confirmar;
    if (!mensaje || form.dataset.confirmado === '1') return;

    e.preventDefault();
    Swal.fire({
        title: '¿Confirma la operación?',
        text: mensaje,
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#3085d6',
        cancelButtonColor: '#d33',
        confirmButtonText: 'Sí, continuar',
        cancelButtonText: 'Cancelar'
    }).then(function (r) {
        if (r.isConfirmed) {
            form.dataset.confirmado = '1';
            form.submit();
        }
    });
});

// Vista previa y recompresión de las fotos tomadas con la cámara del celular:
// se redimensiona a 1600px de lado mayor y se convierte a JPEG antes de subir.
function prepararCapturaFotos(inputId, previewId, maxLado) {
    const input = document.getElementById(inputId);
    const preview = document.getElementById(previewId);
    if (!input) return;

    input.addEventListener('change', async function () {
        preview.innerHTML = '';
        const archivos = Array.from(input.files || []);
        if (!archivos.length) return;

        const transferencia = new DataTransfer();
        for (const archivo of archivos) {
            const comprimido = await comprimirImagen(archivo, maxLado || 1600);
            transferencia.items.add(comprimido);
            const img = document.createElement('img');
            img.src = URL.createObjectURL(comprimido);
            preview.appendChild(img);
        }
        input.files = transferencia.files;
    });
}

// Legajo: al escribir el número trae el nombre del empleado, y el botón de lupa
// abre el popup de búsqueda por nombre. urlPagina es la ruta de la página, sobre
// la que se llaman los handlers ?handler=Legajo y ?handler=BuscarLegajos.
function prepararLegajo(urlPagina) {
    const numero = document.getElementById('legajoNumero');
    const nombre = document.getElementById('legajoNombre');
    const aviso = document.getElementById('legajoAviso');
    const modal = document.getElementById('modalLegajos');
    if (!numero || !nombre) return;

    const buscarNombre = document.getElementById('buscarNombre');
    const resultado = document.getElementById('resultadoLegajos');

    async function resolverNombre() {
        const valor = parseInt(numero.value, 10);
        aviso.textContent = '';
        if (!valor) { nombre.value = ''; return; }

        try {
            const r = await fetch(urlPagina + '?handler=Legajo&legajo=' + valor, {
                headers: { 'Accept': 'application/json' }
            });
            const datos = await r.json();
            if (datos && datos.nombre) {
                nombre.value = datos.nombre.trim();
            } else {
                nombre.value = '';
                aviso.textContent = 'El legajo no existe en la tabla de legajos.';
            }
        } catch {
            aviso.textContent = 'No se pudo validar el legajo.';
        }
    }

    numero.addEventListener('change', resolverNombre);
    numero.addEventListener('blur', resolverNombre);

    function abrirModal() {
        modal.hidden = false;
        buscarNombre.value = '';
        resultado.innerHTML = '';
        buscarNombre.focus();
    }

    function cerrarModal() { modal.hidden = true; }

    function elegir(legajo, nombreEmpleado) {
        numero.value = legajo;
        nombre.value = (nombreEmpleado || '').trim();
        aviso.textContent = '';
        cerrarModal();
    }

    async function ejecutarBusqueda() {
        const texto = buscarNombre.value.trim();
        if (texto.length < 2) {
            resultado.innerHTML = '<p>Escriba al menos 2 letras.</p>';
            return;
        }

        resultado.innerHTML = '<p>Buscando...</p>';
        try {
            const r = await fetch(
                urlPagina + '?handler=BuscarLegajos&nombre=' + encodeURIComponent(texto),
                { headers: { 'Accept': 'application/json' } });
            const filas = await r.json();

            if (!filas.length) {
                resultado.innerHTML = '<p>Sin resultados.</p>';
                return;
            }

            const tabla = document.createElement('table');
            tabla.className = 'tabla-smt';
            tabla.innerHTML =
                '<thead><tr><th>Legajo</th><th>Nombre</th><th>DNI</th></tr></thead>';
            const cuerpo = document.createElement('tbody');

            filas.forEach(function (f) {
                const tr = document.createElement('tr');
                tr.className = 'fila-elegible';
                [f.legajo, (f.nombre || '').trim(), f.dni ?? ''].forEach(function (v) {
                    const td = document.createElement('td');
                    td.textContent = v;
                    tr.appendChild(td);
                });
                tr.addEventListener('click', function () { elegir(f.legajo, f.nombre); });
                cuerpo.appendChild(tr);
            });

            tabla.appendChild(cuerpo);
            resultado.innerHTML = '';
            resultado.appendChild(tabla);
        } catch {
            resultado.innerHTML = '<p>No se pudo realizar la búsqueda.</p>';
        }
    }

    document.getElementById('btnBuscarLegajo').addEventListener('click', abrirModal);
    document.getElementById('btnCerrarLegajos').addEventListener('click', cerrarModal);
    document.getElementById('btnEjecutarBusqueda').addEventListener('click', ejecutarBusqueda);

    buscarNombre.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') { e.preventDefault(); ejecutarBusqueda(); }
    });

    modal.addEventListener('click', function (e) {
        if (e.target === modal) cerrarModal();
    });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && !modal.hidden) cerrarModal();
    });
}

function prepararImpresionQr() {
    const boton = document.getElementById('btnImprimirQr');
    const todos = document.getElementById('marcarTodos');
    if (!boton) return;

    if (todos) {
        todos.addEventListener('change', function () {
            document.querySelectorAll('.marcar-bien').forEach(function (c) {
                c.checked = todos.checked;
            });
        });
    }

    boton.addEventListener('click', function () {
        const ids = Array.from(document.querySelectorAll('.marcar-bien:checked'))
            .map(function (c) { return c.value; });

        if (!ids.length) {
            alert('Seleccione al menos un bien para imprimir.');
            return;
        }

        window.open(boton.dataset.url + '?ids=' + ids.join(',') + '&auto=1', '_blank', 'noopener');
    });
}

function prepararProveedor(urlPagina) {
    const numero = document.getElementById('proveedorNumero');
    const nombre = document.getElementById('proveedorNombre');
    const aviso = document.getElementById('proveedorAviso');
    const modal = document.getElementById('modalProveedores');
    if (!numero || !nombre) return;

    const buscarTexto = document.getElementById('buscarProveedor');
    const resultado = document.getElementById('resultadoProveedores');

    async function resolverNombre() {
        const valor = parseInt(numero.value, 10);
        aviso.textContent = '';
        if (!valor) { nombre.value = ''; return; }

        try {
            const r = await fetch(urlPagina + '?handler=Proveedor&proveedor=' + valor, {
                headers: { 'Accept': 'application/json' }
            });
            const datos = await r.json();
            if (datos && datos.nombre) {
                nombre.value = datos.nombre.trim();
            } else {
                nombre.value = '';
                aviso.textContent = 'El proveedor no existe en la tabla de proveedores.';
            }
        } catch {
            aviso.textContent = 'No se pudo validar el proveedor.';
        }
    }

    numero.addEventListener('change', resolverNombre);
    numero.addEventListener('blur', resolverNombre);

    function abrirModal() {
        modal.hidden = false;
        buscarTexto.value = '';
        resultado.innerHTML = '';
        buscarTexto.focus();
    }

    function cerrarModal() { modal.hidden = true; }

    function elegir(proveedor, nombreProveedor) {
        numero.value = proveedor;
        nombre.value = (nombreProveedor || '').trim();
        aviso.textContent = '';
        cerrarModal();
    }

    async function ejecutarBusqueda() {
        const texto = buscarTexto.value.trim();
        if (texto.length < 2) {
            resultado.innerHTML = '<p>Escriba al menos 2 caracteres.</p>';
            return;
        }

        resultado.innerHTML = '<p>Buscando...</p>';
        try {
            const r = await fetch(
                urlPagina + '?handler=BuscarProveedores&texto=' + encodeURIComponent(texto),
                { headers: { 'Accept': 'application/json' } });
            const filas = await r.json();

            if (!filas.length) {
                resultado.innerHTML = '<p>Sin resultados.</p>';
                return;
            }

            const tabla = document.createElement('table');
            tabla.className = 'tabla-smt';
            tabla.innerHTML =
                '<thead><tr><th>Código</th><th>Nombre comercial</th><th>Razón social</th><th>CUIT</th></tr></thead>';
            const cuerpo = document.createElement('tbody');

            filas.forEach(function (f) {
                const tr = document.createElement('tr');
                tr.className = 'fila-elegible';
                [f.proveedor, (f.nombre || '').trim(), (f.razonsocial || '').trim(), f.cuit ?? '']
                    .forEach(function (v) {
                        const td = document.createElement('td');
                        td.textContent = v;
                        tr.appendChild(td);
                    });
                tr.addEventListener('click', function () { elegir(f.proveedor, f.nombre); });
                cuerpo.appendChild(tr);
            });

            tabla.appendChild(cuerpo);
            resultado.innerHTML = '';
            resultado.appendChild(tabla);
        } catch {
            resultado.innerHTML = '<p>No se pudo realizar la búsqueda.</p>';
        }
    }

    document.getElementById('btnBuscarProveedor').addEventListener('click', abrirModal);
    document.getElementById('btnCerrarProveedores').addEventListener('click', cerrarModal);
    document.getElementById('btnEjecutarBusquedaProveedor').addEventListener('click', ejecutarBusqueda);

    buscarTexto.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') { e.preventDefault(); ejecutarBusqueda(); }
    });

    modal.addEventListener('click', function (e) {
        if (e.target === modal) cerrarModal();
    });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && !modal.hidden) cerrarModal();
    });
}

function comprimirImagen(archivo, maxLado) {
    return new Promise(function (resolve) {
        if (!archivo.type.startsWith('image/')) { resolve(archivo); return; }

        const lector = new FileReader();
        lector.onload = function () {
            const img = new Image();
            img.onload = function () {
                const escala = Math.min(1, maxLado / Math.max(img.width, img.height));
                const canvas = document.createElement('canvas');
                canvas.width = Math.round(img.width * escala);
                canvas.height = Math.round(img.height * escala);
                canvas.getContext('2d').drawImage(img, 0, 0, canvas.width, canvas.height);
                canvas.toBlob(function (blob) {
                    if (!blob) { resolve(archivo); return; }
                    const nombre = archivo.name.replace(/\.[^.]+$/, '') + '.jpg';
                    resolve(new File([blob], nombre, { type: 'image/jpeg' }));
                }, 'image/jpeg', 0.85);
            };
            img.onerror = function () { resolve(archivo); };
            img.src = lector.result;
        };
        lector.onerror = function () { resolve(archivo); };
        lector.readAsDataURL(archivo);
    });
}

/**
 * Lista desplegable en la que se puede escribir para filtrar (para catálogos largos como
 * el nomenclador). Las opciones vienen en un <script type="application/json"> como
 * [{ t: 'texto', ... }]; alElegir recibe la opción elegida o null si se vacía el campo.
 * Busca todas las palabras escritas, sin importar tildes ni mayúsculas.
 */
function prepararComboBusqueda(cfg) {
    const texto = document.getElementById(cfg.texto);
    const datos = document.getElementById(cfg.datos);
    if (!texto || !datos) return;

    const normalizar = s => (s || '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase();
    const opciones = JSON.parse(datos.textContent).map(o => Object.assign(o, { n: normalizar(o.t) }));
    const maximo = cfg.maximo || 60;

    const lista = document.createElement('ul');
    lista.className = 'combo-lista';
    lista.hidden = true;
    texto.parentNode.appendChild(lista);

    let elegida = opciones.find(o => o.t === texto.value) || null;
    let visibles = [];
    let activa = -1;

    function marcar() {
        Array.from(lista.children).forEach((li, i) => li.classList.toggle('activa', i === activa));
        const li = lista.children[activa];
        if (li) li.scrollIntoView({ block: 'nearest' });
    }

    function mostrar() {
        const palabras = normalizar(texto.value).split(/\s+/).filter(Boolean);
        visibles = opciones.filter(o => palabras.every(p => o.n.includes(p))).slice(0, maximo);
        lista.innerHTML = '';
        visibles.forEach(o => {
            const li = document.createElement('li');
            li.textContent = o.t;
            li.addEventListener('mousedown', e => { e.preventDefault(); elegir(o); });
            lista.appendChild(li);
        });
        if (!visibles.length) {
            const li = document.createElement('li');
            li.className = 'combo-vacio';
            li.textContent = 'Sin coincidencias';
            lista.appendChild(li);
        }
        activa = visibles.length ? 0 : -1;
        lista.hidden = false;
        marcar();
    }

    function elegir(o) {
        elegida = o;
        texto.value = o ? o.t : '';
        lista.hidden = true;
        cfg.alElegir(o);
    }

    texto.addEventListener('focus', () => { texto.select(); mostrar(); });
    texto.addEventListener('input', mostrar);
    texto.addEventListener('keydown', e => {
        if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
            e.preventDefault();
            if (lista.hidden) { mostrar(); return; }
            const paso = e.key === 'ArrowDown' ? 1 : -1;
            activa = Math.max(0, Math.min(visibles.length - 1, activa + paso));
            marcar();
        } else if (e.key === 'Enter' && !lista.hidden) {
            e.preventDefault();
            if (activa >= 0) elegir(visibles[activa]);
        } else if (e.key === 'Escape') {
            texto.value = elegida ? elegida.t : '';
            lista.hidden = true;
        }
    });
    // Al salir: vacío borra la elección; un texto a medio escribir vuelve a la última válida.
    texto.addEventListener('blur', () => {
        lista.hidden = true;
        if (!texto.value.trim()) { elegir(null); return; }
        if (!elegida || texto.value !== elegida.t) texto.value = elegida ? elegida.t : '';
    });
}
