-- Ubicación organizativa del bien: departamento (obligatorio), sección, subsección y oficina.
-- Reemplaza a inventario_id_departamento.sql (no correr aquel).
-- Requisito: haber corrido antes inventario_id_oficina.sql (columna id_oficina).
-- Correr de a un paso y mirar el resultado de cada control antes de seguir.

-- 0) Control previo: inventario y staging se relacionan por el mismo id.
--    con_mismo_id y mismo_id_y_identificacion tienen que dar igual (o casi) al total.
SELECT COUNT(*) AS total_inventario,
       SUM(s.id IS NOT NULL) AS con_mismo_id,
       SUM(s.id IS NOT NULL AND COALESCE(TRIM(s.identificacion), '') = COALESCE(TRIM(i.identificacion), '')) AS mismo_id_y_identificacion
FROM inventario i
LEFT JOIN staging_economato s ON s.id = i.id;

-- 1) Columnas nuevas (por ahora admiten NULL), con sus FK.
--    ON UPDATE RESTRICT porque MariaDB no permite un CHECK (paso 6) sobre columnas con
--    FK en cascada; los id son autoincrementales y no cambian, así que no se pierde nada.
--    Por lo mismo, la FK de oficina (creada con CASCADE en inventario_id_oficina.sql) se
--    vuelve a crear con RESTRICT.
ALTER TABLE `inventario` DROP FOREIGN KEY IF EXISTS `fk_inventario_oficina`;

ALTER TABLE `inventario`
  ADD CONSTRAINT `fk_inventario_oficina` FOREIGN KEY (`id_oficina`)
    REFERENCES `oficinas` (`id_oficina`) ON UPDATE RESTRICT ON DELETE RESTRICT,
  ADD COLUMN `id_departamento` INT(10) UNSIGNED NULL DEFAULT NULL AFTER `reparticion`,
  ADD COLUMN `id_seccion`      INT(10) UNSIGNED NULL DEFAULT NULL AFTER `id_departamento`,
  ADD COLUMN `id_subseccion`   INT(10) UNSIGNED NULL DEFAULT NULL AFTER `id_seccion`,
  ADD INDEX `fk_inventario_departamento` (`id_departamento`),
  ADD INDEX `fk_inventario_seccion` (`id_seccion`),
  ADD INDEX `fk_inventario_subseccion` (`id_subseccion`),
  ADD CONSTRAINT `fk_inventario_departamento` FOREIGN KEY (`id_departamento`)
    REFERENCES `departamentos` (`id_departamento`) ON UPDATE RESTRICT ON DELETE RESTRICT,
  ADD CONSTRAINT `fk_inventario_seccion` FOREIGN KEY (`id_seccion`)
    REFERENCES `secciones` (`id_seccion`) ON UPDATE RESTRICT ON DELETE RESTRICT,
  ADD CONSTRAINT `fk_inventario_subseccion` FOREIGN KEY (`id_subseccion`)
    REFERENCES `subsecciones` (`id_subseccion`) ON UPDATE RESTRICT ON DELETE RESTRICT;

-- 2) Bienes que ya tienen oficina: la cadena completa sale de la oficina.
UPDATE inventario i
JOIN oficinas o     ON o.id_oficina = i.id_oficina
JOIN subsecciones b ON b.id_subseccion = o.id_subseccion
JOIN secciones s    ON s.id_seccion = b.id_seccion
SET i.id_subseccion = b.id_subseccion,
    i.id_seccion = s.id_seccion,
    i.id_departamento = s.id_departamento;

-- 3) El resto: departamento desde staging, con las mismas expresiones con que se armó
--    departamentos (vía oficinasprevio). detalle es latin1 y staging utf8mb4: se convierte.
--    El CASE evita convertir reparticiones vacías o no numéricas: en modo estricto
--    (STRICT_TRANS_TABLES) un CAST de '' corta el UPDATE con error 1292.
UPDATE inventario i
JOIN staging_economato s ON s.id = i.id
JOIN departamentos d
  ON d.reparticion = CASE WHEN TRIM(s.reparticion) REGEXP '^[0-9]+([.]0+)?$'
                         THEN CAST(REPLACE(TRIM(s.reparticion), '.0', '') AS UNSIGNED) END
 AND CONVERT(d.detalle USING utf8mb4) COLLATE utf8mb4_unicode_ci = TRIM(s.oficina)
SET i.id_departamento = d.id_departamento
WHERE i.id_departamento IS NULL;

-- 4) Los que no cruzaron van a un departamento "SIN ASIGNAR" (coddep = 0) de su repartición.
-- 4a) Control: si alguna repartición YA tiene un coddep 0 con otro significado, aparece acá
--     (en ese caso avisar antes de seguir, porque el paso 4b la reutilizaría).
SELECT d.id_departamento, d.reparticion, d.coddep, d.detalle
FROM departamentos d
WHERE d.coddep = 0;

-- 4b) Crea el "SIN ASIGNAR" en las reparticiones que lo necesitan.
INSERT IGNORE INTO departamentos (reparticion, coddep, detalle)
SELECT DISTINCT r.reparticion, 0, 'SIN ASIGNAR'
FROM inventario i
JOIN reparticiones r ON r.reparticion = CASE WHEN TRIM(i.reparticion) REGEXP '^[0-9]+([.]0+)?$' THEN CAST(TRIM(i.reparticion) AS DECIMAL(10,0)) END
WHERE i.id_departamento IS NULL;

-- 4c) Se los asigna.
UPDATE inventario i
JOIN departamentos d ON d.reparticion = CASE WHEN TRIM(i.reparticion) REGEXP '^[0-9]+([.]0+)?$' THEN CAST(TRIM(i.reparticion) AS DECIMAL(10,0)) END
                     AND d.coddep = 0
SET i.id_departamento = d.id_departamento
WHERE i.id_departamento IS NULL;

-- 5) Control: tiene que devolver CERO filas. Si devuelve alguna, son bienes sin repartición
--    válida (vacía o que no está en reparticiones): corregirlos antes del paso 6.
SELECT id, identificacion, reparticion
FROM inventario
WHERE id_departamento IS NULL;

-- 6) Departamento obligatorio y reglas de la jerarquía:
--    no hay subsección sin sección ni oficina sin subsección.
--    MariaDB 10.4 no deja cambiar NULL/NOT NULL de una columna con FK (error 1832):
--    se borra la FK y se vuelve a crear en la misma sentencia del MODIFY.
--    Si la segunda sentencia falla, los datos quedan intactos y se pueden repetir las dos.
ALTER TABLE `inventario` DROP FOREIGN KEY IF EXISTS `fk_inventario_departamento`;

ALTER TABLE `inventario`
  MODIFY COLUMN `id_departamento` INT(10) UNSIGNED NOT NULL,
  ADD CONSTRAINT `fk_inventario_departamento` FOREIGN KEY (`id_departamento`)
    REFERENCES `departamentos` (`id_departamento`) ON UPDATE RESTRICT ON DELETE RESTRICT,
  ADD CONSTRAINT `ck_inventario_subseccion` CHECK (`id_subseccion` IS NULL OR `id_seccion` IS NOT NULL),
  ADD CONSTRAINT `ck_inventario_oficina` CHECK (`id_oficina` IS NULL OR `id_subseccion` IS NOT NULL);

-- 7) Resumen final.
SELECT COUNT(*) AS total,
       SUM(id_seccion IS NOT NULL) AS con_seccion,
       SUM(id_subseccion IS NOT NULL) AS con_subseccion,
       SUM(id_oficina IS NOT NULL) AS con_oficina,
       SUM(d.coddep = 0) AS en_sin_asignar
FROM inventario i
JOIN departamentos d ON d.id_departamento = i.id_departamento;

-- Para volver atrás:
-- ALTER TABLE `inventario`
--   DROP CONSTRAINT `ck_inventario_subseccion`, DROP CONSTRAINT `ck_inventario_oficina`,
--   DROP FOREIGN KEY `fk_inventario_departamento`, DROP FOREIGN KEY `fk_inventario_seccion`,
--   DROP FOREIGN KEY `fk_inventario_subseccion`;
-- ALTER TABLE `inventario`
--   DROP INDEX `fk_inventario_departamento`, DROP INDEX `fk_inventario_seccion`,
--   DROP INDEX `fk_inventario_subseccion`,
--   DROP COLUMN `id_departamento`, DROP COLUMN `id_seccion`, DROP COLUMN `id_subseccion`;
-- DELETE FROM departamentos WHERE coddep = 0 AND detalle = 'SIN ASIGNAR';
