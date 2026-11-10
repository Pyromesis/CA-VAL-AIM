# Entrenamiento y seguimiento del coach

La biblioteca añade 180 progresiones propias y 50 ejercicios de reacción y queda en 402 ejercicios visibles:

| Sección | Ejercicios |
| --- | ---: |
| Precisión y frenado | 88 |
| Clic sobre blancos móviles | 20 |
| Ajustes finos | 42 |
| Sensibilidad | 25 |
| Seguimiento preciso | 46 |
| Seguimiento reactivo | 20 |
| Control en curvas | 20 |
| Cambio y confirmación | 20 |
| Reacción y adquisición | 64 |
| Minijuegos de reacción sin puntería | 35 |
| Combate, movimiento y utilidad | 22 |

Las progresiones combinan cinco disposiciones con cuatro exigencias; cambian distancias, tamaño, trayectoria, velocidad, ventana de respuesta o contacto necesario según la mecánica. Los ejercicios de agentes siguen en su pantalla.

## Seguimiento y prescripción

Las rondas completadas guardan telemetría, revisión y cronología por segundo. La revisión añade observaciones independientes con tiempo, valor, unidad y un indicador de muestra controlada: exceso, movimiento corto, respuesta, correcciones, anticipaciones, disparos imprecisos, recuperación y retraso de seguimiento, y desviación de ráfaga cuando hay evidencia de esas mecánicas. Una jugada puede tener varias observaciones. El análisis en directo sigue guardando observaciones en segundo plano. Las instrucciones y ayudas del coach se presentan al inicio o en la revisión, sin carteles superpuestos durante el juego.

Los blancos móviles no se usan para clasificar el frenado como exceso o defecto de sensibilidad. En bots se conserva la observación y se identifica si el contexto permite una interpretación controlada. Las pruebas A/B también reciben revisión. El entrenamiento libre archiva cada ciclo completado antes de continuar; no añade puntuaciones competitivas por esos ciclos.

El coach puede crear variantes parametrizadas a partir del problema, su dirección y gravedad. Su clave `personal-*` permite reconstruirlas al cargar una lista guardada. Los problemas tácticos utilizan ejercicios de combate existentes. El plan prescribe repeticiones e instrucciones y muestra el resultado al acabar.

La red neuronal local predice precisión a partir de rondas anteriores; la prescripción de ejercicios usa diagnósticos y reglas. Se exigen al menos 24 rondas comparables, separadas por ejercicio, mapa, arma, dificultad y sensibilidad. Las previsiones solo se muestran si superan la media móvil en datos reservados. No hay garantías de mejora ni generación de código mediante un modelo de lenguaje.

## Referencias de la clasificación

- [Voltaic: categorías de sus benchmarks de temporada 5](https://app.voltaic.gg/blog/announcing-the-voltaic-season-5-aiming-benchmarks-beta-for-kovaaks/).
- [Aimlabs: control del seguimiento](https://aimlabs.com/articles/aimlabs/looking-to-improve-your-tracking-control-check-out-these-playlists/).
- [Aimlabs: seguimiento reactivo](https://aimlabs.com/articles/aimlabs/stop-predicting-and-start-reacting-get-better-at-reactive-tracking/).

Son progresiones propias inspiradas en esas categorías, no reproducciones exactas de sus tareas.

## Validación

Compilación sin errores ni advertencias; comprobación de mínimos por sección y resolución de claves; pruebas de prescripciones reproducibles, observaciones simultáneas y persistencia JSON; pruebas existentes del coach y red neuronal; arranque de los 337 ejercicios que ofrecen vista previa, sin fallos; revisión visual del menú a 1280×720 en español.

## Minijuegos de reacción

Diez mecánicas con cinco niveles cada una: destello visual, pulso sonoro, señal periférica, inhibición por formas, selección de números pares, coincidencia con un número recordado, alternancia de reglas, aparición y disparo, búsqueda entre distractores e intercepción móvil. Las siete primeras no exigen apuntar; las tres últimas separan inicio del movimiento y tiempo hasta acertar. Cada ronda tiene 16 intentos y finaliza incluso con omisiones o errores. Los niveles cambian la ventana disponible, el intervalo aleatorio y, según la mecánica, el tamaño, la distancia, el movimiento, los distractores o el conjunto de números.

Se registran respuestas, anticipaciones, falsas alarmas, clics fuera del objetivo, omisiones y esperas correctas. El coach mantiene una revisión por intento y una cronología por segundo y puede prescribir el mismo juego un nivel más fácil. Una pausa reinicia el intento pendiente para no incluir su duración en el tiempo de respuesta. Los tiempos de elección, memoria, audio y puntería no se comparan con baremos de reacción visual simple ni se presentan como rangos competitivos. Estos juegos ofrecen práctica específica; no garantizan una mejora general de la reacción ni de las partidas.
