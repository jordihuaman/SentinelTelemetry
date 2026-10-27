# Guía práctica: crear medidas DAX en SentinelTelemetry

> Para alguien que nunca ha creado una medida. Sigue los pasos en orden.
> Modelo revisado: `PowerBI/SentinelTelemetryReport.pbix` (18 tablas, 36 medidas).

---

## 0. Antes de empezar

El modelo ya tiene lo más importante: las relaciones con `DimFecha` funcionan y las 21 tablas de fechas automáticas desaparecieron. Queda un pendiente que afecta a las medidas de tiempo (0.2) y tres ajustes de un minuto (0.3).

### 0.1 La vista de idle duplicaba el tiempo

La vista `vw_idle_activity_periods` emparejaba `IdleStarted` con el siguiente `IdleStarted` y `IdleEnded` con el siguiente `IdleEnded`. Por eso ambos tipos sumaban la línea de tiempo completa:

| Concepto | Horas (antes) |
|---|---:|
| Suma de periodos `Idle` | 1,990.4 h |
| Suma de periodos `Activo` | 1,990.3 h |
| Cobertura real de la línea de tiempo | 1,990.4 h |

**Corrección aplicada:** `SentinelTelemetry.Database/13_vw_idle_activity_periods.sql` ahora usa un solo `LEAD` sobre todos los eventos:

- `IdleStarted` → periodo `Idle` hasta el siguiente evento.
- `IdleEnded` → periodo `Activo` hasta el siguiente evento.

Así, `Idle + Activo` cubre la línea de tiempo exactamente una vez. Ya está aplicada en la base local. También creé el índice `IX_Events_id_source_timestamp` (script `16_index_events_idle.sql`) para que la consulta no se quede sin memoria al ordenar los 281k eventos.

Con los datos actuales el reparto es:

| Tipo | Periodos | Horas |
|---|---:|---:|
| `Activo` | 2,892 | 1,381.31 h |
| `Idle` | 2,917 | 609.09 h |
| **Total** | **5,809** | **1,990.39 h** |

La comprobación en DAX es:

```dax
EVALUATE
ROW (
    "IdleH", CALCULATE ( SUM ( vw_idle_activity_periods[DuracionSegundos] ), vw_idle_activity_periods[TipoPeriodo] = "Idle" ) / 3600,
    "ActivoH", CALCULATE ( SUM ( vw_idle_activity_periods[DuracionSegundos] ), vw_idle_activity_periods[TipoPeriodo] = "Activo" ) / 3600,
    "TotalH", SUM ( vw_idle_activity_periods[DuracionSegundos] ) / 3600
)
```

`TotalH` debe ser ≈ 1,990.39 h; `IdleH` ≈ 609.09 h, `ActivoH` ≈ 1,381.31 h, y `IdleH + ActivoH` debe coincidir con `TotalH`. Si ves ~1,990 h en cada tipo, Power BI todavía tiene los datos viejos: refresca el modelo.

### 0.2 Dos tablas siguen con fecha UTC

- `vw_window_activity_duration[date]` usa `[Start_Time]` (UTC).
- `vw_idle_activity_periods[date]` usa `[Inicio]` (UTC).

Ejemplo real: la primera ventana es `2026-07-24 04:15 UTC`, que en Perú es `2026-07-23 23:15`. Si usas esas dos tablas en un gráfico por día, ~35% de las filas caen en el día siguiente.

**Corrección en Power BI (columnas calculadas):**

En `vw_window_activity_duration[date]`:

```dax
date = DATE ( YEAR ( [Start_Time] - TIME ( 5, 0, 0 ) ), MONTH ( [Start_Time] - TIME ( 5, 0, 0 ) ), DAY ( [Start_Time] - TIME ( 5, 0, 0 ) ) )
```

En `vw_idle_activity_periods[date]`:

```dax
date = DATE ( YEAR ( [Inicio] - TIME ( 5, 0, 0 ) ), MONTH ( [Inicio] - TIME ( 5, 0, 0 ) ), DAY ( [Inicio] - TIME ( 5, 0, 0 ) ) )
```

Después de editar, `MIN(vw_window_activity_duration[date])` y `MIN(vw_idle_activity_periods[date])` deben ser `2026-07-23`, igual que `MIN(vw_window_unpacked_data[date])`.

**Alternativa a futuro:** devolver `FechaPeru` desde SQL y cambiar la columna calculada para que la use. Para este paso, la corrección en DAX es suficiente y no requiere tocar la base.

Si además quieres mostrar la **hora local** en los ejes, agrega una columna calculada `peru_time`:

```dax
peru_time = [Start_Time] - TIME ( 5, 0, 0 )
```

Para idle sería `peru_time = [Inicio] - TIME ( 5, 0, 0 )`. No es necesario para corregir la fecha; solo si vas a graficar por hora.

### 0.3 Ajustes de un minuto

- `DimFecha[Mes]` → seleccionar la columna → **Ordenar por columna** → `MesNum`.
- `DimFecha` → **Herramientas de tabla** → **Marcar como tabla de fechas** → `Date`.
- `Año`, `MesNum`, `DiaSemanaNum` → **Resumen** → **No resumir**.
- `KPS`, `KPM`, `PorcentajeDeSesiones`, `PorcentajeDeSalidas`, `X_Bin`, `Y_Bin` → **Resumen** → **No resumir**.

---

## 1. La idea en 30 segundos

| | Columna calculada | Medida |
|---|---|---|
| Se calcula | fila por fila, al refrescar | al vuelo, según el visual |
| Se guarda | sí, ocupa memoria | no |
| Reacciona a slicers | no | sí |
| Sirve para | categorías, claves, fechas | KPIs, totales, ratios |

**Regla práctica:** si la fórmula empieza por `SUM`, `COUNTROWS`, `AVERAGE` o `DIVIDE`, es una **medida**.

Una medida no “tiene” un valor fijo: el mismo `Total Pulsaciones` muestra el total del mes, de la app o del día según lo que dejes en el visual.

---

## 2. Tu primera medida, clic a clic

1. Abre el PBIX y guarda (`Ctrl+S`) antes de tocar nada.
2. En el panel izquierdo, entra a **Vista de datos** (icono de tabla) o quédate en **Vista de informe**.
3. En el panel **Datos** (derecha), despliega `vw_keyboard_unpacked_data`.
4. Clic derecho sobre la tabla → **Nueva medida**.
5. En la barra de fórmulas escribe:

```dax
Total Pulsaciones = SUM ( vw_keyboard_unpacked_data[Pulsaciones] )
```

6. Pulsa **Enter** o el check.
7. Con la medida seleccionada, en la cinta **Herramientas de medida**:
   - **Formato:** Número entero
   - **Decimales:** 0
   - **Carpeta para mostrar:** `Teclado`
8. Ve a **Vista de informe** → **Insertar** → **Tarjeta** → arrastra `Total Pulsaciones` a **Campos**.
9. Debe mostrar **373,104** (con los datos actuales).

La medida aparece en el panel Datos con un icono de calculadora. Ese icono es la diferencia entre una medida y una columna.

### Estructura de una medida

```dax
Nombre = FUNCIÓN ( Tabla[Columna] )
```

- `Nombre` es lo que verás en los visuales.
- `FUNCIÓN` es `SUM`, `COUNTROWS`, `DIVIDE`, etc.
- `Tabla[Columna]` siempre lleva el nombre de la tabla cuando hay varias tablas con la misma columna (`date`, `Duration_Seconds`, etc.).

---

## 3. Catálogo de medidas

Crea cada medida en la tabla que se indica, con clic derecho → **Nueva medida**.

### 3.1 Teclado

| Medida | Fórmula | Formato | Carpeta |
|---|---|---|---|
| `Total Pulsaciones` | `SUM ( vw_keyboard_unpacked_data[Pulsaciones] )` | `#,##0` | Teclado |
| `Promedio Diario` | `DIVIDE ( [Total Pulsaciones], COUNTROWS ( VALUES ( DimFecha[Date] ) ) )` | `#,##0` | Teclado |
| `Sesiones de Tecleo` | `COUNTROWS ( vw_keyboard_typing_metrics )` | `#,##0` | Teclado |
| `KPS Promedio` | `DIVIDE ( SUM ( vw_keyboard_typing_metrics[Keystrokes] ), SUM ( vw_keyboard_typing_metrics[Duration_Seconds] ) )` | `0.00` | Teclado |
| `KPM Promedio` | `[KPS Promedio] * 60` | `#,##0` | Teclado |
| `Pulsaciones por Sesión` | `DIVIDE ( [Total Pulsaciones], [Sesiones de Tecleo] )` | `#,##0` | Teclado |
| `Pulsaciones Últimos 7 Días` | `VAR Ultima = MAX ( vw_keyboard_unpacked_data[date] ) RETURN CALCULATE ( [Total Pulsaciones], DATESINPERIOD ( DimFecha[Date], Ultima, -7, DAY ) )` | `#,##0` | Teclado |

Notas:

- `Promedio Diario` divide entre los días del calendario en el contexto (365 si no hay filtro). Si quieres el promedio solo de días con actividad, usa `AVERAGEX ( VALUES ( DimFecha[Date] ), [Total Pulsaciones] )` y llámalo `Promedio Diario Activo`.
- `KPS Promedio` es ponderado por pulsaciones y segundos. El promedio simple de la columna `KPS` daría 3.83; el ponderado da 4.85. El ponderado es el correcto para un KPI global.
- `Pulsaciones Últimos 7 Días` usa la última fecha con datos, no la última fecha de `DimFecha` (que llega al 31/12/2026).

### 3.2 Ratón

| Medida | Fórmula | Formato | Carpeta |
|---|---|---|---|
| `Clics Izquierdos` | `CALCULATE ( COUNTROWS ( vw_pointer_unpacked_data ), vw_pointer_unpacked_data[ClicIzquierdo] = TRUE () )` | `#,##0` | Ratón |
| `Clics Derechos` | `CALCULATE ( COUNTROWS ( vw_pointer_unpacked_data ), vw_pointer_unpacked_data[ClickDerecho] = TRUE () )` | `#,##0` | Ratón |
| `Total Clics` | `[Clics Izquierdos] + [Clics Derechos]` | `#,##0` | Ratón |
| `Movimientos del Ratón` | `CALCULATE ( COUNTROWS ( vw_pointer_unpacked_data ), vw_pointer_unpacked_data[event_type] = "MouseMove" )` | `#,##0` | Ratón |
| `Ratio Clics por Pulsación` | `DIVIDE ( [Total Clics], [Total Pulsaciones] )` | `0.00` | Ratón |

`ClicIzquierdo` y `ClickDerecho` ya son columnas booleanas, por eso se filtran con `= TRUE ()`.

### 3.3 Tiempo y uso

| Medida | Fórmula | Formato | Carpeta |
|---|---|---|---|
| `Tiempo en App (h)` | `DIVIDE ( SUM ( vw_window_activity_duration[Duration_Seconds] ), 3600 )` | `0.0 "h"` | Tiempo |
| `Tiempo en App (h) con tope` | `DIVIDE ( SUMX ( vw_window_activity_duration, MIN ( vw_window_activity_duration[Duration_Seconds], 1800 ) ), 3600 )` | `0.0 "h"` | Tiempo |
| `Tiempo Activo (h)` | `DIVIDE ( CALCULATE ( SUM ( vw_idle_activity_periods[DuracionSegundos] ), vw_idle_activity_periods[TipoPeriodo] = "Activo" ), 3600 )` | `0.0 "h"` | Tiempo |
| `Tiempo Inactivo (h)` | `DIVIDE ( CALCULATE ( SUM ( vw_idle_activity_periods[DuracionSegundos] ), vw_idle_activity_periods[TipoPeriodo] = "Idle" ), 3600 )` | `0.0 "h"` | Tiempo |
| `% Tiempo Activo` | `DIVIDE ( [Tiempo Activo (h)], [Tiempo Activo (h)] + [Tiempo Inactivo (h)] )` | `0.0%` | Tiempo |

Advertencias:

- `Tiempo en App (h)` sin ajuste da **2,030 h en 85 días** (~24 h/día). El 82% de ese tiempo viene de 67 filas de más de 8 horas. Usa el tope mientras corriges las ventanas largas, pero no lo dejes como métrica definitiva.
- `Tiempo Activo` y `Tiempo Inactivo` ya son válidas: la vista de idle está corregida. Con los datos actuales, `Tiempo Activo` ≈ 1,381.31 h, `Tiempo Inactivo` ≈ 609.09 h y `% Tiempo Activo` ≈ 69.4%. Si refrescas con más telemetría, los valores cambiarán.

### 3.4 Apps y navegación

| Medida | Fórmula | Formato | Carpeta |
|---|---|---|---|
| `Apps Distintas` | `DISTINCTCOUNT ( vw_window_unpacked_data[Aplicacion] )` | `#,##0` | Apps |
| `Transiciones` | `SUM ( vw_transitions_between_apps[Frecuencia] )` | `#,##0` | Apps |
| `% Salidas` | `DIVIDE ( SUM ( vw_transitions_between_apps[Frecuencia] ), SUMX ( VALUES ( vw_transitions_between_apps[AppActual] ), CALCULATE ( MAX ( vw_transitions_between_apps[TotalSalidas] ) ) ) )` | `0.0%` | Apps |
| `% Sesiones con App` | `DIVIDE ( SUM ( vw_apps_when_turnedOn_metrics[VecesQueAparece] ), MAX ( vw_apps_when_turnedOn_metrics[TotalSesiones] ) )` | `0.0%` | Apps |

`% Salidas` está pensada para una matriz con `AppActual` en filas y `AppSiguiente` en columnas: cada celda muestra qué porcentaje de las salidas de esa app va a la app siguiente. En una tarjeta global dará 100% (o más), porque suma todas las apps.

`% Sesiones con App` está pensada para un gráfico de barras por `Programa`. En una tarjeta global puede pasar de 100%; si sumas todas las apps, esa misma fórmula se lee como “apps abiertas por sesión” (1.75), no como porcentaje.

### 3.5 Idle (actividad e inactividad)

| Medida | Fórmula | Formato | Carpeta |
|---|---|---|---|
| `Horas del Periodo (h)` | `DIVIDE ( SUM ( vw_idle_activity_periods[DuracionSegundos] ), 3600 )` | `0.0 "h"` | Tiempo |
| `Periodos de Idle` | `CALCULATE ( COUNTROWS ( vw_idle_activity_periods ), vw_idle_activity_periods[TipoPeriodo] = "Idle" )` | `#,##0` | Tiempo |

`Horas del Periodo (h)` sirve para dos cosas:

- En un anillo con `TipoPeriodo` en la leyenda muestra el reparto **Activo 1.381,3 h (69,4%) / Idle 609,1 h (30,6%)**.
- En una tabla con una fila por periodo muestra la duración de ese bloque.

`Periodos de Idle` cuenta cuántas veces el equipo pasó a inactivo: **2.917**.

Además, para poder agrupar por día de la semana, agrega una columna calculada a `DimFecha`:

```dax
DiaSemana =
SWITCH (
    [DiaSemanaNum],
    1, "Lunes",
    2, "Martes",
    3, "Miércoles",
    4, "Jueves",
    5, "Viernes",
    6, "Sábado",
    7, "Domingo"
)
```

Después: selecciona `DiaSemana` → **Herramientas de columna** → **Ordenar por columna** → `DiaSemanaNum`. Sin ese paso, los días salen en orden alfabético.

### 3.6 Archivos, portapapeles y explorador

| Medida | Fórmula | Formato | Carpeta |
|---|---|---|---|
| `Archivos Totales` | `COUNTROWS ( vw_fileManagement_unpacked_data )` | `#,##0` | Archivos |
| `Archivos Creados` | `CALCULATE ( [Archivos Totales], vw_fileManagement_unpacked_data[event_type] = "Created" )` | `#,##0` | Archivos |
| `Archivos Modificados` | `CALCULATE ( [Archivos Totales], vw_fileManagement_unpacked_data[event_type] = "Modified" )` | `#,##0` | Archivos |
| `Copias` | `COUNTROWS ( vw_clipboard_unpacked_data )` | `#,##0` | Portapapeles |
| `Longitud Media` | `AVERAGE ( vw_clipboard_unpacked_data[Longitud_Contenido] )` | `#,##0` | Portapapeles |
| `Carpetas Distintas` | `DISTINCTCOUNT ( vw_explorer_unpacked_data[Ruta] )` | `#,##0` | Explorador |

---

## 4. Cómo usar las medidas en los visuales

| Visual | Campos |
|---|---|
| **Tarjeta** | `Total Pulsaciones`, `Total Clics`, `KPS Promedio` |
| **Gráfico de líneas** | Eje X: `DimFecha[Date]` · Eje Y: `Total Pulsaciones` |
| **Barras horizontales** | Eje Y: `Process_Name` (de `vw_window_activity_duration`) · Eje X: `Tiempo en App (h)` · Filtro: Top 10 |
| **Matriz de transiciones** | Filas: `AppActual` · Columnas: `AppSiguiente` · Valores: `Transiciones` y `% Salidas` |
| **Dispersión / heatmap** | X: `X_Bin` · Y: `Y_Bin` · Tamaño: `Densidad` |
| **Anillo** | Leyenda: `Tipo_Contenido` · Valores: `Copias` |
| **Barras** | Eje Y: `Programa` · Eje X: `% Sesiones con App` |

### El error que más te va a morder: tablas sin relación

Una medida solo responde a filtros de tablas **relacionadas** con las tablas que usa.

- `Total Pulsaciones` + `DimFecha[Date]` → funciona (hay relación).
- `Total Pulsaciones` + `Aplicacion` → **no** funciona: teclado y ventana no están relacionados. Verás el mismo total repetido en todas las apps.
- `Total Clics` + `Aplicacion` → tampoco.
- `Tiempo en App (h)` + `Process_Name` → **sí** funciona: ambos son de `vw_window_activity_duration`.
- `Tiempo en App (h)` + `Aplicacion` → **no** funciona: `Aplicacion` es de `vw_window_unpacked_data`, que no está relacionada con `vw_window_activity_duration`. Para el eje de apps usa `Process_Name`.

Para “pulsaciones por aplicación” haría falta una vista que relacione cada evento de teclado con la ventana en primer plano en ese instante. Es un paso posterior; por ahora no mezcles esas columnas en el mismo visual.

---

## 5. Cómo comprobar que quedaron bien

Crea una página **Pruebas** y pon tarjetas con cada medida. Con los datos actuales deberías ver:

| Medida | Valor esperado |
|---|---:|
| Total Pulsaciones | 373,104 |
| Clics Izquierdos | 11,918 |
| Clics Derechos | 193 |
| Total Clics | 12,111 |
| Sesiones de Tecleo | 21,690 |
| KPS Promedio | 4.85 |
| KPM Promedio | 291.19 |
| Apps Distintas | 117 |
| Archivos Totales | 4,880 |
| Archivos Creados | 2,137 |
| Copias | 1,863 |
| Longitud Media | 3,337.58 |
| Carpetas Distintas | 212 |
| Tiempo en App (h) | 2,030.09 (inflado) |
| Tiempo en App (h) con tope | 259.97 |
| Tiempo Activo (h) | 1,381.31 |
| Tiempo Inactivo (h) | 609.09 |
| % Tiempo Activo | 69.4% |
| Horas del Periodo (h) | 1,990.39 (suma de todos los periodos) |
| Periodos de Idle | 2,917 |
| Tiempo en Apps (h) | 238.77 |
| Apps Utilizadas | 115 |
| Eventos de Ventana | 33,150 |

Si algún número no coincide, revisa el nombre de la tabla/columna y que no hayas arrastrado la columna en vez de la medida.

---

## 6. Errores comunes de novato

1. **Arrastrar la columna en vez de la medida.** Verás “Suma de Pulsaciones” en el visual. Eso es una medida implícita: no se guarda, no se formatea y no se reutiliza. Usa siempre el icono de calculadora.
2. **Sumar KPS, KPM o porcentajes.** Son promedios/ratios. Para KPS usa `DIVIDE ( SUM ( Keystrokes ), SUM ( Duration_Seconds ) )`; para porcentajes, `DIVIDE`.
3. **Poner el mismo nombre que una columna.** `Pulsaciones` ya existe como columna; llama a la medida `Total Pulsaciones`.
4. **Usar `/` en vez de `DIVIDE`.** `DIVIDE` devuelve blanco si el denominador es 0 y evita errores.
5. **No calificar la tabla.** `[date]` existe en varias tablas; escribe `vw_window_unpacked_data[date]`.
6. **Crear una columna calculada para un total.** Un total debe ser medida; una columna calculada se repite en cada fila y no reacciona a los filtros.
7. **Olvidar el formato.** Una medida sin formato se ve como un número crudo. Usa `#,##0`, `0.00`, `0.0%` o `0.0 "h"`.
8. **Mezclar tablas sin relación.** Ver la sección 4.

---

## 7. Checklist final

- [x] Guardé el PBIX.
- [x] Corregí la vista de idle y creé el índice `IX_Events_id_source_timestamp`.
- [x] Refresqué el modelo de Power BI después del cambio.
- [x] Corregí la fecha UTC de `vw_window_activity_duration` y `vw_idle_activity_periods`.
- [x] Ordené `DimFecha[Mes]` por `MesNum` y marqué `DimFecha` como tabla de fechas.
- [x] Puse “No resumir” en KPS, KPM, porcentajes, bins e ids.
- [x] Creé las 7 medidas de teclado.
- [x] Creé las medidas de ratón.
- [x] Creé las medidas de apps y archivos.
- [x] Creé la página **Pruebas** y validé los valores esperados.
- [x] Construí la página **Resumen Diario**.
- [x] Ajusté la fecha final de la segmentación a **16/10/2026**.
- [x] Construí la página **Uso de aplicaciones**.
- [x] Construí la página **Actividad e Inactividad** (Idle).
- [x] Construí la página **Transiciones**.
- [x] Guardé otra vez.

---

## 8. Página: Actividad e Inactividad (Idle)

### 8.1 Estructura

| Zona | Visual | Campos |
|---|---|---|
| Arriba izquierda | Cuadro de texto | **ACTIVIDAD E INACTIVIDAD**, 28 pt, negrita |
| Arriba centro | Segmentación | `DimFecha[Date]`, 23/07/2026 – 16/10/2026 |
| Fila de tarjetas | 4 tarjetas | `Tiempo Activo (h)`, `Tiempo Inactivo (h)`, `% Tiempo Activo`, `Periodos de Idle` |
| Izquierda | Gráfico de líneas | Eje X `DimFecha[Date]` · Eje Y `Tiempo Activo (h)` y `Tiempo Inactivo (h)` |
| Abajo izquierda | Columnas | Eje X `DimFecha[DiaSemana]` · Eje Y `% Tiempo Activo` |
| Abajo centro | Anillo | Leyenda `TipoPeriodo` · Valores `Horas del Periodo (h)` |
| Derecha | Tabla | `peru_time`, `Horas del Periodo (h)` · filtro `TipoPeriodo = "Idle"` · Top 10 |

### 8.2 Por qué el anillo es la mejor validación

Si el anillo muestra **Activo 69,4% / Idle 30,6%**, la vista está corregida. Si muestra ~50% / ~50% o dos slices de ~1.990 h, Power BI sigue con los datos viejos: refresca.

### 8.3 Qué significa cada cosa

- **Activo**: desde que termina un idle hasta el siguiente evento (usuario trabajando).
- **Inactivo**: desde que empieza un idle hasta el siguiente evento.
- `Activo + Idle = 1.990,4 h`, que es la cobertura real de la línea de tiempo.
- Los bloques de más de 12 h en la tabla son noches o equipos apagados: el último periodo se estira hasta el siguiente evento real.

### 8.4 Formato de la tabla: `dd/MM/yyyy HH:mm`

En Power BI, `mm` significa **minutos**, no meses. Con el formato `dd/mm/yyyy hh:mm` la tabla muestra día/minuto/año (`27/38/2026`). El formato correcto es:

```
dd/MM/yyyy HH:mm
```

- `MM` = mes · `mm` = minutos
- `HH` = 24 horas · `hh` = 12 horas

### 8.5 Cuidado con las selecciones activas

Si haces clic en una fila de la tabla y después aplicas el filtro **Top N**, la fila seleccionada puede quedar oculta y el filtro sigue activo. Todas las tarjetas, la línea y el anillo mostrarán solo ese periodo (por ejemplo `0,07 h`). No es un error de datos: cierra y vuelve a abrir el PBIX, o haz clic en una fila visible y luego en un espacio vacío del lienzo.

### 8.6 Promedios por día de la semana

Con el rango completo:

| Día | % Activo |
|---|---:|
| Lunes | 63,9% |
| Martes | 69,2% |
| Miércoles | 77,6% |
| Jueves | 56,7% |
| Viernes | 77,4% |
| Sábado | 85,6% |
| Domingo | 44,4% |

---

## 9. Página: Transiciones

### 9.1 Qué mide esta vista

`vw_transitions_between_apps` (script `15_vw_transitions_between_apps.sql`) mira cada evento de ventana en orden de tiempo y anota **a qué app pasaste después**. Descarta los cambios hacia la misma app, así que solo quedan saltos reales.

| Columna | Significado |
|---|---|
| `AppActual` | App de la que sales |
| `AppSiguiente` | App a la que entras |
| `Frecuencia` | Cuántas veces ocurrió ese par |
| `TotalSalidas` | Suma de todas las salidas de esa `AppActual` |
| `PorcentajeDeSalidas` | `Frecuencia / TotalSalidas`, ya redondeado a 1 decimal |

**Importante:** la vista **no tiene columna de fecha**. Por eso esta página es **histórica**: se muestran los 16.662 saltos de todo el periodo y **no lleva segmentación de fecha**. Si algún día quieres filtrarla por fecha, hay que agregar `MIN(timestamp)` a la vista.

### 9.2 Medidas nuevas

Ya existen `Transiciones` y `% Salidas`. Faltan tres medidas. En `_Medidas` → **Nueva medida**:

```dax
Apps con Transiciones =
DISTINCTCOUNT ( vw_transitions_between_apps[AppActual] )
```

```dax
Pares de Transición =
COUNTROWS ( vw_transitions_between_apps )
```

```dax
App con Más Salidas =
VAR Ranking =
    TOPN ( 1, VALUES ( vw_transitions_between_apps[AppActual] ), [Transiciones], DESC )
RETURN
    CONCATENATEX ( Ranking, vw_transitions_between_apps[AppActual], ", " )
```

- `Apps con Transiciones` = **117**
- `Pares de Transición` = **833** (cada fila es un par origen→destino)
- `App con Más Salidas` = **explorer**
- `Transiciones` = **16.662**

`App con Más Salidas` usa `TOPN` para quedarse con la app de mayor `[Transiciones]` y `CONCATENATEX` para convertirla en texto; sin `CONCATENATEX` la tarjeta esperaría una tabla y mostraría un error.

### 9.3 Columna calculada: `Par de Apps`

Para el gráfico de barras necesitas el par en una sola columna.

1. Panel **Datos** → tabla `vw_transitions_between_apps` → clic derecho → **Nueva columna**.
2. Pega:

```dax
Par de Apps = vw_transitions_between_apps[AppActual] & " → " & vw_transitions_between_apps[AppSiguiente]
```

3. En **Propiedades → Ordenar por columna** deja **(ninguno)**; el texto ya se ordena solo.
4. Si quieres el par en dos líneas, usa `UNICHAR(10)` en lugar de `" → "`.

### 9.4 Columna calculada: `Es Top 10 Destino`

Power BI falla con el **segundo** Top N de la matriz: al configurarlo en `AppSiguiente` guarda el campo en el panel de filtros pero **sin la condición**, y la matriz muestra las 117 columnas. Se ve en el archivo guardado: el filtro `AppSiguiente` existe, pero sin “Por valor”.

La solución es una columna que marque los 10 destinos más frecuentes:

1. Tabla `vw_transitions_between_apps` → clic derecho → **Nueva columna**.
2. Pega:

```dax
Es Top 10 Destino =
VAR Totales =
    ADDCOLUMNS (
        VALUES ( vw_transitions_between_apps[AppSiguiente] ),
        "@Entradas",
            SUMX (
                FILTER (
                    ALL ( vw_transitions_between_apps ),
                    vw_transitions_between_apps[AppSiguiente] = EARLIER ( vw_transitions_between_apps[AppSiguiente] )
                ),
                vw_transitions_between_apps[Frecuencia]
            )
    )
VAR MiDestino = vw_transitions_between_apps[AppSiguiente]
VAR MiTotal =
    MAXX ( FILTER ( Totales, vw_transitions_between_apps[AppSiguiente] = MiDestino ), [@Entradas] )
VAR Ranking = RANKX ( Totales, [@Entradas], MiTotal, DESC, DENSE )
RETURN
    Ranking <= 10
```

3. Clic derecho en la columna nueva → **Ocultar en vista de informe**.

`Totales` calcula las entradas de cada destino sumando `Frecuencia` con `SUMX` sobre toda la tabla. Es importante que sea `SUMX` y no `CALCULATE`: en una columna calculada, `RANKX` con `CALCULATE` hereda el destino de la fila actual y devuelve rango 1 para todas las filas, con lo que la columna queda `True` en los 833 pares.

El resultado correcto es **10 destinos en `True`** y **364 pares**. Si ves `True` en los 833, la fórmula quedó con la versión de `CALCULATE`.

El corte queda así, sin empates:

| Puesto | Destino | Entradas |
|---:|---|---:|
| 1 | explorer | 4.064 |
| 2 | chrome | 2.337 |
| 3 | brave | 2.038 |
| 4 | SSMS | 1.532 |
| 5 | WhatsApp.Root | 1.162 |
| 6 | SearchHost | 790 |
| 7 | devenv | 712 |
| 8 | opera | 587 |
| 9 | WINWORD | 375 |
| 10 | claude | 277 |
| 11 | Notepad | 259 |

### 9.5 Estructura de la página

| Zona | Visual | Campos |
|---|---|---|
| Arriba izquierda | Cuadro de texto | **TRANSICIONES**, 28 pt, negrita |
| Arriba derecha | Cuadro de texto pequeño | `Histórico completo · sin filtro de fecha` |
| Fila de tarjetas | 4 tarjetas | `Transiciones`, `Pares de Transición`, `Apps con Transiciones`, `App con Más Salidas` |
| Izquierda | Barras horizontales | Eje Y `Par de Apps` · Eje X `Transiciones` · Top 10 |
| Derecha | Matriz | Filas `AppActual` (Top 10) · Columnas `AppSiguiente` (`Es Top 10 Destino`) · Valores `Transiciones` |

Pasos del gráfico de barras:

1. **Visualizaciones → Gráfico de barras agrupadas**.
2. **Eje Y** → `Par de Apps`.
3. **Eje X** → `Transiciones`.
4. **Filtros → Par de Apps → Tipo de filtro: Top N → Mostrar elementos: 10 → Por valor: `Transiciones`**.
5. **Formato → Barras → Color:** el que uses en las otras páginas.
6. **Formato → Etiquetas de datos → Activar**.

Pasos de la matriz:

1. **Visualizaciones → Matriz**.
2. **Filas** → `AppActual`.
3. **Columnas** → `AppSiguiente`.
4. **Valores** → `Transiciones`.
5. **Filtros → AppActual → Top N → 10 → por `Transiciones`**.
6. **Filtros → AppSiguiente → tipo **Básico** → `Es Top 10 Destino` = **Verdadero**`.
7. Si quedó un filtro `AppSiguiente` con “Top N” sin valor, quítalo: **... → Quitar filtro**.
8. **Formato → Subtotales → desactiva Filas y Columnas**.
9. **Formato → Valores → Color de fondo → Activar → fx → Gradiente → mínimo blanco, máximo azul**.

### 9.6 Tres advertencias sobre la matriz

- **El segundo Top N no se guarda.** Es un fallo de la interfaz con esta tabla: al ponerlo en `AppSiguiente` el filtro queda vacío. Por eso se usa la columna `Es Top 10 Destino`.
- **Los subtotales mienten con Top N.** Si dejas los subtotales activados, el total de una fila suma solo las 10 columnas visibles, no las 117. Con el Top N puesto, el subtotal ya no es el total real de la app. Por eso se desactivan.
- **Las celdas vacías no son un error.** El 10×10 oculta 107 apps y 107 destinos. Un hueco significa “ese par no está entre los 10 más frecuentes”, no que nunca ocurrió.

Si prefieres el detalle completo, agrega una tabla aparte (no la matriz) con `AppActual`, `AppSiguiente`, `Transiciones` y `% Salidas`, con Top 100 por `Transiciones`.

### 9.7 Valores esperados

Tarjetas:

| Métrica | Valor |
|---|---:|
| Transiciones | 16.662 |
| Pares de Transición | 833 |
| Apps con Transiciones | 117 |
| App con Más Salidas | explorer |

Top 10 de pares (barras y celdas de la matriz):

| Puesto | Par | Transiciones | % Salidas del origen |
|---:|---|---:|---:|
| 1 | explorer → chrome | 719 | 17,7% |
| 2 | chrome → explorer | 697 | 29,8% |
| 3 | brave → SSMS | 662 | 32,5% |
| 4 | SSMS → brave | 642 | 41,9% |
| 5 | explorer → brave | 575 | 14,1% |
| 6 | brave → explorer | 552 | 27,1% |
| 7 | SSMS → explorer | 425 | 27,7% |
| 8 | explorer → WhatsApp.Root | 422 | 10,4% |
| 9 | explorer → SSMS | 400 | 9,8% |
| 10 | WhatsApp.Root → chrome | 398 | 34,3% |

Salidas por app (para comprobar la matriz con `AppActual`):

| App | Salidas |
|---|---:|
| explorer | 4.064 |
| chrome | 2.337 |
| brave | 2.038 |
| SSMS | 1.532 |
| WhatsApp.Root | 1.162 |
| SearchHost | 790 |
| devenv | 713 |
| opera | 587 |
| WINWORD | 375 |
| claude | 277 |

La suma de las 117 apps da exactamente **16.662**, igual que `Transiciones`.

Comprobaciones de la matriz:

- La suma de todas las celdas visibles da **11.523**. Los 5.139 saltos restantes van a destinos fuera del top 10; no se pierden, solo no se muestran.
- La fila `explorer` visible suma **3.097** de sus 4.064 salidas.
- Celdas sueltas: `explorer → chrome` = 719 · `chrome → explorer` = 697 · `brave → SSMS` = 662 · `SSMS → brave` = 642 · `WhatsApp.Root → chrome` = 398.

### 9.8 Cómo leer `% Salidas`

`% Salidas` = `Frecuencia / TotalSalidas` de la **app de origen**. Responde: “de todo lo que salí de explorer, ¿qué porcentaje terminó en chrome?”.

- En la matriz, los porcentajes de una fila suman **100% solo si están las 117 columnas**. Con Top 10 suman menos; es normal.
- `explorer → chrome` es el par más frecuente en números absolutos (719), pero `SSMS → brave` pesa más dentro de SSMS (41,9%). Los dos datos responden preguntas distintas: volumen total y hábito de la app.

---

## 10. Siguiente paso sugerido

Después de **Transiciones** queda:

1. **Corregir `vw_window_activity_duration`** para reemplazar el tope de 30 minutos por una duración real. Cuando esté, la página **Uso de Aplicaciones** puede dejar de usar `Tiempo en App (h) con tope`.
2. Opcional: cambiar la configuración regional del modelo a **Español (Perú)** para que las fechas y los separadores de miles salgan en formato local.
3. Opcional: agregar una columna de fecha a `vw_transitions_between_apps` para poder filtrar esta página por fecha.
