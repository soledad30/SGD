# Guia de Usuario Completa - GestorDocumentoApp

Esta guia esta pensada para alguien que entra por primera vez y no sabe:
- donde hacer clic,
- que campo llenar,
- por que hacerlo.

La idea es que puedas operar el sistema sin perderte.

---

## 1) Que hace este software (en palabras simples)

GestorDocumentoApp sirve para controlar cambios de documentos/artefactos de proyecto con trazabilidad.

Flujo principal:
1. Creas **Proyecto**.
2. Creas **Elemento (CI)** dentro del proyecto.
3. Registras una **Solicitud de Cambio (CR)** para ese elemento.
4. Vinculas una **Version** al cambio.
5. Vinculas evidencia Git (**commit/PR**).
6. Llevas el cambio a **linea base (baseline)**.

Si haces esto bien, puedes demostrar:
- que cambio se hizo,
- por que se hizo,
- quien lo aprobo,
- y en que version quedo implementado.

---

## 2) Donde entrar primero (menu principal)

En la barra superior veras:
- **Proyectos** -> para crear y administrar proyectos.
- **Elementos** -> para listar/crear CI por separado.
- **Menu (icono hamburguesa)** -> Dashboard, Notificaciones, Configuracion.

En **Configuracion** (`Register/Index`) puedes entrar a:
- Tipo de elemento,
- Tipo de requerimiento,
- Usuarios y roles.

---

## 3) Paso 0 recomendado: preparar catalogos

Antes de crear datos del flujo, entra a **Configuracion** y revisa:
- Tipos de elemento (ej. Documento, Modulo, Manual),
- Tipos de requerimiento (ej. Funcional, No funcional).

Por que hacerlo:
- evita valores inconsistentes,
- facilita filtros y reportes.

---

## 4) Paso 1: crear o usar una cuenta

### Registro
- Pantalla: `Account/Register`
- Campos:
  - **Correo**: obligatorio y valido.
  - **Contrasena**: minimo 8, con mayuscula, numero y simbolo.
  - **Confirmar contrasena**: igual a la anterior.
  - **Aceptar terminos**: obligatorio.

### Login
- Pantalla: `Account/Login`
- Campos:
  - Correo
  - Contrasena
  - (Opcional) recordar sesion

Por que:
- todo se guarda por usuario; cada usuario ve y gestiona su propio alcance.

---

## 5) Paso 2: crear un proyecto

Menu: **Proyectos** -> **Crear**

Campos:
- **Nombre** (obligatorio)
- **Fecha de creacion** (obligatorio)
- **Descripcion** (opcional)

Por que:
- el proyecto agrupa los elementos y su historial.

Ejemplo:
- Nombre: `Sistema de Ventas`
- Fecha: actual
- Descripcion: `Control de requisitos y documentos del sistema`

---

## 6) Paso 3: crear elementos (CI)

Puedes crearlos de dos formas:
- desde **Elementos > Crear**, o
- desde **Proyecto > Ver > Crear** (asignado al proyecto actual).

Campos principales:
- **Nombre** (obligatorio)
- **Fecha de creacion** (obligatorio)
- **Descripcion** (opcional)
- **Proyecto** (obligatorio si creas desde modulo Elementos)
- **Tipo de elemento** (recomendado)

Por que:
- el elemento es el objeto que sufre cambios y versiones.

Ejemplos de CI:
- Manual de usuario
- Diseno de BD
- Modulo de login
- Documento de pruebas

---

## 7) Paso 4: gestionar miembros del proyecto (si trabajas en equipo)

Ruta: `Proyecto > Show > Miembros`

Que puedes hacer:
- agregar miembro por email,
- asignar rol,
- marcar permisos:
  - **Puede editar**
  - **Puede aprobar**
- activar/desactivar miembro.

Por que:
- define responsabilidades reales de trabajo y aprobacion.

---

## 8) Paso 5: crear solicitud de cambio (CR)

Puedes crear CR desde:
- modulo **Solicitud de Cambio**, o
- boton de agregar cambio dentro del elemento.

Campos (importantes):
- **Codigo** (obligatorio): identificador unico, por ejemplo `CR-2026-001`.
- **Descripcion**: que se cambia y por que.
- **Observaciones**: notas de soporte.
- **Clasificacion** (obligatorio)
- **Prioridad** (obligatorio)
- **Proceso / Status** (obligatorio): estado del flujo.
- **Estado / Action** (obligatorio): resultado de gestion.
- **Elemento (CI)** (obligatorio)

Reglas clave:
- no puedes crear una CR nueva directamente en `Baselined`,
- para baselinar luego, necesitas evidencia Git valida.

---

## 9) Paso 6: avanzar la CR en su flujo

El sistema valida transiciones en orden. Flujo esperado:

1. Initiated
2. Received
3. Analyzed
4. Action
5. Assigned
6. Checkout
7. ModifiedAndTested
8. Reviewed
9. Approved
10. Checkin
11. Baselined

Si saltas pasos, veras: **"Transicion de estado no valida"**.

Por que:
- asegura trazabilidad coherente y auditable.

---

## 10) Paso 7: aprobacion formal (muy importante)

En `Detalle de CR` veras la seccion **Workflow de aprobacion**.

### Solicitar aprobacion
Llenar:
- **Responsable (UserId)**: quien decide.
- **SLA (horas)**: entre 1 y 720.
Luego clic en **Solicitar aprobacion**.

### Aprobar / Rechazar
Siempre se ven, pero solo se habilitan cuando:
- la aprobacion esta en estado **Pendiente**, y
- el usuario logueado es el **responsable asignado**.

Si no se cumple, quedan deshabilitados y aparece mensaje explicativo.

Por que:
- evita que otro usuario decida fuera de control.

---

## 11) Paso 8: crear version del elemento

Ruta habitual:
- `Proyecto > Show > clic en Elemento > Versiones > Crear`.

Campos obligatorios:
- **Nombre**
- **ElementUrl** (URL al documento/artefacto)
- **Codigo de version** (ej. `v1.0.0`)
- **Fase** (1..6)
- **Iteracion**
- **Tipo de requerimiento**
- **Peticion de cambio (CR)**

Campos opcionales:
- **ToolUrl**
- **Version anterior**

Importante sobre CR en este formulario:
- la lista de CR no muestra todas,
- solo muestra CR del elemento que esten aprobadas y elegibles.

Si la lista aparece vacia:
- significa que no tienes CR valida para versionar aun.

---

## 12) Paso 9: trazabilidad Git (explicacion clara)

En `Detalle de CR` seccion **Trazabilidad Git (Commit/PR)** debes vincular evidencia tecnica.

Campos:
- **Repositorio**: `owner/repo` o URL de GitHub.
- **Commit SHA**: hash del commit (opcional si usas PR).
- **PR Number**: numero de PR (opcional si ya tienes commit).
- **Version ID**: recomendable para amarrar evidencia-version.
- **PR URL**: opcional.

Regla minima:
- debes registrar al menos commit o PR.

Validaciones del sistema:
- repositorio existente/accesible,
- commit existente (si lo enviaste),
- PR existente (si lo enviaste).

Por que:
- esto justifica tecnicamente el cambio ante auditoria.

---

## 13) Paso 10: llevar a baseline (cierre formal)

Para que una CR llegue a `Baselined`, el sistema exige:
- `Action = Approved`,
- evidencia Git verificable:
  - commit existente, o
  - PR merged.

Tambien puedes baselinar al activar una version (`SetVersion`) vinculada a esa CR.

Resultado:
- la CR queda cerrada en linea base,
- y la trazabilidad queda completa.

---

## 14) Como leer la trazabilidad final

Cadena ideal:

`Proyecto -> Elemento -> CR -> Version -> Commit/PR`

Si esa cadena existe, puedes responder:
- que se cambio,
- cuando,
- por quien,
- bajo que aprobacion,
- en que release/version.

---

## 15) Exportes y evidencia

Desde detalle de CR puedes exportar:
- JSON
- CSV
- Excel
- PDF

Usalos para:
- sustentacion academica,
- auditoria,
- reporte de avance,
- evidencia de entrega.

---

## 16) Errores frecuentes y solucion rapida

- **"La peticion de cambio es requerido" en crear version**
  - No hay CR elegible; crea/actualiza una CR aprobada del mismo elemento.

- **No aparecen o no se habilitan Aprobar/Rechazar**
  - Falta solicitar aprobacion, o no eres el responsable asignado.

- **"Transicion de estado no valida"**
  - estas saltando etapas del workflow de CR.

- **"No puedes baselinar... sin evidencia Git"**
  - falta vincular commit/PR verificable.

- **"La version indicada no pertenece a la CR"**
  - el Version ID cargado en trazabilidad no coincide con esa CR.

---

## 17) Ruta recomendada (checklist corto)

1. Configurar catalogos (tipos).
2. Crear proyecto.
3. Crear elemento.
4. Crear CR.
5. Solicitar y completar aprobacion.
6. Crear version vinculada a CR.
7. Vincular commit/PR en trazabilidad.
8. Baselinar.
9. Exportar evidencia.

Si sigues esta secuencia no te bloqueas en formularios.

---

## 18) Ejemplo completo real (para practicar)

- Proyecto: `App Biblioteca`
- Elemento: `Manual de Usuario`
- CR:
  - Codigo: `CR-2026-015`
  - Clasificacion: adaptativa
  - Prioridad: alta
  - Status: avanzar hasta `Checkin`
  - Action: `Approved`
- Solicitud de aprobacion:
  - Responsable: usuario aprobador
  - SLA: 48
- Version:
  - Nombre: `Manual v2`
  - VersionCode: `v2.0.0`
  - Fase: Implementacion
  - Iteracion: 2
  - CR: `CR-2026-015`
- Trazabilidad Git:
  - Repositorio: `organizacion/repositorio`
  - Commit: `abc1234`
  - PR: `42`
- Baseline final.

Con eso completas el ciclo de gestion de cambio.

