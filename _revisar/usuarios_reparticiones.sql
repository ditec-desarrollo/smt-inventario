-- Reparticiones cuyos bienes puede ver cada usuario.
-- Ejecutar ANTES de copiar los archivos nuevos: el modelo ya lee estas columnas.

CREATE TABLE `usuarios_reparticiones` (
  `id_usuario`  INT NOT NULL,
  `reparticion` INT NOT NULL,
  PRIMARY KEY (`id_usuario`, `reparticion`),
  KEY `fk_ur_reparticion` (`reparticion`),
  CONSTRAINT `fk_ur_usuario` FOREIGN KEY (`id_usuario`) REFERENCES `usuarios` (`id_usuario`)
    ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT `fk_ur_reparticion` FOREIGN KEY (`reparticion`) REFERENCES `reparticiones` (`reparticion`)
    ON UPDATE CASCADE ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 1 = ve los bienes de todas las reparticiones (incluidas las que se creen después).
ALTER TABLE `usuarios`
  ADD COLUMN `todas_reparticiones` TINYINT(1) NOT NULL DEFAULT 0 AFTER `id_grupo`;

-- Los usuarios que ya existen quedan con "todas" para que nadie se quede sin ver bienes
-- el día que se instala. Después el administrador les asigna las que correspondan.
-- Los usuarios nuevos arrancan sin ninguna: hasta que se les asignen, no ven bienes.
UPDATE `usuarios` SET `todas_reparticiones` = 1;

-- Para volver atrás:
-- DROP TABLE `usuarios_reparticiones`;
-- ALTER TABLE `usuarios` DROP COLUMN `todas_reparticiones`;
