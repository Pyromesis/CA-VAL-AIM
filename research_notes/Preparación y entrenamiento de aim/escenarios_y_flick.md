# Escenarios, flick y progresión — notas de investigación

Fecha de revisión: 2026-11-25. Fuentes primarias y revisión directa tras fallos del proveedor de subagentes.

## Qué está probado y qué no

- No hay evidencia que identifique un mejor escenario de aim trainer para VALORANT ni una duración universalmente óptima. Las rutinas reconocidas son material de entrenadores, no ensayos clínicos.
- La práctica física es insustituible por ver vídeos. Revisión sistemática de 18 estudios de reaching/aiming: la observación produce adaptación, pero los efectos directos son mucho menores que con práctica física y faltan aftereffects. Los vídeos son guía, no entrenamiento por sí solos: https://www.nature.com/articles/s41539-024-00271-5
- Variabilidad: Caballero et al. (PeerJ 2024), 65 participantes en una tarea de lanzamiento. Todos mejoraron, pero no hubo diferencias significativas entre grupos de práctica; el grupo de baja variabilidad mostró los mayores tamaños de efecto y la autora advierte muestra pequeña. No se puede afirmar que más variabilidad siempre sea mejor; ajustarla al nivel y a la variabilidad del aprendiz: https://pmc.ncbi.nlm.nih.gov/articles/PMC11212619/
- Especificidad y transferencia: la revisión aplicada de Czyż y Coker recomienda elegir el tipo de variabilidad según la tarea y el aprendiz, no maximizar aleatoriedad: https://journals.sagepub.com/doi/10.1177/17479541231159473
- Distribución: un estudio de tracking visuomotor encontró mayor esfuerzo cognitivo con 60 min seguidos que con cuatro bloques de 15 min; no demuestra una receta universal, pero apoya pausas y evitar maratones: https://pmc.ncbi.nlm.nih.gov/articles/PMC2822735/

## Fuentes de entrenadores y su alcance correcto

- Voltaic VALORANT & CS Routine: 60–100 min, tracking/switching/clicking. Es una rutina completa de entrenamiento, no un calentamiento antes de ranked. Describe finalidad por escenario y permite elegir por nivel: https://voltaic.gg/resources/valorant-routine-kovaaks
- Voltaic Speed Switching: objetivos grandes y cortos para adquisición y confirmación rápida; pide eliminaciones completas, flick inicial y pocas microcorrecciones: https://voltaic.gg/resources/benchmark-focus-speed-switching-kovaaks
- Voltaic Stability Switching: composición y aceleración conservando control, distinto de perseguir velocidad pura: https://voltaic.gg/resources/benchmark-focus-stability-switching-kovaaks
- Voltaic VALORANT Fundamentals: separa tracking, clicking y switching, incluye instrucciones de brazo, ajuste antes de clic y reconocer el blanco antes de moverse: https://voltaic.gg/resources/valorant-fundamentals
- RAMP VALORANT de VT Minigod: 16 min, fases Raise/Activate/Mobilize/Potentiate. La propia página dice explícitamente que es experimental y no está probado científicamente: https://voltaic.gg/resources/valorant-ramp-kovaaks
- Voltaic Aim Lab routine: 12 min de warm-up y recomendación de no exceder 40 min de aim training por rendimientos decrecientes; es recomendación editorial de la guía, no un umbral médico: https://voltaic.gg/resources/valorant-routine

## Selección por fallo medido, no por popularidad

Usar el diagnóstico del coach y una sola consigna por bloque:

| Fallo/objetivo | Escenario ya disponible en CA-VAL-AIM | Uso correcto |
|---|---|---|
| Se pasa del blanco | `flickx:coach-brake-near`, `flickx:coach-brake-wide`, `flickx:tech-brake-ladder` | Frenar antes de aumentar velocidad; estáticos, sin perseguir puntuación |
| Se queda corto | `flickx:tech-wide-arm`, `flickx:sens-braking` | Completar el primer gesto con brazo; no bajar sens para compensar |
| Llegada irregular | `flickx:tech-clean-pace`, `flickx:settle-confirm` | Ritmo repetible, parar y confirmar antes del clic |
| Muchas correcciones | `microx:tech-one-correction`, `microx:coach-micro-confirm` | Una sola corrección deliberada; si hacen falta varias, reiniciar más despacio |
| Temblor/tensión | `flickx:tech-still-hold`, `trackv:tech-smooth-slow` | Agarre mínimo que conserve control; no atribuir todo temblor a sens o agarre |
| Cambios de objetivo | `flickx:tech-sweep-order`, `flickx:tech-confirm-switch` | Confirmar eliminación, ojos al siguiente, cada cambio con su frenada |
| Dispara antes de tiempo | `reflex:tech-wait-commit`, `flickx:settle-confirm` | Esperar contacto real, no anticipar |
| Flick bien en aislamiento, falla en duelo | `flickx:tech-stop-shoot`, `peekduel`, `deathmatch` | Transferir una consigna a movimiento, sonido y arma reales |
| Vertical débil | `flickx:tech-vertical-brake` | Medir vertical por separado, no ocultarlo en una media horizontal |
| Reactivo lento | `reflex:tech-eyes-first`, `popup`, `reaction` | Ojos y decisión antes del gesto; no entrenar clics anticipados |

## Flick y frenada: instrucciones editoriales seguras

1. Mirar el blanco antes de iniciar el gesto.
2. Acelerar con una sola trayectoria y reducir velocidad al llegar; no imponer el mismo porcentaje de recorrido a todas las distancias.
3. Confirmar contacto/parada y después disparar. Si el objetivo exige seguimiento, no confundir clic estático con tracking.
4. Corregir una vez de forma pequeña si hace falta; una cadena de correcciones indica que el primer gesto fue demasiado rápido o mal leído.
5. Subir velocidad o reducir tamaño solo cuando la forma se mantiene. El 85–90 % o “uno de cada cuatro” del contenido anterior son guías editoriales, no umbrales científicos.
6. Terminar con transferencia a una tarea menos controlada. No usar deathmatch como prueba A/B ni como evidencia de aprendizaje permanente.

## Estructuras editoriales de 10/20/30 minutos

No son duraciones óptimas ni planes de Voltaic. Son diseños propios usando drills existentes:

- **10 min, una prioridad:** 1 min medición estática → 4 min técnica aislada → 2 min integración/micro → 2 min repetición equivalente → 1 min revisión. Elegir solo una de las filas anteriores.
- **20 min, flick a duelo:** 2 min baseline → 5 min frenada según fallo → 3 min micro → 3 min switching → 4 min `peekduel`/stop-shoot → 2 min baseline equivalente → 1 min revisión. Aumentar demanda solo si se mantiene la consigna.
- **30 min, sesión completa:** 3 min activación suave → 6 min prioridad medida → 4 min segunda habilidad → 4 min switching/tracking → 5 min tarea dinámica → 5 min deathmatch con una consigna → 3 min repetición y registro. No es calentamiento pre-ranked y debe poder dividirse.

Métricas: overshoot/undershoot por separado, primer disparo, microcorrecciones, jitter con tiempo sobre blanco y comparación equivalente. Parar por dolor, hormigueo, pérdida de fuerza, dos bloques con deterioro claro de ejecución o imposibilidad de mantener atención. No entrenar cansado para “sumar minutos”.

## Vídeos/guías ya acreditados

- Aimlabs, *Better Flicking Guaranteed! | Aim Guide*: https://www.youtube.com/watch?v=xAE2uPM59FY
- Struth Gaming, *How to Flick Shot (complete guide)*: https://www.youtube.com/watch?v=Pvu9QcGQl3A
- Zapper, *Aim Guide for PERFECT Micro Corrections (Ep. 1)*: https://www.youtube.com/watch?v=s0BuZ4sQpsU
- Voltaic, *Voltaic Speed Target Switching Training*: https://www.youtube.com/watch?v=y-xYxbYl1d0
- Horcus, *Cómo MEJORAR AIM en VALORANT 2026! Rutina y Guía completa*: https://www.youtube.com/watch?v=VXqO4q0h93I
