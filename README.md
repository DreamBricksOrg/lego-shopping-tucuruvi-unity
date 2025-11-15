# LEGO Shopping Tucuruvi Totem

## Overview
This Unity project powers an interactive photo booth used in the LEGO Shopping Tucuruvi store. Guests are guided through instructional, capture, validation, and thank-you screens that are orchestrated by `UIManager`. The app controls a webcam feed, captures still images, submits them to an external ComfyUI-powered service, then displays the AI-generated result alongside a QR code that links to the image. Maintenance and logging utilities keep the kiosk reliable in a retail environment.

## Key Content

### Scenes
- **Assets/Scenes/SampleScene.unity** – Root scene that wires up the kiosk UI canvas, state machines, and maintenance overlay used throughout the experience.

### Gameplay & Flow Scripts (`Assets/Scripts`)
- **UIManager.cs** – Central controller that fades between named screens, enforces a single active canvas, and records telemetry for each transition. 【F:Assets/Scripts/UIManager.cs†L1-L215】
- **INSTRUCOES.cs** – Drives the instruction screen timer and call-to-action pulsing, logging when the user takes too long. 【F:Assets/Scripts/INSTRUCOES.cs†L1-L82】
- **CAPTURA.cs** – Handles countdown, webcam flash effect, and grabbing a still frame from the webcam render texture before handing control to validation. 【F:Assets/Scripts/CAPTURA.cs†L1-L90】
- **VALIDACAO.cs** – Uploads the captured photo, polls job status, manages accept/reject buttons, and triggers maintenance when remote errors are persistent. 【F:Assets/Scripts/VALIDACAO.cs†L1-L210】
- **AGRADECIMENTO.cs** – Final screen that fetches the generated artwork, shows a QR code for the guest, and resets back to the start after a timer. 【F:Assets/Scripts/AGRADECIMENTO.cs†L1-L127】
- **MANUTENCAO.cs** – Enables a maintenance overlay, monitors kiosk health via heartbeat requests, and coordinates with `UIManager` to disable customer-facing screens when intervention is required. 【F:Assets/Scripts/MANUTENCAO.cs†L1-L146】
- **WebcamToRenderTexture.cs** – Manages the physical camera device, pushes frames into a `RenderTexture`, and applies rotation/flip metadata or config-driven overrides. 【F:Assets/Scripts/WebcamToRenderTexture.cs†L1-L116】

### Logging & Utilities (`Assets/Scripts/logUtil`)
- **LogUtil.cs** and companions – Load logging configuration from JSON, persist kiosk events, and upload telemetry to the configured endpoints. 【F:Assets/Scripts/logUtil/LogUtil.cs†L1-L200】

### Configuration & Assets
- **Assets/StreamingAssets/config.ini** – Runtime-editable configuration for timers, network endpoints, camera settings, and maintenance toggles. 【F:Assets/StreamingAssets/config.ini†L1-L29】
- **Assets/StreamingAssets/datalog.json** – Initial seed file used by the logging subsystem for local persistence before uploads.
- **Assets/Resources/WebcamRotateBlit.shader** (referenced via `WebcamToRenderTexture`) – GPU shader used to rotate and flip webcam frames when `useShaderRotation` is enabled.

## Working With The Project
1. Open `SampleScene.unity` in the Unity Editor to access the kiosk canvas layout and link script references.
2. Adjust kiosk behavior (timers, network URLs, camera orientation) through `Assets/StreamingAssets/config.ini` without rebuilding the player.
3. Run the scene in Play Mode; `WebcamToRenderTexture` will initialise the configured camera, `CAPTURA` will start the countdown, and `UIManager` will drive screen transitions through the full guest journey.
4. Monitor logs in `StreamingAssets/datalog.json`; the `logUtil` scripts batch them to the remote logging service defined in the config.
