-- =====================================================================
-- 1) MariaDB: tabla nomenclador
-- =====================================================================
-- id_nomenclador es la clave del catálogo; (cuenta, subcuenta) es la clave
-- funcional, única, y es la que guarda inventario.
CREATE TABLE `nomenclador` (
  `id_nomenclador` INT UNSIGNED NOT NULL AUTO_INCREMENT,
  `cuenta`         INT          NOT NULL,
  `subcuenta`      INT          NOT NULL,
  `descripcion`    VARCHAR(200) NOT NULL DEFAULT '',
  `estado`         TINYINT      NOT NULL DEFAULT 0 COMMENT '0 activo, 1 baja lógica',
  PRIMARY KEY (`id_nomenclador`),
  UNIQUE KEY `uq_nomenclador_cuenta` (`cuenta`, `subcuenta`),
  KEY `ix_nomenclador_descripcion` (`descripcion`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================================
-- 2) SQL Server: controlar que no haya cuenta/subcuenta repetidas
--    (si devuelve filas, hay que depurarlas antes: el UNIQUE las rechaza)
-- =====================================================================
-- SELECT CUENTA, SUBCUENTA, COUNT(*) AS veces
-- FROM dbo.NOMENCLADOR
-- GROUP BY CUENTA, SUBCUENTA
-- HAVING COUNT(*) > 1;

-- =====================================================================
-- 3) SQL Server: SELECT que arma los INSERT para MariaDB
--    Ejecutar en SSMS con resultados en grilla, copiar la columna entera
--    (clic en el encabezado, Ctrl+C) y pegarla en HeidiSQL sobre la base MariaDB.
--    - RTRIM porque descripcion es char(200) y viene rellena con espacios.
--    - Se duplican las comillas simples y las barras invertidas (en MariaDB
--      la barra es carácter de escape dentro de las cadenas).
--    - descripcion NULL pasa como cadena vacía.
-- =====================================================================
-- SET NOCOUNT ON;
-- SELECT 'INSERT INTO `nomenclador` (`cuenta`, `subcuenta`, `descripcion`) VALUES ('
--      + CAST(CUENTA AS varchar(10)) + ', '
--      + CAST(SUBCUENTA AS varchar(10)) + ', '''
--      + REPLACE(REPLACE(LTRIM(RTRIM(ISNULL(descripcion, ''))), '\', '\\'), '''', '''''')
--      + ''');' AS sentencia
-- FROM dbo.NOMENCLADOR
-- ORDER BY CUENTA, SUBCUENTA;

-- =====================================================================
-- 4) MariaDB, después de cargar los datos: controlar la cantidad
--    (tiene que dar lo mismo que SELECT COUNT(*) FROM dbo.NOMENCLADOR)
-- =====================================================================
-- SELECT COUNT(*) FROM `nomenclador`;

-- =====================================================================
-- 5) MariaDB: cuenta y subcuenta del nomenclador en cada bien
--    Ejecutar ANTES de copiar los archivos nuevos: el modelo ya lee estas columnas.
--    Admiten NULL (bienes todavía sin clasificar). Con una sola de las dos en NULL
--    la FK no controla nada, por eso la aplicación exige las dos o ninguna.
-- =====================================================================
ALTER TABLE `inventario`
  ADD COLUMN `cuenta`    INT NULL DEFAULT NULL,
  ADD COLUMN `subcuenta` INT NULL DEFAULT NULL,
  ADD INDEX `fk_inventario_nomenclador` (`cuenta`, `subcuenta`),
  ADD CONSTRAINT `fk_inventario_nomenclador`
    FOREIGN KEY (`cuenta`, `subcuenta`) REFERENCES `nomenclador` (`cuenta`, `subcuenta`)
    ON UPDATE CASCADE ON DELETE RESTRICT;

-- Para volver atrás:
-- ALTER TABLE `inventario` DROP FOREIGN KEY `fk_inventario_nomenclador`,
--   DROP INDEX `fk_inventario_nomenclador`, DROP COLUMN `cuenta`, DROP COLUMN `subcuenta`;
-- DROP TABLE `nomenclador`;
