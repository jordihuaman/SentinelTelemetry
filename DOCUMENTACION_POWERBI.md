# SentinelTelemetry — Documentación técnica y guía de datos para Power BI

> Documento de contexto. Sirve para (a) dar contexto completo a un modelo de IA sobre este
> repositorio y (b) diseñar/construir los informes de Power BI sobre la base de datos.

---

## 1. Resumen ejecutivo

**SentinelTelemetry** es una aplicación de **telemetría local de actividad de usuario para Windows**.
Un servicio en segundo plano (bandeja del sistema) captura continuamente:

- Ventana/aplicación en primer plano.
- Ciclo de vida de aplicaciones (apertura/cierre de procesos con ventana visible).
- Teclado (pulsaciones agregadas por segundo).
- Ratón (posición, clics, movimiento).
- Portapapeles (tipo y longitud del contenido).
- Explorador de archivos (carpeta que se está navegando).
- Sistema de archivos (creación/borrado/renombrado/modificación en carpetas vigiladas).
- Inactividad/reanudación de actividad (idle detection).
- Eventos de sesión: Login, Logout, Lock, Unlock, RemoteConnect, RemoteDisconnect, Suspend, Resume.

Todo se persiste en **SQL Server** en un **modelo genérico de eventos** (tabla `Events` con un
payload `data_json` en JSON) y se expone para analítica mediante **15 vistas SQL** que "desempaquetan"
ese JSON en columnas.

- **Plataforma:** solo Windows (P/Invoke, hooks de teclado/portapapeles, WinForms/WPF, COM Shell32/SHDocVw).
- **Runtime:** .NET 10 (`net10.0` y `net10.0-windows`), `<Nullable>enable</Nullable>`, `ImplicitUsings`.
- **Solución:** `SentinelTelemetry.slnx`.
- **Base de datos:** `SentinelTelemetryDb` en SQL Server (creada automáticamente por el propio servicio).

---

## 2. Arquitectura y estructura de la solución

```
SentinelTelemetry.slnx
├── SentinelTelemetry.Core          (net10.0)              → Modelos + interfaces
├── SentinelTelemetry.Agent         (net10.0-windows)      → Collectors (namespace SentinelTelemetry.Telemetry)
├── SentinelTelemetry.Storage       (net10.0)              → Repositorio SQL Server (namespace SentinelTelemetry.Persistence)
├── SentinelTelemetry.Service       (net10.0-windows, WinExe) → Worker + bandeja del sistema
├── SentinelTelemetry.UI            (net10.0-windows, WPF) → UI vacía (placeholder)
└── SentinelTelemetry.Database      (.sql)                 → Esquema y vistas
```

### 2.1 SentinelTelemetry.Core
- `Models/EventBase.cs` → clase base abstracta de **todos** los eventos.
- `Models/*Event.cs` → un modelo por tipo de evento (ver §4).
- `Interfaces/IEventRepository.cs` → `Task SaveEventAsync(EventBase evento)`.
- `Interfaces/IEventCollector.cs` → contrato de los collectors "pull" (`Capture()`).

### 2.2 SentinelTelemetry.Agent (collectors)
Cada collector produce eventos; el `Worker` los recoge y los guarda.

| Archivo | Tipo | Entrega |
|---|---|---|
| `WindowCollector.cs` | pull | ventana en primer plano |
| `ApplicationCollector.cs` | pull | aperturas/cierres de procesos |
| `PointerCollector.cs` | pull | posición + botones del ratón |
| `KeyboardCollector.cs` | pull | pulsaciones acumuladas |
| `KeyboardHook.cs` | hook | cuenta cada pulsación |
| `ClipboardHook.cs` / `ClipboardCollector.cs` | hook | cambios de portapapeles |
| `ExplorerCollector.cs` | pull | carpetas abiertas en el Explorador (COM ShellWindows) |
| `FileCollector.cs` | FileSystemWatcher | cambios en carpetas vigiladas |
| `IdleCollector.cs` | pull/estado | inicio/fin de inactividad |
| `SessionCollector.cs` | eventos Win32 | login/logout/lock/unlock/etc. |
| `NativeMethods.cs` | — | P/Invoke (GetCursorPos, GetAsyncKeyState, GetForegroundWindow…) |

### 2.3 SentinelTelemetry.Service
- `Program.cs` → `Host.CreateApplicationBuilder`, registra `Worker` (BackgroundService) y
  `PauseController`; crea el `SqlServerEventRepository` **con reintentos** (10 intentos, 3 s)
  y levanta un icono de bandeja (`NotifyIcon`) con menú **Pausar/Reanudar** y **Cerrar**.
- `Worker.cs` → bucle principal: cada **1 segundo** captura, filtra y persiste eventos.
- `PauseController.cs` → pausa/reanudación sin cerrar el servicio.
- `appsettings.json` → cadena de conexión y carpetas vigiladas.

### 2.4 SentinelTelemetry.Storage
- `SqlServerEventRepository.cs`:
  - En el constructor `EnsureDatabaseAndTableCreated()`: crea la BD si no existe y crea las
    tablas `Source`, `Category`, `Version`, `Events` (con `UNIQUE` en los nombres y FKs).
  - `SaveEventAsync(EventBase)`:
    1. Resuelve/crea `id_source`, `id_category`, `id_version` (patrón "get-or-create").
    2. Inserta en `Events` con `data_json = JsonSerializer.Serialize(evento, evento.GetType())`.

> ⚠️ **Nota para IA/Dev:** `SentinelTelemetry.Database/01_Schema_Creation.sql` es un script manual
> y tiene diferencias menores con el esquema que crea el código (le faltan las claves `UNIQUE` y
> una coma antes de las `CONSTRAINT`). **El esquema autoritativo es el que crea
> `SqlServerEventRepository.EnsureDatabaseAndTableCreated()`.**

---

## 3. Modelo de datos (SQL Server)

### 3.1 Diagrama lógico

```
Category (id_category PK)      Source (id_source PK)        Version (id_version PK)
   ▲                               ▲                              ▲
   │                               │                              │
   └─────────────── Events (id_event PK) ────────────────────────┘
                     id_event      UNIQUEIDENTIFIER
                     timestamp     DATETIME2(7)  (UTC)
                     id_source     INT  FK → Source
                     id_category   INT  FK → Category
                     id_version    INT  FK → Version
                     event_type    NVARCHAR(100)
                     data_json     NVARCHAR(MAX)  (payload JSON del evento)
```

### 3.2 Tablas

**`Events`** — tabla de hechos (una fila por evento).

| Columna | Tipo | Notas |
|---|---|---|
| `id_event` | `UNIQUEIDENTIFIER` | PK. `Guid.NewGuid()` generado en el cliente. |
| `timestamp` | `DATETIME2(7)` | **UTC** (`DateTime.UtcNow`). |
| `id_source` | `INT` | FK. Origen/collector. |
| `id_category` | `INT` | FK. Categoría funcional. |
| `id_version` | `INT` | FK. Versión del esquema del evento (hoy siempre `1`). |
| `event_type` | `NVARCHAR(100)` | Tipo de evento (ver §4). |
| `data_json` | `NVARCHAR(MAX)` | Serialización JSON **PascalCase** del evento completo. |

**Dimensiones** (catálogos, se auto-rellenan):

| Tabla | Columnas |
|---|---|
| `Source` | `id_source`, `source_name` (UNIQUE), `description` |
| `Category` | `id_category`, `category_name` (UNIQUE), `description` |
| `Version` | `id_version`, `numero_version` (UNIQUE), `description` |

### 3.3 Clave: el JSON del payload

`data_json` **incluye las propiedades base y las específicas**, en **PascalCase**:

```jsonc
// Ejemplo WindowEvent
{ "ProcessName":"Code", "WindowTitle":"Worker.cs - SentinelTelemetry",
  "Id":"...", "Timestamp":"2026-05-01T14:23:11.1234567Z",
  "Source":"Window", "Category":"System", "EventType":"ActiveWindowSnapshot", "Version":1 }
```

Por eso las vistas leen con `JSON_VALUE(data_json, '$.Propiedad')`.

---

## 4. Catálogo de eventos (¡crítico para Power BI!)

Combinación `Source` / `Category` / `event_type` que produce cada collector:

| Source | Category | event_type | Modelo | Campos en `data_json` |
|---|---|---|---|---|
| `Window` | `System` | `ActiveWindowSnapshot` | `WindowEvent` | `ProcessName`, `WindowTitle` |
| `Application` | `Lifecycle` | `Opened` / `Closed` | `ApplicationEvent` | `ProcessName`, `ProcessId`, `WindowTitle` |
| `Pointer` | `Input` | `PointerSnapshot`, y en runtime **`MouseMove` / `LeftClick` / `RightClick`** | `PointerEvent` | `X`, `Y`, `LeftClick`, `RightClick` |
| `Keyboard` | `Input` | `KeyboardSnapshot` | `KeyboardEvent` | `KeyPressCount` |
| `ClipboardCollector` | `Clipboard` | `ClipboardChanged` | `ClipboardEvent` | `ContentType`, `ContentLength` |
| `ExplorerCollector` | `Explorer` | `FolderOpened` / `FolderChanged` / `FolderClosed` | `ExplorerEvent` | `FolderPath` |
| `FileCollector` | `File` | `Created` / `Deleted` / `Renamed` / `Modified` | `FileEvent` | `FilePath`, `OldFilePath` |
| `Idle` | `Attention` | `IdleStarted` / `IdleEnded` | `IdleEvent` | (ninguno) |
| `Session` | `System` | `Login`, `Logout`, `Lock`, `Unlock`, `RemoteConnect`, `RemoteDisconnect`, `Suspend`, `Resume` | `SessionEvent` | (ninguno) |

### 4.1 Reglas de captura (importantes para interpretar los datos)

- **Worker**: ciclo de **1 segundo**.
- **Window**: se guarda **solo cuando cambia** `ProcessName` o `WindowTitle` (no hay muestreo constante).
- **Keyboard**: el evento solo se guarda si `KeyPressCount > 0` (una fila por segundo con tecleo).
- **Pointer**: solo se guarda si hay clic o movimiento **≥ 4 px** (distancia euclídea) desde la última posición guardada.
  El `event_type` se reescribe a `MouseMove`, `LeftClick` o `RightClick` antes de persistir.
- **Application**: se basa en procesos con ventana principal visible; en el **primer ciclo no emite eventos** (fija la foto inicial).
- **Explorer**: el primer ciclo no emite; detecta aperturas/cambios/cierres por `HWND`.
- **Idle**: umbral **30 s** sin actividad (`TimeSpan.FromSeconds(30)`); emite `IdleStarted` / `IdleEnded`.
- **Files**: `FileSystemWatcher` sobre las carpetas de `appsettings.json`, con deduplicación de `Modified`
  (una fila por archivo por ciclo) y exclusión de carpetas `node_modules`, `.git`, `bin`, `obj`, `.vs`.

### 4.2 Configuración (`appsettings.json`)

```jsonc
{
  "ConnectionStrings": { "SentinelDb": "Server=.;Database=SentinelTelemetryDb;Trusted_Connection=True;TrustServerCertificate=True;" },
  "FileWatcher": {
    "WatchedFolders": ["C:\\Users\\Jordi\\Desktop", "...\\Documents", "...\\Downloads", "...\\Pictures", "...\\Videos"],
    "ExcludedFolderNames": ["node_modules", ".git", "bin", "obj", ".vs"]
  }
}
```

---

## 5. Catálogo de vistas SQL (la capa que consume Power BI)

Todas las vistas están en `SentinelTelemetry.Database/`.

### Vistas “desempaquetado” (JSON → columnas)

| Vista | Fuente | Filtro | Columnas de negocio |
|---|---|---|---|
| `vw_pointer_unpacked_data` | Source=`Pointer` | — | `PosicionX` (int), `PosicionY` (int), `ClicIzquierdo` (texto), `ClickDerecho` (texto) |
| `vw_window_unpacked_data` | Source=`Window` | — | `Aplicacion` (`ProcessName`), `Ventana_Interna` (`WindowTitle`) |
| `vw_keyboard_unpacked_data` | Source=`Keyboard` | — | `Pulsaciones` (int) |
| `vw_clipboard_unpacked_data` | Source=`ClipboardCollector` | — | `Tipo_Contenido`, `Longitud_Contenido` (texto) |
| `vw_applicationCycle_unpacked_data` | Source=`Application` | — | `Programa` (`ProcessName`) |
| `vw_explorer_unpacked_data` | Source=`ExplorerCollector` | — | `Ruta` (`FolderPath`) |
| `vw_fileManagement_unpacked_data` | Source=`FileCollector` | — | `RutaAnterior`, `RutaActual` |

Todas incluyen además: `id_event`, `timestamp`, `id_source`, `id_category`, `id_version`, `event_type`.

### Vistas analíticas (métricas ya calculadas)

| Vista | Qué calcula | Notas |
|---|---|---|
| `vw_window_activity_duration` | `Process_Name`, `Window_Title`, `Start_Time`, `End_Time` (LEAD), `Duration_Seconds` | Filtra `event_type='ActiveWindowSnapshot'`; la **última fila tiene `End_Time`/`Duration` NULL**. No cruza con idle (una sesión idle larga infla la duración). |
| `vw_keyboard_daily_summary` | pulsaciones por día | `CAST(DATEADD(HOUR,-5, timestamp) AS DATE)` → **día en hora Perú (UTC-5)**. |
| `vw_keyboard_typing_metrics` | sesiones de tecleo con `KPS` y `KPM` | Segmenta por gap ≥ 2000 ms; `Duration_Seconds` con mínimo 1. |
| `vw_pointer_heatmap` | mapa de calor de coordenadas | Bins de 10×10 px (`X_Bin`, `Y_Bin`, `Densidad`). |
| `vw_idle_activity_periods` | periodos `Idle` y `Activo` con `DuracionSegundos` | Alterna `IdleStarted`→`IdleEnded` y `IdleEnded`→siguiente `IdleStarted`. |
| `vw_apps_when_turnedOn_metrics` | apps abiertas en los 5 min siguientes a Login/Unlock | `VecesQueAparece`, `TotalSesiones`, `PorcentajeDeSesiones`. |
| `vw_transitions_between_apps` | matriz de transiciones entre apps | `AppActual`, `AppSiguiente`, `Frecuencia`, `PorcentajeDeSalidas`. |

---

## 6. Guía para Power BI

### 6.1 Conexión

1. **Obtener datos → SQL Server**.
2. Servidor: `localhost` (o `.`), Base de datos: `SentinelTelemetryDb`.
3. Modo: **Import** recomendado (volumen alto pero vistas ya agregadas; las de detalle pueden
   llegar a ser grandes). Usa **DirectQuery** solo para detalle en tiempo real.
4. Autenticación: Windows (`Trusted_Connection=True`).

### 6.2 Modelo en estrella recomendado

- **Hecho:** `Events` (o una vista de detalle) — grano = 1 evento.
- **Dimensiones:** `Source`, `Category`, `Version` (catálogos reales).
- **Tablas de hechos desnormalizadas (vistas):** `vw_pointer_unpacked_data`,
  `vw_window_unpacked_data`, etc. Úsalas como *facts* secundarios.
- **Tablas puente/analíticas:** `vw_*` de métricas.
- **Dimensión de fecha (`DimFecha`):** créala con DAX (`CALENDAR`/`CALENDARAUTO`) y relaciónala
  con `timestamp` **convertido a hora local**.

Relaciones clave:

```
Source[id_source] ─1:*─ Events[id_source]
Category[id_category] ─1:*─ Events[id_category]
Version[id_version] ─1:*─ Events[id_version]
DimFecha[Fecha] ─1:*─ Events[FechaLocal]
```

### 6.3 Zona horaria (¡ojo!)

`Events.timestamp` está en **UTC**. Las vistas `vw_keyboard_daily_summary` y
`vw_keyboard_typing_metrics` ya aplican `-5 h` (Perú). **El resto NO.** Para el modelo:

- Columna calculada en DAX: `FechaLocal = [timestamp] - TIME(5,0,0)` (formato fecha/hora).
- O bien transformación en Power Query: `DateTimeZone.From([timestamp]) |> to local (UTC-5)`.

### 6.4 Transformaciones recomendadas en Power Query

- **Booleans como texto:** `ClicIzquierdo` / `ClickDerecho` llegan como `"true"`/`"false"` →
  conviértelos a `True/False` (o a 1/0) para contar clics.
- **Números como texto:** `Longitud_Contenido` y `X_Bin`/`Y_Bin` pueden llegar como texto →
  `Tipo con número entero`.
- **Parsear JSON nativo (alternativa a las vistas):** si necesitas campos no expuestos,
  `Json.Document([data_json])` y expande. Vistas primero; JSON solo si falta algo.
- **Normalizar `ProcessName`:** puede traer mayúsculas raras; usa una columna `Aplicacion_Normalizada = UPPER(TRIM(...))`.

### 6.5 Medidas DAX de ejemplo

```dax
Pulsaciones Totales = SUM ( vw_keyboard_unpacked_data[Pulsaciones] )

Clics Izquierdos = CALCULATE (
    COUNTROWS ( vw_pointer_unpacked_data ),
    vw_pointer_unpacked_data[ClicIzquierdo] = "true"
)

Tiempo Activo (h) =
VAR Segundos = SUMX ( vw_idle_activity_periods,
                      IF ( vw_idle_activity_periods[TipoPeriodo] = "Activo",
                           vw_idle_activity_periods[DuracionSegundos] ) )
RETURN DIVIDE ( Segundos, 3600 )

Tiempo Inactivo (h) =
VAR Segundos = SUMX ( vw_idle_activity_periods,
                      IF ( vw_idle_activity_periods[TipoPeriodo] = "Idle",
                           vw_idle_activity_periods[DuracionSegundos] ) )
RETURN DIVIDE ( Segundos, 3600 )

KPM Promedio = AVERAGE ( vw_keyboard_typing_metrics[KPM] )

Sesiones de Tecleo = COUNTROWS ( vw_keyboard_typing_metrics )

Cambios de Archivo = COUNTROWS ( vw_fileManagement_unpacked_data )

Ratio Retorno (Idle→Activo) =
DIVIDE ( [Tiempo Inactivo (h)], [Tiempo Activo (h)] )

% Salidas a App X =
AVERAGE ( vw_transitions_between_apps[PorcentajeDeSalidas] )
```

### 6.6 Páginas/páginas sugeridas para el informe

1. **Resumen diario** — pulsaciones/día, tiempo activo vs inactivo, nº de cambios de archivo, nº de sesiones.
2. **Productividad de escritura** — KPS/KPM por hora y por sesión (`vw_keyboard_typing_metrics`).
3. **Uso de aplicaciones** — tiempo por `Aplicacion` (`vw_window_activity_duration`), top apps, timeline.
4. **Transiciones** — matriz `AppActual` × `AppSiguiente` (`vw_transitions_between_apps`) para mapa de flujo.
5. **Apps al iniciar sesión** — `vw_apps_when_turnedOn_metrics` (barras por `PorcentajeDeSesiones`).
6. **Mapa de calor de ratón** — scatter/burbujas con `X_Bin`/`Y_Bin` y `Densidad` (`vw_pointer_heatmap`).
7. **Explorador y archivos** — rutas más visitadas (`vw_explorer_unpacked_data`) y actividad por carpeta
   (`vw_fileManagement_unpacked_data`).
8. **Portapapeles** — distribución por `Tipo_Contenido` y longitud media.
9. **Sesiones e idle** — línea temporal de `Login/Logout/Lock/Unlock/Suspend/Resume` + periodos.

### 6.7 Advertencias y “gotchas” para el análisis

- **Es una sola máquina.** No hay columna de dispositivo ni de usuario; `Source` es el *collector*,
  no el equipo. Si en el futuro hay varios equipos, habrá que añadir una dimensión `Device`.
- **`Version` = 1 siempre** (el modelo nunca incrementa `EventBase.Version`). No esperes variación.
- **Duraciones por `LEAD`** (`vw_window_activity_duration`, `vw_idle_activity_periods`): la última
  fila de cada serie queda con `NULL`. Trátalo con `ISNULL`/filtros.
- **`Duration_Seconds` de ventana** no descuenta idle: si el equipo quedó inactivo, la “duración”
  de la app en primer plano puede exagerarse. Cruza con `vw_idle_activity_periods` para corregir.
- **`vw_window_activity_duration` no filtra por `Source`,** solo por `event_type`. Hoy es seguro
  porque `ActiveWindowSnapshot` es único, pero si se reutiliza el nombre en otro collector se
  mezclarían datos.
- **Keyboard/pointer son muestreos**, no logs exhaustivos: la keyboard sólo registra segundos con
  tecleo; el pointer descarta movimientos < 4 px. No son un registro completo de cada pulsación/px.
- **FileWatcher**: puede perder eventos ante overflow de buffer (se loguea `Error`). `Modified` está
  deduplicado por archivo/ciclo.
- **`LeftClick`/`RightClick` son estado del botón en el instante del muestreo,** no un conteo exacto
  de clics.
- **Zona horaria:** solo dos vistas aplican UTC-5. Todo lo demás en UTC.

---

## 7. Preguntas/consultas de ejemplo (SQL) para Power BI / validación

```sql
-- Volumen por origen y tipo en los últimos 7 días (UTC)
SELECT S.source_name, E.event_type, COUNT(*) AS Eventos
FROM dbo.Events E
JOIN dbo.Source S ON S.id_source = E.id_source
WHERE E.timestamp >= DATEADD(DAY, -7, SYSUTCDATETIME())
GROUP BY S.source_name, E.event_type
ORDER BY Eventos DESC;

-- Top 10 aplicaciones por tiempo estimado en primer plano (hora local UTC-5)
SELECT TOP 10 Process_Name AS Aplicacion,
       ROUND(SUM(Duration_Seconds) / 3600.0, 2) AS Horas
FROM vw_window_activity_duration
WHERE Duration_Seconds IS NOT NULL
GROUP BY Process_Name
ORDER BY Horas DESC;

-- Balance diario activo vs inactivo (hora local UTC-5)
SELECT CAST(DATEADD(HOUR,-5, Inicio) AS DATE) AS Fecha,
       TipoPeriodo,
       ROUND(SUM(DuracionSegundos)/3600.0, 2) AS Horas
FROM vw_idle_activity_periods
GROUP BY CAST(DATEADD(HOUR,-5, Inicio) AS DATE), TipoPeriodo
ORDER BY Fecha DESC, TipoPeriodo;

-- Clics del ratón (parseando el booleano textual del JSON)
SELECT SUM(CASE WHEN ClicIzquierdo = 'true' THEN 1 ELSE 0 END) AS ClicsIzq,
       SUM(CASE WHEN ClickDerecho    = 'true' THEN 1 ELSE 0 END) AS ClicsDer
FROM vw_pointer_unpacked_data;
```

---

## 8. Glosario rápido

| Término | Significado |
|---|---|
| **Collector** | Módulo que captura una fuente de telemetría. |
| **Evento** | Registro atómico (`id_event` + `timestamp` + `data_json`). |
| **Snapshot** | Captura puntual del estado (p. ej. `ActiveWindowSnapshot`, `KeyboardSnapshot`). |
| **Idle** | Inactividad ≥ 30 s sin teclado/ratón. |
| **Pulsación / KeyPressCount** | Pulsaciones de tecla acumuladas en un segundo. |
| **KPS / KPM** | Pulsaciones por segundo / por minuto. |
| **Unpacked view** | Vista que convierte `data_json` en columnas. |
| **Worker** | Bucle del servicio que orquesta captura y persistencia (1 s). |
