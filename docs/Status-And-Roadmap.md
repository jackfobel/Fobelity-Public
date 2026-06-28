# Current Status and Roadmap

This page separates what is currently implemented or represented in the project from areas that are planned, evolving, or intentionally marked for future work.

---

## Current Status

Implemented or actively represented in the project:

- Raspberry Pi edge agent running .NET / ASP.NET Core.
- Raspberry Pi hardware integration with motion sensor, camera, microphone, speakers, and GPIO/breadboard-connected devices.
- Passive infrared motion monitoring.
- Camera snapshot capture.
- Azure Face detection and identification.
- Azure Speech text-to-speech.
- Azure Speech command recognition pipeline.
- Wake-word / command listening pipeline.
- One-shot command mode.
- A2A actuator-agent communication path.
- Support for synchronous responses and longer-running A2A task responses.
- Avatar speech integration through HTTP and optional SignalR hub transport.
- Screen-based avatar feedback with viseme-driven lip synchronization.
- Local edge diagnostic endpoints.
- GPIO output control.
- Workshop mini-split HVAC control path.
- House thermostat / Ecobee-style thermostat integration path.
- Home automation architecture for HVAC and device control through DeviceHub-style services.
- Local health/status endpoints for testing and troubleshooting.
- Structured logging and Speech SDK diagnostics.

---

## Current Limitations / In Progress

Areas that are active, partial, or intentionally not described as complete:

- Smart lighting groups and scenes are planned/evolving rather than presented as complete.
- More complete visual demo assets are still being added to the repository.
- Avatar animation quality can continue improving even though the visible avatar interaction path is working.
- Documentation is being split into deeper component-level files.
- Safety and confirmation policies can be expanded for broader physical actuation scenarios.

---

## Future Enhancements

Planned or evolving areas:

- Persistent memory for long-term user preferences.
- Richer environmental context such as room temperature, occupancy, time of day, and historical behavior.
- Expanded IoT device catalog.
- More complete smart lighting groups and scenes.
- Stronger confirmation and safety policies for physical actions.
- Additional multi-agent workflows for separating research, planning, and actuation.
- Improved local wake-word reliability and latency on the Raspberry Pi.
- Richer WebGL avatar animation and expression control.
- Security hardening for local endpoints.
- CI/CD and infrastructure-as-code for cloud deployment.
- Architecture diagrams and short demo videos.
- Deeper telemetry dashboards for runtime, energy usage, and automation cost.

---

## Roadmap Themes

### Reliability

Improve wake-word stability, audio-device selection, and local service resilience on the Raspberry Pi.

### Safety

Add stronger policy controls for device actuation, especially around HVAC limits, unattended actions, and confirmation workflows.

### Observability

Expand telemetry from simple logs and status endpoints into dashboards that show runtime, device history, energy/cost impact, and automation success/failure patterns.

### Interaction Quality

Improve avatar animation, facial expressions, lip sync quality, and screen-based status overlays.

### Device Coverage

Expand DeviceHub tools for more lighting, HVAC, workshop, and home automation devices while preserving clear service boundaries.
