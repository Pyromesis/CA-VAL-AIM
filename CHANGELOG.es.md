# Registro de cambios

## [Sin publicar]

## [2.3.0] - 2026-10-29

### Añadido

- **Cambia de arma con 1 / 2 / 3.** Todos los ejercicios con arma tienen ahora tres ranuras: el 1 saca el arma grande
  (el arma del ejercicio o tu rifle principal cuando el ejercicio usa una pistola o el cuchillo), el 2 la pistola (la
  del propio ejercicio o, si no tiene, la Classic) y el 3 el cuchillo, con tus controles de VALORANT. Cada ranura conserva
  su cargador, así que cambiar de arma no regala nunca una recarga; el 1 sigue alternando Vandal ↔ Phantom durante la
  cuenta atrás o en el juego libre. Los ejercicios de arma fija (Ráfaga Vandal / Phantom, Transferencia de ráfaga,
  Operator, Arsenal, Chamber y Buscador de mira) mantienen sus propias reglas de arma.
- **Patrones de retroceso reales de la Vandal y la Phantom.** Las dos armas usan ahora su patrón de spray medido
  (25 balas y 9,0° de subida / 5,0° de barrido en la Vandal; 30 balas y 7,5° / 4,5° en la Phantom), con el desvío
  lateral exacto de cada bala y sin el giro aleatorio de los patrones reconstruidos. El gráfico del entrenamiento de
  spray y la nota del arsenal lo reflejan.

### Corregido

- **La ventana de novedades ya está en español.** La app lee `CHANGELOG.es.md` cuando el idioma es español; antes
  cargaba siempre el archivo inglés. El aviso de actualización y los tooltips de la franja de actualización también
  están traducidos.
- **Las dudas antes de disparar vuelven a medirse.** El tiempo sobre el objetivo antes del clic salía 0 ms en todos
  los enfrentamientos porque el frame del disparo ya lleva su retroceso (más que el radio angular de la cabeza); el
  coach ahora mide el tramo anterior al disparo, así que el aviso de dudar, la fase de clic del desglose de tiempo
  hasta el impacto y los momentos de Rebobinar usan valores reales.

## [2.2.0] - 2026-10-28

### Añadido

- **Coach jugada a jugada.** La pantalla de resultados ahora evalúa cada enfrentamiento de la partida por separado:
  reacción, flick, asentamiento, clic, pasarse y primer disparo, cada uno con un veredicto, qué falló y el ejercicio
  que lo corrige. Se guardan hasta 48 jugadas, las peores primero; **VER** abre ese momento exacto en Rebobinar y
  **PRACTICAR** inicia el ejercicio.
- **Una ventana de actualización al arrancar que no puedes perderte.** Cuando hay una versión nueva, CA-VAL-AIM abre
  una ventana con **ACTUALIZAR AHORA** y **MÁS TARDE**. ACTUALIZAR AHORA la descarga, verifica el SHA-256 y reinicia
  en la versión nueva; MÁS TARDE deja un aviso bajo la cabecera y no vuelve a interrumpir la sesión.

### Cambiado

- **Español de principio a fin.** Menús, Ajustes, Coach, Agentes, Lock-In, detalles del calentamiento, ejercicios,
  resultados, Rebobinar, el buscador de miras, el informe de errores y la ventana de actualización están traducidos,
  incluidos los recuentos dinámicos y las frases construidas en código. Los nombres de marca (`CA-VAL-AIM`,
  `LOCK-IN`) nunca se traducen, y una frase se deja en inglés antes que mostrarla a medias.
- Las puntuaciones y medidas del calentamiento usan la coma decimal en español; los ajustes, archivos de estadísticas
  y exportaciones mantienen el punto.
- El eje de la línea temporal del calentamiento queda debajo de las etiquetas de fase en vez de cruzarlas.

### Corregido

- El español ya no dice `Lock-Ins` en mitad de una frase, y los selectores de Ajustes ya no filtran
  `Valorant (On)`, `(default)` ni `On + Boost` en una pantalla que por lo demás está en español.

### Empaquetado

- **Una versión ya no puede compilarse sin los mapas.** `tools\build-release.ps1` comprueba el conjunto de 13 mapas,
  lo copia al instalador y se detiene si falta alguno; `installer\ValTrainer.iss` se niega a compilar sin `maps\`, y
  el flujo de trabajo de Release descarga el conjunto publicado y comprueba los 13 antes de compilar. La 2.1.0 ya
  los incluye.

## [2.1.0] - 2026-10-27

### Añadido

- **Listas de ejercicios.** Crea un conjunto con nombre a partir de cualquier ejercicio de la biblioteca y
  reprodúcelo del tirón: cada partida da paso a la siguiente, el último ejercicio conserva su pantalla de resultados
  normal y la lista se guarda en `playlists.json` junto a tus ajustes.
- **Cualquier arma en cualquier ejercicio.** Un selector **ARMA** en el menú y en el menú de pausa da a cada
  ejercicio que acepta un arma cualquier arma jugable, o mantiene la propia de cada ejercicio; la elección se
  recuerda entre sesiones.
- **Dificultad progresiva.** Un interruptor en el menú de pausa sube el nivel tras una partida puntuada fuerte y lo
  baja tras una floja, comparando precisión y puntuación con tu mejor marca personal; la pantalla de resultados
  muestra cuándo se movió el nivel.
- **El coach lee la dirección, el temblor y el error del flick.** Ahora separa los flicks a izquierda, derecha,
  arriba y abajo (tasa de acierto, error y lado en el que fallas), señala el temblor de puntería (muchas correcciones
  diminutas tras el flick), quedarse corto y pasarse, y el tiempo de reacción, y recomienda un ejercicio para cada uno.
- **Nuevo icono de la app:** un rastro de flick que aterriza en un anillo, sobre el rojo de VALORANT.

### Cambiado

- **El resto de la app se suma al estilo minimalista.** Agentes, Coach, Estadísticas, Ajustes y el menú de pausa
  usan ahora las superficies planas, las líneas finas, la tipografía ligera y el único acento rojo del menú 2.0;
  los botones, encabezados, tarjetas y fichas de agente compartidos se han rediseñado con ellos, y el hover ya no
  escala la tipografía.
- **La dificultad ahora escala los 60 ejercicios de la biblioteca.** Flick, Tracking, Micro y Reflejos cambian el
  tamaño, la distancia, los ángulos, la duración, la velocidad y las ventanas de reacción del objetivo con el nivel,
  no solo los temporizadores.
- **La biblioteca recuerda la pestaña seleccionada** al volver de un ejercicio.
- **El menú de pausa se ha reconstruido** en dos columnas con las acciones de la partida, la dificultad, la
  dificultad progresiva, el arma, la sensibilidad, el audio y los gráficos en un solo sitio.

### Corregido

- **El instalador incluye los mapas reales.** La compilación de la versión ahora copia el conjunto extraído `maps/` al
  instalador y a la carpeta portátil, y el selector de mapa llama **TRAZADO** al respaldo en vez de BLOCKOUT /
  MAQUETA.

## [2.0.1] - 2026-10-25

### Cambiado

- **Instalación para todos los usuarios en Program Files.** El instalador ahora pide permiso de administrador una vez
  e instala en `C:\Program Files\CA-VAL-AIM` con accesos directos para todos los usuarios, en vez de en
  `%LOCALAPPDATA%\Programs` sin aviso de UAC. Las actualizaciones dentro de la app mantienen el modo de la copia
  instalada: una copia para todos los usuarios pide UAC, una copia por usuario (2.0.0 y anteriores) sigue siendo por
  usuario. Los ajustes, estadísticas y grabaciones se quedan en `%APPDATA%\ValTrainer` en ambos casos.

### Corregido

- **El instalador nunca propone una carpeta bajo %TEMP% ni una que ya no existe.** Una entrada de desinstalación
  obsoleta podía hacer que las instalaciones nuevas usaran por defecto una carpeta temporal (y siguieran reutilizándola
  después de borrar sus archivos). Ahora, en ese caso, el instalador recurre a `C:\Program Files\CA-VAL-AIM`; un
  `/DIR=` explícito sigue teniendo prioridad.

## [2.0.0] - 2026-10-25

### Añadido

- **60 nuevos ejercicios de puntería, 15 por familia**, cada uno con su propia pestaña en la biblioteca: **Flick**
  (cerca, media distancia, larga, amplia, giro, línea de cabeza, vertical, de dentro afuera, triple, con tiempo,
  relámpago, a la deriva, diminutos, grandes, distancia mixta), **Tracking** (strafes lentos y rápidos, cerca, larga
  distancia, para y sigue, órbita, balanceo, ocho, zigzag, saltarín, errante, arranque, acercamiento, diminutos y
  rápidos, amplio), **Micro** (micropasos, ajustes pequeños y medianos, lejos, precisión, línea de cabeza, escalera,
  racimo, a la deriva, con tiempo, rápidos, cerca, mixtos, doble, cuerpo a cabeza) y **Reflejos** (centro, cerca,
  amplio, periférico, rápidos, relámpago, larga distancia, grandes, diminutos, veloces, paciente, vertical, doble,
  cerca, mixtos). Cambian de distancia, tamaño, velocidad, tiempos y patrón, alimentan al coach como los ejercicios
  existentes y se juegan en la vista previa.
- **Sens Finder se afina con microajustes.** Cada bloque añade cuatro correcciones de 0,8–3° a una cabeza a media
  escala, puntuadas por separado; pesan un 25 % mientras la búsqueda estrecha la sensibilidad y un 40 % en los pasos
  finos y la confirmación, donde deciden las correcciones pequeñas.
- **Ajustes de sonido de la interfaz** (Ajustes → Audio): *Sonidos de interfaz* activados/desactivados y *Volumen de
  interfaz*.

### Cambiado

- **Solo se publica el instalador.** Las versiones incluyen `ValTraining-<version>-Setup.exe` y `SHA256SUMS.txt`, sin
  zip portátil (como en la 1.17.0). Una copia portátil de una versión anterior sigue viendo la actualización y abre
  la página de la versión; conserva el nombre de su exe si una versión posterior vuelve a incluir un zip portátil.
- **Interfaz más silenciosa.** El hover es mudo (antes sonaba un tic brillante de 1,5 kHz en cada botón), el clic es
  un «toc» suave y grave de 60 ms y la confirmación son dos notas quedas (antes usaba el sonido de baja). Los cambios
  de pantalla y la carga no suenan.
- **El resto del menú sigue el nuevo estilo**: el selector de mapa (‹ Nombre › con un selector de mapa a pantalla
  completa), la franja de actualización y el enlace de actualización del pie, la ventana de vista previa del ejercicio
  y los paneles de *Novedades* y actualizaciones. El barrido rojo de la parte superior de la pantalla en cada cambio
  de pantalla ha desaparecido de las pantallas minimalistas.
- **Crédito al proyecto original, explícito.** `NOTICE`, el README, `CREDITS.md` y Ajustes → Acerca de → Licencias
  nombran ahora a **ValTrainer** y enlazan a <https://github.com/idoraz1/ValTrainer>, y el instalador incluye
  `NOTICE.txt` junto al ejecutable. `THIRD-PARTY-NOTICES.txt` repite la atribución. No ha cambiado ningún código ni
  término de licencia.

### Corregido

- **Se acabaron las apariciones dentro de paredes o bajo el mapa.** La colisión de los 13 mapas está sellada contra
  el arte (suelo oculto bajo suelos elevados rellenado, paredes de arte atravesables cerradas). Cada mapa incluye un
  `spawn_exclude.json` de la auditoría de apariciones: el Deathmatch nunca aparece en los puntos de la piscina que
  rechaza la comprobación de arte, y 7 puntos de ejercicio que estaban en el arte, bajo un suelo o sobre una losa
  invisible se mueven al punto limpio más cercano (Bind Hall Door y plant 1, Icebox Fence, Fracture Drop, Sunset
  retake 0, Corrode Mid Window y la segunda entrada de retake de Pearl, que se movió 3,7 m en los datos del mapa).
  Otros siete puntos que el minimapa marcaba fuera del área jugable se mueven 0,5–3,9 m al punto más cercano dentro
  de ella con suelo y sin pared (Ascent Boxes y retake 1, Bind Hall y retake 1, Fracture Site Corner, Pearl A Link y
  Default). Auditoría final: todos los inicios de ejercicio, todos los puntos creados a mano y el 100 % de la piscina
  de apariciones del Deathmatch de cada mapa pasan; una segunda pasada de sellado cerró más paredes atravesables en
  Breeze, Icebox, Pearl, Summit, Corrode, Abyss y Lotus.

## [1.18.0] - 2026-10-24

### Añadido

- **Laboratorio de reacción**: cinco pruebas de reacción (visual, sonora, periférica, elección / go-no-go, flick), cada
  una con su propio historial y bandas de rango (tiempos de reacción humanos típicos, no datos de Riot).
- **Seguimiento aéreo** (un objetivo que hace strafe y salta con el arco de salto de VALORANT) y **Seguimiento
  reactivo** (un objetivo que invierte, se para y arranca; mide lo rápido que lo vuelves a adquirir).
- **Campo de tiro del arsenal**: todas las armas de VALORANT con su daño, cadencia y velocidad al correr; Q / E
  cambian de arma, y los muñecos a 10–50 m muestran el daño y el tiempo para matar. Los modelos de arma son
  marcadores procedurales sencillos hechos para este proyecto (sin recursos de Riot).
- **Deathmatch al estilo VALORANT**: 12 jugadores (tú y 11 bots) por defecto, primero a 40 o 9 minutos, marcador con
  Tab, bots con nombres de agentes y cuatro dificultades. Los bots Difícil y Pesadilla asoman a tirones, aguantan
  ángulos y disparan en ráfaga agachados; los bots oyen tus pasos al correr (camina con Mayús para no hacer ruido).
- **Coach en vivo** (Ajustes → Jugabilidad → Coach en vivo, activado por defecto): avisos cortos durante la partida
  con la prueba y una corrección (mira demasiado baja / alta, disparar en movimiento, ráfagas largas, pasarse o
  quedarse corto, tracking, porcentaje de tiros a la cabeza, anticipar la señal, morir corriendo), repetidos en la
  pantalla de resultados.
- **Apariciones seguras**: cada inicio de ejercicio, punto de bot y aparición del Deathmatch se comprueba contra la
  colisión (suelo real bajo los pies, sin solapamientos, espacio para la cabeza, dentro del área jugable del minimapa)
  y se mueve al punto seguro más cercano en caso contrario.

### Cambiado

- **ValTraining ahora es CA-VAL-AIM.** El nombre nuevo se usa en el título de la ventana, el menú, el instalador, los
  accesos del menú Inicio, la entrada de desinstalación, el ejecutable (`CA-VAL-AIM.exe`, con `CA-VAL-AIM.pck`) y el
  repositorio de GitHub (`Pyromesis/CA-VAL-AIM`).
- **Las actualizaciones automáticas siguen funcionando tras el cambio de nombre.** Los archivos de la versión
  conservan a propósito el nombre `ValTraining-<version>-Setup.exe`: ValTraining 1.16.1–1.17.0 solo buscan archivos
  `ValTraining-…` (o `ValTrainer-…`), así que encuentran esta versión, la descargan y la instalan como siempre. Desde
  esta versión, el actualizador acepta `CA-VAL-AIM-…`, `ValTraining-…` y `ValTrainer-…`, así que el prefijo puede
  cambiar más adelante sin dejar a nadie tirado.
- **Tus datos se quedan donde estaban.** Los ajustes, estadísticas y grabaciones siguen en `%APPDATA%\ValTrainer`;
  los registros y la caché de shaders siguen en `%APPDATA%\Godot\app_userdata\ValTrainer` (sin recompilar shaders tras
  la actualización). Una actualización se instala en la carpeta existente, elimina el antiguo `ValTrainer.exe` /
  `ValTrainer.pck` y sustituye los accesos de ValTraining por los de CA-VAL-AIM. Las instalaciones nuevas van a
  `%LOCALAPPDATA%\Programs\CA-VAL-AIM`.
- **El repositorio de GitHub ahora es `Pyromesis/CA-VAL-AIM`.** GitHub redirige las URL antiguas de
  `Pyromesis/ValTraining` (web y API), así que los marcadores y enlaces a incidencias siguen funcionando. Las copias
  hasta la 1.17.0 pueden mostrar **ABRIR DESCARGA** una vez en vez de actualizarse solas, porque GitHub reescribe las
  URL de descarga de las versiones tras cambiar el nombre.
- **Un menú principal nuevo y minimalista.** Un lienzo plano con mucho espacio, tipografía de titulares ligera, líneas
  finas en vez de paneles y un solo color de acento para la acción principal y la elección actual. Todo llega con un
  breve ascenso escalonado; el hover y el foco de teclado entran y salen con suavidad. La biblioteca lista cada
  ejercicio con tu mejor marca en tu nivel, con pestañas por categoría; la columna derecha tiene la dificultad, el
  mapa, el **vestíbulo de Deathmatch** (dificultad de bot Fácil / Medio / Difícil / Pesadilla, 4–12 jugadores,
  3–15 minutos) y tu configuración importada. El rifle, la munición y el idioma están en el pie. *Otra vez: <último
  ejercicio>* repite tu último ejercicio con un clic. La vista previa de la mira está en Ajustes.
- **Pantalla de carga con progreso real.** Al iniciar un ejercicio de mapa se muestra el nombre del mapa y una barra
  de progreso fina mientras cargan sus archivos de nivel; la barra sigue el trabajo real (primero la colisión, luego
  cada nivel leído en un hilo de trabajo y construido uno por fotograma, ponderado por tamaño). Esc cancela. Los
  ejercicios de campo de tiro entran directamente.
- **Cambios de pantalla suaves.** Cada cambio de pantalla hace un fundido cruzado desde una imagen fija de la pantalla
  anterior, así que nunca hay un fotograma vacío o a medio construir, y el tirón del primer fotograma de un ejercicio
  ocurre detrás del fundido.
- **Tiempos de reacción por categoría** (Estadísticas → TIEMPOS DE REACCIÓN): último, mejor y media reciente por
  categoría del Laboratorio de reacción (visual, sonora, periférica, elección, flick), la tendencia, una banda de
  rango, el extra de la categoría (falsas alarmas, tiempo en acertar) y el historial de cada partida. Haz clic en una
  fila para jugarla.

### Corregido

- **Coach en vivo: pasarse / quedarse corto.** El aviso usa ahora la misma medida que el coach posterior a la partida
  (dónde termina el primer movimiento de cada flick, a lo largo del flick) en los últimos 15 segundos, calculada fuera
  del hilo principal y con los umbrales del coach posterior. Antes solo miraba los disparos fallados y casi nunca
  saltaba; quedarse corto por poco, que es normal, ya no lo activa.
- **Los bots del Deathmatch ya no se rompen al pisar un agujero de la colisión.** La altura del suelo ahí es «sin
  suelo», lo que convertía la posición del bot en NaN (errores en el registro y un bot que desaparecía). El bot ahora
  se queda en el suelo y planifica una ruta nueva; la misma protección está en los atacantes de defensa de sitio y en
  los botiquines, y los bots ignoran cualquier posición u orientación no finita.

## [1.17.0] - 2026-10-24

### Añadido

- **Los 13 mapas están completos.** Cada mapa se extrae ahora entero: todos los subniveles de arte, todos los sitios
  (A, B y C), el mid, ambos spawns y todos los conectores, en vez de solo el lado A. Breeze, Haven, Icebox y Split,
  que eran blockouts trazados a mano, también se juegan sobre la geometría real; los blockouts quedan como respaldo
  para instalaciones sin la carpeta `maps/`.
- **Colores reales de los mapas.** Cada superficie toma el color de su material real, medido por el extractor a
  partir de las texturas y tintes de capa del propio juego, en vez de una docena de grises genéricos, así que cada
  mapa tiene su propio aspecto. También se aplican las anulaciones de material por colocación (el suelo del sitio B
  de Bind es arena, no la rejilla gris del motor). Los archivos de nivel pesan menos que antes.
- **Nada de los mapas es transparente ya**: el cristal, las vallas y la vegetación se dibujan sólidos.
- **Línea guía** (Ajustes → Jugabilidad → Línea guía, activada por defecto): una línea desde la mira hasta el
  objetivo más cercano con su distancia de flick, una flecha en el borde cuando está fuera de pantalla y, en los
  ejercicios de mapa, una ruta en el suelo por el camino más corto hasta el enemigo más cercano mientras no haya
  ninguno a la vista. Desactivada / Línea / Línea + Ruta.
- **Vandal ↔ Phantom en el ejercicio.** En todos los ejercicios de rifle, *Equipar principal* (1) cambia el rifle
  durante la cuenta atrás (o en cualquier momento en juego libre) y lo mantiene como rifle principal. La cuenta atrás
  muestra qué rifle llevas.
- **Coach: orden de objetivos.** Gridshot registra a por qué objetivo vas después; el coach señala si te saltas el
  más cercano (`TARGET_ORDER`, con correcciones, ejercicios y momentos de Rebobinar) y elogia un orden limpio.
- **Coach: tendencias de habilidad.** Cada habilidad muestra si mejora o baja (esta semana frente a antes, o las
  partidas nuevas frente a las antiguas); el tooltip da el cambio en rangos.
- **Coach: el plan de hoy.** Un ejercicio por cada problema principal, del más importante al menos, cada uno iniciado
  con ese problema como foco del coach.
- Herramientas de mapas en el repositorio: `tools/mappipe` (extractor; la clave del pak se pasa con `--key`),
  `compose_map.py`, `flatten_glb.py`, `build_maps.py`, `author_spots.py`, `render_plan.py`, `api_callouts.py`,
  `map_points.py`. Ver `tools/mapgen/README.md`.

### Corregido

- **Los mapas estaban espejados.** La extracción colocaba cada pieza con una rotación donde Unreal → glTF necesita
  una reflexión, así que los mapas salían espejados de este a oeste y cada pieza se espejaba otra vez en su sitio,
  abriendo huecos entre piezas vecinas. Los mapas ahora coinciden con el juego real y con los minimapas oficiales.
- El minimapa y el informe usan los minimapas oficiales con una calibración exacta (transformación de
  valorant-api.com), así que los marcadores están donde están en el mapa 3D.
- Los ejercicios de mapa en Lotus, Pearl, Sunset, Summit, Abyss, Corrode y Fracture se generaron a partir de un mapa
  parcial y colocaban a los defensores en la entrada o fuera de la vista. Todos los ejercicios de mapa (excepto Ascent
  y Bind, creados a mano) se generan ahora desde la colisión completa: la ruta es el paseo real desde el spawn de
  atacantes, los defensores ocupan celdas cubiertas que ven la entrada o el sitio, y las plantas, retakes, smokes y
  flashes los siguen.
- Caracteres corruptos («â€”») en los subtítulos y textos de los ejercicios de mapa.
- El menú principal lanzaba una excepción al construir su panel de récords para un jugador nuevo sin partidas.

## [1.16.2] - 2026-10-23

### Añadido

- `tools/mapgen/gen_collision.py` reconstruye el `collision.json` de un mapa directamente desde sus GLB, y
  `tools/mapgen/audit_collision.py` comprueba el resultado contra las definiciones de ejercicio de
  `MapSpots.*.cs` (aparición pisable, choke/enemigos/plantas/retakes alcanzables a pie). Ver
  `tools/mapgen/README.md`.

### Corregido

- La colisión de los ocho mapas extraídos (Ascent, Fracture, Lotus, Sunset, Pearl, Summit, Abyss, Corrode) se
  reconstruye a partir del arte. Lotus, Pearl, Sunset y Corrode aparecían en una isla de 4–5 m, así que cada
  ejercicio empezaba contra una pared invisible; Fracture aparecía dentro del suelo. Esas apariciones ahora se
  apoyan en el suelo real, y los puntos de choke, enemigo y planta son alcanzables.
- Los pasillos que la colisión antigua cortaba por la mitad vuelven a estar abiertos: las paredes invisibles ya no
  bloquean las entradas a los sitios, y el jugador ya no se cae por suelos que faltan.
- Los skyboxes, fondos y placas de terreno lejano ya no se convierten en cajas de colisión. Todo lo que queda fuera
  del volumen jugable se descarta, lo que también baja Fracture de 41 000 cajas a 20 000.
- Las apariciones de ejercicio y las posiciones de enemigo creadas a mano se ajustan al suelo real de la colisión en
  tiempo de ejecución, así que un punto colocado unos decímetros fuera del arte ya no hace caer al jugador fuera del
  mapa.
- El nivel de vista pintada / fondo de Bind ya no se carga dentro del mapa jugable, donde sus dunas de arena cubrían
  Hookah y el sitio B. El cielo pone el horizonte y el suelo vuelve a verse.

### Problemas conocidos

- El `Rook_Art_AtkPathMid.glb` de Corrode es un archivo vacío de 132 bytes (también en los paquetes 1.16.1), así que
  el pasillo entre el spawn de atacantes y el sitio A no tiene ni arte ni colisión. Necesita una extracción nueva
  antes de que ese mapa se juegue como el real.

## 1.16.1 — 2026-10-21

### Añadido

- La app es ahora **ValTraining** en todas partes: ventana, menú, instalador, accesos del menú Inicio y paquetes de
  la versión.
- Actualizaciones automáticas dentro de la app: ValTraining consulta GitHub al arrancar y cada seis horas, muestra la
  versión nueva en el menú con un botón ACTUALIZAR siempre visible (y un aviso cuando hay una versión disponible), la
  descarga en segundo plano, la verifica con `SHA256SUMS.txt` y se convierte en REINICIAR PARA ACTUALIZAR. Desactivar
  las comprobaciones automáticas en Ajustes → Acerca de sigue dejando el botón ACTUALIZAR, que comprueba y descarga a
  petición.
- El instalador y el zip portátil se llaman `ValTraining-<version>-Setup.exe` y
  `ValTraining-<version>-Portable.zip`. El actualizador sigue aceptando los nombres antiguos `ValTrainer-…`.

### Corregido

- La versión que muestra la app viene ahora del ejecutable, así que las actualizaciones que solo sustituyen el
  ensamblado informan de la versión que realmente son y no de la versión incluida en la exportación original.
- El empaquetado de la versión ahora rechaza una app preparada a la que le falta `ValTrainer.dll`.

## 1.16.0 — 2026-10-20

### Añadido

- Los 13 mapas estándar son seleccionables: Ascent, Bind, Breeze, Fracture, Haven, Icebox, Lotus, Pearl, Split,
  Sunset, Summit, Abyss y Corrode.
- Geometría, texturas y colisión reales extraídas para Ascent, Fracture, Lotus, Sunset, Pearl, Summit, Abyss y
  Corrode.
- Comportamiento de spray/retroceso de la Vandal y la Phantom, incluidos los gráficos de spray y la transferencia de
  spray.
- Importación de sensibilidad y miras de VALORANT, Sens Finder y el coach de sensibilidad.
- Interfaz en español y texto de resultados localizado.
- Paquetes de la versión con todos los mapas: el instalador y el zip portátil incluyen la geometría opcional `maps/`.

### Corregido

- La colisión ya no deja al jugador subir o volar a través de techos, ni atravesar paredes.
- El manejo de suelos negativos y suelos que faltan ya no hace caer al jugador fuera del mapa.
- Comprobaciones de seguridad de caída y de movimiento.

### Empaquetado

- `ValTrainer-1.16.0-Setup.exe` y `ValTrainer-1.16.0-Portable.zip` son autocontenidos e incluyen los recursos de los
  mapas.
- `SHA256SUMS.txt` se publica con la versión.
- El repositorio Git excluye los binarios de mapas extraídos de varios gigabytes; ver `README.md` y `NOTICE`.
