-- Agrega el estado "SOLICITUD DE BAJA" si todavía no existe. Se puede correr más de una vez.
INSERT INTO estados (detalle)
SELECT 'SOLICITUD DE BAJA' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM estados WHERE detalle = 'SOLICITUD DE BAJA');

SELECT * FROM estados ORDER BY estado;
