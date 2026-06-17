Practico Evaluativo programacion Joa Luna Cátedra de Producción de Juegos UPC Cohorte 2025

Juego multijugador de recolección de esferas luminosas

INDICE:
1)lógica de networking (Netcode for GameObjects)
2)configuración de Unity que cada uno necesita para funcionar.
3)Resumen de los scripts del proyecto


---

Resumen general

Juego 2 jugadores en red local (Host + Client) hecho con **Unity Netcode for GameObjects**. Cada jugador recorre el mapa, recolecta esferas de luz (`Collectible`) y las entrega en una zona central (`DeliveryZone`) para sumar puntos. Al agotarse el tiempo (`matchDuration`), gana quien tenga más puntaje; si hay igualdad, es empate. Toda la lógica de puntaje/spawn/tiempo corre en el servidor (server-authoritative); el movimiento de cada jugador lo maneja su propio cliente (owner-authoritative).

---

1) Lógica de networking (Netcode for GameObjects)

**Modelo de conexión:** un jugador es Host (servidor + cliente local en el mismo proceso) y el otro es Client puro, conectado por IP/puerto (`UnityTransport`, puerto 7777 por defecto, configurable en `MainMenuManager`).

**Autoridad de servidor (server-authoritative) vs. autoridad del owner:**
- Toda la lógica de juego que importa para que no haya trampas o desincronización (recolectar, entregar, puntaje, spawn de ítems, countdown, fall-respawn) está protegida con `if (!IsServer) return;` — solo el servidor decide, los clientes solo *ven* el resultado.
- El movimiento del jugador es la excepción: cada cliente mueve su propio personaje (`IsOwner`) y el `NetworkTransform` Owner Authoritative lo replica, para que se sienta responsivo sin esperar al servidor.

**Sincronización de estado — `NetworkVariable<T>`:** `score`, `hasObject`, `playerIndex` (en `PlayerData`) y `timeRemaining`, `gameOver` (en `GameManager`). El servidor escribe `.Value`, Netcode las empuja automáticamente a todos los clientes; cualquier script (UIManager, CameraFollow, etc.) puede simplemente leerlas sin pedir nada por RPC.

**RPCs usados:**
- `ServerRpc` → `RequestRestartServerRpc` (cliente pide, solo el servidor ejecuta el `LoadScene`).
- `ClientRpc` → `PlayDeliverySoundClientRpc`, `PlayPickupSoundClientRpc` (servidor avisa a todos para reproducir un sonido) y `RepositionPlayerClientRpc` (servidor avisa, pero **dirigido a un solo cliente** vía `ClientRpcParams.Send.TargetClientIds`, para no mover a todo el mundo cuando reposiciona a uno solo).

**Ciclo de vida en red:** `OnNetworkSpawn`/`OnNetworkDespawn` reemplazan a `Start`/`OnDestroy` en todo lo que depende de saber si el objeto ya es servidor/cliente/owner (esa info no está disponible todavía en `Awake`).

**Carga de escenas en red:** siempre se usa `NetworkManager.Singleton.SceneManager.LoadScene(...)` (no el `SceneManager.LoadScene` normal de Unity) para que el Host cargue la escena sincronizadamente en todos los clientes conectados — se usa al arrancar partida (`OnHostClick`) y al reiniciar (`RequestRestartServerRpc`).

---

2. Configuración de Unity (checklist obligatorio)

Esto es lo que tiene que estar armado a mano en el Editor para que el código de arriba funcione. Si algo de esto falta, el código compila igual pero falla en runtime (o solo en build, como vimos con los shaders).

Escena `MainMenu`
- GameObject vacío con `MainMenuManager.cs`.
- GameObject con el componente **`NetworkManager`** + **`UnityTransport`**. Tiene que existir *antes* de que se llame `StartHost`/`StartClient`, así que va en esta escena (el `NetworkManager` se persiste solo entre escenas, no hace falta duplicarlo en `Game`).

NetworkManager (Inspector)
- **Player Prefab**: asignar el prefab del jugador, para que Netcode lo spawnee automáticamente apenas un cliente se conecta.
- **NetworkPrefabs (lista)**: agregar también el prefab del `Collectible`, porque `GameManager` lo instancia y spawnea en runtime (`Instantiate` + `NetworkObject.Spawn()`) — si no está en esta lista, los clientes no van a poder verlo.

Escena `Game` — objetos sueltos
- GameObject vacío con **NetworkObject** + `GameManager.cs`. Asignar en el Inspector: `collectiblePrefab`, `spawnPoints[]` (Transforms vacíos repartidos en el mapa), `playerSpawnPoints[]` (uno por jugador), y los `AudioSource` si se usan.
- Objeto de la zona de entrega: Collider marcado **Is Trigger**, con `DeliveryZone.cs`. No necesita NetworkObject.
- Main Camera con `CameraFollow.cs`.
- GameObject vacío con `UIManager.cs`.
- El `EventSystem` lo crea el código solo si no existe; no hace falta armarlo a mano.

Prefab del Player
- **NetworkObject**.
- **NetworkTransform**, configurado como **Owner Authoritative** (clave para que `PlayerController` funcione como está escrito).
- **Rigidbody** (lo usa `PlayerController.Awake()` para congelar la rotación) + Collider.
- Scripts: `PlayerController.cs` + `PlayerData.cs`.
- El Renderer del modelo debe usar un **Material propio editable** (no el "Lit" default de URP, que es de solo lectura) con **Emission activado y en un color distinto de negro** — necesario para que el color/brillo sobreviva en builds standalone (ver punto 4.6).

Prefab del Collectible
- **NetworkObject** + Collider marcado **Is Trigger**.
- Script `Collectible.cs`.
- Mismo requisito de material editable con Emission activado.

Materiales y shaders (imprescindible para que ande igual en build que en el Editor)
- Materiales de Player / Collectible / DeliveryZone: propios (no los default de los paquetes), con **Emission** tildado y un color no-negro — si no, el color/brillo desaparece al exportar (el Editor compila todos los shaders, el build solo los que detecta usados).
- **Project Settings → Graphics → Always Included Shaders**: agregar `Skybox/Procedural` y `Particles/Standard Unlit` — si no, el cielo nocturno y el campo de estrellas no aparecen en builds standalone (`Shader.Find` devuelve null si nada los referencia).

Build Settings y red
- **File → Build Settings → Scenes in Build**: deben estar agregadas tanto `MainMenu` como `Game`, con esos nombres exactos (los scripts cargan por nombre de string).
- Puerto 7777 (UDP) por defecto: si se prueba entre dos PCs distintas en la misma LAN, el Firewall de Windows tiene que permitir el ejecutable o ese puerto, o el cliente no va a poder conectarse al host.
- Cada vez que se cambia un script, un material o una configuración de Graphics, hay que **reexportar las dos builds** — un .exe ya generado no se actualiza solo.

3)Resumen de funciones por script

 MainMenuManager.cs
*Va en un GameObject vacío de la escena `MainMenu`.*

- `BuildUI()` / `BuildMainPanel()` / `BuildJoinPanel()` / `BuildInstructionsPanel()`: construyen toda la UI del menú por código (sin Canvas armado a mano): panel principal (Host/Unirse/Salir), panel de IP, panel de instrucciones.
- `OnHostClick()`: configura el `UnityTransport` para escuchar en `0.0.0.0:port`, llama `NetworkManager.Singleton.StartHost()` y, si arrancó bien, carga la escena `Game` para todos vía `NetworkManager.Singleton.SceneManager.LoadScene(...)`.
- `OnJoinClick()`: toma la IP escrita (o la default), configura el transporte con esa IP y puerto, y llama `StartClient()`.
- `OnQuitClick()`, `SwitchPanel()`: navegación simple de paneles, sin red.
- `CreatePanel/CreateText/CreateButton/CreateInputField/GetRoundedSprite/IsInsideRoundedRect`: helpers genéricos de UI, sin lógica de juego.

DeliveryZone.cs
*Va en el objeto de la zona central (Collider en modo Trigger, sin NetworkObject propio).*

- `Start()` → `SetupVisual()`: pinta el material en amarillo emisivo y agrega una luz puntual. Corre igual en host y en cliente (es solo estética).
- `OnTriggerEnter()`: **solo se ejecuta si `IsServer`**. Busca el `PlayerData` del objeto que entró; si tenía un objeto cargado (`hasObject.Value == true`), lo descarga, suma 1 punto (`score.Value++`) y dispara el sonido.
- `PlayDeliverySoundClientRpc()`: RPC servidor→todos los clientes, solo reproduce el audio de entrega.

Collectible.cs
*Va en el prefab del objeto a recolectar (Collider en modo Trigger).*

- `OnNetworkSpawn()`: si es servidor, incrementa `GameManager.Instance.collectibleCount` (para limitar cuántos hay vivos a la vez); además llama `SetupVisual()` en todos los clientes.
- `OnNetworkDespawn()`: el servidor decrementa ese contador.
- `SetupVisual()`: pinta el material magenta emisivo y agrega luz puntual (estético, en todos los clientes).
- `OnTriggerEnter()`: **solo servidor**. Si el jugador que tocó el ítem no estaba cargando nada, le marca `hasObject.Value = true`, dispara el sonido y hace `NetworkObject.Despawn()` (esto destruye el objeto en todos los clientes a la vez).
- `PlayPickupSoundClientRpc()`: RPC que reproduce el sonido desde un objeto temporal aparte, para que no se corte al despawnearse el ítem.

GameManager.cs
*Va en un objeto vacío con NetworkObject, en la escena `Game`.*

- `Awake()`: asigna el singleton `Instance` y llama `SetupNightSky()` (cielo + estrellas), local en todos los clientes y host.
- `SetupNightSky()` / `CreateStarField()`: crean el skybox procedural oscuro y el campo de estrellas con `ParticleSystem`, todo por código (sin assets).
- `OnNetworkSpawn()`: arranca la música; **si no es servidor, no hace nada más**. Si es servidor: inicializa `timeRemaining`, arranca las corrutinas `CountdownRoutine`, `SpawnRoutine`, `FallRespawnRoutine`, reubica jugadores ya conectados (`ResetAllPlayers`) y se suscribe a `OnClientConnectedCallback`.
- `RequestRestartServerRpc()`: **ServerRpc** (cualquier cliente puede llamarla, `RequireOwnership = false`); solo el servidor recarga la escena `Game`.
- `AssignSpawn()` / `ResetAllPlayers()` / `OnClientConnected()` / `SpawnPlayerDelayed()`: lógica server-only de asignar punto de spawn e índice de color a cada jugador que se conecta (o al reiniciar partida), y notificarle su posición vía `RepositionPlayerClientRpc` dirigido solo a ese cliente.
- `FallRespawnRoutine()`: corrutina server-only que cada `respawnCheckInterval` revisa si algún jugador cayó del mapa y lo reposiciona.
- `CountdownRoutine()`: server-only, descuenta `timeRemaining` y al llegar a 0 pone `gameOver.Value = true`.
- `SpawnRoutine()` / `SpawnCollectible()` / `TryGetFreeSpawnPosition()` / `IsPositionOccupied()`: server-only, generan collectibles periódicamente en spawn points libres (chequeando que no se solapen con otro vía `Physics.OverlapSphere`).
- `Update()` / `PlayLocalResultSound()`: en **todos los clientes**, cuando `gameOver.Value` se vuelve true, calcula localmente quién ganó (comparando `score.Value` de todos) y reproduce victoria/derrota — esta parte sí tiene en cuenta los empates (no reproduce "victoria" a nadie si hay más de un puntaje máximo).

UIManager.cs
*Va en un GameObject vacío de la escena `Game` (HUD).*

- `BuildUI()`: construye por código el panel de tiempo, el panel de puntajes y el panel de Game Over (oculto al inicio).
- `Update()`: lee `GameManager.Instance.timeRemaining.Value` y `gameOver.Value` (NetworkVariables, ya replicadas automáticamente) y actualiza el HUD; cuando `gameOver` pasa a true, llama `ShowGameOver()` una sola vez.
- `UpdateScores()` / `GetConnectedPlayers()`: leen el `PlayerData` de cada `ConnectedClientsList` y refrescan los textos de puntaje.
- `ShowGameOver()`: determina el puntaje más alto y cuántos jugadores lo comparten; si hay empate muestra el texto de empate, si no, el nombre del ganador.
- `OnRestartClick()` / `OnMainMenuClick()`: el primero llama al `RequestRestartServerRpc` del GameManager; el segundo apaga la red (`NetworkManager.Singleton.Shutdown()`) y carga `MainMenu` localmente.
- `Hex/MakePanel/MakeText/MakeButton/GetRoundedSprite/IsInsideRoundedRect`: helpers de UI, sin lógica de red.

PlayerData.cs
*Va en el prefab del jugador, junto a PlayerController.*

- Declara las `NetworkVariable<T>`: `score`, `hasObject`, `playerIndex`. El servidor es el único que las escribe; Netcode las replica solo a todos los clientes automáticamente (no hace falta ningún RPC para leerlas).
- `OnNetworkSpawn()`: se suscribe a los callbacks `OnValueChanged` de `hasObject` y `playerIndex`, y llama `SetupPlayerVisual()` — esto corre en **todos los clientes**, cada uno reacciona localmente a los cambios de estado.
- `OnPlayerIndexChanged()` / `ApplyColor()`: recolorean el modelo y la luz según si es jugador 0 o 1.
- `OnHasObjectChanged()` / `SpawnCarriedSphere()` / `DestroyCarriedSphere()`: muestran/ocultan la esferita magenta sobre la cabeza cuando el jugador agarra o entrega un ítem.

PlayerController.cs
*Va en el prefab del jugador, junto a NetworkObject + NetworkTransform (Owner Authoritative).*

- `Awake()`: congela la rotación del Rigidbody para que no se vuelque.
- `Update()`: **`if (!IsOwner) return;`** — solo el cliente dueño de este jugador lee su propio input y mueve su transform; el `NetworkTransform` (configurado como Owner Authoritative) se encarga de replicar esa posición a los demás. Esto es lo opuesto al resto del juego: aquí la autoridad es del cliente, no del servidor.

CameraFollow.cs
*Va en la Main Camera de la escena Game.*

- `LateUpdate()`: busca `NetworkManager.Singleton.LocalClient.PlayerObject` para saber a quién seguir (cada instancia de la cámara —host o client— sigue únicamente a su propio jugador local). Maneja zoom con la rueda del mouse y rotación orbital con el mouse X. No tiene ninguna llamada de red propia, solo lee el jugador local.

---


