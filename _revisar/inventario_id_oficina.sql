-- Vincula cada bien con la oficina de la estructura organizativa.
-- Ejecutar ANTES de copiar los archivos nuevos: el modelo ya lee la columna id_oficina.
-- No toca los datos existentes: las columnas seccion, subseccion y oficina se conservan
-- y la aplicación las sigue completando con los códigos de la oficina elegida.

ALTER TABLE `inventario`
  ADD COLUMN `id_oficina` INT(10) UNSIGNED NULL DEFAULT NULL AFTER `oficina`,
  ADD INDEX `fk_inventario_oficina` (`id_oficina`),
  ADD CONSTRAINT `fk_inventario_oficina`
    FOREIGN KEY (`id_oficina`) REFERENCES `oficinas` (`id_oficina`)
    ON UPDATE CASCADE ON DELETE RESTRICT;

-- Para volver atrás:
-- ALTER TABLE `inventario` DROP FOREIGN KEY `fk_inventario_oficina`,
--   DROP INDEX `fk_inventario_oficina`, DROP COLUMN `id_oficina`;

-- Diagnóstico (solo consulta, no modifica nada): bienes heredados cuya combinación
-- repartición + sección + subsección + oficina coincide con UNA sola oficina del catálogo.
-- Como inventario no guarda el departamento, si la combinación aparece en más de un
-- departamento la oficina es ambigua y hay que elegirla a mano desde el formulario.
--
-- SELECT i.id, i.reparticion, i.seccion, i.subseccion, i.oficina,
--        COUNT(o.id_oficina) AS coincidencias, MIN(o.id_oficina) AS id_oficina
-- FROM `inventario` i
-- JOIN `departamentos` d ON d.reparticion = CAST(i.reparticion AS DECIMAL(10,0))
-- JOIN `secciones` s     ON s.id_departamento = d.id_departamento AND s.seccion = i.seccion
-- JOIN `subsecciones` b  ON b.id_seccion = s.id_seccion AND b.subseccion = i.subseccion
-- JOIN `oficinas` o      ON o.id_subseccion = b.id_subseccion AND o.oficina = i.oficina
-- WHERE i.id_oficina IS NULL
-- GROUP BY i.id, i.reparticion, i.seccion, i.subseccion, i.oficina;
