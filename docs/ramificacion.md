# Estrategia de ramificación y políticas del repositorio

## 1. Objetivo

Establecer una estrategia de gestión de configuración del software para el e-commerce de Ingeniería y Calidad del Software mediante Git y GitHub.

La estrategia busca garantizar trazabilidad, colaboración, revisión de código e integración controlada.

## 2. Repositorios del proyecto

- Backend: `Grupo-ICSoft/ICS2026-backend`
- Frontend: `Grupo-ICSoft/ICS2026-frontend`

## 3. Modelo de ramificación

### 3.1. Rama main

Representa la versión estable del proyecto. Los cambios se integran mediante Pull Requests aprobados.

### 3.2. Rama development

Es la rama de integración y la rama predeterminada de los repositorios. Los cambios se incorporan mediante Pull Requests y sus revisiones correspondientes.

### 3.3. Ramas feature

Las ramas `feature/<nombre>` permiten desarrollar funcionalidades, mejoras y tareas técnicas.

Ejemplos:

- `feature/doc-ramificacion`
- `feature/dockerizar-api`
- `feature/pipeline-ci-cd`

Se crean desde `development` y se integran nuevamente mediante Pull Request.

### 3.4. Ramas hotfix

Las ramas `hotfix/<descripcion>` se utilizan para corregir problemas.

Para correcciones urgentes de producción, se propone partir de `main`, integrar allí mediante Pull Request e incorporar posteriormente la corrección a `development`.

Las correcciones no urgentes pueden gestionarse desde `development` conforme a las convenciones acordadas.

## 4. Protección de ramas

Las ramas `main` y `development` utilizan políticas de protección:

- Pull Request obligatorio.
- Al menos una aprobación de otro integrante.
- Invalidación de aprobaciones anteriores cuando se agregan nuevos commits.
- Resolución de conversaciones antes del merge.
- Restricciones aplicables también a administradores.
- Force push deshabilitado.
- Eliminación de ramas protegidas deshabilitada.

## 5. Procedimiento de trabajo

1. Partir de `development` actualizada.
2. Crear una rama `feature/<nombre>`.
3. Implementar los cambios.
4. Registrar commits descriptivos.
5. Publicar la rama en GitHub.
6. Abrir un Pull Request hacia `development`.
7. Solicitar revisión por otro integrante.
8. Resolver observaciones y obtener aprobación.
9. Realizar el merge cuando se cumplan las políticas.
10. Eliminar la rama temporal cuando ya no sea necesaria.

Para publicar cambios estables se utiliza un Pull Request hacia `main`.

## 6. Convenciones de commits

- `feat:` nueva funcionalidad.
- `fix:` corrección de errores.
- `docs:` documentación.
- `ci:` integración y despliegue automatizados.
- `style:` cambios de formato.
- `chore:` mantenimiento.

Ejemplo: `docs: documentar estrategia de ramificacion`

## 7. Revisión por pares

Cada Pull Request debe recibir al menos una aprobación de un integrante diferente del autor.

La integración solo se realiza cuando se cumplen las políticas de revisión y protección establecidas.

