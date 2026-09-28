# Imperios en Guerra (C# + Unity 6)

RTS inspirado en Age of Empires y ambientado en la Guerra de Troya: **Jugador vs IA**, en dos mapas de 15x15 (uno por jugador).
Eliges **Grecia** (hoplitas con +15 de vida) o **Troya** (edificios con +20 % de vida); la IA juega con el otro bando.
En el juego, **F1** abre la guía "Cómo jugar y ganar".

El informe formal (MVC, UML, diagrama de flujo, concurrencia, pruebas de escritorio) está en
`Documentacion/Informe_Imperios_en_Guerra.docx`; se regenera con `Herramientas/generar_diagramas.py` y `Herramientas/generar_informe.py`.

## Cómo ejecutar
1. Abrir el proyecto en Unity 6.6 y abrir `Assets/Scenes/SampleScene.unity`.
2. Pulsar **Play**. La interfaz se construye sola (`Vista/ArranqueVista.cs` agrega la Vista al `JuegoManager`).
3. Build de escritorio: *File > Build Profiles > Windows/Mac/Linux > Build*.

Los archivos `configuracion.txt`, `log_partida.txt` y `resultado_final.txt` se guardan en
`ArchivosPartida/`, junto al proyecto (en el editor) o junto al ejecutable (en el build).

## Controles
| Acción | Cómo |
|---|---|
| Seleccionar unidad/edificio | Clic izquierdo en tu mapa |
| Mover | Con unidades seleccionadas: clic en casilla libre (o clic derecho) |
| Recolectar | Aldeanos seleccionados + clic en árbol, mina de oro o arbusto |
| Construir | Botón Casa/Cuartel/Centro Urbano + clic en casilla libre |
| Entrenar | Botones Aldeano (Centro Urbano) / Infante, Arquero (Cuartel) |
| Atacar | "Seleccionar ejército" + clic en el mapa enemigo |
| Cancelar | Esc o clic derecho en modo construcción |

Del mapa enemigo solo se ven los edificios y las casillas reveladas por tus disparos (el arquero revela 3x3).
**Victoria:** el rival se queda sin Centro Urbano **y** sin unidades militares.

## Arquitectura MVC
- `Assets/Scripts/Modelo` — clases C# puras (ensamblado `Modelo.asmdef` con `noEngineReferences: true`: Unity impide usar `UnityEngine` ahí).
  `Partida, Jugador, Mapa, Casilla, Unidad, Edificio, Recursos, ReglasJuego, GestorConcurrencia, LogicaIA, GestorArchivos, InicializadorPartida`.
- `Assets/Scripts/Controlador` — `JuegoControlador` (MonoBehaviour), `Seleccion`, interfaces `IVistaJuego` e `IEntradaJugador`.
- `Assets/Scripts/Vista` — `VistaJuegoUI`, `VistaMapa`, `CeldaVista`, `VistaUnidad`, `FabricaUI`, `CatalogoSprites`.

## Hilos
| Hilo | Tipo | Propósito | Sincronización | Finalización |
|---|---|---|---|---|
| Recolección (1 por aldeano) | `Task` | Caminar al recurso y extraer cada 2 s | `lock` en `Mapa` y `Recursos` | Token global + token de la orden de la unidad |
| Construcción (1 por edificio) | `Task` | Avance 0-100 % | `volatile` en progreso, `lock` en mapa | Token global |
| Entrenamiento (1 por edificio) | `Task` | Tiempo de entrenamiento + aparición | `Interlocked` (un entrenamiento por edificio), `lock` de población | Token global |
| Movimiento (1 por unidad) | `Task` | Paso a paso con BFS | `lock` en `Mapa.MoverUnidad` | Una orden nueva cancela la anterior |
| Recarga (1 por disparo) | `Task` | Tiempo entre disparos | `Interlocked.CompareExchange` | Token global |
| `Hilo-IA` | `Thread` | Decisiones de la IA | Usa la misma API validada que el humano | `CancellationToken` + `Join` |
| `Hilo-Log` | `Thread` | Escribir `log_partida.txt` | `BlockingCollection` (productor-consumidor) | `CompleteAdding` + `Join` |

Los hilos nunca tocan Unity: publican `EventoJuego` en una `ConcurrentQueue` (`ColaEventos`) que el
Controlador vacía en `Update()` (hilo principal) para actualizar la Vista.

## Pruebas
*Window > General > Test Runner > EditMode > Run All* (`Assets/Tests/EditMode/PruebasModelo.cs`, 14 pruebas).

## Otros
- `Herramientas/generar_sprites.py` regenera los sprites de `Assets/Resources/Sprites` (se pueden reemplazar por arte propio con el mismo nombre).
- `Respaldo_Scripts_2026-09-25/` contiene la versión anterior de los scripts.
